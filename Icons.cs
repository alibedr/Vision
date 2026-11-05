using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vision
{
    public enum IconKind
    {
        None, Send, SendAll, Connect, Disconnect, Clear, Maximize, Restore, Close,
        Back, Refresh, NewFolder, Upload, Settings, SidebarHide, SidebarShow, Folder,
        Plus, Search, Edit, Trash, Copy, Check, ChevronRight, ChevronDown, Module, ExpandAll, CollapseAll,
        ChevronUp, Warning, Info, Terminal, Book, Home, Clock, Dot,
        Save, Bolt, Power, Swap, Minus, Return,
    }

    /// <summary>
    /// Kodla çizilen çizgi ikonlar (16×16 tasarım alanı, istenen boyuta ölçeklenir).
    /// Yazı tipine bağlı değildir: her Windows sürümünde ve her ölçekte aynı ve keskin görünür.
    /// </summary>
    public static class IconRenderer
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF bounds, Color color)
        {
            if (kind == IconKind.None) return;
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = Math.Min(bounds.Width, bounds.Height) / 16f;
            g.TranslateTransform(bounds.X + (bounds.Width - 16 * s) / 2f, bounds.Y + (bounds.Height - 16 * s) / 2f);
            g.ScaleTransform(s, s);

            using (var pen = new Pen(color, 1.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                switch (kind)
                {
                    case IconKind.Send:
                        g.DrawPolygon(pen, new[] { P(2.2f, 2.6f), P(14.2f, 8f), P(2.2f, 13.4f), P(4.4f, 8f) });
                        g.DrawLine(pen, 4.4f, 8f, 9.2f, 8f);
                        break;

                    case IconKind.SendAll:   // yayın: nokta + iki yanda dalgalar
                        g.FillEllipse(brush, 6.4f, 6.4f, 3.2f, 3.2f);
                        g.DrawArc(pen, 4.2f, 4.2f, 7.6f, 7.6f, -50, 100);
                        g.DrawArc(pen, 4.2f, 4.2f, 7.6f, 7.6f, 130, 100);
                        g.DrawArc(pen, 1.4f, 1.4f, 13.2f, 13.2f, -50, 100);
                        g.DrawArc(pen, 1.4f, 1.4f, 13.2f, 13.2f, 130, 100);
                        break;

                    case IconKind.Connect:
                        DrawPlug(g, pen);
                        break;

                    case IconKind.Disconnect:
                        DrawPlug(g, pen);
                        g.DrawLine(pen, 2.2f, 2.2f, 13.8f, 13.8f);
                        break;

                    case IconKind.Clear:     // silgi
                        g.DrawPolygon(pen, new[] { P(5.2f, 13.2f), P(1.9f, 9.9f), P(9.1f, 2.7f), P(13.6f, 7.2f), P(7.6f, 13.2f) });
                        g.DrawLine(pen, 4.6f, 7.3f, 9.4f, 12.1f);
                        g.DrawLine(pen, 7.6f, 13.2f, 14.4f, 13.2f);
                        break;

                    case IconKind.Maximize:
                        DrawRoundRect(g, pen, 2.8f, 2.8f, 10.4f, 10.4f, 1.6f);
                        break;

                    case IconKind.Restore:
                        DrawRoundRect(g, pen, 2.4f, 5.4f, 8.2f, 8.2f, 1.4f);
                        g.DrawLines(pen, new[] { P(5.4f, 5.4f), P(5.4f, 2.4f), P(13.6f, 2.4f), P(13.6f, 10.6f), P(10.6f, 10.6f) });
                        break;

                    case IconKind.Close:
                        g.DrawLine(pen, 4f, 4f, 12f, 12f);
                        g.DrawLine(pen, 12f, 4f, 4f, 12f);
                        break;

                    case IconKind.Back:
                        g.DrawLine(pen, 13.2f, 8f, 3f, 8f);
                        g.DrawLines(pen, new[] { P(7.2f, 3.8f), P(3f, 8f), P(7.2f, 12.2f) });
                        break;

                    case IconKind.Refresh:   // saat yönünde dönen yay + uçta ok
                        g.DrawArc(pen, 2.2f, 2.2f, 11.6f, 11.6f, 0, 315);
                        g.DrawLine(pen, 12.1f, 3.9f, 13.8f, 5.4f);
                        g.DrawLines(pen, new[] { P(13.8f, 1.8f), P(13.8f, 5.4f), P(10.2f, 5.4f) });
                        break;

                    case IconKind.NewFolder:
                        DrawFolder(g, pen);
                        g.DrawLine(pen, 8f, 7.6f, 8f, 11.8f);
                        g.DrawLine(pen, 5.9f, 9.7f, 10.1f, 9.7f);
                        break;

                    case IconKind.Folder:
                        DrawFolder(g, pen);
                        break;

                    case IconKind.Upload:
                        g.DrawLine(pen, 8f, 10.6f, 8f, 2.4f);
                        g.DrawLines(pen, new[] { P(4.4f, 6f), P(8f, 2.4f), P(11.6f, 6f) });
                        g.DrawLines(pen, new[] { P(2.4f, 9.8f), P(2.4f, 13.6f), P(13.6f, 13.6f), P(13.6f, 9.8f) });
                        break;

                    case IconKind.Settings:  // dişli
                        g.DrawEllipse(pen, 5.7f, 5.7f, 4.6f, 4.6f);
                        g.DrawEllipse(pen, 3.4f, 3.4f, 9.2f, 9.2f);
                        using (var tooth = new Pen(color, 2.1f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                double a = i * Math.PI / 4;
                                float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                                g.DrawLine(tooth, 8 + 4.6f * c, 8 + 4.6f * sn, 8 + 6.6f * c, 8 + 6.6f * sn);
                            }
                        }
                        break;

                    case IconKind.Plus:
                        g.DrawLine(pen, 8f, 3f, 8f, 13f);
                        g.DrawLine(pen, 3f, 8f, 13f, 8f);
                        break;

                    case IconKind.Search:    // büyüteç
                        g.DrawEllipse(pen, 2.4f, 2.4f, 8.4f, 8.4f);
                        g.DrawLine(pen, 9.6f, 9.6f, 13.6f, 13.6f);
                        break;

                    case IconKind.Edit:      // kalem
                        g.DrawPolygon(pen, new[] { P(10.6f, 2.6f), P(13.4f, 5.4f), P(5.6f, 13.2f), P(2.4f, 13.6f), P(2.8f, 10.4f) });
                        g.DrawLine(pen, 9f, 4.2f, 11.8f, 7f);
                        break;

                    case IconKind.Trash:     // çöp kutusu
                        g.DrawLine(pen, 2.4f, 4.2f, 13.6f, 4.2f);
                        g.DrawLines(pen, new[] { P(6f, 4.2f), P(6f, 2.4f), P(10f, 2.4f), P(10f, 4.2f) });
                        g.DrawLines(pen, new[] { P(3.8f, 4.2f), P(4.6f, 13.6f), P(11.4f, 13.6f), P(12.2f, 4.2f) });
                        g.DrawLine(pen, 6.6f, 6.8f, 6.8f, 11.2f);
                        g.DrawLine(pen, 9.4f, 6.8f, 9.2f, 11.2f);
                        break;

                    case IconKind.Copy:      // iki sayfa
                        DrawRoundRect(g, pen, 5.4f, 5.4f, 8.4f, 8.4f, 1.4f);
                        g.DrawLines(pen, new[] { P(10.6f, 5.4f), P(10.6f, 2.2f), P(2.2f, 2.2f), P(2.2f, 10.6f), P(5.4f, 10.6f) });
                        break;

                    case IconKind.Check:
                        g.DrawLines(pen, new[] { P(2.8f, 8.4f), P(6.4f, 12f), P(13.2f, 4.2f) });
                        break;

                    case IconKind.ChevronRight:
                        g.DrawLines(pen, new[] { P(6f, 3.6f), P(10.4f, 8f), P(6f, 12.4f) });
                        break;

                    case IconKind.ChevronDown:
                        g.DrawLines(pen, new[] { P(3.6f, 6f), P(8f, 10.4f), P(12.4f, 6f) });
                        break;

                    case IconKind.Module:    // üst üste katmanlar (komut grubu)
                        g.DrawPolygon(pen, new[] { P(8f, 1.8f), P(14.2f, 5f), P(8f, 8.2f), P(1.8f, 5f) });
                        g.DrawLines(pen, new[] { P(1.8f, 8.2f), P(8f, 11.4f), P(14.2f, 8.2f) });
                        g.DrawLines(pen, new[] { P(1.8f, 11.2f), P(8f, 14.4f), P(14.2f, 11.2f) });
                        break;

                    case IconKind.ExpandAll:     // aşağı ve yukarı açılan oklar
                        g.DrawLines(pen, new[] { P(4.4f, 5.6f), P(8f, 2.2f), P(11.6f, 5.6f) });
                        g.DrawLines(pen, new[] { P(4.4f, 10.4f), P(8f, 13.8f), P(11.6f, 10.4f) });
                        break;

                    case IconKind.CollapseAll:   // içe kapanan oklar
                        g.DrawLines(pen, new[] { P(4.4f, 2.2f), P(8f, 5.6f), P(11.6f, 2.2f) });
                        g.DrawLines(pen, new[] { P(4.4f, 13.8f), P(8f, 10.4f), P(11.6f, 13.8f) });
                        g.DrawLine(pen, 3f, 8f, 13f, 8f);
                        break;

                    case IconKind.ChevronUp:
                        g.DrawLines(pen, new[] { P(3.6f, 10f), P(8f, 5.6f), P(12.4f, 10f) });
                        break;

                    case IconKind.Warning:   // üçgen + ünlem
                        g.DrawPolygon(pen, new[] { P(8f, 1.8f), P(14.6f, 13.6f), P(1.4f, 13.6f) });
                        g.DrawLine(pen, 8f, 6.2f, 8f, 9.4f);
                        g.FillEllipse(brush, 7.1f, 10.9f, 1.8f, 1.8f);
                        break;

                    case IconKind.Info:      // daire + i
                        g.DrawEllipse(pen, 1.6f, 1.6f, 12.8f, 12.8f);
                        g.DrawLine(pen, 8f, 7.2f, 8f, 11.2f);
                        g.FillEllipse(brush, 7.1f, 4.1f, 1.8f, 1.8f);
                        break;

                    case IconKind.Terminal:  // pencere + istem ">_"
                        DrawRoundRect(g, pen, 1.6f, 2.4f, 12.8f, 11.2f, 1.8f);
                        g.DrawLines(pen, new[] { P(4.4f, 6.2f), P(6.6f, 8.2f), P(4.4f, 10.2f) });
                        g.DrawLine(pen, 8.2f, 10.4f, 11.4f, 10.4f);
                        break;

                    case IconKind.Book:      // açık kitap
                        g.DrawLines(pen, new[] { P(8f, 4.2f), P(8f, 13.4f) });
                        g.DrawLines(pen, new[] { P(8f, 4.2f), P(5.6f, 3f), P(1.6f, 3f), P(1.6f, 12.2f), P(5.6f, 12.2f), P(8f, 13.4f) });
                        g.DrawLines(pen, new[] { P(8f, 4.2f), P(10.4f, 3f), P(14.4f, 3f), P(14.4f, 12.2f), P(10.4f, 12.2f), P(8f, 13.4f) });
                        break;

                    case IconKind.Save:      // disket
                        g.DrawLines(pen, new[] { P(2.2f, 2.2f), P(11.4f, 2.2f), P(13.8f, 4.6f), P(13.8f, 13.8f), P(2.2f, 13.8f), P(2.2f, 2.2f) });
                        g.DrawLines(pen, new[] { P(5f, 2.4f), P(5f, 5.8f), P(10.4f, 5.8f), P(10.4f, 2.4f) });
                        DrawRoundRect(g, pen, 4.6f, 8.8f, 6.8f, 5f, 0.6f);
                        break;

                    case IconKind.Bolt:      // şimşek (hızlı komut)
                        g.DrawPolygon(pen, new[] { P(9.4f, 1.6f), P(3.4f, 9f), P(7.6f, 9f), P(6.6f, 14.4f), P(12.6f, 7f), P(8.4f, 7f) });
                        break;

                    case IconKind.Power:     // güç (yeniden başlat)
                        g.DrawArc(pen, 2.6f, 3f, 10.8f, 10.8f, -50, 280);
                        g.DrawLine(pen, 8f, 1.8f, 8f, 7.8f);
                        break;

                    case IconKind.Swap:      // iki yönlü değiştir
                        g.DrawLine(pen, 3f, 5.2f, 12.6f, 5.2f);
                        g.DrawLines(pen, new[] { P(10f, 2.6f), P(12.6f, 5.2f), P(10f, 7.8f) });
                        g.DrawLine(pen, 13f, 10.8f, 3.4f, 10.8f);
                        g.DrawLines(pen, new[] { P(6f, 8.2f), P(3.4f, 10.8f), P(6f, 13.4f) });
                        break;

                    case IconKind.Minus:
                        g.DrawLine(pen, 3f, 8f, 13f, 8f);
                        break;

                    case IconKind.Return:    // dönüş oku (↩)
                        g.DrawLines(pen, new[] { P(13.4f, 3f), P(13.4f, 8.6f), P(3.4f, 8.6f) });
                        g.DrawLines(pen, new[] { P(6.4f, 5.6f), P(3.4f, 8.6f), P(6.4f, 11.6f) });
                        break;

                    case IconKind.Clock:
                        g.DrawEllipse(pen, 1.8f, 1.8f, 12.4f, 12.4f);
                        g.DrawLines(pen, new[] { P(8f, 4.6f), P(8f, 8f), P(10.6f, 9.6f) });
                        break;

                    case IconKind.Dot:
                        g.FillEllipse(brush, 4.5f, 4.5f, 7f, 7f);
                        break;

                    case IconKind.Home:
                        g.DrawLines(pen, new[] { P(1.8f, 7.6f), P(8f, 2.2f), P(14.2f, 7.6f) });
                        g.DrawLines(pen, new[] { P(3.6f, 6.2f), P(3.6f, 13.6f), P(12.4f, 13.6f), P(12.4f, 6.2f) });
                        g.DrawLines(pen, new[] { P(6.6f, 13.6f), P(6.6f, 9.6f), P(9.4f, 9.6f), P(9.4f, 13.6f) });
                        break;

                    case IconKind.SidebarHide:   // kenar çubuğu açık: sol bölme dolu
                    case IconKind.SidebarShow:   // kenar çubuğu kapalı: sol bölme boş
                        if (kind == IconKind.SidebarHide)
                            using (var fill = new SolidBrush(Color.FromArgb(110, color)))
                                g.FillRectangle(fill, 2.2f, 3.2f, 4f, 9.6f);
                        DrawRoundRect(g, pen, 1.6f, 2.6f, 12.8f, 10.8f, 2f);
                        g.DrawLine(pen, 6.2f, 3.4f, 6.2f, 12.6f);
                        break;
                }
            }
            g.Restore(state);
        }

        private static PointF P(float x, float y)
        {
            return new PointF(x, y);
        }

        private static void DrawPlug(Graphics g, Pen pen)
        {
            g.DrawLine(pen, 6f, 1.6f, 6f, 4.6f);
            g.DrawLine(pen, 10f, 1.6f, 10f, 4.6f);
            using (var path = new GraphicsPath())
            {
                path.AddLine(4f, 4.6f, 12f, 4.6f);
                path.AddLine(12f, 4.6f, 12f, 7.6f);
                path.AddBezier(12f, 7.6f, 12f, 10f, 10.4f, 11.2f, 8f, 11.2f);
                path.AddBezier(8f, 11.2f, 5.6f, 11.2f, 4f, 10f, 4f, 7.6f);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
            g.DrawLine(pen, 8f, 11.2f, 8f, 14.4f);
        }

        private static void DrawFolder(Graphics g, Pen pen)
        {
            g.DrawLines(pen, new[]
            {
                P(1.6f, 12.6f), P(1.6f, 3.6f), P(6f, 3.6f), P(7.4f, 5.2f), P(14.4f, 5.2f), P(14.4f, 12.6f), P(1.6f, 12.6f),
            });
        }

        private static void DrawRoundRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
        {
            using (var path = RoundRect(new RectangleF(x, y, w, h), r)) g.DrawPath(pen, path);
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public enum IconTone { Ghost, Primary, Success, Danger, DangerFill, Secondary }

    /// <summary>
    /// İkonlu düğme. Yalnız ikon (dar alanlar) ya da ikon + metin olarak kullanılır. İpucu ve ekran okuyucu adı
    /// her zaman tam işlevi söyler (ör. "Bağlantı Kur"). Klavyeyle de kullanılabilir (normal Button'dan türer).
    /// </summary>
    public class IconButton : Button
    {
        private IconKind _icon;
        private IconTone _tone;
        private bool _hover, _pressed;
        private string _label = "";

        public IconButton(IconKind icon, IconTone tone)
        {
            _icon = icon;
            _tone = tone;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Size = new Size(28, 28);
            Font = Theme.UiBold;
            base.Text = "";
        }

        public IconKind Icon
        {
            get { return _icon; }
            set { if (_icon != value) { _icon = value; Invalidate(); } }
        }

        public IconTone Tone
        {
            get { return _tone; }
            set { if (_tone != value) { _tone = value; Invalidate(); } }
        }

        private Color _customFore = Color.Empty, _customBack = Color.Empty;

        /// <summary>Düğmenin yazısı = Label (Theme.MakeButton ile oluşturulan yazılı düğmeler de bu sınıftır).</summary>
        public override string Text
        {
            get { return _label; }
            set { Label = value; }
        }

        /// <summary>Hayalet (Ghost) düğmede yazı rengini değiştirir (ör. yol çubuğunda bağlantı rengi).</summary>
        public override Color ForeColor
        {
            get { return base.ForeColor; }
            set { base.ForeColor = value; _customFore = value; Invalidate(); }
        }

        /// <summary>Hayalet (Ghost) düğmede kalıcı zemin (ör. Yardım'da etkin sayfa).</summary>
        public override Color BackColor
        {
            get { return base.BackColor; }
            set { base.BackColor = value; _customBack = value; Invalidate(); }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size ts = _label.Length > 0 ? TextRenderer.MeasureText(_label, Font, Size.Empty, TextFormatFlags.NoPadding) : new Size(0, 16);
            int iconW = _icon == IconKind.None ? 0 : IconSize + (_label.Length > 0 ? 8 : 0);
            return new Size(iconW + ts.Width + 16 + Padding.Horizontal, Math.Max(ts.Height + 8, 24));
        }

        /// <summary>İkonun yanında gösterilecek metin (boşsa yalnız ikon).</summary>
        public string Label
        {
            get { return _label; }
            set { if (_label == (value ?? "")) return; _label = value ?? ""; AccessibleName = _label.Length > 0 ? _label : AccessibleName; Invalidate(); }
        }

        /// <summary>Arka plan kutusunun düğme kenarlarından içeri boşluğu (geniş tıklama alanında küçük görünüm için).</summary>
        public int Inset { get; set; }

        /// <summary>İkon boyutu (px).</summary>
        public int IconSize { get; set; } = 16;

        /// <summary>İpucunu ve ekran okuyucu adını birlikte ayarlar.</summary>
        public void SetTip(ToolTip tips, string text)
        {
            AccessibleName = text;
            if (tips != null) tips.SetToolTip(this, text);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _pressed = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color parentBack = Parent != null ? Parent.BackColor : Theme.Window;
            using (var pb = new SolidBrush(parentBack)) g.FillRectangle(pb, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color back, fore;
            Colors(out back, out fore);
            var box = new RectangleF(Inset + 0.5f, Inset + 0.5f, Width - 2 * Inset - 1, Height - 2 * Inset - 1);
            if (back.A > 0)
                using (var path = IconRenderer.RoundRect(box, 5))
                using (var b = new SolidBrush(back))
                    g.FillPath(b, path);
            if (_tone == IconTone.Secondary)
                using (var path = IconRenderer.RoundRect(box, 5))
                using (var p = new Pen(Theme.Border))
                    g.DrawPath(p, path);
            if (Focused && ShowFocusCues)
                using (var path = IconRenderer.RoundRect(box, 5))
                using (var p = new Pen(Theme.Accent, 1.5f))
                    g.DrawPath(p, path);

            int size = Math.Min(IconSize, Math.Min(Width, Height) - 6);
            if (_label.Length == 0)
            {
                IconRenderer.Draw(g, _icon, new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size), fore);
            }
            else
            {
                Size ts = TextRenderer.MeasureText(_label, Font, Size.Empty, TextFormatFlags.NoPadding);
                int gap = _icon == IconKind.None ? 0 : 8;
                int total = (_icon == IconKind.None ? 0 : size) + gap + ts.Width;
                bool left = TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.TopLeft || TextAlign == ContentAlignment.BottomLeft;
                float x = left ? 8 + Padding.Left : Math.Max(8, (Width - total) / 2f);
                if (_icon == IconKind.None) size = 0;
                if (_icon != IconKind.None)
                    IconRenderer.Draw(g, _icon, new RectangleF(x, (Height - size) / 2f, size, size), fore);
                var textRect = new Rectangle((int)(x + size + gap), 0, Math.Max(1, Width - (int)(x + size + gap) - 4), Height);
                TextRenderer.DrawText(g, _label, Font, textRect, fore,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
            }
        }

        private void Colors(out Color back, out Color fore)
        {
            bool customBack = !_customBack.IsEmpty && _customBack.A > 0 && _tone == IconTone.Ghost;
            bool customFore = !_customFore.IsEmpty && _tone == IconTone.Ghost;
            if (!Enabled)
            {
                back = _tone == IconTone.Primary || _tone == IconTone.DangerFill || _tone == IconTone.Secondary ? Theme.SurfaceHigh
                     : customBack ? _customBack : Color.Transparent;
                // Yol çubuğunda bulunulan klasör (pasif ama vurgulu) okunur kalsın
                fore = customFore && _customFore == Theme.Text ? Theme.Text : Theme.TextFaint;
                return;
            }
            switch (_tone)
            {
                case IconTone.Primary:
                    back = _pressed ? Theme.AccentFill : _hover ? Theme.AccentFillHover : Theme.AccentFill;
                    fore = Color.White;
                    break;
                case IconTone.Success:
                    back = _pressed ? Color.FromArgb(28, 70, 52) : _hover ? Color.FromArgb(22, 56, 42) : Color.Transparent;
                    fore = Theme.Success;
                    break;
                case IconTone.Secondary:    // ikincil düğme: yükseltilmiş zemin + ince kenarlık (OnPaint'te)
                    back = _pressed ? Theme.Border : _hover ? Theme.Hover : Theme.SurfaceHigh;
                    fore = Theme.Text;
                    break;
                case IconTone.DangerFill:   // geniş "Bağlantıyı Kes" düğmesi: hafif kırmızı zemin, kırmızı yazı
                    back = _pressed ? Color.FromArgb(90, 40, 44) : _hover ? Color.FromArgb(72, 36, 40) : Color.FromArgb(56, 32, 36);
                    fore = Theme.Danger;
                    break;
                case IconTone.Danger:
                    back = _pressed ? Color.FromArgb(90, 40, 44) : _hover ? Color.FromArgb(72, 36, 40) : Color.Transparent;
                    fore = Theme.Danger;
                    break;
                default:
                    back = _pressed ? Theme.Border : _hover ? Theme.Hover : customBack ? _customBack : Color.Transparent;
                    fore = customFore ? (_hover && _customFore == Theme.TextMuted ? Theme.Text : _customFore)
                         : _hover || _pressed ? Theme.Text : Theme.TextMuted;
                    break;
            }
        }
    }
}
