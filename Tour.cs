using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>Uygulama turunun bir adımı (G36).</summary>
    public sealed class TourStep
    {
        public string Title;
        /// <summary>Açıklama (adım gösterilirken hesaplanır: ör. açık kart yoksa ek not).</summary>
        public Func<string> Body;
        /// <summary>Vurgulanacak alan (ekran koordinatı); null: alan yok, balon ortada.</summary>
        public Func<Rectangle?> Target;
        /// <summary>Adıma girerken (ör. ilgili sekmeye geç).</summary>
        public Action Enter;
        /// <summary>İsteğe bağlı küçük çizim (durum renkleri, renklendirme örneği, kısayollar…).</summary>
        public Action<Graphics, Rectangle> Illustration;
        public int IllustrationHeight;
    }

    /// <summary>
    /// Uygulama turu (G36): ana pencerenin üzerine yarı saydam bir katman koyar, anlatılan bölümü bu katmanda açık bırakıp
    /// vurgular ve yanına açıklama balonu yerleştirir. Geri / İleri / Turu atla; Esc atlar, ← → adım değiştirir.
    /// "Bir daha gösterme" işaretlenirse ya da tur sonuna kadar izlenirse bir daha kendiliğinden açılmaz
    /// (Yardım → Uygulama turu ile her zaman yeniden açılır). Tur hiçbir ayarı / kartı değiştirmez, yalnız gösterir.
    /// </summary>
    public sealed class TourController : IDisposable
    {
        private readonly Form _owner;
        private readonly IList<TourStep> _steps;
        private readonly TourOverlay _overlay = new TourOverlay();
        private readonly TourBalloon _balloon;
        private readonly Timer _follow = new Timer { Interval = 250 };
        private int _index = -1;
        private Rectangle? _shownHole;
        private Rectangle _shownOwner;
        private bool _closed;

        /// <summary>Tur kapandı: (sonuna kadar izlendi mi, "bir daha gösterme" işaretli mi).</summary>
        public event Action<bool, bool> Closed;

        public TourController(Form owner, IList<TourStep> steps)
        {
            _owner = owner;
            _steps = steps;
            _balloon = new TourBalloon();
            _balloon.NextClicked += () => Go(_index + 1);
            _balloon.BackClicked += () => Go(_index - 1);
            _balloon.SkipClicked += () => Finish(false);
            _follow.Tick += (s, e) => Reposition(false);
            _owner.Move += OwnerChanged;
            _owner.Resize += OwnerChanged;
            _owner.FormClosing += OwnerClosing;
        }

        public bool IsOpen { get { return !_closed && _index >= 0; } }

        public void Start()
        {
            _overlay.Show(_owner);
            _balloon.Show(_overlay);
            _follow.Start();
            Go(0);
        }

        private void Go(int index)
        {
            if (_closed) return;
            if (index >= _steps.Count) { Finish(true); return; }
            if (index < 0) index = 0;
            _index = index;
            TourStep step = _steps[index];
            try { if (step.Enter != null) step.Enter(); }
            catch (Exception ex) { ErrorText.LogTechnical("tur adımı", ex); }
            string body = "";
            try { body = step.Body != null ? step.Body() : ""; } catch { }
            _balloon.SetStep(index, _steps.Count, step.Title, body, step.Illustration, step.IllustrationHeight);
            // Sekme değişince yerleşim bir sonraki iletide oturur: konum o zaman hesaplanır
            _owner.BeginInvoke(new Action(() => Reposition(true)));
        }

        private void OwnerChanged(object sender, EventArgs e) { Reposition(true); }

        private void OwnerClosing(object sender, FormClosingEventArgs e) { Finish(false); }

        /// <summary>Katmanı pencereye, deliği hedefe, balonu deliğin yanına yerleştirir.</summary>
        private void Reposition(bool force)
        {
            if (_closed || _index < 0) return;
            if (_owner.WindowState == FormWindowState.Minimized) return;
            Rectangle client = _owner.RectangleToScreen(_owner.ClientRectangle);
            Rectangle? hole = null;
            try { hole = _steps[_index].Target != null ? _steps[_index].Target() : null; } catch { hole = null; }
            if (hole.HasValue)
            {
                Rectangle h = Rectangle.Inflate(hole.Value, 6, 6);
                h.Intersect(client);
                hole = h.Width > 8 && h.Height > 8 ? h : (Rectangle?)null;
            }
            if (!force && hole == _shownHole && client == _shownOwner) return;
            _shownHole = hole;
            _shownOwner = client;

            _overlay.Bounds = client;
            _overlay.SetHole(hole.HasValue ? new Rectangle(hole.Value.X - client.X, hole.Value.Y - client.Y, hole.Value.Width, hole.Value.Height) : (Rectangle?)null);
            _balloon.Location = PlaceBalloon(client, hole, _balloon.Size);
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
            bool dontShow = _balloon.DontShowAgain;
            _follow.Stop();
            _owner.Move -= OwnerChanged;
            _owner.Resize -= OwnerChanged;
            _owner.FormClosing -= OwnerClosing;
            try { _balloon.Close(); } catch { }
            try { _overlay.Close(); } catch { }
            var h = Closed;
            if (h != null) h(completed, dontShow);
        }

        public void Dispose()
        {
            Finish(false);
            _follow.Dispose();
            _balloon.Dispose();
            _overlay.Dispose();
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
        private const int BalloonWidth = 430;
        private readonly Label _progress = Theme.MakeLabel("", Theme.SectionHeader, Theme.Accent);
        private readonly Label _title = Theme.MakeLabel("", new Font("Segoe UI Semibold", 13f), Theme.Text);
        private readonly Label _body = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Panel _art = new Panel();
        private readonly CheckBox _dontShow = new CheckBox { Text = "Bir daha gösterme", AutoSize = true };
        private readonly Button _skip = Theme.MakeButton("Turu atla", Theme.ButtonKind.Ghost);
        private readonly Button _back = Theme.MakeButton("Geri", Theme.ButtonKind.Secondary);
        private readonly Button _next = Theme.MakeButton("İleri", Theme.ButtonKind.Primary);
        private Action<Graphics, Rectangle> _illustration;

        public event Action NextClicked;
        public event Action BackClicked;
        public event Action SkipClicked;

        public TourBalloon()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.SurfaceHigh;
            KeyPreview = true;
            Font = Theme.Ui;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            _body.AutoSize = true;
            _body.MaximumSize = new Size(BalloonWidth - 44, 0);
            _title.AutoSize = true;
            _title.MaximumSize = new Size(BalloonWidth - 44, 0);
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
            _skip.Width = 92; _back.Width = 76; _next.Width = 96;
            Controls.AddRange(new Control[] { _progress, _title, _body, _art, _dontShow, _skip, _back, _next });

            _next.Click += (s, e) => Raise(NextClicked);
            _back.Click += (s, e) => Raise(BackClicked);
            _skip.Click += (s, e) => Raise(SkipClicked);
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Raise(SkipClicked); }
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; Raise(NextClicked); }
                else if (e.KeyCode == Keys.Left) { e.Handled = true; Raise(BackClicked); }
            };
        }

        public bool DontShowAgain { get { return _dontShow.Checked; } }

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

        public void SetStep(int index, int count, string title, string body, Action<Graphics, Rectangle> art, int artHeight)
        {
            SuspendLayout();
            _progress.Text = "UYGULAMA TURU  ·  " + (index + 1) + " / " + count;
            _title.Text = title ?? "";
            _body.Text = body ?? "";
            _illustration = art;
            int x = 22, y = 20;
            _progress.Location = new Point(x, y);
            y += _progress.PreferredHeight + 6;
            _title.Location = new Point(x, y);
            y += _title.PreferredHeight + 8;
            _body.Location = new Point(x, y);
            y += _body.PreferredHeight + 12;
            if (art != null && artHeight > 0)
            {
                _art.SetBounds(x, y, BalloonWidth - 44, artHeight);
                _art.Visible = true;
                _art.Invalidate();
                y += artHeight + 14;
            }
            else _art.Visible = false;
            y += 4;
            bool last = index == count - 1;
            _next.Text = last ? "Bitir" : "İleri";
            _back.Enabled = index > 0;
            _skip.Visible = !last;
            _next.Location = new Point(BalloonWidth - 22 - _next.Width, y);
            _back.Location = new Point(_next.Left - 8 - _back.Width, y);
            _skip.Location = new Point(_back.Left - 8 - _skip.Width, y);
            _dontShow.Location = new Point(x - 2, y + 40);
            Size = new Size(BalloonWidth, y + 40 + _dontShow.PreferredSize.Height + 16);
            ResumeLayout(true);
            Invalidate();
            if (IsHandleCreated) _next.Focus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _next.Focus();
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
