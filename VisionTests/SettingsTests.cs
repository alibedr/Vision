using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Vision;

namespace VisionTests
{
    /// <summary>Ayarlar, hızlı komutlar ve tema paleti.</summary>
    public sealed class SettingsTests
    {
        [Test]
        public void HizliKomut_EskiTekKomutListeyeAlinir()
        {
            var c = new CardConfig { Ip = "10.0.0.5", QuickCommand = "i" };
            c.NormalizeQuickCommands();
            Check.Equal(1, c.QuickCommands.Count, "liste");
            Check.Equal("i", c.QuickCommands[0], "eski komut");
            Check.Equal("i", c.QuickCommand, "seçili");
        }

        [Test]
        public void HizliKomut_EnFazlaBesYinelenenVeBosAtilir()
        {
            List<string> list = new List<string> { "a", " ", "b", "a", "c", "d", "e", "f", "g" };
            string active = "z";
            QuickCommandSet.Normalize(ref list, ref active);
            Check.Equal(5, list.Count, "en fazla 5");
            Check.True(list.Distinct().Count() == list.Count, "yinelenen olmamalı");
            Check.Equal("a", active, "listede olmayan seçili komut yer yoksa ilk komut seçilir");
        }

        [Test]
        public void ParcaliAktarim_KartBellegiSiniri()
        {
            // Kartın RAM'i: bir parça + ara bellek (G42.6 / G42.7): en fazla ~72 MB
            Check.Equal(64L * 1024 * 1024, FileBrowser.PartBytes, "parça");
            Check.Equal(8 * 1024 * 1024, FileBrowser.ChunkBytes, "ara bellek");
            Check.True(FileBrowser.PartThresholdBytes <= FileBrowser.PartBytes, "eşik parçadan büyük olmamalı");
        }

        [Test]
        public void KopyaSuresi_BoyutlaArtar()
        {
            int small = FileBrowser.CopyTimeoutSec(1024);
            int big = FileBrowser.CopyTimeoutSec(2684354560L);
            Check.True(small >= 300, "en az 5 dk");
            Check.True(big > small + 2000, "2,5 GB'ta çok daha uzun (" + big + " sn)");
        }

        /// <summary>
        /// G44.3: tema geçişi renkleri değerden değere çevirdiği için her temanın paletindeki renkler birbirinden farklı
        /// olmalı ve saf beyaz / siyah ya da Windows sistem renkleri olmamalı (Yüksek kontrast'taki #f0f0f0 hatası).
        /// </summary>
        [Test]
        public void Tema_PaletRenkleriBenzersizVeSistemRengiDegil()
        {
            var reserved = new HashSet<int> { Color.White.ToArgb(), Color.Black.ToArgb() };
            foreach (KnownColor k in Enum.GetValues(typeof(KnownColor)))
            {
                Color c = Color.FromKnownColor(k);
                if (c.IsSystemColor && c.A == 255) reserved.Add(Color.FromArgb(255, c).ToArgb());
            }
            FieldInfo pField = typeof(Theme).GetField("_p", BindingFlags.NonPublic | BindingFlags.Static);
            Check.NotNull(pField, "Theme._p");
            string original = Theme.CurrentThemeId;
            try
            {
                foreach (ThemeDefinition def in ThemeCatalog.All)
                {
                    Theme.Initialize(def.Id, null);
                    object palette = pField.GetValue(null);
                    var seen = new Dictionary<int, string>();
                    foreach (FieldInfo f in palette.GetType().GetFields())
                    {
                        int argb = ((Color)f.GetValue(palette)).ToArgb();
                        Check.True(!reserved.Contains(argb), def.Id + ": " + f.Name + " bir sistem rengi / saf beyaz-siyah");
                        string other;
                        Check.True(!seen.TryGetValue(argb, out other), def.Id + ": " + f.Name + " ile " + other + " aynı renk");
                        seen[argb] = f.Name;
                    }
                }
            }
            finally
            {
                Theme.Initialize(original, null);
            }
        }
    }
}
