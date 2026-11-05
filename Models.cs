using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml.Serialization;

namespace TelnetCards
{
    /// <summary>Tek bir kartın yapılandırması.</summary>
    public sealed class CardConfig
    {
        [XmlAttribute] public string Ip { get; set; }

        /// <summary>
        /// İsteğe bağlı görünen ad (ör. "Haberlesme"). Yalnız gösterim içindir; bağlantı ve komutlar her zaman Ip ile yapılır.
        /// </summary>
        [XmlAttribute("Ad")] public string Name { get; set; }

        /// <summary>Kartın bağlı olduğu grup (kaydedilmez, yüklemede bağlanır).</summary>
        [XmlIgnore] public CardGroup Group { get; set; }

        /// <summary>Teknik kimlik (log / tanılama): "IP:port".</summary>
        [XmlIgnore] public string DisplayName { get { return Ip + ":" + (Group != null ? Group.Port : 23); } }

        [XmlIgnore] public bool HasName { get { return !string.IsNullOrWhiteSpace(Name); } }

        /// <summary>Kullanıcıya gösterilen etiket: ad yoksa yalnız IP, varsa "IP (Ad)".</summary>
        [XmlIgnore] public string Label { get { return HasName ? Ip + " (" + Name.Trim() + ")" : Ip; } }

        public const int MaxNameLength = 40;

        /// <summary>Görünen ad geçerliyse null, değilse Türkçe hata (boş = adı kaldır, geçerli).</summary>
        public static string ValidateName(string name)
        {
            string n = (name ?? "").Trim();
            if (n.Length == 0) return null;
            if (n.Length > MaxNameLength) return "Ad en fazla " + MaxNameLength + " karakter olabilir.";
            foreach (char c in n) if (c < 32) return "Ad tek satır olmalı.";
            return null;
        }
    }

    /// <summary>Aynı port üzerinden konuşan kartlardan oluşan grup.</summary>
    public sealed class CardGroup
    {
        public CardGroup()
        {
            Cards = new List<CardConfig>();
            Port = 23;
            Expanded = true;
        }

        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public int Port { get; set; }
        [XmlAttribute] public bool Expanded { get; set; }

        [XmlElement("Card")] public List<CardConfig> Cards { get; set; }

        public void LinkCards()
        {
            foreach (var c in Cards) c.Group = this;
        }
    }

    /// <summary>
    /// Bağlantı izleme ayarları. Arayüzde yoktur; config.xml içindeki &lt;Monitor&gt; bölümünden
    /// mimariye dokunmadan değiştirilebilir. Varsayılanlarla kapanan / kablosu çekilen kart ~9 sn'de algılanır.
    /// </summary>
    public sealed class ConnectionMonitorOptions
    {
        public ConnectionMonitorOptions()
        {
            KeepAliveIdleMs = 3000;
            KeepAliveIntervalMs = 2000;
            KeepAliveProbeCount = 3;
            RetransmitTimeoutSec = 10;
            AutoReconnect = true;
            ReconnectMinMs = 3000;
            ReconnectMaxMs = 15000;
            ReconnectTimeoutMs = 3000;
            TelnetNopHeartbeat = false;
            HeartbeatIntervalMs = 5000;
            MonitorTickMs = 1000;
        }

        /// <summary>Son trafikten sonra ilk sağlık yoklamasına kadar geçen süre.</summary>
        public int KeepAliveIdleMs { get; set; }
        /// <summary>Yoklamalar arası süre (= yoklama başına zaman aşımı).</summary>
        public int KeepAliveIntervalMs { get; set; }
        /// <summary>Kopma ilanı için ardışık cevapsız yoklama sayısı (tek kayıp kopma sayılmaz).</summary>
        public int KeepAliveProbeCount { get; set; }
        /// <summary>Gönderilen veri hiç onaylanmazsa bağlantının düşürüleceği süre (Windows).</summary>
        public int RetransmitTimeoutSec { get; set; }
        /// <summary>Bağlıyken kopan kart, ulaşılabilir olunca kendiliğinden yeniden bağlansın.</summary>
        public bool AutoReconnect { get; set; }
        public int ReconnectMinMs { get; set; }
        public int ReconnectMaxMs { get; set; }
        public int ReconnectTimeoutMs { get; set; }
        /// <summary>İsteğe bağlı: boştaki bağlantıya telnet NOP gönder (varsayılan kapalı; keepalive yeterli).</summary>
        public bool TelnetNopHeartbeat { get; set; }
        public int HeartbeatIntervalMs { get; set; }
        public int MonitorTickMs { get; set; }

