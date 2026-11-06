using System;
using System.Collections.Generic;
using System.Drawing;

namespace Vision
{
    /// <summary>Kart ekranında renklendirilecek bir metin ve rengi (zemin rengi).</summary>
    public sealed class HighlightRule
    {
        public HighlightRule(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        public string Text { get; private set; }
        public Color Color { get; set; }

        /// <summary>Renkli zemin üzerinde okunur yazı rengi (açık zeminde siyah, koyu zeminde beyaz).</summary>
        public Color TextColor { get { return HighlightPalette.ContrastText(Color); } }
    }

    /// <summary>
    /// Bir kartın renklendirme kuralları. Eşleşme büyük/küçük harf duyarsızdır ("error" → "ERROR", "Error" da boyanır).
    /// Aynı metin ikinci kez eklenirse yalnız rengi değişir. Arayüzden bağımsız (test edilebilir).
    /// </summary>
    public sealed class HighlightRules
    {
        public const int MaxRules = 30;
        public const int MaxTextLength = 200;

        private readonly List<HighlightRule> _rules = new List<HighlightRule>();

        public IList<HighlightRule> Items { get { return _rules.AsReadOnly(); } }
        public int Count { get { return _rules.Count; } }

        /// <summary>Kural eklenince / değişince / silinince artar.</summary>
        public int Version { get; private set; }

        /// <summary>Seçimden renklendirilecek metni çıkarır: tek satır, kenar boşlukları kırpılmış. Uygun değilse null.</summary>
        public static string Normalize(string selection)
        {
            if (selection == null) return null;
            string t = selection.Trim();
            if (t.Length == 0 || t.Length > MaxTextLength || t.IndexOf('\n') >= 0 || t.IndexOf('\r') >= 0) return null;
            return t;
        }

        public HighlightRule Find(string text)
        {
            if (text == null) return null;
            foreach (HighlightRule r in _rules)
                if (string.Equals(r.Text, text, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>Metni verilen renkle boyar (varsa rengini değiştirir). Eklenemezse (sınır / geçersiz) false.</summary>
        public bool Set(string text, Color color)
        {
            string t = Normalize(text);
            if (t == null) return false;
            HighlightRule existing = Find(t);
            if (existing != null)
            {
                existing.Color = color;
                _rules.Remove(existing);
                _rules.Add(existing);         // en son değiştirilen üstte boyansın (çakışmada o görünür)
            }
            else
            {
                if (_rules.Count >= MaxRules) return false;
                _rules.Add(new HighlightRule(t, color));
            }
            Version++;
            return true;
        }

        public bool Remove(string text)
        {
            HighlightRule r = Find(text);
            if (r == null) return false;
            _rules.Remove(r);
            Version++;
            return true;
        }

        public void Clear()
        {
            if (_rules.Count == 0) return;
            _rules.Clear();
            Version++;
        }

        /// <summary>
        /// Metindeki tüm eşleşmeler (başlangıç, uzunluk, kural), kuralların eklenme sırasıyla. Çakışmada sonraki kural
        /// sonra boyanacağı için üstte görünür.
        /// </summary>
        public List<HighlightMatch> Matches(string text)
        {
            var list = new List<HighlightMatch>();
            if (string.IsNullOrEmpty(text) || _rules.Count == 0) return list;
            foreach (HighlightRule r in _rules)
            {
                int i = 0;
                while ((i = text.IndexOf(r.Text, i, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    list.Add(new HighlightMatch(i, r.Text.Length, r));
                    i += r.Text.Length;
                }
            }
            return list;
        }
    }

    public struct HighlightMatch
    {
        public HighlightMatch(int start, int length, HighlightRule rule)
        {
            Start = start;
            Length = length;
            Rule = rule;
        }

        public readonly int Start;
        public readonly int Length;
        public readonly HighlightRule Rule;
    }

    /// <summary>Sağ tık menüsündeki hazır renkler.</summary>
    public static class HighlightPalette
    {
        public static readonly KeyValuePair<string, Color>[] Colors =
        {
            new KeyValuePair<string, Color>("Sarı", Color.FromArgb(255, 214, 0)),
            new KeyValuePair<string, Color>("Turuncu", Color.FromArgb(255, 140, 0)),
            new KeyValuePair<string, Color>("Kırmızı", Color.FromArgb(229, 57, 53)),
            new KeyValuePair<string, Color>("Pembe", Color.FromArgb(236, 64, 122)),
            new KeyValuePair<string, Color>("Mor", Color.FromArgb(142, 108, 214)),
            new KeyValuePair<string, Color>("Mavi", Color.FromArgb(30, 136, 229)),
            new KeyValuePair<string, Color>("Camgöbeği", Color.FromArgb(0, 188, 212)),
            new KeyValuePair<string, Color>("Yeşil", Color.FromArgb(67, 160, 71)),
        };

        /// <summary>Zemin rengine göre okunur yazı rengi (göreli parlaklık).</summary>
        public static Color ContrastText(Color back)
        {
            double l = 0.2126 * Lin(back.R) + 0.7152 * Lin(back.G) + 0.0722 * Lin(back.B);
            // Siyahla ve beyazla karşıtlık oranı karşılaştırılır; büyük olan seçilir
            double withBlack = (l + 0.05) / 0.05;
            double withWhite = 1.05 / (l + 0.05);
            return withBlack >= withWhite ? Color.Black : Color.White;
        }

        private static double Lin(int c)
        {
            double v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        /// <summary>Renk adı (hazır renklerden biriyse adı, değilse #RRGGBB).</summary>
        public static string NameOf(Color c)
        {
            foreach (var kv in Colors)
                if (kv.Value.ToArgb() == c.ToArgb()) return kv.Key;
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
    }
}
