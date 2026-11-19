using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Seri sekmesindeki bir COM portu penceresi. İki yoldan yazılır (1.10):
    ///  • Alttaki komut satırı (Telnet'teki gibi): komut yazılıp düzenlenir, Enter ile satır karta gider; ↑ / ↓,
    ///    fare tekerleği, Ctrl+R ile seri komut geçmişi, yazarken öneri.
    ///  • Terminale tıklayıp doğrudan (TeraTerm gibi): her tuş anında porta gider (Enter = CR, Backspace, Tab, Esc, oklar,
    ///    F1–F12, Home / End / PgUp / PgDn / Insert / Delete, Ctrl+harf).
    /// Kopyalama: fareyle seçilen metin kendiliğinden panoya alınır, sağ tık → Kopyala; yapıştırma: sağ tık → Yapıştır
    /// ya da Shift+Insert. Yukarıdaki bir yere bakarken gelen çıktı ekranı kaydırmaz ("↓ Yeni Satırlar" göstergesi).
    /// Kapatmak portu da kapatır.
    /// </summary>
    public sealed class SerialTerminalView : UserControl
    {
        private readonly Panel _header = new Panel { Dock = DockStyle.Top, Height = 38 };
        private readonly Label _title = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _subtitle = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly IconButton _connectBtn = new IconButton(IconKind.Connect, IconTone.Success);
        private readonly IconButton _historyBtn = new IconButton(IconKind.History, IconTone.Ghost);
        private readonly IconButton _saveBtn = new IconButton(IconKind.Save, IconTone.Ghost);
        private readonly IconButton _clearBtn = new IconButton(IconKind.Clear, IconTone.Ghost);
        private readonly IconButton _settingsBtn = new IconButton(IconKind.Settings, IconTone.Ghost);
        private readonly IconButton _maxBtn = new IconButton(IconKind.Maximize, IconTone.Ghost);
        private readonly IconButton _closeBtn = new IconButton(IconKind.Close, IconTone.Ghost);
        private readonly Panel _outputHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 6, 4, 4) };
        private readonly SerialTerminalBox _output = new SerialTerminalBox();
        private readonly Label _strip = new Label { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Font = Theme.UiSmall };
        // Alttaki komut satırı (Telnet'teki gibi)
        private readonly Panel _inputBar = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 6, 8, 6) };
        private readonly Panel _field = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 6, 2) };
        private readonly Label _prompt = Theme.MakeLabel("›", Theme.UiBold, Theme.Accent);
        private readonly TextBox _input = new TextBox { BorderStyle = BorderStyle.None, Font = Theme.Mono };
        private readonly IconButton _sendBtn = new IconButton(IconKind.Send, IconTone.Primary);
        private InputAutoComplete _autoComplete;
        // Yukarıdayken gelen çıktı: ekran yerinde kalır, gösterge çıkar (Telnet'teki gibi)
        private readonly PillButton _newLinesPill = new PillButton { Visible = false };
        private readonly NewLinesIndicator _indicator = new NewLinesIndicator();
        private Size _pillSize;
        private bool _programScroll;
        // "Dosyaya kaydet" bildirimi
        private readonly GlyphLabel _toast = new GlyphLabel { Visible = false, AutoSize = true, Padding = new Padding(10, 5, 12, 5), Font = Theme.UiSmall, Cursor = Cursors.Hand };
        private readonly Timer _toastTimer = new Timer { Interval = 5000 };
        private string _toastPath;
        private readonly ToolTip _tips = new DarkToolTip();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly SerialScreen _screen = new SerialScreen();
        private readonly Timer _timer = new Timer { Interval = 50 };
        private int _presentTick;
        private bool _typed;            // son eklemeden beri kullanıcı yazdı: yankı gelince en altta kalınır
        private bool _selected, _maximized;

        public SerialTerminalView(SerialSession session)
        {
            Session = session;
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            Padding = new Padding(1);
            MinimumSize = new Size(260, 160);

            BuildHeader();
            _output.Dock = DockStyle.Fill;
            // Yazınca (TeraTerm gibi) en alta inilir; kartın yankısı da en altta görünür
            _output.KeysTyped = s => { _typed = true; if (!IsAtBottom()) ScrollToEnd(); Session.SendKeys(s); };
            _output.PasteRequested = Paste;
            _output.ContextMenuStrip = _menu;
            _outputHost.Controls.Add(_toast);          // önce eklenenler öne çizilir → çıktının üstünde yüzer
            _outputHost.Controls.Add(_newLinesPill);
            _outputHost.Controls.Add(_output);
            Theme.ApplyDarkScrollBars(_output);
            BuildMenu();
            BuildInput();
            BuildOverlays();

            Controls.Add(_outputHost);
            Controls.Add(_inputBar);
            Controls.Add(_strip);
            Controls.Add(_header);

            Session.StateChanged += OnSessionStateChanged;
            _timer.Tick += (s, e) => Pump();
            _timer.Start();
            RefreshState();
            WireSelection(this);
        }

        public SerialSession Session { get; private set; }

        public event Action<SerialTerminalView> CloseRequested;
        public event Action<SerialTerminalView> MaximizeToggled;
        public event Action<SerialTerminalView> Activated;
        public event Action<SerialTerminalView> SettingsRequested;

        public bool Selected
        {
            get { return _selected; }
            set { if (_selected != value) { _selected = value; Invalidate(); } }
        }

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

        public void FocusTerminal() { _output.Focus(); }

        /// <summary>Terminalin kendisi odakta (doğrudan yazma): F tuşları uygulamaya değil karta gider.</summary>
        public bool TerminalFocused { get { return _output.Focused; } }

        // ---------- Oluşturma ----------

        private void BuildHeader()
        {
            _header.BackColor = Theme.Surface;
            _header.Paint += (s, e) =>
            {
                Color dot = Session.State == SerialState.Open ? Theme.Success : Session.State == SerialState.Error ? Theme.Danger : Theme.TextFaint;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(dot)) e.Graphics.FillEllipse(b, 10, 15, 9, 9);
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };
            _title.AutoSize = false;
            _title.AutoEllipsis = true;
            _title.Height = 17;
            _title.Location = new Point(26, 4);
            _subtitle.AutoSize = false;
            _subtitle.AutoEllipsis = true;
            _subtitle.Height = 16;
            _subtitle.Location = new Point(27, 20);

            var buttons = new IconButton[] { _connectBtn, _saveBtn, _clearBtn, _settingsBtn, _maxBtn, _closeBtn };
            foreach (var b in buttons) { b.Height = 26; b.Width = 28; b.Anchor = AnchorStyles.Top | AnchorStyles.Right; }
            _saveBtn.SetTip(_tips, "Dosyaya kaydet — ekrandaki çıktıyı logs\\SeriKanalLoglari klasörüne kaydeder\n(her tıklama yeni dosya)");
            _clearBtn.SetTip(_tips, "Temizle — ekranı temizler (onay sorar)");
            _settingsBtn.SetTip(_tips, "Port ayarları (baud rate, data, parity, stop, akış denetimi, gecikme)");
            _closeBtn.SetTip(_tips, "Kapat — pencereyi ve portu kapatır");
            IsMaximized = false;

            _header.Controls.AddRange(new Control[] { _title, _subtitle });
            _header.Controls.AddRange(buttons);
            _header.Resize += (s, e) => LayoutHeader();
            LayoutHeader();

            _connectBtn.Click += (s, e) =>
            {
                if (Session.State == SerialState.Open) Session.Close(); else Session.Open();
                _output.Focus();
            };
            _saveBtn.Click += async (s, e) => await SaveSnapshotAsync();
            _clearBtn.Click += (s, e) => { AskClear(); _output.Focus(); };
            _settingsBtn.Click += (s, e) => { var h = SettingsRequested; if (h != null) h(this); };
            _maxBtn.Click += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _closeBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(this); };
            _header.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _title.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
        }

        private void LayoutHeader()
        {
            int w = _header.Width;
            _saveBtn.Visible = w >= 280;
            int x = w - 6;
            x -= _closeBtn.Width; _closeBtn.Location = new Point(x, 6);
            x -= _maxBtn.Width + 2; _maxBtn.Location = new Point(x, 6);
            x -= _settingsBtn.Width + 2; _settingsBtn.Location = new Point(x, 6);
            x -= _clearBtn.Width + 2; _clearBtn.Location = new Point(x, 6);
            if (_saveBtn.Visible) { x -= _saveBtn.Width + 2; _saveBtn.Location = new Point(x, 6); }
            x -= _connectBtn.Width + 2; _connectBtn.Location = new Point(x, 6);
            _title.Width = Math.Max(40, x - _title.Left - 8);
            _subtitle.Width = Math.Max(40, x - _subtitle.Left - 8);
        }

        private void BuildMenu()
        {
            _menu.Renderer = new DarkMenuRenderer();
            var copy = new ToolStripMenuItem("Kopyala", null, (s, e) => CopySelection());
            var paste = new ToolStripMenuItem("Yapıştır  (Shift+Insert)", null, (s, e) => Paste());
            var all = new ToolStripMenuItem("Tümünü seç", null, (s, e) => _output.SelectAll());
            var clear = new ToolStripMenuItem("Temizle", null, (s, e) => BeginInvoke(new Action(AskClear)));
            _menu.Items.AddRange(new ToolStripItem[] { copy, paste, new ToolStripSeparator(), all, clear });
            _menu.Opening += (s, e) =>
            {
                copy.Enabled = _output.SelectionLength > 0;
                paste.Enabled = Session.State == SerialState.Open && Clipboard.ContainsText();
            };
        }

        // ---------- Durum ----------

        private void OnSessionStateChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(RefreshState)); } catch { }
        }

        public void RefreshState()
        {
            if (IsDisposed) return;
            SerialPortProfile p = Session.Profile;
            _title.Text = p.Label;
            _subtitle.Text = p.PortName + "  ·  " + p.Summary;
            SerialState st = Session.State;
            bool open = st == SerialState.Open;
            _connectBtn.Icon = open ? IconKind.Disconnect : IconKind.Connect;
            _connectBtn.Tone = open ? IconTone.Danger : IconTone.Success;
            _connectBtn.SetTip(_tips, open ? "Portu kapat" : "Portu aç (bağlan)");
            _settingsBtn.Enabled = !open;
            ApplyInputLook(open);
            if (open)
            {
                _strip.Text = "●  Açık — aşağıya komut yazın ya da terminale tıklayıp doğrudan yazın";
                _strip.BackColor = Theme.Surface;
                _strip.ForeColor = Theme.Success;
            }
            else if (st == SerialState.Error)
            {
                _strip.Text = "✕  " + (Session.LastError ?? "Hata");
                _strip.BackColor = Theme.StripError;
                _strip.ForeColor = Theme.Danger;
            }
            else
            {
                _strip.Text = "○  Kapalı — bağlanmak için başlıktaki bağlan düğmesine basın";
                _strip.BackColor = Theme.Surface;
                _strip.ForeColor = Theme.TextMuted;
            }
            _tips.SetToolTip(_strip, _strip.Text);
            _header.Invalidate();
        }

        // ---------- Ekran ----------

        private void Pump()
        {
            if (IsDisposed) return;
            string raw = Session.TakePending();
            if (raw != null) AppendRaw(raw);
            if (Session.State == SerialState.Open && ++_presentTick >= 40)   // ~2 sn'de bir: port kayboldu mu?
            {
                _presentTick = 0;
                Session.CheckPresent();
            }
        }

        /// <summary>
        /// Gelen çıktıyı ekrana işler. Kullanıcı en alttaysa (ya da az önce yazdıysa) akış izlenir: küçük eklemeleri denetim
        /// kendisi çizer (titremez). Kullanıcı yukarıdaki bir yere bakıyorsa ekran YERİNDE kalır: çizim kapalıyken eklenir,
        /// ilk görünen satır, yatay konum ve seçim geri alınır, "↓ Yeni Satırlar" göstergesi çıkar (1.10 — titreme
        /// düzeltmesinden sonra metin imlecin olduğu sona eklenince kutu kendiliğinden en alta kayıyordu).
        /// </summary>
        private void AppendRaw(string raw)
        {
            int lineLen = CurrentLineLength();
            List<SerialScreen.Op> ops = _screen.Process(raw, lineLen);
            if (ops.Count == 0) return;
            bool follow = _typed || IsAtBottom();   // kullanıcının konumu EKLEMEDEN ÖNCE
            _typed = false;
            int newLines = 0;
            foreach (SerialScreen.Op o in ops) if (o.Text != null) foreach (char c in o.Text) if (c == '\n') newLines++;
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;

            _indicator.BeginAppend();
            bool wasProgram = _programScroll;
            _programScroll = true;
            try
            {
                if (follow)
                {
                    // Büyük log yığını ya da satır kırpma: çizim kapatılır, sonda bir kez çizilir
                    bool bulk = raw.Length > 512 || NeedsTrim();
                    if (bulk) _output.BeginUpdateNoRedraw();
                    try
                    {
                        ApplyOps(ops);
                        if (NeedsTrim()) TrimLines();
                        _output.Select(_output.TextLength, 0);
                        if (!IsScrolledToBottom()) ScrollToEnd();
                    }
                    finally
                    {
                        if (bulk) _output.EndUpdateNoRedraw();
                    }
                }
                else
                {
                    ViewState view = SaveView();
                    _output.BeginUpdateNoRedraw();
                    try
                    {
                        ApplyOps(ops);
                        int removedChars = 0, removedLines = 0;
                        if (NeedsTrim()) TrimLines(out removedChars, out removedLines);
                        view.FirstLine = Math.Max(0, view.FirstLine - removedLines);
                        int s = Math.Max(0, selStart - removedChars);
                        if (removedChars > selStart) selLen = 0;
                        _output.Select(Math.Min(s, _output.TextLength), Math.Min(selLen, Math.Max(0, _output.TextLength - s)));
                        RestoreView(view);
                    }
                    finally
                    {
                        _output.EndUpdateNoRedraw();
                    }
                }
            }
            finally
            {
                _programScroll = wasProgram;
                _indicator.EndAppend(newLines, follow);
                SyncPill();
            }
        }

        private void ApplyOps(List<SerialScreen.Op> ops)
        {
            foreach (SerialScreen.Op o in ops)
            {
                if (o.Erase > 0)
                {
                    int n = Math.Min(o.Erase, CurrentLineLength());
                    if (n > 0)
                    {
                        _output.Select(_output.TextLength - n, n);
                        _output.SelectedText = "";
                    }
                }
                if (!string.IsNullOrEmpty(o.Text))
                {
                    _output.Select(_output.TextLength, 0);
                    _output.SelectedText = o.Text;
                }
            }
        }

        // ---------- Görünüm konumu (Telnet penceresindeki gibi: satır numarası + yatay piksel) ----------

        private const int EM_GETSCROLLPOS = 0x0400 + 221;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;
        private const int WM_HSCROLL = 0x0114;
        private const int SB_THUMBPOSITION = 4;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private struct ViewState { public int FirstLine; public int X; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref POINT lParam);

        private ViewState SaveView()
        {
            if (!_output.IsHandleCreated) return new ViewState();
            IntPtr h = _output.Handle;
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            return new ViewState { FirstLine = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero), X = pos.X };
        }

        private void RestoreView(ViewState v)
        {
            if (!_output.IsHandleCreated) return;
            IntPtr h = _output.Handle;
            int current = (int)SendMessage(h, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            int target = Math.Max(0, v.FirstLine);
            if (current != target) SendMessage(h, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(target - current));
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            if (pos.X != v.X)
            {
                int px = Math.Max(0, Math.Min(0xFFFF, v.X));
                SendMessage(h, WM_HSCROLL, (IntPtr)((px << 16) | SB_THUMBPOSITION), IntPtr.Zero);
            }
        }

        private const int WM_VSCROLL = 0x0115;
        private const int SB_BOTTOM = 7;
        private const int SB_VERT = 1;
        private const int SIF_RANGE = 0x1, SIF_PAGE = 0x2, SIF_POS = 0x4;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetScrollInfo(IntPtr hWnd, int bar, ref SCROLLINFO info);

        /// <summary>Dikey çubuk zaten en altta mı (o zaman kaydırılmaz; her tuşta kaydırmak satırı titretiyordu).</summary>
        private bool IsScrolledToBottom()
        {
            if (!_output.IsHandleCreated) return true;
            var si = new SCROLLINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_RANGE | SIF_PAGE | SIF_POS };
            if (!GetScrollInfo(_output.Handle, SB_VERT, ref si)) return true;
            if (si.nPage <= 0 || si.nMax - si.nMin + 1 <= si.nPage) return true;   // kaydırılacak kadar metin yok
            return si.nPos + si.nPage >= si.nMax;
        }

        /// <summary>
        /// En alta iner. ScrollToCaret imleç satırını yatay kaydırma çubuğunun altında, yarım görünür bırakabiliyordu;
        /// dikey çubukla gerçekten en alta inilir (telnet penceresindeki gibi), son satır tam görünür.
        /// </summary>
        private void ScrollToEnd()
        {
            bool wasProgram = _programScroll;
            _programScroll = true;
            _output.BeginUpdateNoRedraw();
            try
            {
                _output.Select(_output.TextLength, 0);
                _output.ScrollToCaret();
                if (_output.IsHandleCreated) SendMessage(_output.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
            }
            finally
            {
                _output.EndUpdateNoRedraw();
                _programScroll = wasProgram;
            }
            _indicator.Reset();
            SyncPill();
        }

        // Metnin tamamı kopyalanmadan (hızlı log akışında her 50 ms'de çağrılır) son satırın uzunluğu
        private int CurrentLineLength()
        {
            int len = _output.TextLength;
            if (len == 0) return 0;
            int start = _output.GetFirstCharIndexFromLine(_output.GetLineFromCharIndex(len));
            return start < 0 ? 0 : len - start;
        }

        /// <summary>
        /// Son satır görünüyor mu ("en altta")? Telnet penceresindeki gibi satır numarasıyla ölçülür (kaydırma çubuğu
        /// değerleri uzun çıktıda sıkıştırıldığı için piksel ölçüsü birkaç satır yukarıdayken de "altta" diyebiliyordu).
        /// </summary>
        private bool IsAtBottom()
        {
            if (!_output.IsHandleCreated || _output.TextLength == 0) return true;
            int lastLine = _output.GetLineFromCharIndex(_output.TextLength);
            int bottomChar = _output.GetCharIndexFromPosition(new Point(0, Math.Max(0, _output.ClientSize.Height - 1)));
            int lastVisibleLine = _output.GetLineFromCharIndex(bottomChar);
            return lastVisibleLine >= lastLine - 1;
        }

        // Sınır biraz aşılınca bir kerede kırpılır (her yeni satırda kırpıp ekranı yeniden çizmemek için)
        private const int TrimSlack = 500;

        private bool NeedsTrim()
        {
            return _output.GetLineFromCharIndex(_output.TextLength) + 1 > SerialSession.MaxLines + TrimSlack;
        }

        private void TrimLines()
        {
            int chars, lines;
            TrimLines(out chars, out lines);
        }

        /// <summary>En eski satırları siler; silinen karakter ve satır sayısını verir (yukarıdayken konum düzeltmesi için).</summary>
        private void TrimLines(out int removedChars, out int removedLines)
        {
            removedChars = removedLines = 0;
            int lines = _output.GetLineFromCharIndex(_output.TextLength) + 1;
            if (lines <= SerialSession.MaxLines) return;
            int cut = _output.GetFirstCharIndexFromLine(lines - SerialSession.MaxLines);
            if (cut <= 0) return;
            removedLines = lines - SerialSession.MaxLines;
            removedChars = cut;
            _output.Select(0, cut);
            _output.SelectedText = "";
        }

        // ---------- Kopyala / yapıştır / geçmiş / kaydet ----------

        /// <summary>Temizle: onay sorar — seri çıktının log dosyası yok, silinen ekran geri gelmez (varsayılan Vazgeç).</summary>
        private void AskClear()
        {
            int c = ChoiceDialog.Ask(FindForm(), "Ekranı temizle", Session.Profile.Label + " ekranı temizlensin mi?",
                "Ekrandaki çıktı silinir; seri çıktı ayrıca bir dosyaya yazılmadığı için geri alınamaz. Karta hiçbir şey gönderilmez.\n" +
                "Saklamak isterseniz önce \"Dosyaya kaydet\" ile kaydedin.",
                new[] { "Vazgeç", "Temizle" }, 0, 1);
            if (c == 1) _output.Clear();
        }

        private void CopySelection()
        {
            if (_output.SelectionLength == 0) return;
            try { Clipboard.SetText(_output.SelectedText.Replace("\n", "\r\n")); } catch { }
        }

        private void Paste()
        {
            if (Session.State != SerialState.Open) return;
            string t = null;
            try { if (Clipboard.ContainsText()) t = Clipboard.GetText(); } catch { }
            if (string.IsNullOrEmpty(t)) { _output.Focus(); return; }
            // Birden çok satır: her satır bir komut olarak çalışır — önce sorulur (TeraTerm'deki "Confirm change paste" gibi)
            string[] lines = t.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
            if (lines.Length > 1)
            {
                string preview = string.Join("\n", lines.Take(6).Select(l => l.Length > 90 ? l.Substring(0, 90) + "…" : l)) +
                                 (lines.Length > 6 ? "\n… (+" + (lines.Length - 6) + " satır)" : "");
                int c = ChoiceDialog.Ask(FindForm(), "Çok satırlı yapıştırma",
                    lines.Length + " satır karta gönderilecek; her satır Enter'la gider ve komut olarak çalışır.",
                    preview, new[] { "Vazgeç", "Gönder" }, 0, -1);
                if (c != 1) { _output.Focus(); return; }
            }
            Session.Paste(t);
            _output.Focus();
        }

        /// <summary>
        /// Seri komut geçmişi (komut satırındaki düğme ya da Ctrl+R): seçilen komut komut satırına yazılır (gönderilmez,
        /// Telnet'teki gibi); çift tık hemen gönderir.
        /// </summary>
        private void ShowHistory()
        {
            bool sendNow;
            string query = _input.SelectionLength > 0 ? _input.SelectedText : _input.Text;
            string cmd = CommandHistoryDialog.Pick(FindForm(), SerialHistoryStore.History, query, "Seri — " + Session.Profile.Label,
                                                   "Komut satırına yaz", out sendNow);
            if (cmd == null || Session.State != SerialState.Open) { _input.Focus(); return; }
            SerialHistoryStore.History.ResetNavigation();
            if (sendNow) SendLine(cmd);
            else SetInput(cmd);
            _input.Focus();
        }

        // ---------- Komut satırı ----------

        private void BuildInput()
        {
            _inputBar.BackColor = Theme.SurfaceHigh;
            _field.BackColor = Theme.Input;
            _prompt.Dock = DockStyle.Left;
            _prompt.AutoSize = false;
            _prompt.Width = 14;
            _prompt.TextAlign = ContentAlignment.MiddleLeft;
            _input.Dock = DockStyle.Fill;
            _input.BackColor = Theme.Input;
            _input.ForeColor = Theme.Text;
            // Komut alanının sağında geçmiş düğmesi (Telnet'teki gibi; üst başlıktaki düğmenin yerine)
            _historyBtn.Dock = DockStyle.Right;
            _historyBtn.Width = 24;
            _historyBtn.IconSize = 14;
            _historyBtn.TabStop = false;
            _historyBtn.SetTip(_tips, "Seri komut geçmişi (Ctrl+R) — seçilen komut komut satırına yazılır; çift tık hemen gönderir");
            _historyBtn.Click += (s, e) => ShowHistory();
            _field.Controls.Add(_input);
            _field.Controls.Add(_prompt);
            _field.Controls.Add(_historyBtn);

            _sendBtn.Dock = DockStyle.Right;
            _sendBtn.Width = 36;
            _sendBtn.SetTip(_tips, "Gönder (Enter) — satır karta gider (sonunda Enter)");
            var gap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };
            _inputBar.Controls.Add(_field);
            _inputBar.Controls.Add(gap);
            _inputBar.Controls.Add(_sendBtn);

            _input.KeyDown += Input_KeyDown;
            HistoryWheel.Attach(_input, () => Session.State == SerialState.Open,
                () => { string prev = SerialHistoryStore.History.Previous(_input.Text); if (prev != null) SetInput(prev); },
                () => { string next = SerialHistoryStore.History.Next(); if (next != null) SetInput(next); });
            _autoComplete = new InputAutoComplete(_input, () => SerialHistoryStore.History);
            _sendBtn.Click += (s, e) => { SendInput(); _input.Focus(); };
            _field.Click += (s, e) => _input.Focus();
        }

        private void ApplyInputLook(bool open)
        {
            Color back = open ? Theme.Input : Theme.OffInputBack;
            _input.ReadOnly = !open;
            _input.BackColor = back;
            _field.BackColor = back;
            _inputBar.BackColor = open ? Theme.SurfaceHigh : Theme.OffHeaderBack;
            _input.Cursor = open ? Cursors.IBeam : Cursors.No;
            _sendBtn.Enabled = open;
            Theme.SetCueBanner(_input, open ? "Komut yazıp Enter'a basın  (↑/↓ geçmiş · Ctrl+R ara)  — ya da terminale tıklayıp doğrudan yazın"
                                            : "Port kapalı — komut yazılamaz");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (Session.State != SerialState.Open)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) e.SuppressKeyPress = true;
                return;
            }
            if (_autoComplete != null && _autoComplete.HandleKeyDown(e)) return;   // Tab / ↓ / → / Esc (öneri)
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = e.Handled = true;
                SendInput();
            }
            else if (e.KeyCode == Keys.Up && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string prev = SerialHistoryStore.History.Previous(_input.Text);
                if (prev != null) SetInput(prev);
            }
            else if (e.KeyCode == Keys.Down && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string next = SerialHistoryStore.History.Next();
                if (next != null) SetInput(next);
            }
            else if (e.KeyCode == Keys.R && e.Control && !e.Alt)
            {
                e.SuppressKeyPress = e.Handled = true;
                ShowHistory();
            }
            else if (e.KeyCode == Keys.C && e.Control && !e.Alt && _input.SelectionLength == 0)
            {
                e.SuppressKeyPress = true;
                Session.SendKeys("\u0003");   // seçim yoksa: karta kesme (Ctrl+C), Telnet'teki gibi
            }
        }

        /// <summary>Komut satırındaki metni gönderir (boşsa yalnız Enter — ör. "Press [Enter] to directly boot").</summary>
        private void SendInput()
        {
            if (Session.State != SerialState.Open) return;
            string cmd = _input.Text;
            SetInput("");
            SerialHistoryStore.History.ResetNavigation();
            SendLine(cmd);
        }

        private void SendLine(string cmd)
        {
            _typed = true;
            if (!IsAtBottom()) ScrollToEnd();
            Session.TypeCommand(cmd ?? "", true);   // geçmişe Enter'lı satır olarak girer
        }

        private void SetInput(string text)
        {
            if (_autoComplete != null) _autoComplete.SetTextQuiet(text);
            else { _input.Text = text; _input.SelectionStart = _input.TextLength; }
        }

        // ---------- Gösterge ve bildirim ----------

        private const string PillWidest = "↓  Yeni Satırlar (999+)";

        private void BuildOverlays()
        {
            _newLinesPill.Click += (s, e) => { ScrollToEnd(); _indicator.OnClick(); SyncPill(); _output.Focus(); };
            // Yalnız kullanıcının kaydırması göstergeyi kapatır (program eklerken gelen kaymalar sayılmaz)
            _output.VScroll += (s, e) => CheckReachedBottom();
            _output.MouseUp += (s, e) => CheckReachedBottom();
            _outputHost.Resize += (s, e) => { if (_newLinesPill.Visible) PlacePill(); PlaceToast(); };
            _toast.Click += (s, e) =>
            {
                string path = _toastPath;
                HideToast();
                if (path != null)
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); }
                    catch (Exception ex) { ErrorText.LogTechnical("klasör aç", ex); }
                }
            };
            _toastTimer.Tick += (s, e) => HideToast();
        }

        private void CheckReachedBottom()
        {
            if (_programScroll || !_indicator.Visible) return;
            _indicator.OnScroll(IsAtBottom());
            SyncPill();
        }

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

        private void PlacePill()
        {
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

        private void ShowToast(string text, bool ok, string path)
        {
            _toastPath = path;
            _toast.Text = text;
            _toast.BackColor = ok ? Theme.SuccessBanner : Theme.ErrorBanner;
            _toast.ForeColor = ok ? Theme.SuccessBannerText : Color.White;
            _toast.MaximumSize = new Size(Math.Max(120, _outputHost.ClientSize.Width - 24), 0);
            _toast.Visible = true;
            _toast.BringToFront();
            PlaceToast();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            _toast.Visible = false;
        }

        private void PlaceToast()
        {
            if (!_toast.Visible) return;
            _toast.Location = new Point(Math.Max(4, _outputHost.ClientSize.Width - _toast.Width - 12), 8);
        }

        /// <summary>
        /// "Dosyaya kaydet": ekrandaki çıktı sormadan logs\SeriKanalLoglari altına kaydedilir (Telnet'teki gibi; klasör
        /// yoksa oluşur). Bildirime tıklayınca dosya Windows Gezgini'nde seçili açılır.
        /// </summary>
        private async System.Threading.Tasks.Task SaveSnapshotAsync()
        {
            string text = _output.Text;
            _saveBtn.Enabled = false;
            try
            {
                SnapshotResult r = await TerminalSnapshot.SaveSerialAsync(Session.Profile, text, DateTime.Now);
                if (r.Ok) ShowToast("✓  Kaydedildi: " + Path.GetFileName(r.Path) + "  (klasörü açmak için tıklayın)", true, r.Path);
                else ShowToast("✕  Kaydedilemedi: " + r.Error, false, null);
            }
            finally
            {
                _saveBtn.Enabled = true;
            }
        }

        // ---------- Seçim / çizim ----------

        private void WireSelection(Control c)
        {
            c.MouseDown += (s, e) => { var h = Activated; if (h != null) h(this); };
            foreach (Control child in c.Controls) WireSelection(child);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color border = _selected ? Theme.Accent : Session.State == SerialState.Error ? Theme.Danger : Theme.Border;
            using (var p = new Pen(border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _toastTimer.Dispose();
                Session.StateChanged -= OnSessionStateChanged;
                _tips.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Terminal alanı: kullanıcı metni değiştiremez; tuşlar porta gönderilir. Seçim bırakılınca seçilen metin panoya
        /// kopyalanır (TeraTerm gibi).
        /// </summary>
        private sealed class SerialTerminalBox : RichTextBox
        {
            private const int WM_SETREDRAW = 0x000B;
            private const int WM_PASTE = 0x0302;
            private const int WM_CUT = 0x0300;
            private const int WM_CLEAR = 0x0303;
            private const int WM_UNDO = 0x0304;
            private const int WM_SYSKEYDOWN = 0x0104;
            private const int WM_SYSKEYUP = 0x0105;

            public Action<string> KeysTyped;
            public Action PasteRequested;

            public SerialTerminalBox()
            {
                BorderStyle = BorderStyle.None;
                BackColor = Theme.Input;
                ForeColor = Theme.Terminal;
                Font = Theme.Mono;
                WordWrap = false;
                HideSelection = false;
                DetectUrls = false;
                ShortcutsEnabled = false;
                AcceptsTab = true;
                ScrollBars = RichTextBoxScrollBars.ForcedBoth;
                Cursor = Cursors.IBeam;
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            private int _noRedraw;   // iç içe çağrılabilir (toplu ekleme içinde en alta inme)

            public void BeginUpdateNoRedraw()
            {
                if (_noRedraw++ == 0 && IsHandleCreated) SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }

            public void EndUpdateNoRedraw()
            {
                if (_noRedraw == 0 || --_noRedraw > 0 || !IsHandleCreated) return;
                SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                Invalidate();
            }

            protected override bool IsInputKey(Keys keyData)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k == Keys.Tab || k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.Escape || k == Keys.Enter)
                    return true;
                return base.IsInputKey(keyData);
            }

            protected override bool ProcessDialogKey(Keys keyData)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k == Keys.Escape || k == Keys.Enter || k == Keys.Tab) return false;   // form'un Kabul / İptal düğmesine gitmesin
                return base.ProcessDialogKey(keyData);
            }

            private void Send(string s)
            {
                var h = KeysTyped;
                if (h != null) h(s);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                Keys k = e.KeyCode;
                string seq = null;
                if (e.Shift && k == Keys.Insert) { e.Handled = e.SuppressKeyPress = true; var p = PasteRequested; if (p != null) p(); return; }
                // Ctrl+V: panodaki metin karta yazılır; Ctrl+C: seçili metin varsa yalnız kopyalanır (karta kesme gitmez),
                // seçim yoksa karta kesme (Ctrl+C) gider — Telnet ekranındaki gibi (1.10)
                if (e.Control && !e.Alt && !e.Shift && k == Keys.V) { e.Handled = e.SuppressKeyPress = true; var p = PasteRequested; if (p != null) p(); return; }
                if (e.Control && !e.Alt && !e.Shift && k == Keys.C && SelectionLength > 0)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    try { Clipboard.SetText(SelectedText.Replace("\n", "\r\n")); } catch { }
                    return;
                }
                if (k == Keys.Enter) seq = "\r";
                else if (k == Keys.Back) seq = "\b";       // TeraTerm varsayılanı: Backspace = BS (Ctrl+H)
                else if (k == Keys.Tab) seq = "\t";
                else if (k == Keys.Escape) seq = "\u001b";
                else if (k == Keys.Up) seq = "\u001b[A";
                else if (k == Keys.Down) seq = "\u001b[B";
                else if (k == Keys.Right) seq = "\u001b[C";
                else if (k == Keys.Left) seq = "\u001b[D";
                else if (k == Keys.Delete) seq = "\u007f";   // TeraTerm varsayılanı ("Transmit DEL by: Delete key"): DEL — BIOS'ta Del ile kurulum / boot menüsü
                else if (e.Shift && !e.Control && !e.Alt && (k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End))
                {
                    // Shift + sayfa tuşları: yalnız görünümü kaydırır (karta gitmez)
                    e.Handled = e.SuppressKeyPress = true;
                    ScrollView(k);
                    return;
                }
                else if (!e.Control && !e.Alt) seq = FunctionKeySequence(k);
                if (seq == null && e.Control && !e.Alt && k >= Keys.A && k <= Keys.Z) seq = ((char)(k - Keys.A + 1)).ToString();   // Ctrl+C = 0x03 …
                if (seq != null)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    Send(seq);
                    return;
                }
                base.OnKeyDown(e);
            }

            /// <summary>
            /// F1–F12, Home / End / PgUp / PgDn / Insert: TeraTerm'in varsayılan olarak gönderdiği VT dizileri (1.10). Kart
            /// açılırken "Press [F2/Del] to enter setup", "Press [F10] to show boot menu" gibi menüler bunları bekler.
            /// </summary>
            internal static string FunctionKeySequence(Keys k)
            {
                switch (k)
                {
                    case Keys.F1: return "\u001bOP";
                    case Keys.F2: return "\u001bOQ";
                    case Keys.F3: return "\u001bOR";
                    case Keys.F4: return "\u001bOS";
                    case Keys.F5: return "\u001b[15~";
                    case Keys.F6: return "\u001b[17~";
                    case Keys.F7: return "\u001b[18~";
                    case Keys.F8: return "\u001b[19~";
                    case Keys.F9: return "\u001b[20~";
                    case Keys.F10: return "\u001b[21~";
                    case Keys.F11: return "\u001b[23~";
                    case Keys.F12: return "\u001b[24~";
                    case Keys.Home: return "\u001b[1~";
                    case Keys.Insert: return "\u001b[2~";
                    case Keys.End: return "\u001b[4~";
                    case Keys.PageUp: return "\u001b[5~";
                    case Keys.PageDown: return "\u001b[6~";
                    default: return null;
                }
            }

            private const int WM_VSCROLL_BOX = 0x0115;
            private const int SB_PAGEUP = 2, SB_PAGEDOWN = 3, SB_TOP = 6, SB_BOTTOM_BOX = 7;

            private void ScrollView(Keys k)
            {
                if (!IsHandleCreated) return;
                int code = k == Keys.PageUp ? SB_PAGEUP : k == Keys.PageDown ? SB_PAGEDOWN : k == Keys.Home ? SB_TOP : SB_BOTTOM_BOX;
                SendMessage(Handle, WM_VSCROLL_BOX, (IntPtr)code, IntPtr.Zero);
            }

            protected override void OnKeyPress(KeyPressEventArgs e)
            {
                e.Handled = true;                     // metin kutusuna yazılmaz; karta gider
                if (e.KeyChar >= ' ' || e.KeyChar == '\t') Send(e.KeyChar.ToString());
            }

            protected override void OnMouseUp(MouseEventArgs mevent)
            {
                base.OnMouseUp(mevent);
                if (mevent.Button == MouseButtons.Left && SelectionLength > 0)
                {
                    try { Clipboard.SetText(SelectedText.Replace("\n", "\r\n")); } catch { }
                }
            }

            protected override void WndProc(ref Message m)
            {
                // Kullanıcının metni değiştirmesine yol açabilecek iletiler yok sayılır (yapıştır karta gider)
                if (m.Msg == WM_PASTE) { var p = PasteRequested; if (p != null) p(); return; }
                // F10 Windows'ta menü tuşudur (WM_SYSKEYDOWN): pencere menüsü yerine karta gider
                if ((m.Msg == WM_SYSKEYDOWN || m.Msg == WM_SYSKEYUP) && (int)m.WParam == (int)Keys.F10 && (ModifierKeys & Keys.Alt) == 0)
                {
                    if (m.Msg == WM_SYSKEYDOWN) Send(FunctionKeySequence(Keys.F10));
                    return;
                }
                if (m.Msg == WM_CUT || m.Msg == WM_CLEAR || m.Msg == WM_UNDO) return;
                base.WndProc(ref m);
            }
        }
    }
}
