using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Çalışma alanındaki bir kart penceresi: başlık (durum, bağlantı süresi, RX/TX aktivitesi, düğmeler),
    /// terminal çıktısı ve komut satırı. Bağlantının sahibi değildir; CardSession'ı gösterir.
    /// Kapatmak bağlantıyı kesmez.
    /// </summary>
    public sealed partial class CardView : UserControl
    {
        // Satır sınırına ek güvenlik: satır sonu içermeyen çok uzun akışlarda bellek sınırı
        private const int MaxOutputChars = 4000000;
        private int _appliedMaxLines;
        private InputAutoComplete _autoComplete;
        private static readonly TimeSpan ActivityWindow = TimeSpan.FromMilliseconds(180);

        private readonly Panel _header = new Panel { Dock = DockStyle.Top, Height = 38 };
        private readonly Label _title = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _subtitle = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Label _uptime = new GlyphLabel { Text = "⏱ --:--:--", Font = Theme.UiSmall, ForeColor = Theme.TextFaint, AutoSize = true, BackColor = Color.Transparent };
        private readonly Label _rx = Theme.MakeLabel("RX", Theme.UiSmall, Theme.TextFaint);
        private readonly Label _tx = Theme.MakeLabel("TX", Theme.UiSmall, Theme.TextFaint);
        // İkonlu düğmeler: ipucu ve ekran okuyucu adı tam işlevi söyler ("Bağlantı Kur", "Temizle" …)
        private readonly IconButton _connectBtn = new IconButton(IconKind.Connect, IconTone.Success);
        private readonly IconButton _clearBtn = new IconButton(IconKind.Clear, IconTone.Ghost);
        private readonly IconButton _maxBtn = new IconButton(IconKind.Maximize, IconTone.Ghost);
        private readonly IconButton _closeBtn = new IconButton(IconKind.Close, IconTone.Ghost);
        private readonly ToolTip _tips = new DarkToolTip();

        private readonly Label _stateStrip = new GlyphLabel
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Font = Theme.UiSmall,
        };

        private readonly Panel _outputHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 6, 4, 4) };
        private readonly RichTextBox _output = new TerminalBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Input,
            ForeColor = Theme.Terminal,
            Font = Theme.Mono,
            WordWrap = false,
            HideSelection = false,
            DetectUrls = false,
        };
        private readonly PillButton _newLinesPill = new PillButton { Visible = false };
        private readonly AlertBanner _dropBanner = new AlertBanner { Visible = false };
        private readonly Panel _field = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 6, 2) };
        private bool? _shownConnectedLook;
        private DateTime? _shownDrop;

        private readonly Panel _inputBar = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 6, 8, 6) };
        private readonly Label _prompt = Theme.MakeLabel("›", Theme.UiBold, Theme.Accent);
        private readonly TextBox _input = new TextBox { BorderStyle = BorderStyle.None, Font = Theme.Mono };
        private readonly IconButton _sendBtn = new IconButton(IconKind.Send, IconTone.Primary);
        private readonly Panel _quickGap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };
        private readonly Panel _rebootGap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };

        private bool _selected;
        private bool _maximized;
        private CardState _shownState = (CardState)(-1);
        private string _shownUptime;

        public CardView(CardSession session)
        {
            Session = session;
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            Padding = new Padding(1);   // 1px kenarlık için
            MinimumSize = new Size(260, 160);

            BuildHeader();
            BuildInput();
            BuildOutput();
            BuildActions();
            ((TerminalBox)_output).ZoomWheel = OnTerminalZoomWheel;

            // Alttan yukarı: komut satırı, dönüş değerleri çubuğu, (açıksa) dönüş listesi, terminal
            Controls.Add(_outputHost);
            Controls.Add(_respPanel);
            Controls.Add(_respBar);
            Controls.Add(_inputBar);
            Controls.Add(_stateStrip);
            Controls.Add(_header);

            // Geçmişi yükle (görünüm kapalıyken gelen veriler dahil)
            _output.Text = Session.AttachView();       // oturum geçmişi zaten satır sınırına kırpılmış
            _appliedMaxLines = Session.MaxLines;
            _output.HandleCreated += (s, e) => ColorizeAlerts(0, null);
            ScrollToEnd();
            RefreshState();
            RefreshUptime();

            // Kartın herhangi bir yerine tıklamak onu seçer
            WireSelection(this);
        }

        public CardSession Session { get; private set; }

        public event Action<CardView> CloseRequested;
        public event Action<CardView> MaximizeToggled;
        public event Action<CardView> Activated;

        public bool IsMaximized
        {
            get { return _maximized; }
            set
            {
                _maximized = value;
                _maxBtn.Icon = value ? IconKind.Restore : IconKind.Maximize;
                _maxBtn.SetTip(_tips, value ? "Izgaraya dön" : "Büyüt");
            }
        }

        public bool Selected
        {
            get { return _selected; }
            set { if (_selected != value) { _selected = value; Invalidate(); } }
        }

        public void FocusInput()
        {
            _input.Focus();
        }

        // ---------- Oluşturma ----------

        private void BuildHeader()
        {
            _header.BackColor = Theme.Surface;
            _header.Paint += Header_Paint;

            _title.AutoSize = false;
            _title.AutoEllipsis = true;
            _title.Height = 17;
            _title.Location = new Point(26, 4);
            RefreshTitle();
            _subtitle.AutoSize = false;
            _subtitle.AutoEllipsis = true;
            _subtitle.Height = 16;
            _subtitle.Location = new Point(27, 20);

            _uptime.Font = new Font("Consolas", 8.5f);
            _rx.Font = new Font("Consolas", 7.5f, FontStyle.Bold);
            _tx.Font = _rx.Font;

            foreach (var b in new IconButton[] { _connectBtn, _snapBtn, _clearBtn, _maxBtn, _closeBtn })
            {
                b.Height = 26;
                b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }
            foreach (var b in new IconButton[] { _connectBtn, _snapBtn, _clearBtn, _maxBtn, _closeBtn }) b.Width = 28;

            _tips.SetToolTip(_uptime, "Bağlantı süresi (bağlantı kurulduğu andan itibaren)");
            _clearBtn.SetTip(_tips, "Temizle — ekranı ve geçmişi temizler");
            _connectBtn.SetTip(_tips, ConnectionAction.ConnectLabel);
            _maxBtn.SetTip(_tips, "Büyüt");
            _closeBtn.SetTip(_tips, "Kapat — görünümü kapatır (bağlantı ve kart yapılandırması korunur)");
            _tips.SetToolTip(_rx, "Veri alınıyor");
            _tips.SetToolTip(_tx, "Veri gönderiliyor");

            _header.Controls.AddRange(new Control[] { _title, _subtitle, _uptime, _rx, _tx, _connectBtn, _snapBtn, _clearBtn, _maxBtn, _closeBtn });
            _header.Resize += (s, e) => LayoutHeader();
            LayoutHeader();

            _connectBtn.Click += async (s, e) =>
            {
                if (SelectionLogic.IsLive(Session.State)) Session.Disconnect();
                else await Session.ConnectAsync();
            };
            _clearBtn.Click += (s, e) => { Session.ClearHistory(); _output.Clear(); HideNewLinesPill(); };
            _maxBtn.Click += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _closeBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(this); };
            _header.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _title.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
        }

        private void LayoutHeader()
        {
            int w = _header.Width;
            // İkonlar az yer kaplar; çok dar kartlarda önce RX/TX, sonra süre gizlenir
            _clearBtn.Visible = w >= 300;
            _snapBtn.Visible = w >= 290;
            _rx.Visible = _tx.Visible = w >= 380;
            _uptime.Visible = w >= 330;

            int x = w - 6;
            x -= _closeBtn.Width; _closeBtn.Location = new Point(x, 6);
            x -= _maxBtn.Width + 2; _maxBtn.Location = new Point(x, 6);
            if (_clearBtn.Visible) { x -= _clearBtn.Width + 2; _clearBtn.Location = new Point(x, 6); }
            if (_snapBtn.Visible) { x -= _snapBtn.Width + 2; _snapBtn.Location = new Point(x, 6); }
            x -= _connectBtn.Width + 2; _connectBtn.Location = new Point(x, 6);
            if (_rx.Visible)
            {
                x -= _tx.Width + 10; _tx.Location = new Point(x, 12);
                x -= _rx.Width + 4; _rx.Location = new Point(x, 12);
            }
            if (_uptime.Visible)
            {
                x -= _uptime.Width + 10; _uptime.Location = new Point(x, 12);
            }
            _subtitle.Width = Math.Max(40, x - _subtitle.Left - 8);
            _title.Width = Math.Max(40, x - _title.Left - 8);
        }

        private void BuildOutput()
        {
            _outputHost.Controls.Add(_dropBanner);     // önce eklenenler öne çizilir → çıktının üstünde yüzer
            _outputHost.Controls.Add(_newLinesPill);
            _outputHost.Controls.Add(_output);
            Theme.ApplyDarkScrollBars(_output);

            _outputHost.Resize += (s, e) => { if (_newLinesPill.Visible) PlacePill(); PlaceBanner(); PlaceToast(); };
            _dropBanner.Click += (s, e) => _dropBanner.Visible = false;   // tıklayınca gizlenir (kırmızı şerit kalır)
            _newLinesPill.Click += (s, e) => { ScrollToEnd(); _indicator.OnClick(); SyncPill(); _output.Focus(); };
            // Yalnız KULLANICININ kaydırması göstergeyi kapatır. Metin eklerken programın yaptığı geçici kaymalar
            // (AppendText bir anlığına en alta kayar) yok sayılır; yoksa gösterge her satırda kapanıp sayaç sıfırlanıyordu.
            _output.VScroll += (s, e) => CheckReachedBottom();
            _output.KeyUp += (s, e) => CheckReachedBottom();
            _output.MouseUp += (s, e) => CheckReachedBottom();
        }

        private void BuildInput()
        {
            _inputBar.BackColor = Theme.SurfaceHigh;

            var field = _field;
            field.BackColor = Theme.Input;
            _prompt.Dock = DockStyle.Left;
            _prompt.AutoSize = false;
            _prompt.Width = 14;
            _prompt.TextAlign = ContentAlignment.MiddleLeft;
            _input.Dock = DockStyle.Fill;
            _input.BackColor = Theme.Input;
            _input.ForeColor = Theme.Text;
            field.Controls.Add(_input);
            field.Controls.Add(_prompt);

            _sendBtn.Dock = DockStyle.Right;
            _sendBtn.Width = 36;
            _sendBtn.SetTip(_tips, "Gönder (Enter)");
            var gap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };

            // Sağdan sola: [Gönder] [reboot ⇄] [⚡ hızlı komut] [komut satırı]
            _quickBtn.Dock = DockStyle.Right;
            _rebootBtn.Dock = DockStyle.Right;
            _inputBar.Controls.Add(field);
            _inputBar.Controls.Add(_quickGap);
            _inputBar.Controls.Add(_quickBtn);
            _inputBar.Controls.Add(_rebootGap);
            _inputBar.Controls.Add(_rebootBtn);
            _inputBar.Controls.Add(gap);
            _inputBar.Controls.Add(_sendBtn);
            _inputBar.Resize += (s, e) => LayoutInputActions();

            // Enter tuşu YALNIZCA burada işlenir (tek işleyici). SuppressKeyPress, TextBox'ın
            // kendi Enter işlemesini ve uyarı sesini engeller; formda AcceptButton yoktur.
            _input.KeyDown += Input_KeyDown;
            // Kartın kendi komut geçmişinden otomatik tamamlama (Tab / ↓ / → ile kabul; çalıştırmaz)
            _autoComplete = new InputAutoComplete(_input, () => Session.CommandHistory);
            _sendBtn.Click += async (s, e) => await SendCurrentAsync();
            field.Click += (s, e) => _input.Focus();
        }

        private void WireSelection(Control root)
        {
            root.MouseDown += (s, e) => { var h = Activated; if (h != null) h(this); };
            root.Enter += (s, e) => { var h = Activated; if (h != null) h(this); };
            foreach (Control c in root.Controls) WireSelection(c);
        }

        // ---------- Çizim ----------

        private void Header_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color dot = Theme.StateColor(Session.State);
            using (var halo = new SolidBrush(Color.FromArgb(50, dot)))
                g.FillEllipse(halo, 8, 13, 13, 13);
            using (var br = new SolidBrush(dot))
                g.FillEllipse(br, 11, 16, 7, 7);
            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            CardState cs = Session.State;
            Color border = Session.DroppedAtUtc.HasValue && (cs == CardState.Error || cs == CardState.Recovering) ? Theme.Danger
                         : _selected ? Theme.Accent : Theme.Border;
            using (var pen = new Pen(border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // ---------- Güncelleme (ana pencerenin zamanlayıcısından çağrılır) ----------

        /// <summary>Yeni gelen veriyi basar; durum, süre ve aktivite göstergelerini günceller.</summary>
        public void Tick()
        {
            string text = Session.TakePending();
            if (text != null) AppendOutput(text);

            DateTime now = DateTime.UtcNow;
            _rx.ForeColor = now - Session.LastRxUtc < ActivityWindow ? Theme.Success : Theme.TextFaint;
            _tx.ForeColor = now - Session.LastTxUtc < ActivityWindow ? Theme.Accent : Theme.TextFaint;

            if (Session.State != _shownState) RefreshState();
            RefreshUptime();
            RefreshResponses();
            CheckReachedBottom();
            if (_appliedMaxLines != Session.MaxLines) ApplyLineLimitNow();   // ayar değişti: hemen uygula
        }

        private void RefreshUptime()
        {
            TimeSpan? d = Session.ConnectedDuration(DateTime.UtcNow);
            string text = d.HasValue ? "⏱ " + CardSession.FormatDuration(d.Value) : "⏱ --:--:--";
            if (text == _shownUptime) return;       // yalnız saniye değişince yeniden çiz
            bool widthChanged = _shownUptime == null || _shownUptime.Length != text.Length;
            _shownUptime = text;
            _uptime.Text = text;
            _uptime.ForeColor = d.HasValue ? Theme.Success : Theme.TextFaint;
            if (widthChanged) LayoutHeader();
        }

        private void RefreshState()
        {
            CardState st = Session.State;
            _shownState = st;

            string group = Session.Card.Group != null ? Session.Card.Group.Name : "";
            int port = Session.Card.Group != null ? Session.Card.Group.Port : 23;
            string sub = group + "  ·  port " + port + "  ·  " + Theme.StateText(st);
            bool lost = st == CardState.Error || st == CardState.Recovering;
            if (lost && !string.IsNullOrEmpty(Session.LastError)) sub += " (" + Session.LastError + ")";
            _subtitle.Text = sub;
            _subtitle.ForeColor = lost ? Theme.Danger : Theme.TextMuted;

            bool live = SelectionLogic.IsLive(st);
            // Bağlıyken kırmızı "fişi çek", değilken yeşil "fişi tak"; ipucu ve ekran okuyucu adı tam etiket
            _connectBtn.Icon = live ? IconKind.Disconnect : IconKind.Connect;
            _connectBtn.Tone = live ? IconTone.Danger : IconTone.Success;
            _connectBtn.SetTip(_tips, live ? ConnectionAction.DisconnectLabel + " (yalnız bu kart)" : ConnectionAction.ConnectLabel + " (yalnız bu kart)");
            _sendBtn.Enabled = st == CardState.Connected;
            RefreshActionButtons();
            _prompt.ForeColor = st == CardState.Connected ? Theme.Accent : Theme.TextFaint;

            // Bağlı değilken kart ekranda olsa da bunu açıkça göster
            _stateStrip.Visible = st != CardState.Connected;
            switch (st)
            {
                case CardState.Connecting:
                    _stateStrip.Text = "●  Bağlanıyor…";
                    _stateStrip.BackColor = Color.FromArgb(58, 46, 22);
                    _stateStrip.ForeColor = Theme.Warning;
                    break;
                case CardState.Error:
                    _stateStrip.Text = "●  Bağlantı yok: " + (Session.LastError ?? "hata") + "  —  komut gönderilemez";
                    _stateStrip.BackColor = Color.FromArgb(60, 26, 30);
                    _stateStrip.ForeColor = Theme.Danger;
                    break;
                default:
                    _stateStrip.Text = "●  Bağlı değil  —  bağlanmak için \"Bağlantı Kur\"u kullanın";
                    _stateStrip.BackColor = Theme.SurfaceHigh;
                    _stateStrip.ForeColor = Theme.TextMuted;
                    break;
            }

            DateTime? drop = lost ? Session.DroppedAtUtc : null;
            if (drop.HasValue)
            {
                string at = drop.Value.ToLocalTime().ToString("HH:mm:ss");
                _stateStrip.Text = "⚠  BAĞLANTI KESİLDİ (" + at + ")  —  " + (Session.LastError ?? "") +
                    (st == CardState.Recovering
                        ? "  ·  kart ulaşılabilir olunca otomatik yeniden bağlanılacak"
                        : "  ·  yeniden bağlanmak için \"Bağlantı Kur\"");
                _stateStrip.BackColor = Color.FromArgb(120, 28, 34);
                _stateStrip.ForeColor = Color.White;
            }
            ApplyLook(st == CardState.Connected, drop);
            _header.Invalidate();
            Invalidate();   // kenarlık rengi
        }

        // ---------- Çıktı ve kaydırma ----------

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public int cbSize; public int fMask; public int nMin; public int nMax;
            public int nPage; public int nPos; public int nTrackPos;
        }

        private const int WM_SETREDRAW = 0x000B;
        private const int EM_GETSCROLLPOS = 0x0400 + 221;
        private const int EM_SETSCROLLPOS = 0x0400 + 222;
        private const int SB_VERT = 1;
        private const int SIF_ALL = 0x17;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref POINT lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO lpsi);

        /// <summary>
        /// Yeni metni ekler. Kullanıcı en alttaysa akışı izlemeye devam eder; yukarıdaysa kaydırma
        /// konumu ve seçimi olduğu gibi korunur ve "↓ Yeni satırlar" göstergesi çıkar.
        /// (Eskiden AppendText imleci sona taşıdığı için kutu her seferinde en alta kayıyordu.)
        /// </summary>
        private void AppendOutput(string text)
        {
            if (!IsHandleCreated)
            {
                _output.AppendText(text);
                return;
            }

            // Kullanıcının konumu EKLEMEDEN ÖNCE ölçülür; ekleme sırasındaki kayma olayları kullanıcıdan gelmiyor
            bool userWasAtBottom = IsAtBottom();
            int newLines = 0;
            foreach (char c in text) if (c == '\n') newLines++;

            _indicator.BeginAppend();
            try
            {
                AppendOutputCore(text, userWasAtBottom);
            }
            finally
            {
                _indicator.EndAppend(newLines, userWasAtBottom);
                SyncPill();
            }
        }

        private void AppendOutputCore(string text, bool follow)
        {
            // Ekleme ve kırpma tek seferde, çizim kapalıyken yapılır (satır sınırı aşıldığında titreme / gereksiz çizim yok)
            IntPtr h = _output.Handle;
            var pos = new POINT();
            if (!follow) SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;

            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                int start = PrepareAppend();
                _output.AppendText(text);
                ColorizeAlerts(start, text);
                int removed = TrimToLimit(!follow, ref pos);   // en eski satırlar silinir, en yeniler kalır

                if (follow)
                {
                    ScrollToEnd();
                }
                else
                {
                    // Kullanıcı eski çıktıyı okuyor: seçim ve kaydırma konumu (silinen kısım kadar kaydırılarak) korunur
                    selStart = Math.Max(0, selStart - removed);
                    if (removed > 0 && selLen > 0 && selStart == 0) selLen = 0;
                    _output.Select(selStart, selLen);
                    SendMessage(h, EM_SETSCROLLPOS, IntPtr.Zero, ref pos);
                }
            }
            finally
            {
                SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _output.Invalidate();
            }
        }

        /// <summary>Satır sınırı değiştiğinde mevcut çıktıyı hemen yeni sınıra kırpar (konum korunur).</summary>
        private void ApplyLineLimitNow()
        {
            _appliedMaxLines = Session.MaxLines;
            Session.ApplyLineLimit();
            if (!IsHandleCreated || _output.TextLength == 0) return;
            bool follow = IsAtBottom();
            IntPtr h = _output.Handle;
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                int removed = TrimToLimit(!follow, ref pos);
                if (follow) ScrollToEnd();
                else
                {
                    _output.Select(Math.Max(0, selStart - removed), removed > 0 && selStart - removed <= 0 ? 0 : selLen);
                    SendMessage(h, EM_SETSCROLLPOS, IntPtr.Zero, ref pos);
                }
            }
            finally
            {
                SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _output.Invalidate();
            }
        }

        /// <summary>
        /// Çıktıyı ayarlı satır sınırında tutar: fazla satırlar BAŞTAN silinir (hiçbir zaman tümü silinmez).
        /// Ek güvenlik olarak çok uzun, satır sonu içermeyen akışta karakter sınırı da uygulanır.
        /// keepPosition: kullanıcı yukarıdaysa kaydırma konumu silinen yükseklik kadar düzeltilir. Silinen karakter sayısını döndürür.
        /// </summary>
        private int TrimToLimit(bool keepPosition, ref POINT pos)
        {
            int lastLine = _output.GetLineFromCharIndex(_output.TextLength);
            bool endsWithNewline = _output.GetFirstCharIndexFromLine(lastLine) == _output.TextLength;
            int excess = LineBudget.ExcessLines(lastLine, endsWithNewline, Session.MaxLines);

            int cut = 0;
            if (excess > 0) cut = _output.GetFirstCharIndexFromLine(excess);
            if (_output.TextLength - cut > MaxOutputChars)
            {
                int c2 = _output.TextLength - MaxOutputChars / 2;
                int ls = _output.GetFirstCharIndexFromLine(_output.GetLineFromCharIndex(c2));
                cut = Math.Max(cut, ls > 0 ? ls : c2);
            }
            if (cut <= 0) return 0;

            int removedHeight = 0;
            if (keepPosition)
            {
                // Silinecek kısmın piksel yüksekliği = kesim noktasının belge içindeki y'si.
                // GetPositionFromCharIndex o anki görünüme göredir; ekleme sırasında kutu kaymış olabileceği için
                // ŞU ANKİ kaydırma konumu eklenir.
                var now = new POINT();
                SendMessage(_output.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref now);
                Point p = _output.GetPositionFromCharIndex(cut);
                removedHeight = p.Y + now.Y;
            }

            _output.ReadOnly = false;
            _output.Select(0, cut);
            _output.SelectedText = "";
            _output.ReadOnly = true;

            if (keepPosition) pos.Y = Math.Max(0, pos.Y - removedHeight);
            return cut;
        }

        /// <summary>Kartın görünen adı değişince başlığı günceller ("IP" ya da "IP (Ad)").</summary>
        public void RefreshTitle()
        {
            _title.Text = Session.Card.Label;
            _tips.SetToolTip(_title, Session.Card.Label + "  ·  " + Session.Card.DisplayName);
            if (_dropBanner.Visible) _shownDrop = null;   // uyarı kutusu yeni adla yeniden kurulsun
        }

        private bool IsAtBottom()
        {
            if (!IsHandleCreated || _output.TextLength == 0) return true;
            var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_ALL };
            if (!GetScrollInfo(_output.Handle, SB_VERT, ref si) || si.nPage == 0 || si.nMax <= si.nPage)
                return true;                                   // kaydırma çubuğu yok: her şey görünüyor
            int lineHeight = Math.Max(8, _output.Font.Height);
            return si.nPos + si.nPage >= si.nMax - lineHeight; // son satır görünüyorsa "en altta"
        }

        private void ScrollToEnd()
        {
            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.ScrollToCaret();
        }

        private const string PillWidest = "↓  Yeni Satırlar (999+)";
        private Size _pillSize;
        private readonly NewLinesIndicator _indicator = new NewLinesIndicator();

        /// <summary>
        /// Göstergenin durumunu (NewLinesIndicator) ekrana yansıtır: yalnız ilk görünmede yerleştirilir,
        /// sonrasında sadece sayaç metni değişir (yeniden oluşturma, boyutlandırma ya da taşıma yok).
        /// </summary>
        private void SyncPill()
        {
            if (!_indicator.Visible)
            {
                if (_newLinesPill.Visible) _newLinesPill.Visible = false;
                return;
            }
            string text = _indicator.Text;
            if (_newLinesPill.Text != text) _newLinesPill.Text = text;
            if (!_newLinesPill.Visible)
            {
                PlacePill();
                _newLinesPill.Visible = true;
                _newLinesPill.BringToFront();
            }
        }

        private void HideNewLinesPill()
        {
            _indicator.Reset();
            SyncPill();
        }

        private void PlacePill()
        {
            // Boyut en geniş sayaca göre bir kez sabitlenir: basamak sayısı artınca gösterge kaymaz
            if (_pillSize.IsEmpty)
            {
                string t = _newLinesPill.Text;
                _newLinesPill.Text = PillWidest;
                _pillSize = _newLinesPill.GetPreferredSize(Size.Empty);
                _newLinesPill.Text = t;
                _newLinesPill.Size = _pillSize;
            }
            int x = Math.Max(4, (_outputHost.ClientSize.Width - _pillSize.Width) / 2);
            int y = Math.Max(4, _outputHost.ClientSize.Height - _pillSize.Height - 12);
            var loc = new Point(x, y);
            if (_newLinesPill.Location != loc) _newLinesPill.Location = loc;
        }

        /// <summary>Kullanıcı kaydırma çubuğunu sürükleyerek en alta indiyse de göstergeyi kapat.</summary>
        private void CheckReachedBottom()
        {
            if (!_indicator.Visible) return;
            _indicator.OnScroll(IsAtBottom());   // program eklerken gelen kaymalar sınıf içinde yok sayılır
            SyncPill();
        }

        // ---------- Bağlı / bağlı değil görünümü ----------

        // Bağlı değilken kullanılan gri tonlar (bağlı kartın renkleri hiç değişmez)
        private static readonly Color OffOutputBack = Color.FromArgb(27, 29, 33);
        private static readonly Color OffOutputText = Color.FromArgb(137, 143, 153);   // gri ama okunur (≥ 4.5:1)
        private static readonly Color OffHeaderBack = Color.FromArgb(31, 33, 38);
        private static readonly Color OffInputBack = Color.FromArgb(36, 38, 43);
        private static readonly Color AlertText = Color.FromArgb(255, 99, 99);

        /// <summary>
        /// Bağlıysa normal renkler; değilse tüm kart gri tonlara geçer ve konsol yazmaya kapanır.
        /// Bağlıyken kopma olduysa çıktının üstünde kırmızı uyarı kutusu da gösterilir.
        /// </summary>
        private void ApplyLook(bool connected, DateTime? drop)
        {
            if (_shownConnectedLook != connected)
            {
                _shownConnectedLook = connected;
                Color outBack = connected ? Theme.Input : OffOutputBack;
                Color outText = connected ? Theme.Terminal : OffOutputText;
                Color headBack = connected ? Theme.Surface : OffHeaderBack;
                Color inBack = connected ? Theme.Input : OffInputBack;

                _output.SuspendLayout();
                _output.BackColor = outBack;
                _output.ForeColor = outText;           // tüm metni yeniden renklendirir…
                ColorizeAlerts(0, null);               // …kırmızı uyarı satırlarını geri boya
                _output.ResumeLayout();
                _outputHost.BackColor = outBack;

                _header.BackColor = headBack;
                _title.ForeColor = connected ? Theme.Text : Theme.TextMuted;
                BackColor = headBack;

                // Konsol: bağlı değilken yazılamaz
                _input.ReadOnly = !connected;
                _input.TabStop = connected;
                if (!connected) _input.Clear();
                _input.BackColor = inBack;
                _field.BackColor = inBack;
                _inputBar.BackColor = connected ? Theme.SurfaceHigh : OffHeaderBack;
                _input.Cursor = connected ? Cursors.IBeam : Cursors.No;
                Theme.SetCueBanner(_input, connected ? "Komut yazıp Enter'a basın  (↑/↓ geçmiş)" : "Kart bağlı değil — komut yazılamaz");
            }

            if (_shownDrop != drop)
            {
                _shownDrop = drop;
                if (drop.HasValue)
                {
                    _dropBanner.Title = "⚠  BAĞLANTI KESİLDİ";
                    _dropBanner.Detail = Session.Card.Label + "  ·  " + drop.Value.ToLocalTime().ToString("HH:mm:ss") +
                                         "  ·  " + (Session.LastError ?? ErrorText.ConnectionLost);
                    _dropBanner.Visible = true;
                    _dropBanner.BringToFront();
                    PlaceBanner();
                }
                else _dropBanner.Visible = false;
            }
        }

        /// <summary>Eklenecek metnin varsayılan (o anki tema) renkte başlamasını sağlar; ekleme başlangıcını döndürür.</summary>
        private int PrepareAppend()
        {
            int start = _output.TextLength;
            _output.SelectionStart = start;
            _output.SelectionLength = 0;
            _output.SelectionColor = _output.ForeColor;   // önceki satır kırmızıysa rengi devralmasın
            return start;
        }

        private Font _alertFont;

        /// <summary>
        /// "!!! " ile başlayan uyarı satırlarını kırmızı ve kalın boyar. appended verilirse yalnız yeni eklenen
        /// kısım taranır (yoğun akışta tüm metni kopyalamamak için); null ise tüm metin taranır (nadiren).
        /// Oturum uyarıları her zaman satır başına yazar.
        /// </summary>
        private void ColorizeAlerts(int start, string appended)
        {
            if (!_output.IsHandleCreated) return;          // tutamaç oluşunca HandleCreated tümünü boyar
            string text = appended ?? _output.Text;
            int offset = appended != null ? start : 0;
            if (text.IndexOf(CardSession.AlertPrefix, StringComparison.Ordinal) < 0) return;

            if (_alertFont == null) _alertFont = new Font(_output.Font, FontStyle.Bold);
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            int i = 0;
            while ((i = text.IndexOf(CardSession.AlertPrefix, i, StringComparison.Ordinal)) >= 0)
            {
                if (i == 0 || text[i - 1] == '\n')
                {
                    int end = text.IndexOf('\n', i);
                    if (end < 0) end = text.Length;
                    _output.Select(offset + i, end - i);
                    _output.SelectionColor = AlertText;
                    _output.SelectionFont = _alertFont;
                }
                i += CardSession.AlertPrefix.Length;
            }
            _output.Select(selStart, selLen);
        }

        private void PlaceBanner()
        {
            if (!_dropBanner.Visible) return;
            int w = Math.Min(_outputHost.ClientSize.Width - 24, 520);
            Size sz = _dropBanner.GetPreferredSize(new Size(w, 0));
            _dropBanner.Size = new Size(Math.Max(120, w), sz.Height);
            _dropBanner.Location = new Point(Math.Max(4, (_outputHost.ClientSize.Width - _dropBanner.Width) / 2), 10);
        }

        // ---------- Komut gönderme ----------

        private async Task SendCurrentAsync()
        {
            if (Session.State != CardState.Connected) return;
            string cmd = _input.Text;
            _input.Clear();
            Session.CommandHistory.Add(cmd);    // boş ve art arda tekrar eden komut saklanmaz

            // Komut gönderen kullanıcı yeni çıktıyı görmek ister: en alta in
            ScrollToEnd();
            HideNewLinesPill();
            try
            {
                await Session.SendCommandAsync(cmd);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(Session.Card.DisplayName + " komut", ex);
                AppendOutput("\n*** Gönderilemedi: " + ErrorText.Describe(ex) + "\n");
            }
        }

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (Session.State != CardState.Connected)
            {
                // Bağlı olmayan kartın konsoluna yazılamaz (ReadOnly); geçmiş/gönderim tuşları da çalışmaz
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) e.SuppressKeyPress = true;
                return;
            }
            // Önce otomatik tamamlama (Tab / ↓ / → / Esc); Enter'a dokunmaz
            if (_autoComplete != null && _autoComplete.HandleKeyDown(e)) return;

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                await SendCurrentAsync();   // yalnız yazılan gönderilir; soluk öneri metne dahil değildir
            }
            else if (e.KeyCode == Keys.Up && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string prev = Session.CommandHistory.Previous(_input.Text);
                if (prev != null) SetInput(prev);
            }
            else if (e.KeyCode == Keys.Down && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string next = Session.CommandHistory.Next();
                if (next != null) SetInput(next);   // en yeninin ötesinde yazılan taslak geri gelir
            }
            else if (e.KeyCode == Keys.C && e.Control && _input.SelectionLength == 0 && Session.State == CardState.Connected)
            {
                // Ctrl+C: karta kesme (ETX) gönder
                e.SuppressKeyPress = true;
                try { await Session.SendRawAsync("\x03"); } catch { }
            }
        }

        private void SetInput(string text)
        {
            if (_autoComplete != null) _autoComplete.SetTextQuiet(text);   // geçmişten gelen metinde öneri gösterme
            else
            {
                _input.Text = text;
                _input.SelectionStart = _input.TextLength;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Session.DetachView();
                _tips.Dispose();
                _toastTimer.Dispose();
                _rebootCooldown.Dispose();
                _quickMenu.Dispose();
                if (_alertFont != null) _alertFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Bağlantı kopunca çıktının üstünde görünen kırmızı uyarı kutusu. Tıklanınca gizlenir.</summary>
    internal sealed class AlertBanner : Control
    {
        private static readonly Color Back = Color.FromArgb(96, 22, 28);
        private static readonly Color Edge = Color.FromArgb(248, 113, 113);
        private readonly Font _titleFont = new Font("Segoe UI Semibold", 10.5f);

        public AlertBanner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Back;
            ForeColor = Color.White;
            Font = Theme.Ui;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        private string _title = "", _detail = "";
        public string Title { get { return _title; } set { _title = value ?? ""; Invalidate(); } }
        public string Detail { get { return _detail; } set { _detail = value ?? ""; Invalidate(); } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(proposedSize.Width, 18 + _titleFont.Height + 4 + Font.Height + 14 + 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var pen = new Pen(Edge, 2))
                g.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
            using (var bar = new SolidBrush(Edge))
                g.FillRectangle(bar, 0, 0, 5, Height);
            int x = 18, y = 12;
            string title = _title;
            if (title.StartsWith("⚠"))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                IconRenderer.Draw(g, IconKind.Warning, new RectangleF(x, y + 1, 18, 18), Color.White);
                title = title.Substring(1).TrimStart();
                TextRenderer.DrawText(g, title, _titleFont, new Point(x + 26, y), Color.White, TextFormatFlags.NoPrefix);
            }
            else TextRenderer.DrawText(g, title, _titleFont, new Point(x, y), Color.White, TextFormatFlags.NoPrefix);
            y += _titleFont.Height + 4;
            TextRenderer.DrawText(g, _detail, Font, new Rectangle(x, y, Width - x - 10, Font.Height + 2),
                Color.FromArgb(255, 205, 205), TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            y += Font.Height + 6;
            TextRenderer.DrawText(g, "Kartın ekranı ve geçmişi korunuyor. Kapatmak için tıklayın.", Theme.UiSmall,
                new Point(x, y), Color.FromArgb(220, 160, 160), TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _titleFont.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Çıktının üstünde yüzen, yuvarlak köşeli küçük düğme ("↓ Yeni satırlar").</summary>
    internal sealed class PillButton : Control
    {
        private bool _hover;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            BackColor = Theme.AccentFill;   // opak: her çizimde arkadaki paneli yeniden boyamaz (köşeler Region ile kırpılır)
            ForeColor = Color.White;
            Font = Theme.UiBold;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size t = TextRenderer.MeasureText(Text ?? "", Font);
            return new Size(t.Width + 28, t.Height + 12);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // Köşeleri gerçekten yuvarlak yap (arkadaki çıktı köşelerden görünsün)
            using (var path = Pill(new Rectangle(0, 0, Width, Height)))
                Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Pill(r))
            using (var br = new SolidBrush(_hover ? Theme.AccentFillHover : Theme.AccentFill))
                g.FillPath(br, path);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static GraphicsPath Pill(Rectangle r)
        {
            int d = Math.Max(2, r.Height);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }
    }
}
