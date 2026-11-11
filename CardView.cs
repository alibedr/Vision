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
            // Kaydırma çubukları her zaman görünür: uzun satır gelince yatay çubuk belirip görünür alanı kısaltmaz,
            // son satır onun altında kalmaz ve alan veriye göre değişmediği için ekran sıçramaz
            ScrollBars = RichTextBoxScrollBars.ForcedBoth,
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
        private readonly IconButton _historyBtn = new IconButton(IconKind.History, IconTone.Ghost) { IconSize = 14, TabStop = false };
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
            Theme.Changed += OnThemeChanged;
            BackColor = Theme.Surface;
            Padding = new Padding(1);   // 1px kenarlık için
            MinimumSize = new Size(260, 160);

            BuildHeader();
            BuildInput();
            BuildOutput();
            BuildActions();
            BuildSentinel();
            BuildHighlightMenu();
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
            _output.HandleCreated += (s, e) => { RecolorKeepingView(); if (_following) ScrollToEnd(); };
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
            _clearBtn.Click += (s, e) => ClearTerminal();
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
            _output.VScroll += (s, e) => { TrackFollow(); CheckReachedBottom(); };
            _output.KeyUp += (s, e) => { TrackFollow(); CheckReachedBottom(); };
            _output.MouseUp += (s, e) => { TrackFollow(); CheckReachedBottom(); };
            // Kart penceresi boyut değiştirince (düzen, pencere) akış izleniyorsa en alta inilir. Yalnız dış panelin
            // boyutu izlenir: kaydırma çubukları kalıcı olduğundan metin kutusunun alanı veriyle değişmez (döngü olmaz).
            _outputHost.Resize += (s, e) =>
            {
                if (!_following || _resizeScrollQueued || !_output.IsHandleCreated || !IsHandleCreated) return;
                _resizeScrollQueued = true;
                BeginInvoke((Action)(() =>
                {
                    _resizeScrollQueued = false;
                    if (!IsDisposed && _output.IsHandleCreated && _following) ScrollToEnd();
                }));
            };
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
            // Komut alanının sağında geçmiş düğmesi: yazılmış tüm komutlar + geriye doğru arama (Ctrl+R)
            _historyBtn.Dock = DockStyle.Right;
            _historyBtn.Width = 24;
            _historyBtn.SetTip(_tips, "Komut geçmişi (Ctrl+R) — daha önce yazılan tüm komutları listeler ve içinde arar;\nseçilen komut komut satırına yazılır (gönderilmez)");
            _historyBtn.Click += (s, e) => ShowHistory();
            field.Controls.Add(_input);
            field.Controls.Add(_prompt);
            field.Controls.Add(_historyBtn);

            _sendBtn.Dock = DockStyle.Right;
            _sendBtn.Width = 36;
            _sendBtn.SetTip(_tips, "Gönder (Enter)");
            var gap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };

            // Sağdan sola: [Gönder] [reboot ⇄] [⚡ hızlı komut] [Sentinel] [komut satırı]
            _quickBtn.Dock = DockStyle.Right;
            _rebootBtn.Dock = DockStyle.Right;
            _inputBar.Controls.Add(field);
            _inputBar.Controls.Add(_sentinelGap);
            _inputBar.Controls.Add(_sentinelBtn);
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
            // Ortak, kalıcı komut geçmişinden otomatik tamamlama (Tab / ↓ / → ile kabul; çalıştırmaz)
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
            RefreshSentinelButton();
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
            _historyBtn.Enabled = st == CardState.Connected;   // bağlı değilken komut satırı yazılamaz
            RefreshActionButtons();
            _prompt.ForeColor = st == CardState.Connected ? Theme.Accent : Theme.TextFaint;

            // Bağlı değilken kart ekranda olsa da bunu açıkça göster
            _stateStrip.Visible = st != CardState.Connected;
            switch (st)
            {
                case CardState.Connecting:
                    _stateStrip.Text = "●  Bağlanıyor…";
                    _stateStrip.BackColor = Theme.StripConnecting;
                    _stateStrip.ForeColor = Theme.Warning;
                    break;
                case CardState.Error:
                    _stateStrip.Text = "●  Bağlantı yok: " + (Session.LastError ?? "hata") + "  —  komut gönderilemez";
                    _stateStrip.BackColor = Theme.StripError;
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
                _stateStrip.BackColor = Theme.StripDropped;
                _stateStrip.ForeColor = Color.White;
            }
            ApplyLook(st == CardState.Connected, drop);
            ApplyPendingInput(st);
            _header.Invalidate();
            Invalidate();   // kenarlık rengi
        }

        // ---------- Çıktı ve kaydırma ----------

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const int WM_SETREDRAW = 0x000B;
        private const int EM_GETSCROLLPOS = 0x0400 + 221;
        private const int WM_VSCROLL = 0x0115;
        private const int SB_BOTTOM = 7;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;
        private const int WM_HSCROLL = 0x0114;
        private const int SB_THUMBPOSITION = 4;

        /// <summary>
        /// Görünümün konumu: dikeyde ilk görünen SATIR, yatayda piksel. RichEdit'in EM_GETSCROLLPOS / EM_SETSCROLLPOS ve
        /// kaydırma çubuğu değerleri 16 bittir: belge 65.535 pikseli geçince (ör. ~4.000+ satır) dikey konum orantılı
        /// sıkıştırılıyor, geri yüklenince ekran en üste ya da en alta atlıyordu. Satır numarası bu sınırdan etkilenmez.
        /// </summary>
        private struct ViewState
        {
            public int FirstLine;
            public int X;
        }

        private ViewState SaveView()
        {
            IntPtr h = _output.Handle;
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);   // yalnız yatay değer kullanılır
            return new ViewState
            {
                FirstLine = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero),
                X = pos.X,
            };
        }

        /// <summary>Kaydedilen satırı yeniden ilk görünen satır yapar ve yatay konumu geri alır.</summary>
        private void RestoreView(ViewState v)
        {
            IntPtr h = _output.Handle;
            int current = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            int target = Math.Max(0, v.FirstLine);
            if (current != target) SendMessage(h, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(target - current));
            SetHorizontal(v.X);
        }

        /// <summary>Yalnız yatay konumu değiştirir (dikey konuma dokunmaz).</summary>
        private void SetHorizontal(int x)
        {
            IntPtr h = _output.Handle;
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            if (pos.X == x) return;
            int px = Math.Max(0, Math.Min(0xFFFF, x));
            SendMessage(h, WM_HSCROLL, (IntPtr)((px << 16) | SB_THUMBPOSITION), IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref POINT lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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

        private bool _following = true;   // kullanıcı en altta: akış izleniyor (yalnız kullanıcının kaydırması değiştirir)
        private bool _programScroll;      // program ekliyor / kaydırıyor: kaydırma olayları kullanıcıdan gelmiyor
        private bool _resizeScrollQueued;

        /// <summary>
        /// Tüm metnin renklerini yeniden uygular (uyarı satırları, renklendirmeler) ama ekranı OYNATMAZ: çizim kapalıyken
        /// yapılır, kaydırma konumu ve seçim geri alınır. Eskiden satırlar tek tek seçilirken ekran baştan sona kayıyordu
        /// (ör. bağlantı kesilip kart griye dönerken ekran "akıyor" gibi görünüyordu).
        /// </summary>
        private void RecolorKeepingView(Action before = null)
        {
            if (!_output.IsHandleCreated) { if (before != null) before(); return; }
            IntPtr h = _output.Handle;
            ViewState view = SaveView();
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            bool wasProgram = _programScroll;
            _programScroll = true;
            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                if (before != null) before();
                ColorizeAlerts(0, null);
                ApplyHighlights(0, null);
                _output.Select(selStart, selLen);
                RestoreView(view);
            }
            finally
            {
                SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _programScroll = wasProgram;
                _output.Invalidate();
            }
        }

        /// <summary>Kullanıcı kaydırınca akışı izleyip izlemediği güncellenir (programın kaydırmaları sayılmaz).</summary>
        private void TrackFollow()
        {
            if (_programScroll) return;
            _following = IsAtBottom();
        }

        private void AppendOutputCore(string text, bool follow)
        {
            bool wasProgram = _programScroll;
            _programScroll = true;
            try
            {
                AppendOutputInner(text, follow);
            }
            finally
            {
                _programScroll = wasProgram;
            }
            _following = follow;
        }

        private void AppendOutputInner(string text, bool follow)
        {
            // Ekleme ve kırpma tek seferde, çizim kapalıyken yapılır (satır sınırı aşıldığında titreme / gereksiz çizim yok)
            IntPtr h = _output.Handle;
            // Konum her zaman EKLEMEDEN ÖNCE alınır: akış izlenirken de yatay konum (view.X) korunur
            ViewState view = SaveView();
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;

            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                int start = PrepareAppend();
                _output.AppendText(text);
                ColorizeAlerts(start, text);
                ApplyHighlights(start, text);                  // kullanıcının renklendirdiği metinler (sağ tık → Renklendir)
                int removed = TrimToLimit(!follow, ref view);  // en eski satırlar silinir, en yeniler kalır

                if (follow)
                {
                    // En alta inme çizim açıldıktan sonra yapılır (aşağıda); burada yalnız seçim sona alınır
                }
                else
                {
                    // Kullanıcı eski çıktıyı okuyor: seçim ve kaydırma konumu (silinen kısım kadar kaydırılarak) korunur
                    selStart = Math.Max(0, selStart - removed);
                    if (removed > 0 && selLen > 0 && selStart == 0) selLen = 0;
                    _output.Select(selStart, selLen);
                    RestoreView(view);
                }
            }
            finally
            {
                SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _output.Invalidate();
            }
            // Akış izleniyorsa: çizim açıldıktan sonra (kaydırma ölçüleri güncel) yalnız dikeyde en alta inilir; yatay
            // konum eklemeden önceki yerinde kalır. (Eskiden imleç gelen uzun satırın sonuna gittiği için kutu sağa
            // kayıyor, satır sonu gelince başa dönüyordu.)
            if (follow) ScrollToEnd(view.X);
        }

        /// <summary>Satır sınırı değiştiğinde mevcut çıktıyı hemen yeni sınıra kırpar (konum korunur).</summary>
        private void ApplyLineLimitNow()
        {
            _appliedMaxLines = Session.MaxLines;
            Session.ApplyLineLimit();
            if (!IsHandleCreated || _output.TextLength == 0) return;
            bool follow = IsAtBottom();
            IntPtr h = _output.Handle;
            ViewState view = SaveView();
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                int removed = TrimToLimit(!follow, ref view);
                if (follow) ScrollToEnd(view.X);
                else
                {
                    _output.Select(Math.Max(0, selStart - removed), removed > 0 && selStart - removed <= 0 ? 0 : selLen);
                    RestoreView(view);
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
        /// keepPosition: kullanıcı yukarıdaysa ilk görünen satır silinen satır sayısı kadar düzeltilir. Silinen karakter sayısını döndürür.
        /// </summary>
        private int TrimToLimit(bool keepPosition, ref ViewState view)
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

            // Silinen satır sayısı (kesim her zaman bir satır başında ya da satırın ortasındaysa o satıra kadar)
            int removedLines = _output.GetLineFromCharIndex(cut);

            _output.ReadOnly = false;
            _output.Select(0, cut);
            _output.SelectedText = "";
            _output.ReadOnly = true;

            if (keepPosition) view.FirstLine = Math.Max(0, view.FirstLine - removedLines);
            return cut;
        }

        /// <summary>Kartın görünen adı değişince başlığı günceller ("IP" ya da "IP (Ad)").</summary>
        public void RefreshTitle()
        {
            if (IsHandleCreated) RefreshState();   // alt satırdaki grup adı (grup yeniden adlandırılmış olabilir)
            _title.Text = Session.Card.Label;
            _tips.SetToolTip(_title, Session.Card.Label + "  ·  " + Session.Card.DisplayName);
            if (_dropBanner.Visible) _shownDrop = null;   // uyarı kutusu yeni adla yeniden kurulsun
        }

        /// <summary>
        /// Son satır görünüyor mu ("en altta")? Satır numarasıyla ölçülür: kaydırma çubuğu değerleri 16 bit olduğundan
        /// uzun çıktıda (65.535 pikselden yüksek) orantılı sıkıştırılıyor, bir satırlık pay gerçekte 8–10 satır
        /// ediyordu; kullanıcı birkaç satır yukarı çıksa da "en altta" sayılıp aşağı çekiliyordu.
        /// </summary>
        private bool IsAtBottom()
        {
            if (!IsHandleCreated || _output.TextLength == 0) return true;
            int lastLine = _output.GetLineFromCharIndex(_output.TextLength);
            int bottomChar = _output.GetCharIndexFromPosition(new Point(0, Math.Max(0, _output.ClientSize.Height - 1)));
            int lastVisibleLine = _output.GetLineFromCharIndex(bottomChar);
            // Metin genelde satır sonuyla biter (son satır boş): son dolu satır görünüyorsa da en altta sayılır
            return lastVisibleLine >= lastLine - 1;
        }

        /// <summary>En alta (en yeni satıra) iner; yatay kaydırma konumu değişmez.</summary>
        private void ScrollToEnd()
        {
            ScrollToEnd(null);
        }

        /// <summary>
        /// En alta iner. İmleç metnin sonuna taşınıp görünür yapılır (dikey); bu sırada RichTextBox imleci göstermek
        /// için yatayda da kayar. Uzun satırlar kutudan genişse kutu her yeni parçada sağa, her satır sonunda sola
        /// atlıyordu. Bu yüzden yatay konum keepX'e (verilmezse çağrı anındaki konuma) geri alınır: hangi satırın
        /// sonuna bakılacağına kullanıcı yatay kaydırma çubuğuyla karar verir.
        /// </summary>
        private void ScrollToEnd(int? keepX)
        {
            if (!_output.IsHandleCreated)
            {
                _output.SelectionStart = _output.TextLength;
                _output.SelectionLength = 0;
                _output.ScrollToCaret();
                return;
            }
            IntPtr h = _output.Handle;
            bool wasProgram = _programScroll;
            _programScroll = true;
            try
            {
                var before = new POINT();
                SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref before);   // yalnız yatay değer kullanılır
                int x = keepX ?? before.X;

                _output.SelectionStart = _output.TextLength;
                _output.SelectionLength = 0;
                _output.ScrollToCaret();
                // ScrollToCaret imleç satırını kısmen görünür sayabilir ve yatay kaydırma çubuğunun kapladığı yeri
                // hesaba katmaz: dikey kaydırma çubuğuyla gerçekten en alta inilir (son satır tam görünür)
                SendMessage(h, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);

                // Yatay geri alınır; dikey (en alt) değişmez. EM_SETSCROLLPOS kullanılmaz: dikey değeri 16 bit olduğundan
                // uzun çıktıda ekranı yukarıya atıyordu.
                SetHorizontal(x);
            }
            finally
            {
                _programScroll = wasProgram;
            }
            _following = true;
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

        /// <summary>Kartın terminal ekranını ve geçmişini temizler (kart silgisi ve genel silgi aynı işi yapar).</summary>
        public void ClearTerminal()
        {
            Session.ClearHistory();
            _output.Clear();
            HideNewLinesPill();
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

        /// <summary>Tema değişti: bağlı / bağlı değil renkleri yeni temayla yeniden uygulanır, uyarı satırları yeniden boyanır.</summary>
        private void OnThemeChanged()
        {
            if (IsDisposed) return;
            _shownConnectedLook = null;
            RefreshState();
            _shownSentinelVersion = -1;      // simge rengi yeni temadan (bildirim gösterilmez)
            RefreshSentinelButton(true);
        }

        // Bağlı değilken kullanılan gri tonlar (bağlı kartın renkleri hiç değişmez)
        private static Color OffOutputBack { get { return Theme.OffOutputBack; } }
        private static Color OffOutputText { get { return Theme.OffOutputText; } }   // gri ama okunur (≥ 4.5:1)
        private static Color OffHeaderBack { get { return Theme.OffHeaderBack; } }
        private static Color OffInputBack { get { return Theme.OffInputBack; } }
        private static Color AlertText { get { return Theme.AlertText; } }

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
                // Tüm metin yeniden renklenir (yazı rengi, kırmızı uyarı satırları, renklendirmeler); ekran kaymaz
                RecolorKeepingView(() =>
                {
                    _output.BackColor = outBack;
                    _output.ForeColor = outText;
                });
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
                Theme.SetCueBanner(_input, connected ? "Komut yazıp Enter'a basın  (↑/↓ geçmiş · Ctrl+R ara)" : "Kart bağlı değil — komut yazılamaz");
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
            // Önceki satır kırmızıysa ya da metin renklendirilmiş bir kelimeyle bitiyorsa yeni metin o rengi / zemini devralmasın
            SetCharColors(false, _output.ForeColor, Color.Empty);
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
            OpenBootChangeIfNeeded(cmd);
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

        /// <summary>
        /// bspBootChange / bootChange gönderilmeden hemen önce Boot ayarları penceresini açar (G27): kartın sorduğu alanlar
        /// araya giren loglardan ayrı orada gösterilir. Kart zaten bir boot alanında bekliyorsa (yazılan bir alan değeridir)
        /// pencere açılmaz.
        /// </summary>
        private void OpenBootChangeIfNeeded(string cmd)
        {
            if (!BootChangeParser.IsBootChangeCommand(cmd) || Session.InteractiveInputActive) return;
            try { BootChangeDialog.Begin(Session, FindForm(), cmd); }
            catch (Exception ex) { ErrorText.LogTechnical(Session.Card.DisplayName + " boot ayarları penceresi", ex); }
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
            else if (e.KeyCode == Keys.R && e.Control && !e.Alt)
            {
                // Ctrl+R: geriye doğru arama — yazılan metinle başlar
                e.SuppressKeyPress = true;
                e.Handled = true;
                ShowHistory();
            }
            else if (e.KeyCode == Keys.C && e.Control && _input.SelectionLength == 0 && Session.State == CardState.Connected)
            {
                // Ctrl+C: karta kesme (ETX) gönder
                e.SuppressKeyPress = true;
                try { await Session.SendRawAsync("\x03"); } catch { }
            }
        }

        private string _pendingInput;   // kart bağlanınca komut satırına yazılacak komut (kütüphaneden aktarma)

        /// <summary>
        /// Komutu komut satırına yazar (GÖNDERMEZ) ve odaklanır — Komut Kütüphanesi'nden "Karta aktar".
        /// Kart bağlı değilse bağlanmayı başlatır; komut bağlantı kurulunca yazılır (kurulamazsa vazgeçilir).
        /// </summary>
        public void InsertCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            if (Session.State == CardState.Connected)
            {
                _pendingInput = null;
                Session.CommandHistory.ResetNavigation();
                SetInput(command);
                _input.Focus();
                ShowToast("✓  Kütüphaneden alındı — düzenleyip Enter ile gönderin.", true, null);
                return;
            }
            _pendingInput = command;
            ShowToast("ℹ  Kart bağlı değil — bağlanılıyor; bağlanınca komut satırına yazılacak.", true, null);
            if (!SelectionLogic.IsLive(Session.State)) ConnectForPending();
        }

        private async void ConnectForPending()
        {
            try { await Session.ConnectAsync(); }
            catch (Exception ex) { ErrorText.LogTechnical(Session.Card.DisplayName + " bağlantı", ex); }
        }

        /// <summary>Durum değişince bekleyen (kütüphaneden aktarılan) komutu yazar ya da bağlantı kurulamadıysa bırakır.</summary>
        private void ApplyPendingInput(CardState st)
        {
            if (_pendingInput == null) return;
            if (st == CardState.Connected)
            {
                string cmd = _pendingInput;
                _pendingInput = null;
                Session.CommandHistory.ResetNavigation();
                SetInput(cmd);
                _input.Focus();
                ShowToast("✓  Bağlandı — komut yazıldı; düzenleyip Enter ile gönderin.", true, null);
            }
            else if (st == CardState.Error || st == CardState.Disconnected)
            {
                _pendingInput = null;
                ShowToast("⚠  Karta bağlanılamadı — komut yazılmadı.", false, null);
            }
        }

        /// <summary>Komut geçmişi penceresini açar; seçilen komut komut satırına yazılır (gönderilmez).</summary>
        private void ShowHistory()
        {
            string query = _input.SelectionLength > 0 ? _input.SelectedText : _input.Text;
            string chosen = CommandHistoryDialog.Pick(FindForm(), Session.CommandHistory, query, Session.Card.Label);
            if (chosen == null || Session.State != CardState.Connected) return;
            Session.CommandHistory.ResetNavigation();
            SetInput(chosen);
            _input.Focus();
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
                Theme.Changed -= OnThemeChanged;
                Session.DetachView();
                _tips.Dispose();
                _toastTimer.Dispose();
                _rebootCooldown.Dispose();
                _quickMenu.Dispose();
                if (_alertFont != null) _alertFont.Dispose();
                DisposeHighlightResources();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Bağlantı kopunca çıktının üstünde görünen kırmızı uyarı kutusu. Tıklanınca gizlenir.</summary>
    internal sealed class AlertBanner : Control
    {
        private static Color Back { get { return Theme.ErrorBanner; } }
        private static Color Edge { get { return Theme.IsDark ? Theme.Danger : Color.FromArgb(255, 170, 170); } }
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
                Theme.DropBannerTitle, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            y += Font.Height + 6;
            TextRenderer.DrawText(g, "Kartın ekranı ve geçmişi korunuyor. Kapatmak için tıklayın.", Theme.UiSmall,
                new Point(x, y), Theme.DropBannerFaint, TextFormatFlags.NoPrefix);
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
