using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Komut geçmişi penceresi: daha önce yazılmış TÜM komutlar en yeniden eskiye listelenir. Üstteki arama kutusu
    /// geriye doğru arama (reverse search, Ctrl+R) yapar: yazdıkça yalnız metni içeren komutlar kalır, eşleşen kısım vurgulanır.
    /// Seçilen komut komut satırına yazılır (GÖNDERİLMEZ; kullanıcı düzenleyip Enter'a basar).
    ///   ↑/↓ seç · Enter / çift tık: komut satırına al · Ctrl+R: sonraki (daha eski) eşleşme · Shift+Del (listede Del): geçmişten sil · Esc: kapat
    /// </summary>
    public sealed class CommandHistoryDialog : DarkDialog
    {
        private readonly CommandHistory _history;
        private readonly TextBox _search = new TextBox();
        private readonly HistoryList _list = new HistoryList();
        private readonly Label _count = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Label _empty = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Button _accept = Theme.MakeButton("Komut satırına al", Theme.ButtonKind.Primary);
        private readonly Button _close = Theme.MakeButton("Kapat", Theme.ButtonKind.Secondary);
        private readonly Button _delete = Theme.MakeButton("Sil", Theme.ButtonKind.Secondary);
        private readonly Button _deleteAll = Theme.MakeButton("Tümünü sil…", Theme.ButtonKind.Danger);
        private readonly ToolTip _tips = new DarkToolTip();

        public CommandHistoryDialog(CommandHistory history, string initialQuery, string target)
            : base("Komut geçmişi" + (string.IsNullOrEmpty(target) ? "" : "  —  " + target), new Size(640, 500))
        {
            _history = history;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(460, 340);
            KeyPreview = true;

            // --- Üst: arama
            var top = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Surface, Padding = new Padding(16, 14, 16, 0) };
            var caption = Theme.MakeLabel("GERİYE DOĞRU ARA (Ctrl+R)", Theme.SectionHeader, Theme.TextMuted);
            caption.Location = new Point(16, 12);
            Theme.StyleTextBox(_search);
            _search.Font = new Font("Consolas", 10.5f);
            _search.Location = new Point(16, 32);
            _search.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Theme.SetCueBanner(_search, "Aramak için yazın — komutun herhangi bir yerinde geçen metin");
            var hint = Theme.MakeLabel("↑/↓ seç  ·  Enter: komut satırına al (gönderilmez)  ·  Ctrl+R: sonraki eşleşme  ·  Shift+Del: geçmişten sil",
                                       Theme.UiSmall, Theme.TextFaint);
            hint.Location = new Point(16, 62);
            top.Controls.AddRange(new Control[] { caption, _search, hint });
            top.Resize += (s, e) => _search.Width = Math.Max(100, top.ClientSize.Width - 32);

            // --- Orta: liste
            var middle = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 6, 4, 6) };
            _list.Dock = DockStyle.Fill;
            _empty.AutoSize = false;
            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.BackColor = Theme.Input;
            _empty.Visible = false;
            middle.Controls.Add(_empty);
            middle.Controls.Add(_list);
            Theme.ApplyDarkScrollBars(_list);

            // --- Alt: sayaç + düğmeler
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            _count.Location = new Point(16, (FooterHeight - _count.PreferredHeight) / 2);
            _accept.Width = 150;
            _close.Width = 90;
            _delete.Width = 80;
            _deleteAll.Width = 110;
            footer.Controls.AddRange(new Control[] { _count, _deleteAll, _delete, _close, _accept });
            footer.Resize += (s, e) =>
            {
                int y = (FooterHeight - _accept.Height) / 2;
                int x = footer.ClientSize.Width - 16;
                x -= _accept.Width; _accept.Location = new Point(x, y);
                x -= 8 + _close.Width; _close.Location = new Point(x, y);
                x -= 8 + _delete.Width; _delete.Location = new Point(x, y);
                x -= 8 + _deleteAll.Width; _deleteAll.Location = new Point(x, y);
                _deleteAll.Visible = _deleteAll.Left > _count.Right + 8;
            };
            _tips.SetToolTip(_delete, "Seçili komutu geçmişten siler (Del)");
            _tips.SetToolTip(_deleteAll, "Tüm komut geçmişini siler (tüm kartlar için ortaktır)");

            // Yerleşim sırası: Fill en son yerleşsin diye önce eklenir
            Controls.Add(middle);
            Controls.Add(top);
            Controls.Add(footer);

            AcceptButton = _accept;
            CancelButton = _close;
            _close.DialogResult = DialogResult.Cancel;
            _accept.Click += (s, e) => AcceptSelected();
            _delete.Click += (s, e) => DeleteSelected();
            _deleteAll.Click += (s, e) => DeleteAll();
            _list.DoubleClick += (s, e) => AcceptSelected();
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            _list.KeyDown += List_KeyDown;
            _list.KeyPress += (s, e) =>
            {
                // Listedeyken yazmaya başlamak aramaya yazar
                if (char.IsControl(e.KeyChar)) return;
                e.Handled = true;
                _search.Focus();
                _search.SelectedText = e.KeyChar.ToString();
            };
            _search.KeyDown += Search_KeyDown;
            _search.TextChanged += (s, e) => Filter();

            _search.Text = initialQuery ?? "";
            Filter();
            Shown += (s, e) => { _search.Focus(); _search.SelectionStart = _search.TextLength; };
        }

        /// <summary>Seçilen komut (Komut satırına al); kapatıldıysa null.</summary>
        public string Chosen { get; private set; }

        /// <summary>Pencereyi açar; seçilen komutu (yoksa null) döndürür.</summary>
        public static string Pick(IWin32Window owner, CommandHistory history, string initialQuery, string target)
        {
            using (var d = new CommandHistoryDialog(history, initialQuery, target))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Chosen : null;
        }

        private void Filter()
        {
            string q = _search.Text;
            List<string> matches = _history.Search(q);
            _list.Query = q;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (string c in matches) _list.Items.Add(c);
                if (_list.Items.Count > 0) _list.SelectedIndex = 0;   // en yeni eşleşme
            }
            finally
            {
                _list.EndUpdate();
            }
            _list.Invalidate();

            bool any = _list.Items.Count > 0;
            _empty.Text = _history.Count == 0 ? "Henüz komut geçmişi yok.\nKart komut satırına yazdığınız komutlar burada listelenir."
                                              : "\"" + q + "\" içeren komut bulunamadı.";
            _empty.Visible = !any;
            _list.Visible = any;
            _count.Text = q.Length == 0 ? _history.Count + " komut  ·  en yeni üstte"
                                        : _list.Items.Count + " eşleşme  /  " + _history.Count + " komut";
            _count.Size = _count.PreferredSize;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool sel = _list.SelectedIndex >= 0 && _list.Visible;
            _accept.Enabled = sel;
            _delete.Enabled = sel;
            _deleteAll.Enabled = _history.Count > 0;
        }

        private void MoveSelection(int delta)
        {
            int n = _list.Items.Count;
            if (n == 0) return;
            int i = _list.SelectedIndex < 0 ? 0 : _list.SelectedIndex + delta;
            _list.SelectedIndex = Math.Max(0, Math.Min(n - 1, i));
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            int page = Math.Max(1, _list.ClientSize.Height / _list.ItemHeight - 1);
            switch (e.KeyCode)
            {
                case Keys.Down: MoveSelection(1); break;
                case Keys.Up: MoveSelection(-1); break;
                case Keys.PageDown: MoveSelection(page); break;
                case Keys.PageUp: MoveSelection(-page); break;
                case Keys.R:
                    if (!e.Control) return;
                    MoveSelection(e.Shift ? -1 : 1);       // Ctrl+R: daha eski eşleşme · Ctrl+Shift+R: daha yeni
                    break;
                case Keys.Delete:
                    if (!e.Shift || _list.SelectedIndex < 0) return;   // Shift+Del aramadan da siler; düz Del metni siler
                    DeleteSelected();
                    break;
                default: return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.R && e.Control) { MoveSelection(e.Shift ? -1 : 1); e.Handled = true; e.SuppressKeyPress = true; }
        }

        private void AcceptSelected()
        {
            if (_list.SelectedIndex < 0 || !_list.Visible) return;
            Chosen = (string)_list.SelectedItem;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0) return;
            _history.Remove((string)_list.Items[i]);
            Filter();
            if (_list.Items.Count > 0) _list.SelectedIndex = Math.Min(i, _list.Items.Count - 1);
        }

        private void DeleteAll()
        {
            int c = ChoiceDialog.Ask(this, "Komut geçmişini sil",
                "Tüm komut geçmişi (" + _history.Count + " komut) silinsin mi?",
                "Geçmiş tüm kartlar ve genel komut çubuğu için ortaktır; otomatik tamamlama önerileri de silinir. Geri alınamaz.",
                new[] { "Vazgeç", "Tümünü sil" }, 0, 1);
            if (c != 1) return;
            _history.Clear();
            Filter();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Komut listesi: eşleşen kısım vurgulu çizilir.</summary>
        private sealed class HistoryList : ListBox
        {
            private readonly Font _font = new Font("Consolas", 10f);

            public HistoryList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = _font;          // önce yazı tipi: ListBox yazı tipi değişince satır yüksekliğini sıfırlar
                ItemHeight = 26;
                BackColor = Theme.Input;
                ForeColor = Theme.Text;
            }

            public string Query { get; set; }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                Graphics g = e.Graphics;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color back = selected ? Theme.ListSelectedStrong : BackColor;
                Color fore = selected ? Theme.ListSelectedText : Theme.Text;
                using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);

                string text = (string)Items[e.Index];
                const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
                var r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                string q = Query ?? "";
                int at = q.Length == 0 ? -1 : text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    TextRenderer.DrawText(g, text, _font, r, fore, flags | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    // önce · eşleşme (vurgulu zemin) · sonra
                    string pre = text.Substring(0, at), hit = text.Substring(at, q.Length), post = text.Substring(at + q.Length);
                    int x = r.X;
                    int preW = Measure(g, pre);
                    if (pre.Length > 0) TextRenderer.DrawText(g, pre, _font, new Rectangle(x, r.Y, r.Right - x, r.Height), fore, flags);
                    x += preW;
                    int hitW = Measure(g, hit);
                    if (x < r.Right)
                    {
                        var hr = new Rectangle(x, r.Y + 4, Math.Min(hitW, r.Right - x), r.Height - 8);
                        using (var hb = new SolidBrush(Theme.Warning)) g.FillRectangle(hb, hr);
                        TextRenderer.DrawText(g, hit, _font, new Rectangle(x, r.Y, r.Right - x, r.Height), Color.Black, flags);
                    }
                    x += hitW;
                    if (x < r.Right && post.Length > 0)
                        TextRenderer.DrawText(g, post, _font, new Rectangle(x, r.Y, r.Right - x, r.Height), fore, flags | TextFormatFlags.EndEllipsis);
                }
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            private int Measure(Graphics g, string s)
            {
                if (s.Length == 0) return 0;
                return TextRenderer.MeasureText(g, s, _font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _font.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
