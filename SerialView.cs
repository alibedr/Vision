using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// "Seri" sekmesi (DENEME): solda bilgisayarda görünen COM portları (adlarıyla), sağda telnet'teki gibi en fazla
    /// 12 pencerelik ızgara. Porta çift tıklamak (ya da "Aç") pencereyi açıp portu açar; pencereler büyütülebilir.
    /// Telnet altyapısından bağımsızdır.
    /// </summary>
    public sealed class SerialView : UserControl
    {
        public const int MaxViews = 12;
        private const int Gap = 10;
        private const int MinViewWidth = 340, MinViewHeight = 220;

        private readonly Func<List<SerialPortProfile>> _profiles;
        private readonly Action _save;

        private readonly Panel _left = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = Theme.Sidebar };
        private readonly PortList _list = new PortList();
        private readonly Button _openBtn = Theme.MakeButton("Aç", Theme.ButtonKind.Primary);
        private readonly Button _settingsBtn = Theme.MakeButton("Ayarlar…", Theme.ButtonKind.Secondary);
        private readonly Label _listHint = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Panel _grid = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(Gap), AutoScroll = true };
        private readonly Label _empty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextFaint,
            Font = new Font("Segoe UI", 10.5f),
            Text = "Açık seri port yok\n\nSoldaki listeden bir porta çift tıklayın ya da seçip \"Aç\"a basın.\n" +
                   "Port ayarları (baud rate vb.) kartın seri ayarlarıyla aynı olmalı: \"Ayarlar…\".\n\n" +
                   "Açılan pencerede terminale tıklayıp yazın; her tuş anında karta gider.",
        };
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly ToolTip _tips = new DarkToolTip();
        private readonly Timer _timer = new Timer { Interval = 3000 };

        private readonly Dictionary<string, SerialSession> _sessions = new Dictionary<string, SerialSession>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SerialTerminalView> _views = new List<SerialTerminalView>();
        private SerialTerminalView _maximized, _selectedView;
        private List<SerialPortInfo> _ports = new List<SerialPortInfo>();
        private string _portKey = "";
        private bool _listing;

        public SerialView(Func<List<SerialPortProfile>> profiles, Action save)
        {
            _profiles = profiles;
            _save = save;
            BackColor = Theme.Window;

            // --- Sol: portlar
            var caption = Theme.MakeLabel("SERİ PORTLAR", Theme.SectionHeader, Theme.TextMuted);
            caption.Location = new Point(14, 12);
            caption.BackColor = Color.Transparent;
            var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(8, 34, 8, 0) };
            _list.Dock = DockStyle.Fill;
            listHost.Controls.Add(_list);
            listHost.Controls.Add(caption);
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = Theme.Sidebar };
            _openBtn.SetBounds(10, 8, 120, 32);
            _settingsBtn.SetBounds(138, 8, 120, 32);
            _listHint.AutoSize = false;
            _listHint.SetBounds(12, 46, 246, 54);
            _listHint.Text = "Çift tık: pencereyi aç ve bağlan. Port numarası değişebileceği için porta \"Ayarlar…\"dan ad verin.";
            buttons.Controls.AddRange(new Control[] { _openBtn, _settingsBtn, _listHint });
            buttons.Resize += (s, e) =>
            {
                int w = (buttons.ClientSize.Width - 28) / 2;
                _openBtn.SetBounds(10, 8, w, 32);
                _settingsBtn.SetBounds(18 + w, 8, w, 32);
                _listHint.Width = buttons.ClientSize.Width - 24;
            };
            _left.Controls.Add(listHost);
            _left.Controls.Add(buttons);
            _left.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, _left.Width - 1, 0, _left.Width - 1, _left.Height); };
            _tips.SetToolTip(_openBtn, "Seçili portun penceresini açar ve portu açar (en fazla " + MaxViews + " pencere)");
            _tips.SetToolTip(_settingsBtn, "Port ayarları: ad, baud rate, data, parity, stop, akış denetimi, gecikme");

            _grid.Controls.Add(_empty);
            _grid.Layout += (s, e) => LayoutViews();

            Controls.Add(_grid);
            Controls.Add(_left);

            _menu.Renderer = new DarkMenuRenderer();
            var mOpen = new ToolStripMenuItem("Aç ve bağlan", null, (s, e) => OpenSelected());
            var mClose = new ToolStripMenuItem("Kapat", null, (s, e) => CloseSelectedPort());
            var mSettings = new ToolStripMenuItem("Ayarlar…", null, (s, e) => EditSettings(SelectedPort));
            _menu.Items.AddRange(new ToolStripItem[] { mOpen, mClose, new ToolStripSeparator(), mSettings });
            _menu.Opening += (s, e) =>
            {
                string p = SelectedPort;
                if (p == null) { e.Cancel = true; return; }
                SerialSession ss;
                bool open = _sessions.TryGetValue(p, out ss) && ss.State == SerialState.Open;
                mOpen.Enabled = !open;
                mClose.Enabled = open;
            };
            _list.ContextMenuStrip = _menu;
            _list.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int i = _list.IndexFromPoint(e.Location);
                if (i >= 0) _list.SelectedIndex = i;
            };
            _list.DoubleClick += (s, e) => OpenSelected();
            _list.SelectedIndexChanged += (s, e) => RefreshButtons();
            _openBtn.Click += (s, e) => OpenSelected();
            _settingsBtn.Click += (s, e) => EditSettings(SelectedPort);
            _timer.Tick += (s, e) => RefreshPorts(false);
            VisibleChanged += (s, e) => { if (Visible) RefreshPorts(true); };
            RefreshButtons();
        }

        /// <summary>Açık port sayısı (durum çubuğu için).</summary>
        public int OpenCount { get { return _sessions.Values.Count(s => s.State == SerialState.Open); } }

        private string SelectedPort
        {
            get { var it = _list.SelectedItem as PortList.Item; return it != null ? it.PortName : null; }
        }

        private SerialPortProfile ProfileOf(string port)
        {
            List<SerialPortProfile> list = _profiles();
            return list.FirstOrDefault(p => string.Equals(p.PortName, port, StringComparison.OrdinalIgnoreCase));
        }

        // ---------- Port listesi ----------

        private void RefreshPorts(bool force)
        {
            if (_listing) return;
            if (!_timer.Enabled) _timer.Start();
            string[] names = SerialPorts.Names();
            string key = string.Join(",", names);
            // Portlar değişmediyse listeye dokunulmaz (her 3 sn'de yeniden çizmek listeyi titretiyordu)
            if (!force && key == _portKey) return;
            _portKey = key;
            _listing = true;
            Task.Run(() => SerialPorts.List()).ContinueWith(t =>
            {
                _listing = false;
                if (IsDisposed) return;
                if (t.Status == TaskStatus.RanToCompletion) _ports = t.Result;
                FillList();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void FillList()
        {
            string keep = SelectedPort;
            var items = new List<PortList.Item>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SerialPortInfo p in _ports) { names.Add(p.PortName); items.Add(new PortList.Item { PortName = p.PortName, Description = p.Description, Present = true }); }
            // Kayıtlı ama şu an takılı olmayan portlar (adı verilmişse) da listede: "takılı değil"
            foreach (SerialPortProfile pr in _profiles())
                if (!names.Contains(pr.PortName) && !string.IsNullOrWhiteSpace(pr.Name))
                    items.Add(new PortList.Item { PortName = pr.PortName, Description = pr.PortName, Present = false });
            items.Sort((a, b) => SerialPorts.PortNumber(a.PortName).CompareTo(SerialPorts.PortNumber(b.PortName)));
            _list.Resolve = port => ProfileOf(port);
            _list.SessionOf = port => { SerialSession s; return _sessions.TryGetValue(port, out s) ? s : null; };
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var it in items) _list.Items.Add(it);
            _list.EndUpdate();
            int i = items.FindIndex(x => string.Equals(x.PortName, keep, StringComparison.OrdinalIgnoreCase));
            if (i < 0 && items.Count > 0) i = 0;
            if (i >= 0) _list.SelectedIndex = i;
            _list.EmptyText = items.Count == 0 ? "Bilgisayarda seri port görünmüyor.\nUSB-seri çeviriciyi takın." : null;
            _list.Invalidate();
            RefreshButtons();
        }

        private void RedrawList() { _list.Invalidate(); RefreshButtons(); }

        private void RefreshButtons()
        {
            string p = SelectedPort;
            var it = _list.SelectedItem as PortList.Item;
            _openBtn.Enabled = p != null && it != null && it.Present;
            _settingsBtn.Enabled = p != null || _ports.Count > 0;
        }

        // ---------- Açma / kapama ----------

        private void OpenSelected()
        {
            string port = SelectedPort;
            var it = _list.SelectedItem as PortList.Item;
            if (port == null || it == null || !it.Present) return;
            OpenPort(port);
        }

        public void OpenPort(string port)
        {
            SerialSession session;
            if (!_sessions.TryGetValue(port, out session))
            {
                if (_views.Count >= MaxViews)
                {
                    ChoiceDialog.Ask(FindForm(), "En fazla " + MaxViews + " pencere", "Aynı anda en fazla " + MaxViews + " seri port penceresi açılabilir.",
                                     "Yeni bir port açmak için önce bir pencereyi kapatın.", new[] { "Tamam" }, 0, -1);
                    return;
                }
                SerialPortProfile pr = ProfileOf(port);
                if (pr == null)
                {
                    pr = new SerialPortProfile { PortName = port };
                    _profiles().Add(pr);
                    _save();
                }
                session = new SerialSession(pr);
                session.LineEntered += line => SerialHistoryStore.History.Add(line);
                string key = port;
                session.StateChanged += () =>
                {
                    if (IsHandleCreated) try { BeginInvoke(new Action(() => { _list.InvalidatePort(key); RefreshButtons(); })); } catch { }
                };
                _sessions[port] = session;
                CreateView(session);
            }
            SerialTerminalView view = _views.First(v => v.Session == session);
            if (_maximized != null && _maximized != view) SetMaximized(null);
            Select(view);
            if (session.State != SerialState.Open) session.Open();
            view.FocusTerminal();
            RedrawList();
        }

        private void CloseSelectedPort()
        {
            string p = SelectedPort;
            SerialSession s;
            if (p != null && _sessions.TryGetValue(p, out s)) s.Close();
        }

        private void CreateView(SerialSession session)
        {
            var v = new SerialTerminalView(session);
            v.CloseRequested += CloseView;
            v.MaximizeToggled += x => SetMaximized(_maximized == x ? null : x);
            v.Activated += Select;
            v.SettingsRequested += x => EditSettings(x.Session.Profile.PortName);
            _views.Add(v);
            _grid.Controls.Add(v);
            _empty.Visible = false;
            _grid.PerformLayout();
        }

        private void CloseView(SerialTerminalView v)
        {
            _views.Remove(v);
            if (_maximized == v) _maximized = null;
            if (_selectedView == v) _selectedView = null;
            _grid.Controls.Remove(v);
            _sessions.Remove(v.Session.Profile.PortName);
            v.Session.Dispose();          // pencereyi kapatmak portu da kapatır
            v.Dispose();
            _empty.Visible = _views.Count == 0;
            _grid.PerformLayout();
            RedrawList();
        }

        private void Select(SerialTerminalView v)
        {
            if (_selectedView == v) return;
            if (_selectedView != null) _selectedView.Selected = false;
            _selectedView = v;
            if (v != null) v.Selected = true;
        }

        private void SetMaximized(SerialTerminalView v)
        {
            if (_maximized != null) _maximized.IsMaximized = false;
            _maximized = v;
            if (v != null) v.IsMaximized = true;
            foreach (var x in _views) x.Visible = v == null || x == v;
            _grid.PerformLayout();
            if (v != null) v.FocusTerminal();
        }

        private void LayoutViews()
        {
            if (_views.Count == 0) { _grid.AutoScrollMinSize = Size.Empty; return; }
            Point scroll = _grid.AutoScrollPosition;
            Rectangle area = new Rectangle(_grid.Padding.Left, _grid.Padding.Top,
                Math.Max(0, _grid.ClientSize.Width - _grid.Padding.Horizontal), Math.Max(0, _grid.ClientSize.Height - _grid.Padding.Vertical));
            if (_maximized != null)
            {
                _grid.AutoScrollMinSize = Size.Empty;
                _maximized.Bounds = area;
                return;
            }
            int n = _views.Count;
            int cols = CardGrid.BestColumnCount(n, area.Width, area.Height);
            int rows = (n + cols - 1) / cols;
            int cellW = Math.Max(MinViewWidth, (area.Width - Gap * (cols - 1)) / cols);
            int cellH = Math.Max(MinViewHeight, (area.Height - Gap * (rows - 1)) / rows);
            int totalW = cellW * cols + Gap * (cols - 1), totalH = cellH * rows + Gap * (rows - 1);
            _grid.AutoScrollMinSize = (totalW > area.Width || totalH > area.Height)
                ? new Size(totalW + _grid.Padding.Horizontal, totalH + _grid.Padding.Vertical) : Size.Empty;
            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = r == rows - 1 ? n - r * cols : cols;
                int w = inRow == cols ? cellW : Math.Max(MinViewWidth, (Math.Max(totalW, area.Width) - Gap * (inRow - 1)) / inRow);
                _views[i].Visible = true;
                _views[i].Bounds = new Rectangle(area.Left + c * (w + Gap) + scroll.X, area.Top + r * (cellH + Gap) + scroll.Y, w, cellH);
            }
        }

        // ---------- Ayarlar ----------

        private void EditSettings(string port)
        {
            var locked = new HashSet<string>(_sessions.Where(kv => kv.Value.State == SerialState.Open).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
            List<SerialPortProfile> changed = SerialSettingsDialog.Edit(FindForm(), _ports, port ?? (_ports.Count > 0 ? _ports[0].PortName : null),
                                                                       ProfileOf, locked);
            if (changed == null) return;
            List<SerialPortProfile> all = _profiles();
            foreach (SerialPortProfile p in changed)
            {
                int i = all.FindIndex(x => string.Equals(x.PortName, p.PortName, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) all[i] = p; else all.Add(p);
                SerialSession s;
                if (_sessions.TryGetValue(p.PortName, out s)) s.UpdateProfile(p);
            }
            _save();
            foreach (var v in _views) v.RefreshState();
            FillList();
        }

        /// <summary>Uygulama kapanırken açık portları kapatır.</summary>
        public void CloseAll()
        {
            foreach (var s in _sessions.Values.ToList()) s.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _tips.Dispose(); CloseAll(); }
            base.Dispose(disposing);
        }

        // =====================================================================

        /// <summary>Port listesi: ad (ya da port), port · Windows adı · ayar özeti, durum noktası.</summary>
        private sealed class PortList : ListBox
        {
            public sealed class Item
            {
                public string PortName;
                public string Description;
                public bool Present;
            }

            private readonly Font _bold = new Font("Segoe UI Semibold", 9.75f);
            public Func<string, SerialPortProfile> Resolve;
            public Func<string, SerialSession> SessionOf;
            public string EmptyText;

            public PortList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                ItemHeight = 48;
                BackColor = Theme.Sidebar;
                ForeColor = Theme.Text;
                Font = Theme.Ui;
            }

            private const int WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014;

            protected override void WndProc(ref Message m)
            {
                // Arka plan silinmez (her çizimde önce boşaltılıp sonra çizilmesi titreme yapıyordu): satırlar kendi
                // arka planını çizer, satırların altındaki boş alan WM_PAINT'ten sonra boyanır.
                if (m.Msg == WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
                base.WndProc(ref m);
                if (m.Msg != WM_PAINT) return;
                using (Graphics g = CreateGraphics())
                {
                    int bottom = Items.Count == 0 ? 0 : GetItemRectangle(Items.Count - 1).Bottom;
                    if (bottom < ClientSize.Height)
                        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, 0, Math.Max(0, bottom), ClientSize.Width, ClientSize.Height - bottom);
                    if (Items.Count == 0 && !string.IsNullOrEmpty(EmptyText))
                        TextRenderer.DrawText(g, EmptyText, Theme.UiSmall, new Rectangle(4, 8, ClientSize.Width - 8, 60), Theme.TextMuted,
                                              TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                }
            }

            /// <summary>Yalnız değişen satırı yeniden çizer (tüm listeyi değil).</summary>
            public void InvalidatePort(string port)
            {
                for (int i = 0; i < Items.Count; i++)
                    if (string.Equals(((Item)Items[i]).PortName, port, StringComparison.OrdinalIgnoreCase)) { Invalidate(GetItemRectangle(i)); return; }
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var it = (Item)Items[e.Index];
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Graphics g = e.Graphics;
                using (var b = new SolidBrush(selected ? Theme.ListSelectedStrong : BackColor)) g.FillRectangle(b, e.Bounds);
                SerialPortProfile pr = Resolve != null ? Resolve(it.PortName) : null;
                SerialSession s = SessionOf != null ? SessionOf(it.PortName) : null;
                SerialState st = s != null ? s.State : SerialState.Closed;
                Color dot = !it.Present ? Theme.TextFaint : st == SerialState.Open ? Theme.Success : st == SerialState.Error ? Theme.Danger : Theme.TextMuted;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(dot)) g.FillEllipse(b, e.Bounds.X + 8, e.Bounds.Y + 11, 9, 9);
                Color fore = selected ? Theme.ListSelectedText : it.Present ? Theme.Text : Theme.TextMuted;
                Color muted = selected ? Theme.ListSelectedText : Theme.TextMuted;
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                string title = pr != null && !string.IsNullOrWhiteSpace(pr.Name) ? pr.Name + "  (" + it.PortName + ")" : it.PortName;
                TextRenderer.DrawText(g, title, _bold, new Rectangle(e.Bounds.X + 24, e.Bounds.Y + 6, e.Bounds.Width - 30, 20), fore, f);
                string state = !it.Present ? "takılı değil" : st == SerialState.Open ? "açık" : st == SerialState.Error ? "hata" : "kapalı";
                string desc = it.Description != it.PortName ? it.Description.Replace("(" + it.PortName + ")", "").Trim() + "  ·  " : "";
                string sub = desc + (pr != null ? pr.Summary : new SerialPortProfile().Summary) + "  ·  " + state;
                TextRenderer.DrawText(g, sub, Theme.UiSmall, new Rectangle(e.Bounds.X + 24, e.Bounds.Y + 26, e.Bounds.Width - 30, 16), muted, f);
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); Invalidate(); }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _bold.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
