using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Bir kartın boot ayarları ekranı (G27): kart bspBootChange / bootChange gönderince kartın tane tane sorduğu alanlar
    /// araya giren loglardan ayrılarak bir listede gösterilir; şu an sorulan alan vurgulanır, yeni değer buradan yazılıp
    /// gönderilir. Kartın tüm çıktısı terminalde de görünmeye devam eder. Ekran kartı yalnız izler: gönderilenler kartın
    /// kendi bootChange girişine gider ("" = değiştirmeden geç, "." = temizle, "-" = önceki alan, Ctrl+D = çık).
    /// İki yerde kullanılır (1.9): tek kartın Boot ayarları penceresi (<see cref="BootChangeDialog"/>) ve genel komut
    /// çubuğundan gönderilince açılan çok kartlı pencere (<see cref="BootChangeMultiDialog"/>).
    /// </summary>
    public sealed class BootChangePanel : UserControl
    {
        private const int MaxAutoSteps = 80;
        private const int FooterHeight = 64;

        // Kart başına en fazla bir boot ekranı (hangi pencerede olursa olsun)
        private static readonly Dictionary<CardSession, BootChangePanel> Active = new Dictionary<CardSession, BootChangePanel>();

        /// <summary>Kartın açık boot ekranı; yoksa null.</summary>
        public static BootChangePanel Find(CardSession session)
        {
            BootChangePanel p;
            return session != null && Active.TryGetValue(session, out p) && !p.IsDisposed && !p._detached ? p : null;
        }

        /// <summary>Kapat düğmesi: bulunduğu pencere kapanmalı.</summary>
        public event Action<BootChangePanel> CloseRequested;
        /// <summary>Komut bitti ya da yapılamadı (her komut için bir kez).</summary>
        public event Action<BootChangePanel> Finished;
        /// <summary>Listede gösterilen kısa durum değişti.</summary>
        public event Action<BootChangePanel> StateChanged;
        private bool _finishedRaised, _detached;
        private readonly bool _lockCardInput;
        private bool _quitSent;   // Ctrl+D (Çık) ile çıkıldı: "tamamlandı" değil "çıkıldı" 
        private string _listStatus = "";
        private Color _listColor = Theme.TextMuted;

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
        // Boot ayarı kayıtlarıyla bağlantı (G51.9)
        private readonly Button _saveRecord = Theme.MakeButton("Bu ayarları kaydet…", Theme.ButtonKind.Primary);
        private readonly Button _records = Theme.MakeButton("Boot ayarı kayıtları…", Theme.ButtonKind.Secondary);
        private int _shownLogCount = -1;

        /// <param name="lockCardInput">
        /// true: komut sürerken kartın komut satırı kilitlenir (genel komut çubuğundan açılan çok kartlı pencere, 1.9).
        /// Tek kart penceresinde false: kullanıcı terminalden de devam edebilir.
        /// </param>
        public BootChangePanel(CardSession session, string command, bool lockCardInput = false)
        {
            _session = session;
            _lockCardInput = lockCardInput;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            Active[session] = this;

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
            _tips.SetToolTip(_quit, "Boot ayarlarından hemen çıkar (karta Ctrl+D gönderilir).\nKartın imajına göre girdiğiniz değerler kaydedilmeyebilir;\nkaydedilmesini istiyorsanız \"Kalanları geçip bitir\"i kullanın.");

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
            _saveRecord.Width = 160;
            _records.Width = 170;
            footer.Controls.AddRange(new Control[] { _footerHint, _records, _saveRecord, _close });
            footer.Resize += (s, e) =>
            {
                int y = (FooterHeight - _close.Height) / 2;
                _close.Location = new Point(footer.ClientSize.Width - 16 - _close.Width, y);
                _saveRecord.Location = new Point(_close.Left - 8 - _saveRecord.Width, y);
                _records.Location = new Point(_saveRecord.Left - 8 - _records.Width, y);
                _footerHint.Location = new Point(16, (FooterHeight - _footerHint.PreferredHeight) / 2);
                _footerHint.Visible = _footerHint.Left + _footerHint.PreferredWidth < _records.Left - 8;
            };
            _tips.SetToolTip(_saveRecord, "Komut bitince kartın boot ayarlarını (bu pencerenin okuduğu değerlerle) adıyla kaydeder.\n" +
                                          "Kayıtlar \"Boot ayarı kayıtları\" penceresinde karşılaştırılır ve geri yüklenir.");
            _tips.SetToolTip(_records, "Bu kartın boot ayarı kayıtlarını açar (kaydet, karşılaştır, geri yükle).");
            _saveRecord.Click += (s, e) => SaveAsRecord();
            // Komut bittiyse pencerenin okuduğu değerler "Karttaki (şu an)" olarak verilir: kart yeniden okunmaz
            _records.Click += (s, e) => BootRecordsDialog.ShowFor(OwnerWindow, _session.Card,
                _parser.Phase == BootChangePhase.Finished ? _parser.Fields : null, _command);

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
            _close.Click += (s, e) => { var h = CloseRequested; if (h != null) h(this); };
            _list.DoubleClick += (s, e) => GoToSelected();
            _input.KeyDown += Input_KeyDown;
            _timer.Tick += (s, e) => Pump();

            Start(command);
        }

        public CardSession Session { get { return _session; } }

        /// <summary>Kartın komutu bitti ya da yapılamadı.</summary>
        public bool IsOver { get { return _parser.IsOver; } }

        /// <summary>Kart bağlı ve komut bitmedi: pencere kapatılırsa kart boot ekranında kalır.</summary>
        public bool IsUnfinished { get { return !_parser.IsOver && _session.State == CardState.Connected; } }

        /// <summary>Kapatırken sorulmalı mı (tek kart penceresi; kart bir alan sormaya başladı).</summary>
        public bool NeedsCloseConfirm { get { return IsUnfinished && _parser.Phase != BootChangePhase.Starting; } }

        /// <summary>Kartın şu an sorduğu alanın adı.</summary>
        public string CurrentFieldLabel { get { return _parser.Current != null ? _parser.Current.Label : "boot"; } }

        /// <summary>Kart listesi için kısa durum ve rengi.</summary>
        public string ListStatus { get { return _listStatus; } }
        public Color ListColor { get { return _listColor; } }

        /// <summary>Kartın bir değer girilmesini beklediği an (listede vurgulanır).</summary>
        public bool AwaitingUser { get { return _parser.AwaitingInput && !_sending && !_skipAll && _goTo == null; } }

        /// <summary>Kart başarıyla bitti mi.</summary>
        public bool Succeeded { get { return _parser.Phase == BootChangePhase.Finished && !_quitSent; } }

        /// <summary>Kullanıcı Çık (Ctrl+D) ile çıktı.</summary>
        public bool QuitByUser { get { return _parser.Phase == BootChangePhase.Finished && _quitSent; } }

        public void FocusInput() { if (_input.Enabled) _input.Focus(); }

        /// <summary>Kalan alanları değiştirmeden geçip komutu bitirir (girilen değerler kaydedilir).</summary>
        public void FinishRest() { StartSkipAll(); }

        private IWin32Window OwnerWindow
        {
            get
            {
                Form f = FindForm();
                if (f == null) return this;
                return f.Owner ?? (IWin32Window)f;
            }
        }

        /// <summary>Komutu baştan izlemeye başlar (kart penceresinden aynı komut yeniden yazılınca da).</summary>
        public void Start(string command)
        {
            if (_tap == null) _tap = _session.OpenTap();
            else _tap.Take();   // önceki komuttan kalan veri atılır
            _command = (command ?? "").Trim();
            _parser = new BootChangeParser();
            _outcomeLogged = false;
            _startedUtc = DateTime.UtcNow;
            _skipAll = false;
            _goTo = null;
            _autoSerial = -1;
            _autoSteps = 0;
            _finishedRaised = false;
            _quitSent = false;
            _sendError = null;
            _shownVersion = -1;
            _shownLogCount = -1;
            _session.InteractiveInputActive = true;
            _session.BootInputLocked = _lockCardInput;
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
            // Parçalar geldikleri gibi verilir: istem, ardından gelen logdan ayrı okunur
            List<TerminalTap.Chunk> chunks = _tap != null ? _tap.Take() : null;
            if (chunks != null) foreach (TerminalTap.Chunk c in chunks) _parser.Feed(c.Text, c.Utc);
            _parser.Tick(now);

            if (!_parser.IsOver && _session.State != CardState.Connected)
                _parser.Fail("Kartın bağlantısı koptu (" + (_session.LastError ?? ErrorText.ConnectionLost) + ").");

            if (_parser.IsOver)
            {
                LogOutcomeOnce();
                _session.InteractiveInputActive = false;
                _session.BootInputLocked = false;
                _skipAll = false;
                _goTo = null;
                if (!_finishedRaised)
                {
                    _finishedRaised = true;
                    var h = Finished;
                    if (h != null) h(this);   // zamanlayıcıdan (UI iş parçacığı); pencere kapatacaksa kendisi BeginInvoke eder
                }
            }
            else RunAutoStep();

            UpdateView(false);
        }

        private bool _outcomeLogged;

        /// <summary>Sonucu kartın olay günlüğüne bir kez yazar (G49): değişen alanlar eski → yeni.</summary>
        private void LogOutcomeOnce()
        {
            if (_outcomeLogged) return;
            _outcomeLogged = true;
            List<BootField> changed = _parser.Fields.Where(f => f.Changed).ToList();
            string fields = string.Join("; ", changed.Select(f => f.Label + ": \"" + (f.OriginalValue ?? "") + "\" → \"" + f.EffectiveValue + "\""));
            // Kayıt penceresi açıksa kartın şu anki ayarları oraya da geçer (G51.10)
            if (_parser.Phase == BootChangePhase.Finished && _quitSent)
            {
                // Ctrl+D: kalan alanlar okunmadı, girilenlerin kaydedildiği de kesin değil — kayıt penceresine verilmez
                _session.LogEvent(CardEventLog.Boot, _command + " Çık (Ctrl+D) ile bırakıldı" +
                    (changed.Count > 0 ? "; o ana kadar gönderilenler (kartın imajına göre kaydedilmemiş olabilir): " + fields : " — hiçbir alan değiştirilmedi."));
                return;
            }
            if (_parser.Phase == BootChangePhase.Finished) BootRecordsDialog.ProvideCurrent(_session.Card, _parser.Fields, _command);
            if (_parser.Phase == BootChangePhase.Finished)
                _session.LogEvent(CardEventLog.Boot, changed.Count == 0 ? _command + " tamamlandı — hiçbir alan değiştirilmedi."
                                                                        : _command + " tamamlandı — " + changed.Count + " alan değişti: " + fields);
            else
                _session.LogEvent(CardEventLog.Boot, _command + " tamamlanamadı: " + (_parser.FailMessage ?? "bilinmeyen neden") +
                                                     (changed.Count > 0 ? "  Gönderilen değişiklikler: " + fields : ""));
        }

        /// <summary>
        /// "Bu ayarları kaydet…" (G51.9): komut bittikten sonra bu pencerenin okuduğu değerler (gönderilen yeni değerler dahil)
        /// boot ayarı kaydı olarak saklanır; kart yeniden okunmaz. Değeri güvenle okunamayan alan varsa kaydedilmez.
        /// </summary>
        private void SaveAsRecord()
        {
            if (_parser.Phase != BootChangePhase.Finished || _quitSent) return;
            List<string> unknown = _parser.Fields.Where(f => f.Uncertain && f.Entered == null).Select(f => f.Label).ToList();
            if (unknown.Count > 0)
            {
                ChoiceDialog.Ask(OwnerWindowSelf, "Kaydedilemedi", "Bazı alanların değeri güvenle okunamadı.",
                    string.Join(", ", unknown) + " — araya giren log yüzünden. Boot ayarı kayıtları penceresindeki \"Karttan oku ve kaydet…\" " +
                    "kartı yeniden okuyarak kaydeder.", new[] { "Tamam" }, 0, -1);
                return;
            }
            BootRecord replace;
            string name = BootRecordsDialog.AskNewRecord(OwnerWindowSelf, _session.Card, out replace);
            if (name == null) return;
            string cmd = BootRecordLogic.NormalizeCommand(_command);
            BootRecord rec = BootRecordLogic.FromFields(name, cmd, _session.Card.Ip, _parser.Fields, DateTime.Now);
            BootRecordsDialog.AddRecord(_session.Card, rec, replace, cmd, _session, "Boot ayarları penceresinden");
            _status.Text = "✓  \"" + rec.Name + "\" kaydedildi (" + rec.Fields.Count + " alan). Boot ayarı kayıtları penceresinden karşılaştırılıp geri yüklenebilir.";
            _status.ForeColor = Theme.Success;
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

        private IWin32Window OwnerWindowSelf { get { return (IWin32Window)FindForm() ?? this; } }

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
            _quitSent = true;
            try { await _session.SendRawAsync(BootChangeParser.QuitChar); }
            catch (Exception ex) { _quitSent = false; _sendError = "Gönderilemedi: " + ErrorText.Describe(ex); }
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

        /// <summary>Pencerenin klavye önizlemesinden: Ctrl+D çık, F2 değeri düzenle. İşlendiyse true.</summary>
        public bool HandleKey(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D && e.Control) { e.Handled = true; e.SuppressKeyPress = true; var t = QuitAsync(); return true; }
            if (e.KeyCode == Keys.F2) { e.Handled = true; EditCurrentValue(); return true; }
            return false;
        }

        /// <summary>
        /// Ekran kapanırken: bitmemişse olay günlüğüne yazılır, kart terminali serbest bırakılır. Birden çok kez çağrılabilir.
        /// </summary>
        public void Detach()
        {
            if (_detached) return;
            _detached = true;
            if (_parser != null && !_parser.IsOver && !_outcomeLogged && _parser.Fields.Any(f => f.Entered != null))
            {
                _outcomeLogged = true;
                List<BootField> changed = _parser.Fields.Where(f => f.Changed).ToList();
                _session.LogEvent(CardEventLog.Boot, _command + " penceresi komut bitmeden kapatıldı (terminalden devam edilebilir)" +
                    (changed.Count > 0 ? "; o ana kadar gönderilen değişiklikler: " +
                        string.Join("; ", changed.Select(f => f.Label + ": \"" + (f.OriginalValue ?? "") + "\" → \"" + f.EffectiveValue + "\"")) : "."));
            }
            _timer.Stop();
            _timer.Dispose();
            if (_tap != null) { _tap.Dispose(); _tap = null; }
            _session.InteractiveInputActive = false;
            _session.BootInputLocked = false;
            BootChangePanel p;
            if (Active.TryGetValue(_session, out p) && p == this) Active.Remove(_session);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Detach(); _tips.Dispose(); }
            base.Dispose(disposing);
        }

        // Uygulama turu (G36) için: yalnız okunur
        internal Control[] TourStatus { get { return new Control[] { _title, _status }; } }
        internal Control[] TourCurrent { get { return new Control[] { _currentCaption, _currentLabel, _currentValue }; } }
        internal Control[] TourInput { get { return new Control[] { _input, _send }; } }
        internal Control[] TourActions { get { return new Control[] { _keep, _edit, _clear, _prev, _skipRest, _quit }; } }
        internal Control TourList { get { return _list; } }
        internal Control TourLogs { get { return _logBar; } }
        internal Control TourClose { get { return _close; } }
        internal Control[] TourRecords { get { return new Control[] { _records, _saveRecord }; } }

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
            // Düğmeler sığmazsa alt satıra geçer; hiçbiri gizlenmez (Çık önceden dar pencerede görünmüyordu, 1.9)
            int x = 18, y = 176, right = w - 18;
            foreach (Button b in new[] { _keep, _edit, _clear, _prev, _skipRest, _quit })
            {
                if (x > 18 && x + b.Width > right) { x = 18; y += 34; }
                b.Location = new Point(x, y);
                x += b.Width + 6;
            }
            _quit.Left = Math.Max(_quit.Left, right - _quit.Width);   // satırının sağında
            int height = y + _quit.Height + 10;
            if (top.Height != height) top.Height = height;
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
                    if (_quitSent)
                    {
                        status = "⏹  Çıkıldı (Ctrl+D)" + (changed == 0 ? " — hiçbir alan değiştirilmedi."
                                 : " — " + changed + " alan gönderilmişti; kartın imajına göre kaydedilmemiş olabilir, terminalden kontrol edin.");
                        color = Theme.Warning;
                        break;
                    }
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
            ReportListStatus(auto, awaiting, cur);

            // Şu anki alan
            if (_parser.Phase == BootChangePhase.Prompting && cur != null)
            {
                _currentLabel.Text = cur.Label;
                _currentValue.Text = cur.Uncertain
                    ? "kartta:  okunamadı — istemle aynı anda log geldi; kartın değerini terminalde görebilirsiniz"
                    : "kartta:  " + (string.IsNullOrEmpty(cur.CardValue) ? "(boş)" : cur.CardValue);
                _currentValue.ForeColor = cur.Uncertain ? Theme.Warning : Theme.TextMuted;
            }
            else if (_parser.IsOver)
            {
                _currentLabel.Text = _parser.Phase != BootChangePhase.Finished ? "Boot ayarları yapılamadı"
                                   : _quitSent ? "Boot ayarlarından çıkıldı" : "Boot ayarları tamamlandı";
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
            _edit.Enabled = canAct && cur != null && !cur.Uncertain && !string.IsNullOrEmpty(cur.CardValue);
            _clear.Enabled = canAct;
            _prev.Enabled = canAct && _parser.CurrentIndex > 0;
            _skipRest.Enabled = _parser.Phase == BootChangePhase.Prompting && !_skipAll;
            _quit.Enabled = !_parser.IsOver && _session.State == CardState.Connected;
            _saveRecord.Enabled = _parser.Phase == BootChangePhase.Finished && !_quitSent && _parser.Fields.Count > 0;
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

        /// <summary>Çok kartlı penceredeki kart listesi için kısa durum; değişince StateChanged.</summary>
        private void ReportListStatus(bool auto, bool awaiting, BootField cur)
        {
            string text;
            Color color;
            switch (_parser.Phase)
            {
                case BootChangePhase.Starting:
                    text = "Kartın cevabı bekleniyor…"; color = Theme.TextMuted; break;
                case BootChangePhase.Prompting:
                    if (auto) { text = _skipAll ? "Kalan alanlar geçiliyor…" : "Seçilen alana gidiliyor…"; color = Theme.TextMuted; }
                    else if (awaiting) { text = "Giriş bekliyor: " + cur.Label; color = Theme.Accent; }
                    else { text = "Sürüyor…"; color = Theme.TextMuted; }
                    break;
                case BootChangePhase.Finished:
                    int changed = _parser.Fields.Count(f => f.Changed);
                    if (_quitSent) { text = "⏹  Çıkıldı (Ctrl+D)"; color = Theme.Warning; break; }
                    text = "✓  Tamamlandı — " + (changed == 0 ? "değişiklik yok" : changed + " alan değişti");
                    color = Theme.Success;
                    break;
                default:
                    text = "✕  " + (_parser.FailMessage ?? "Tamamlanamadı");
                    color = Theme.Danger;
                    break;
            }
            if (_sendError != null) { text = "⚠  " + _sendError; color = Theme.Danger; }
            if (text == _listStatus && color == _listColor) return;
            _listStatus = text;
            _listColor = color;
            var h = StateChanged;
            if (h != null) h(this);
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
                             string.Join("|", fields.Select(f => f.Label + "=" + f.CardValue + (f.Uncertain ? "?" : "") + ">" + f.Entered));
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
                    string oldText = string.IsNullOrEmpty(f.OriginalValue) ? (f.Uncertain ? "okunamadı" : "(boş)") : f.OriginalValue;
                    string newText = f.EffectiveValue.Length == 0 ? "(boş)" : f.EffectiveValue;
                    int w1 = TextRenderer.MeasureText(g, oldText + "  →  ", _mono, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                    TextRenderer.DrawText(g, oldText + "  →  ", _mono, valueRect, muted, flags);
                    var nr = new Rectangle(valueRect.X + w1, valueRect.Y, Math.Max(0, valueRect.Width - w1), valueRect.Height);
                    TextRenderer.DrawText(g, newText, _mono, nr, current ? fore : Theme.Accent, flags);
                }
                else
                {
                    if (f.Uncertain)
                        TextRenderer.DrawText(g, "okunamadı (araya log girdi) — terminale bakın", _normal, valueRect, current ? fore : Theme.Warning, flags);
                    else
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
