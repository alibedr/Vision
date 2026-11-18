using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Seri sekmesindeki bir COM portu penceresi (DENEME). TeraTerm'deki gibi terminale tıklayıp yazılır: her tuş anında
    /// porta gider (Enter = CR, Backspace, Tab, Esc, oklar, Ctrl+harf). Kopyalama: fareyle seçilen metin kendiliğinden
    /// panoya alınır, sağ tık → Kopyala; yapıştırma: sağ tık → Yapıştır ya da Shift+Insert.
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
            _output.KeysTyped = s => { _typed = true; ScrollToEnd(); Session.SendKeys(s); };
            _output.PasteRequested = Paste;
            _output.ContextMenuStrip = _menu;
            _outputHost.Controls.Add(_output);
            Theme.ApplyDarkScrollBars(_output);
            BuildMenu();

            Controls.Add(_outputHost);
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

            var buttons = new IconButton[] { _connectBtn, _historyBtn, _saveBtn, _clearBtn, _settingsBtn, _maxBtn, _closeBtn };
            foreach (var b in buttons) { b.Height = 26; b.Width = 28; b.Anchor = AnchorStyles.Top | AnchorStyles.Right; }
            _historyBtn.SetTip(_tips, "Seri komut geçmişi — tek tık: komutu yazar (Enter'sız), çift tık: hemen gönderir");
            _saveBtn.SetTip(_tips, "Dosyaya kaydet — ekrandaki çıktıyı metin dosyasına kaydeder");
            _clearBtn.SetTip(_tips, "Temizle — ekranı temizler");
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
            _historyBtn.Click += (s, e) => ShowHistory();
            _saveBtn.Click += (s, e) => SaveToFile();
            _clearBtn.Click += (s, e) => { _output.Clear(); _output.Focus(); };
            _settingsBtn.Click += (s, e) => { var h = SettingsRequested; if (h != null) h(this); };
            _maxBtn.Click += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _closeBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(this); };
            _header.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
            _title.DoubleClick += (s, e) => { var h = MaximizeToggled; if (h != null) h(this); };
        }

        private void LayoutHeader()
        {
            int w = _header.Width;
            _saveBtn.Visible = w >= 300;
            _historyBtn.Visible = w >= 280;
            int x = w - 6;
            x -= _closeBtn.Width; _closeBtn.Location = new Point(x, 6);
            x -= _maxBtn.Width + 2; _maxBtn.Location = new Point(x, 6);
            x -= _settingsBtn.Width + 2; _settingsBtn.Location = new Point(x, 6);
            x -= _clearBtn.Width + 2; _clearBtn.Location = new Point(x, 6);
            if (_saveBtn.Visible) { x -= _saveBtn.Width + 2; _saveBtn.Location = new Point(x, 6); }
            if (_historyBtn.Visible) { x -= _historyBtn.Width + 2; _historyBtn.Location = new Point(x, 6); }
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
            var clear = new ToolStripMenuItem("Temizle", null, (s, e) => _output.Clear());
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
            if (open)
            {
                _strip.Text = "●  Açık — yazmak için terminale tıklayın (her tuş anında karta gider)";
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

        private void AppendRaw(string raw)
        {
            int lineLen = CurrentLineLength();
            List<SerialScreen.Op> ops = _screen.Process(raw, lineLen);
            if (ops.Count == 0) return;
            bool follow = _typed || IsAtBottom();
            _typed = false;
            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            _output.BeginUpdateNoRedraw();
            try
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
                TrimLines();
                if (follow || selLen == 0)
                {
                    _output.Select(_output.TextLength, 0);
                    if (follow) ScrollToEnd();
                }
                else
                {
                    _output.Select(Math.Min(selStart, _output.TextLength), Math.Min(selLen, Math.Max(0, _output.TextLength - selStart)));
                }
            }
            finally
            {
                _output.EndUpdateNoRedraw();
            }
        }

        private const int WM_VSCROLL = 0x0115;
        private const int SB_BOTTOM = 7;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// En alta iner. ScrollToCaret imleç satırını yatay kaydırma çubuğunun altında, yarım görünür bırakabiliyordu;
        /// dikey çubukla gerçekten en alta inilir (telnet penceresindeki gibi), son satır tam görünür.
        /// </summary>
        private void ScrollToEnd()
        {
            _output.Select(_output.TextLength, 0);
            _output.ScrollToCaret();
            if (_output.IsHandleCreated) SendMessage(_output.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
        }

        // Metnin tamamı kopyalanmadan (hızlı log akışında her 50 ms'de çağrılır) son satırın uzunluğu
        private int CurrentLineLength()
        {
            int len = _output.TextLength;
            if (len == 0) return 0;
            int start = _output.GetFirstCharIndexFromLine(_output.GetLineFromCharIndex(len));
            return start < 0 ? 0 : len - start;
        }

        private bool IsAtBottom()
        {
            if (_output.TextLength == 0) return true;
            Point last = _output.GetPositionFromCharIndex(_output.TextLength - 1);
            return last.Y < _output.ClientSize.Height + _output.Font.Height;
        }

        private void TrimLines()
        {
            int lines = _output.GetLineFromCharIndex(_output.TextLength) + 1;
            if (lines <= SerialSession.MaxLines) return;
            int cut = _output.GetFirstCharIndexFromLine(lines - SerialSession.MaxLines);
            if (cut <= 0) return;
            _output.Select(0, cut);
            _output.SelectedText = "";
        }

        // ---------- Kopyala / yapıştır / geçmiş / kaydet ----------

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
            if (!string.IsNullOrEmpty(t)) Session.Paste(t);
            _output.Focus();
        }

        private void ShowHistory()
        {
            bool withEnter;
            string cmd = CommandHistoryDialog.Pick(FindForm(), SerialHistoryStore.History, "", "Seri — " + Session.Profile.Label,
                                                   "Karta yaz", out withEnter);
            if (cmd != null && Session.State == SerialState.Open) Session.TypeCommand(cmd, withEnter);
            _output.Focus();
        }

        private void SaveToFile()
        {
            using (var d = new SaveFileDialog())
            {
                d.Title = "Seri çıktıyı kaydet";
                d.Filter = "Metin dosyası (*.txt)|*.txt|Tüm dosyalar (*.*)|*.*";
                string safe = Session.Profile.Label;
                foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
                d.FileName = safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, _output.Text.Replace("\n", "\r\n"), new UTF8Encoding(false)); }
                catch (Exception ex)
                {
                    ChoiceDialog.Ask(FindForm(), "Kaydedilemedi", "Dosya yazılamadı.", ErrorText.Describe(ex), new[] { "Tamam" }, 0, -1);
                }
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

            public void BeginUpdateNoRedraw() { if (IsHandleCreated) SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero); }

            public void EndUpdateNoRedraw()
            {
                if (!IsHandleCreated) return;
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
                if (k == Keys.Enter) seq = "\r";
                else if (k == Keys.Back) seq = "\b";
                else if (k == Keys.Tab) seq = "\t";
                else if (k == Keys.Escape) seq = "\u001b";
                else if (k == Keys.Up) seq = "\u001b[A";
                else if (k == Keys.Down) seq = "\u001b[B";
                else if (k == Keys.Right) seq = "\u001b[C";
                else if (k == Keys.Left) seq = "\u001b[D";
                else if (k == Keys.Delete) seq = "\u001b[3~";
                else if (e.Control && !e.Alt && k >= Keys.A && k <= Keys.Z) seq = ((char)(k - Keys.A + 1)).ToString();   // Ctrl+C = 0x03 …
                if (seq != null)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    Send(seq);
                    return;
                }
                // Sayfa / Home / End: yalnız görünümü kaydırır (karta gitmez)
                base.OnKeyDown(e);
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
                if (m.Msg == WM_CUT || m.Msg == WM_CLEAR || m.Msg == WM_UNDO) return;
                base.WndProc(ref m);
            }
        }
    }
}
