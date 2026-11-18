using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Olay günlüğü penceresi (G49): seçilen kartın olayları en yeniden eskiye. Tür süzgeci, arama, CSV olarak kaydetme ve
    /// günlük klasörünü açma. Pencere açıkken yeni olaylar kendiliğinden eklenir. Tek pencere açılır; ikinci istek onu öne
    /// getirip kartı seçer. Kart Gezgini'nde kartın sağ tık menüsünden ve terminalin sağ tık menüsünden açılır.
    /// </summary>
    public sealed class EventLogDialog : DarkDialog
    {
        /// <summary>Kart listesi (ana pencere atar). Seçicide bu kartlar gösterilir.</summary>
        public static Func<IList<CardConfig>> CardSource;

        private static EventLogDialog _open;

        private readonly DarkComboBox _cardCombo = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DarkComboBox _kindCombo = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _search = new TextBox();
        private readonly EventList _list = new EventList();
        private readonly Label _info = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Label _empty = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly TextBox _detail = new TextBox();
        private readonly Label _count = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Button _csv = Theme.MakeButton("CSV olarak kaydet…", Theme.ButtonKind.Secondary);
        private readonly Button _folder = Theme.MakeButton("Klasörü aç", Theme.ButtonKind.Secondary);
        private readonly Button _close = Theme.MakeButton("Kapat", Theme.ButtonKind.Primary);
        private readonly Timer _timer = new Timer { Interval = 2000 };
        private readonly ToolTip _tips = new DarkToolTip();

        private List<CardEvent> _all = new List<CardEvent>();
        private List<CardEvent> _shown = new List<CardEvent>();
        private string _key;
        private DateTime _stamp;
        private long _length = -1;

        private sealed class CardItem
        {
            public CardConfig Card;
            public override string ToString() { return Card.Label + (Card.Group != null && Card.Group.Port != 23 ? "  :" + Card.Group.Port : ""); }
        }

        private EventLogDialog()
            : base("Olay günlüğü", new Size(900, 560))
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(620, 380);
            MaximizeBox = true;
            ShowInTaskbar = false;
            KeyPreview = true;

            // --- Üst: kart, tür, arama
            var top = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Theme.Surface };
            var cardCap = Theme.MakeLabel("KART", Theme.SectionHeader, Theme.TextMuted);
            var kindCap = Theme.MakeLabel("TÜR", Theme.SectionHeader, Theme.TextMuted);
            var searchCap = Theme.MakeLabel("ARA", Theme.SectionHeader, Theme.TextMuted);
            cardCap.Location = new Point(16, 10);
            _cardCombo.SetBounds(16, 30, 260, 26);
            kindCap.Location = new Point(288, 10);
            _kindCombo.SetBounds(288, 30, 170, 26);
            searchCap.Location = new Point(470, 10);
            Theme.StyleTextBox(_search);
            _search.Location = new Point(470, 31);
            Theme.SetCueBanner(_search, "Olay metninde ara");
            _info.Location = new Point(16, 64);
            top.Controls.AddRange(new Control[] { cardCap, _cardCombo, kindCap, _kindCombo, searchCap, _search, _info });
            top.Resize += (s, e) => _search.Width = Math.Max(120, top.ClientSize.Width - 470 - 16);

            _kindCombo.Items.Add("Tümü");
            foreach (string c in CardEventLog.Categories) _kindCombo.Items.Add(c);
            _kindCombo.SelectedIndex = 0;

            // --- Orta: liste + seçili olayın tamamı
            var middle = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 4, 4, 4) };
            _list.Dock = DockStyle.Fill;
            _empty.AutoSize = false;
            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.BackColor = Theme.Input;
            _empty.Visible = false;
            var header = new HeaderRow { Dock = DockStyle.Top, Height = 24 };
            middle.Controls.Add(_empty);
            middle.Controls.Add(_list);
            middle.Controls.Add(header);
            Theme.ApplyDarkScrollBars(_list);

            var detailHost = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Theme.Surface, Padding = new Padding(16, 8, 16, 8) };
            _detail.Multiline = true;
            _detail.ReadOnly = true;
            _detail.BorderStyle = BorderStyle.None;
            _detail.BackColor = Theme.Surface;
            _detail.ForeColor = Theme.Text;
            _detail.Font = Theme.Ui;
            _detail.Dock = DockStyle.Fill;
            _detail.ScrollBars = ScrollBars.Vertical;
            detailHost.Controls.Add(_detail);

            // --- Alt
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            _count.Location = new Point(16, (FooterHeight - _count.PreferredHeight) / 2);
            _csv.Width = 160;
            _folder.Width = 110;
            _close.Width = 90;
            footer.Controls.AddRange(new Control[] { _count, _csv, _folder, _close });
            footer.Resize += (s, e) =>
            {
                int y = (FooterHeight - _close.Height) / 2;
                int x = footer.ClientSize.Width - 16;
                x -= _close.Width; _close.Location = new Point(x, y);
                x -= 8 + _folder.Width; _folder.Location = new Point(x, y);
                x -= 8 + _csv.Width; _csv.Location = new Point(x, y);
            };
            _tips.SetToolTip(_csv, "Şu an listelenen olayları (süzgeç ve arama uygulanmış) CSV dosyasına kaydeder");
            _tips.SetToolTip(_folder, "Olay günlüğü dosyalarının bulunduğu klasörü açar (logs\\olaylar)");

            Controls.Add(middle);
            Controls.Add(detailHost);
            Controls.Add(top);
            Controls.Add(footer);

            CancelButton = _close;
            _close.Click += (s, e) => Close();
            _csv.Click += (s, e) => SaveCsv();
            _folder.Click += (s, e) => OpenFolder();
            _cardCombo.SelectedIndexChanged += (s, e) => LoadCard();
            _kindCombo.SelectedIndexChanged += (s, e) => Filter();
            _search.TextChanged += (s, e) => Filter();
            _list.SelectedIndexChanged += (s, e) => ShowDetail();
            _timer.Tick += (s, e) => ReloadIfChanged();
            FormClosed += (s, e) => { _timer.Stop(); if (_open == this) _open = null; };
            _timer.Start();
        }

        /// <summary>Pencereyi açar (açıksa öne getirir) ve kartı seçer.</summary>
        public static void ShowFor(IWin32Window owner, CardConfig card)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new EventLogDialog();
                _open.FillCards(card);
                if (owner != null) _open.Show(owner); else _open.Show();
            }
            else
            {
                _open.FillCards(card);
                if (_open.WindowState == FormWindowState.Minimized) _open.WindowState = FormWindowState.Normal;
                _open.Activate();
            }
        }

        private void FillCards(CardConfig select)
        {
            IList<CardConfig> cards = CardSource != null ? CardSource() : null;
            var list = (cards ?? new List<CardConfig>()).ToList();
            if (select != null && !list.Contains(select)) list.Insert(0, select);
            _cardCombo.BeginUpdate();
            _cardCombo.Items.Clear();
            int index = 0;
            for (int i = 0; i < list.Count; i++)
            {
                _cardCombo.Items.Add(new CardItem { Card = list[i] });
                if (list[i] == select) index = i;
            }
            _cardCombo.EndUpdate();
            if (_cardCombo.Items.Count > 0)
            {
                if (_cardCombo.SelectedIndex == index) LoadCard();
                else _cardCombo.SelectedIndex = index;
            }
            else LoadCard();
        }

        private CardConfig SelectedCard
        {
            get { var it = _cardCombo.SelectedItem as CardItem; return it != null ? it.Card : null; }
        }

        private void LoadCard()
        {
            CardConfig card = SelectedCard;
            _key = card != null ? CardEventLog.KeyFor(card) : null;
            Text = "Olay günlüğü" + (card != null ? "  —  " + card.Label : "");
            _length = -1;
            ReloadIfChanged();
        }

        private void ReloadIfChanged()
        {
            if (_key == null) { _all = new List<CardEvent>(); Filter(); return; }
            var fi = new FileInfo(CardEventLog.PathFor(_key));
            long len = fi.Exists ? fi.Length : 0;
            DateTime stamp = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue;
            if (len == _length && stamp == _stamp) return;
            _length = len;
            _stamp = stamp;
            _all = CardEventLog.Read(_key);
            _info.Text = (_all.Count == 0 ? "Bu kart için henüz olay yok" : _all.Count + " olay  ·  " + _all[0].Time.ToString("dd.MM.yyyy HH:mm") + " — " +
                         _all[_all.Count - 1].Time.ToString("dd.MM.yyyy HH:mm")) +
                         "  ·  günlük " + DeviceSpace.FormatBytes(len) + " / " + DeviceSpace.FormatBytes(CardEventLog.MaxBytes) +
                         " (dolunca en eski olaylar silinir)";
            _info.Size = _info.PreferredSize;
            Filter();
        }

        private void Filter()
        {
            string kind = _kindCombo.SelectedIndex > 0 ? (string)_kindCombo.SelectedItem : null;
            string q = _search.Text.Trim();
            CardEvent keep = _list.SelectedIndex >= 0 && _list.SelectedIndex < _shown.Count ? _shown[_list.SelectedIndex] : null;
            _shown = _all.Where(e => (kind == null || e.Category == kind) &&
                                     (q.Length == 0 || (e.Message ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0
                                                    || (e.Category ?? "").IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0))
                         .Reverse().ToList();   // en yeni üstte
            _list.Query = q;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (CardEvent e in _shown) _list.Items.Add(e);
                int sel = keep != null ? _shown.IndexOf(keep) : -1;
                if (sel < 0 && _shown.Count > 0) sel = 0;
                if (sel >= 0) _list.SelectedIndex = sel;
            }
            finally
            {
                _list.EndUpdate();
            }
            _empty.Text = _all.Count == 0 ? "Bu kart için kayıtlı olay yok." : "Süzgece uyan olay yok.";
            _empty.Visible = _shown.Count == 0;
            _count.Text = _shown.Count == _all.Count ? _all.Count + " olay" : _shown.Count + " / " + _all.Count + " olay";
            _count.Size = _count.PreferredSize;
            _csv.Enabled = _shown.Count > 0;
            ShowDetail();
        }

        private void ShowDetail()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _shown.Count) { _detail.Text = ""; return; }
            CardEvent e = _shown[i];
            _detail.Text = e.Time.ToString("dd.MM.yyyy HH:mm:ss") + "  ·  " + e.Category + Environment.NewLine + e.Message;
        }

        private void SaveCsv()
        {
            using (var d = new SaveFileDialog())
            {
                d.Title = "Olay günlüğünü CSV olarak kaydet";
                d.Filter = "CSV dosyası (*.csv)|*.csv";
                d.FileName = "olaylar_" + (_key ?? "kart") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // Eskiden yeniye; Excel Türkçe karakterleri doğru açsın diye UTF-8 BOM'lu
                    File.WriteAllText(d.FileName, CardEventLog.ToCsv(Enumerable.Reverse(_shown)), new UTF8Encoding(true));
                    _info.Text = "✓  " + _shown.Count + " olay kaydedildi: " + d.FileName;
                    _info.Size = _info.PreferredSize;
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("olay günlüğü CSV", ex);
                    ChoiceDialog.Ask(this, "Kaydedilemedi", "CSV dosyası yazılamadı.", ErrorText.Describe(ex), new[] { "Tamam" }, 0, -1);
                }
            }
        }

        private void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(CardEventLog.Folder);
                string file = _key != null ? CardEventLog.PathFor(_key) : null;
                if (file != null && File.Exists(file)) Process.Start("explorer.exe", "/select,\"" + file + "\"");
                else Process.Start("explorer.exe", "\"" + CardEventLog.Folder + "\"");
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("olay günlüğü klasörü", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }

        // ---------- Liste ----------

        private const int TimeColumn = 150, KindColumn = 130;

        private sealed class HeaderRow : Control
        {
            public HeaderRow()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                BackColor = Theme.Input;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var b = new SolidBrush(Theme.Input)) e.Graphics.FillRectangle(b, ClientRectangle);
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(e.Graphics, "ZAMAN", Theme.SectionHeader, new Rectangle(8, 0, TimeColumn, Height), Theme.TextMuted, f);
                TextRenderer.DrawText(e.Graphics, "TÜR", Theme.SectionHeader, new Rectangle(8 + TimeColumn, 0, KindColumn, Height), Theme.TextMuted, f);
                TextRenderer.DrawText(e.Graphics, "OLAY", Theme.SectionHeader, new Rectangle(8 + TimeColumn + KindColumn, 0, Width, Height), Theme.TextMuted, f);
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }
        }

        private sealed class EventList : ListBox
        {
            public EventList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = Theme.Ui;
                ItemHeight = 26;
                BackColor = Theme.Input;
                ForeColor = Theme.Text;
            }

            public string Query { get; set; }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var ev = (CardEvent)Items[e.Index];
                Graphics g = e.Graphics;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color back = selected ? Theme.ListSelectedStrong : BackColor;
                Color fore = selected ? Theme.ListSelectedText : Theme.Text;
                Color muted = selected ? Theme.ListSelectedText : Theme.TextMuted;
                using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                          TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                int x = e.Bounds.X + 8;
                TextRenderer.DrawText(g, ev.Time.ToString("dd.MM.yyyy HH:mm:ss"), Font, new Rectangle(x, e.Bounds.Y, TimeColumn - 8, e.Bounds.Height), muted, f);
                x += TimeColumn;
                TextRenderer.DrawText(g, ev.Category, Font, new Rectangle(x, e.Bounds.Y, KindColumn - 8, e.Bounds.Height),
                                      selected ? fore : KindColor(ev.Category, ev.Message), f);
                x += KindColumn;
                TextRenderer.DrawText(g, ev.Message, Font, new Rectangle(x, e.Bounds.Y, e.Bounds.Right - x - 6, e.Bounds.Height),
                                      selected ? fore : IsFailure(ev.Message) ? Theme.Danger : fore, f);
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            private static bool IsFailure(string m)
            {
                m = m ?? "";
                return m.StartsWith("Başarısız") || m.StartsWith("Bağlantı koptu") || m.StartsWith("Bağlanılamadı") || m.Contains("tamamlanamadı");
            }

            // Pencere büyüyüp küçülünce "Olay" sütunu tüm satırlarda yeni genişliğe göre yeniden çizilsin
            protected override void OnClientSizeChanged(EventArgs e)
            {
                base.OnClientSizeChanged(e);
                Invalidate();
            }

            private static Color KindColor(string kind, string message)
            {
                if (IsFailure(message)) return Theme.Danger;
                if (kind == CardEventLog.Reboot) return Theme.Warning;
                if (kind == CardEventLog.Connection) return Theme.Accent;
                return Theme.TextMuted;
            }
        }
    }
}
