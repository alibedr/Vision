using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vision
{
    public enum ThemeMode { Dark = 0, Light = 1 }

    /// <summary>Uygulamanın teması (koyu / açık): renkler, yazı tipleri ve ortak stil yardımcıları.</summary>
    public static class Theme
    {
        // ---------- Renk paletleri (koyu / açık) ----------
        // Her renk iki temada da tanımlıdır; arayüz renkleri her zaman Theme.X üzerinden okunur. Tema değişince
        // açık pencerelerdeki denetimlerin eski temadaki renkleri yeni temadaki karşılıklarıyla değiştirilir
        // (bu yüzden bir temadaki tüm değerler birbirinden farklıdır). Açık temada metin / zemin kontrastı
        // WCAG AA'ya göre ölçüldü: ana metin ≈ 15:1, ikincil ≥ 6:1, ipucu ve durum renkleri ≥ 4.5:1.

        private sealed class Palette
        {
            public Color Window;
            public Color Sidebar;
            public Color Surface;
            public Color SurfaceHigh;
            public Color Input;
            public Color Border;
            public Color Hover;
            public Color Selection;
            public Color Text;
            public Color TextMuted;
            public Color TextFaint;
            public Color Terminal;
            public Color Accent;
            public Color AccentHover;
            public Color AccentFill;
            public Color AccentFillHover;
            public Color Success;
            public Color Warning;
            public Color Danger;
            public Color Idle;
            public Color DangerSurface;
            public Color DangerHover;
            public Color DangerPressed;
            public Color DangerIdle;
            public Color SuccessHover;
            public Color SuccessPressed;
            public Color ErrorBanner;
            public Color SuccessBanner;
            public Color SuccessBannerText;
            public Color WarningBanner;
            public Color WarningBannerText;
            public Color InfoBanner;
            public Color InfoBannerText;
            public Color StripConnecting;
            public Color StripError;
            public Color StripDropped;
            public Color DropBannerTitle;
            public Color DropBannerFaint;
            public Color AccentSurface;
            public Color DropHighlight;
            public Color DropHighlightSoft;
            public Color TreeSelectedStrong;
            public Color TreeSelectedSoft;
            public Color TreeTag;
            public Color ListSelectedStrong;
            public Color ListSelectedText;
            public Color RowDivider;
            public Color RowDividerMid;
            public Color RowDividerStrong;
            public Color ZebraRow;
            public Color ComboActive;
            public Color OffOutputBack;
            public Color OffOutputText;
            public Color OffHeaderBack;
            public Color OffInputBack;
            public Color AlertText;
            public Color CommandBarBack;
            public Color CommandFieldBack;
            public Color CommandFieldBorder;
        }

        private static readonly Palette DarkPalette = new Palette
        {
                Window = Color.FromArgb(17,19,24),
                Sidebar = Color.FromArgb(22,25,31),
                Surface = Color.FromArgb(28,32,39),
                SurfaceHigh = Color.FromArgb(36,41,50),
                Input = Color.FromArgb(14,16,20),
                Border = Color.FromArgb(58,65,78),
                Hover = Color.FromArgb(44,50,61),
                Selection = Color.FromArgb(38,64,102),
                Text = Color.FromArgb(226,230,237),
                TextMuted = Color.FromArgb(163,171,184),
                TextFaint = Color.FromArgb(128,137,151),
                Terminal = Color.FromArgb(205,214,224),
                Accent = Color.FromArgb(56,132,255),
                AccentHover = Color.FromArgb(84,152,255),
                AccentFill = Color.FromArgb(37,99,235),
                AccentFillHover = Color.FromArgb(29,84,212),
                Success = Color.FromArgb(46,194,126),
                Warning = Color.FromArgb(240,168,48),
                Danger = Color.FromArgb(248,113,113),
                Idle = Color.FromArgb(116,124,138),
                DangerSurface = Color.FromArgb(70,30,34),
                DangerHover = Color.FromArgb(72,36,40),
                DangerPressed = Color.FromArgb(90,40,44),
                DangerIdle = Color.FromArgb(56,32,36),
                SuccessHover = Color.FromArgb(22,56,42),
                SuccessPressed = Color.FromArgb(28,70,52),
                ErrorBanner = Color.FromArgb(96,22,28),
                SuccessBanner = Color.FromArgb(22,58,44),
                SuccessBannerText = Color.FromArgb(190,240,214),
                WarningBanner = Color.FromArgb(92,66,18),
                WarningBannerText = Color.FromArgb(255,226,170),
                InfoBanner = Color.FromArgb(28,52,92),
                InfoBannerText = Color.FromArgb(214,228,255),
                StripConnecting = Color.FromArgb(58,46,22),
                StripError = Color.FromArgb(60,26,30),
                StripDropped = Color.FromArgb(120,28,34),
                DropBannerTitle = Color.FromArgb(255,205,205),
                DropBannerFaint = Color.FromArgb(220,160,160),
                AccentSurface = Color.FromArgb(30,52,88),
                DropHighlight = Color.FromArgb(32,78,150),
                DropHighlightSoft = Color.FromArgb(30,58,104),
                TreeSelectedStrong = Color.FromArgb(44,78,128),
                TreeSelectedSoft = Color.FromArgb(26,46,78),
                TreeTag = Color.FromArgb(150,196,255),
                ListSelectedStrong = Color.FromArgb(52,84,130),
                ListSelectedText = Color.FromArgb(200,208,220),
                RowDivider = Color.FromArgb(32,36,44),
                RowDividerMid = Color.FromArgb(40,45,54),
                RowDividerStrong = Color.FromArgb(46,52,63),
                ZebraRow = Color.FromArgb(18,21,26),
                ComboActive = Color.FromArgb(20,23,29),
                OffOutputBack = Color.FromArgb(27,29,33),
                OffOutputText = Color.FromArgb(137,143,153),
                OffHeaderBack = Color.FromArgb(31,33,38),
                OffInputBack = Color.FromArgb(36,38,43),
                AlertText = Color.FromArgb(255,99,99),
                CommandBarBack = Color.FromArgb(22,32,50),
                CommandFieldBack = Color.FromArgb(12,18,30),
                CommandFieldBorder = Color.FromArgb(52,92,160),
        };

        private static readonly Palette LightPalette = new Palette
        {
                Window = Color.FromArgb(242,244,247),
                Sidebar = Color.FromArgb(233,236,241),
                Surface = Color.FromArgb(251,252,253),
                SurfaceHigh = Color.FromArgb(238,241,245),
                Input = Color.FromArgb(255,255,255),
                Border = Color.FromArgb(200,206,216),
                Hover = Color.FromArgb(224,229,236),
                Selection = Color.FromArgb(205,222,248),
                Text = Color.FromArgb(27,31,38),
                TextMuted = Color.FromArgb(80,88,102),
                TextFaint = Color.FromArgb(108,116,130),
                Terminal = Color.FromArgb(32,38,48),
                Accent = Color.FromArgb(29,95,215),
                AccentHover = Color.FromArgb(22,77,186),
                AccentFill = Color.FromArgb(37,99,235),
                AccentFillHover = Color.FromArgb(29,84,212),
                Success = Color.FromArgb(20,128,74),
                Warning = Color.FromArgb(168,98,0),
                Danger = Color.FromArgb(196,38,38),
                Idle = Color.FromArgb(122,130,144),
                DangerSurface = Color.FromArgb(252,226,226),
                DangerHover = Color.FromArgb(250,214,214),
                DangerPressed = Color.FromArgb(246,200,200),
                DangerIdle = Color.FromArgb(253,237,237),
                SuccessHover = Color.FromArgb(214,240,226),
                SuccessPressed = Color.FromArgb(196,232,212),
                ErrorBanner = Color.FromArgb(190,30,36),
                SuccessBanner = Color.FromArgb(21,112,68),
                SuccessBannerText = Color.FromArgb(255,255,254),
                WarningBanner = Color.FromArgb(150,92,0),
                WarningBannerText = Color.FromArgb(255,254,254),
                InfoBanner = Color.FromArgb(30,88,190),
                InfoBannerText = Color.FromArgb(254,255,255),
                StripConnecting = Color.FromArgb(255,247,230),
                StripError = Color.FromArgb(253,228,228),
                StripDropped = Color.FromArgb(185,28,34),
                DropBannerTitle = Color.FromArgb(255,236,236),
                DropBannerFaint = Color.FromArgb(255,222,222),
                AccentSurface = Color.FromArgb(214,229,252),
                DropHighlight = Color.FromArgb(180,208,250),
                DropHighlightSoft = Color.FromArgb(190,214,250),
                TreeSelectedStrong = Color.FromArgb(190,212,246),
                TreeSelectedSoft = Color.FromArgb(222,234,252),
                TreeTag = Color.FromArgb(29,95,214),
                ListSelectedStrong = Color.FromArgb(186,208,242),
                ListSelectedText = Color.FromArgb(40,48,60),
                RowDivider = Color.FromArgb(232,235,240),
                RowDividerMid = Color.FromArgb(226,230,236),
                RowDividerStrong = Color.FromArgb(218,223,231),
                ZebraRow = Color.FromArgb(247,248,250),
                ComboActive = Color.FromArgb(246,248,251),
                OffOutputBack = Color.FromArgb(236,237,240),
                OffOutputText = Color.FromArgb(100,106,116),
                OffHeaderBack = Color.FromArgb(229,231,235),
                OffInputBack = Color.FromArgb(224,226,230),
                AlertText = Color.FromArgb(200,30,30),
                CommandBarBack = Color.FromArgb(222,232,248),
                CommandFieldBack = Color.FromArgb(255,255,253),
                CommandFieldBorder = Color.FromArgb(110,150,220),
        };

        private static Palette _p = DarkPalette;

        // ---------- Tema seçimi ----------

        /// <summary>Geçerli tema.</summary>
        public static ThemeMode Mode { get; private set; }

        public static bool IsDark { get { return Mode == ThemeMode.Dark; } }

        /// <summary>Tema değişti (açık pencereler yeniden renklendirildikten sonra). Durum renklerini kendisi seçen denetimler dinler.</summary>
        public static event Action Changed;

        /// <summary>Uygulama açılırken, HİÇBİR denetim oluşturulmadan önce temayı seçer (yeniden renklendirme yapmaz).</summary>
        public static void Initialize(ThemeMode mode)
        {
            Mode = Enum.IsDefined(typeof(ThemeMode), mode) ? mode : ThemeMode.Dark;
            _p = Mode == ThemeMode.Light ? LightPalette : DarkPalette;
        }

        /// <summary>
        /// Temayı çalışırken değiştirir: açık tüm pencerelerdeki denetimlerin renkleri yeni temaya çevrilir, başlık
        /// çubukları ve kaydırma çubukları güncellenir, her şey yeniden çizilir. Bağlantılar ve terminal içerikleri etkilenmez.
        /// </summary>
        public static void SetMode(ThemeMode mode)
        {
            if (!Enum.IsDefined(typeof(ThemeMode), mode) || mode == Mode) return;
            Palette from = _p, to = mode == ThemeMode.Light ? LightPalette : DarkPalette;
            Dictionary<int, Color> map = BuildMap(from, to);
            _p = to;
            Mode = mode;

            foreach (Form f in Application.OpenForms)
            {
                f.SuspendLayout();
                Recolor(f, map);
                f.ResumeLayout(false);
                ApplyDarkTitleBar(f);
            }
            RefreshScrollBars();
            var h = Changed;
            if (h != null) h();
            foreach (Form f in Application.OpenForms) f.Invalidate(true);
        }

        private static Dictionary<int, Color> BuildMap(Palette from, Palette to)
        {
            var map = new Dictionary<int, Color>();
            foreach (System.Reflection.FieldInfo fi in typeof(Palette).GetFields())
            {
                var a = (Color)fi.GetValue(from);
                var b = (Color)fi.GetValue(to);
                int key = a.ToArgb();
                if (!map.ContainsKey(key)) map[key] = b;
            }
            return map;
        }

        private static Color MapColor(Color c, Dictionary<int, Color> map)
        {
            if (c.IsEmpty || c.A == 0) return c;                       // saydam / tanımsız
            Color n;
            if (map.TryGetValue(Color.FromArgb(255, c).ToArgb(), out n))
                return c.A == 255 ? n : Color.FromArgb(c.A, n);        // yarı saydam tonlar da çevrilir
            return c;                                                  // tema dışı renk (beyaz, simge renkleri…) olduğu gibi
        }

        /// <summary>Denetimi ve tüm alt denetimlerini (menü ve araç çubuğu öğeleri dahil) yeni temaya çevirir.</summary>
        public static void Recolor(Control c, Dictionary<int, Color> map)
        {
            try
            {
                Color back = c.BackColor, fore = c.ForeColor;
                Color nb = MapColor(back, map), nf = MapColor(fore, map);
                if (nb != back) c.BackColor = nb;
                if (nf != fore) c.ForeColor = nf;      // RichTextBox: tüm metni yeni renge boyar (uyarı satırlarını kart yeniden boyar)

                var ts = c as ToolStrip;
                if (ts != null) foreach (ToolStripItem it in ts.Items) RecolorItem(it, map);
                var ll = c as LinkLabel;
                if (ll != null)
                {
                    ll.LinkColor = MapColor(ll.LinkColor, map);
                    ll.ActiveLinkColor = MapColor(ll.ActiveLinkColor, map);
                }
                if (c.ContextMenuStrip != null) Recolor(c.ContextMenuStrip, map);
            }
            catch { }
            foreach (Control child in c.Controls) Recolor(child, map);
        }

        private static void RecolorItem(ToolStripItem it, Dictionary<int, Color> map)
        {
            it.ForeColor = MapColor(it.ForeColor, map);
            it.BackColor = MapColor(it.BackColor, map);
            var mi = it as ToolStripDropDownItem;
            if (mi != null) foreach (ToolStripItem sub in mi.DropDownItems) RecolorItem(sub, map);
        }

        /// <summary>Pencere zemini</summary>
        public static Color Window { get { return _p.Window; } }
        /// <summary>Kenar çubuğu, sekme şeridi, menü, durum çubuğu</summary>
        public static Color Sidebar { get { return _p.Sidebar; } }
        /// <summary>Kart başlığı, panel yüzeyi</summary>
        public static Color Surface { get { return _p.Surface; } }
        /// <summary>Yükseltilmiş yüzey: kutular, ikincil düğmeler, ipuçları</summary>
        public static Color SurfaceHigh { get { return _p.SurfaceHigh; } }
        /// <summary>Metin kutuları, terminal zemini</summary>
        public static Color Input { get { return _p.Input; } }
        /// <summary>Kenarlık ve ayraçlar</summary>
        public static Color Border { get { return _p.Border; } }
        /// <summary>Üzerine gelince</summary>
        public static Color Hover { get { return _p.Hover; } }
        /// <summary>Seçili satır</summary>
        public static Color Selection { get { return _p.Selection; } }
        /// <summary>Ana metin</summary>
        public static Color Text { get { return _p.Text; } }
        /// <summary>İkincil metin (her iki temada ≥ 4.5:1)</summary>
        public static Color TextMuted { get { return _p.TextMuted; } }
        /// <summary>İpucu / pasif metin</summary>
        public static Color TextFaint { get { return _p.TextFaint; } }
        /// <summary>Terminal yazısı</summary>
        public static Color Terminal { get { return _p.Terminal; } }
        /// <summary>Vurgu: METİN ve göstergeler</summary>
        public static Color Accent { get { return _p.Accent; } }
        /// <summary>Vurgu (üzerine gelince)</summary>
        public static Color AccentHover { get { return _p.AccentHover; } }
        /// <summary>Vurgu ZEMİNİ: üzerinde beyaz yazı (≥ 5:1)</summary>
        public static Color AccentFill { get { return _p.AccentFill; } }
        /// <summary>Vurgu zemini (üzerine gelince)</summary>
        public static Color AccentFillHover { get { return _p.AccentFillHover; } }
        /// <summary>Başarı / bağlı</summary>
        public static Color Success { get { return _p.Success; } }
        /// <summary>Uyarı / bağlanıyor</summary>
        public static Color Warning { get { return _p.Warning; } }
        /// <summary>Hata / tehlike</summary>
        public static Color Danger { get { return _p.Danger; } }
        /// <summary>Bağlı değil göstergesi</summary>
        public static Color Idle { get { return _p.Idle; } }
        /// <summary>Hatalı giriş kutusu, tehlikeli seçenek zemini</summary>
        public static Color DangerSurface { get { return _p.DangerSurface; } }
        /// <summary>Kırmızı düğme (üzerine gelince)</summary>
        public static Color DangerHover { get { return _p.DangerHover; } }
        /// <summary>Kırmızı düğme (basılı)</summary>
        public static Color DangerPressed { get { return _p.DangerPressed; } }
        /// <summary>Geniş kırmızı düğmenin zemini</summary>
        public static Color DangerIdle { get { return _p.DangerIdle; } }
        /// <summary>Yeşil düğme (üzerine gelince)</summary>
        public static Color SuccessHover { get { return _p.SuccessHover; } }
        /// <summary>Yeşil düğme (basılı)</summary>
        public static Color SuccessPressed { get { return _p.SuccessPressed; } }
        /// <summary>Hata şeridi / bildirim zemini (beyaz yazı)</summary>
        public static Color ErrorBanner { get { return _p.ErrorBanner; } }
        /// <summary>Başarı bildirimi zemini</summary>
        public static Color SuccessBanner { get { return _p.SuccessBanner; } }
        /// <summary>Başarı bildirimi yazısı</summary>
        public static Color SuccessBannerText { get { return _p.SuccessBannerText; } }
        /// <summary>Uyarı şeridi zemini</summary>
        public static Color WarningBanner { get { return _p.WarningBanner; } }
        /// <summary>Uyarı şeridi yazısı</summary>
        public static Color WarningBannerText { get { return _p.WarningBannerText; } }
        /// <summary>Bilgi şeridi zemini</summary>
        public static Color InfoBanner { get { return _p.InfoBanner; } }
        /// <summary>Bilgi şeridi yazısı</summary>
        public static Color InfoBannerText { get { return _p.InfoBannerText; } }
        /// <summary>Kart durum şeridi: bağlanıyor</summary>
        public static Color StripConnecting { get { return _p.StripConnecting; } }
        /// <summary>Kart durum şeridi: bağlantı yok</summary>
        public static Color StripError { get { return _p.StripError; } }
        /// <summary>Kart durum şeridi: bağlantı kesildi (beyaz yazı)</summary>
        public static Color StripDropped { get { return _p.StripDropped; } }
        /// <summary>Kopma uyarısı ayrıntı yazısı</summary>
        public static Color DropBannerTitle { get { return _p.DropBannerTitle; } }
        /// <summary>Kopma uyarısı soluk yazı</summary>
        public static Color DropBannerFaint { get { return _p.DropBannerFaint; } }
        /// <summary>Vurgulu seçenek / modül zemini</summary>
        public static Color AccentSurface { get { return _p.AccentSurface; } }
        /// <summary>Sürükle-bırak hedef satırı</summary>
        public static Color DropHighlight { get { return _p.DropHighlight; } }
        /// <summary>Sürükle-bırak hedefi (liste)</summary>
        public static Color DropHighlightSoft { get { return _p.DropHighlightSoft; } }
        /// <summary>Kart Gezgini: gezgindeki kart (seçili)</summary>
        public static Color TreeSelectedStrong { get { return _p.TreeSelectedStrong; } }
        /// <summary>Kart Gezgini: gezgindeki kart</summary>
        public static Color TreeSelectedSoft { get { return _p.TreeSelectedSoft; } }
        /// <summary>Kart Gezgini: "gezginde" etiketi</summary>
        public static Color TreeTag { get { return _p.TreeTag; } }
        /// <summary>Komut listesi: seçili rozet</summary>
        public static Color ListSelectedStrong { get { return _p.ListSelectedStrong; } }
        /// <summary>Komut listesi: seçili satırın özeti</summary>
        public static Color ListSelectedText { get { return _p.ListSelectedText; } }
        /// <summary>Satır ayracı (açık)</summary>
        public static Color RowDivider { get { return _p.RowDivider; } }
        /// <summary>Satır ayracı</summary>
        public static Color RowDividerMid { get { return _p.RowDividerMid; } }
        /// <summary>Satır ayracı (belirgin)</summary>
        public static Color RowDividerStrong { get { return _p.RowDividerStrong; } }
        /// <summary>Listede çift satır</summary>
        public static Color ZebraRow { get { return _p.ZebraRow; } }
        /// <summary>Açık liste kutusunun zemini</summary>
        public static Color ComboActive { get { return _p.ComboActive; } }
        /// <summary>Bağlı olmayan kart: terminal zemini</summary>
        public static Color OffOutputBack { get { return _p.OffOutputBack; } }
        /// <summary>Bağlı olmayan kart: terminal yazısı</summary>
        public static Color OffOutputText { get { return _p.OffOutputText; } }
        /// <summary>Bağlı olmayan kart: başlık</summary>
        public static Color OffHeaderBack { get { return _p.OffHeaderBack; } }
        /// <summary>Bağlı olmayan kart: komut satırı</summary>
        public static Color OffInputBack { get { return _p.OffInputBack; } }
        /// <summary>Terminaldeki "!!!" uyarı satırları</summary>
        public static Color AlertText { get { return _p.AlertText; } }
        /// <summary>Genel komut çubuğu zemini</summary>
        public static Color CommandBarBack { get { return _p.CommandBarBack; } }
        /// <summary>Genel komut çubuğu: komut alanı</summary>
        public static Color CommandFieldBack { get { return _p.CommandFieldBack; } }
        /// <summary>Genel komut çubuğu: komut alanı kenarlığı</summary>
        public static Color CommandFieldBorder { get { return _p.CommandFieldBorder; } }

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
                    b.FlatAppearance.MouseOverBackColor = DangerHover;
                    b.FlatAppearance.MouseDownBackColor = DangerPressed;
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

        /// <summary>Başlık çubuğunu temaya uydurur: koyu temada koyu, açık temada açık (Windows 10 1809+). Desteklenmiyorsa sessizce geçer.</summary>
        public static void ApplyDarkTitleBar(Form form)
        {
            try
            {

                int on = IsDark ? 1 : 0;
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

        private static readonly List<WeakReference> ScrollBarOwners = new List<WeakReference>();

        private static void ApplyScrollTheme(Control c)
        {
            try { SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null); c.Invalidate(); } catch { }
        }

        /// <summary>Kaydırma çubuklarını temaya uydurur (Windows 10+); tema değişince kendiliğinden güncellenir. Desteklenmiyorsa sessizce geçer.</summary>
        public static void ApplyDarkScrollBars(Control c)
        {
            try
            {
                lock (ScrollBarOwners) ScrollBarOwners.Add(new WeakReference(c));
                if (c.IsHandleCreated) ApplyScrollTheme(c);
                c.HandleCreated += (s, e) => ApplyScrollTheme(c);
            }
            catch { }
        }

        private static void RefreshScrollBars()
        {
            lock (ScrollBarOwners)
            {
                ScrollBarOwners.RemoveAll(w => { var c = w.Target as Control; return c == null || c.IsDisposed; });
                foreach (WeakReference w in ScrollBarOwners)
                {
                    var c = w.Target as Control;
                    if (c != null && c.IsHandleCreated) ApplyScrollTheme(c);
                }
            }
        }
    }
}