        public TelnetClient.KeepAliveOptions ToKeepAlive()
        {
            return new TelnetClient.KeepAliveOptions
            {
                IdleMs = KeepAliveIdleMs,
                IntervalMs = KeepAliveIntervalMs,
                ProbeCount = KeepAliveProbeCount,
                RetransmitTimeoutSec = RetransmitTimeoutSec,
            };
        }

        /// <summary>n'inci başarısız denemeden sonraki bekleme: 3, 6, 12, 15, 15… sn</summary>
        public TimeSpan ReconnectDelay(int failedAttempts)
        {
            double ms = ReconnectMinMs * Math.Pow(2, Math.Max(0, failedAttempts - 1));
            return TimeSpan.FromMilliseconds(Math.Min(ReconnectMaxMs, Math.Max(ReconnectMinMs, ms)));
        }
    }

    /// <summary>Tüm kartlar için ortak terminal ayarları.</summary>
    public sealed class AppSettings
    {
        public AppSettings()
        {
            Monitor = new ConnectionMonitorOptions();
            Transfer = new TransferSettings();
            ConnectTimeoutMs = DefaultConnectTimeoutMs;
            MaxTerminalLines = DefaultMaxTerminalLines;
            LineEnding = "CRLF";
            LocalEcho = true;
        }

        public string LineEnding { get; set; }   // CRLF, CR, LF, NONE
        public bool LocalEcho { get; set; }
        public bool Timestamp { get; set; }
        public bool LogToFile { get; set; }

        public const int DefaultConnectTimeoutMs = 10000;   // önceki sabit değer 5000 ms idi (iki katına çıkarıldı)
        public const int DefaultMaxTerminalLines = 1000;
        public const int MinTerminalLines = 100;
        public const int MaxTerminalLinesLimit = 100000;

        /// <summary>"Bağlantı Kur" ile bağlanırken en fazla beklenecek süre (ms). Kart hızlı cevap verirse beklenmez.</summary>
        public int ConnectTimeoutMs { get; set; }

        /// <summary>Her kartın terminalinde tutulacak en fazla satır; aşılınca en eski satırlar silinir.</summary>
        public int MaxTerminalLines { get; set; }

        /// <summary>Kart Gezgini kenar çubuğu daraltılmış mı (tercih hatırlanır).</summary>
        public bool SidebarCollapsed { get; set; }

        /// <summary>"Birlikte aç (salt okunur)" açıklama penceresi bir daha gösterilmesin.</summary>
        public bool HideReadOnlyOpenInfo { get; set; }

        /// <summary>Kayıttan gelen değerleri geçerli aralığa çeker.</summary>
        public void Normalize()
        {
            if (ConnectTimeoutMs < 1000) ConnectTimeoutMs = DefaultConnectTimeoutMs;
            if (MaxTerminalLines < MinTerminalLines) MaxTerminalLines = MinTerminalLines;
            if (MaxTerminalLines > MaxTerminalLinesLimit) MaxTerminalLines = MaxTerminalLinesLimit;
        }

        public ConnectionMonitorOptions Monitor { get; set; }
        /// <summary>Bilgisayardan karta yükleme (FTP + kartta copy/xcopy) ayarları.</summary>
        public TransferSettings Transfer { get; set; }

        [XmlIgnore]
        public string LineEndingChars
        {
            get
            {
                switch (LineEnding)
                {
                    case "CR": return "\r";
                    case "LF": return "\n";
                    case "NONE": return "";
                    default: return "\r\n";
                }
            }
        }
    }

    [XmlRoot("TelnetCards")]
    public sealed class AppConfig
    {
        public AppConfig()
        {
            Groups = new List<CardGroup>();
            Settings = new AppSettings();
        }

        [XmlArray("Groups"), XmlArrayItem("Group")] public List<CardGroup> Groups { get; set; }
        public AppSettings Settings { get; set; }
    }

