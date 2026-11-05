using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>Uygulamanın koyu teması: renkler, yazı tipleri ve ortak stil yardımcıları.</summary>
    public static class Theme
    {
        // Yüzeyler (koyudan açığa)
        public static readonly Color Window = Color.FromArgb(17, 19, 24);
        public static readonly Color Sidebar = Color.FromArgb(22, 25, 31);
        public static readonly Color Surface = Color.FromArgb(28, 32, 39);
        public static readonly Color SurfaceHigh = Color.FromArgb(36, 41, 50);
        public static readonly Color Input = Color.FromArgb(14, 16, 20);
        public static readonly Color Border = Color.FromArgb(58, 65, 78);
        public static readonly Color Hover = Color.FromArgb(44, 50, 61);
        public static readonly Color Selection = Color.FromArgb(38, 64, 102);

        // Metin
        public static readonly Color Text = Color.FromArgb(226, 230, 237);
        // Kontrast (WCAG AA, normal metin ≥ 4.5:1) için ölçülüp ayarlandı: ikincil metin her zeminde ≥ 4.5,
        // ipucu metni koyu zeminlerde ≥ 4.5, pasif düğmede ≥ 3.
        public static readonly Color TextMuted = Color.FromArgb(163, 171, 184);
        public static readonly Color TextFaint = Color.FromArgb(128, 137, 151);
        public static readonly Color Terminal = Color.FromArgb(205, 214, 224);

        // Vurgu ve durum renkleri
        /// <summary>Vurgu: koyu zemin üzerindeki METİN ve göstergeler için (parlak mavi).</summary>
        public static readonly Color Accent = Color.FromArgb(56, 132, 255);
        public static readonly Color AccentHover = Color.FromArgb(84, 152, 255);
        /// <summary>Vurgu ZEMİNİ: üzerinde beyaz yazı olan düğmeler için (beyazla ≥ 5:1).</summary>
        public static readonly Color AccentFill = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentFillHover = Color.FromArgb(29, 84, 212);
        public static readonly Color Success = Color.FromArgb(46, 194, 126);
        public static readonly Color Warning = Color.FromArgb(240, 168, 48);
        public static readonly Color Danger = Color.FromArgb(248, 113, 113);
        public static readonly Color Idle = Color.FromArgb(116, 124, 138);

        public static readonly Font Ui = new Font("Segoe UI", 9f);
        public static readonly Font UiBold = new Font("Segoe UI Semibold", 9f);
        public static readonly Font UiSmall = new Font("Segoe UI", 8f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 11f);
        public static readonly Font SectionHeader = new Font("Segoe UI Semibold", 8f);
        public static readonly Font Mono = new Font("Consolas", 9.5f);

        public static Color StateColor(CardState s)
        {
            switch (s)
            {
                case CardState.Connected: return Success;
                case CardState.Connecting: return Warning;
                case CardState.Error:
                case CardState.Recovering: return Danger;     // kopmuş kart mevcut kırmızı göstergeyle
                default: return Idle;
            }
        }

        public static string StateText(CardState s)
        {
            switch (s)
            {
                case CardState.Connected: return "Bağlı";
                case CardState.Connecting: return "Bağlanıyor";
                case CardState.Error: return "Bağlantı yok";
                case CardState.Recovering: return "Bağlantı kesildi · yeniden bağlanılacak";
                default: return "Bağlı değil";
            }
        }

        public enum ButtonKind { Primary, Secondary, Ghost, Danger }

        /// <summary>
        /// Yazılı düğme: uygulamanın tüm düğmeleri aynı çizimi kullanır (yuvarlatılmış köşe, üzerine gelince / basınca
        /// renk, klavye odak halkası, pasif görünüm). Birincil: mavi dolgu; ikincil: kenarlıklı yüzey; hayalet: yalnız yazı;
        /// tehlike: kırmızı.
        /// </summary>
        public static Button MakeButton(string text, ButtonKind kind)
        {
            IconTone tone = kind == ButtonKind.Primary ? IconTone.Primary
                          : kind == ButtonKind.Danger ? IconTone.DangerFill
                          : kind == ButtonKind.Ghost ? IconTone.Ghost : IconTone.Secondary;
            var b = new IconButton(IconKind.None, tone) { Size = new Size(100, 32) };
            b.Font = kind == ButtonKind.Primary || kind == ButtonKind.Danger ? UiBold : Ui;
            b.Text = text;
            return b;
        }

        /// <summary>Simgeli + yazılı düğme.</summary>
        public static IconButton MakeButton(string text, IconKind icon, ButtonKind kind)
        {
            var b = (IconButton)MakeButton(text, kind);
            b.Icon = icon;
            return b;
        }

        public static void StyleButton(Button b, ButtonKind kind)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.Font = kind == ButtonKind.Primary ? UiBold : Ui;
            b.FlatAppearance.BorderSize = kind == ButtonKind.Secondary ? 1 : 0;
            b.FlatAppearance.BorderColor = Border;
            switch (kind)
            {
                case ButtonKind.Primary:
                    b.BackColor = AccentFill; b.ForeColor = Color.White;
                    b.FlatAppearance.MouseOverBackColor = AccentFillHover;
                    b.FlatAppearance.MouseDownBackColor = AccentFill;
                    break;
                case ButtonKind.Danger:
                    b.BackColor = SurfaceHigh; b.ForeColor = Danger;
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 36, 40);
                    b.FlatAppearance.MouseDownBackColor = Color.FromArgb(90, 40, 44);
                    break;
                case ButtonKind.Ghost:
                    b.BackColor = Color.Transparent; b.ForeColor = TextMuted;
                    b.FlatAppearance.MouseOverBackColor = Hover;
                    b.FlatAppearance.MouseDownBackColor = Border;
                    break;
                default:
                    b.BackColor = SurfaceHigh; b.ForeColor = Text;
                    b.FlatAppearance.MouseOverBackColor = Hover;
                    b.FlatAppearance.MouseDownBackColor = Border;
                    break;
            }
        }

        public static void StyleTextBox(TextBox t)
        {
            t.BackColor = Input;
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = Ui;
            TextBoxBorder.Attach(t);   // kenarlık: tema rengi, odakta mavi
        }

        private static Icon _appIcon;
        private static bool _appIconLoaded;

        /// <summary>Uygulama simgesi (exe'ye gömülü); bulunamazsa null.</summary>
        public static Icon AppIcon
        {
            get
            {
                if (!_appIconLoaded)
                {
                    _appIconLoaded = true;
                    try { _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { _appIcon = null; }
                }
                return _appIcon;
            }
        }

        public static void StyleNumeric(NumericUpDown n)
        {
            n.BackColor = Input;
            n.ForeColor = Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = Ui;
            // Yukarı / aşağı okları koyu temada çiz (iç denetim Paint olayını kendi çiziminden sonra yayınlar)
            if (n.Controls.Count > 0)
            {
                Control ud = n.Controls[0];
                ud.Paint += (s, e) =>
                {
                    Rectangle r = ud.ClientRectangle;
                    int half = r.Height / 2;
                    using (var bg = new SolidBrush(n.Enabled ? SurfaceHigh : Surface)) e.Graphics.FillRectangle(bg, r);
                    using (var p = new Pen(Border))
                    {
                        e.Graphics.DrawLine(p, 0, 0, 0, r.Height);
                        e.Graphics.DrawLine(p, 0, half, r.Width, half);
                    }
                    Color c = n.Enabled ? TextMuted : TextFaint;
                    float sz = Math.Min(10, half - 2);
                    IconRenderer.Draw(e.Graphics, IconKind.ChevronUp, new RectangleF((r.Width - sz) / 2f + 0.5f, (half - sz) / 2f + 0.5f, sz, sz), c);
                    IconRenderer.Draw(e.Graphics, IconKind.ChevronDown, new RectangleF((r.Width - sz) / 2f + 0.5f, half + (r.Height - half - sz) / 2f, sz, sz), c);
                };
            }
        }

        public static void StyleCombo(ComboBox c)
        {
            if (c is DarkComboBox) { c.BackColor = Input; c.ForeColor = Text; c.Font = Ui; return; }
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Input;
            c.ForeColor = Text;
            c.Font = Ui;
        }

        public static void StyleCheck(CheckBox c)
        {
            c.ForeColor = TextMuted;
            c.Font = Ui;
            c.AutoSize = true;
            c.Cursor = Cursors.Hand;
        }

        public static Label MakeLabel(string text, Font font, Color color)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent };
        }

        // ---------- Windows 10/11 koyu pencere çerçevesi ve kaydırma çubukları ----------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        /// <summary>Başlık çubuğunu koyu yapar (Windows 10 1809+). Desteklenmiyorsa sessizce geçer.</summary>
        public static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Boş metin kutusunda soluk ipucu metni gösterir (EM_SETCUEBANNER).</summary>
        public static void SetCueBanner(TextBox box, string text)
        {
            EventHandler apply = (s, e) => { try { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); } catch { } };
            if (box.IsHandleCreated) apply(box, EventArgs.Empty);
            else box.HandleCreated += apply;
        }

        /// <summary>Kaydırma çubuklarını koyu temaya alır (Windows 10+). Desteklenmiyorsa sessizce geçer.</summary>
        public static void ApplyDarkScrollBars(Control c)
        {
            try
            {
                if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                else c.HandleCreated += (s, e) => { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
            }
            catch { }
        }
    }
}
