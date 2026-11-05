using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Koyu temalı ipucu balonu. Windows'un varsayılan açık renkli ipucu yerine uygulamanın renkleriyle çizilir;
    /// uzun metinler satıra bölünür (en fazla ~420 px genişlik).
    /// </summary>
    public sealed class DarkToolTip : ToolTip
    {
        private const int MaxWidth = 420;
        private static readonly Padding Pad = new Padding(10, 7, 10, 8);

        public DarkToolTip()
        {
            OwnerDraw = true;
            InitialDelay = 450;
            ReshowDelay = 120;
            AutoPopDelay = 20000;
            BackColor = Theme.SurfaceHigh;
            ForeColor = Theme.Text;
            Popup += OnPopup;
            Draw += OnDraw;
        }

        private void OnPopup(object sender, PopupEventArgs e)
        {
            string text = GetToolTip(e.AssociatedControl);
            Size s = TextRenderer.MeasureText(text, Theme.Ui, new Size(MaxWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            e.ToolTipSize = new Size(Math.Min(MaxWidth, s.Width) + Pad.Horizontal, s.Height + Pad.Vertical);
        }

        private void OnDraw(object sender, DrawToolTipEventArgs e)
        {
            Rectangle r = e.Bounds;
            using (var bg = new SolidBrush(Theme.SurfaceHigh)) e.Graphics.FillRectangle(bg, r);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
            var tr = new Rectangle(r.X + Pad.Left, r.Y + Pad.Top, r.Width - Pad.Horizontal, r.Height - Pad.Vertical);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, Theme.Ui, tr, Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
        }
    }

    /// <summary>
    /// Tek çizgili metin kutusunun (BorderStyle.FixedSingle) kenarlığını tema rengiyle çizer; odaktayken mavi.
    /// Windows'un siyah kenarlığının yerine geçer. Kutunun kendisine dokunmaz (NativeWindow ile dinler).
    /// </summary>
    public sealed class TextBoxBorder : NativeWindow
    {
        private const int WM_NCPAINT = 0x0085, WM_SETFOCUS = 0x0007, WM_KILLFOCUS = 0x0008, WM_PAINT = 0x000F;
        private readonly TextBox _box;

        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private TextBoxBorder(TextBox box)
        {
            _box = box;
            if (box.IsHandleCreated) AssignHandle(box.Handle);
            box.HandleCreated += (s, e) => AssignHandle(box.Handle);
            box.HandleDestroyed += (s, e) => ReleaseHandle();
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, TextBoxBorder> Attached =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, TextBoxBorder>();

        public static void Attach(TextBox box)
        {
            TextBoxBorder existing;
            if (Attached.TryGetValue(box, out existing)) return;
            Attached.Add(box, new TextBoxBorder(box));
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCPAINT || m.Msg == WM_PAINT || m.Msg == WM_SETFOCUS || m.Msg == WM_KILLFOCUS) DrawBorder();
        }

        private void DrawBorder()
        {
            if (_box.BorderStyle != BorderStyle.FixedSingle || Handle == IntPtr.Zero) return;
            IntPtr dc = GetWindowDC(Handle);
            if (dc == IntPtr.Zero) return;
            try
            {
                using (Graphics g = Graphics.FromHdc(dc))
                using (var p = new Pen(_box.Focused && !_box.ReadOnly ? Theme.Accent : Theme.Border))
                    g.DrawRectangle(p, 0, 0, _box.Width - 1, _box.Height - 1);
            }
            catch (Exception) { }
            finally { ReleaseDC(Handle, dc); }
        }
    }

    /// <summary>
    /// Metninin başındaki sembolü (⚠ ℹ ✓ ✕ ● ⏱ ⇶) yazı tipiyle değil çizilmiş simgeyle gösteren etiket.
    /// Böylece semboller her Windows sürümünde aynı ve keskin görünür. Metni ve renkleri normal Label gibi ayarlanır;
    /// sembolsüz metinde Label'dan farkı yoktur. Closable: sağda kapatma (✕) işareti çizilir.
    /// </summary>
    public class GlyphLabel : Label
    {
        private const int IconGap = 7;

        public bool Closable { get; set; }

        private static IconKind KindOf(char c)
        {
            switch (c)
            {
                case '⚠': return IconKind.Warning;
                case 'ℹ': return IconKind.Info;
                case '✓': return IconKind.Check;
                case '✕': return IconKind.Close;
                case '●': return IconKind.Dot;
                case '⏱': return IconKind.Clock;
                case '⇶': return IconKind.SendAll;
                default: return IconKind.None;
            }
        }

        private bool Split(out IconKind kind, out string body)
        {
            string t = Text ?? "";
            kind = t.Length > 0 ? KindOf(t[0]) : IconKind.None;
            body = kind == IconKind.None ? t : t.Substring(1).TrimStart(' ', '\u00A0');
            return kind != IconKind.None;
        }

        private int IconPx { get { return Math.Max(11, Math.Min(16, Font.Height - 3)); } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            IconKind kind; string body;
            if (!Split(out kind, out body)) return base.GetPreferredSize(proposedSize);
            Size ts = TextRenderer.MeasureText(body, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return new Size(Padding.Horizontal + IconPx + IconGap + ts.Width + 2 + (Closable ? 28 : 0), Math.Max(ts.Height, IconPx) + Padding.Vertical + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            IconKind kind; string body;
            if (!Split(out kind, out body) && !Closable) { base.OnPaint(e); return; }
            Graphics g = e.Graphics;
            Rectangle r = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal - (Closable ? 28 : 0), Height - Padding.Vertical);
            bool middle = TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.MiddleRight || AutoSize;
            int size = IconPx;
            int x = r.X;
            if (kind != IconKind.None)
            {
                float iy = middle ? r.Y + (r.Height - size) / 2f : r.Y + (Font.Height - size) / 2f + 1;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                IconRenderer.Draw(g, kind, new RectangleF(x, iy, size, size), Enabled ? ForeColor : Theme.TextFaint);
                x += size + IconGap;
            }
            var tr = new Rectangle(x, r.Y, Math.Max(1, r.Right - x), r.Height);
            TextFormatFlags f = TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.NoPadding |
                                (middle ? TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine : TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, body, Font, tr, Enabled ? ForeColor : Theme.TextFaint, f);
            if (Closable)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                IconRenderer.Draw(g, IconKind.Close, new RectangleF(Width - 24, (Height - 12) / 2f, 12, 12), Color.FromArgb(170, ForeColor));
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }
    }

    /// <summary>
    /// Açma / kapama anahtarı (CheckBox yerine). Checked, CheckedChanged, klavye (Boşluk) ve ekran okuyucu davranışı
    /// CheckBox ile aynıdır; yalnız çizimi modern bir anahtar biçimindedir.
    /// </summary>
    public sealed class ToggleSwitch : CheckBox
    {
        private const int TrackW = 30, TrackH = 16, Gap = 8;
        private bool _hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = Theme.Ui;
            ForeColor = Theme.TextMuted;
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size ts = TextRenderer.MeasureText(Text ?? "", Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(TrackW + (string.IsNullOrEmpty(Text) ? 0 : Gap + ts.Width) + 2, Math.Max(TrackH + 6, ts.Height + 6));
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color parentBack = Parent != null ? Parent.BackColor : Theme.Window;
            using (var pb = new SolidBrush(parentBack)) g.FillRectangle(pb, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float ty = (Height - TrackH) / 2f;
            var track = new RectangleF(1, ty, TrackW - 2, TrackH);
            Color trackFill = !Enabled ? Theme.Surface : Checked ? (_hover ? Theme.AccentFillHover : Theme.AccentFill) : (_hover ? Theme.Hover : Theme.SurfaceHigh);
            using (var path = IconRenderer.RoundRect(track, TrackH / 2f))
            {
                using (var b = new SolidBrush(trackFill)) g.FillPath(b, path);
                if (!Checked)
                    using (var p = new Pen(_hover && Enabled ? Theme.TextFaint : Theme.Border)) g.DrawPath(p, path);
                if (Focused && ShowFocusCues)
                    using (var p = new Pen(Theme.Accent, 1.5f)) g.DrawPath(p, path);
            }
            float knob = TrackH - 6;
            float kx = Checked ? track.Right - 3 - knob : track.X + 3;
            Color knobColor = !Enabled ? Theme.TextFaint : Checked ? Color.White : Theme.TextMuted;
            using (var b = new SolidBrush(knobColor)) g.FillEllipse(b, kx, ty + 3, knob, knob);

            if (!string.IsNullOrEmpty(Text))
            {
                Color fore = !Enabled ? Theme.TextFaint : Checked || _hover ? Theme.Text : Theme.TextMuted;
                var tr = new Rectangle(TrackW + Gap, 0, Width - TrackW - Gap, Height);
                TextRenderer.DrawText(g, Text, Font, tr, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>
    /// Koyu temalı açılır liste (yalnız seçim; DropDownList). Kapalı hâli ve açılan liste uygulamanın renkleriyle çizilir:
    /// yuvarlatılmamış ince kenarlık (odakta / üzerine gelince mavi), sağda ok simgesi.
    /// Dışarıdan DrawItem işleyicisi bağlanırsa (ör. iki sütunlu öğeler) liste öğelerini o çizer.
    /// </summary>
    public sealed class DarkComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private bool _hover;

        /// <summary>Kapalı hâlde gösterilecek metin (null: öğenin kendi metni). Ör. listede uzun, kutuda kısa ad.</summary>
        public Func<object, string> ClosedText { get; set; }

        public DarkComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 20;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index >= 0 && e.Index < Items.Count)
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var bg = new SolidBrush(selected ? Theme.Selection : Theme.SurfaceHigh)) e.Graphics.FillRectangle(bg, e.Bounds);
                var tr = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, tr, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            base.OnDrawItem(e);   // dışarıdan bağlı DrawItem işleyicisi varsa üzerine çizer
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT) PaintClosed();
        }

        /// <summary>Kapalı açılır listeyi (seçili değer + ok) Windows'un çiziminin üzerine çizer.</summary>
        private void PaintClosed()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    Rectangle r = ClientRectangle;
                    bool active = Enabled && (_hover || Focused || DroppedDown);
                    using (var bg = new SolidBrush(Enabled ? (active ? Color.FromArgb(20, 23, 29) : BackColor) : Theme.Surface)) g.FillRectangle(bg, r);
                    using (var p = new Pen(Focused || DroppedDown ? Theme.Accent : _hover && Enabled ? Theme.TextFaint : Theme.Border))
                        g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
                    string text = SelectedIndex < 0 ? "" : ClosedText != null ? ClosedText(SelectedItem) : GetItemText(SelectedItem);
                    var tr = new Rectangle(8, 0, r.Width - 8 - 26, r.Height);
                    TextRenderer.DrawText(g, text, Font, tr, Enabled ? Theme.Text : Theme.TextFaint,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    IconRenderer.Draw(g, DroppedDown ? IconKind.ChevronUp : IconKind.ChevronDown,
                        new RectangleF(r.Width - 22, (r.Height - 12) / 2f, 12, 12), Enabled ? Theme.TextMuted : Theme.TextFaint);
                }
            }
            catch (Exception) { }
        }
    }
}
