using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Sol kenar çubuğu: "Kart Ekle" düğmesi, arama kutusu ve grup → kart ağacı.
    /// Gruba tıklamak açar/kapatır, karta çift tıklamak kartı açar, kutucuklarla seçilen kartlar topluca açılır.
    /// İşlemleri olay olarak bildirir; bağlantı ve görünümlerle ilgili kararları ana pencere verir.
    /// </summary>
    public sealed class CardExplorer : UserControl
    {
        private readonly List<CardGroup> _groups;
        private readonly Func<CardConfig, CardState> _stateOf;
        private readonly Func<CardConfig, bool> _isOpen;
        private readonly HashSet<CardConfig> _checked = new HashSet<CardConfig>();

        private readonly ExplorerTree _tree = new ExplorerTree();
        private readonly TextBox _search = new TextBox();
        private readonly Label _countLabel = Theme.MakeLabel("", Theme.UiSmall, Theme.TextFaint);
        private readonly Button _addBtn = Theme.MakeButton("Kart Ekle", IconKind.Plus, Theme.ButtonKind.Primary);
        private readonly IconButton _connectBtn = new IconButton(IconKind.Connect, IconTone.Primary) { Label = ConnectionAction.ConnectLabel };
        private readonly Button _showBtn = Theme.MakeButton("Seçilenleri Göster", IconKind.Terminal, Theme.ButtonKind.Secondary);
        private readonly IconButton _clearChecksBtn = new IconButton(IconKind.Close, IconTone.Ghost) { IconSize = 12 };
        private readonly Label _selectionInfo = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly ToolTip _tips = new DarkToolTip();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly Font _groupFont = new Font("Segoe UI Semibold", 9.5f);
        private readonly Font _cardFont = new Font("Consolas", 9.5f);
        private bool _suppressCheck;

        public CardExplorer(List<CardGroup> groups, Func<CardConfig, CardState> stateOf, Func<CardConfig, bool> isOpen)
        {
            _groups = groups;
            _stateOf = stateOf;
            _isOpen = isOpen;

            BackColor = Theme.Sidebar;
            DoubleBuffered = true;
            BuildLayout();
            BuildTree();
            _menu.Renderer = new DarkMenuRenderer();
            _menu.ShowImageMargin = false;
            _menu.Font = Theme.Ui;
            Rebuild();
        }

        // ---------- Olaylar ----------

        /// <summary>Kart ekleme penceresi istenir. Parametre doluysa o gruba eklenir.</summary>
        public event Action<CardGroup> AddCardsRequested;
        /// <summary>Kartları çalışma alanına ekler (bağlantıya dokunmaz).</summary>
        public event Action<List<CardConfig>> OpenRequested;
        /// <summary>Çalışma alanında TAM OLARAK bu kartlar gösterilsin (bağlantıya dokunmaz).</summary>
        public event Action<List<CardConfig>> ShowExactlyRequested;
        /// <summary>Kartlara bağlan (true) ya da bağlantılarını kes (false). Görünüme dokunmaz.</summary>
        public event Action<List<CardConfig>, bool> ConnectRequested;
        public event Action<CardGroup> RemoveGroupRequested;
        /// <summary>Kartı "Kart Dosya Gezgini" sekmesinde aç (bağlantıya dokunmaz).</summary>
        public event Action<CardConfig> BrowseFilesRequested;
        /// <summary>Kartın görünen adı değiştirilmek isteniyor.</summary>
        public event Action<CardConfig> RenameCardRequested;

        private CardConfig _highlighted;

        /// <summary>
        /// Kart Dosya Gezgini'nde gezilen kart: ağaçta ayrı bir vurguyla gösterilir (seçimi, işaretleri ve
        /// bağlantı durumunu DEĞİŞTİRMEZ). null: vurgu yok.
        /// </summary>
        public CardConfig HighlightedCard
        {
            get { return _highlighted; }
            set
            {
                if (_highlighted == value) return;
                _highlighted = value;
                if (value != null) EnsureCardVisible(value);
                _tree.Invalidate();
            }
        }

        /// <summary>Kartın düğümü görünür olsun (grubu kapalıysa açılır, gerekirse kaydırılır).</summary>
        private void EnsureCardVisible(CardConfig card)
        {
            foreach (TreeNode gn in _tree.Nodes)
                foreach (TreeNode cn in gn.Nodes)
                    if (cn.Tag == card)
                    {
                        if (!gn.IsExpanded) gn.Expand();
                        cn.EnsureVisible();
                        return;
                    }
        }
        public event Action<CardConfig> RemoveCardRequested;
        /// <summary>Kaydedilmesi gereken bir görünüm değişikliği oldu (ör. grup açıldı/kapandı).</summary>
        public event Action ConfigChanged;

        // ---------- Yerleşim ----------

        private void BuildLayout()
        {
            // Üst kısım: başlık + Kart Ekle + arama
            var top = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = Theme.Sidebar, Padding = new Padding(14, 14, 14, 8) };
            var title = Theme.MakeLabel("KART GEZGİNİ", Theme.SectionHeader, Theme.TextMuted);
            title.Location = new Point(14, 16);
            _countLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _addBtn.Height = 36;
            _addBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _addBtn.Click += (s, e) => { var h = AddCardsRequested; if (h != null) h(null); };

            var searchBox = new Panel { Height = 32, BackColor = Theme.Input, Padding = new Padding(30, 8, 8, 4), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            searchBox.Paint += (s, e) =>
            {
                IconRenderer.Draw(e.Graphics, IconKind.Search, new RectangleF(9, 9, 14, 14), _search.Focused ? Theme.Accent : Theme.TextMuted);
                using (var p = new Pen(_search.Focused ? Theme.Accent : Theme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, searchBox.Width - 1, searchBox.Height - 1);
            };
            searchBox.Click += (s, e) => _search.Focus();
            _search.GotFocus += (s, e) => searchBox.Invalidate();
            _search.LostFocus += (s, e) => searchBox.Invalidate();
            _search.BorderStyle = BorderStyle.None;
            _search.BackColor = Theme.Input;
            _search.ForeColor = Theme.Text;
            _search.Font = Theme.Ui;
            _search.Dock = DockStyle.Fill;
            _search.TextChanged += (s, e) => Rebuild();
            _search.HandleCreated += (s, e) => SetCueBanner(_search, "Grup veya IP ara…");
            searchBox.Controls.Add(_search);

            top.Controls.Add(title);
            top.Controls.Add(_countLabel);
            top.Controls.Add(_addBtn);
            top.Controls.Add(searchBox);
            top.Resize += (s, e) =>
            {
                int w = top.Width - 28;
                _countLabel.Location = new Point(top.Width - 14 - _countLabel.Width, 16);
                _addBtn.SetBounds(14, 42, w, 36);
                searchBox.SetBounds(14, 89, w, 32);
            };

            // Alt kısım: seçim bilgisi + bağlantı ve görünüm düğmeleri (birbirinden bağımsız)
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 124, BackColor = Theme.Sidebar };
            _connectBtn.Height = 34;
            _showBtn.Height = 34;
            _clearChecksBtn.Height = 22;
            _clearChecksBtn.Width = 24;
            _selectionInfo.AutoSize = false;
            _selectionInfo.AutoEllipsis = true;
            _selectionInfo.Height = 18;
            bottom.Controls.Add(_selectionInfo);
            bottom.Controls.Add(_clearChecksBtn);
            bottom.Controls.Add(_connectBtn);
            bottom.Controls.Add(_showBtn);
            bottom.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
            bottom.Resize += (s, e) =>
            {
                int w = bottom.Width - 28;
                _selectionInfo.SetBounds(14, 12, w - 28, 18);
                _clearChecksBtn.Location = new Point(bottom.Width - 10 - _clearChecksBtn.Width, 9);
                _connectBtn.SetBounds(14, 38, w, 34);
                _showBtn.SetBounds(14, 78, w, 34);
            };

            _tips.SetToolTip(_connectBtn, "Seçili kartlara bağlanır ya da bağlantılarını keser.\nÇalışma alanında gösterilen kartları değiştirmez.");
            _tips.SetToolTip(_showBtn, "Çalışma alanında tam olarak seçili kartları gösterir.\nBağlantı kurmaz, var olan bağlantıları kesmez.");
            _tips.SetToolTip(_clearChecksBtn, "Tüm kutucukların işaretini kaldır");

            _connectBtn.Click += (s, e) => OnConnectClicked();
            _showBtn.Click += (s, e) =>
            {
                List<CardConfig> sel = SelectedCards();
                if (sel.Count == 0) return;
                var h = ShowExactlyRequested;
                if (h != null) h(sel);
            };
            _clearChecksBtn.Click += (s, e) => { _checked.Clear(); Rebuild(); };

            var treeHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(6, 0, 2, 0) };
            treeHost.Controls.Add(_tree);

            Controls.Add(treeHost);
            Controls.Add(bottom);
            Controls.Add(top);
        }

        private void BuildTree()
        {
            _tree.Dock = DockStyle.Fill;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = Theme.Sidebar;
            _tree.ForeColor = Theme.Text;
            _tree.Font = Theme.Ui;
            _tree.CheckBoxes = true;
            _tree.ShowLines = false;
            _tree.ShowRootLines = true;
            _tree.FullRowSelect = true;
            _tree.HotTracking = false;
            _tree.ItemHeight = 28;
            _tree.Indent = 16;
            _tree.HideSelection = false;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            Theme.ApplyDarkScrollBars(_tree);

            _tree.DrawNode += Tree_DrawNode;
            _tree.AfterCheck += Tree_AfterCheck;
            _tree.NodeMouseClick += Tree_NodeMouseClick;
            _tree.NodeDoubleClicked += Tree_NodeDoubleClicked;
            _tree.AfterExpand += (s, e) => SetExpanded(e.Node, true);
            _tree.AfterCollapse += (s, e) => SetExpanded(e.Node, false);
            _tree.KeyDown += Tree_KeyDown;
            _tree.AfterSelect += (s, e) => UpdateActionButtons();
        }

        // ---------- Ağacı doldurma ----------

        /// <summary>Gruplar değiştiğinde ağacı yeniden kurar (arama filtresini uygular).</summary>
        public void Rebuild()
        {
            string filter = _search.Text.Trim();
            bool filtering = filter.Length > 0;
            object selectedTag = _tree.SelectedNode != null ? _tree.SelectedNode.Tag : null;

            // Silinmiş kartların işaretlerini temizle
            var alive = new HashSet<CardConfig>(_groups.SelectMany(g => g.Cards));
            _checked.RemoveWhere(c => !alive.Contains(c));

            _suppressCheck = true;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode toSelect = null;

            foreach (CardGroup g in _groups)
            {
                bool groupMatch = !filtering || g.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                var cards = g.Cards.Where(c => groupMatch || c.Ip.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               (c.HasName && c.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (filtering && cards.Count == 0 && !groupMatch) continue;

                var gn = new TreeNode(Pad(g.Name, 14)) { Tag = g };
                foreach (CardConfig c in cards)
                {
                    var cn = new TreeNode(Pad(c.Label, 18)) { Tag = c, Checked = _checked.Contains(c) };
                    gn.Nodes.Add(cn);
                    if (c == selectedTag) toSelect = cn;
                }
                gn.Checked = cards.Count > 0 && cards.All(c => _checked.Contains(c));
                _tree.Nodes.Add(gn);
                if (filtering || g.Expanded) gn.Expand();
                if (g == selectedTag) toSelect = gn;
            }

            if (toSelect != null) _tree.SelectedNode = toSelect;
            _tree.EndUpdate();
            _suppressCheck = false;

            int cardCount = _groups.Sum(g => g.Cards.Count);
            _countLabel.Text = _groups.Count + " grup · " + cardCount + " kart";
            if (_countLabel.Parent != null)
                _countLabel.Location = new Point(_countLabel.Parent.Width - 14 - _countLabel.Width, 16);
            UpdateActionButtons();
        }

        /// <summary>Durum noktalarını yeniden çizer (bağlantı durumu değişince çağrılır).</summary>
        public void RefreshStates()
        {
            _tree.Invalidate();
            UpdateActionButtons();   // bağlantı düğmesi seçili kartların durumunu yansıtır
        }

        // Metin, sahibi çizilen ağaçta durum noktası ve rozet için tıklanabilir alanı genişletir
        private static string Pad(string text, int extraSpaces)
        {
            return "   " + text + new string(' ', extraSpaces);
        }

        // ---------- Çizim ----------

        private void Tree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Bounds.Height <= 0 || e.Bounds.Width <= 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            var row = new Rectangle(e.Bounds.X, e.Bounds.Y, Math.Max(e.Bounds.Width, _tree.ClientSize.Width - e.Bounds.X - 2), e.Bounds.Height);
            using (var bg = new SolidBrush(selected ? Theme.Selection : Theme.Sidebar))
                g.FillRectangle(bg, row);

            var group = e.Node.Tag as CardGroup;
            if (group != null)
            {
                int connected = group.Cards.Count(c => _stateOf(c) == CardState.Connected);
                string name = group.Name;
                TextRenderer.DrawText(g, name, _groupFont, new Point(row.X + 2, row.Y + 5), Theme.Text, TextFormatFlags.NoPrefix);
                Size nameSize = TextRenderer.MeasureText(g, name, _groupFont, Size.Empty, TextFormatFlags.NoPrefix);

                string badge = connected + "/" + group.Cards.Count;
                Size bs = TextRenderer.MeasureText(g, badge, Theme.UiSmall, Size.Empty, TextFormatFlags.NoPrefix);
                var badgeRect = new Rectangle(row.X + 2 + nameSize.Width + 4, row.Y + 6, bs.Width + 6, 17);
                using (var path = RoundRect(badgeRect, 8))
                using (var br = new SolidBrush(connected > 0 ? Color.FromArgb(40, Theme.Success) : Theme.SurfaceHigh))
                    g.FillPath(br, path);
                TextRenderer.DrawText(g, badge, Theme.UiSmall, badgeRect, connected > 0 ? Theme.Success : Theme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                string port = ":" + group.Port;
                TextRenderer.DrawText(g, port, Theme.UiSmall, new Point(badgeRect.Right + 6, row.Y + 7), Theme.TextFaint, TextFormatFlags.NoPrefix);
                return;
            }

            var card = e.Node.Tag as CardConfig;
            if (card != null)
            {
                // Dosya Gezgini'nde gezilen kart: vurgulu zemin + solda mavi şerit (seçimden bağımsız)
                bool explored = card == _highlighted;
                if (explored)
                {
                    using (var hb = new SolidBrush(selected ? Theme.TreeSelectedStrong : Theme.TreeSelectedSoft)) g.FillRectangle(hb, row);
                    using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, row.X, row.Y + 3, 3, row.Height - 6);
                }

                CardState st = _stateOf(card);
                Color dot = Theme.StateColor(st);
                using (var br = new SolidBrush(dot))
                    g.FillEllipse(br, row.X + 6, row.Y + 10, 8, 8);

                bool open = _isOpen(card);
                Color fore = open || explored || selected ? Theme.Text : Theme.TextMuted;
                int x = row.X + 20;
                TextRenderer.DrawText(g, card.Ip, _cardFont, new Point(x, row.Y + 6), fore, TextFormatFlags.NoPrefix);
                x += TextRenderer.MeasureText(g, card.Ip, _cardFont, Size.Empty, TextFormatFlags.NoPrefix).Width;
                if (card.HasName)
                {
                    // "IP (Ad)": ad parantez içinde, IP'nin hemen yanında
                    string nm = "(" + card.Name.Trim() + ")";
                    TextRenderer.DrawText(g, nm, Theme.Ui, new Point(x - 2, row.Y + 6), explored || open || selected ? Theme.Text : Theme.TextMuted, TextFormatFlags.NoPrefix);
                    x += TextRenderer.MeasureText(g, nm, Theme.Ui, Size.Empty, TextFormatFlags.NoPrefix).Width;
                }
                if (open)
                {
                    TextRenderer.DrawText(g, "ekranda", Theme.UiSmall, new Point(x, row.Y + 7), Theme.Accent, TextFormatFlags.NoPrefix);
                    x += TextRenderer.MeasureText(g, "ekranda", Theme.UiSmall, Size.Empty, TextFormatFlags.NoPrefix).Width;
                }
                if (explored)
                    TextRenderer.DrawText(g, "gezginde", Theme.UiSmall, new Point(x, row.Y + 7), Theme.TreeTag, TextFormatFlags.NoPrefix);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
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

        // ---------- Etkileşim ----------

        private void Tree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_suppressCheck) return;
            _suppressCheck = true;
            try
            {
                var group = e.Node.Tag as CardGroup;
                if (group != null)
                {
                    // Grup kutusu: görünen tüm kartları işaretle / kaldır
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Checked = e.Node.Checked;
                        SetChecked((CardConfig)child.Tag, e.Node.Checked);
                    }
                }
                else
                {
                    SetChecked((CardConfig)e.Node.Tag, e.Node.Checked);
                    TreeNode parent = e.Node.Parent;
                    if (parent != null) parent.Checked = parent.Nodes.Cast<TreeNode>().All(n => n.Checked);
                }
            }
            finally
            {
                _suppressCheck = false;
            }
            UpdateActionButtons();
        }

        private void SetChecked(CardConfig card, bool on)
        {
            if (on) _checked.Add(card);
            else _checked.Remove(card);
        }

        private void Tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeViewHitTestLocations where = _tree.HitTest(e.Location).Location;
            if (e.Button == MouseButtons.Right)
            {
                _tree.SelectedNode = e.Node;
                ShowMenu(e.Node, e.Location);
                return;
            }
            // Grup adına tek tıklama: aç / kapat
            if (e.Button == MouseButtons.Left && e.Node.Tag is CardGroup &&
                (where == TreeViewHitTestLocations.Label || where == TreeViewHitTestLocations.RightOfLabel))
            {
                if (e.Node.IsExpanded) e.Node.Collapse();
                else e.Node.Expand();
            }
        }

        private void Tree_NodeDoubleClicked(TreeNode node)
        {
            var card = node.Tag as CardConfig;
            if (card != null) RaiseOpen(new List<CardConfig> { card });
        }

        private void Tree_KeyDown(object sender, KeyEventArgs e)
        {
            TreeNode n = _tree.SelectedNode;
            if (n == null) return;
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var card = n.Tag as CardConfig;
                if (card != null) RaiseOpen(new List<CardConfig> { card });
                else RaiseOpen(((CardGroup)n.Tag).Cards.ToList());
            }
            else if (e.KeyCode == Keys.F2 && n.Tag is CardConfig)
            {
                e.SuppressKeyPress = true;
                var h = RenameCardRequested;
                if (h != null) h((CardConfig)n.Tag);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                var card = n.Tag as CardConfig;
                if (card != null) { var h = RemoveCardRequested; if (h != null) h(card); }
                else { var h = RemoveGroupRequested; if (h != null) h((CardGroup)n.Tag); }
            }
        }

        private void SetExpanded(TreeNode node, bool expanded)
        {
            var g = node.Tag as CardGroup;
            if (g == null || _search.Text.Trim().Length > 0 || g.Expanded == expanded) return;
            g.Expanded = expanded;
            var h = ConfigChanged;
            if (h != null) h();
        }

        private void ShowMenu(TreeNode node, Point location)
        {
            _menu.Items.Clear();
            var group = node.Tag as CardGroup;
            if (group != null)
            {
                List<CardConfig> checkedInGroup = group.Cards.Where(c => _checked.Contains(c)).ToList();
                AddItem("Tümünü ekranda göster", () => RaiseOpen(group.Cards.ToList()), group.Cards.Count > 0);
                AddItem("Seçilileri ekranda göster (" + checkedInGroup.Count + ")", () => RaiseOpen(checkedInGroup), checkedInGroup.Count > 0);
                _menu.Items.Add(new ToolStripSeparator());
                AddItem("Tümünü bağla", () => RaiseConnect(group.Cards.ToList(), true), group.Cards.Count > 0);
                AddItem("Tümünün bağlantısını kes", () => RaiseConnect(group.Cards.ToList(), false), group.Cards.Count > 0);
                _menu.Items.Add(new ToolStripSeparator());
                AddItem("Gruba kart ekle…", () => { var h = AddCardsRequested; if (h != null) h(group); }, true);
                ToolStripItem del = AddItem("Grubu sil", () => { var h = RemoveGroupRequested; if (h != null) h(group); }, true);
                del.ForeColor = Theme.Danger;
            }
            else
            {
                var card = (CardConfig)node.Tag;
                CardState st = _stateOf(card);
                bool live = st == CardState.Connected || st == CardState.Connecting;
                AddItem("Ekranda göster", () => RaiseOpen(new List<CardConfig> { card }), true);
                AddItem(live ? "Bağlantıyı kes" : "Bağlan", () => RaiseConnect(new List<CardConfig> { card }, !live), true);
                AddItem("Dosya Gezgininde aç", () => { var h = BrowseFilesRequested; if (h != null) h(card); }, true);
                AddItem(card.HasName ? "Adı değiştir…" : "Ad ver…", () => { var h = RenameCardRequested; if (h != null) h(card); }, true);
                _menu.Items.Add(new ToolStripSeparator());
                ToolStripItem del = AddItem("Kartı kaldır", () => { var h = RemoveCardRequested; if (h != null) h(card); }, true);
                del.ForeColor = Theme.Danger;
            }
            _menu.Show(_tree, location);
        }

        private ToolStripItem AddItem(string text, Action action, bool enabled)
        {
            var item = new ToolStripMenuItem(text) { Enabled = enabled, ForeColor = Theme.Text };
            item.Click += (s, e) => action();
            _menu.Items.Add(item);
            return item;
        }

        /// <summary>
        /// Seçili kartlar: işaretli kutucuklar varsa onlar (kenar çubuğu sırasıyla);
        /// yoksa ağaçta vurgulanan kart ya da vurgulanan grubun tüm kartları.
        /// </summary>
        public List<CardConfig> SelectedCards()
        {
            if (_checked.Count > 0)
                return _groups.SelectMany(g => g.Cards).Where(c => _checked.Contains(c)).ToList();

            TreeNode n = _tree.SelectedNode;
            if (n == null) return new List<CardConfig>();
            var card = n.Tag as CardConfig;
            if (card != null) return new List<CardConfig> { card };
            var group = n.Tag as CardGroup;
            return group != null ? group.Cards.ToList() : new List<CardConfig>();
        }

        private void OnConnectClicked()
        {
            List<CardConfig> sel = SelectedCards();
            ConnectionAction action = SelectionLogic.DecideConnection(sel.Select(c => _stateOf(c)).ToList());
            if (!action.Enabled) return;

            // "Bağlantı Kur": yalnız bağlı olmayanlara bağlan (bağlı olanlara dokunma)
            // "Bağlantıyı Kes": seçili kartların hepsinin bağlantısını kes
            List<CardConfig> targets = action.Disconnect
                ? sel
                : sel.Where(c => !SelectionLogic.IsLive(_stateOf(c))).ToList();
            RaiseConnect(targets, !action.Disconnect);
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            List<CardConfig> sel = SelectedCards();
            ConnectionAction action = SelectionLogic.DecideConnection(sel.Select(c => _stateOf(c)).ToList());

            _connectBtn.Label = action.Label;
            _connectBtn.Icon = action.Disconnect ? IconKind.Disconnect : IconKind.Connect;
            _connectBtn.Tone = action.Disconnect ? IconTone.DangerFill : IconTone.Primary;
            if (_connectBtn.Enabled != action.Enabled) _connectBtn.Enabled = action.Enabled;

            _showBtn.Enabled = sel.Count > 0;
            _showBtn.ForeColor = sel.Count > 0 ? Theme.Text : Theme.TextFaint;

            int shown = sel.Count(c => _isOpen(c));
            string info;
            if (sel.Count == 0) info = "Kart seçmek için kutucukları işaretleyin";
            else
            {
                info = sel.Count + " kart seçili  ·  " + action.Live + " bağlı  ·  " + shown + " ekranda";
                if (_checked.Count == 0) info = (_tree.SelectedNode != null && _tree.SelectedNode.Tag is CardGroup ? "Grup: " : "Kart: ") + info;
            }
            if (_selectionInfo.Text != info) _selectionInfo.Text = info;
            _clearChecksBtn.Visible = _checked.Count > 0;
        }

        private void RaiseOpen(List<CardConfig> cards)
        {
            if (cards.Count == 0) return;
            var h = OpenRequested;
            if (h != null) h(cards);
        }

        private void RaiseConnect(List<CardConfig> cards, bool connect)
        {
            var h = ConnectRequested;
            if (h != null) h(cards, connect);
        }

        // ---------- Yardımcılar ----------

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox box, string text)
        {
            try { SendMessage(box.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, text); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _menu.Dispose();
                _tips.Dispose();
                _groupFont.Dispose();
                _cardFont.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Çift tıklamada varsayılan aç/kapa davranışını (ve onay kutusu hatasını) engelleyip
        /// kendi olayını tetikleyen ağaç.
        /// </summary>
        private sealed class ExplorerTree : TreeView
        {
            private const int WM_LBUTTONDBLCLK = 0x0203;

            public event Action<TreeNode> NodeDoubleClicked;

            public ExplorerTree()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDBLCLK)
                {
                    long lp = m.LParam.ToInt64();
                    var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                    TreeViewHitTestInfo hit = HitTest(pt);
                    if (hit.Node != null && hit.Location != TreeViewHitTestLocations.StateImage &&
                        hit.Location != TreeViewHitTestLocations.PlusMinus)
                    {
                        var h = NodeDoubleClicked;
                        if (h != null) h(hit.Node);
                    }
                    return;   // varsayılan davranışı yutuyoruz
                }
                base.WndProc(ref m);
            }
        }
    }

    /// <summary>Sağ tık menüleri için koyu renk düzeni.</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled) e.TextColor = Theme.TextFaint;
            base.OnRenderItemText(e);
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.SurfaceHigh; } }
            public override Color MenuStripGradientBegin { get { return Theme.Sidebar; } }
            public override Color MenuStripGradientEnd { get { return Theme.Sidebar; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.SurfaceHigh; } }
            public override Color MenuItemPressedGradientMiddle { get { return Theme.SurfaceHigh; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.SurfaceHigh; } }
            public override Color ToolStripBorder { get { return Theme.Sidebar; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color MenuItemBorder { get { return Theme.Hover; } }
            public override Color MenuItemSelected { get { return Theme.Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
            public override Color ImageMarginGradientBegin { get { return Theme.SurfaceHigh; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.SurfaceHigh; } }
            public override Color ImageMarginGradientEnd { get { return Theme.SurfaceHigh; } }
        }
    }
}
