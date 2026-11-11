using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;

namespace Vision
{
    /// <summary>
    /// Kullanıcının bir temada değiştirebildiği renkler. Arayüzün ~60 rengi bu 10 renkten türetilir (Theme.BuildPalette):
    /// ör. ikincil yazı = yazı ile arka plan arası bir ton, uyarı şeridi = uyarı rengi ile arka plan arası.
    /// </summary>
    public enum ThemeColorKey
    {
        Window = 0,
        Surface = 1,
        Border = 2,
        Text = 3,
        Accent = 4,
        Success = 5,
        Warning = 6,
        Danger = 7,
        Input = 8,
        Terminal = 9,
    }

    /// <summary>Düzenleyicide gösterilen ad ve açıklamalar.</summary>
    public static class ThemeColorInfo
    {
        public const int Count = 10;

        public static string Label(ThemeColorKey k)
        {
            switch (k)
            {
                case ThemeColorKey.Window: return "Arka plan";
                case ThemeColorKey.Surface: return "Paneller";
                case ThemeColorKey.Border: return "Kenarlıklar";
                case ThemeColorKey.Text: return "Yazı";
                case ThemeColorKey.Accent: return "Vurgu";
                case ThemeColorKey.Success: return "Başarılı / bağlı";
                case ThemeColorKey.Warning: return "Uyarı";
                case ThemeColorKey.Danger: return "Hata";
                case ThemeColorKey.Input: return "Terminal zemini";
                case ThemeColorKey.Terminal: return "Terminal yazısı";
                default: return k.ToString();
            }
        }

        public static string Description(ThemeColorKey k)
        {
            switch (k)
            {
                case ThemeColorKey.Window: return "Pencerenin genel zemini, kenar çubuğu";
                case ThemeColorKey.Surface: return "Kart başlıkları, diyaloglar, düğmeler";
                case ThemeColorKey.Border: return "Çerçeveler ve ayırıcı çizgiler";
                case ThemeColorKey.Text: return "Ana yazı; ikincil yazılar bundan türetilir";
                case ThemeColorKey.Accent: return "Seçim, odak, birincil düğmeler, bağlantılar";
                case ThemeColorKey.Success: return "Bağlı durum, başarılı işlem";
                case ThemeColorKey.Warning: return "Bağlanıyor, uyarı şeritleri";
                case ThemeColorKey.Danger: return "Hata, kopan bağlantı, uyarı satırları";
                case ThemeColorKey.Input: return "Kart ekranı ve komut alanlarının zemini";
                case ThemeColorKey.Terminal: return "Kart ekranındaki çıktı yazısı";
                default: return "";
            }
        }
    }

    /// <summary>Hazır tema: adı ve 10 temel rengin varsayılan değerleri.</summary>
    public sealed class ThemeDefinition
    {
        public ThemeDefinition(string id, string name, string description, params Color[] colors)
        {
            if (colors == null || colors.Length != ThemeColorInfo.Count) throw new ArgumentException("10 renk gerekli");
            Id = id;
            Name = name;
            Description = description;
            Defaults = colors;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        /// <summary>ThemeColorKey sırasıyla varsayılan renkler (değiştirmeyin; kopyası alınır).</summary>
        public Color[] Defaults { get; private set; }

        public Color[] CopyDefaults() { return (Color[])Defaults.Clone(); }
    }

    /// <summary>Vision ile gelen hazır temalar. Koyu ve Açık, önceki sürümlerin iki teması ile birebir aynıdır.</summary>
    public static class ThemeCatalog
    {
        public const string DarkId = "koyu";
        public const string LightId = "acik";

