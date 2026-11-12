using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>Soru adımında bir seçenek: tıklanınca <see cref="Target"/> kimlikli adıma gidilir.</summary>
    public sealed class TourChoice
    {
        public string Label;
        public string Target;
    }

    /// <summary>Uygulama turunun bir adımı (G36).</summary>
    public sealed class TourStep
    {
        /// <summary>Atlama hedefi olarak kullanılan kimlik (isteğe bağlı).</summary>
        public string Id;
        public string Title;
        /// <summary>Açıklama (adım gösterilirken hesaplanır).</summary>
        public Func<string> Body;
        /// <summary>Vurgulanacak alan (ekran koordinatı); null: alan yok, balon ortada.</summary>
        public Func<Rectangle?> Target;
        /// <summary>Adıma girerken (ör. ilgili sekmeye geç).</summary>
        public Action Enter;
        /// <summary>İsteğe bağlı küçük çizim (durum renkleri, renklendirme örneği, kısayollar…).</summary>
        public Action<Graphics, Rectangle> Illustration;
        public int IllustrationHeight;
        /// <summary>Adımın gösterildiği pencere (null: ana pencere). Pencere yoksa adım atlanır.</summary>
        public Func<Form> Host;
        /// <summary>
        /// Doluysa adım kullanıcının bir işlemini bekler (ör. "Kart Ekle'ye basın"): koşul yanlıştan doğruya dönünce tur
        /// kendiliğinden ilerler. Adıma girildiğinde koşul zaten doğruysa normal "İleri" gösterilir.
        /// </summary>
        public Func<bool> WaitFor;
        /// <summary>Bekleme adımında kullanıcıya verilen görev ("Şimdi siz: …").</summary>
        public string Task;
        /// <summary>false: adım atlanır (ör. bağlı kart yok).</summary>
        public Func<bool> Available;
        /// <summary>Doluysa İleri yerine seçenek düğmeleri gösterilir.</summary>
        public IList<TourChoice> Choices;
        /// <summary>İleri bu kimlikli adıma gider (dallanmanın sonunda ortak adıma dönmek için).</summary>
        public string NextId;
    }

    /// <summary>
    /// Uygulama turu (G36): anlatılan pencerenin üzerine yarı saydam bir katman koyar, anlatılan bölümü bu katmanda açık
    /// bırakıp vurgular ve yanına açıklama balonu yerleştirir. Adımlar kullanıcıdan bir işlem bekleyebilir (Kart Ekle'ye
    /// basmak, karta bağlanmak, komut yazmak…); işlem yapılınca tur kendiliğinden ilerler, "Bu adımı geç" ile atlanabilir.
    /// Açılan pencerelerde (Kart Ekle, boot ayarları) katman ve balon o pencereye taşınır. Esc turu kapatır.
    /// </summary>
    public sealed class TourController : IDisposable
    {
        private readonly Form _main;
        private readonly IList<TourStep> _steps;
        private readonly Timer _follow = new Timer { Interval = 250 };
        private readonly Stack<int> _history = new Stack<int>();
        private Form _host;
        private TourOverlay _overlay;
        private TourBalloon _balloon;
        private int _index = -1;
        private bool _waitPrev;
        private bool _dontShow;
        private Rectangle? _shownHole;
        private Rectangle _shownHost;
        private Size _shownBalloon;
        private bool _closed;

        /// <summary>Tur kapandı: (sonuna kadar izlendi mi, "bir daha gösterme" işaretli mi).</summary>
        public event Action<bool, bool> Closed;

        public TourController(Form main, IList<TourStep> steps)
        {
            _main = main;
            _steps = steps;
            _follow.Tick += (s, e) => Tick();
            _main.FormClosing += MainClosing;
        }

        public bool IsOpen { get { return !_closed && _index >= 0; } }

        public void Start()
        {
            _follow.Start();
            GoTo(0, +1, false);
        }

        // ---------- Gezinme ----------

        private Form HostOf(TourStep step)
        {
            if (step.Host == null) return _main;
            try { Form f = step.Host(); return f != null && !f.IsDisposed && f.Visible ? f : null; }
            catch { return null; }
        }

        private bool IsAvailable(TourStep step)
        {
            if (HostOf(step) == null) return false;
            if (step.Available == null) return true;
            try { return step.Available(); } catch { return false; }
        }

        private static bool Eval(Func<bool> f)
        {
            try { return f != null && f(); } catch { return false; }
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _steps.Count; i++)
                if (_steps[i].Id == id) return i;
            return -1;
        }

        /// <summary>Adıma gider; kullanılamayan adımlar <paramref name="dir"/> yönünde atlanır.</summary>
        private void GoTo(int target, int dir, bool record)
        {
            if (_closed) return;
            int i = target;
            while (i >= 0 && i < _steps.Count && !IsAvailable(_steps[i])) i += dir;
            if (i >= _steps.Count) { Finish(true); return; }
            if (i < 0) return;
            if (record && _index >= 0) _history.Push(_index);
            _index = i;
            TourStep step = _steps[i];
            try { if (step.Enter != null) step.Enter(); }
            catch (Exception ex) { ErrorText.LogTechnical("tur adımı", ex); }
            Form host = HostOf(step) ?? _main;
            EnsureHost(host);
            _waitPrev = step.WaitFor != null && Eval(step.WaitFor);
            string body = "";
            try { body = step.Body != null ? step.Body() : ""; } catch { }
            bool waiting = step.WaitFor != null && !_waitPrev;
            _balloon.SetStep(i, _steps.Count, step.Title, body, waiting ? step.Task : null, step.Illustration, step.IllustrationHeight,
                             waiting ? "Bu adımı geç" : (i == _steps.Count - 1 ? "Bitir" : "İleri"), step.Choices,
                             _history.Count > 0, waiting);
            // Sekme değişince yerleşim bir sonraki iletide oturur: konum o zaman hesaplanır
            if (host.IsHandleCreated) host.BeginInvoke(new Action(() => Reposition(true)));
        }

        private void Next()
        {
            if (_closed || _index < 0) return;
            string next = _steps[_index].NextId;
            int target = next != null ? IndexOf(next) : -1;
            GoTo(target >= 0 ? target : _index + 1, +1, true);
        }

        private void Back()
        {
            while (_history.Count > 0)
            {
                int h = _history.Pop();
                if (IsAvailable(_steps[h])) { GoTo(h, -1, false); return; }
            }
        }

        private void Choose(TourChoice choice)
        {
            int target = choice != null ? IndexOf(choice.Target) : -1;
            if (target < 0) Next();
            else GoTo(target, +1, true);
        }

        private void Tick()
        {
            if (_closed || _index < 0) return;
            TourStep step = _steps[_index];
            if (step.WaitFor != null)
            {
                bool now = Eval(step.WaitFor);
                if (now && !_waitPrev) { Next(); return; }
                _waitPrev = now;
            }
            // Adımın penceresi kapandı (ör. Kart Ekle iptal edildi): sıradaki uygun adıma geç
            Form host = HostOf(step);
            if (host == null || host != _host || _balloon == null || _balloon.IsDisposed || _overlay == null || _overlay.IsDisposed)
            {
                if (host == null) { GoTo(_index + 1, +1, true); return; }
                GoTo(_index, +1, false);
                return;
            }
            Reposition(false);
        }

        // ---------- Pencereler ----------

        /// <summary>
        /// Katmanı ve balonu adımın penceresine bağlar. Pencere değişince ikisi de yeniden oluşturulur: kalıcı (modal)
        /// bir pencere açıkken önceden açılmış pencereler devre dışı kalır, sonradan açılanlar kullanılabilir.
        /// </summary>
        private void EnsureHost(Form host)
        {
            if (host == _host && _overlay != null && !_overlay.IsDisposed && _balloon != null && !_balloon.IsDisposed) return;
            CloseUi();
            _host = host;
            _host.Move += HostChanged;
            _host.Resize += HostChanged;
            _overlay = new TourOverlay();
            _balloon = new TourBalloon { DontShowAgain = _dontShow };
            _balloon.NextClicked += Next;
            _balloon.BackClicked += Back;
            _balloon.SkipClicked += () => Finish(false);
            _balloon.ChoiceClicked += Choose;
            _balloon.DontShowChanged += v => _dontShow = v;
            _shownHole = null;
            _shownHost = Rectangle.Empty;
            _shownBalloon = Size.Empty;
            _overlay.Show(_host);
            _balloon.Show(_overlay);
        }

        private void CloseUi()
        {
            if (_host != null)
            {
                _host.Move -= HostChanged;
                _host.Resize -= HostChanged;
            }
            if (_balloon != null) { try { _balloon.Close(); _balloon.Dispose(); } catch { } _balloon = null; }
            if (_overlay != null) { try { _overlay.Close(); _overlay.Dispose(); } catch { } _overlay = null; }
            _host = null;
        }

        private void HostChanged(object sender, EventArgs e) { Reposition(true); }

        private void MainClosing(object sender, FormClosingEventArgs e) { Finish(false); }

        /// <summary>Katmanı pencereye, deliği hedefe, balonu deliğin (ya da açılan pencerenin) yanına yerleştirir.</summary>
        private void Reposition(bool force)
        {
            if (_closed || _index < 0 || _host == null || _host.IsDisposed) return;
            if (_overlay == null || _overlay.IsDisposed || _balloon == null || _balloon.IsDisposed) return;
            if (_host.WindowState == FormWindowState.Minimized) return;
            Rectangle client = _host.RectangleToScreen(_host.ClientRectangle);
            Rectangle? hole = null;
            try { hole = _steps[_index].Target != null ? _steps[_index].Target() : null; } catch { hole = null; }
            if (hole.HasValue)
            {
                Rectangle h = Rectangle.Inflate(hole.Value, 6, 6);
                h.Intersect(client);
                hole = h.Width > 8 && h.Height > 8 ? h : (Rectangle?)null;
            }
            if (!force && hole == _shownHole && client == _shownHost && _balloon.Size == _shownBalloon) return;
            _shownHole = hole;
            _shownHost = client;
            _shownBalloon = _balloon.Size;

            _overlay.Bounds = client;
            _overlay.SetHole(hole.HasValue ? new Rectangle(hole.Value.X - client.X, hole.Value.Y - client.Y, hole.Value.Width, hole.Value.Height) : (Rectangle?)null);
            if (_host == _main)
                _balloon.Location = PlaceBalloon(client, hole, _balloon.Size);
            else
            {
                // Açılan pencere: balon pencerenin yanında (ekranın çalışma alanı içinde), anlatılan alanın hizasında
                Rectangle area = Screen.FromControl(_host).WorkingArea;
                Rectangle b = _host.Bounds;
                Rectangle anchor = hole.HasValue ? new Rectangle(b.X, hole.Value.Y, b.Width, hole.Value.Height) : b;
                _balloon.Location = PlaceBalloon(area, anchor, _balloon.Size);
            }
        }

        private static Point PlaceBalloon(Rectangle area, Rectangle? hole, Size size)
        {
            const int gap = 16, margin = 12;
            Rectangle inner = Rectangle.Inflate(area, -margin, -margin);
            if (!hole.HasValue)
                return new Point(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2);
            Rectangle h = hole.Value;
            Func<int, int, int, int> clamp = (v, lo, hi) => Math.Max(lo, Math.Min(v, Math.Max(lo, hi)));
            var candidates = new List<Point>
            {
                new Point(h.Right + gap, clamp(h.Top, inner.Top, inner.Bottom - size.Height)),                       // sağ
                new Point(h.Left - gap - size.Width, clamp(h.Top, inner.Top, inner.Bottom - size.Height)),          // sol
                new Point(clamp(h.Left, inner.Left, inner.Right - size.Width), h.Bottom + gap),                       // alt
                new Point(clamp(h.Left, inner.Left, inner.Right - size.Width), h.Top - gap - size.Height),           // üst
            };
            foreach (Point p in candidates)
                if (inner.Contains(new Rectangle(p, size))) return p;
            // Hedef çok büyük (ör. kart ızgarası): balon hedefin içinde, sağ altta
            return new Point(clamp(h.Right - size.Width - 24, inner.Left, inner.Right - size.Width),
                             clamp(h.Bottom - size.Height - 24, inner.Top, inner.Bottom - size.Height));
        }

        private void Finish(bool completed)
        {
            if (_closed) return;
            _closed = true;
            _follow.Stop();
            _main.FormClosing -= MainClosing;
            CloseUi();
            var h = Closed;
            if (h != null) h(completed, _dontShow);
        }

        public void Dispose()
        {
            Finish(false);
            _follow.Dispose();
        }
    }

    /// <summary>Yarı saydam koyu katman; vurgulanan alan katmanda boşluktur (altındaki denetim görünür ve kullanılabilir).</summary>
    internal sealed class TourOverlay : Form
    {
        private Rectangle? _hole;

        public TourOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.62;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;   // WS_EX_TOOLWINDOW: Alt+Tab'da görünmesin
                return cp;
            }
        }

        public void SetHole(Rectangle? hole)
        {
            _hole = hole;
            var region = new Region(new Rectangle(Point.Empty, Size));
            if (hole.HasValue)
                using (GraphicsPath p = RoundRect(hole.Value, 12)) region.Exclude(p);
            Region old = Region;
            Region = region;
            if (old != null) old.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            if (!_hole.HasValue) return;
            // Vurgu çerçevesi: deliğin hemen dışında
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Accent, 4))
            using (GraphicsPath p = RoundRect(Rectangle.Inflate(_hole.Value, 2, 2), 14))
                e.Graphics.DrawPath(pen, p);
        }

        internal static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Turun açıklama balonu.</summary>
    internal sealed class TourBalloon : Form
    {
        private const int BalloonWidth = 440;
        private readonly Label _progress = Theme.MakeLabel("", Theme.SectionHeader, Theme.Accent);
        private readonly Label _title = Theme.MakeLabel("", new Font("Segoe UI Semibold", 13f), Theme.Text);
        private readonly Label _body = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Label _task = Theme.MakeLabel("", new Font("Segoe UI Semibold", 10f), Theme.Accent);
        private readonly Panel _art = new Panel();
        private readonly CheckBox _dontShow = new CheckBox { Text = "Bir daha gösterme", AutoSize = true };
        private readonly Button _skip = Theme.MakeButton("Turu kapat", Theme.ButtonKind.Ghost);
        private readonly Button _back = Theme.MakeButton("Geri", Theme.ButtonKind.Secondary);
        private readonly Button _next = Theme.MakeButton("İleri", Theme.ButtonKind.Primary);
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Action<Graphics, Rectangle> _illustration;
        private bool _waiting;

        public event Action NextClicked;
        public event Action BackClicked;
        public event Action SkipClicked;
        public event Action<TourChoice> ChoiceClicked;
        public event Action<bool> DontShowChanged;

        public TourBalloon()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.SurfaceHigh;
            KeyPreview = true;
            Font = Theme.Ui;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            foreach (Label l in new[] { _title, _body, _task })
            {
                l.AutoSize = true;
                l.MaximumSize = new Size(BalloonWidth - 44, 0);
            }
            _task.Padding = new Padding(10, 7, 10, 7);
            _task.BackColor = Theme.Input;
            _art.BackColor = Theme.Input;
            _art.Paint += (s, e) =>
            {
                if (_illustration == null) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                try { _illustration(e.Graphics, _art.ClientRectangle); } catch { }
            };
            _dontShow.ForeColor = Theme.TextMuted;
            _dontShow.BackColor = Color.Transparent;
            _dontShow.Font = Theme.UiSmall;
            _dontShow.CheckedChanged += (s, e) => { var h = DontShowChanged; if (h != null) h(_dontShow.Checked); };
            _skip.Width = 96; _back.Width = 76; _next.Width = 112;
            Controls.AddRange(new Control[] { _progress, _title, _body, _task, _art, _dontShow, _skip, _back, _next });

            _next.Click += (s, e) => Raise(NextClicked);
            _back.Click += (s, e) => Raise(BackClicked);
            _skip.Click += (s, e) => Raise(SkipClicked);
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Raise(SkipClicked); }
                else if (e.KeyCode == Keys.Left && _back.Enabled) { e.Handled = true; Raise(BackClicked); }
                else if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) && !_waiting && _next.Visible)
                {
                    e.Handled = true; e.SuppressKeyPress = true; Raise(NextClicked);
                }
            };
        }

        public bool DontShowAgain
        {
            get { return _dontShow.Checked; }
            set { _dontShow.Checked = value; }
        }

        // Balon odak almadan açılır: açılan penceredeki alanlar (ör. Kart Ekle) odağı kaybetmesin
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;          // WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        public void SetStep(int index, int count, string title, string body, string task, Action<Graphics, Rectangle> art, int artHeight,
                            string nextText, IList<TourChoice> choices, bool canBack, bool waiting)
        {
            SuspendLayout();
            _waiting = waiting;
            _progress.Text = "UYGULAMA TURU  ·  " + (index + 1) + " / " + count;
            _title.Text = title ?? "";
            _body.Text = body ?? "";
            _illustration = art;
            int x = 22, y = 20;
            _progress.Location = new Point(x, y);
            y += _progress.PreferredHeight + 6;
            _title.Location = new Point(x, y);
            y += _title.PreferredHeight + 8;
            _body.Visible = _body.Text.Length > 0;
            if (_body.Visible)
            {
                _body.Location = new Point(x, y);
                y += _body.PreferredHeight + 12;
            }
            _task.Visible = !string.IsNullOrEmpty(task);
            if (_task.Visible)
            {
                _task.Text = "▶  Şimdi siz: " + task;
                _task.MaximumSize = new Size(BalloonWidth - 44, 0);
                _task.Location = new Point(x, y);
                y += _task.PreferredHeight + 12;
            }
            if (art != null && artHeight > 0)
            {
                _art.SetBounds(x, y, BalloonWidth - 44, artHeight);
                _art.Visible = true;
                _art.Invalidate();
                y += artHeight + 14;
            }
            else _art.Visible = false;
            y += 4;

            foreach (Button b in _choiceButtons) { Controls.Remove(b); b.Dispose(); }
            _choiceButtons.Clear();
            int right = BalloonWidth - 22;
            if (choices != null && choices.Count > 0)
            {
                _next.Visible = false;
                for (int i = choices.Count - 1; i >= 0; i--)
                {
                    TourChoice c = choices[i];
                    Button b = Theme.MakeButton(c.Label, i == 0 ? Theme.ButtonKind.Primary : Theme.ButtonKind.Secondary);
                    b.Width = Math.Max(76, TextRenderer.MeasureText(c.Label, b.Font).Width + 28);
                    b.Location = new Point(right - b.Width, y);
                    b.Click += (s, e) => { var h = ChoiceClicked; if (h != null) h(c); };
                    right = b.Left - 8;
                    _choiceButtons.Add(b);
                    Controls.Add(b);
                }
            }
            else
            {
                _next.Visible = true;
                _next.Text = nextText;
                _next.Width = Math.Max(96, TextRenderer.MeasureText(nextText, _next.Font).Width + 28);
                _next.Location = new Point(right - _next.Width, y);
                right = _next.Left - 8;
            }
            _back.Enabled = canBack;
            _back.Location = new Point(right - _back.Width, y);
            _skip.Location = new Point(x - 6, y);
            _dontShow.Location = new Point(x - 2, y + 40);
            Size = new Size(BalloonWidth, y + 40 + _dontShow.PreferredSize.Height + 16);
            ResumeLayout(true);
            Invalidate();
            // Odak yalnız balon zaten etkinse taşınır (etkin olmayan pencereye odak vermek onu öne alır, açık menüleri kapatır)
            if (IsHandleCreated && Form.ActiveForm == this) (_next.Visible ? _next : (Control)_back).Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, 0, Width, 4);
        }

        private static void Raise(Action a) { if (a != null) a(); }
    }
}