    /// <summary>
    /// Yapılandırmayı exe'nin yanındaki Konfig\config.xml dosyasında saklar (uygulamayla birlikte taşınır).
    /// Exe yazma izni olmayan bir klasördeyse (ör. Program Files) %APPDATA%\TelnetCards\config.xml kullanılır.
    /// İlk açılışta eski konumlardaki (%APPDATA%) yapılandırma Konfig klasörüne kopyalanır; hiçbir şey kaybolmaz.
    /// </summary>
    public static class ConfigStore
    {
        public const string FolderName = "Konfig";
        public const string FileName = "config.xml";

        /// <summary>Exe'nin yanındaki Konfig\config.xml.</summary>
        public static string PortablePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, FileName); }
        }

        private static string AppDataPath(string folder)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, FileName);
        }

        /// <summary>Önceki sürümlerin kullandığı konumlar (yeniden eskiye).</summary>
        public static string[] LegacyPaths
        {
            get { return new[] { AppDataPath(AppInfo.DataFolder), AppDataPath("Kartograf") }; }
        }

        /// <summary>
        /// Kullanılacak dosya: Konfig klasörüne yazılabiliyorsa orası, değilse %APPDATA%.
        /// fallback=true ise Konfig klasörüne yazılamadı.
        /// </summary>
        public static string ResolvePath(out bool fallback)
        {
            string portable = PortablePath;
            fallback = !CanWrite(Path.GetDirectoryName(portable));
            return fallback ? AppDataPath(AppInfo.DataFolder) : portable;
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".yazma-testi");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Dosya yoksa eski konumlardaki ilk yapılandırmayı kopyalar (taşımaz; eskisi yedek olarak kalır).</summary>
        public static string MigrateIfMissing(string path)
        {
            if (File.Exists(path)) return null;
            foreach (string old in LegacyPaths)
            {
                if (string.Equals(Path.GetFullPath(old), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (!File.Exists(old)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(old, path);
                    return old;
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("yapılandırma taşıma " + old, ex);
                }
            }
            return null;
        }

        public static AppConfig Load(string path)
        {
            AppConfig cfg = null;
            try
            {
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                        cfg = (AppConfig)new XmlSerializer(typeof(AppConfig)).Deserialize(fs);
                }
            }
            catch
            {
                // Bozuk dosya: yedekle, boş yapılandırmayla devam et
                try { File.Copy(path, path + ".bozuk", true); } catch { }
            }

            if (cfg == null) cfg = new AppConfig();
            if (cfg.Settings == null) cfg.Settings = new AppSettings();
            if (cfg.Settings.Monitor == null) cfg.Settings.Monitor = new ConnectionMonitorOptions();
            if (cfg.Settings.Transfer == null) cfg.Settings.Transfer = new TransferSettings();
            cfg.Settings.Normalize();
            if (cfg.Groups == null) cfg.Groups = new List<CardGroup>();
            foreach (var g in cfg.Groups)
            {
                if (g.Cards == null) g.Cards = new List<CardConfig>();
                g.LinkCards();
            }
            return cfg;
        }

        public static void Save(AppConfig cfg, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(AppConfig)).Serialize(fs, cfg);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    public static class IpUtil
    {
        /// <summary>Yalnızca dört bölümlü, geçerli IPv4 adreslerini kabul eder (ör. "10" gibi kısaltmaları reddeder).</summary>
        public static bool IsValidIPv4(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Trim().Split('.');
            if (parts.Length != 4) return false;
            foreach (string p in parts)
            {
                int v;
                if (p.Length == 0 || p.Length > 3 || !int.TryParse(p, out v) || v < 0 || v > 255) return false;
                foreach (char ch in p) if (ch < '0' || ch > '9') return false;
            }
            IPAddress ignored;
            return IPAddress.TryParse(text.Trim(), out ignored);
        }

        /// <summary>IP adresini 'offset' kadar ilerletir. Geçersizse ya da taşarsa null döner.</summary>
        public static string Increment(string ip, int offset)
        {
            if (!IsValidIPv4(ip)) return null;
            string[] p = ip.Trim().Split('.');
            long value = (long.Parse(p[0]) << 24) | (long.Parse(p[1]) << 16) | (long.Parse(p[2]) << 8) | long.Parse(p[3]);
            value += offset;
            if (value < 0 || value > 0xFFFFFFFFL) return null;
            return ((value >> 24) & 255) + "." + ((value >> 16) & 255) + "." + ((value >> 8) & 255) + "." + (value & 255);
        }
    }
}
