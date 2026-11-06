using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Vision
{
    /// <summary>
    /// Kartın açılış (boot) parametrelerinden okunan FTP bilgileri. Yalnız bellekte tutulur; dosyaya, log'a ya da
    /// ekrana parola yazılmaz. Her kartın kendi bilgisi vardır (CardSession.Ftp).
    /// </summary>
    public sealed class CardFtpCredentials
    {
        /// <summary>FTP sunucusu (boot parametresi h = host inet).</summary>
        public string Host { get; set; }
        /// <summary>FTP kullanıcısı (u = user).</summary>
        public string User { get; set; }
        /// <summary>FTP parolası (pw = ftp password). Yalnız bellekte.</summary>
        public string Password { get; set; }
        /// <summary>
        /// Kartın FTP sunucusunu gördüğü aygıt adı: boot satırında aygıt parantezi kapandıktan sonra ':' işaretine kadar
        /// olan kelime ("gei(0,0)host:" → "host", "gei(1,0)ali:" → "ali"). Bulunamazsa "host".
        /// </summary>
        public string HostName { get; set; }
        public DateTime ObtainedAtUtc { get; set; }

        /// <summary>Kartın aygıtı: "host:" gibi.</summary>
        public string CardDevice { get { return DeviceName + ":"; } }

        /// <summary>Aygıt adı, sonundaki ':' olmadan ("host", "ali").</summary>
        public string DeviceName { get { return string.IsNullOrWhiteSpace(HostName) ? "host" : HostName.Trim().TrimEnd(':'); } }

        /// <summary>Kartın FTP köküne eriştiği yol: "/host." + aygıt adı ("gei(0,0)host:" → "/host.host").</summary>
        public string CardFtpRoot { get { return TransferSettings.CardRootFor(DeviceName); } }

        /// <summary>Parolasız özet (ekranda gösterilebilir).</summary>
        public override string ToString()
        {
            return User + "@" + Host + " (kartın yolu " + CardFtpRoot + ")";
        }
    }

    /// <summary>Kartın boot parametrelerinin (version çıktısındaki boot satırı) ayrıştırma sonucu.</summary>
    public sealed class BootParams
    {
        public string Host;
        public string User;
        public string Password;
        public bool PasswordPresent;
        public string HostName;
        /// <summary>Çıktıda tanınan alan sayısı (hiç yoksa çıktı boot parametresi değildir).</summary>
        public int FieldsFound;
        /// <summary>Çıktı etkileşimli düzenleme istemiyle bitti (kart her alan için giriş bekliyor).</summary>
        public bool WaitingForInput;

        // Tablo biçimi:  "host inet (h)        : 192.168.1.100"   (alan adı serbest, anahtar parantez içinde).
        // Anahtardan sonra açıklama parantezleri olabilir: "ftp password (pw) (blank = use rsh) : gizli".
        // Değer, anahtardan (ve açıklamalardan) SONRAKİ ilk ':' işaretinden itibaren satır sonuna kadar alınır;
        // değerin kendisindeki ':' ya da '=' (ör. "10.0.0.5:ffffff00", "a:b=c" parolası) bölünmez.
        private static readonly Regex TableLine = new Regex(
            @"^\s*(?<label>[^:()]*?)\s*\(\s*(?<key>[a-z]{1,3})\s*\)(?:\s*\([^()]*\))*\s*:(?<value>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Parantez anahtarı olmayan bspBootChange alanları ("boot device :", "processor number :", "host name :", "file name :")
        private static readonly Regex PlainFieldLine = new Regex(@"^\s*(?:boot\s*device|processor\s*number|host\s*name|file\s*name)\s*:",
                                                                 RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Kısa biçim:  "h: 192.168.1.100"  ya da  "pw = gizli"
        private static readonly Regex ShortLine = new Regex(@"^\s*(?<key>h|u|pw|hn)\s*[:=]\s*(?<value>.*?)\s*$",
                                                            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // Boot satırı:  "fei(0,0)host:/vxWorks e=192.168.1.10:ffffff00 h=192.168.1.100 u=target pw=gizli f=0x8"
        private static readonly Regex BootLineToken = new Regex(@"(?:^|\s)(?<key>h|u|pw|tn|hn)=(?<value>\S*)", RegexOptions.Compiled);
        // Boot satırının başı: "gei(0,3)host:/vxWorks …" → parantez kapandıktan sonra ':' işaretine kadar olan kelime ("host")
        private static readonly Regex BootLineHost = new Regex(@"^\s*[^\s(]*\([^)]*\)(?<host>[^:\s()]+):", RegexOptions.Compiled);
        private static readonly Regex HostNameLine = new Regex(@"^\s*host\s*name\s*:\s*(?<value>\S*)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // version çıktısındaki işaret satırı: "Boot line:" / "BootLine:" (boot satırı bir alt satırda ya da aynı satırda gelir)
        private static readonly Regex BootLineMarker = new Regex(@"^\s*boot\s*line\s*:\s*(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EndOfBootLine = new Regex(@"^\s*(?:->|value\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// "version" komutunun çıktısını ayrıştırır. Çıktıda "Boot line:" (ya da "BootLine:") satırı aranır; ondan sonraki
        /// satır kartın boot satırıdır ("fei(0,0)host:/vxWorks e=… h=10.0.0.5 u=kullanici pw=parola f=0x8 …").
        /// h= FTP sunucusu, u= kullanıcı, pw= parola olarak alınır. YALNIZ boot satırındaki anahtarlar okunur; version
        /// çıktısının diğer satırlarındaki "=" işaretleri (ör. "value = 0 = 0x0") karışmaz.
        /// "Boot line:" işareti yoksa genel ayrıştırıcıya (Parse) düşülür.
        /// </summary>
        public static BootParams ParseVersion(IEnumerable<string> lines)
        {
            var list = (lines ?? Enumerable.Empty<string>()).Select(l => (l ?? "").Replace("\t", " ").TrimEnd()).ToList();
            int marker = list.FindIndex(l => BootLineMarker.IsMatch(l));
            if (marker < 0) return Parse(list);

            // İşaretten sonraki boot satırı (işaretle aynı satırda da olabilir). Uzun satır terminalde bölünmüşse
            // boş satıra, dönüş değerine ya da isteme kadar olan satırlar birleştirilir.
            var parts = new List<string>();
            string rest = BootLineMarker.Match(list[marker]).Groups["rest"].Value.Trim();
            if (rest.Length > 0) parts.Add(rest);
            for (int i = marker + 1; i < list.Count; i++)
            {
                string l = list[i];
                if (l.Trim().Length == 0) { if (parts.Count > 0) break; continue; }
                if (EndOfBootLine.IsMatch(l)) break;
                parts.Add(l.Trim());
            }
            string bootLine = string.Join(" ", parts);

            var p = new BootParams();
            foreach (Match t in BootLineToken.Matches(bootLine)) Set(p, t.Groups["key"].Value, t.Groups["value"].Value);
            Match bh = BootLineHost.Match(bootLine);
            if (bh.Success && p.HostName == null) p.HostName = bh.Groups["host"].Value;
            return p;
        }

        /// <summary>
        /// bspBootChange (ya da bootChange / boot satırı) çıktısını ayrıştırır. Biçim farklılıklarına dayanıklıdır:
        /// tablo ("host inet (h) : …"), kısa ("h: …") ve boot satırı ("… h=… u=… pw=…") biçimleri tanınır;
        /// boşluk / sekme farkları, satır sonundaki istem ve eksik alanlar sorun çıkarmaz.
        /// </summary>
        public static BootParams Parse(IEnumerable<string> lines)
        {
            var p = new BootParams();
            string last = null;
            bool seenInteractiveHeader = false;
            foreach (string raw in lines ?? Enumerable.Empty<string>())
            {
                string line = (raw ?? "").Replace("\t", " ").TrimEnd();
                if (line.Trim().Length == 0) continue;
                last = line;
                if (line.Contains("clear field") || line.Contains("previous field") || line.Contains("= quit")) { seenInteractiveHeader = true; continue; }

                Match hn = HostNameLine.Match(line);
                if (hn.Success) { Set(p, "hn", hn.Groups["value"].Value); continue; }

                Match m = TableLine.Match(line);
                if (m.Success) { Set(p, m.Groups["key"].Value.ToLowerInvariant(), m.Groups["value"].Value); continue; }

                m = ShortLine.Match(line);
                if (m.Success) { Set(p, m.Groups["key"].Value.ToLowerInvariant(), m.Groups["value"].Value); continue; }

                if (line.Contains("="))
                {
                    bool any = false;
                    foreach (Match t in BootLineToken.Matches(line)) { Set(p, t.Groups["key"].Value, t.Groups["value"].Value); any = true; }
                    if (any)
                    {
                        Match bh = BootLineHost.Match(line);
                        if (bh.Success && p.HostName == null) p.HostName = bh.Groups["host"].Value;
                    }
                }
            }
            // Etkileşimli düzenleme (bspBootChange / bootChange): başlık "'.' = clear field; '-' = go to previous field; ^D = quit"
            // ve son satır bir boot alanı ("alan (k) : değer " ya da "host name : …") → kart o alan için Enter bekliyor.
            // "value = …" dönüş satırı ya da kabuk istemi (->) gelince düzenleme bitmiştir. Alan olmayan son satırlar
            // ("sysMvraöSet: '…' success" gibi) giriş beklemez; böylece komut bittikten sonra kabuğa fazladan Enter gitmez.
            if (last != null)
            {
                string t = last.TrimStart();
                bool done = t.StartsWith("->") || Regex.IsMatch(t, @"^value\s*=", RegexOptions.IgnoreCase);
                bool field = TableLine.IsMatch(last) || (seenInteractiveHeader && PlainFieldLine.IsMatch(last));
                if (!done && field) p.WaitingForInput = true;
            }
            return p;
        }

        private static void Set(BootParams p, string key, string value)
        {
            value = (value ?? "").Trim();
            // Etkileşimli istemde değer, kullanıcı girişi beklenirken satırın sonunda boşlukla durur; temizlenmiş hâli alınır
            switch (key)
            {
                case "h": p.FieldsFound++; if (value.Length > 0) p.Host = StripMask(value); break;
                case "u": p.FieldsFound++; if (value.Length > 0) p.User = value; break;
                case "pw": p.FieldsFound++; p.PasswordPresent = true; if (value.Length > 0) p.Password = value; break;
                case "hn": p.FieldsFound++; if (value.Length > 0) p.HostName = value; break;
                default: p.FieldsFound++; break;
            }
        }

        /// <summary>"192.168.1.100:ffffff00" → "192.168.1.100" (adres maskesi varsa).</summary>
        private static string StripMask(string v)
        {
            int c = v.IndexOf(':');
            return c > 0 ? v.Substring(0, c) : v;
        }

        /// <summary>Sonucu doğrular. Sorun yoksa null, varsa kullanıcıya gösterilecek Türkçe açıklama.</summary>
        public string Validate()
        {
            if (FieldsFound == 0)
                return "Kartın cevabında boot parametreleri bulunamadı (h / u / pw alanları yok).";
            var missing = new List<string>();
            if (string.IsNullOrEmpty(Host)) missing.Add("FTP sunucusu (h)");
            if (string.IsNullOrEmpty(User)) missing.Add("kullanıcı (u)");
            if (!PasswordPresent || string.IsNullOrEmpty(Password)) missing.Add("FTP parolası (pw)");
            if (missing.Count > 0)
            {
                string msg = "Kartın boot parametrelerinde eksik bilgi: " + string.Join(", ", missing) + ".";
                if (!string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(User) && string.IsNullOrEmpty(Password))
                    msg += " Parola boşsa kart dosyalara FTP yerine RSH ile erişiyor olabilir.";
                return msg;
            }
            if (!IsValidHost(Host))
                return "Kartın boot parametresindeki FTP sunucusu adresi geçersiz: \"" + Host + "\".";
            return null;
        }

        private static bool IsValidHost(string h)
        {
            System.Net.IPAddress ip;
            if (Regex.IsMatch(h, @"^[\d.]+$"))   // yalnız rakam ve nokta: geçerli bir IPv4 olmalı (ör. 999.1.1.1 değil)
            {
                string[] parts = h.Split('.');
                int n;
                return parts.Length == 4 && parts.All(x => int.TryParse(x, out n) && n >= 0 && n <= 255);
            }
            if (System.Net.IPAddress.TryParse(h, out ip)) return true;
            return Regex.IsMatch(h, @"^[A-Za-z0-9]([A-Za-z0-9\-.]{0,252}[A-Za-z0-9])?$");
        }
    }

    /// <summary>Karttan FTP bilgisi alma sonucu.</summary>
    public sealed class CardFtpResult
    {
        public bool Ok;
        public CardFtpCredentials Credentials;
        public string Error;
        /// <summary>Komut kartın imajında yoksa adı.</summary>
        public string MissingCommand;
        /// <summary>Parolası gizlenmiş ham cevap (tanılama paneli için).</summary>
        public ShellResponse MaskedResponse;
    }

    /// <summary>
    /// Kartın FTP bilgisini kartın kendisinden "version" komutuyla alır (çıktıdaki "Boot line:" satırının altındaki
    /// boot satırı: h= FTP sunucusu, u= kullanıcı, pw= parola). Kartın MEVCUT telnet oturumunu kullanır.
    /// version etkileşimli değildir: karta tek komut gider, hiçbir boot parametresi değiştirilmez.
    /// </summary>
    public static class CardFtpConfigurator
    {
        public const string Command = "version";
        private const int TimeoutSec = 6;
        public const string Mask = "••••••";

        public static async Task<CardFtpResult> ObtainAsync(Func<string, int, Task<ShellResponse>> exec)
        {
            var result = new CardFtpResult();
            ShellResponse resp = await exec(Command, TimeoutSec).ConfigureAwait(false);
            var all = new List<string>(resp.OutputLines);
            string raw = resp.Raw ?? "";

            if (resp.Failure != null) return Fail(result, resp, resp.Failure, null);
            if (resp.UndefinedSymbol != null)
                return Fail(result, resp, "Kartın imajında \"" + resp.UndefinedSymbol + "\" komutu yok; FTP bilgisi karttan alınamadı.", resp.UndefinedSymbol);

            BootParams bp = BootParams.ParseVersion(all);

            ShellResponse masked = MaskSecret(ShellResponse.Parse(Command, raw), bp.Password);
            result.MaskedResponse = masked;

            string invalid = bp.Validate();
            if (invalid != null)
            {
                if (resp.TimedOut && bp.FieldsFound == 0) invalid = "Kart \"" + Command + "\" komutuna zamanında cevap vermedi.";
                else if (bp.FieldsFound == 0) invalid = "Kartın \"" + Command + "\" cevabında \"Boot line:\" satırı ya da h= / u= / pw= değerleri bulunamadı.";
                result.Error = invalid;
                return result;
            }
            result.Ok = true;
            result.Credentials = new CardFtpCredentials
            {
                Host = bp.Host,
                User = bp.User,
                Password = bp.Password,
                HostName = bp.HostName,
                ObtainedAtUtc = DateTime.UtcNow,
            };
            return result;
        }

        private static CardFtpResult Fail(CardFtpResult r, ShellResponse resp, string error, string missing)
        {
            r.Error = error;
            r.MissingCommand = missing;
            r.MaskedResponse = MaskSecret(resp, null);
            return r;
        }

        /// <summary>Cevaptaki parolayı (ve pw= / (pw) : alanlarını) gizler.</summary>
        public static ShellResponse MaskSecret(ShellResponse r, string secret)
        {
            Func<string, string> mask = s =>
            {
                if (string.IsNullOrEmpty(s)) return s;
                if (!string.IsNullOrEmpty(secret)) s = s.Replace(secret, Mask);
                // "ftp password (pw) (blank = use rsh) : gizli" → değerin tamamı (boşluk içerse de) gizlenir
                s = Regex.Replace(s, @"(\(\s*pw\s*\)(?:[ \t]*\([^()\r\n]*\))*[ \t]*:[ \t]*)[^\r\n]*\S", "$1" + Mask, RegexOptions.IgnoreCase);
                s = Regex.Replace(s, @"(\bpw\s*[:=]\s*)\S+", "$1" + Mask, RegexOptions.IgnoreCase);
                return s;
            };
            ShellResponse m = r.Failure != null ? ShellResponse.Failed(r.Command, r.Failure, mask(r.Raw ?? "")) : ShellResponse.Parse(r.Command, mask(r.Raw ?? ""));
            m.TimedOut = r.TimedOut;
            return m;
        }
    }

    /// <summary>
    /// Bir kartın FTP'de bıraktığı kopyalar (yükleme sırasında FTP'ye gönderilen dosya / klasörler).
    /// "FTP kopyalarını sil" düğmesi bunlara göre etkinleşir; hiçbiri kendiliğinden silinmez
    /// (yalnız kullanıcı düğmeye basınca ya da "kopyalama sonrası sil" ayarı açıkken).
    /// </summary>
    public sealed class FtpCopyTracker
    {
        private readonly object _lock = new object();
        private readonly List<FtpCopy> _items = new List<FtpCopy>();

        public sealed class FtpCopy
        {
            public string Name;
            public bool IsDirectory;
            public DateTime CreatedUtc;
        }

        public event Action Changed;

        public IList<FtpCopy> Items { get { lock (_lock) return _items.ToList(); } }
        public int Count { get { lock (_lock) return _items.Count; } }

        public void Add(string name, bool isDir)
        {
            lock (_lock)
            {
                if (_items.Any(i => i.Name == name)) return;
                _items.Add(new FtpCopy { Name = name, IsDirectory = isDir, CreatedUtc = DateTime.UtcNow });
            }
            Raise();
        }

        public void Remove(string name)
        {
            bool removed;
            lock (_lock) removed = _items.RemoveAll(i => i.Name == name) > 0;
            if (removed) Raise();
        }

        public void Clear()
        {
            lock (_lock) _items.Clear();
            Raise();
        }

        private void Raise()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
