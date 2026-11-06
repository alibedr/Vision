using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// "Komut Kütüphanesi" sekmesi. Solda arama + modül ağacı (modül başlıkları altında komutlar; daraltılabilir,
    /// sürükle-bırak ile komut taşıma), sağda seçilen komutun ayrıntısı / düzenlemesi ya da seçilen modülün özeti.
    /// Her değişiklik Komutlar\Komutlar.xml'e hemen yazılır.
    /// </summary>
    public sealed partial class CommandLibraryView : UserControl
    {
        private const string DragFormat = "Vision.CommandDoc";
        private const int ModuleRowHeight = 38;
        private const int CommandRowHeight = 46;
        private const int CommandIndent = 30;
        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        private readonly CommandLibrary _library;

        // Üst çubuk
        private readonly Label _stats = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly IconButton _newModuleBtn = new IconButton(IconKind.Module, IconTone.Secondary) { Label = "Yeni modül" };
        private readonly IconButton _newBtn = new IconButton(IconKind.Plus, IconTone.Primary) { Label = "Yeni komut" };
        private readonly IconButton _folderBtn = new IconButton(IconKind.Folder, IconTone.Ghost);

        // Sol: arama + ağaç
        private readonly TextBox _search = new TextBox { BorderStyle = BorderStyle.None };
        private readonly IconButton _clearSearchBtn = new IconButton(IconKind.Close, IconTone.Ghost) { IconSize = 12, TabStop = false };
        private readonly TreeListBox _list = new TreeListBox { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawVariable, IntegralHeight = false };
        private readonly Label _count = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly IconButton _expandAllBtn = new IconButton(IconKind.ExpandAll, IconTone.Ghost) { IconSize = 14, TabStop = false };
        private readonly IconButton _collapseAllBtn = new IconButton(IconKind.CollapseAll, IconTone.Ghost) { IconSize = 14, TabStop = false };
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();

        // Sağ
        private readonly Label _banner = new GlyphLabel { Closable = true, Dock = DockStyle.Top, Height = 34, Visible = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 8, 0), Cursor = Cursors.Hand };
        private readonly Panel _detail = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
        private readonly Label _empty = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, BackColor = Theme.Window, Font = new Font("Segoe UI", 10.5f) };
        private readonly ToolTip _tips = new DarkToolTip();

        // Ağaç durumu
        private readonly List<LibRow> _rows = new List<LibRow>();
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private int _hoverIndex = -1;
        private int _dropIndex = -1;
        private Point _dragStart;
        private CommandDoc _dragCandidate;

        // Seçim
        private CommandDoc _current;          // görüntülenen komut (yeni taslakta null)
        private string _currentModuleKey;     // görüntülenen modül sayfası ("" = Modülsüz); komut sayfasında null
        private bool _editing;
        private bool _isNew;
        private bool _suppressSelect;
        private int _lastIndex = -1;

        /// <summary>
        /// Kütüphane exe'ye gömülü ve salt okunur mu? Öyleyse ekleme, düzenleme, silme, modüle taşıma ve sürükle-bırak
        /// kapalıdır; komutlar yalnız okunur, aranır, kopyalanır ve karta aktarılır. İçerik Vision'ın yeni sürümleriyle değişir.
        /// </summary>
        private bool ReadOnlyLibrary { get { return _library.IsReadOnly; } }

        public CommandLibraryView(CommandLibrary library)
        {
            _library = library;
            BackColor = Theme.Window;
            DoubleBuffered = true;
            BuildLayout();
            BuildCommandPage();
            BuildModulePage();
            WireEvents();
            ReloadFromDisk(null, null);
            SeedBundledIfEmpty();     // ilk açılış: boş kütüphane hazır VxWorks 6.4 komutlarıyla dolar
        }

        // =====================================================================
        // Yerleşim
        // =====================================================================

        private void BuildLayout()
        {
            // ---- Üst çubuk: başlık + istatistik | [klasör] [Yeni modül] [Yeni komut]
            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Surface };
            var title = Theme.MakeLabel("Komut Kütüphanesi", Theme.Title, Theme.Text);
            title.Location = new Point(20, 12);
            _stats.Location = new Point(21, 38);
            _newBtn.Size = new Size(122, 34);
            _newModuleBtn.Size = new Size(122, 34);
            _folderBtn.Size = new Size(34, 34);
            top.Controls.AddRange(new Control[] { title, _stats, _newBtn, _newModuleBtn, _folderBtn });
            BuildBundledButton(top);
            if (ReadOnlyLibrary)
            {
                // Gömülü kütüphane: ekleme / düzenleme düğmeleri yok
                _newBtn.Visible = _newModuleBtn.Visible = _folderBtn.Visible = _bundledBtn.Visible = false;
            }
            top.Resize += (s, e) =>
            {
                _newBtn.Location = new Point(top.Width - 20 - _newBtn.Width, 17);
                _newModuleBtn.Location = new Point(_newBtn.Left - 8 - _newModuleBtn.Width, 17);
                _folderBtn.Location = new Point(_newModuleBtn.Left - 8 - _folderBtn.Width, 17);
                _bundledBtn.Location = new Point(_folderBtn.Left - 8 - _bundledBtn.Width, 17);
            };
            top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };
            _newBtn.SetTip(_tips, "Yeni komut — seçili modüle eklenir  ·  Ctrl+Shift+N");
            _newModuleBtn.SetTip(_tips, "Yeni modül — komutları gruplamak için (ör. \"Task komutları\")  ·  Ctrl+Shift+M");
            _folderBtn.SetTip(_tips, "Komutlar.xml dosyasının bulunduğu klasörü aç");

            // ---- Sol panel
            var left = new Panel { Dock = DockStyle.Left, Width = 330, BackColor = Theme.Sidebar };

            var searchBox = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.Input };
            _search.BackColor = Theme.Input;
            _search.ForeColor = Theme.Text;
            _search.Font = Theme.Ui;
            Theme.SetCueBanner(_search, "Ara… (komut, açıklama, modül)   Ctrl+F");
            _clearSearchBtn.Size = new Size(24, 24);
            _clearSearchBtn.Visible = false;
            _clearSearchBtn.SetTip(_tips, "Aramayı temizle (Esc)");
            searchBox.Controls.Add(_search);
            searchBox.Controls.Add(_clearSearchBtn);
            searchBox.Resize += (s, e) =>
            {
                _search.SetBounds(32, (searchBox.Height - _search.PreferredHeight) / 2 + 1, searchBox.Width - 32 - 30, _search.PreferredHeight);
                _clearSearchBtn.Location = new Point(searchBox.Width - 28, 5);
            };
            searchBox.Paint += (s, e) =>
            {
                IconRenderer.Draw(e.Graphics, IconKind.Search, new RectangleF(10, 10, 14, 14), _search.Focused ? Theme.Accent : Theme.TextMuted);
                using (var p = new Pen(_search.Focused ? Theme.Accent : Theme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, searchBox.Width - 1, searchBox.Height - 1);
            };
            searchBox.Click += (s, e) => _search.Focus();
            _search.GotFocus += (s, e) => searchBox.Invalidate();
            _search.LostFocus += (s, e) => searchBox.Invalidate();
            var searchHost = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Theme.Sidebar, Padding = new Padding(12, 12, 12, 8) };
            searchHost.Controls.Add(searchBox);

            var countBar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Theme.Sidebar };
            _count.Location = new Point(14, 9);
            _expandAllBtn.Size = _collapseAllBtn.Size = new Size(26, 26);
            _expandAllBtn.SetTip(_tips, "Tüm modülleri genişlet");
            _collapseAllBtn.SetTip(_tips, "Tüm modülleri daralt");
            countBar.Controls.AddRange(new Control[] { _count, _expandAllBtn, _collapseAllBtn });
            countBar.Resize += (s, e) =>
            {
                _collapseAllBtn.Location = new Point(countBar.Width - 8 - _collapseAllBtn.Width, 3);
                _expandAllBtn.Location = new Point(_collapseAllBtn.Left - 2 - _expandAllBtn.Width, 3);
            };
            countBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, countBar.Width, 0); };

            _list.Dock = DockStyle.Fill;
            _list.BackColor = Theme.Sidebar;
            _list.ForeColor = Theme.Text;
            _list.AllowDrop = !ReadOnlyLibrary;
            _list.ContextMenuStrip = _menu;
            _menu.Renderer = new DarkMenuRenderer();
            Theme.ApplyDarkScrollBars(_list);
            var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(6, 0, 2, 4) };
            listHost.Controls.Add(_list);

            left.Controls.Add(listHost);
            left.Controls.Add(countBar);
            left.Controls.Add(searchHost);
            left.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, left.Width - 1, 0, left.Width - 1, left.Height); };
            var splitter = new Splitter { Dock = DockStyle.Left, Width = 3, BackColor = Theme.Border, MinSize = 240 };

            // ---- Sağ
            _detail.Controls.Add(_empty);
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
            right.Controls.Add(_detail);
            right.Controls.Add(_banner);

            Controls.Add(right);
            Controls.Add(splitter);
            Controls.Add(left);
            Controls.Add(top);
        }

        private void WireEvents()
        {
            _newBtn.Click += (s, e) => StartNew(SelectedModuleKey());
            _newModuleBtn.Click += (s, e) => NewModule();
            _folderBtn.Click += (s, e) => OpenFolder();
            _search.TextChanged += (s, e) =>
            {
                _clearSearchBtn.Visible = _search.TextLength > 0;
                RebuildList(_current, _current == null ? _currentModuleKey : null);
            };
            _search.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && _list.Items.Count > 0)
                {
                    e.SuppressKeyPress = true;
                    _list.Focus();
                    if (_list.SelectedIndex < 0) _list.SelectedIndex = FirstCommandIndex();
                }
                else if (e.KeyCode == Keys.Escape && _search.TextLength > 0 && !_editing) { e.SuppressKeyPress = true; _search.Clear(); }
            };
            _clearSearchBtn.Click += (s, e) => { _search.Clear(); _search.Focus(); };
            _expandAllBtn.Click += (s, e) => SetAllCollapsed(false);
            _collapseAllBtn.Click += (s, e) => SetAllCollapsed(true);

            _list.MeasureItem += (s, e) =>
            {
                LibRow r = e.Index >= 0 && e.Index < _rows.Count ? _rows[e.Index] : null;   // liste _rows'tan kurulur
                e.ItemHeight = r != null && r.Kind == LibRowKind.Module ? ModuleRowHeight : CommandRowHeight;
            };
            _list.DrawItem += List_DrawItem;
            _list.SelectedIndexChanged += List_SelectedIndexChanged;
            _list.IsToggleArea = (i, x) => i >= 0 && i < _rows.Count && _rows[i].Kind == LibRowKind.Module && x < 26 && _search.Text.Trim().Length == 0;
            _list.ToggleClick += i => { if (i < _rows.Count) ToggleModule(_rows[i].ModuleKey); };
            _list.MouseDown += List_MouseDown;
            _list.MouseMove += List_MouseMove;
            _list.MouseLeave += (s, e) => SetHover(-1);
            _list.MouseUp += (s, e) => _dragCandidate = null;
            _list.DoubleClick += (s, e) =>
            {
                LibRow r = SelectedRow;
                if (r == null || _editing) return;
                if (r.Kind == LibRowKind.Command) { if (!ReadOnlyLibrary) StartEdit(); }
                else ToggleModule(r.ModuleKey);
            };
            _list.KeyDown += List_KeyDown;
            _list.DragOver += List_DragOver;
            _list.DragDrop += List_DragDrop;
            _list.DragLeave += (s, e) => SetDropTarget(-1);
            _menu.Opening += (s, e) => { BuildMenu(); e.Cancel = _menu.Items.Count == 0; };
            _banner.Click += (s, e) => _banner.Visible = false;

            WireCommandPage();
            WireModulePage();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S) && _editing) { SaveEdit(); return true; }
            if (keyData == Keys.Escape && _editing) { CancelEdit(); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.N) && !ReadOnlyLibrary) { StartNew(SelectedModuleKey()); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.M) && !ReadOnlyLibrary) { NewModule(); return true; }
            if (keyData == (Keys.Control | Keys.F)) { _search.Focus(); _search.SelectAll(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // Dosya metin düzenleyiciyle dışarıdan değiştirildiyse (ve düzenleme yoksa) yeniden yükle
            if (Visible && !_editing && _library.ChangedOnDisk())
            {
                ReloadFromDisk(_current != null ? _current.Name : null, _current == null ? _currentModuleKey : null);
                ShowBanner("ℹ  Komutlar.xml dışarıdan değiştirilmiş; kütüphane yeniden yüklendi.", false);
            }
        }

        // =====================================================================
        // Ağaç (modül başlıkları + komutlar)
        // =====================================================================

        private enum LibRowKind { Module, Command }

        private sealed class LibRow
        {
            public LibRowKind Kind;
            /// <summary>Satırın modülü: modül adı, "" = Modülsüz.</summary>
            public string ModuleKey;
            public CommandModule Module;   // Modülsüz için null
            public CommandDoc Doc;
            public int Count;
            public bool Expanded;
            public bool First;
        }

        private static string Key(string module)
        {
            return (module ?? "").Trim().ToLower(Turkish);
        }

        private LibRow SelectedRow
        {
            get { return _list.SelectedItem as LibRow; }
        }

        /// <summary>Seçili satırın modülü (yeni komutun ekleneceği yer); seçim yoksa "" (Modülsüz).</summary>
        private string SelectedModuleKey()
        {
            if (_current != null) return _current.Module ?? "";
            if (_currentModuleKey != null) return _currentModuleKey;
            LibRow r = SelectedRow;
            return r != null ? r.ModuleKey : "";
        }

        private void ReloadFromDisk(string selectName, string selectModule)
        {
            _library.Load();
            if (_library.LoadError != null) ShowBanner("⚠  " + _library.LoadError, true);
            CommandDoc sel = selectName != null ? _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, selectName)) : null;
            if (selectModule != null && selectModule.Length > 0 && _library.FindModule(selectModule) == null) selectModule = null;
            _editing = false;
            _isNew = false;
            RebuildList(sel, sel == null ? selectModule : null);
            if (sel == null && selectModule == null && _list.Items.Count > 0)
            {
                int i = FirstCommandIndex();
                if (i >= 0) _list.SelectedIndex = i;   // → ShowDoc / ShowModule
            }
            else if (sel != null) ShowDoc(sel);
            else if (selectModule != null) ShowModule(selectModule);
            else ShowDoc(null);
        }

        private int FirstCommandIndex()
        {
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Kind == LibRowKind.Command) return i;
            return _rows.Count > 0 ? 0 : -1;
        }

        /// <summary>Ağacı yeniden kurar ve verilen komutu (yoksa modül başlığını) seçili gösterir; seçim olayı tetiklenmez.</summary>
        private void RebuildList(CommandDoc selectDoc, string selectModuleKey)
        {
            string q = _search.Text.Trim();
            bool searching = q.Length > 0;
            List<CommandDoc> hits = _library.Filter(q);
            CompareInfo ci = Turkish.CompareInfo;

            _rows.Clear();
            var groups = _library.Modules.Select(m => new KeyValuePair<string, CommandModule>(m.Name, m)).ToList();
            groups.Add(new KeyValuePair<string, CommandModule>("", null));
            foreach (var g in groups)
            {
                string key = g.Key;
                List<CommandDoc> docs = hits.Where(d => CommandLibrary.SameName(d.Module ?? "", key)).ToList();
                int total = searching ? docs.Count : _library.CommandsIn(key).Count;
                if (key.Length == 0)
                {
                    if (docs.Count == 0) continue;   // Modülsüz grup yalnız komutu varsa görünür
                }
                else if (searching && docs.Count == 0 && ci.IndexOf(key, q, CompareOptions.IgnoreCase) < 0) continue;

                bool expanded = searching || !_collapsed.Contains(Key(key));
                _rows.Add(new LibRow { Kind = LibRowKind.Module, ModuleKey = key, Module = g.Value, Count = total, Expanded = expanded, First = _rows.Count == 0 });
                if (!expanded) continue;
                foreach (CommandDoc d in docs) _rows.Add(new LibRow { Kind = LibRowKind.Command, ModuleKey = key, Doc = d });
            }

            int idx = -1;
            if (selectDoc != null)
            {
                idx = _rows.FindIndex(r => r.Kind == LibRowKind.Command && (r.Doc == selectDoc || CommandLibrary.SameName(r.Doc.Name, selectDoc.Name)));
                if (idx < 0) idx = _rows.FindIndex(r => r.Kind == LibRowKind.Module && CommandLibrary.SameName(r.ModuleKey, selectDoc.Module ?? ""));
            }
            else if (selectModuleKey != null)
                idx = _rows.FindIndex(r => r.Kind == LibRowKind.Module && CommandLibrary.SameName(r.ModuleKey, selectModuleKey));

            int topIndex = _list.Items.Count > 0 ? _list.TopIndex : 0;
            _suppressSelect = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (LibRow r in _rows) _list.Items.Add(r);
            if (topIndex < _list.Items.Count) _list.TopIndex = topIndex;
            _list.SelectedIndex = idx;
            _lastIndex = idx;
            _list.EndUpdate();
            _suppressSelect = false;
            _hoverIndex = -1;

            int totalCmds = _library.Items.Count, totalMods = _library.Modules.Count;
            _stats.Text = totalCmds + " komut  ·  " + totalMods + " modül  ·  " +
                          (ReadOnlyLibrary ? "VxWorks 6.4 · Vision'a gömülü (salt okunur)" : "Komutlar\\Komutlar.xml");
            _count.Text = searching ? hits.Count + " / " + totalCmds + " komut eşleşti" : totalCmds + " komut  ·  " + totalMods + " modül";
            _expandAllBtn.Enabled = _collapseAllBtn.Enabled = !searching && _rows.Any(r => r.Kind == LibRowKind.Module);
            if (!_editing && _current == null && _currentModuleKey == null) UpdateEmpty();
        }

        private void ToggleModule(string key)
        {
            if (_search.Text.Trim().Length > 0) return;   // aramada hepsi açık
            string k = Key(key);
            if (!_collapsed.Remove(k)) _collapsed.Add(k);
            bool collapsedNow = _collapsed.Contains(k);
            // Daraltılan modüldeki komut seçiliyse seçim başlığa geçer (sayfa değişmez)
            CommandDoc keep = _current != null && !(collapsedNow && CommandLibrary.SameName(_current.Module ?? "", key)) ? _current : null;
            RebuildList(keep, keep == null ? (_current != null ? _current.Module ?? "" : _currentModuleKey) : null);
        }

        private void SetAllCollapsed(bool collapse)
        {
            _collapsed.Clear();
            if (collapse)
            {
                foreach (CommandModule m in _library.Modules) _collapsed.Add(Key(m.Name));
                _collapsed.Add(Key(""));
            }
            CommandDoc keep = collapse ? null : _current;
            RebuildList(keep, keep == null ? (_current != null ? _current.Module ?? "" : _currentModuleKey) : null);
        }

        private void Expand(string key)
        {
            _collapsed.Remove(Key(key));
        }

        private void List_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSelect) return;
            LibRow target = SelectedRow;
            if (target == null) return;
            bool same = target.Kind == LibRowKind.Command
                ? target.Doc == _current && !_isNew
                : _current == null && !_isNew && _currentModuleKey != null && CommandLibrary.SameName(_currentModuleKey, target.ModuleKey);
            if (same) { _lastIndex = _list.SelectedIndex; return; }
            if (!ConfirmLeave())
            {
                // Kullanıcı vazgeçti: eski seçime dön
                _suppressSelect = true;
                _list.SelectedIndex = _lastIndex < _list.Items.Count ? _lastIndex : -1;
                _suppressSelect = false;
                return;
            }
            // ConfirmLeave içinde kayıt yapıldıysa liste yeniden kurulmuş olabilir: hedefi yeniden bul
            if (target.Kind == LibRowKind.Command)
            {
                CommandDoc doc = _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, target.Doc.Name)) ?? target.Doc;
                SelectQuietly(_rows.FindIndex(r => r.Kind == LibRowKind.Command && CommandLibrary.SameName(r.Doc.Name, doc.Name)));
                ShowDoc(doc);
            }
            else
            {
                SelectQuietly(_rows.FindIndex(r => r.Kind == LibRowKind.Module && CommandLibrary.SameName(r.ModuleKey, target.ModuleKey)));
                ShowModule(target.ModuleKey);
            }
        }

        private void SelectQuietly(int index)
        {
            _suppressSelect = true;
            _list.SelectedIndex = index < _list.Items.Count ? index : -1;
            _suppressSelect = false;
            _lastIndex = _list.SelectedIndex;
        }

        // ---------- Çizim ----------

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            var row = (LibRow)_list.Items[e.Index];
            Graphics g = e.Graphics;
            Rectangle b = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool hover = e.Index == _hoverIndex && !selected;
            bool drop = e.Index == _dropIndex;

            using (var bg = new SolidBrush(Theme.Sidebar)) g.FillRectangle(bg, b);
            if (row.Kind == LibRowKind.Module) DrawModuleRow(g, b, row, selected, hover, drop);
            else DrawCommandRow(g, b, row, selected, hover);
        }

        private void DrawModuleRow(Graphics g, Rectangle b, LibRow row, bool selected, bool hover, bool drop)
        {
            if (!row.First)
                using (var p = new Pen(Theme.RowDividerMid)) g.DrawLine(p, b.X + 4, b.Y + 1, b.Right - 4, b.Y + 1);

            var box = new RectangleF(b.X + 2, b.Y + 4, b.Width - 6, b.Height - 7);
            Color back = drop ? Theme.DropHighlightSoft : selected ? Theme.Selection : hover ? Theme.Hover : Color.Transparent;
            if (back.A > 0)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = IconRenderer.RoundRect(box, 5))
                using (var br = new SolidBrush(back))
                    g.FillPath(br, path);
                if (drop)
                    using (var path = IconRenderer.RoundRect(box, 5))
                    using (var p = new Pen(Theme.Accent, 1.5f))
                        g.DrawPath(p, path);
            }

            bool noModule = row.ModuleKey.Length == 0;
            int cy = b.Y + b.Height / 2;
            bool canToggle = _search.Text.Trim().Length == 0;
            if (canToggle)
                IconRenderer.Draw(g, row.Expanded ? IconKind.ChevronDown : IconKind.ChevronRight, new RectangleF(b.X + 8, cy - 6, 12, 12), Theme.TextMuted);
            IconRenderer.Draw(g, noModule ? IconKind.Folder : IconKind.Module, new RectangleF(b.X + 26, cy - 8, 16, 16),
                noModule ? Theme.TextMuted : Theme.Accent);

            string count = row.Count.ToString();
            Size cs = TextRenderer.MeasureText(count, Theme.UiSmall, Size.Empty, TextFormatFlags.NoPadding);
            var pill = new RectangleF(b.Right - 14 - cs.Width - 14, cy - 10, cs.Width + 14, 20);
            using (var path = IconRenderer.RoundRect(pill, 9.5f))
            using (var br = new SolidBrush(selected ? Theme.ListSelectedStrong : Theme.SurfaceHigh))
                g.FillPath(br, path);
            TextRenderer.DrawText(g, count, Theme.UiSmall, Rectangle.Round(pill), selected ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            string name = noModule ? CommandLibrary.NoModuleName : row.ModuleKey;
            var nameRect = new Rectangle(b.X + 50, b.Y, (int)pill.Left - b.X - 56, b.Height);
            TextRenderer.DrawText(g, name, Theme.UiBold, nameRect, noModule ? Theme.TextMuted : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void DrawCommandRow(Graphics g, Rectangle b, LibRow row, bool selected, bool hover)
        {
            CommandDoc d = row.Doc;
            // Modül içindeki girinti çizgisi
            using (var p = new Pen(Theme.RowDividerStrong)) g.DrawLine(p, b.X + 17, b.Y, b.X + 17, b.Bottom);

            var box = new RectangleF(b.X + CommandIndent - 6, b.Y + 2, b.Width - CommandIndent + 2, b.Height - 4);
            Color back = selected ? Theme.Selection : hover ? Theme.Hover : Color.Transparent;
            if (back.A > 0)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = IconRenderer.RoundRect(box, 5))
                using (var br = new SolidBrush(back))
                    g.FillPath(br, path);
            }
            if (selected)
                using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, b.X + 15, b.Y + 9, 4, b.Height - 18);

            int x = b.X + CommandIndent + 4;
            var nameRect = new Rectangle(x, b.Y + 6, b.Right - x - 8, 20);
            var sumRect = new Rectangle(x, b.Y + 25, b.Right - x - 8, 17);
            using (var nf = new Font("Consolas", 10f, FontStyle.Bold))
                TextRenderer.DrawText(g, d.Name, nf, nameRect, Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string summary = d.Summary.Length > 0 ? d.Summary : "(açıklama yok)";
            TextRenderer.DrawText(g, summary, Theme.UiSmall, sumRect, selected ? Theme.ListSelectedText : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void SetHover(int index)
        {
            if (index == _hoverIndex) return;
            int old = _hoverIndex;
            _hoverIndex = index;
            InvalidateRow(old);
            InvalidateRow(index);
        }

        private void SetDropTarget(int index)
        {
            if (index == _dropIndex) return;
            int old = _dropIndex;
            _dropIndex = index;
            InvalidateRow(old);
            InvalidateRow(index);
        }

        private void InvalidateRow(int i)
        {
            if (i >= 0 && i < _list.Items.Count) _list.Invalidate(_list.GetItemRectangle(i));
        }

        // ---------- Fare / klavye ----------

        private int RowAt(Point client)
        {
            int i = _list.IndexFromPoint(client);
            if (i < 0 || i >= _list.Items.Count) return -1;
            return _list.GetItemRectangle(i).Contains(client) ? i : -1;
        }

        private void List_MouseDown(object sender, MouseEventArgs e)
        {
            int i = RowAt(e.Location);
            _dragCandidate = null;
            if (i < 0) return;
            LibRow r = _rows[i];
            if (e.Button == MouseButtons.Right)
            {
                if (_list.SelectedIndex != i) _list.SelectedIndex = i;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (r.Kind == LibRowKind.Command && !_editing && !ReadOnlyLibrary) { _dragCandidate = r.Doc; _dragStart = e.Location; }
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            SetHover(RowAt(e.Location));
            if (_dragCandidate == null || e.Button != MouseButtons.Left) return;
            Size ds = SystemInformation.DragSize;
            if (Math.Abs(e.X - _dragStart.X) < ds.Width && Math.Abs(e.Y - _dragStart.Y) < ds.Height) return;
            CommandDoc doc = _dragCandidate;
            _dragCandidate = null;
            var data = new DataObject();
            data.SetData(DragFormat, doc.Name);
            data.SetText(doc.Usage != null && doc.Usage.Length > 0 ? doc.Usage : doc.Name);   // başka uygulamaya bırakılırsa yazılış
            _list.DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
            SetDropTarget(-1);
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!e.Data.GetDataPresent(DragFormat)) return;
            Point p = _list.PointToClient(new Point(e.X, e.Y));
            // Kenara yaklaşınca kaydır
            if (p.Y < 18 && _list.TopIndex > 0) _list.TopIndex--;
            else if (p.Y > _list.ClientSize.Height - 18 && _list.TopIndex < _list.Items.Count - 1) _list.TopIndex++;

            int i = RowAt(p);
            CommandDoc doc = DraggedDoc(e.Data);
            if (i < 0 || doc == null) { SetDropTarget(-1); return; }
            string key = _rows[i].ModuleKey;
            if (CommandLibrary.SameName(doc.Module ?? "", key)) { SetDropTarget(-1); return; }
            e.Effect = DragDropEffects.Move;
            SetDropTarget(_rows.FindIndex(r => r.Kind == LibRowKind.Module && CommandLibrary.SameName(r.ModuleKey, key)));
        }

        private void List_DragDrop(object sender, DragEventArgs e)
        {
            int target = _dropIndex;
            SetDropTarget(-1);
            CommandDoc doc = DraggedDoc(e.Data);
            if (target < 0 || doc == null) return;
            MoveTo(doc, _rows[target].ModuleKey);
        }

        private CommandDoc DraggedDoc(IDataObject data)
        {
            var name = data.GetData(DragFormat) as string;
            return name == null ? null : _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, name));
        }

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            LibRow r = SelectedRow;
            if (r == null || _editing) return;
            if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                if (r.Kind == LibRowKind.Command) DeleteCurrent();
                else DeleteModule(r.ModuleKey);
            }
            else if (e.KeyCode == Keys.F2 || (e.KeyCode == Keys.Enter && r.Kind == LibRowKind.Command))
            {
                e.SuppressKeyPress = true;
                if (r.Kind == LibRowKind.Command) StartEdit();
                else if (r.ModuleKey.Length > 0) EditModule(r.ModuleKey);
            }
            else if (r.Kind == LibRowKind.Module && (e.KeyCode == Keys.Enter || (e.KeyCode == Keys.Left && r.Expanded) || (e.KeyCode == Keys.Right && !r.Expanded)))
            {
                e.SuppressKeyPress = true;
                ToggleModule(r.ModuleKey);
            }
            else if (r.Kind == LibRowKind.Command && e.KeyCode == Keys.Left)
            {
                e.SuppressKeyPress = true;
                int h = _rows.FindIndex(x => x.Kind == LibRowKind.Module && CommandLibrary.SameName(x.ModuleKey, r.ModuleKey));
                if (h >= 0) _list.SelectedIndex = h;
            }
        }

        // ---------- Sağ tık menüsü ----------

        private void BuildMenu()
        {
            _menu.Items.Clear();
            if (_editing) return;
            Point p = _list.PointToClient(Cursor.Position);
            LibRow r = RowAt(p) >= 0 ? SelectedRow : null;

            if (ReadOnlyLibrary)
            {
                // Gömülü kütüphane: yalnız okuma işlemleri
                if (r != null && r.Kind == LibRowKind.Command)
                {
                    if (!string.IsNullOrEmpty(r.Doc.Usage)) AddMenu(_menu.Items, "Yazılışı kopyala", CopyUsage, null);
                    AddTransferSubmenu(_menu.Items, r.Doc);
                }
                else
                {
                    AddMenu(_menu.Items, "Tümünü genişlet", () => SetAllCollapsed(false), null);
                    AddMenu(_menu.Items, "Tümünü daralt", () => SetAllCollapsed(true), null);
                }
                return;
            }

            if (r != null && r.Kind == LibRowKind.Command)
            {
                CommandDoc d = r.Doc;
                AddMenu(_menu.Items, "Düzenle", StartEdit, "F2");
                if (!string.IsNullOrEmpty(d.Usage)) AddMenu(_menu.Items, "Yazılışı kopyala", CopyUsage, null);
                AddTransferSubmenu(_menu.Items, d);
                var move = new ToolStripMenuItem("Modüle taşı") { ForeColor = Theme.Text };
                foreach (CommandModule m in _library.Modules)
                {
                    string name = m.Name;
                    ToolStripMenuItem it = AddMenu(move.DropDownItems, name, () => MoveTo(d, name), null);
                    if (CommandLibrary.SameName(d.Module ?? "", name)) { it.Checked = true; it.Enabled = false; }
                }
                if (_library.Modules.Count > 0) move.DropDownItems.Add(new ToolStripSeparator());
                ToolStripMenuItem none = AddMenu(move.DropDownItems, CommandLibrary.NoModuleName + " (modülden çıkar)", () => MoveTo(d, ""), null);
                if (string.IsNullOrEmpty(d.Module)) { none.Checked = true; none.Enabled = false; }
                move.DropDownItems.Add(new ToolStripSeparator());
                AddMenu(move.DropDownItems, "Yeni modül…", () => NewModule(d), null);
                move.DropDown.BackColor = Theme.SurfaceHigh;
                _menu.Items.Add(move);
                _menu.Items.Add(new ToolStripSeparator());
                AddMenu(_menu.Items, "Sil…", DeleteCurrent, "Delete").ForeColor = Theme.Danger;
            }
            else if (r != null)
            {
                string key = r.ModuleKey;
                AddMenu(_menu.Items, key.Length > 0 ? "Bu modüle yeni komut" : "Yeni komut", () => StartNew(key), "Ctrl+Shift+N");
                if (key.Length > 0) AddMenu(_menu.Items, "Modülü düzenle…", () => EditModule(key), "F2");
                _menu.Items.Add(new ToolStripSeparator());
                AddMenu(_menu.Items, "Tümünü genişlet", () => SetAllCollapsed(false), null);
                AddMenu(_menu.Items, "Tümünü daralt", () => SetAllCollapsed(true), null);
                if (key.Length > 0)
                {
                    _menu.Items.Add(new ToolStripSeparator());
                    AddMenu(_menu.Items, "Modülü sil…", () => DeleteModule(key), "Delete").ForeColor = Theme.Danger;
                }
            }
            else
            {
                AddMenu(_menu.Items, "Yeni modül…", () => NewModule(), "Ctrl+Shift+M");
                AddMenu(_menu.Items, "Yeni komut", () => StartNew(""), "Ctrl+Shift+N");
            }
        }

        private static ToolStripMenuItem AddMenu(ToolStripItemCollection items, string text, Action action, string shortcut)
        {
            var item = new ToolStripMenuItem(text) { ForeColor = Theme.Text, ShortcutKeyDisplayString = shortcut };
            item.Click += (s, e) => action();
            items.Add(item);
            return item;
        }

        /// <summary>
        /// Modül başlığındaki ok (▸/▾) alanına tıklama seçimi değiştirmeden modülü açıp kapatır:
        /// tıklama yerel liste kutusuna hiç ulaşmaz (seçili komut ve açık sayfa değişmez).
        /// </summary>
        private sealed class TreeListBox : ListBox
        {
            public Func<int, int, bool> IsToggleArea;
            public event Action<int> ToggleClick;

            protected override void WndProc(ref Message m)
            {
                const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONDBLCLK = 0x0203;
                if ((m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK) && IsToggleArea != null)
                {
                    int lp = m.LParam.ToInt32();
                    var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                    int i = IndexFromPoint(pt);
                    if (i >= 0 && GetItemRectangle(i).Contains(pt) && IsToggleArea(i, pt.X))
                    {
                        if (!Focused) Focus();
                        var h = ToggleClick;
                        if (h != null) h(i);
                        return;
                    }
                }
                base.WndProc(ref m);
            }
        }

        // =====================================================================
        // Ortak
        // =====================================================================

        private void UpdateEmpty()
        {
            bool command = _current != null || _isNew;
            bool module = !command && _currentModuleKey != null;
            if (!command && !module)
            {
                _empty.Text = _library.Items.Count == 0 && _library.Modules.Count == 0
                    ? "Henüz komut yok.\n\nKomutları gruplamak için önce \"Yeni modül\" ile bir modül oluşturun\n(ör. \"Task komutları\", \"Bellek ve ağ\"),\nsonra \"Yeni komut\" ile modüle komut ekleyin."
                    : _list.Items.Count == 0 ? "Aramayla eşleşen komut ya da modül yok." : "Soldaki listeden bir komut ya da modül seçin.";
            }
            _empty.Visible = !command && !module;
            _form.Visible = command;
            _modulePage.Visible = module;
            if (_empty.Visible) _empty.BringToFront();
            else if (command) _form.BringToFront();
            else _modulePage.BringToFront();
        }

        private void OpenFolder()
        {
            if (ReadOnlyLibrary) return;   // gömülü kütüphane değiştirilemez
            string dir = Path.GetDirectoryName(_library.Path);
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("komut klasörü", ex);
                ShowBanner("⚠  Klasör açılamadı: " + dir, true);
            }
        }

        private void ShowBanner(string text, bool error)
        {
            _banner.Text = text;
            _tips.SetToolTip(_banner, "Kapatmak için tıklayın");
            _banner.BackColor = error ? Theme.ErrorBanner : Theme.SuccessBanner;
            _banner.ForeColor = error ? Color.White : Theme.SuccessBannerText;
            _banner.Visible = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tips.Dispose();
                _copiedTimer.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
