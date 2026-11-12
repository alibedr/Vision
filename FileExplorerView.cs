using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// "Kart Dosya Gezgini" sekmesi: kart seçimi, breadcrumb, Geri / Yenile, ikonlu dosya listesi,
    /// yükleniyor / boş / hata durumları ve kartın ham cevabını gösteren tanılama paneli.
    /// Komutları kartın mevcut oturumu (CardSession.ExecuteAsync) üzerinden çalıştırır; ayrı bağlantı açmaz.
    /// </summary>
    public sealed partial class FileExplorerView : UserControl
    {
        private readonly Func<List<CardConfig>> _cards;
        private readonly Func<CardConfig, CardSession> _sessionOf;
        private readonly AppSettings _settings;
        private readonly Action _saveSettings;
        private readonly Dictionary<CardConfig, FileBrowser> _browsers = new Dictionary<CardConfig, FileBrowser>();

        private CardConfig _card;
        private FileBrowser _browser;
        private CardState _shownState = (CardState)(-1);
        private bool _suppressComboEvent;

        // Üst çubuk
        private readonly Panel _top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Surface };
        private readonly Label _cardLabel = Theme.MakeLabel("KART", Theme.SectionHeader, Theme.TextMuted);
        private readonly ComboBox _cardCombo = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _stateLabel = new GlyphLabel { Font = Theme.Ui, ForeColor = Theme.TextMuted, AutoSize = true, BackColor = Color.Transparent };
        private readonly IconButton _connectBtn = new IconButton(IconKind.Connect, IconTone.Primary) { Label = ConnectionAction.ConnectLabel };

        // Gezinme çubuğu
        private readonly IconButton _backBtn = new IconButton(IconKind.Back, IconTone.Ghost);
        private readonly IconButton _refreshBtn = new IconButton(IconKind.Refresh, IconTone.Ghost);
        private readonly FlowLayoutPanel _crumbs = new FlowLayoutPanel { WrapContents = false, AutoScroll = false, BackColor = Theme.Input };

        // İçerik
        private readonly Label _banner = new GlyphLabel { Closable = true, Dock = DockStyle.Top, Height = 34, Visible = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 8, 0), Cursor = Cursors.Hand };
        private readonly DarkListView _list = new DarkListView();
        private readonly Label _overlay = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, BackColor = Theme.Input, Visible = false };
        private readonly ImageList _icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(20, 20) };

        // Alt çubuk + ham çıktı
        private readonly Label _status = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Button _rawToggle = Theme.MakeButton("Ham cevap", IconKind.ChevronRight, Theme.ButtonKind.Ghost);
        private readonly TextBox _raw = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
        private readonly Panel _rawPanel = new Panel { Dock = DockStyle.Bottom, Height = 170, Visible = false, Padding = new Padding(12, 8, 12, 8) };

        public FileExplorerView(Func<List<CardConfig>> cards, Func<CardConfig, CardSession> sessionOf, AppSettings settings, Action saveSettings)
        {
            _cards = cards;
            _sessionOf = sessionOf;
            _settings = settings;
            _saveSettings = saveSettings;
            BackColor = Theme.Window;
            DoubleBuffered = true;

            BuildIcons();
            BuildLayout();
            RefreshCards();
        }

        // ---------- Yerleşim ----------

        private void BuildLayout()
        {
            // Üst çubuk: başlık + kart seçimi + bağlantı durumu
            var top = _top;
            var title = Theme.MakeLabel("Kart Dosya Gezgini", Theme.Title, Theme.Text);
            title.Location = new Point(18, 9);
            var hint = Theme.MakeLabel("Kartın cihazlarını, klasörlerini ve dosyalarını devs / ls komutlarıyla gezin", Theme.UiSmall, Theme.TextMuted);
            hint.Location = new Point(19, 34);

            Theme.StyleCombo(_cardCombo);
            _cardCombo.Width = 300;
            _cardCombo.Font = new Font("Segoe UI", 9.5f);
            _connectBtn.Width = 134;
            _connectBtn.Height = 30;
            top.Controls.AddRange(new Control[] { title, hint, _cardLabel, _cardCombo, _stateLabel, _connectBtn });
            top.Resize += (s, e) => LayoutTop();
            top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };

            // Gezinme çubuğu: Geri, Yenile, breadcrumb
            var nav = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Window, Padding = new Padding(14, 8, 14, 8) };
            _backBtn.Size = _refreshBtn.Size = new Size(32, 32);
            _backBtn.SetTip(_navTips, "Geri (Backspace)");
            _refreshBtn.SetTip(_navTips, "Yenile (F5)");
            var crumbHost = new Panel { BackColor = Theme.Border, Padding = new Padding(1) };
            _crumbs.Dock = DockStyle.Fill;
            _crumbs.Padding = new Padding(6, 4, 6, 0);
            crumbHost.Controls.Add(_crumbs);
            BuildOperationButtons();
            nav.Controls.AddRange(new Control[] { _backBtn, _refreshBtn, crumbHost, _newFolderBtn, _uploadBtn, _ftpCopiesBtn, _ftpBtn });
            nav.Resize += (s, e) =>
            {
                _backBtn.Location = new Point(12, 8);
                _refreshBtn.Location = new Point(_backBtn.Right + 2, 8);
                // Sağda: [+ Yeni klasör] [⇪ Yükle…] [FTP ayarları]
                _ftpBtn.Location = new Point(nav.Width - 12 - _ftpBtn.Width, 8);
                _ftpCopiesBtn.Location = new Point(_ftpBtn.Left - 4 - _ftpCopiesBtn.Width, 8);
                _uploadBtn.Location = new Point(_ftpCopiesBtn.Left - 4 - _uploadBtn.Width, 8);
                _newFolderBtn.Location = new Point(_uploadBtn.Left - 4 - _newFolderBtn.Width, 8);
                crumbHost.SetBounds(_refreshBtn.Right + 10, 8, Math.Max(100, _newFolderBtn.Left - 10 - (_refreshBtn.Right + 10)), 32);
            };

            // Liste
            _list.SmallImageList = _icons;
            _list.Columns.Add("Ad", 300);
            _list.Columns.Add("Tür", 100);
            _list.Columns.Add("Boyut", 160);   // cihazlarda "226 MB boş / 250 MB" + doluluk çubuğu
            _list.Columns.Add("Değiştirilme", 130);
            _list.Columns.Add("Yol", 300);
            var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(0) };
            _list.Dock = DockStyle.Fill;
            listHost.Controls.Add(_overlay);
            listHost.Controls.Add(_list);
            listHost.Resize += (s, e) => PlaceOverlay(listHost);
            _list.Resize += (s, e) => FitLastColumn();
            Theme.ApplyDarkScrollBars(_list);

            // Alt durum çubuğu
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Sidebar };
            _status.Location = new Point(14, 7);
            _rawToggle.Width = 110;
            _rawToggle.Height = 24;
            _rawToggle.Font = Theme.UiSmall;
            bottom.Controls.Add(_status);
            bottom.Controls.Add(_rawToggle);
            bottom.Resize += (s, e) => _rawToggle.Location = new Point(bottom.Width - _rawToggle.Width - 8, 3);
            bottom.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };

            _rawPanel.BackColor = Theme.Sidebar;
            _raw.BackColor = Theme.Input;
            _raw.ForeColor = Theme.Terminal;
            _raw.Font = Theme.Mono;
            _rawPanel.Controls.Add(_raw);
            Theme.ApplyDarkScrollBars(_raw);

            Controls.Add(listHost);
            Controls.Add(_banner);
            Controls.Add(_rawPanel);
            Controls.Add(bottom);
            Controls.Add(nav);
            Controls.Add(top);

            // Olaylar
            _cardCombo.SelectedIndexChanged += (s, e) => { if (!_suppressComboEvent) SelectCard(SelectedComboCard()); };
            _connectBtn.Click += async (s, e) =>
            {
                // Kartın MEVCUT oturumu üzerinden bağlanır (Kart Gezgini'ndeki "Bağlantı Kur" ile aynı)
                if (_card != null) await _sessionOf(_card).ConnectAsync();
            };
            _backBtn.Click += async (s, e) => { if (_browser != null) await _browser.BackAsync(); };
            _refreshBtn.Click += async (s, e) => await RefreshCurrentAsync();
            _list.ItemActivate += async (s, e) =>
            {
                if ((ModifierKeys & Keys.Control) != 0) return;   // Ctrl+Enter: "Varsayılan uygulamayla aç (salt okunur)"
                await ActivateSelectedAsync();
            };
            _list.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Back) { e.SuppressKeyPress = true; if (_browser != null) await _browser.BackAsync(); }
                else if (e.KeyCode == Keys.F5) { e.SuppressKeyPress = true; await RefreshCurrentAsync(); }
                else await HandleOperationKeyAsync(e);
            };
            WireOperations();
            _banner.Click += (s, e) => _banner.Visible = false;
            new DarkToolTip().SetToolTip(_banner, "Kapatmak için tıklayın");
            _rawToggle.Click += (s, e) =>
            {
                _rawPanel.Visible = !_rawPanel.Visible;
                ((IconButton)_rawToggle).Icon = _rawPanel.Visible ? IconKind.ChevronDown : IconKind.ChevronRight;
            };
            new DarkToolTip().SetToolTip(_rawToggle, "Kartın son komuta verdiği cevabı olduğu gibi göster (tanılama)");
        }

        // ---------- Kart seçimi ----------

        private sealed class CardItem
        {
            public CardConfig Card;
            public override string ToString()
            {
                return (Card.Group != null ? Card.Group.Name + "  ·  " : "") + Card.Label;   // "IP (Ad)"
            }
        }

        private CardConfig SelectedComboCard()
        {
            var item = _cardCombo.SelectedItem as CardItem;
            return item != null ? item.Card : null;
        }

        /// <summary>
        /// Kartın bağlantı durumu (oturum yoksa bağlı değil). Ayarlanırsa kart listesinde yalnız bağlı kartlar görünür.
        /// Oturum oluşturmadan sorar.
        /// </summary>
        public Func<CardConfig, CardState> StateOf { get; set; }

        private string _listedSignature;

        /// <summary>
        /// Kart listesinde gösterilecek kartlar (G33.5): yalnız bağlı / bağlanmakta olanlar. O an seçili kart bağlantısı
        /// kopsa da listede kalır (ör. reboot sırasında açık klasör kaybolmasın; "Bağlan" ile devam edilir).
        /// </summary>
        private List<CardConfig> ListedCards(List<CardConfig> all)
        {
            if (StateOf == null) return all;
            return all.Where(c => c == _card || SelectionLogic.IsLive(StateOf(c))).ToList();
        }

        private string ListedSignature(List<CardConfig> listed)
        {
            return string.Join("|", listed.Select(c => c.GetHashCode() + ":" + c.Label + ":" + (c.Group != null ? c.Group.Name : "")));
        }

        /// <summary>Yapılandırılmış kart listesi ya da bağlantılar değişince çağrılır; seçim mümkünse korunur.</summary>
        public void RefreshCards()
        {
            List<CardConfig> all = _cards();
            foreach (var gone in _browsers.Keys.Where(k => !all.Contains(k)).ToList()) _browsers.Remove(gone);
            if (_card != null && !all.Contains(_card)) { _card = null; _browser = null; }   // kart silindi
            List<CardConfig> cards = ListedCards(all);
            _listedSignature = ListedSignature(cards);

            _suppressComboEvent = true;
            _cardCombo.BeginUpdate();
            _cardCombo.Items.Clear();
            foreach (CardConfig c in cards) _cardCombo.Items.Add(new CardItem { Card = c });
            int idx = _card != null ? cards.IndexOf(_card) : -1;
            if (idx < 0 && cards.Count > 0) idx = 0;
            _cardCombo.SelectedIndex = idx;
            _cardCombo.EndUpdate();
            _suppressComboEvent = false;

            CardConfig sel = idx >= 0 ? cards[idx] : null;
            if (sel != _card) SelectCard(sel);
            else RefreshUi();
        }

        /// <summary>
        /// Başka bir yerden (ör. Kart Gezgini'nde "Dosyalarını gez") belirli bir kartı seçmek için. Kart bağlı değilse de
        /// seçilir ve listeye eklenir (seçili kart her zaman listede durur); "Bağlan" ile bağlanılabilir.
        /// </summary>
        public void ShowCard(CardConfig card)
        {
            if (card == null) return;
            if (card != _card)
            {
                SelectCard(card);
                RefreshCards();
            }
        }

        /// <summary>Dosya Gezgini'nde seçili kart (kart yoksa null).</summary>
        public CardConfig SelectedCard { get { return _card; } }

        /// <summary>Seçili kart değişti (Kart Gezgini'nde vurgulamak için).</summary>
        public event Action<CardConfig> SelectedCardChanged;

        private void SelectCard(CardConfig card)
        {
            bool changed = _card != card;
            _card = card;
            if (changed) { var h = SelectedCardChanged; if (h != null) h(card); }
            _browser = null;
            if (card != null && !_browsers.TryGetValue(card, out _browser))
            {
                CardSession session = _sessionOf(card);
                var b = new FileBrowser(session.ExecuteAsync);   // kartın mevcut oturumu; ayrı bağlantı yok
                b.Changed += () => { if (b == _browser) RefreshUi(); };
                // FTP kopyaları listesi değişince (yükleme başladı / silindi) düğme durumu güncellenir
                session.FtpCopies.Changed += () =>
                {
                    if (!IsHandleCreated) return;
                    try { BeginInvoke(new Action(() => { if (b == _browser) RefreshUi(); })); } catch (InvalidOperationException) { }
                };
                _browsers.Add(card, b);
                _browser = b;
            }
            _banner.Visible = false;
            _shownState = (CardState)(-1);
            RefreshUi();
            AutoLoadIfPossible();
        }

        /// <summary>Kart bağlıysa ve cihaz listesi henüz yüklenmediyse devs çalıştırır.</summary>
        private async void AutoLoadIfPossible()
        {
            FileBrowser b = _browser;
            if (b == null || b.Loaded || b.Busy || _card == null) return;
            if (_sessionOf(_card).State != CardState.Connected) return;
            await b.LoadDevicesAsync();
        }

        private async System.Threading.Tasks.Task RefreshCurrentAsync()
        {
            if (_browser == null) return;
            if (!_browser.Loaded) await _browser.LoadDevicesAsync();
            else await _browser.RefreshAsync();
        }

        private async System.Threading.Tasks.Task OpenSelectedAsync()
        {
            if (_browser == null || _browser.Busy || _list.SelectedItems.Count == 0) return;
            if (_card == null || _sessionOf(_card).State != CardState.Connected) return;
            var entry = _list.SelectedItems[0].Tag as FsEntry;
            if (entry != null) await _browser.OpenAsync(entry);
        }

        // ---------- Ana pencere zamanlayıcısından ----------

        /// <summary>Seçili kartın bağlantı durumu değişirse arayüzü günceller; bağlanınca cihazları yükler.</summary>
        public void Tick()
        {
            // Bağlanan / kopan kartlar: liste yalnız bağlı kartları gösterir (değişince yenilenir)
            if (StateOf != null)
            {
                List<CardConfig> listed = ListedCards(_cards());
                if (ListedSignature(listed) != _listedSignature) RefreshCards();
            }
            if (_card == null || !Visible) return;
            // Cihaz boyutları: Cihazlar görünürken ve boştayken arka planda birer birer (G34)
            if (_browser != null && !_browser.Busy && _sessionOf(_card).State == CardState.Connected)
            {
                var ignored = _browser.QueryNextSpaceAsync();
            }
            CardState st = _sessionOf(_card).State;
            if (st == _shownState) return;
            RefreshUi();
            if (st == CardState.Connected) AutoLoadIfPossible();
        }

        // ---------- Arayüzü modelden güncelle ----------

        private void RefreshUi()
        {
            CardState st = _card != null ? _sessionOf(_card).State : CardState.Disconnected;
            _shownState = st;
            bool connected = st == CardState.Connected;
            FileBrowser b = _browser;
            bool busy = b != null && b.Busy;

            // Bağlantı durumu
            _stateLabel.Text = _card == null ? "" : "●  " + Theme.StateText(st);
            _stateLabel.ForeColor = Theme.StateColor(st);
            _connectBtn.Visible = _card != null && !SelectionLogic.IsLive(st);
            LayoutTop();

            // Gezinme düğmeleri
            SetEnabled(_backBtn, connected && b != null && b.CanGoBack && !busy);
            SetEnabled(_refreshBtn, connected && b != null && !busy);
            RebuildCrumbs(b, connected && !busy);

            // Liste (hata durumunda önceki içerik korunur)
            if (b != null) FillList(b);
            else _list.Items.Clear();
            _list.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            UpdateOperationButtons(connected, busy);

            // Bilgi / hata şeridi
            if (b != null && b.Error != null && b.ErrorIsWarning)
                ShowBanner("⚠  " + b.Error, Theme.WarningBanner, Theme.WarningBannerText);   // uyarı (eksik komut vb.)
            else if (b != null && b.Error != null) ShowBanner("⚠  " + b.Error, Theme.ErrorBanner, Color.White);
            else if (b != null && b.Notice != null) ShowBanner("ℹ  " + b.Notice, Theme.InfoBanner, Theme.InfoBannerText);
            else if (!busy) _banner.Visible = false;

            // Boş / yükleniyor / bağlı değil katmanı
            string overlay = null;
            if (_card == null)
                overlay = _cards().Count == 0
                    ? "Henüz kart yok.\n\nSoldaki Kart Gezgini'nden \"Kart Ekle\" ile kart ekleyin."
                    : "Bağlı kart yok.\n\nKart Gezgini'nden bir karta bağlanın; bağlanan kartlar yukarıdaki listede görünür.";
            else if (!connected)
                overlay = (st == CardState.Recovering
                              ? "Bu kartın bağlantısı kesildi (" + (_sessionOf(_card).LastError ?? ErrorText.ConnectionLost) + ").\n" +
                                "Kart yeniden ulaşılabilir olunca bağlantı otomatik kurulacak ve cihazlar yüklenecek."
                              : st == CardState.Error
                              ? "Bu kartın bağlantısı yok (" + (_sessionOf(_card).LastError ?? ErrorText.ConnectionLost) + ")."
                              : "Bu kart bağlı değil.") +
                          "\n\nDosya Gezgini ayrı bir bağlantı açmaz; kartın mevcut bağlantısını kullanır.\n" +
                          "Bağlanmak için sağ üstteki \"Bağlantı Kur\"a basın.";
            else if (busy && _list.Items.Count == 0)
                overlay = "Yükleniyor…\n\n" + b.PendingCommand;
            else if (b != null && b.Loaded && b.Entries.Count == 0 && b.Error == null)
                overlay = !b.Current.IsRoot ? "Bu klasör boş." : "Kart cihaz listesi döndürmedi.";
            else if (b != null && !b.Loaded && !busy && b.Error != null)
                overlay = "Cihaz listesi alınamadı.\n\n\"Yenile\" ile tekrar deneyin.";
            _overlay.Text = overlay ?? "";
            _overlay.Visible = overlay != null;
            if (_overlay.Visible) { PlaceOverlay(_overlay.Parent); _overlay.BringToFront(); }

            // Durum satırı ve ham cevap
            _status.Text = StatusText(b, busy, connected);
            ShellResponse r = b != null ? b.LastResponse : null;
            _raw.Text = r == null ? "(henüz komut çalıştırılmadı)"
                : "> " + r.Command + Environment.NewLine + r.Raw.Replace("\n", Environment.NewLine) + Environment.NewLine +
                  "[dönüş değeri: " + (r.ReturnCode.HasValue ? r.ReturnCode.Value.ToString() : "bulunamadı") +
                  (r.TimedOut ? ", zaman aşımı" : "") + (r.Failure != null ? ", " + r.Failure : "") + "]";
        }

        /// <summary>Üst çubuk sağdan sola dizilir: [KART] [kart seçimi] [● durum] [Bağlantı Kur]</summary>
        private void LayoutTop()
        {
            int right = _top.Width - 18;
            _connectBtn.Location = new Point(right - _connectBtn.Width, 17);
            int stateRight = _connectBtn.Visible ? _connectBtn.Left - 12 : right;
            _stateLabel.Location = new Point(stateRight - _stateLabel.Width, 23);
            _cardCombo.Location = new Point(_stateLabel.Left - 14 - _cardCombo.Width, 19);
            _cardLabel.Location = new Point(_cardCombo.Left - _cardLabel.Width - 8, 25);
        }

        private static string StatusText(FileBrowser b, bool busy, bool connected)
        {
            if (b == null) return "";
            if (busy) return "Yükleniyor…  " + b.PendingCommand;
            if (!connected || !b.Loaded) return "";
            string where = b.Current.IsRoot ? "Cihazlar" : b.Current.Path;
            string count = b.Entries.Count + (b.Current.IsRoot ? " cihaz" : " öğe");
            int bad = b.Current.IsRoot ? b.Entries.Count(e => e.NotEnterable) : 0;
            if (bad > 0) count += "  ·  " + bad + " cihazın içine girilemedi";
            string rc = b.LastResponse != null && b.LastResponse.ReturnCode.HasValue && !b.Current.IsRoot
                ? "  ·  son dönüş değeri " + b.LastResponse.ReturnCode.Value : "";
            // Bir diskin içindeyken o diskin doluluğu (G34)
            string space = "";
            if (!b.Current.IsRoot)
            {
                string dev;
                DeviceSpace sp = b.SpaceForPath(b.Current.Path, out dev);
                if (sp != null) space = "  ·  " + dev + ": " + sp.Detail;
            }
            return count + "  ·  " + where + rc + space;
        }

        private void ShowBanner(string text, Color back, Color fore)
        {
            _banner.Text = text;
            _banner.BackColor = back;
            _banner.ForeColor = fore;
            _banner.Visible = true;
        }

        private static void SetEnabled(Button b, bool enabled)
        {
            if (b.Enabled != enabled) b.Enabled = enabled;   // IconButton pasif görünümünü kendisi çizer
        }

        private readonly ToolTip _navTips = new DarkToolTip();

        private void FillList(FileBrowser b)
        {
            // Aynı içerik yeniden çizilmesin (seçim ve kaydırma korunur)
            string signature = (b.Current.Path ?? "<root>") + "|" + string.Join("|", b.Entries.Select(e => e.Name + ":" + e.KindText));
            if (_list.Tag as string == signature) { UpdateSizeCells(); return; }
            _list.Tag = signature;

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (FsEntry e in b.Entries)
            {
                var item = new ListViewItem(e.Name, IconKey(e.Kind)) { Tag = e };
                item.SubItems.Add(e.KindText);
                item.SubItems.Add(e.SizeText);
                item.SubItems.Add(e.Modified ?? "");
                item.SubItems.Add(e.FullPath);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) { _list.Items[0].Selected = true; _list.Items[0].Focused = true; }
            FitLastColumn();
        }

        /// <summary>
        /// Cihaz boyutları arka planda geldikçe yalnız Boyut hücreleri güncellenir (liste baştan çizilmez; seçim ve
        /// kaydırma korunur).
        /// </summary>
        private void UpdateSizeCells()
        {
            foreach (ListViewItem item in _list.Items)
            {
                var e = item.Tag as FsEntry;
                if (e == null || item.SubItems.Count < 3) continue;
                string text = e.SizeText;
                if (item.SubItems[2].Text != text) item.SubItems[2].Text = text;
            }
        }

        private void RebuildCrumbs(FileBrowser b, bool clickable)
        {
            List<Breadcrumb> crumbs = b != null && b.Loaded ? b.Breadcrumbs : new List<Breadcrumb>();
            string sig = string.Join("/", crumbs.Select(c => c.Label)) + "|" + clickable;
            if (_crumbs.Tag as string == sig) return;
            _crumbs.Tag = sig;

            _crumbs.SuspendLayout();
            foreach (Control c in _crumbs.Controls.Cast<Control>().ToList()) c.Dispose();
            for (int i = 0; i < crumbs.Count; i++)
            {
                Breadcrumb crumb = crumbs[i];
                bool last = i == crumbs.Count - 1;
                if (i > 0)
                    _crumbs.Controls.Add(new Label { Text = "›", AutoSize = true, ForeColor = Theme.TextFaint, Font = Theme.Ui, Margin = new Padding(0, 4, 0, 0) });
                var btn = Theme.MakeButton(crumb.Label, i == 0 ? IconKind.Home : IconKind.None, Theme.ButtonKind.Ghost);
                btn.IconSize = 14;
                btn.AutoSize = true;
                btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                btn.Height = 22;
                btn.Margin = new Padding(0, 0, 0, 0);
                btn.Padding = new Padding(2, 0, 2, 0);
                btn.Font = last ? Theme.UiBold : Theme.Ui;
                btn.ForeColor = last ? Theme.Text : Theme.Accent;
                btn.Enabled = clickable && !last;
                FsLocation target = crumb.Location;
                btn.Click += async (s, e) => { if (_browser != null) await _browser.GoToAsync(target); };
                MakeCrumbDropTarget(btn, target);
                _crumbs.Controls.Add(btn);
            }
            _crumbs.ResumeLayout();
        }

        private void PlaceOverlay(Control host)
        {
            if (host == null) return;
            int top = 30;   // sütun başlıkları görünür kalsın
            _overlay.SetBounds(0, top, host.ClientSize.Width, Math.Max(0, host.ClientSize.Height - top));
        }

        private void FitLastColumn()
        {
            if (_list.Columns.Count < 2) return;
            int last = _list.Columns.Count - 1;
            int used = 0;
            for (int i = 0; i < last; i++) used += _list.Columns[i].Width;
            int w = _list.ClientSize.Width - used - 4;
            if (w > 120 && _list.Columns[last].Width != w) _list.Columns[last].Width = w;
        }

        // ---------- İkonlar (dosya gerektirmez, çalışma anında çizilir) ----------

        private static string IconKey(FsKind k)
        {
            switch (k)
            {
                case FsKind.Device: return "device";
                case FsKind.Folder: return "folder";
                case FsKind.File: return "file";
                default: return "unknown";
            }
        }

        private void BuildIcons()
        {
            _icons.Images.Add("device", DrawIcon(DrawDevice));
            _icons.Images.Add("folder", DrawIcon(g => DrawFolder(g, Color.FromArgb(240, 190, 72), Color.FromArgb(214, 160, 48), false)));
            _icons.Images.Add("unknown", DrawIcon(g => DrawFolder(g, Color.FromArgb(120, 128, 140), Color.FromArgb(96, 104, 116), true)));
            _icons.Images.Add("file", DrawIcon(DrawFile));
        }

        private static Bitmap DrawIcon(Action<Graphics> draw)
        {
            var bmp = new Bitmap(20, 20);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g);
            }
            return bmp;
        }

        private static void DrawDevice(Graphics g)
        {
            using (var path = Rounded(new Rectangle(2, 5, 16, 11), 3))
            using (var body = new SolidBrush(Color.FromArgb(150, 160, 176)))
            using (var edge = new Pen(Color.FromArgb(96, 104, 118)))
            {
                g.FillPath(body, path);
                g.DrawPath(edge, path);
            }
            using (var slot = new Pen(Color.FromArgb(90, 98, 112), 1.4f)) g.DrawLine(slot, 5, 12, 11, 12);
            using (var led = new SolidBrush(Color.FromArgb(46, 194, 126))) g.FillEllipse(led, 13, 10, 3.5f, 3.5f);
        }

        private static void DrawFolder(Graphics g, Color front, Color back, bool question)
        {
            using (var b = new SolidBrush(back)) g.FillPath(b, Rounded(new Rectangle(2, 4, 8, 5), 2));
            using (var b = new SolidBrush(back)) g.FillRectangle(b, 2, 6, 16, 11);
            using (var path = Rounded(new Rectangle(2, 7, 16, 10), 2))
            using (var f = new SolidBrush(front))
                g.FillPath(f, path);
            if (question)
            {
                using (var font = new Font("Segoe UI", 7f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "?", font, new Rectangle(2, 7, 16, 10), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static void DrawFile(Graphics g)
        {
            var pts = new[] { new Point(4, 2), new Point(12, 2), new Point(16, 6), new Point(16, 18), new Point(4, 18) };
            using (var b = new SolidBrush(Color.FromArgb(226, 232, 240))) g.FillPolygon(b, pts);
            using (var p = new Pen(Color.FromArgb(150, 160, 176))) g.DrawPolygon(p, pts);
            using (var fold = new SolidBrush(Color.FromArgb(180, 190, 204)))
                g.FillPolygon(fold, new[] { new Point(12, 2), new Point(12, 6), new Point(16, 6) });
            using (var line = new Pen(Color.FromArgb(150, 160, 176)))
            {
                g.DrawLine(line, 6, 10, 14, 10);
                g.DrawLine(line, 6, 13, 14, 13);
                g.DrawLine(line, 6, 16, 11, 16);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _icons.Dispose();
                _itemMenu.Dispose();
                _opTips.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Koyu temalı ayrıntı listesi (sütun başlıkları ve satırlar elle çizilir).</summary>
    internal sealed class DarkListView : ListView
    {
        /// <summary>Sürükle-bırakta üzerine gelinen klasör satırı (-1: yok).</summary>
        public int DropHighlightIndex = -1;

        public DarkListView()
        {
            View = View.Details;
            FullRowSelect = true;
            MultiSelect = false;
            HideSelection = false;
            OwnerDraw = true;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
            using (var p = new Pen(Theme.Border))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 6, e.Bounds.Right - 1, e.Bounds.Bottom - 6);
            }
            var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text.ToUpperInvariant(), Theme.SectionHeader, r, Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;   // satırlar OnDrawSubItem'da çizilir
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            Color back = e.ItemIndex == DropHighlightIndex ? Theme.DropHighlight
                       : selected ? Theme.Selection : (e.ItemIndex % 2 == 0 ? Theme.Input : Theme.ZebraRow);
            using (var bg = new SolidBrush(back)) e.Graphics.FillRectangle(bg, e.Bounds);

            var entry = e.Item.Tag as FsEntry;
            if (e.ColumnIndex == 0)
            {
                int x = e.Bounds.X + 8;
                if (SmallImageList != null && e.Item.ImageKey != null && SmallImageList.Images.ContainsKey(e.Item.ImageKey))
                {
                    Image img = SmallImageList.Images[e.Item.ImageKey];
                    e.Graphics.DrawImage(img, x, e.Bounds.Y + (e.Bounds.Height - 20) / 2, 20, 20);
                }
                x += 28;
                bool notEnterable = entry != null && entry.NotEnterable;   // içine girilemeyen cihaz soluk çizilir
                Color fore = !Enabled ? Theme.TextFaint : notEnterable ? Theme.TextMuted : Theme.Text;
                Font f = entry != null && !notEnterable && (entry.Kind == FsKind.Folder || entry.Kind == FsKind.Device) ? Theme.UiBold : Font;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, f, new Rectangle(x, e.Bounds.Y, e.Bounds.Right - x - 4, e.Bounds.Height),
                    fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else
            {
                var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                bool pathColumn = e.ColumnIndex == Columns.Count - 1;
                Color fore = pathColumn ? Theme.TextFaint
                           : e.ColumnIndex == 1 && entry != null && entry.NotEnterable ? Theme.Warning : Theme.TextMuted;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, pathColumn ? Theme.Mono : Font, r, fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                // Cihazın doluluk çubuğu (Boyut sütunu): %90 üstü sarı, %97 üstü kırmızı
                if (e.ColumnIndex == 2 && entry != null && entry.Kind == FsKind.Device && entry.Space != null && entry.Space.HasFree)
                {
                    double ratio = entry.Space.UsedRatio;
                    var track = new Rectangle(r.X, e.Bounds.Bottom - 6, Math.Max(10, r.Width - 8), 3);
                    using (var tb = new SolidBrush(Theme.Border)) e.Graphics.FillRectangle(tb, track);
                    Color fill = ratio >= 0.97 ? Theme.Danger : ratio >= 0.90 ? Theme.Warning : Theme.Accent;
                    int w = (int)Math.Round(track.Width * ratio);
                    if (w > 0) using (var fb = new SolidBrush(fill)) e.Graphics.FillRectangle(fb, new Rectangle(track.X, track.Y, w, track.Height));
                }
            }
        }

        // Satır yüksekliğini artırmak için küçük resim listesi 20 px; ayrıca seçim değişince tüm satırı yeniden çiz
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }
    }
}
