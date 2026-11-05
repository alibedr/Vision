using System;
using System.Collections.Generic;
using System.Text;

namespace TelnetCards
{
    /// <summary>
    /// Karttan gelen satır sonlarını tek bir '\n'e çevirir. Durumludur: paketlere bölünmüş
    /// satır sonlarını da doğru birleştirir.
    ///   \r\n, \n\r, \r\r\n  → tek satır sonu   (eskiden bazı varyantlar çift satır üretiyordu)
    ///   tek \r ya da tek \n → tek satır sonu
    ///   \r\0 (telnet CR-NUL) → tek satır sonu
    /// </summary>
    public sealed class LineNormalizer
    {
        // Az önce bir satır sonu yazıldıysa, onunla çift oluşturabilecek karakter ('\0' = yok)
        private char _partner;

        public string Process(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\0') continue;                       // telnet CR NUL dolgusu

                if (c == '\r' || c == '\n')
                {
                    if (c == _partner)
                    {
                        _partner = '\0';                       // çiftin ikinci yarısı: yut
                        continue;
                    }
                    if (c == '\r' && _partner == '\n')
                        continue;                              // \r\r\n içindeki fazladan \r: yut, \n'yi beklemeye devam

                    sb.Append('\n');
                    _partner = c == '\r' ? '\n' : '\r';
                    continue;
                }

                _partner = '\0';
                sb.Append(c);
            }
            return sb.ToString();
        }