        private static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        // Sıra: Arka plan, Paneller, Kenarlıklar, Yazı, Vurgu, Başarılı, Uyarı, Hata, Terminal zemini, Terminal yazısı
        public static readonly IList<ThemeDefinition> All = new List<ThemeDefinition>
        {
            new ThemeDefinition(DarkId, "Koyu", "Vision'ın varsayılan koyu teması",
                C(17,19,24), C(28,32,39), C(58,65,78), C(226,230,237), C(56,132,255),
                C(46,194,126), C(240,168,48), C(248,113,113), C(14,16,20), C(205,214,224)),
            new ThemeDefinition(LightId, "Açık", "Aydınlık ortamlar için açık tema",
                C(242,244,247), C(251,252,253), C(200,206,216), C(27,31,38), C(29,95,215),
                C(20,128,74), C(168,98,0), C(196,38,38), C(255,255,255), C(32,38,48)),
            new ThemeDefinition("solarized", "Solarized", "Göz yormayan, düşük parlaklıkta mavi-yeşil tonlar",
                C(0,43,54), C(7,54,66), C(41,79,90), C(238,232,213), C(38,139,210),
                C(133,153,0), C(181,137,0), C(220,50,47), C(0,33,43), C(147,161,161)),
            new ThemeDefinition("monokai", "Monokai", "Kod düzenleyicilerden tanıdık sıcak koyu tema",
                C(39,40,34), C(49,50,43), C(73,72,62), C(248,248,242), C(102,217,239),
                C(166,226,46), C(230,219,116), C(249,38,114), C(30,31,26), C(248,248,240)),
            // Visual Studio Code "Dark Modern" (varsayılan koyu tema) renkleri
            new ThemeDefinition("vscode-dark-modern", "VS Code Dark Modern", "Visual Studio Code'un varsayılan koyu teması (Dark Modern)",
                C(24,24,24), C(31,31,31), C(43,43,43), C(204,204,204), C(0,120,212),
                C(46,160,67), C(204,167,0), C(248,81,73), C(24,24,24), C(204,204,204)),
            new ThemeDefinition("yuksek-kontrast", "Yüksek kontrast", "En yüksek okunabilirlik: siyah zemin, parlak yazı",
                C(8,8,8), C(20,20,20), C(170,170,170), C(255,255,254), C(26,188,255),
                C(0,230,118), C(255,214,0), C(255,82,82), C(3,3,3), C(240,240,240)),
            // Kimlik "klasik-terminal" kalır: bu temayı seçip kaydetmiş kullanıcıların ayarı bozulmaz
            new ThemeDefinition("klasik-terminal", "Matrix", "Siyah zemin üzerinde yeşil fosforlu, Matrix tarzı görünüm",
                C(14,19,14), C(22,30,22), C(44,66,44), C(196,232,196), C(46,204,64),
                C(140,240,140), C(230,200,60), C(240,90,80), C(5,9,5), C(51,255,51)),
        };

        public static ThemeDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static ThemeDefinition FindOrDefault(string id)
        {
            return Find(id) ?? All[0];
        }

        /// <summary>Hazır temanın renkleri + kullanıcının o temada kaydettiği değişiklikler.</summary>
        public static Color[] EffectiveColors(ThemeDefinition def, IList<ThemeCustomization> customizations)
        {
            Color[] colors = def.CopyDefaults();
            ThemeCustomization c = FindCustomization(customizations, def.Id);
            if (c != null && c.Colors != null)
                foreach (ThemeColorValue v in c.Colors)
                {
                    Color parsed;
                    if (Enum.IsDefined(typeof(ThemeColorKey), v.Key) && TryParseHex(v.Hex, out parsed)) colors[(int)v.Key] = parsed;
                }
            return colors;
        }

        public static ThemeCustomization FindCustomization(IList<ThemeCustomization> list, string id)
        {
            if (list == null) return null;
            return list.FirstOrDefault(c => c != null && string.Equals(c.ThemeId, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Kullanıcı bu temada renk değiştirmiş mi?</summary>
        public static bool IsCustomized(IList<ThemeCustomization> list, string id)
        {
            ThemeCustomization c = FindCustomization(list, id);
            return c != null && c.Colors != null && c.Colors.Count > 0;
        }

        /// <summary>
        /// Temanın renklerini kaydeder: yalnız varsayılandan FARKLI renkler saklanır; hepsi varsayılansa kayıt silinir.
        /// </summary>
        public static void StoreCustomization(List<ThemeCustomization> list, ThemeDefinition def, Color[] colors)
        {
            list.RemoveAll(c => c == null || string.Equals(c.ThemeId, def.Id, StringComparison.OrdinalIgnoreCase));
            var diff = new List<ThemeColorValue>();
            for (int i = 0; i < ThemeColorInfo.Count; i++)
                if (colors[i].ToArgb() != def.Defaults[i].ToArgb())
                    diff.Add(new ThemeColorValue { Key = (ThemeColorKey)i, Hex = ToHex(colors[i]) });
            if (diff.Count > 0) list.Add(new ThemeCustomization { ThemeId = def.Id, Colors = diff });
        }

        public static string ToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public static bool TryParseHex(string s, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim().TrimStart('#');
            if (t.Length != 6) return false;
            int v;
            if (!int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out v)) return false;
            color = Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
            return true;
        }
    }

    /// <summary>Kullanıcının bir hazır temada yaptığı renk değişiklikleri (config.xml'de saklanır).</summary>
    public sealed class ThemeCustomization
    {
        [XmlAttribute("Tema")] public string ThemeId { get; set; }
        [XmlElement("Renk")] public List<ThemeColorValue> Colors { get; set; }
    }

    public sealed class ThemeColorValue
    {
        [XmlAttribute("Ad")] public ThemeColorKey Key { get; set; }
        [XmlAttribute("Deger")] public string Hex { get; set; }
    }
}
