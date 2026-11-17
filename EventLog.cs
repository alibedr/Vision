using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Vision
{
    /// <summary>Olay günlüğündeki tek kayıt.</summary>
    public sealed class CardEvent
    {
        public DateTime Time { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Kart olay günlüğü (G49): her kartın kullanıcıya yönelik zaman çizelgesi — bağlandı / koptu, yeniden başlatma,
    /// boot ayarı değişikliği, Sentinel, dosya işlemleri ve aktarımlar. logs\olaylar\&lt;ip[_port]&gt;.log dosyasına
    /// "zaman TAB tür TAB olay" satırları olarak yazılır. Kart başına en fazla 5 MB tutulur; dolunca en eski kayıtlar silinir.
    /// hatalar.log'dan farkı: o geliştiriciye yönelik teknik ayrıntıdır, bu kullanıcının okuyacağı olay listesidir.
    /// </summary>
    public static class CardEventLog
    {
        public const string Connection = "Bağlantı";
        public const string Reboot = "Yeniden başlatma";
        public const string Boot = "Boot ayarı";
        public const string Sentinel = "Sentinel";
        public const string Files = "Dosya";
        public const string Transfer = "Aktarım";

        public static readonly string[] Categories = { Connection, Reboot, Boot, Sentinel, Files, Transfer };

        /// <summary>Kart başına günlük dosyasının üst sınırı.</summary>
        public const long MaxBytes = 5L * 1024 * 1024;
        /// <summary>Sınır aşılınca en eski kayıtlar silinerek bu boyuta inilir (her yazışta kırpmamak için pay bırakılır).</summary>
        public const long TrimToBytes = MaxBytes * 9 / 10;

        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly object Lock = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Testler için klasör değiştirilebilir (null: logs\olaylar).</summary>
        internal static string FolderOverride;

        public static string Folder
        {
            get
            {
                return FolderOverride ?? Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"), "olaylar");
            }
        }

        /// <summary>Kartın günlük anahtarı: IP (port 23 değilse "_port" eklenir).</summary>
        public static string KeyFor(string ip, int port)
        {
            string k = (ip ?? "").Trim();
            if (port != 23 && port > 0) k += "_" + port;
            foreach (char c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_');
            return k.Length == 0 ? "bilinmeyen" : k;
        }

        public static string KeyFor(CardConfig card)
        {
            return card == null ? "bilinmeyen" : KeyFor(card.Ip, card.Group != null ? card.Group.Port : 23);
        }

        public static string PathFor(string key) { return Path.Combine(Folder, key + ".log"); }

        public static void Write(CardConfig card, string category, string message)
        {
            Write(KeyFor(card), category, message, DateTime.Now);
        }

        /// <summary>Kaydı ekler. Hata olursa sessizce geçilir (günlük yazılamadı diye iş durmaz).</summary>
        public static void Write(string key, string category, string message, DateTime time)
        {
            try
            {
                string line = time.ToString(TimeFormat, CultureInfo.InvariantCulture) + "\t" + Clean(category) + "\t" + Clean(message) + "\r\n";
                string path = PathFor(key);
                lock (Lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.AppendAllText(path, line, Utf8);
                    var fi = new FileInfo(path);
                    if (fi.Length > MaxBytes) TrimOldest(path, TrimToBytes);
                }
            }
            catch { }
        }

        /// <summary>Dosyayı en yeni kayıtlar kalacak şekilde <paramref name="targetBytes"/> boyutunun altına indirir.</summary>
        internal static void TrimOldest(string path, long targetBytes)
        {
            string[] lines = File.ReadAllLines(path, Utf8);
            long total = 0;
            int first = lines.Length;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                long size = Utf8.GetByteCount(lines[i]) + 2;
                if (total + size > targetBytes) break;
                total += size;
                first = i;
            }
            var sb = new StringBuilder();
            for (int i = first; i < lines.Length; i++) sb.Append(lines[i]).Append("\r\n");
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), Utf8);
            File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Kartın kayıtları (eskiden yeniye). Dosya yoksa boş liste.</summary>
        public static List<CardEvent> Read(string key)
        {
            var list = new List<CardEvent>();
            try
            {
                string path = PathFor(key);
                string[] lines;
                lock (Lock)
                {
                    if (!File.Exists(path)) return list;
                    lines = File.ReadAllLines(path, Utf8);
                }
                foreach (string l in lines)
                {
                    CardEvent e = Parse(l);
                    if (e != null) list.Add(e);
                }
            }
            catch { }
            return list;
        }

        internal static CardEvent Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] p = line.Split(new[] { '\t' }, 3);
            DateTime t;
            if (p.Length < 3 || !DateTime.TryParseExact(p[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return null;
            return new CardEvent { Time = t, Category = p[1], Message = p[2] };
        }

        /// <summary>Kaydı CSV satırına çevirir (Excel için noktalı virgül ayraçlı).</summary>
        public static string ToCsv(IEnumerable<CardEvent> events)
        {
            var sb = new StringBuilder();
            sb.Append("Zaman;Tür;Olay\r\n");
            foreach (CardEvent e in events)
            {
                sb.Append(e.Time.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append(';')
                  .Append(CsvField(e.Category)).Append(';')
                  .Append(CsvField(e.Message)).Append("\r\n");
            }
            return sb.ToString();
        }

        private static string CsvField(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        }
    }
}
