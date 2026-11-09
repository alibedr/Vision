using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Xml.Serialization;

namespace Vision
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

        /// <summary>Kartın hızlı komut düğmesine atanmış komut (yalnız bu karta gönderilir; boş: ayarlanmamış).</summary>
        [XmlAttribute("HizliKomut")] public string QuickCommand { get; set; }

        /// <summary>Kartın yeniden başlatma düğmesinde seçili komut ("reboot" / "reboot 2").</summary>
        [XmlAttribute("Yeniden")] public RebootOption RebootOption { get; set; }

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

    /// <summary>Yeniden başlatma komutu seçimi: "reboot" ya da "reboot 2".</summary>
    public enum RebootOption { Reboot = 0, Reboot2 = 1 }

    public static class RebootCommands
    {
        public static string CommandFor(RebootOption o) { return o == RebootOption.Reboot2 ? "reboot 2" : "reboot"; }
        public static RebootOption Toggle(RebootOption o) { return o == RebootOption.Reboot2 ? RebootOption.Reboot : RebootOption.Reboot2; }
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
            SeparateReturnValues = true;
            TerminalZoom = DefaultTerminalZoom;
            GlobalRebootOption = RebootOption.Reboot;
        }

        public string LineEnding { get; set; }   // CRLF, CR, LF, NONE
        public bool LocalEcho { get; set; }
        public bool Timestamp { get; set; }
        /// <summary>
        /// Eski "Dosyaya kaydet" (sürekli kayıt) anahtarı. Yerine kart başına / genel anlık kayıt düğmeleri geldi;
        /// alan yalnız eski ayar dosyalarıyla uyum için okunur ve her açılışta kapatılır.
        /// </summary>
        public bool LogToFile { get; set; }

        /// <summary>Komut dönüş değerleri ("value = …") canlı terminal akışından ayrılıp kartın dönüş panelinde gösterilir.</summary>
        public bool SeparateReturnValues { get; set; }

        public const float DefaultTerminalZoom = 1.0f;
        public const float MinTerminalZoom = 0.6f;
        public const float MaxTerminalZoom = 2.5f;

        /// <summary>Tüm kart terminallerinin ortak yakınlaştırma oranı (1.0 = %100).</summary>
        public float TerminalZoom { get; set; }

        /// <summary>Genel komut çubuğunun hızlı komutu (boş: ayarlanmamış).</summary>
        public string GlobalQuickCommand { get; set; }

        /// <summary>Genel komut çubuğunda seçili yeniden başlatma komutu.</summary>
        public RebootOption GlobalRebootOption { get; set; }

        /// <summary>Yakınlaştırma adımları (%60 … %250).</summary>
        public static readonly float[] ZoomSteps = { 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.25f, 1.4f, 1.6f, 1.8f, 2.0f, 2.25f, 2.5f };

        /// <summary>Bir sonraki (+1) / önceki (-1) yakınlaştırma adımı; sınırda kalır.</summary>
        public static float StepZoom(float current, int step)
        {
            float cur = ClampZoom(current);
            int i = Array.FindIndex(ZoomSteps, z => z >= cur - 0.001f);
            if (i < 0) i = ZoomSteps.Length - 1;
            if (step > 0 && ZoomSteps[i] <= cur + 0.001f) i++;
            else if (step < 0) i--;
            return ZoomSteps[Math.Max(0, Math.Min(ZoomSteps.Length - 1, i))];
        }

        public static float ClampZoom(float z)
        {
            if (float.IsNaN(z) || z <= 0) return DefaultTerminalZoom;
            return Math.Max(MinTerminalZoom, Math.Min(MaxTerminalZoom, (float)Math.Round(z, 2)));
        }

        public const int DefaultConnectTimeoutMs = 40000;   // 10000 ms'nin 4 katı (ilk sürüm 5000 ms, sonra 10000 ms idi)

        /// <summary>Önceki sürümlerin varsayılanları: kayıtlı ayarda bunlardan biri varsa yeni varsayılana yükseltilir.</summary>
        private static readonly int[] LegacyDefaultConnectTimeouts = { 5000, 10000 };
        public const int DefaultMaxTerminalLines = 1000;
        public const int MinTerminalLines = 100;
        public const int MaxTerminalLinesLimit = 100000;

        /// <summary>"Bağlantı Kur" ile bağlanırken en fazla beklenecek süre (ms). Kart hızlı cevap verirse beklenmez.</summary>
        public int ConnectTimeoutMs { get; set; }

        /// <summary>Her kartın terminalinde tutulacak en fazla satır; aşılınca en eski satırlar silinir.</summary>
        public int MaxTerminalLines { get; set; }

        /// <summary>Kart Gezgini kenar çubuğu daraltılmış mı (tercih hatırlanır).</summary>
        public bool SidebarCollapsed { get; set; }

        /// <summary>Eski sürümlerin tema ayarı (koyu / açık). Yeni sürüm UiThemeId kullanır; uyumluluk için güncel tutulur.</summary>
        public ThemeMode UiTheme { get; set; }

        /// <summary>Seçili hazır tema (ThemeCatalog kimliği: koyu, acik, solarized…). Sekme şeridindeki Temalar menüsünden seçilir.</summary>
        public string UiThemeId { get; set; }

        /// <summary>Kullanıcının hazır temalarda değiştirdiği renkler (yalnız varsayılandan farklı olanlar).</summary>
        public List<ThemeCustomization> ThemeCustomizations { get; set; }

        /// <summary>Seçili temanın kullanıcı değişiklikleriyle birlikte 10 temel rengi.</summary>
        public Color[] EffectiveThemeColors()
        {
            return ThemeCatalog.EffectiveColors(ThemeCatalog.FindOrDefault(UiThemeId), ThemeCustomizations);
        }

        /// <summary>"Birlikte aç (salt okunur)" açıklama penceresi bir daha gösterilmesin.</summary>
        public bool HideReadOnlyOpenInfo { get; set; }

        /// <summary>Kayıttan gelen değerleri geçerli aralığa çeker.</summary>
        public void Normalize()
        {
            // Eski sürümün varsayılanı dosyaya yazılmış olabilir; kullanıcı elle başka bir değer girdiyse dokunulmaz
            if (ConnectTimeoutMs < 1000 || Array.IndexOf(LegacyDefaultConnectTimeouts, ConnectTimeoutMs) >= 0)
                ConnectTimeoutMs = DefaultConnectTimeoutMs;
            if (MaxTerminalLines < MinTerminalLines) MaxTerminalLines = MinTerminalLines;
            if (MaxTerminalLines > MaxTerminalLinesLimit) MaxTerminalLines = MaxTerminalLinesLimit;
            TerminalZoom = ClampZoom(TerminalZoom);
            if (!Enum.IsDefined(typeof(RebootOption), GlobalRebootOption)) GlobalRebootOption = RebootOption.Reboot;
            if (!Enum.IsDefined(typeof(ThemeMode), UiTheme)) UiTheme = ThemeMode.Dark;
            // Önceki sürümden geçiş: yalnız koyu / açık vardı
            if (ThemeCatalog.Find(UiThemeId) == null)
                UiThemeId = UiTheme == ThemeMode.Light ? ThemeCatalog.LightId : ThemeCatalog.DarkId;
            if (ThemeCustomizations == null) ThemeCustomizations = new List<ThemeCustomization>();
            ThemeCustomizations.RemoveAll(c => c == null || ThemeCatalog.Find(c.ThemeId) == null);
            LogToFile = false;   // sürekli kayıt kaldırıldı: anlık kayıt düğmeleri kullanılır
            if (Transfer != null) Transfer.ApplyNewDefaults();
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

    [XmlRoot("TelnetCards")]   // eski ad: mevcut config.xml dosyaları okunmaya devam etsin diye değiştirilmedi
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
    /// Yapılandırmayı kullanıcının bilgisayarında, %APPDATA%\TelnetCards\config.xml dosyasında saklar
    /// (projenin ilk sürümündeki gibi). Böylece yeni bir Vision sürümü indirilip başka bir klasörden
    /// çalıştırıldığında gruplar, kartlar ve ayarlar kaybolmaz; her sürüm aynı dosyayı kullanır.
    /// AppData'da dosya yoksa ilk açılışta eski konumlardaki (exe yanındaki Konfig klasörü, %APPDATA%\Kartograf)
    /// yapılandırma oraya kopyalanır; eskisi yedek olarak yerinde kalır.
    /// </summary>
    public static class ConfigStore
    {
        /// <summary>Bir önceki sürümün exe yanında kullandığı klasör (yalnız taşıma için okunur).</summary>
        public const string LegacyPortableFolderName = "Konfig";
        public const string FileName = "config.xml";

        private static string AppDataPath(string folder)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, FileName);
        }

        /// <summary>Kalıcı yapılandırma dosyası: %APPDATA%\TelnetCards\config.xml.</summary>
        public static string AppDataConfigPath
        {
            get { return AppDataPath(AppInfo.DataFolder); }
        }

        /// <summary>Bir önceki sürümün exe yanındaki Konfig\config.xml dosyası.</summary>
        public static string LegacyPortablePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LegacyPortableFolderName, FileName); }
        }

        /// <summary>Önceki sürümlerin kullandığı konumlar (yeniden eskiye).</summary>
        public static string[] LegacyPaths
        {
            get { return new[] { LegacyPortablePath, AppDataPath("Kartograf") }; }
        }

        /// <summary>
        /// Kullanılacak dosya: her zaman %APPDATA%\TelnetCards\config.xml.
        /// fallback=true ise bu klasöre yazılamıyor (ayarlar kaydedilemeyebilir).
        /// </summary>
        public static string ResolvePath(out bool fallback)
        {
            string path = AppDataConfigPath;
            fallback = !CanWrite(Path.GetDirectoryName(path));
            return path;
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

        /// <summary>
        /// Dosya yoksa eski konumlardaki ilk yapılandırmayı kopyalar (taşımaz; eskisi yedek olarak kalır).
        /// Dosya varsa ama exe yanındaki Konfig\config.xml ondan DAHA YENİYSE (ilk sürümden kalma eski bir AppData
        /// dosyası + Konfig'li sürümde yapılmış güncel ayarlar) güncel olan alınır; AppData'daki eski dosya
        /// config.xml.onceki olarak yedeklenir. İlk kayıttan sonra AppData dosyası daha yeni olur, tekrar kopyalanmaz.
        /// </summary>
        public static string MigrateIfMissing(string path)
        {
            if (File.Exists(path))
            {
                string portable = LegacyPortablePath;
                try
                {
                    if (File.Exists(portable) &&
                        !string.Equals(Path.GetFullPath(portable), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) &&
                        File.GetLastWriteTimeUtc(portable) > File.GetLastWriteTimeUtc(path))
                    {
                        File.Copy(path, path + ".onceki", true);
                        File.Copy(portable, path, true);
                        return portable;
                    }
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("yapılandırma taşıma " + portable, ex);
                }
                return null;
            }
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
