using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Uygulama kabuğu: solda Kart Gezgini, sağda üst araç çubuğu + kart ızgarası, altta durum çubuğu.
    /// Kart oturumlarının (bağlantıların) sahibi burasıdır; görünümler açılıp kapansa da oturumlar yaşar.
    /// Her kartın üç durumu ayrıdır: yapılandırma (_config), bağlantı (CardSession.State), görünüm (_grid).
    /// Bağlantı işlemleri görünümü, görünüm işlemleri bağlantıyı değiştirmez.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly string _configPath;
        /// <summary>%APPDATA% yapılandırma klasörüne yazılamadıysa durum çubuğunda gösterilecek not.</summary>
        private readonly string _configNote;
        private readonly AppConfig _config;
        private readonly Dictionary<CardConfig, CardSession> _sessions = new Dictionary<CardConfig, CardSession>();

        private readonly SplitContainer _split = new SplitContainer();
        private readonly CardExplorer _explorer;
        private readonly CardGrid _grid = new CardGrid { Dock = DockStyle.Fill };
        private readonly Label _workspaceTitle = Theme.MakeLabel("Telnet", Theme.Title, Theme.Text);
        private readonly Label _workspaceSub = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly ComboBox _lineEnd = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 158 };

        /// <summary>Satır sonu seçeneği: kutuda kısa ad, açılan listede Türkçe açıklama gösterilir.</summary>
        private sealed class LineEndingOption
        {
            public string Short;     // kutuda görünen
            public string Detail;    // listede görünen açıklama
            public string Hint;      // ne zaman seçilir
            public override string ToString() { return Short; }
        }

        private static readonly LineEndingOption[] LineEndingOptions =
        {
            new LineEndingOption { Short = "CR+LF  (varsayılan)", Detail = "CR+LF  —  Satır başı + Yeni satır  (\\r\\n)", Hint = "Varsayılan. Telnet standardı; çoğu cihaz bununla çalışır." },
            new LineEndingOption { Short = "CR", Detail = "CR  —  Yalnız Satır başı  (\\r)", Hint = "Her Enter'da iki istem (->) geliyorsa bunu seçin." },
            new LineEndingOption { Short = "LF", Detail = "LF  —  Yalnız Yeni satır  (\\n)", Hint = "Linux/Unix tarzı cihazlar; komut gidip çalışmıyorsa deneyin." },
            new LineEndingOption { Short = "Yok", Detail = "Yok  —  Hiçbir şey ekleme", Hint = "Yazılan metin olduğu gibi gider (satır sonu eklenmez)." },
        };
        private readonly CheckBox _echo = new ToggleSwitch { Text = "Yerel eko" };
        private readonly CheckBox _timestamp = new ToggleSwitch { Text = "Zaman damgası" };
        private readonly CheckBox _separate = new ToggleSwitch { Text = "Dönüş değerlerini ayır" };
        // Ortak terminal yakınlaştırması: [−] [%100] [+]
        private readonly IconButton _zoomOut = new IconButton(IconKind.Minus, IconTone.Ghost) { IconSize = 14 };
        private readonly IconButton _zoomIn = new IconButton(IconKind.Plus, IconTone.Ghost) { IconSize = 14 };
        private readonly IconButton _zoomReset = new IconButton(IconKind.None, IconTone.Ghost);
        private readonly NumericUpDown _maxLines = new NumericUpDown
        {
            Minimum = AppSettings.MinTerminalLines,
            Maximum = AppSettings.MaxTerminalLinesLimit,
            Increment = 500,
            Width = 76,
            ThousandsSeparator = true,
        };
        private readonly Button _closeAllBtn = Theme.MakeButton("Tümünü kapat", IconKind.Close, Theme.ButtonKind.Ghost);
        private readonly Label _statusLeft = new GlyphLabel { Font = Theme.UiSmall, ForeColor = Theme.TextMuted, AutoSize = true, BackColor = Color.Transparent };
        private readonly Label _statusRight = Theme.MakeLabel("", Theme.UiSmall, Theme.TextFaint);
        private readonly Label _signature = Theme.MakeLabel(AppInfo.Signature, Theme.UiSmall, Theme.TextMuted);
        private readonly Timer _uiTimer = new Timer { Interval = 40 };
        private readonly GlobalCommandBar _globalBar;
        private readonly FileExplorerView _fileExplorer;
        private readonly ConnectionMonitor _monitor;
        // İzleyicinin zamanlayıcı iş parçacığından güvenle okuyabilmesi için oturumların değişmez kopyası
        private volatile CardSession[] _sessionSnapshot = new CardSession[0];
        private readonly Panel _workspaceHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
        private readonly TabStripButton _workspaceTab = new TabStripButton("Telnet", IconKind.Terminal);   // iç adı: çalışma alanı (Workspace)
        private readonly IconButton _sidebarToggle = new IconButton(IconKind.SidebarHide, IconTone.Ghost) { Inset = 5, IconSize = 18, TabStop = false };
        private readonly TabStripButton _filesTab = new TabStripButton("Kart Dosya Gezgini", IconKind.Folder);
        private readonly TabStripButton _commandsTab = new TabStripButton("Komut Kütüphanesi", IconKind.Book);
        private readonly CommandLibraryView _commandLibrary;
        private readonly TabStripButton _sentinelTab = new TabStripButton("Sentinel", IconKind.Pulse);
        private readonly SentinelView _sentinelView;
        private MainTab _activeTab = MainTab.Workspace;

        private enum MainTab { Workspace, Files, Commands, Sentinel }
        private volatile bool _statesDirty = true;

        public MainForm()
        {
            bool fallback;
            _configPath = ConfigStore.ResolvePath(out fallback);
            ConfigStore.MigrateIfMissing(_configPath);   // exe yanındaki eski Konfig\config.xml ilk açılışta %APPDATA%'ya kopyalanır
            _config = ConfigStore.Load(_configPath);
            if (fallback) _configNote = "Ayarlar kaydedilemiyor (%APPDATA%\\" + AppInfo.DataFolder + " klasörüne yazılamadı)";
            // FTP bilgisi artık her karttan otomatik alınır: eski sürümlerde elle girilmiş parola dosyada tutulmaz
            if (_config.Settings.Transfer != null) _config.Settings.Transfer.ClearLegacyCredentials();

            Text = AppInfo.Name + "  —  " + AppInfo.Author;
            if (Theme.AppIcon != null) Icon = Theme.AppIcon;
            Size = new Size(1360, 820);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            KeyPreview = true;

            _explorer = new CardExplorer(_config.Groups, StateOf, IsOpen) { Dock = DockStyle.Fill };
            // Genel komut çubuğunun hedefi: yalnız çalışma alanında açık olan kartlar
            _globalBar = new GlobalCommandBar(() => _grid.Views.Select(v => v.Session).ToList(), _config.Settings, SaveConfig) { Dock = DockStyle.Bottom };
            // Dosya gezgini kartların MEVCUT oturumlarını kullanır (GetSession); ayrı bağlantı açmaz
            _fileExplorer = new FileExplorerView(() => _config.Groups.SelectMany(g => g.Cards).ToList(), GetSession, _config.Settings, SaveConfig)
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };
            // Komut kütüphanesi exe'ye gömülü (Komutlar\Komutlar.xml kaynağı); uygulamadan değiştirilemez
            _commandLibrary = new CommandLibraryView(CommandLibrary.Embedded())   // gömülü, salt okunur
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };
            // Kütüphaneden "Karta aktar": komut seçilen kartın (ya da genel çubuğun) komut satırına yazılır, gönderilmez
            _commandLibrary.EnableTransfer(() => _config.Groups, StateOf, TransferCommandToCard, TransferCommandToAll);
            // Sentinel: kartın içindeki web sunucusunun sayfası uygulama içinde (yalnız var olan oturumlar; yeni bağlantı açmaz)
            _sentinelView = new SentinelView(() => _config.Groups.SelectMany(g => g.Cards).ToList(), FindSession)
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };
            BuildLayout();
            LoadSettingsToUi();
            WireEvents();
            _grid.ApplyZoom(_config.Settings.TerminalZoom);
            UpdateZoomUi();

            // Periyodik bağlantı izleme: tek zamanlayıcı, tüm kartlar (yeni bağlantı açmaz)
            _monitor = new ConnectionMonitor(() => _sessionSnapshot, _config.Settings.Monitor.MonitorTickMs);

            _uiTimer.Tick += (s, e) => OnUiTick();
            _uiTimer.Start();
        }

        // ---------- Yerleşim ----------

        private void BuildLayout()
        {
            _split.Dock = DockStyle.Fill;
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SplitterWidth = 3;
            _split.BackColor = Theme.Border;
            _split.Panel1.BackColor = Theme.Sidebar;
            _split.Panel2.BackColor = Theme.Window;
            _split.Panel1.Controls.Add(_explorer);

            // Çalışma alanı üst çubuğu
            var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Surface };
            _workspaceTitle.Location = new Point(18, 8);
            _workspaceSub.Location = new Point(19, 32);

            var options = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Theme.Surface,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            var lineLabel = Theme.MakeLabel("Satır sonu (Enter)", Theme.Ui, Theme.TextMuted);
            lineLabel.Margin = new Padding(0, 8, 4, 0);
            Theme.StyleCombo(_lineEnd);
            _lineEnd.Items.AddRange(LineEndingOptions);
            _lineEnd.DrawMode = DrawMode.OwnerDrawFixed;
            _lineEnd.ItemHeight = 20;
            _lineEnd.DropDownWidth = 600;
            _lineEnd.DrawItem += LineEnd_DrawItem;
            ((DarkComboBox)_lineEnd).ClosedText = o => ((LineEndingOption)o).Short;
            _lineEnd.Margin = new Padding(0, 4, 12, 0);
            foreach (var cb in new[] { _echo, _timestamp, _separate })
            {
                Theme.StyleCheck(cb);
                cb.Margin = new Padding(0, 5, 14, 0);
            }
            _closeAllBtn.Size = new Size(124, 30);
            var maxLinesLabel = Theme.MakeLabel("Satır sınırı", Theme.Ui, Theme.TextMuted);
            maxLinesLabel.Margin = new Padding(2, 8, 4, 0);
            Theme.StyleNumeric(_maxLines);
            _maxLines.Margin = new Padding(0, 4, 12, 0);
            _closeAllBtn.Margin = new Padding(0, 1, 0, 0);
            // Gruplar arasında ince ayraç: [satır sonu] | [eko · zaman damgası · kayıt] | [satır sınırı] | [tümünü kapat]
            Func<Control> sep = () => new Panel { Width = 1, Height = 22, BackColor = Theme.Border, Margin = new Padding(4, 6, 16, 0) };
            var zoomLabel = Theme.MakeLabel("Yazı", Theme.Ui, Theme.TextMuted);
            zoomLabel.Margin = new Padding(0, 8, 2, 0);
            _zoomOut.Size = _zoomIn.Size = new Size(26, 28);
            _zoomReset.Size = new Size(50, 28);
            _zoomReset.Font = Theme.Ui;
            _zoomOut.Margin = _zoomIn.Margin = _zoomReset.Margin = new Padding(0, 2, 0, 0);
            _zoomIn.Margin = new Padding(0, 2, 12, 0);
            options.Controls.AddRange(new Control[] { lineLabel, _lineEnd, sep(), _echo, _timestamp, _separate, sep(), maxLinesLabel, _maxLines, sep(), zoomLabel, _zoomOut, _zoomReset, _zoomIn, sep(), _closeAllBtn });

            var tips = new DarkToolTip();
            tips.SetToolTip(_echo, "Gönderilen komutları kart ekranında göster");
            tips.SetToolTip(_lineEnd,
                "Enter'a basınca komutun sonuna eklenip karta gönderilen görünmez karakter.\n\n" +
                "CR+LF (varsayılan): Satır başı + Yeni satır (\\r\\n) — telnet standardı, çoğu cihaz.\n" +
                "CR: Yalnız Satır başı (\\r) — her Enter'da iki istem geliyorsa seçin.\n" +
                "LF: Yalnız Yeni satır (\\n) — Linux/Unix tarzı cihazlar.\n" +
                "Yok: Hiçbir şey eklenmez — metin olduğu gibi gider.");
            tips.SetToolTip(_timestamp, "Gelen her satırın başına saat ekle");
            tips.SetToolTip(_separate, "Açık: komutların dönüş değerleri (\"value = …\", \"task spawned …\") canlı loglarla karışmaz;\n" +
                                       "her kartın altındaki \"Dönüş\" panelinde komutla birlikte gösterilir.\nKapalı: eskisi gibi terminalde görünür.");
            _zoomOut.SetTip(tips, "Tüm kart terminallerinde yazıyı küçült  (Ctrl + −  ya da  Ctrl + tekerlek)");
            _zoomIn.SetTip(tips, "Tüm kart terminallerinde yazıyı büyüt  (Ctrl + +  ya da  Ctrl + tekerlek)");
            _zoomReset.SetTip(tips, "Ortak yakınlaştırma — tıklayınca varsayılana (%100) döner  (Ctrl + 0)");
            string maxTip = "Her kartın terminalinde tutulacak en fazla satır (varsayılan " + AppSettings.DefaultMaxTerminalLines + ").\n" +
                            "Sınır aşılınca en eski satırlar silinir, yeni çıktı gelmeye devam eder.\n" +
                            "Tüm kartlara hemen uygulanır.  (" + AppSettings.MinTerminalLines + " – " + AppSettings.MaxTerminalLinesLimit.ToString("N0") + ")";
            tips.SetToolTip(_maxLines, maxTip);
            tips.SetToolTip(maxLinesLabel, maxTip);
            tips.SetToolTip(_closeAllBtn, "Tüm kart görünümlerini ekrandan kaldır (bağlantılar sürer)");

            bar.Controls.Add(_workspaceTitle);
            bar.Controls.Add(_workspaceSub);
            bar.Controls.Add(options);
            bar.Resize += (s, e) =>
            {
                options.Location = new Point(Math.Max(8, bar.Width - options.Width - 14), 12);
                // Dar pencerede seçenekler başlığın üstüne binmesin: yer yoksa başlık gizlenir
                bool room = options.Left > _workspaceTitle.Left + Math.Max(_workspaceTitle.Width, _workspaceSub.Width) + 16;
                _workspaceTitle.Visible = _workspaceSub.Visible = room;
            };
            bar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1); };

            // Durum çubuğu
            var status = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Theme.Sidebar };
            _statusLeft.Location = new Point(12, 6);
            _statusRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            status.Controls.Add(_statusLeft);
            status.Controls.Add(_statusRight);
            status.Controls.Add(_signature);
            // İmza en sağda; kısayol ipuçları onun solunda (dar pencerede ipuçları gizlenir, imza kalır)
            status.Resize += (s, e) =>
            {
                _signature.Location = new Point(status.Width - _signature.Width - 14, 6);
                _statusRight.Location = new Point(_signature.Left - _statusRight.Width - 24, 6);
                _statusRight.Visible = _statusRight.Left > _statusLeft.Right + 24;
            };
            status.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(p, 0, 0, status.Width, 0);
                    if (_statusRight.Visible) e.Graphics.DrawLine(p, _signature.Left - 12, 7, _signature.Left - 12, status.Height - 7);
                }
            };
            _statusRight.Text = "Ctrl+N: kart ekle   ·   Ctrl+G: genel komut   ·   Ctrl+1 / 2 / 3 / 4: sekmeler   ·   F1: yardım";
            _signature.Cursor = Cursors.Hand;
            _signature.Click += (s, e) => ShowHelp(HelpPage.About);
            _signature.MouseEnter += (s, e) => _signature.ForeColor = Theme.Accent;
            _signature.MouseLeave += (s, e) => _signature.ForeColor = Theme.TextMuted;
            new DarkToolTip().SetToolTip(_signature, AppInfo.Copyright);

            // Sekme şeridi: [◀ kenar çubuğu] Telnet | Kart Dosya Gezgini | Komut Kütüphanesi
            var tabStrip = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Sidebar, Padding = new Padding(6, 0, 8, 0) };
            _workspaceTab.Dock = DockStyle.Left;
            _filesTab.Dock = DockStyle.Left;
            _commandsTab.Dock = DockStyle.Left;
            _sentinelTab.Dock = DockStyle.Left;
            _sidebarToggle.Dock = DockStyle.Left;
            _sidebarToggle.Width = 42;
            tabStrip.Controls.Add(_sentinelTab);     // Dock=Left: son eklenen en solda
            tabStrip.Controls.Add(_commandsTab);
            tabStrip.Controls.Add(_filesTab);
            tabStrip.Controls.Add(_workspaceTab);
            tabStrip.Controls.Add(_sidebarToggle);
            BuildHeaderButtons(tabStrip);       // sağda: Yardım + tema (eski üst menü çubuğu kaldırıldı)
            _sidebarToggle.Click += (s, e) => SetSidebarCollapsed(!_split.Panel1Collapsed);
            tabStrip.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, tabStrip.Height - 1, tabStrip.Width, tabStrip.Height - 1); };
            _workspaceTab.Click += (s, e) => ShowTab(MainTab.Workspace);
            _filesTab.Click += (s, e) => ShowTab(MainTab.Files);
            _commandsTab.Click += (s, e) => ShowTab(MainTab.Commands);
            _sentinelTab.Click += (s, e) => ShowTab(MainTab.Sentinel);
            _workspaceTab.Active = true;

            // Genel komut çubuğu YALNIZ çalışma alanına aittir (dosya gezgini sekmesinde görünmez/kullanılamaz)
            _workspaceHost.Controls.Add(_grid);
            _workspaceHost.Controls.Add(bar);
            _workspaceHost.Controls.Add(_globalBar);
            _split.Panel2.Controls.Add(_workspaceHost);
            _split.Panel2.Controls.Add(_fileExplorer);
            _split.Panel2.Controls.Add(_commandLibrary);
            _split.Panel2.Controls.Add(_sentinelView);
            _split.Panel2.Controls.Add(tabStrip);

            Controls.Add(_split);
            Controls.Add(status);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                _split.Panel1MinSize = 220;
                _split.Panel2MinSize = 400;
                _split.SplitterDistance = 290;
            }
            catch (InvalidOperationException) { }
            SetSidebarCollapsed(_config.Settings.SidebarCollapsed);   // son tercih
            UpdateWorkspaceTitle();
            UpdateStatus();
        }

        private void WireEvents()
        {
            _explorer.AddCardsRequested += ShowAddCardsDialog;
            _explorer.OpenRequested += OpenCards;
            _explorer.ShowExactlyRequested += ShowExactly;
            _explorer.BrowseFilesRequested += card => { _fileExplorer.ShowCard(card); ShowTab(MainTab.Files); };
            _explorer.RenameCardRequested += RenameCard;
            _explorer.RenameGroupRequested += RenameGroup;
            // Dosya Gezgini'nde gezilen kart, Kart Gezgini'nde vurgulanır (yalnız o sekmedeyken)
            _fileExplorer.SelectedCardChanged += card => { if (_activeTab == MainTab.Files) _explorer.HighlightedCard = card; };
            _explorer.ConnectRequested += ConnectCards;
            _explorer.RemoveGroupRequested += RemoveGroup;
            _explorer.RemoveCardRequested += RemoveCard;
            _explorer.ConfigChanged += SaveConfig;

            _grid.ViewsChanged += (s, e) => { UpdateWorkspaceTitle(); _globalBar.UpdateTargets(); _statesDirty = true; };
            _grid.ZoomWheel += step => ChangeZoom(step);
            _grid.CardConfigChanged += card => SaveConfig();
            _grid.SentinelRequested += card => { ShowTab(MainTab.Sentinel); _sentinelView.SelectCard(card); };
            _zoomOut.Click += (s, e) => ChangeZoom(-1);
            _zoomIn.Click += (s, e) => ChangeZoom(+1);
            _zoomReset.Click += (s, e) => SetZoom(AppSettings.DefaultTerminalZoom);
            _globalBar.SaveSnapshotsRequested += async () => await SaveAllSnapshotsAsync();
            _globalBar.ClearAllRequested += ClearAllTerminals;
            _closeAllBtn.Click += (s, e) => _grid.CloseAll();

            _lineEnd.SelectedIndexChanged += (s, e) => SaveSettingsFromUi();
            foreach (var cb in new[] { _echo, _timestamp, _separate })
                cb.CheckedChanged += (s, e) => SaveSettingsFromUi();
            _maxLines.ValueChanged += (s, e) => SaveSettingsFromUi();

            KeyDown += OnFormKeyDown;
        }

        /// <summary>Kutuda kısa ad; açılan listede açıklama + ne zaman seçileceği (iki renk).</summary>
        private void LineEnd_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            LineEndingOption o = LineEndingOptions[e.Index];
            bool inEditBox = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected = (e.State & DrawItemState.Selected) != 0 && !inEditBox;
            using (var bg = new SolidBrush(selected ? Theme.Selection : Theme.SurfaceHigh)) e.Graphics.FillRectangle(bg, e.Bounds);

            if (inEditBox)
            {
                TextRenderer.DrawText(e.Graphics, o.Short, Theme.Ui, e.Bounds, Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                return;
            }
            var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            // İpuçları aynı sütunda başlasın: en uzun açıklamanın genişliği kadar boşluk bırak
            int detailWidth = 0;
            foreach (LineEndingOption x in LineEndingOptions)
                detailWidth = Math.Max(detailWidth, TextRenderer.MeasureText(e.Graphics, x.Detail, Theme.UiBold, Size.Empty, TextFormatFlags.NoPrefix).Width);
            TextRenderer.DrawText(e.Graphics, o.Detail, e.Index == 0 ? Theme.UiBold : Theme.Ui, r, Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            var hintRect = new Rectangle(r.X + detailWidth + 14, r.Y, Math.Max(0, r.Right - (r.X + detailWidth + 14)), r.Height);
            TextRenderer.DrawText(e.Graphics, o.Hint, Theme.UiSmall, hintRect, selected ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                e.SuppressKeyPress = true;
                ShowHelp(HelpPage.Guide);
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.L)   // sonraki tema
            {
                e.SuppressKeyPress = true;
                ToggleTheme();
            }
            else if (e.Control && !e.Shift && e.KeyCode == Keys.N)   // Ctrl+Shift+N: gezgin / kütüphane "yeni"
            {
                e.SuppressKeyPress = true;
                ShowAddCardsDialog(null);
            }
            else if (e.Control && !e.Shift && e.KeyCode == Keys.B)
            {
                e.SuppressKeyPress = true;
                SetSidebarCollapsed(!_split.Panel1Collapsed);
            }
            else if (e.Control && !e.Shift && (e.KeyCode == Keys.D1 || e.KeyCode == Keys.D2 || e.KeyCode == Keys.D3 || e.KeyCode == Keys.D4))
            {
                e.SuppressKeyPress = true;
                ShowTab(e.KeyCode == Keys.D1 ? MainTab.Workspace : e.KeyCode == Keys.D2 ? MainTab.Files
                      : e.KeyCode == Keys.D3 ? MainTab.Commands : MainTab.Sentinel);
            }
            else if (e.Control && e.KeyCode == Keys.G)
            {
                e.SuppressKeyPress = true;
                ShowTab(MainTab.Workspace);  // genel komut yalnız çalışma alanında
                _globalBar.FocusInput();
            }
            else if (e.Control && e.KeyCode == Keys.W && _grid.SelectedView != null && _activeTab == MainTab.Workspace)
            {
                e.SuppressKeyPress = true;
                _grid.Close(_grid.SelectedView);
            }
            else if (e.Control && !e.Alt && _activeTab == MainTab.Workspace &&
                     (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add || e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract ||
                      e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
            {
                // Ortak terminal yakınlaştırması: Ctrl + "+" / "−" / "0"
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) SetZoom(AppSettings.DefaultTerminalZoom);
                else ChangeZoom(e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add ? +1 : -1);
            }
        }

        // ---------- Oturumlar ----------

        /// <summary>Kartın oturumu (yoksa null; oluşturmaz).</summary>
        private CardSession FindSession(CardConfig card)
        {
            CardSession s;
            return _sessions.TryGetValue(card, out s) ? s : null;
        }

        private CardSession GetSession(CardConfig card)
        {
            CardSession s;
            if (!_sessions.TryGetValue(card, out s))
            {
                s = new CardSession(card, _config.Settings);
                s.StateChanged += x => _statesDirty = true;
                _sessions.Add(card, s);
                _sessionSnapshot = _sessions.Values.ToArray();
            }
            return s;
        }

        private CardState StateOf(CardConfig card)
        {
            CardSession s;
            return _sessions.TryGetValue(card, out s) ? s.State : CardState.Disconnected;
        }

        private bool IsOpen(CardConfig card)
        {
            CardSession s;
            return _sessions.TryGetValue(card, out s) && _grid.FindView(s) != null;
        }

        /// <summary>
        /// Kartları Telnet sekmesine ekler (çift tık / sağ tık menüsü). Bağlantıya dokunmaz.
        /// En fazla 12 kart: boş yer kadar açılır, açılamayanlar bildirilir; açık kartlar kapatılmaz.
        /// </summary>
        private void OpenCards(List<CardConfig> cards)
        {
            ShowTab(MainTab.Workspace);
            List<CardSession> shown = _grid.Views.Select(v => v.Session).ToList();
            List<CardSession> rejected;
            List<CardSession> toOpen = SelectionLogic.LimitAdditive(shown, cards.Select(GetSession).ToList(), CardGrid.MaxViews, out rejected);

            CardView last = null;
            foreach (CardSession s in shown.Where(x => cards.Contains(x.Card))) last = _grid.Open(s);   // zaten açık: öne getir
            foreach (CardSession s in toOpen) last = _grid.Open(s) ?? last;
            if (last != null && cards.Count == 1) last.FocusInput();
            _statesDirty = true;
            if (rejected.Count > 0) WarnCardLimit(toOpen.Count, rejected);
        }

        /// <summary>
        /// Komut Kütüphanesi'nden "Karta aktar": kartı Telnet sekmesinde açar (açıksa öne getirir) ve komutu komut satırına
        /// yazar (göndermez). Kart bağlı değilse kart penceresi bağlanır ve bağlanınca yazar.
        /// </summary>
        private void TransferCommandToCard(CardConfig card, string command)
        {
            ShowTab(MainTab.Workspace);
            if (_activeTab != MainTab.Workspace) return;   // kütüphanede kaydedilmemiş değişiklik: kullanıcı vazgeçti
            OpenCards(new List<CardConfig> { card });
            CardView view = _grid.FindView(GetSession(card));
            if (view != null) view.InsertCommand(command);
        }

        /// <summary>Komut Kütüphanesi'nden "ekrandaki tüm kartlar": komut genel komut çubuğuna yazılır (gönderilmez).</summary>
        private void TransferCommandToAll(string command)
        {
            ShowTab(MainTab.Workspace);
            if (_activeTab != MainTab.Workspace) return;
            _globalBar.InsertCommand(command);
        }

        /// <summary>"Seçilenleri Göster": ekranda tam olarak bu kartlar kalır (en fazla 12). Bağlantıya dokunmaz.</summary>
        private void ShowExactly(List<CardConfig> cards)
        {
            ShowTab(MainTab.Workspace);
            List<CardSession> rejected = _grid.ShowExactly(cards.Select(GetSession).ToList());
            _statesDirty = true;
            if (rejected.Count > 0) WarnCardLimit(cards.Count - rejected.Count, rejected);
        }

        private void WarnCardLimit(int openedCount, List<CardSession> rejected)
        {
            string list = string.Join(", ", rejected.Take(8).Select(s => s.Card.Label)) + (rejected.Count > 8 ? " …" : "");
            string head = openedCount > 0
                ? "En fazla " + CardGrid.MaxViews + " kart aynı anda gösterilebilir. " + rejected.Count + " kart açılamadı."
                : "Ekranda zaten " + CardGrid.MaxViews + " kart var; " + rejected.Count + " kart açılamadı.";
            ChoiceDialog.Ask(this, "Kart sınırı", head,
                "Açılamayanlar: " + list + "\n\nYer açmak için ekrandaki bir kartı kapatın (✕). Ekrandaki kartlar ve bağlantılar değiştirilmedi.",
                new[] { "Tamam" }, 0, -1);
        }

        private void ConnectCards(List<CardConfig> cards, bool connect)
        {
            foreach (CardConfig card in cards)
            {
                CardSession s = GetSession(card);
                if (connect) { var ignored = s.ConnectAsync(); }
                else s.Disconnect();
            }
            _statesDirty = true;
        }

        private void DropSession(CardConfig card)
        {
            CardSession s;
            if (!_sessions.TryGetValue(card, out s)) return;
            _grid.CloseSession(s);
            _sessions.Remove(card);
            _sessionSnapshot = _sessions.Values.ToArray();   // silinen kart artık izlenmez
            s.Dispose();
        }

        // ---------- Grup / kart yönetimi ----------

        private void ShowAddCardsDialog(CardGroup target)
        {
            var names = _config.Groups.Select(g => g.Name).ToList();
            using (var dlg = new AddCardsDialog(names, target))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                CardGroup group = target;
                if (group == null)
                {
                    group = new CardGroup { Name = dlg.ResultGroupName, Port = dlg.ResultPort };
                    _config.Groups.Add(group);
                }
                foreach (string ip in dlg.ResultIps)
                    group.Cards.Add(new CardConfig { Ip = ip, Group = group });
                group.Expanded = true;
            }
            SaveConfig();
            _explorer.Rebuild();
            _fileExplorer.RefreshCards();
            UpdateStatus();
        }

        private void RemoveGroup(CardGroup group)
        {
            string msg = "\"" + group.Name + "\" grubu ve içindeki " + group.Cards.Count +
                         " kart silinsin mi?\n\nAçık bağlantılar kapatılır.";
            if (!Confirm(msg, "Grubu sil")) return;

            foreach (CardConfig c in group.Cards.ToList()) DropSession(c);
            _config.Groups.Remove(group);
            SaveConfig();
            _explorer.Rebuild();
            _fileExplorer.RefreshCards();
            UpdateStatus();
        }

        private void RemoveCard(CardConfig card)
        {
            if (!Confirm(card.Ip + " kartı \"" + card.Group.Name + "\" grubundan kaldırılsın mı?", "Kartı kaldır")) return;
            DropSession(card);
            card.Group.Cards.Remove(card);
            SaveConfig();
            _explorer.Rebuild();
            _fileExplorer.RefreshCards();
            UpdateStatus();
        }

        // ---------- Ortak terminal yakınlaştırması ----------

        /// <summary>Bir adım büyütür (+1) / küçültür (-1). Tüm kartlar aynı orana geçer.</summary>
        private void ChangeZoom(int step)
        {
            SetZoom(AppSettings.StepZoom(_config.Settings.TerminalZoom, step));
        }

        private void SetZoom(float zoom)
        {
            zoom = AppSettings.ClampZoom(zoom);
            bool changed = Math.Abs(zoom - _config.Settings.TerminalZoom) > 0.001f;
            _config.Settings.TerminalZoom = zoom;
            _grid.ApplyZoom(zoom);                 // merkezi değer: açık kartların hepsi + sonradan açılanlar
            UpdateZoomUi();
            if (changed) SaveConfig();
        }

        private void UpdateZoomUi()
        {
            float z = _config.Settings.TerminalZoom;
            _zoomReset.Label = "%" + (int)Math.Round(z * 100);
            _zoomOut.Enabled = z > AppSettings.MinTerminalZoom + 0.001f;
            _zoomIn.Enabled = z < AppSettings.MaxTerminalZoom - 0.001f;
        }

        // ---------- Genel temizleme ----------

        /// <summary>Ekrandaki tüm kartların terminal ekranını temizler (her kartın silgi düğmesiyle aynı iş).</summary>
        private void ClearAllTerminals()
        {
            List<CardView> views = _grid.Views.ToList();
            if (views.Count == 0) { _globalBar.ShowResult("Ekranda kart yok — temizlenecek terminal yok.", Theme.Warning, null); return; }
            foreach (CardView v in views) v.ClearTerminal();
            _globalBar.ShowResult("✓  " + views.Count + " kartın ekranı temizlendi", Theme.Success, null);
        }

        // ---------- Genel anlık kayıt ----------

        /// <summary>
        /// Genel komut çubuğunun hedefi olan (ekrandaki) kartların terminal içeriğini AYNI ANDA alır ve her kartı
        /// kendi dosyasına kaydeder. Ekranda olmayan kartlar kaydedilmez; bir kartın hatası diğerlerini durdurmaz.
        /// </summary>
        private async Task SaveAllSnapshotsAsync()
        {
            List<CardView> views = _grid.Views.ToList();
            if (views.Count == 0) { _globalBar.ShowResult("Ekranda kart yok — kaydedilecek terminal yok.", Theme.Warning, null); return; }
            DateTime at = DateTime.Now;
            var captured = views.Select(v => new { Card = v.Session.Card, Text = v.CaptureTerminalText() }).ToList();   // tıklama anı
            SnapshotResult[] results = await Task.WhenAll(captured.Select(c => TerminalSnapshot.SaveAsync(c.Card, c.Text, at)));
            int ok = results.Count(r => r.Ok);
            var failed = results.Where(r => !r.Ok).Select(r => r.Card.Label + " (" + r.Error + ")").ToList();
            string folder = TerminalSnapshot.DefaultFolder;
            string detail = string.Join("\n", results.Select(r => (r.Ok ? "✓ " + r.Card.Label + " → " + Path.GetFileName(r.Path) : "✕ " + r.Card.Label + ": " + r.Error)));
            if (failed.Count == 0)
                _globalBar.ShowResult("✓  " + ok + " kartın terminali ayrı dosyalara kaydedildi (logs)", Theme.Success, detail + "\n\nKlasör: " + folder);
            else if (ok == 0)
                _globalBar.ShowResult("✕  Hiçbir kart kaydedilemedi: " + string.Join(", ", failed), Theme.Danger, detail);
            else
                _globalBar.ShowResult("⚠  " + ok + "/" + results.Length + " kart kaydedildi · kaydedilemedi: " + string.Join(", ", failed), Theme.Warning, detail);
            foreach (CardView v in views)
            {
                SnapshotResult r = results.FirstOrDefault(x => x.Card == v.Session.Card);
                if (r != null) v.ShowToast(r.Ok ? "✓  Kaydedildi: " + Path.GetFileName(r.Path) + "  (klasörü açmak için tıklayın)" : "✕  Kaydedilemedi: " + r.Error, r.Ok, r.Path);
            }
        }

        private bool Confirm(string message, string title)
        {
            return ChoiceDialog.Ask(this, title, message, null, new[] { "Vazgeç", "Evet" }, 0, 1) == 1;
        }

        // ---------- Ayarlar ----------

        private bool _loadingSettings;

        private void LoadSettingsToUi()
        {
            _loadingSettings = true;
            AppSettings s = _config.Settings;
            switch (s.LineEnding)
            {
                case "CR": _lineEnd.SelectedIndex = 1; break;
                case "LF": _lineEnd.SelectedIndex = 2; break;
                case "NONE": _lineEnd.SelectedIndex = 3; break;
                default: _lineEnd.SelectedIndex = 0; break;
            }
            _echo.Checked = s.LocalEcho;
            _timestamp.Checked = s.Timestamp;
            _separate.Checked = s.SeparateReturnValues;
            _maxLines.Value = Math.Max(_maxLines.Minimum, Math.Min(_maxLines.Maximum, s.MaxTerminalLines));
            _loadingSettings = false;
        }

        private void SaveSettingsFromUi()
        {
            if (_loadingSettings) return;
            AppSettings s = _config.Settings;
            switch (_lineEnd.SelectedIndex)
            {
                case 1: s.LineEnding = "CR"; break;
                case 2: s.LineEnding = "LF"; break;
                case 3: s.LineEnding = "NONE"; break;
                default: s.LineEnding = "CRLF"; break;
            }
            s.LocalEcho = _echo.Checked;
            s.Timestamp = _timestamp.Checked;
            bool separateChanged = s.SeparateReturnValues != _separate.Checked;
            s.SeparateReturnValues = _separate.Checked;
            if (separateChanged)
                foreach (CardSession cs in _sessions.Values) cs.SetResponseSeparation(s.SeparateReturnValues);
            int maxLines = (int)_maxLines.Value;
            if (s.MaxTerminalLines != maxLines)
            {
                s.MaxTerminalLines = maxLines;
                // Görünümü kapalı kartların geçmişi de hemen kırpılır; açık kart pencereleri bir sonraki adımda uygular
                foreach (CardSession cs in _sessions.Values) cs.ApplyLineLimit();
            }
            SaveConfig();
        }

        private void SaveConfig()
        {
            try { ConfigStore.Save(_config, _configPath); }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("yapılandırma kaydı", ex);
                _statusLeft.Text = "Yapılandırma kaydedilemedi (dosyaya yazılamadı)";
                _statusLeft.ForeColor = Theme.Danger;
            }
        }

        // ---------- Arayüz güncelleme ----------

        private void ShowHelp(HelpPage page)
        {
            using (var dlg = new HelpDialog(page)) dlg.ShowDialog(this);
        }

        /// <summary>Sekmeler arası geçiş (bağlantılar ve kart görünümleri etkilenmez).</summary>
        private void ShowTab(MainTab tab)
        {
            if (tab == _activeTab) return;
            // Komut kütüphanesinde kaydedilmemiş değişiklik varsa önce sor
            if (_activeTab == MainTab.Commands && !_commandLibrary.ConfirmLeave()) return;
            _activeTab = tab;
            _workspaceTab.Active = tab == MainTab.Workspace;
            _filesTab.Active = tab == MainTab.Files;
            _commandsTab.Active = tab == MainTab.Commands;
            _sentinelTab.Active = tab == MainTab.Sentinel;
            _workspaceHost.Visible = tab == MainTab.Workspace;
            _fileExplorer.Visible = tab == MainTab.Files;
            _commandLibrary.Visible = tab == MainTab.Commands;
            _sentinelView.Visible = tab == MainTab.Sentinel;
            if (tab == MainTab.Files) _fileExplorer.Tick();
            _explorer.HighlightedCard = tab == MainTab.Files ? _fileExplorer.SelectedCard : null;
        }

        /// <summary>
        /// Kart Gezgini kenar çubuğunu daraltır / açar. Ağaç, seçim, işaretler ve sekmeler olduğu gibi kalır;
        /// sağdaki alan boşalan genişliği kullanır. Tercih kaydedilir.
        /// </summary>
        private void SetSidebarCollapsed(bool collapsed)
        {
            _split.Panel1Collapsed = collapsed;
            _sidebarToggle.Icon = collapsed ? IconKind.SidebarShow : IconKind.SidebarHide;
            _sidebarToggle.SetTip(_sidebarTips, (collapsed ? "Kart Gezgini'ni göster" : "Kart Gezgini'ni gizle") + "  (Ctrl+B)");
            if (_config.Settings.SidebarCollapsed != collapsed)
            {
                _config.Settings.SidebarCollapsed = collapsed;
                SaveConfig();
            }
        }

        private readonly ToolTip _sidebarTips = new DarkToolTip();

        /// <summary>Kartın görünen adını verir / değiştirir / kaldırır (bağlantı her zaman IP ile yapılır).</summary>
        private void RenameCard(CardConfig card)
        {
            string name = InputDialog.Ask(this, "Kart adı",
                card.Ip + " için görünen ad (ör. Haberlesme). Boş bırakılırsa ad kaldırılır; kart yalnız IP ile görünür.",
                card.Name ?? "", CardConfig.ValidateName);
            if (name == null) return;   // iptal
            string trimmed = name.Trim();
            card.Name = trimmed.Length == 0 ? null : trimmed;
            SaveConfig();
            _explorer.Rebuild();
            _grid.RefreshTitles();
            _fileExplorer.RefreshCards();
            _statesDirty = true;
        }

        /// <summary>
        /// Grubun adını değiştirir (sağ tık "Grubun adını değiştir…" ya da F2). Kartlar, bağlantılar ve port olduğu gibi
        /// kalır; ad her yerde (kart başlıkları, Dosya Gezgini, Sentinel, Komut Kütüphanesi) hemen güncellenir.
        /// </summary>
        private void RenameGroup(CardGroup group)
        {
            string name = InputDialog.Ask(this, "Grup adı", "\"" + group.Name + "\" grubunun yeni adı:", group.Name ?? "", text =>
            {
                string t = (text ?? "").Trim();
                if (t.Length == 0) return "Grup adı boş olamaz.";
                if (_config.Groups.Any(g => g != group && string.Equals(g.Name, t, StringComparison.OrdinalIgnoreCase)))
                    return "\"" + t + "\" adında bir grup zaten var.";
                return null;
            });
            if (name == null) return;   // iptal
            string trimmed = name.Trim();
            if (trimmed == group.Name) return;
            group.Name = trimmed;
            SaveConfig();
            _explorer.Rebuild();
            _grid.RefreshTitles();
            _fileExplorer.RefreshCards();
            _statesDirty = true;
        }

        // Sekme şeridinin sağı: [Yardım] [Temalar] — ayrı menü çubuğu yok
        private readonly TabStripButton _helpBtn = new TabStripButton("Yardım", IconKind.Info) { IsTab = false };
        private readonly TabStripButton _themeBtn = new TabStripButton("Temalar", IconKind.Palette) { IsTab = false };
        private readonly ContextMenuStrip _helpMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip _themeMenu = new ContextMenuStrip();
        private readonly List<Image> _themeMenuImages = new List<Image>();
        private readonly DarkToolTip _headerTips = new DarkToolTip();

        /// <summary>Yardım ve tema düğmelerini sekme şeridinin sağına yerleştirir (F1 ve Ctrl+Shift+L formda işlenir).</summary>
        private void BuildHeaderButtons(Panel tabStrip)
        {
            _helpMenu.Renderer = new DarkMenuRenderer();
            _helpMenu.ShowImageMargin = false;
            Action<string, string, Action> item = (text, keys, action) =>
            {
                var it = new ToolStripMenuItem(text) { ForeColor = Theme.Text, ShortcutKeyDisplayString = keys };
                it.Click += (s, e) => action();
                _helpMenu.Items.Add(it);
            };
            item("Kullanım Kılavuzu", "F1", () => ShowHelp(HelpPage.Guide));
            item("Gereksinimler", null, () => ShowHelp(HelpPage.Requirements));
            _helpMenu.Items.Add(new ToolStripSeparator());
            item("Yardım klasörünü aç", null, () => HelpDialog.OpenHelpFolder(this));
            _helpMenu.Items.Add(new ToolStripSeparator());
            item("Hakkında", null, () => ShowHelp(HelpPage.About));

            _helpBtn.Dock = DockStyle.Right;
            _themeBtn.Dock = DockStyle.Right;
            _helpBtn.Click += (s, e) => _helpMenu.Show(_helpBtn, new Point(0, _helpBtn.Height));
            _themeMenu.Renderer = new DarkMenuRenderer();
            _themeBtn.Click += (s, e) =>
            {
                BuildThemeMenu();
                _themeMenu.Show(_themeBtn, new Point(0, _themeBtn.Height));
            };
            _headerTips.SetToolTip(_helpBtn, "Kullanım kılavuzu, gereksinimler, hakkında  (F1: kılavuz)");
            tabStrip.Controls.Add(_helpBtn);    // Dock=Right: son eklenen en sağda → [Yardım] [Temalar]
            tabStrip.Controls.Add(_themeBtn);
            RefreshThemeItem();
            Theme.Changed += RefreshThemeItem;   // düzenleyicideki anlık önizlemede de simge rengi güncel kalsın
        }

        /// <summary>Temalar düğmesi: simge rengi geçerli temanın vurgusu.</summary>
        private void RefreshThemeItem()
        {
            ThemeDefinition def = ThemeCatalog.FindOrDefault(Theme.CurrentThemeId);
            _themeBtn.IconColor = Theme.Accent;
            _themeBtn.FitToText();
            string tip = "Tema: " + def.Name + "  ·  hazır temalar ve özelleştirme  (Ctrl+Shift+L: sonraki tema)";
            _themeBtn.AccessibleName = tip;
            _headerTips.SetToolTip(_themeBtn, tip);
        }

        /// <summary>Temalar menüsü: hazır temalar (seçili olan işaretli, renk örneğiyle) ve "Temayı özelleştir…".</summary>
        private void BuildThemeMenu()
        {
            _themeMenu.Items.Clear();
            foreach (Image img in _themeMenuImages) img.Dispose();
            _themeMenuImages.Clear();
            List<ThemeCustomization> custom = _config.Settings.ThemeCustomizations;
            foreach (ThemeDefinition def in ThemeCatalog.All)
            {
                bool customized = ThemeCatalog.IsCustomized(custom, def.Id);
                var it = new ToolStripMenuItem(def.Name + (customized ? "   (özelleştirildi)" : ""))
                {
                    ForeColor = Theme.Text,
                    Checked = string.Equals(def.Id, Theme.CurrentThemeId, StringComparison.OrdinalIgnoreCase),
                    Image = ThemeSwatch(ThemeCatalog.EffectiveColors(def, custom)),
                    ToolTipText = def.Description,
                };
                string id = def.Id;
                it.Click += (s, e) => SelectTheme(id);
                _themeMenu.Items.Add(it);
            }
            _themeMenu.Items.Add(new ToolStripSeparator());
            var edit = new ToolStripMenuItem("Temayı özelleştir…") { ForeColor = Theme.Text };
            edit.Click += (s, e) =>
            {
                ThemeEditorDialog.Edit(this, _config.Settings, SaveConfig);
                RefreshThemeItem();
            };
            _themeMenu.Items.Add(edit);
        }

        /// <summary>Menüde tema örneği: arka plan, panel, vurgu ve terminal yazısı şeritleri.</summary>
        private Image ThemeSwatch(Color[] c)
        {
            var bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(c[(int)ThemeColorKey.Window])) g.FillRectangle(b, 1, 1, 14, 14);
                using (var b = new SolidBrush(c[(int)ThemeColorKey.Surface])) g.FillRectangle(b, 1, 1, 14, 5);
                using (var b = new SolidBrush(c[(int)ThemeColorKey.Accent])) g.FillRectangle(b, 3, 8, 5, 5);
                using (var b = new SolidBrush(c[(int)ThemeColorKey.Terminal])) g.FillRectangle(b, 9, 9, 5, 2);
                using (var p = new Pen(c[(int)ThemeColorKey.Border])) g.DrawRectangle(p, 1, 1, 13, 13);
            }
            _themeMenuImages.Add(bmp);
            return bmp;
        }

        /// <summary>Hazır temaya (kaydedilmiş değişiklikleriyle) geçer ve seçimi kaydeder; bağlantılar ve terminaller etkilenmez.</summary>
        private void SelectTheme(string id)
        {
            ThemeDefinition def = ThemeCatalog.FindOrDefault(id);
            Theme.Apply(def.Id, ThemeCatalog.EffectiveColors(def, _config.Settings.ThemeCustomizations));
            _config.Settings.UiThemeId = def.Id;
            _config.Settings.UiTheme = Theme.Mode;   // eski sürümlerle uyum
            RefreshThemeItem();
            SaveConfig();
        }

        /// <summary>Ctrl+Shift+L: listedeki sonraki hazır temaya geçer.</summary>
        private void ToggleTheme()
        {
            IList<ThemeDefinition> all = ThemeCatalog.All;
            int i = 0;
            for (int k = 0; k < all.Count; k++)
                if (string.Equals(all[k].Id, Theme.CurrentThemeId, StringComparison.OrdinalIgnoreCase)) i = k;
            SelectTheme(all[(i + 1) % all.Count].Id);
        }

        private void OnUiTick()
        {
            // Sentinel: bağlı kartlarda port 8080 denemesi (zamanı gelince arka planda)
            DateTime now = DateTime.UtcNow;
            foreach (CardSession s in _sessions.Values) s.Sentinel.Tick(now, s.State == CardState.Connected, s.Card.Ip);
            _grid.Tick();
            _fileExplorer.Tick();
            _sentinelView.Tick();   // sekme görünmese de: açık sayfa arka planda sürer, sunucu durursa kapanır
            if (_statesDirty)
            {
                _statesDirty = false;
                _explorer.RefreshStates();
                _globalBar.UpdateTargets();
                UpdateStatus();
            }
        }

        private void UpdateWorkspaceTitle()
        {
            int n = _grid.Views.Count;
            _workspaceSub.Text = n == 0 ? "Açık kart yok" : n + " kart açık";
        }

        private void UpdateStatus()
        {
            int total = _config.Groups.Sum(g => g.Cards.Count);
            int connected = _sessions.Values.Count(s => s.State == CardState.Connected);
            int lost = _sessions.Values.Count(s => s.State == CardState.Recovering);
            int errors = _sessions.Values.Count(s => s.State == CardState.Error);
            string text = _config.Groups.Count + " grup  ·  " + total + " kart  ·  " + connected + " bağlı";
            if (lost > 0) text += "  ·  " + lost + " bağlantısı kesik (izleniyor)";
            if (errors > 0) text += "  ·  " + errors + " hatalı";
            if (_configNote != null) text += "  ·  " + _configNote;
            errors += lost;
            bool warn = errors > 0 || _configNote != null;
            _statusLeft.Text = (warn ? "⚠  " : "") + text;
            _statusLeft.ForeColor = warn ? Theme.Warning : Theme.TextMuted;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Komut kütüphanesinde kaydedilmemiş değişiklik varsa sor; vazgeçilirse kapanma
            if (!_commandLibrary.ConfirmLeave())
            {
                e.Cancel = true;
                ShowTab(MainTab.Commands);
                return;
            }
            _uiTimer.Stop();
            _monitor.Dispose();                     // izleme zamanlayıcısı durur
            _sessionSnapshot = new CardSession[0];
            SaveConfig();
            _grid.CloseAll();
            foreach (var s in _sessions.Values) s.Dispose();
            _sessions.Clear();
            base.OnFormClosing(e);
        }
    }

    /// <summary>Sekme şeridindeki düz sekme düğmesi (etkin sekmenin altında vurgu çizgisi).</summary>
    internal sealed class TabStripButton : Control
    {
        private bool _active, _hover;
        private IconKind _icon;

        public TabStripButton(string text, IconKind icon)
        {
            Text = text;
            _icon = icon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = new Font("Segoe UI Semibold", 9.5f);
            BackColor = Theme.Sidebar;
            Cursor = Cursors.Hand;
            Width = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width + 16 + 8 + 32;
        }

        public bool Active
        {
            get { return _active; }
            set { _active = value; Invalidate(); }
        }

        /// <summary>false: sekme değil, şeritteki bir düğme (Yardım, tema) — etkin çizgisi yok.</summary>
        public bool IsTab { get; set; } = true;

        public IconKind Icon
        {
            get { return _icon; }
            set { _icon = value; Invalidate(); }
        }

        /// <summary>Simge rengi (boşsa yazı rengi).</summary>
        public Color IconColor { get; set; }

        /// <summary>Genişliği yazıya göre yeniden hesaplar (metin değişince).</summary>
        public void FitToText()
        {
            Width = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width + 16 + 8 + 32;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var bg = new SolidBrush(Theme.Sidebar)) g.FillRectangle(bg, ClientRectangle);
            if (_hover && !_active)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = IconRenderer.RoundRect(new RectangleF(4.5f, 6.5f, Width - 9, Height - 14), 5))
                using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, path);
            }
            Color fore = _active || _hover ? Theme.Text : Theme.TextMuted;
            Size ts = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int x = (Width - (16 + 8 + ts.Width)) / 2;
            Color iconColor = _active ? Theme.Accent : !IconColor.IsEmpty ? IconColor : fore;
            IconRenderer.Draw(g, _icon, new RectangleF(x, (Height - 16) / 2f - 1, 16, 16), iconColor);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + 24, 0, ts.Width + 4, Height - 2), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (_active && IsTab)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = IconRenderer.RoundRect(new RectangleF(12, Height - 3, Width - 24, 4), 1.5f))
                using (var line = new SolidBrush(Theme.Accent)) g.FillPath(line, path);
            }
        }
    }
}
