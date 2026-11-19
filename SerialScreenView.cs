using System;
using System.Drawing;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Seri tuş kodları (1.10): günlük görünümü ve Ekran görünümü aynı eşlemeyi kullanır. Kodlar TeraTerm'in varsayılan
    /// ayarlarıyla aynıdır (Backspace = BS, Delete = DEL, F1–F4 = ESC O P…S, F5–F12 = ESC [ n ~, Home / End / PgUp / PgDn /
    /// Insert = ESC [ 1 / 4 / 5 / 6 / 2 ~). Kart "uygulama oku tuşları" isterse (DECCKM) oklar ESC O A…D olur.
    /// </summary>
    internal static class SerialKeys
    {
        public static string Sequence(Keys k, bool control, bool alt, bool appCursor)
        {
            if (alt) return null;
            switch (k)
            {
                case Keys.Enter: return "\r";
                case Keys.Back: return "\b";
                case Keys.Tab: return control ? null : "\t";
                case Keys.Escape: return "\u001b";
                case Keys.Up: return appCursor ? "\u001bOA" : "\u001b[A";
                case Keys.Down: return appCursor ? "\u001bOB" : "\u001b[B";
                case Keys.Right: return appCursor ? "\u001bOC" : "\u001b[C";
                case Keys.Left: return appCursor ? "\u001bOD" : "\u001b[D";
                case Keys.Delete: return "\u007f";
            }
            if (!control)
            {
                string f = FunctionKey(k);
                if (f != null) return f;
            }
            if (control && k >= Keys.A && k <= Keys.Z) return ((char)(k - Keys.A + 1)).ToString();   // Ctrl+C = 0x03 …
            return null;
        }

        public static string FunctionKey(Keys k)
        {
            switch (k)
            {
                case Keys.F1: return "\u001bOP";
                case Keys.F2: return "\u001bOQ";
                case Keys.F3: return "\u001bOR";
                case Keys.F4: return "\u001bOS";
                case Keys.F5: return "\u001b[15~";
                case Keys.F6: return "\u001b[17~";
                case Keys.F7: return "\u001b[18~";
                case Keys.F8: return "\u001b[19~";
                case Keys.F9: return "\u001b[20~";
                case Keys.F10: return "\u001b[21~";
                case Keys.F11: return "\u001b[23~";
                case Keys.F12: return "\u001b[24~";
                case Keys.Home: return "\u001b[1~";
                case Keys.Insert: return "\u001b[2~";
                case Keys.End: return "\u001b[4~";
                case Keys.PageUp: return "\u001b[5~";
                case Keys.PageDown: return "\u001b[6~";
                default: return null;
            }
        }

        public static bool IsTerminalKey(Keys k)
        {
            return k == Keys.Tab || k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.Escape || k == Keys.Enter;
        }
    }

    /// <summary>
    /// Seri penceresinin Ekran görünümü (1.10): <see cref="VtScreen"/>'i TeraTerm'deki gibi 80×25'lik bir terminal olarak
    /// çizer (BIOS / boot manager menüleri: renkler, ters renk, çerçeveler, imleç). Yazı boyutu ızgara alana sığacak kadar
    /// ayarlanır. Tuşlar doğrudan karta gider (günlük görünümüyle aynı eşleme).
    /// </summary>
    internal sealed class SerialScreenView : Control
    {
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const float MaxFontSize = 11f, MinFontSize = 5f;

        private readonly VtScreen _screen;
        private Font _font;
        private float _fontSize;
        private Size _cell;

        public Action<string> KeysTyped;
        public Action PasteRequested;
        public int DrawnVersion { get; private set; }

        // Standart 16 renk (VGA): BIOS menüleri bunları kullanır; "varsayılan" renkler temadan gelir
        private static readonly Color[] Palette =
        {
            Color.FromArgb(0, 0, 0), Color.FromArgb(170, 0, 0), Color.FromArgb(0, 170, 0), Color.FromArgb(170, 85, 0),
            Color.FromArgb(0, 0, 170), Color.FromArgb(170, 0, 170), Color.FromArgb(0, 170, 170), Color.FromArgb(170, 170, 170),
            Color.FromArgb(85, 85, 85), Color.FromArgb(255, 85, 85), Color.FromArgb(85, 255, 85), Color.FromArgb(255, 255, 85),
            Color.FromArgb(85, 85, 255), Color.FromArgb(255, 85, 255), Color.FromArgb(85, 255, 255), Color.FromArgb(255, 255, 255),
        };

        public SerialScreenView(VtScreen screen)
        {
            _screen = screen;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.IBeam;
        }

        private void EnsureFont()
        {
            int w = Math.Max(1, ClientSize.Width - 8), h = Math.Max(1, ClientSize.Height - 8);
            // Izgarayı sığdıran en büyük boyut (en çok 11 pt)
            float size = MaxFontSize;
            while (true)
            {
                Size cell = Measure(size);
                if ((cell.Width * _screen.Cols <= w && cell.Height * _screen.Rows <= h) || size <= MinFontSize) break;
                size -= 0.5f;
            }
            if (_font != null && Math.Abs(size - _fontSize) < 0.01f) return;
            if (_font != null) _font.Dispose();
            _fontSize = size;
            _font = new Font(Theme.Mono.FontFamily, size);
            _cell = Measure(size);
        }

        private static Size Measure(float size)
        {
            using (var f = new Font(Theme.Mono.FontFamily, size))
            {
                Size s = TextRenderer.MeasureText("MMMMMMMMMM", f, Size.Empty, TextFormatFlags.NoPadding);
                return new Size(Math.Max(1, s.Width / 10), Math.Max(1, f.Height));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color defBack = Theme.Input, defFore = Theme.Terminal;
            g.Clear(defBack);
            EnsureFont();
            DrawnVersion = _screen.Version;
            const int ox = 4, oy = 4;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            for (int r = 0; r < _screen.Rows; r++)
            {
                int y = oy + r * _cell.Height;
                if (y > e.ClipRectangle.Bottom) break;
                if (y + _cell.Height < e.ClipRectangle.Top) continue;
                for (int c = 0; c < _screen.Cols; c++)
                {
                    VtScreen.Cell cell = _screen[r, c];
                    Color fore = cell.Fg < 0 ? defFore : Palette[(cell.Bold && cell.Fg < 8) ? cell.Fg + 8 : cell.Fg];
                    Color back = cell.Bg < 0 ? defBack : Palette[cell.Bg];
                    if (cell.Reverse) { Color t = fore; fore = back; back = t; }
                    bool cursor = _screen.CursorVisible && r == _screen.CursorRow && c == _screen.CursorCol;
                    if (cursor && Focused) { Color t = fore; fore = back; back = t; }
                    var rect = new Rectangle(ox + c * _cell.Width, y, _cell.Width, _cell.Height);
                    if (back != defBack)
                        using (var b = new SolidBrush(back)) g.FillRectangle(b, rect);
                    if (cursor && !Focused)
                        using (var p = new Pen(fore)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                    char ch = cell.Ch;
                    if (ch != ' ' && ch != '\0')
                        TextRenderer.DrawText(g, ch.ToString(), _font, rect.Location, fore, back, flags);
                    if (cell.Underline)
                        using (var p = new Pen(fore)) g.DrawLine(p, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
                }
            }
        }

        // ---------- Tuşlar ----------

        protected override bool IsInputKey(Keys keyData)
        {
            if (SerialKeys.IsTerminalKey(keyData & Keys.KeyCode)) return true;
            return base.IsInputKey(keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Escape || k == Keys.Enter || k == Keys.Tab) return false;
            return base.ProcessDialogKey(keyData);
        }

        private void Send(string s)
        {
            var h = KeysTyped;
            if (h != null && !string.IsNullOrEmpty(s)) h(s);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys k = e.KeyCode;
            if ((e.Shift && k == Keys.Insert) || (e.Control && !e.Alt && !e.Shift && k == Keys.V))
            {
                e.Handled = e.SuppressKeyPress = true;
                var p = PasteRequested;
                if (p != null) p();
                return;
            }
            string seq = SerialKeys.Sequence(k, e.Control, e.Alt, _screen.AppCursorKeys);
            if (seq != null)
            {
                e.Handled = e.SuppressKeyPress = true;
                Send(seq);
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
            if (e.KeyChar >= ' ' && e.KeyChar != '\u007f') Send(e.KeyChar.ToString());
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            // F10 Windows'ta menü tuşudur: pencere menüsü yerine karta gider
            if ((m.Msg == WM_SYSKEYDOWN || m.Msg == WM_SYSKEYUP) && (int)m.WParam == (int)Keys.F10 && (ModifierKeys & Keys.Alt) == 0)
            {
                if (m.Msg == WM_SYSKEYDOWN) Send(SerialKeys.FunctionKey(Keys.F10));
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _font != null) { _font.Dispose(); _font = null; }
            base.Dispose(disposing);
        }
    }
}
