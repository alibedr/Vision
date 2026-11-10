using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Boot ayarları penceresi (G27): kart terminalinden bspBootChange / bootChange gönderilince açılır. Kartın tane tane
    /// sorduğu alanlar araya giren loglardan ayrılarak bir listede gösterilir; şu an sorulan alan vurgulanır, yeni değer
    /// buradan yazılıp gönderilir. Kartın tüm çıktısı terminalde de görünmeye devam eder.
    /// Pencere kartı yalnız izler: gönderilenler kartın kendi bootChange girişine gider ("" = değiştirmeden geç,
    /// "." = temizle, "-" = önceki alan, Ctrl+D = çık).
    /// </summary>
    public sealed class BootChangeDialog : DarkDialog
    {
        private static readonly Dictionary<CardSession, BootChangeDialog> OpenDialogs = new Dictionary<CardSession, BootChangeDialog>();
        private const int MaxAutoSteps = 80;

        private readonly CardSession _session;
        private TerminalTap _tap;
        private BootChangeParser _parser = new BootChangeParser();
        private string _command;
        private DateTime _startedUtc;
        private int _shownVersion = -1;

        // Otomatik adımlar: "kalanları geçip bitir" ve listeden "bu alana git"
        private bool _skipAll;
        private int? _goTo;
        private int _autoSerial = -1;
        private int _autoSteps;
        private bool _closeWhenDone;
        private bool _sending;
        private string _sendError;

        private readonly Timer _timer = new Timer { Interval = 50 };
        private readonly ToolTip _tips = new DarkToolTip();

        private readonly Label _title = Theme.MakeLabel("", Theme.Title, Theme.Text);
        private readonly Label _status = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Label _currentCaption = Theme.MakeLabel("ŞU AN SORULAN ALAN", Theme.SectionHeader, Theme.TextMuted);
        private readonly Label _currentLabel = Theme.MakeLabel("", new Font("Segoe UI Semibold", 12f), Theme.Text);
        private readonly Label _currentValue = Theme.MakeLabel("", Theme.Mono, Theme.TextMuted);
        private readonly TextBox _input = new TextBox();
        private readonly Button _send = Theme.MakeButton("Gönder", Theme.ButtonKind.Primary);
        private readonly Button _keep = Theme.MakeButton("Değiştirmeden geç", Theme.ButtonKind.Secondary);
        private readonly Button _edit = Theme.MakeButton("Değeri düzenle", Theme.ButtonKind.Secondary);
        private readonly Button _clear = Theme.MakeButton("Temizle (.)", Theme.ButtonKind.Secondary);
        private readonly Button _prev = Theme.MakeButton("◀ Önceki alan", Theme.ButtonKind.Secondary);
        private readonly Button _skipRest = Theme.MakeButton("Kalanları geçip bitir", Theme.ButtonKind.Secondary);
        private readonly Button _quit = Theme.MakeButton("Çık (Ctrl+D)", Theme.ButtonKind.Danger);
        private readonly FieldList _list = new FieldList();
        private readonly Label _empty = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Panel _logBar = new Panel { Dock = DockStyle.Bottom, Height = 28, Cursor = Cursors.Hand };
        private readonly Label _logCaption = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly TextBox _logs = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, BorderStyle = BorderStyle.None, Dock = DockStyle.Bottom, Height = 130, Visible = false };
        private readonly Label _footerHint = Theme.MakeLabel("", Theme.UiSmall, Theme.TextFaint);
        private readonly Button _close = Theme.MakeButton("Kapat", Theme.ButtonKind.Secondary);
        private int _shownLogCount = -1;

        private BootChangeDialog(CardSession session, string command)
            : base("Boot ayarları  —  " + session.Card.Label, new Size(780, 600))
        {
            _session = session;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(620, 460);
            KeyPreview = true;
            if (Theme.AppIcon != null) { Icon = Theme.AppIcon; ShowIcon = true; }

            // --- Üst: durum + şu anki alan + giriş
            var top = new Panel { Dock = DockStyle.Top, Height = 214, BackColor = Theme.Surface };
            top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };
            _title.Location = new Point(18, 12);
            _status.Location = new Point(18, 38);
            _status.AutoSize = false;
            _status.Height = 20;
            _currentCaption.Location = new Point(18, 70);
            _currentLabel.Location = new Point(18, 88);
            _currentValue.Location = new Point(18, 116);
            _currentValue.AutoSize = false;
            _currentValue.Height = 20;
            Theme.StyleTextBox(_input);
            _input.Font = new Font("Consolas", 11f);
            _input.Location = new Point(18, 142);
            Theme.SetCueBanner(_input, "Yeni değer yazın  ·  boş bırakıp Enter: değiştirmeden geç");
            _send.Width = 96;
            foreach (Button b in new[] { _keep, _edit, _clear, _prev, _skipRest, _quit }) b.Height = 28;
            _keep.Width = 140; _edit.Width = 116; _clear.Width = 104; _prev.Width = 116; _skipRest.Width = 150; _quit.Width = 108;
            top.Controls.AddRange(new Control[] { _title, _status, _currentCaption, _currentLabel, _currentValue, _input, _send, _keep, _edit, _clear, _prev, _skipRest, _quit });
            top.Resize += (s, e) => LayoutTop(top);

            _tips.SetToolTip(_send, "Yazılan değeri karta gönderir (Enter). Kart bir sonraki alana geçer.");
            _tips.SetToolTip(_keep, "Bu alanı değiştirmeden bir sonrakine geçer (boş Enter).");
            _tips.SetToolTip(_edit, "Kartın bu alandaki değerini giriş kutusuna kopyalar; düzenleyip Enter'a basın (F2).");
            _tips.SetToolTip(_clear, "Alanı boşaltır (karta \".\" gönderilir).");
            _tips.SetToolTip(_prev, "Bir önceki alana döner (karta \"-\" gönderilir).");
            _tips.SetToolTip(_skipRest, "Kalan alanların hepsini değiştirmeden geçer ve komutu bitirir.\nŞimdiye kadar girdiğiniz değerler kaydedilir.");
            _tips.SetToolTip(_quit, "bootChange'den hemen çıkar (karta Ctrl+D gönderilir).\nKartın imajına göre kalan alanlar ya da değişiklikler kaydedilmeyebilir.");

            // --- Orta: alan listesi
            var middle = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 6, 4, 0) };
            _list.Dock = DockStyle.Fill;
            _empty.AutoSize = false;
            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.BackColor = Theme.Input;
            middle.Controls.Add(_empty);
            middle.Controls.Add(_list);
            Theme.ApplyDarkScrollBars(_list);
            _tips.SetToolTip(_list, "Çift tık: kart o alana gelene kadar alanlar değiştirilmeden geçilir (geri için \"-\" gönderilir).");

            // --- Alt: araya giren loglar
            _logBar.BackColor = Theme.Surface;
            _logBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, _logBar.Width, 0); };
            _logCaption.Location = new Point(14, 6);
            _logCaption.BackColor = Color.Transparent;
            _logBar.Controls.Add(_logCaption);
            _logBar.Click += (s, e) => ToggleLogs();
            _logCaption.Click += (s, e) => ToggleLogs();
            _logs.BackColor = Theme.Input;
            _logs.ForeColor = Theme.Terminal;
            _logs.Font = new Font("Consolas", 9f);
            Theme.ApplyDarkScrollBars(_logs);

            // --- Alt bant
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            _footerHint.Text = "Kartın tüm çıktısı terminalde de görünür  ·  Enter: gönder  ·  F2: değeri düzenle  ·  Ctrl+D: çık  ·  Esc: kapat";
            _footerHint.BackColor = Color.Transparent;
            _close.Width = 90;
            footer.Controls.AddRange(new Control[] { _footerHint, _close });
            footer.Resize += (s, e) =>
            {
                _close.Location = new Point(footer.ClientSize.Width - 16 - _close.Width, (FooterHeight - _close.Height) / 2);
                _footerHint.Location = new Point(16, (FooterHeight - _footerHint.PreferredHeight) / 2);
                _footerHint.Visible = _footerHint.Left + _footerHint.PreferredWidth < _close.Left - 8;
            };

            Controls.Add(middle);
            Controls.Add(_logBar);
            Controls.Add(_logs);
            Controls.Add(top);
            Controls.Add(footer);

            _send.Click += async (s, e) => await SendFromInputAsync();
            _keep.Click += async (s, e) => await SendStepAsync("");
            _edit.Click += (s, e) => EditCurrentValue();
            _clear.Click += async (s, e) => await SendStepAsync(BootChangeParser.ClearToken);
            _prev.Click += async (s, e) => await SendStepAsync(BootChangeParser.PreviousToken);
            _skipRest.Click += (s, e) => StartSkipAll();
            _quit.Click += async (s, e) => await QuitAsync();
            _close.Click += (s, e) => Close();
            _list.DoubleClick += (s, e) => GoToSelected();
            _input.KeyDown += Input_KeyDown;
            KeyDown += Form_KeyDown;
            _timer.Tick += (s, e) => Pump();
            FormClosing += OnClosingAsk;
            FormClosed += (s, e) => Cleanup();

            Start(command);
            Shown += (s, e) => _input.Focus();
        }

        // =====================================================================
        // Açma
        // =====================================================================

        /// <summary>
        /// Kart terminali bspBootChange / bootChange göndermeden HEMEN ÖNCE çağırır: pencere açılır (açıksa yeniden başlar)
        /// ve kartın cevabı izlenmeye başlar. Kart zaten bir boot alanında bekliyorsa (pencere sürüyor) hiçbir şey yapılmaz.
        /// </summary>
        public static void Begin(CardSession session, IWin32Window owner, string command, int cascade = 0)
        {
            BootChangeDialog d;
            if (OpenDialogs.TryGetValue(session, out d) && !d.IsDisposed)
            {
                d.Start(command);
                if (d.WindowState == FormWindowState.Minimized) d.WindowState = FormWindowState.Normal;
                d.Activate();
                return;
            }
            d = new BootChangeDialog(session, command);
            OpenDialogs[session] = d;
            // Pencere sahibinin ortasında (genel komutla birden çok kart: her biri biraz kaydırılarak)
            var ownerForm = owner as Form;
            if (ownerForm != null)
            {
                d.StartPosition = FormStartPosition.Manual;
                int step = 28 * (cascade % 8);
                Rectangle area = Screen.FromControl(ownerForm).WorkingArea;
                int x = ownerForm.Left + (ownerForm.Width - d.Width) / 2 + step;
                int y = ownerForm.Top + (ownerForm.Height - d.Height) / 2 + step;
                d.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - d.Width)), Math.Max(area.Top, Math.Min(y, area.Bottom - d.Height)));
            }
            if (owner != null) d.Show(owner); else d.Show();
        }

        private void Start(string command)
        {
            if (_tap == null) _tap = _session.OpenTap();
            else _tap.Take();   // önceki komuttan kalan veri atılır
            _command = (command ?? "").Trim();
            _parser = new BootChangeParser();
            _startedUtc = DateTime.UtcNow;
            _skipAll = false;
            _goTo = null;
            _autoSerial = -1;
            _autoSteps = 0;
            _closeWhenDone = false;
            _sendError = null;
            _shownVersion = -1;
            _shownLogCount = -1;
            _session.InteractiveInputActive = true;
            _title.Text = _command + "  —  " + _session.Card.Label;
            _title.Size = _title.PreferredSize;
            _timer.Start();
            UpdateView(true);
        }

        // =====================================================================
        // Kartı izleme
        // =====================================================================

        private void Pump()
        {
            DateTime now = DateTime.UtcNow;
            string text = _tap != null ? _tap.Take() : null;
            if (text != null) _parser.Feed(text, now);
            _parser.Tick(now);

            if (!_parser.IsOver && _session.State != CardState.Connected)
                _parser.Fail("Kartın bağlantısı koptu (" + (_session.LastError ?? ErrorText.ConnectionLost) + ").");

            if (_parser.IsOver)
            {
                _session.InteractiveInputActive = false;
                _skipAll = false;
                _goTo = null;
                if (_closeWhenDone) { _closeWhenDone = false; _timer.Stop(); BeginInvoke(new Action(Close)); return; }
            }
            else RunAutoStep();

            UpdateView(false);
        }

        /// <summary>"Kalanları geçip bitir" / "bu alana git": her yeni istemde bir adım gönderilir.</summary>
        private void RunAutoStep()
        {
            if ((!_skipAll && _goTo == null) || _sending || !_parser.AwaitingInput) return;
            if (_parser.PromptSerial == _autoSerial) return;
            if (_autoSteps >= MaxAutoSteps) { _skipAll = false; _goTo = null; _sendError = "Otomatik geçiş durduruldu (çok fazla adım)."; return; }

            string step;
            if (_skipAll) step = "";
            else
            {
                int target = _goTo.Value, cur = _parser.CurrentIndex;
                if (cur == target) { _goTo = null; _input.Focus(); return; }
                step = cur > target ? BootChangeParser.PreviousToken : "";
            }
            _autoSerial = _parser.PromptSerial;
            _autoSteps++;
            var ignored = SendStepAsync(step, true);
        }

        // =====================================================================
        // Gönderme
        // =====================================================================

        private async Task SendFromInputAsync()
        {
            string text = _input.Text;
            await SendStepAsync(text);
        }

        private async Task SendStepAsync(string text, bool automatic = false)
        {
            if (!_parser.AwaitingInput || _sending) return;
            if (!automatic) { _skipAll = false; _goTo = null; }
            if (_session.State != CardState.Connected) { _sendError = "Kart bağlı değil."; UpdateView(true); return; }
            _sending = true;
            _sendError = null;
            _parser.MarkSent(text ?? "");   // kart cevap verene kadar aynı istem yeniden "yeni" sayılmasın
            if (!automatic) _input.Clear();
            UpdateView(true);
            try
            {
                await _session.SendInputLineAsync(text ?? "");
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(_session.Card.DisplayName + " bootChange", ex);
                _sendError = "Gönderilemedi: " + ErrorText.Describe(ex);
            }
            finally
            {
                _sending = false;
            }
            UpdateView(true);
        }

        private void StartSkipAll()
        {
            if (_parser.IsOver) return;
            _goTo = null;
            _skipAll = true;
            _autoSerial = -1;
            UpdateView(true);
        }

        private void GoToSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || _parser.IsOver || _parser.CurrentIndex < 0) return;
            if (i == _parser.CurrentIndex) { _input.Focus(); return; }
            _skipAll = false;
            _goTo = i;
            _autoSerial = -1;
            UpdateView(true);
        }

        private async Task QuitAsync()
        {
            if (_parser.IsOver || _session.State != CardState.Connected) return;
            _skipAll = false;
            _goTo = null;
            try { await _session.SendRawAsync(BootChangeParser.QuitChar); }
            catch (Exception ex) { _sendError = "Gönderilemedi: " + ErrorText.Describe(ex); }
            UpdateView(true);
        }

        private void EditCurrentValue()
        {
            BootField f = _parser.Current;
            if (f == null || !_parser.AwaitingInput) return;
            _input.Text = f.CardValue ?? "";
            _input.Focus();
            _input.SelectAll();
        }

        // =====================================================================
        // Klavye / kapatma
        // =====================================================================

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await SendFromInputAsync();
            }
        }

        private async void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D && e.Control) { e.Handled = true; e.SuppressKeyPress = true; await QuitAsync(); }
            else if (e.KeyCode == Keys.F2) { e.Handled = true; EditCurrentValue(); }
            else if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
        }

        private void OnClosingAsk(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;   // uygulama kapanıyor: sorulmaz
            if (_parser.IsOver || _session.State != CardState.Connected || _parser.Phase == BootChangePhase.Starting) return;
            int c = ChoiceDialog.Ask(this, "Boot ayarları sürüyor",
                "Kart hâlâ \"" + (_parser.Current != null ? _parser.Current.Label : "boot") + "\" alanı için giriş bekliyor.",
                "Kalan alanları değiştirmeden geçip bitirebilir ya da yalnız pencereyi kapatıp terminalden devam edebilirsiniz.",
                new[] { "Kalanları geçip bitir", "Yalnız pencereyi kapat", "Vazgeç" }, 0, -1);
            if (c == 0) { e.Cancel = true; _closeWhenDone = true; StartSkipAll(); }
            else if (c != 1) e.Cancel = true;
        }

        private void Cleanup()
        {
            _timer.Stop();
            _timer.Dispose();
            if (_tap != null) { _tap.Dispose(); _tap = null; }
            _session.InteractiveInputActive = false;
            BootChangeDialog d;
            if (OpenDialogs.TryGetValue(_session, out d) && d == this) OpenDialogs.Remove(_session);
        }

        // =====================================================================
        // Görünüm
        // =====================================================================

        private void LayoutTop(Panel top)
        {
            int w = top.ClientSize.Width;
            _status.Width = Math.Max(100, w - 36);
            _currentValue.Width = Math.Max(100, w - 36);
            _send.Location = new Point(w - 18 - _send.Width, 141);
            _input.Width = Math.Max(120, _send.Left - 8 - 18);
            _send.Height = Math.Max(_send.Height, _input.Height + 2);
            int x = 18, y = 176;
            foreach (Button b in new[] { _keep, _edit, _clear, _prev, _skipRest })
            {
                b.Location = new Point(x, y);
                x += b.Width + 6;
            }
            _quit.Location = new Point(Math.Max(x + 6, w - 18 - _quit.Width), y);
            _quit.Visible = _quit.Right <= w - 4;
        }

        private void ToggleLogs()
        {
            _logs.Visible = !_logs.Visible;
            _shownLogCount = -1;
            UpdateView(true);
        }

        private void UpdateView(bool force)
        {
            if (!force && _parser.Version == _shownVersion && !(_parser.Phase == BootChangePhase.Starting)) return;
            _shownVersion = _parser.Version;

            BootField cur = _parser.Current;
            bool awaiting = _parser.AwaitingInput && !_sending;
            bool auto = _skipAll || _goTo != null;

            // Durum satırı
            string status;
            Color color = Theme.TextMuted;
            switch (_parser.Phase)
            {
                case BootChangePhase.Starting:
                    double secs = (DateTime.UtcNow - _startedUtc).TotalSeconds;
                    status = secs < 6 ? "Kartın cevabı bekleniyor…"
                        : "Kart henüz bir boot alanı sormadı (" + (int)secs + " sn). Komut kartta farklı çalışıyor olabilir; terminale bakın.";
                    if (secs >= 6) color = Theme.Warning;
                    break;
                case BootChangePhase.Prompting:
                    if (auto) status = _skipAll ? "Kalan alanlar değiştirilmeden geçiliyor…" : "Seçilen alana gidiliyor…";
                    else if (awaiting) status = "Kart \"" + cur.Label + "\" alanı için giriş bekliyor.";
                    else status = "Kartın bir sonraki alanı sorması bekleniyor…";
                    color = awaiting && !auto ? Theme.Accent : Theme.TextMuted;
                    break;
                case BootChangePhase.Finished:
                    int changed = _parser.Fields.Count(f => f.Changed);
                    status = "✓  Bitti — " + (changed == 0 ? "hiçbir alan değiştirilmedi." :
                             changed + " alan değiştirildi. Yeni boot ayarları genellikle kart yeniden başlatılınca geçerli olur.");
                    color = Theme.Success;
                    break;
                default:
                    status = "✕  " + (_parser.FailMessage ?? "Komut tamamlanamadı.");
                    color = Theme.Danger;
                    break;
            }
            if (_sendError != null) { status = "⚠  " + _sendError; color = Theme.Danger; }
            _status.Text = status;
            _status.ForeColor = color;

            // Şu anki alan
            if (_parser.Phase == BootChangePhase.Prompting && cur != null)
            {
                _currentLabel.Text = cur.Label;
                _currentValue.Text = "kartta:  " + (string.IsNullOrEmpty(cur.CardValue) ? "(boş)" : cur.CardValue);
            }
            else if (_parser.IsOver)
            {
                _currentLabel.Text = _parser.Phase == BootChangePhase.Finished ? "Boot ayarları tamamlandı" : "Boot ayarları yapılamadı";
                _currentValue.Text = "";
            }
            else
            {
                _currentLabel.Text = "—";
                _currentValue.Text = "";
            }
            _currentLabel.Size = _currentLabel.PreferredSize;

            bool canAct = awaiting && !auto;
            _input.Enabled = canAct;
            _send.Enabled = canAct;
            _keep.Enabled = canAct;
            _edit.Enabled = canAct && cur != null && !string.IsNullOrEmpty(cur.CardValue);
            _clear.Enabled = canAct;
            _prev.Enabled = canAct && _parser.CurrentIndex > 0;
            _skipRest.Enabled = _parser.Phase == BootChangePhase.Prompting && !_skipAll;
            _quit.Enabled = !_parser.IsOver && _session.State == CardState.Connected;
            if (canAct && ContainsFocus && !_input.Focused && !_list.Focused) _input.Focus();

            // Alan listesi
            _list.CurrentIndex = _parser.Phase == BootChangePhase.Prompting ? _parser.CurrentIndex : -1;
            _list.Awaiting = awaiting;
            _list.SetFields(_parser.Fields);
            _empty.Text = _parser.IsOver ? "Kart hiçbir boot alanı göstermedi." : "Kartın ilk boot alanını sorması bekleniyor…";
            _empty.Visible = _parser.Fields.Count == 0;
            _list.Visible = _parser.Fields.Count > 0;

            // Loglar
            int n = _parser.Logs.Count;
            _logCaption.Text = (_logs.Visible ? "▾  " : "▸  ") + "Araya giren kart logları (" + n + ")" +
                               (n > 0 && !_logs.Visible ? "  ·  göstermek için tıklayın" : "");
            _logCaption.Size = _logCaption.PreferredSize;
            if (_logs.Visible && n != _shownLogCount)
            {
                _shownLogCount = n;
                _logs.Text = string.Join(Environment.NewLine, _parser.Logs);
                _logs.SelectionStart = _logs.TextLength;
                _logs.ScrollToCaret();
            }
        }

        // =====================================================================
        // Alan listesi
        // =====================================================================

        private sealed class FieldList : ListBox
        {
            private readonly Font _mono = new Font("Consolas", 10f);
            private readonly Font _bold = new Font("Segoe UI Semibold", 9.5f);
            private readonly Font _normal = new Font("Segoe UI", 9.5f);
            private List<BootField> _fields = new List<BootField>();
            private string _signature;

            public FieldList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = _normal;
                ItemHeight = 30;
                BackColor = Theme.Input;
                ForeColor = Theme.Text;
            }

            public int CurrentIndex { get; set; }
            public bool Awaiting { get; set; }

            public void SetFields(List<BootField> fields)
            {
                string sig = CurrentIndex + "|" + Awaiting + "|" +
                             string.Join("|", fields.Select(f => f.Label + "=" + f.CardValue + ">" + f.Entered));
                if (sig == _signature) return;
                _signature = sig;
                _fields = fields.ToList();
                BeginUpdate();
                try
                {
                    while (Items.Count > _fields.Count) Items.RemoveAt(Items.Count - 1);
                    while (Items.Count < _fields.Count) Items.Add(Items.Count);
                }
                finally { EndUpdate(); }
                if (CurrentIndex >= 0 && CurrentIndex < Items.Count)
                {
                    int visible = Math.Max(1, ClientSize.Height / ItemHeight);
                    if (CurrentIndex < TopIndex || CurrentIndex >= TopIndex + visible)
                        TopIndex = Math.Max(0, CurrentIndex - visible / 2);
                }
                Invalidate();
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= _fields.Count) return;
                Graphics g = e.Graphics;
                BootField f = _fields[e.Index];
                bool current = e.Index == CurrentIndex;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color back = current ? Theme.ListSelectedStrong : selected ? Theme.Selection : (e.Index % 2 == 0 ? Theme.Input : Theme.ZebraRow);
                Color fore = current ? Theme.ListSelectedText : Theme.Text;
                Color muted = current ? Theme.ListSelectedText : Theme.TextMuted;
                using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);

                const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                              TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                int x = e.Bounds.X + 6;
                if (current) TextRenderer.DrawText(g, Awaiting ? "▶" : "…", _bold, new Rectangle(x, e.Bounds.Y, 16, e.Bounds.Height), fore, flags);
                x += 20;
                int labelW = Math.Min(260, Math.Max(150, e.Bounds.Width / 3));
                TextRenderer.DrawText(g, f.Label, current ? _bold : _normal, new Rectangle(x, e.Bounds.Y, labelW - 8, e.Bounds.Height), fore, flags);
                x += labelW;

                string value = f.CardValue ?? "";
                var valueRect = new Rectangle(x, e.Bounds.Y, e.Bounds.Right - x - 6, e.Bounds.Height);
                if (f.Entered != null && f.Changed)
                {
                    // eski → yeni
                    string oldText = string.IsNullOrEmpty(f.OriginalValue) ? "(boş)" : f.OriginalValue;
                    string newText = f.EffectiveValue.Length == 0 ? "(boş)" : f.EffectiveValue;
                    int w1 = TextRenderer.MeasureText(g, oldText + "  →  ", _mono, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                    TextRenderer.DrawText(g, oldText + "  →  ", _mono, valueRect, muted, flags);
                    var nr = new Rectangle(valueRect.X + w1, valueRect.Y, Math.Max(0, valueRect.Width - w1), valueRect.Height);
                    TextRenderer.DrawText(g, newText, _mono, nr, current ? fore : Theme.Accent, flags);
                }
                else
                {
                    TextRenderer.DrawText(g, value.Length == 0 ? "(boş)" : value, _mono, valueRect,
                                          value.Length == 0 ? (current ? fore : Theme.TextFaint) : fore, flags);
                }
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { _mono.Dispose(); _bold.Dispose(); _normal.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