        public void Reset()
        {
            _partner = '\0';
        }
    }

    /// <summary>
    /// Yerel eko açıkken, kartın aynı komutu / Enter'ı geri ekolamasından doğan tekrarları ayıklar.
    /// Cihaz Enter'ı "\r\n" olarak ekolar; yerel eko da satırı bitirdiği için eskiden araya boş satır giriyordu.
    /// Yalnızca gönderimden hemen sonra gelen ve beklenen ekoyla birebir eşleşen veri yutulur;
    /// cihaz cevabı asla kırpılmaz.
    /// </summary>
    public sealed class EchoFilter
    {
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

        private string _expectCommand;    // cihaz komut metnini de ekolarsa beklenen "cmd\n"
        private bool _expectNewline;      // cihaz yalnızca Enter'ı ekolarsa beklenen "\n"
        private DateTime _expiresUtc;

        /// <summary>Komut gönderildiğinde çağrılır (yerel eko satırı zaten ekrana yazılmıştır).</summary>
        public void Expect(string command, DateTime nowUtc)
        {
            _expectCommand = command.Length > 0 ? command + "\n" : null;
            _expectNewline = true;
            _expiresUtc = nowUtc + Window;
        }

        /// <summary>Gelen (normalize edilmiş) metinden beklenen ekoyu çıkarır.</summary>
        public string Filter(string text, DateTime nowUtc)
        {
            if (!_expectNewline || text.Length == 0) return text;
            if (nowUtc > _expiresUtc) { Clear(); return text; }

            // 1) Cihaz komutu ve Enter'ı birlikte ekoladı: "cmd\n..." → "..."
            if (_expectCommand != null && text.StartsWith(_expectCommand, StringComparison.Ordinal))
            {
                int echoLength = _expectCommand.Length;   // Clear() alanı sıfırlamadan önce al
                Clear();
                return text.Substring(echoLength);
            }
            // 2) Cihaz yalnızca Enter'ı ekoladı: "\n..." → "..."
            if (text[0] == '\n')
            {
                Clear();
                return text.Substring(1);
            }
            // 3) Eko yok: cihaz doğrudan cevap verdi; dokunma
            Clear();
            return text;
        }

        public void Clear()
        {
            _expectCommand = null;
            _expectNewline = false;
        }
    }

    /// <summary>
    /// Kart başına komut geçmişi. Yukarı ok daha eski komuta, aşağı ok daha yeniye gider;
    /// en yeninin de ötesine inince kullanıcının yazmakta olduğu metne (taslağa) döner.
    /// Art arda aynı komut ve boş komut saklanmaz.
    /// </summary>
    public sealed class CommandHistory
    {
        private const int MaxItems = 500;

        private readonly List<string> _items = new List<string>();
        private int _index = -1;          // -1: geçmişte gezinilmiyor
        private string _draft = "";

        public int Count { get { return _items.Count; } }

        public IList<string> Items { get { return _items.AsReadOnly(); } }

        /// <summary>
        /// Komutu geçmişe ekler. Aynı komut daha önce varsa yinelenmez, en yeniye taşınır
        /// (↑ ve otomatik tamamlama en son kullanılanı önce bulur). Boş komut saklanmaz.
        /// </summary>
        public void Add(string command)
        {
            if (!string.IsNullOrEmpty(command))
            {
                int existing = _items.LastIndexOf(command);
                if (existing != _items.Count - 1 || _items.Count == 0)
                {
                    if (existing >= 0) _items.RemoveAt(existing);
                    _items.Add(command);
                    if (_items.Count > MaxItems) _items.RemoveAt(0);
                }
            }
            ResetNavigation();
        }

        /// <summary>↑/↓ ile geçmişte geziniliyor mu?</summary>
        public bool IsNavigating { get { return _index != -1; } }

        /// <summary>
        /// Otomatik tamamlama önerisi: yazılanla başlayan (büyük/küçük harf duyarsız), ondan uzun, EN SON kullanılan komut.
        /// Öneri yoksa null.
        /// </summary>
        public string Suggest(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return null;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                string c = _items[i];
                if (c.Length > typed.Length && c.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        /// <summary>Daha eski komut. Gidilecek yer yoksa null (metin değişmesin).</summary>
        public string Previous(string currentText)
        {
            if (_items.Count == 0) return null;
            if (_index == -1)
            {
                _draft = currentText ?? "";
                _index = _items.Count - 1;
            }
            else if (_index > 0) _index--;
            else return null;
            return _items[_index];
        }

        /// <summary>Daha yeni komut; en yeninin ötesinde taslak döner. Gezinilmiyorsa null.</summary>
        public string Next()
        {
            if (_index == -1) return null;
            _index++;
            if (_index >= _items.Count)
            {
                string draft = _draft;
                ResetNavigation();
                return draft;
            }
            return _items[_index];
        }

        public void ResetNavigation()
        {
            _index = -1;
            _draft = "";
        }
    }
}

namespace TelnetCards
{
    /// <summary>
    /// "↓ Yeni Satırlar (N)" göstergesinin durumu (arayüzden bağımsız, test edilebilir).
    /// Kural: kullanıcı aşağıda değilken gelen ilk satırda bir kez görünür, sonra yalnız sayaç artar.
    /// Yalnız KULLANICININ en alta inmesi (ya da göstergeye tıklaması) gizler ve sıfırlar; program metin
    /// eklerken oluşan geçici kaymalar (BeginAppend/EndAppend arası) dikkate alınmaz.
    /// </summary>
    public sealed class NewLinesIndicator
    {
        private bool _appending;

        public bool Visible { get; private set; }
        public int Count { get; private set; }

        /// <summary>Gösterge metni (yalnız sayaç değişir; görünürlük her satırda yeniden kurulmaz).</summary>
        public string Text
        {
            get { return "↓  Yeni Satırlar (" + (Count > 999 ? "999+" : Count.ToString()) + ")"; }
        }

        public void BeginAppend() { _appending = true; }

        /// <summary>Ekleme bitti. userWasAtBottom: kullanıcı eklemeden ÖNCE en alttaydı (akışı izliyordu).</summary>
        public void EndAppend(int newLines, bool userWasAtBottom)
        {
            _appending = false;
            if (userWasAtBottom) { Reset(); return; }   // en alttaysa akış izlenir, gösterge yok
            if (newLines <= 0) return;
            Count += newLines;
            Visible = true;                             // zaten görünürse yalnız sayaç değişir
        }

        /// <summary>Kaydırma olayı. Program eklerken gelenler yok sayılır.</summary>
        public void OnScroll(bool atBottom)
        {
            if (_appending) return;
            if (Visible && atBottom) Reset();
        }

        /// <summary>Göstergeye tıklandı (çağıran en alta kaydırır).</summary>
        public void OnClick() { Reset(); }

        public void Reset()
        {
            Visible = false;
            Count = 0;
        }
    }
}

namespace TelnetCards
{
    /// <summary>Terminal satır sınırı hesabı (arayüzden bağımsız, test edilebilir).</summary>
    public static class LineBudget
    {
        /// <summary>
        /// Metin kutusundaki satır durumu: lastLineIndex = son satırın sıfır tabanlı numarası,
        /// endsWithNewline = metin '\n' ile bitiyor (son satır boş). Silinmesi gereken en eski satır sayısını döndürür.
        /// </summary>
        public static int ExcessLines(int lastLineIndex, bool endsWithNewline, int limit)
        {
            int count = lastLineIndex + (endsWithNewline ? 0 : 1);
            return Math.Max(0, count - Math.Max(1, limit));
        }
    }
}
