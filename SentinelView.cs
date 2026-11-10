using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Sentinel sekmesi (G28): kartın içindeki Sentinel web sunucusunun sayfası (http://kartIP:8080) uygulamanın İÇİNDE
    /// gösterilir; ayrı bir tarayıcı açılmaz. Solda bağlı kartlar Sentinel durumlarıyla, sağda seçilen kartın sayfası.
    /// Sayfa kendini yeniler; Vision yalnız gösterir. Sunucu çalışmıyorsa sayfanın yerine açıklama ve Başlat düğmesi çıkar.
    /// Sekme görünmüyorken sayfa kapatılır (kart boşuna sorgulanmasın); sekmeye dönünce yeniden açılır.
    /// </summary>
    public sealed class SentinelView : UserControl
    {
        private readonly Func<IList<CardConfig>> _cards;
        private readonly Func<CardConfig, CardSession> _sessionOf;   // oturumu olmayan kart için null

        private readonly Panel _top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Surface };
        private readonly Panel _left = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = Theme.Sidebar };
        private readonly CardList _list = new CardList();
        private readonly Label _listEmpty = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Panel _right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
        private readonly Panel _pageBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Window };
        private readonly Label _pageTitle = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _pageUrl = Theme.MakeLabel("", Theme.Mono, Theme.TextMuted);
        private readonly IconButton _refreshBtn = new IconButton(IconKind.Refresh, IconTone.Ghost);
        private readonly IconButton _toggleBtn = Theme.MakeButton("Başlat", IconKind.Pulse, Theme.ButtonKind.Secondary);
        private readonly Panel _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input };
        private readonly Label _overlay = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, BackColor = Theme.Input, Font = Theme.Ui };
        private readonly ToolTip _tips = new DarkToolTip();
        private readonly ComboBox _reloadCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 168 };
        private readonly Label _engineNote = Theme.MakeLabel("", Theme.UiSmall, Theme.Warning);
        private SentinelPage _page;
        private bool _reloadTouched;           // kullanıcı yenileme aralığını kendisi seçti
        private DateTime _nextReloadUtc;

        // Yenileme seçenekleri: 0 = sayfanın kendi yenilemesi
        private static readonly int[] ReloadSeconds = { 0, 2, 5, 10, 30 };
        private static readonly string[] ReloadNames = { "Sayfanın kendi yenilemesi", "Her 2 sn yenile", "Her 5 sn yenile", "Her 10 sn yenile", "Her 30 sn yenile" };

        private CardConfig _selected;
        private string _navigatedUrl;          // tarayıcıda açık olan Sentinel adresi (yoksa null)
        private DateTime _retryAtUtc;          // sayfa yüklenemediyse yeniden deneme zamanı
        private string _listSignature;
        private string _barSignature;

        public SentinelView(Func<IList<CardConfig>> cards, Func<CardConfig, CardSession> sessionOf)
        {
            _cards = cards;
            _sessionOf = sessionOf;
            BackColor = Theme.Window;
            BuildLayout();
        }

        // =====================================================================
        // Yerleşim
        // =====================================================================

        private void BuildLayout()
        {
            var title = Theme.MakeLabel("Sentinel", Theme.Title, Theme.Text);
            title.Location = new Point(18, 9);
            var hint = Theme.MakeLabel("Kartın sağlık sayfası (CPU, task, socket) — kartın içindeki web sunucusundan (port " + SentinelTracker.Port +
                                       "), uygulamanın içinde. Başlatmak için kart penceresindeki nabız düğmesini kullanın.",
                                       Theme.UiSmall, Theme.TextMuted);
            hint.Location = new Point(19, 34);
            _top.Controls.AddRange(new Control[] { title, hint });
            _top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, _top.Height - 1, _top.Width, _top.Height - 1); };

            // Sol: kartlar
            var caption = Theme.MakeLabel("BAĞLI KARTLAR", Theme.SectionHeader, Theme.TextMuted);
            caption.Dock = DockStyle.Top;
            caption.AutoSize = false;
            caption.Height = 34;
            caption.Padding = new Padding(14, 12, 0, 0);
            caption.BackColor = Theme.Sidebar;
            _list.Dock = DockStyle.Fill;
            _list.SelectedIndexChanged += (s, e) =>
            {
                var c = _list.SelectedIndex >= 0 ? _list.Items[_list.SelectedIndex] as CardConfig : null;
                if (c != null && c != _selected) { _selected = c; Tick(); }
            };
            _listEmpty.AutoSize = false;
            _listEmpty.Dock = DockStyle.Fill;
            _listEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _listEmpty.BackColor = Theme.Sidebar;
            _listEmpty.Padding = new Padding(12);
            _listEmpty.Text = "Bağlı kart yok.\n\nKart Gezgini'nden bir karta bağlanın.";
            _left.Controls.Add(_list);
            _left.Controls.Add(_listEmpty);
            _left.Controls.Add(caption);
            _left.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, _left.Width - 1, 0, _left.Width - 1, _left.Height); };
            Theme.ApplyDarkScrollBars(_list);

            // Sağ: sayfa çubuğu + sayfa
            _refreshBtn.Size = new Size(32, 32);
            _refreshBtn.SetTip(_tips, "Sayfayı yenile (F5)");
            _refreshBtn.Click += (s, e) => ReloadPage();
            _toggleBtn.Height = 32;
            _toggleBtn.Width = 110;
            _toggleBtn.Click += async (s, e) => await ToggleAsync();
            Theme.StyleCombo(_reloadCombo);
            _reloadCombo.Items.AddRange(ReloadNames);
            _reloadCombo.SelectedIndex = 0;
            _reloadCombo.SelectedIndexChanged += (s, e) => { _reloadTouched = true; _nextReloadUtc = DateTime.UtcNow.AddSeconds(ReloadSeconds[Math.Max(0, _reloadCombo.SelectedIndex)]); };
            _tips.SetToolTip(_reloadCombo, "Sayfa normalde kendini yeniler. Yenilenmiyorsa Vision'ın sayfayı belirli aralıklarla yeniden yüklemesini seçin.");
            _engineNote.Visible = false;
            _pageBar.Controls.AddRange(new Control[] { _pageTitle, _pageUrl, _engineNote, _reloadCombo, _refreshBtn, _toggleBtn });
            _pageBar.Resize += (s, e) => LayoutPageBar();
            _pageBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, _pageBar.Height - 1, _pageBar.Width, _pageBar.Height - 1); };
            _pageHost.Controls.Add(_overlay);
            _right.Controls.Add(_pageHost);
            _right.Controls.Add(_pageBar);

            Controls.Add(_right);
            Controls.Add(_left);
            Controls.Add(_top);
        }

        private void LayoutPageBar()
        {
            int w = _pageBar.ClientSize.Width;
            _toggleBtn.Location = new Point(w - 14 - _toggleBtn.Width, 8);
            _refreshBtn.Location = new Point(_toggleBtn.Left - 6 - _refreshBtn.Width, 8);
            _reloadCombo.Location = new Point(_refreshBtn.Left - 8 - _reloadCombo.Width, 8 + (32 - _reloadCombo.Height) / 2);
            _pageTitle.Location = new Point(16, 15);
            _pageTitle.Size = _pageTitle.PreferredSize;
            _pageUrl.Location = new Point(_pageTitle.Right + 12, 15);
            _pageUrl.Size = _pageUrl.PreferredSize;
            _engineNote.Size = _engineNote.PreferredSize;
            _engineNote.Location = new Point(_reloadCombo.Left - 12 - _engineNote.Width, 17);
            int urlLimit = _engineNote.Visible && _engineNote.Text.Length > 0 ? _engineNote.Left : _reloadCombo.Left;
            _pageUrl.Visible = _pageUrl.Right < urlLimit - 8;
            _reloadCombo.Visible = _reloadCombo.Left > _pageTitle.Right + 8;
        }

        // =====================================================================
        // Dışarıdan
        // =====================================================================

        /// <summary>Kartı seçer (kart penceresinden "Sentinel sekmesinde aç").</summary>
        public void SelectCard(CardConfig card)
        {
            _selected = card;
            _listSignature = null;
            Tick();
        }

        /// <summary>Ana pencerenin zamanlayıcısından çağrılır (sekme görünürken).</summary>
        public void Tick()
        {
            if (!Visible) { ClosePage(); return; }
            RefreshList();
            RefreshPage();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) ClosePage();
            else Tick();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { ReloadPage(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // =====================================================================
        // Kart listesi
        // =====================================================================

        private sealed class Row
        {
            public CardConfig Card;
            public SentinelStatus Status;
        }

        private List<Row> ConnectedRows()
        {
            var rows = new List<Row>();
            foreach (CardConfig c in _cards())
            {
                CardSession s = _sessionOf(c);
                if (s == null || s.State != CardState.Connected) continue;
                rows.Add(new Row { Card = c, Status = s.Sentinel.Status });
            }
            // Çalışanlar üstte, sonra gruptaki sıra
            return rows.OrderBy(r => r.Status == SentinelStatus.Running ? 0 : r.Status == SentinelStatus.Stopped ? 2 : 1).ToList();
        }

        private void RefreshList()
        {
            List<Row> rows = ConnectedRows();
            if (_selected != null && rows.All(r => r.Card != _selected)) _selected = null;
            if (_selected == null)
            {
                Row first = rows.FirstOrDefault(r => r.Status == SentinelStatus.Running) ?? rows.FirstOrDefault();
                _selected = first != null ? first.Card : null;
            }

            string sig = string.Join("|", rows.Select(r => r.Card.Ip + r.Card.Label + ":" + r.Status)) + "#" + (_selected != null ? _selected.Ip + _selected.Label : "");
            if (sig == _listSignature) return;
            _listSignature = sig;

            _list.StatusOf = c => { CardSession s = _sessionOf(c); return s != null ? s.Sentinel.Status : SentinelStatus.Stopped; };
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (Row r in rows) _list.Items.Add(r.Card);
                int i = _selected != null ? _list.Items.IndexOf(_selected) : -1;
                if (i >= 0) _list.SelectedIndex = i;
            }
            finally { _list.EndUpdate(); }
            _list.Invalidate();
            _list.Visible = rows.Count > 0;
            _listEmpty.Visible = rows.Count == 0;
        }

        // =====================================================================
        // Sayfa
        // =====================================================================

        private void RefreshPage()
        {
            CardSession s = _selected != null ? _sessionOf(_selected) : null;
            SentinelStatus st = s != null ? s.Sentinel.Status : SentinelStatus.Stopped;
            bool connected = s != null && s.State == CardState.Connected;
            string url = _selected != null ? SentinelTracker.UrlFor(_selected.Ip) : null;

            // Üst çubuk
            string barSig = (_selected != null ? _selected.Label + url : "") + st + connected;
            if (barSig != _barSignature)
            {
                _barSignature = barSig;
                _pageTitle.Text = _selected != null ? _selected.Label : "Kart seçilmedi";
                _pageUrl.Text = url ?? "";
                bool busy = st == SentinelStatus.Starting || st == SentinelStatus.Stopping;
                _toggleBtn.Label = st == SentinelStatus.Running ? "Durdur" : st == SentinelStatus.Starting ? "Başlatılıyor…"
                                 : st == SentinelStatus.Stopping ? "Durduruluyor…" : "Başlat";
                _toggleBtn.IconColor = st == SentinelStatus.Running ? Theme.Success : busy ? Theme.Warning : Color.Empty;
                _toggleBtn.Enabled = connected && !busy;
                _toggleBtn.SetTip(_tips, st == SentinelStatus.Running
                    ? "Sentinel'i durdur — karta " + SentinelTracker.StopCommand + " gönderilir"
                    : "Sentinel'i başlat — karta " + SentinelTracker.StartCommand + " gönderilir");
                _refreshBtn.Enabled = st == SentinelStatus.Running;
                _toggleBtn.Visible = _selected != null;
                _refreshBtn.Visible = _selected != null;
                LayoutPageBar();
            }

            // Sayfa / açıklama
            if (st == SentinelStatus.Running && connected)
            {
                EnsurePage();
                DateTime now = DateTime.UtcNow;
                if (_navigatedUrl != url && now >= _retryAtUtc) Navigate(url);
                else
                {
                    // İsteğe bağlı düzenli yenileme (sayfa kendini yenilemiyorsa)
                    int every = ReloadSeconds[Math.Max(0, _reloadCombo.SelectedIndex)];
                    if (every > 0 && _navigatedUrl != null && now >= _nextReloadUtc)
                    {
                        _page.Reload();
                        _nextReloadUtc = now.AddSeconds(every);
                    }
                }
                if (!_page.Control.Visible) _page.Control.Visible = true;
                if (_overlay.Visible) _overlay.Visible = false;
                return;
            }

            ClosePage();
            string text;
            if (_selected == null) text = "Bağlı kart yok.\n\nSentinel sayfası, bağlı ve Sentinel'i çalışan bir kart için gösterilir.";
            else if (st == SentinelStatus.Starting) text = "Sentinel başlatılıyor…\n\nKartın " + SentinelTracker.Port + " portu açılınca sayfa burada görünecek.";
            else if (st == SentinelStatus.Stopping) text = "Sentinel durduruluyor…";
            else
            {
                string msg = s != null ? s.Sentinel.Message : null;
                text = "Bu kartta Sentinel çalışmıyor.\n\nBaşlatmak için sağ üstteki \"Başlat\"ı ya da kart penceresindeki nabız düğmesini kullanın." +
                       (msg != null ? "\n\n⚠  " + msg : "");
            }
            if (_overlay.Text != text) _overlay.Text = text;
            if (!_overlay.Visible) { _overlay.Visible = true; _overlay.BringToFront(); }
            if (_page != null && _page.Control.Visible) _page.Control.Visible = false;
        }

        /// <summary>Görüntüleyiciyi ilk gerektiğinde oluşturur: önce Edge (WebView2), olmazsa eski görüntüleyici.</summary>
        private void EnsurePage()
        {
            if (_page != null) return;
            CreatePage(true);
        }

        private void CreatePage(bool modern)
        {
            SentinelPage page = modern ? (SentinelPage)new EdgeSentinelPage() : new LegacySentinelPage();
            page.LoadFailed = () =>
            {
                // Sunucu henüz cevap veremedi: 2 sn sonra tekrar dene
                _navigatedUrl = null;
                _retryAtUtc = DateTime.UtcNow.AddSeconds(2);
            };
            var edge = page as EdgeSentinelPage;
            if (edge != null)
                edge.InitFailed = ex =>
                {
                    if (IsHandleCreated) BeginInvoke(new Action(() => FallBack(ex)));
                    else FallBack(ex);
                };
            _page = page;
            page.Control.Visible = true;
            _pageHost.Controls.Add(page.Control);
            page.Control.BringToFront();
            page.Start();
        }

        /// <summary>Edge görüntüleyicisi başlatılamadı: eski görüntüleyiciye geçilir, sayfa düzenli yeniden yüklenir.</summary>
        private void FallBack(Exception ex)
        {
            if (_page == null || !_page.IsModern || IsDisposed) return;
            _pageHost.Controls.Remove(_page.Control);
            _page.Dispose();
            _page = null;
            _navigatedUrl = null;
            _retryAtUtc = DateTime.MinValue;
            CreatePage(false);
            if (!_reloadTouched) { _reloadCombo.SelectedIndex = 1; _reloadTouched = false; }
            _engineNote.Text = "⚠  Edge görüntüleyicisi yok — eski görüntüleyici";
            _engineNote.Visible = true;
            _tips.SetToolTip(_engineNote, "Bu bilgisayarda Microsoft Edge WebView2 Runtime bulunamadı (" + ErrorText.Describe(ex) + ").\n" +
                                          "Sayfa Windows'un eski görüntüleyicisiyle gösteriliyor; sayfadaki betikler çalışmayabilir, bu yüzden düzenli yeniden yüklenir.\n" +
                                          "Microsoft'un \"WebView2 Runtime\" kurulumu yapılırsa sayfa tam çalışır.");
            _barSignature = null;
            LayoutPageBar();
            RefreshPage();
        }

        private void Navigate(string url)
        {
            _navigatedUrl = url;
            int every = ReloadSeconds[Math.Max(0, _reloadCombo.SelectedIndex)];
            _nextReloadUtc = DateTime.UtcNow.AddSeconds(Math.Max(every, 1));
            _page.Navigate(url);
        }

        private void ReloadPage()
        {
            if (_page == null || _navigatedUrl == null) return;
            _page.Reload();
        }

        /// <summary>Sayfayı kapatır: görüntüleyici boş sayfaya geçer, kart artık sorgulanmaz.</summary>
        private void ClosePage()
        {
            if (_navigatedUrl == null || _page == null) return;
            _navigatedUrl = null;
            _page.Blank();
        }

        private async Task ToggleAsync()
        {
            CardSession s = _selected != null ? _sessionOf(_selected) : null;
            if (s == null || s.State != CardState.Connected) return;
            SentinelStatus st = s.Sentinel.Status;
            if (st == SentinelStatus.Starting || st == SentinelStatus.Stopping) return;
            bool stop = st == SentinelStatus.Running;
            if (stop) s.Sentinel.BeginStop(DateTime.UtcNow); else s.Sentinel.BeginStart(DateTime.UtcNow);
            _barSignature = null;
            RefreshPage();
            try
            {
                await s.SendCommandAsync(stop ? SentinelTracker.StopCommand : SentinelTracker.StartCommand, "sentinel");
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(s.Card.DisplayName + " sentinel", ex);
                _overlay.Text = "Komut gönderilemedi: " + ErrorText.Describe(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _page != null) _page.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Uygulama içindeki sayfa görüntüleyicisi (WebBrowser) varsayılan olarak çok eski (IE7) kipte çalışır;
        /// Vision.exe için IE11 kipi açılır (yalnız bu kullanıcı, yalnız bu exe; yönetici izni gerekmez).
        /// Program açılırken, hiçbir pencere oluşturulmadan çağrılır.
        /// </summary>
        public static void EnableModernPageMode()
        {
            try
            {
                string exe = System.IO.Path.GetFileName(Application.ExecutablePath);
                const string path = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path))
                {
                    if (key == null) return;
                    object current = key.GetValue(exe);
                    if (!(current is int) || (int)current != 11001)
                        key.SetValue(exe, 11001, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("Sentinel sayfa kipi", ex);
            }
        }

        // =====================================================================
        // Kart listesi çizimi
        // =====================================================================

        private sealed class CardList : ListBox
        {
            private readonly Font _small = new Font("Segoe UI", 8f);

            public CardList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = Theme.Ui;
                ItemHeight = 46;
                BackColor = Theme.Sidebar;
                ForeColor = Theme.Text;
            }

            public Func<CardConfig, SentinelStatus> StatusOf { get; set; }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var card = (CardConfig)Items[e.Index];
                SentinelStatus st = StatusOf != null ? StatusOf(card) : SentinelStatus.Stopped;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Graphics g = e.Graphics;
                using (var b = new SolidBrush(selected ? Theme.TreeSelectedStrong : BackColor)) g.FillRectangle(b, e.Bounds);
                if (selected) using (var a = new SolidBrush(Theme.Accent)) g.FillRectangle(a, e.Bounds.X, e.Bounds.Y + 6, 3, e.Bounds.Height - 12);

                Color statusColor = st == SentinelStatus.Running ? Theme.Success
                                  : st == SentinelStatus.Stopped ? Theme.TextFaint : Theme.Warning;
                IconRenderer.Draw(g, IconKind.Pulse, new RectangleF(e.Bounds.X + 14, e.Bounds.Y + 15, 16, 16), statusColor);

                const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                int x = e.Bounds.X + 40;
                var r1 = new Rectangle(x, e.Bounds.Y + 7, e.Bounds.Right - x - 8, 18);
                TextRenderer.DrawText(g, card.Label, Theme.UiBold, r1, selected ? Theme.ListSelectedText : Theme.Text, flags);
                string status = st == SentinelStatus.Running ? "çalışıyor" : st == SentinelStatus.Starting ? "başlatılıyor…"
                              : st == SentinelStatus.Stopping ? "durduruluyor…" : "çalışmıyor";
                string group = card.Group != null ? card.Group.Name + "  ·  " : "";
                var r2 = new Rectangle(x, e.Bounds.Y + 25, e.Bounds.Right - x - 8, 16);
                TextRenderer.DrawText(g, group + status, _small, r2, st == SentinelStatus.Stopped ? Theme.TextMuted : statusColor, flags);
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left + 12, e.Bounds.Bottom - 1, e.Bounds.Right - 12, e.Bounds.Bottom - 1);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _small.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
