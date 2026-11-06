using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Kart ekranında renklendirme: terminalde bir kelime / metin seçip sağ tık → "Renklendir" → renk. Ekrandaki ve
    /// sonradan gelecek tüm eşleşmelerin zemini o renge boyanır (büyük/küçük harf duyarsız), böylece akan loglar arasında
    /// aranan metin (ör. "error") kolayca görülür. Kurallar karta aittir (CardSession.Highlights); kart penceresi
    /// kapatılıp açılsa da korunur. Seçim yoksa sağ tıklanan kelime seçilir.
    /// </summary>
    public sealed partial class CardView
    {
        private readonly ContextMenuStrip _outputMenu = new ContextMenuStrip();
        private readonly Dictionary<int, Bitmap> _swatches = new Dictionary<int, Bitmap>();

        // Yeni gelen parça bir kelimenin ortasından başlayabilir ("err" + "or"): satırın önceki kısmından en çok bu kadarı
        // yeniden taranır. Kural metni en fazla HighlightRules.MaxTextLength olduğundan yeterlidir.
        private const int HighlightLookBehind = HighlightRules.MaxTextLength + 8;

        // ---------- RichEdit karakter biçimi (zemin rengini "otomatik"e döndürmek için gerekir) ----------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CHARFORMAT2
        {
            public int cbSize;
            public uint dwMask;
            public uint dwEffects;
            public int yHeight;
            public int yOffset;
            public int crTextColor;
            public byte bCharSet;
            public byte bPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szFaceName;
            public short wWeight;
            public short sSpacing;
            public int crBackColor;
            public int lcid;
            public int dwReserved;
            public short sStyle;
            public short wKerning;
            public byte bUnderlineType;
            public byte bAnimation;
            public byte bRevAuthor;
            public byte bUnderlineColor;
        }

        private const int EM_SETCHARFORMAT = 0x0400 + 68;
        private const int SCF_SELECTION = 0x0001;
        private const int SCF_ALL = 0x0004;
        private const uint CFM_BACKCOLOR = 0x04000000, CFE_AUTOBACKCOLOR = 0x04000000;
        private const uint CFM_COLOR = 0x40000000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT2 lParam);

        /// <summary>Seçime (ya da tüm metne) yazı + zemin rengi verir; back == Empty: zemin otomatik (kutunun zemini).</summary>
        private void SetCharColors(bool all, Color text, Color back)
        {
            var cf = new CHARFORMAT2
            {
                cbSize = Marshal.SizeOf(typeof(CHARFORMAT2)),
                dwMask = CFM_COLOR | CFM_BACKCOLOR,
                crTextColor = ColorTranslator.ToWin32(text),
                szFaceName = "",
            };
            if (back.IsEmpty) cf.dwEffects = CFE_AUTOBACKCOLOR;
            else cf.crBackColor = ColorTranslator.ToWin32(back);
            SendMessage(_output.Handle, EM_SETCHARFORMAT, (IntPtr)(all ? SCF_ALL : SCF_SELECTION), ref cf);
        }

        // ---------- Oluşturma ----------

        private void BuildHighlightMenu()
        {
            _outputMenu.Renderer = new DarkMenuRenderer();
            _outputMenu.Opening += (s, e) => { BuildOutputMenu(); e.Cancel = _outputMenu.Items.Count == 0; };
            _output.ContextMenuStrip = _outputMenu;
            _output.MouseDown += Output_MouseDown;
        }

        /// <summary>Sağ tık seçimin dışındaysa tıklanan kelimeyi seçer (kelimeyi ayrıca seçmek gerekmez).</summary>
        private void Output_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || _output.TextLength == 0) return;
            int i = _output.GetCharIndexFromPosition(e.Location);
            int s = _output.SelectionStart, l = _output.SelectionLength;
            if (l > 0 && i >= s && i < s + l) return;          // seçimin üstünde: seçim korunur

            // Tıklanan kelimenin çevresini oku (tüm metni kopyalamadan): aynı satır, tıklamanın en çok 200 karakter öncesi / sonrası
            int line = _output.GetLineFromCharIndex(i);
            int lineStart = Math.Max(0, _output.GetFirstCharIndexFromLine(line));
            int next = _output.GetFirstCharIndexFromLine(line + 1);
            int lineEnd = next < 0 ? _output.TextLength : next - 1;   // satır sonu karakteri hariç
            int from = Math.Max(lineStart, i - 200), to = Math.Min(lineEnd, i + 200);
            if (i < from || i >= to) return;
            _output.Select(from, to - from);
            string text = _output.SelectedText;
            int at = i - from;
            if (text.Length != to - from || at >= text.Length || !IsWordChar(text[at]))
            {
                _output.Select(s, l);                           // kelime yok: önceki seçim geri gelir
                return;
            }
            int a = at, b = at;
            while (a > 0 && IsWordChar(text[a - 1])) a--;
            while (b < text.Length && IsWordChar(text[b])) b++;
            _output.Select(from + a, b - a);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
        }

        private void BuildOutputMenu()
        {
            _outputMenu.Items.Clear();
            HighlightRules rules = Session.Highlights;
            string selection = _output.SelectionLength > 0 ? _output.SelectedText : "";
            string text = HighlightRules.Normalize(selection);

            // Menü açıkken akış seçimi değiştirebilir: kopyalanacak metin menü açılırken alınır
            var copy = MakeMenuItem("Kopyala", () => { try { Clipboard.SetText(selection); } catch (Exception ex) { ErrorText.LogTechnical("kopyala", ex); } });
            copy.ShortcutKeyDisplayString = "Ctrl+C";
            copy.Enabled = selection.Length > 0;
            _outputMenu.Items.Add(copy);
            _outputMenu.Items.Add(new ToolStripSeparator());

            if (text == null)
            {
                var hint = MakeMenuItem(selection.Length > 0 ? "Renklendir  (tek satırlık bir metin seçin)" : "Renklendir  (önce bir kelime seçin)", null);
                hint.Enabled = false;
                _outputMenu.Items.Add(hint);
            }
            else
            {
                HighlightRule existing = rules.Find(text);
                var colorize = MakeMenuItem("Renklendir:  \"" + Shorten(text) + "\"", null);
                colorize.Image = existing != null ? Swatch(existing.Color) : null;
                foreach (var kv in HighlightPalette.Colors)
                {
                    Color c = kv.Value;
                    var it = MakeMenuItem(kv.Key, () => AddHighlight(text, c));
                    it.Image = Swatch(c);
                    it.Checked = existing != null && existing.Color.ToArgb() == c.ToArgb();
                    colorize.DropDownItems.Add(it);
                }
                colorize.DropDownItems.Add(new ToolStripSeparator());
                colorize.DropDownItems.Add(MakeMenuItem("Diğer renk…", () => PickCustomColor(text, existing)));
                StyleDropDown(colorize);
                _outputMenu.Items.Add(colorize);
                if (existing == null && rules.Count >= HighlightRules.MaxRules)
                {
                    colorize.Enabled = false;
                    colorize.Text += "  (en fazla " + HighlightRules.MaxRules + " renklendirme)";
                }
                if (existing != null)
                    _outputMenu.Items.Add(MakeMenuItem("Renklendirmeyi kaldır:  \"" + Shorten(text) + "\"", () => RemoveHighlight(text)));
            }

            if (rules.Count > 0)
            {
                var list = MakeMenuItem("Renklendirmeler (" + rules.Count + ")", null);
                foreach (HighlightRule r in rules.Items)
                {
                    string t = r.Text;
                    var it = MakeMenuItem("\"" + Shorten(t) + "\"  —  kaldır", () => RemoveHighlight(t));
                    it.Image = Swatch(r.Color);
                    it.ToolTipText = HighlightPalette.NameOf(r.Color);
                    list.DropDownItems.Add(it);
                }
                list.DropDownItems.Add(new ToolStripSeparator());
                var all = MakeMenuItem("Tüm renklendirmeleri kaldır", ClearHighlights);
                all.ForeColor = Theme.Danger;
                list.DropDownItems.Add(all);
                StyleDropDown(list);
                _outputMenu.Items.Add(list);
            }
        }

        private ToolStripMenuItem MakeMenuItem(string text, Action action)
        {
            var item = new ToolStripMenuItem(text) { ForeColor = Theme.Text };
            if (action != null) item.Click += (s, e) => action();
            return item;
        }

        private void StyleDropDown(ToolStripMenuItem item)
        {
            item.DropDown.Renderer = _outputMenu.Renderer;
            item.DropDown.BackColor = Theme.SurfaceHigh;
        }

        private static string Shorten(string s)
        {
            return s.Length <= 40 ? s : s.Substring(0, 37) + "…";
        }

        /// <summary>Menüde renk örneği (renk başına bir kez çizilir, kart kapanınca bırakılır).</summary>
        private Bitmap Swatch(Color c)
        {
            Bitmap bmp;
            if (_swatches.TryGetValue(c.ToArgb(), out bmp)) return bmp;
            bmp = new Bitmap(14, 14);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(c)) g.FillRectangle(b, 1, 1, 12, 12);
                using (var p = new Pen(Color.FromArgb(120, 0, 0, 0))) g.DrawRectangle(p, 1, 1, 11, 11);
            }
            _swatches[c.ToArgb()] = bmp;
            return bmp;
        }

        private void PickCustomColor(string text, HighlightRule existing)
        {
            using (var d = new ColorDialog { FullOpen = true, AnyColor = true })
            {
                d.Color = existing != null ? existing.Color : HighlightPalette.Colors[0].Value;
                if (d.ShowDialog(FindForm()) == DialogResult.OK) AddHighlight(text, d.Color);
            }
        }

        // ---------- Kurallar ----------

        private void AddHighlight(string text, Color color)
        {
            if (!Session.Highlights.Set(text, color))
            {
                ShowToast("⚠  Renklendirilemedi: en fazla " + HighlightRules.MaxRules + " renklendirme olabilir.", false, null);
                return;
            }
            RepaintHighlights();
        }

        private void RemoveHighlight(string text)
        {
            if (Session.Highlights.Remove(text)) RepaintHighlights();
        }

        private void ClearHighlights()
        {
            if (Session.Highlights.Count == 0) return;
            Session.Highlights.Clear();
            RepaintHighlights();
        }

        // ---------- Boyama ----------

        /// <summary>
        /// Renklendirme kurallarını uygular. appended verilirse yalnız yeni eklenen kısım (ve bölünmüş kelimeler için
        /// satırın hemen önceki kısmı) taranır; null ise tüm metin taranır. Seçim korunur.
        /// </summary>
        private void ApplyHighlights(int start, string appended)
        {
            HighlightRules rules = Session.Highlights;
            if (rules.Count == 0 || !_output.IsHandleCreated || _output.TextLength == 0) return;

            int selStart = _output.SelectionStart, selLen = _output.SelectionLength;
            string text;
            int offset;
            if (appended == null)
            {
                text = _output.Text;
                offset = 0;
            }
            else
            {
                // Parça bir kelimenin ortasından başlıyor olabilir: aynı satırın önceki birkaç karakterini de tara
                int lineStart = _output.GetFirstCharIndexFromLine(_output.GetLineFromCharIndex(start));
                int from = Math.Max(Math.Max(0, lineStart), start - HighlightLookBehind);
                string before = "";
                if (from < start)
                {
                    _output.Select(from, start - from);
                    before = _output.SelectedText;
                    if (before.Length != start - from) { before = ""; from = start; }   // beklenmedik: yalnız yeni kısmı tara
                }
                text = before + appended;
                offset = from;
            }

            List<HighlightMatch> matches = rules.Matches(text);
            if (matches.Count == 0) { if (appended != null) _output.Select(selStart, selLen); return; }
            foreach (HighlightMatch m in matches)
            {
                _output.Select(offset + m.Start, m.Length);
                SetCharColors(false, m.Rule.TextColor, m.Rule.Color);
            }
            _output.Select(selStart, selLen);
        }

        /// <summary>Kurallar değişti: tüm metnin renkleri sıfırlanır, uyarı satırları ve renklendirmeler yeniden boyanır.</summary>
        private void RepaintHighlights()
        {
            if (!_output.IsHandleCreated) return;               // tutamaç oluşunca HandleCreated hepsini boyar
            IntPtr h = _output.Handle;
            var pos = new POINT();
            SendMessage(h, EM_GETSCROLLPOS, IntPtr.Zero, ref pos);
            SendMessage(h, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                SetCharColors(true, _output.ForeColor, Color.Empty);   // tüm metin: tema yazı rengi, zemin otomatik
                ColorizeAlerts(0, null);
                ApplyHighlights(0, null);
            }
            finally
            {
                SendMessage(h, EM_SETSCROLLPOS, IntPtr.Zero, ref pos);
                SendMessage(h, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _output.Invalidate();
            }
        }

        private void DisposeHighlightResources()
        {
            _outputMenu.Dispose();
            foreach (Bitmap b in _swatches.Values) b.Dispose();
            _swatches.Clear();
        }
    }
}
