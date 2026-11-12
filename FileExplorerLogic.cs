using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Vision
{
    /// <summary>
    /// Kart kabuğuna gönderilen bir komutun cevabı. Kabuk (VxWorks benzeri) her fonksiyon çağrısından sonra
    /// "value = N = 0x..." satırıyla dönüş değerini basar; bu satır metin çıktısından AYRI tutulur.
    /// </summary>
    public sealed class ShellResponse
    {
        // "value = 0 = 0x0", "value = -1 = 0xffffffff" ...
        private static readonly Regex ValueLine =
            new Regex(@"^\s*value\s*=\s*(-?\d+)\s*=\s*0x[0-9a-fA-F]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Yalnız istemden oluşan satırlar: "->", "-> ", "[vxWorks]# ", "SHELL> "
        private static readonly Regex PromptOnly =
            new Regex(@"^\s*(?:->|\[?[\w\-\.]{1,24}\]?\s?[>#])\s*$", RegexOptions.Compiled);

        // Satırın başındaki istem (ör. "-> ls \"/ata0a\"" ekosunda)
        private static readonly Regex LeadingPrompt = new Regex(@"^\s*->\s*", RegexOptions.Compiled);

        // VxWorks: sembol tablosunda olmayan fonksiyon çağrılınca "undefined symbol: xdelete" basılır ve
        // fonksiyon HİÇ çalıştırılmaz (kartta bir şey değişmez).
        private static readonly Regex UndefinedSymbolLine =
            new Regex(@"undefined\s+symbol\s*:?\s*([A-Za-z_]\w*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Command { get; private set; }
        /// <summary>Karttan gelen ham metin (tanılama paneli için).</summary>
        public string Raw { get; private set; }
        /// <summary>Eko ve dönüş satırı çıkarılmış metin çıktısı (satırlar).</summary>
        public List<string> OutputLines { get; private set; }
        /// <summary>Kabuğun dönüş değeri; cevapta bulunamadıysa null.</summary>
        public int? ReturnCode { get; private set; }
        public bool TimedOut { get; internal set; }
        /// <summary>Gönderilemedi / bağlı değil gibi durumlarda kullanıcıya gösterilecek Türkçe mesaj.</summary>
        public string Failure { get; private set; }
        /// <summary>
        /// Komutun ekosundan ÖNCE yakalanan metin: başka bir komutun (ör. terminalin) gecikmiş cevabıdır,
        /// bu komuta ait değildir. Oturum bunu terminale geri verir.
        /// </summary>
        public string Preamble { get; private set; }

        /// <summary>Komut kartın imajında (sembol tablosunda) yoksa o komutun adı; varsa null.</summary>
        public string UndefinedSymbol { get; private set; }

        public bool IsSuccess { get { return Failure == null && !TimedOut && UndefinedSymbol == null && ReturnCode == 0; } }

        public static bool HasReturnValue(string raw)
        {
            foreach (string line in raw.Split('\n'))
                if (ValueLine.IsMatch(line)) return true;
            return false;
        }

        // Kabuk hataları: bu durumlarda "value = …" satırı BASILMAZ, ardından doğrudan istem gelir
        private static readonly Regex ShellError =
            new Regex(@"undefined\s+symbol|syntax\s+error", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EndsWithPrompt = new Regex(@"\n\s*->\s*$", RegexOptions.Compiled);

        /// <summary>Kabuk, komutu hiç çalıştırmadan hata verdi (eksik komut / sözdizimi).</summary>
        public static bool HasShellError(string raw)
        {
            return ShellError.IsMatch(raw);
        }

        /// <summary>Çıktı kabuk istemiyle ("-> ") bitiyor: kabuk yeni komut bekliyor.</summary>
        public static bool EndsAtPrompt(string raw)
        {
            return EndsWithPrompt.IsMatch(raw);
        }

        public static ShellResponse Failed(string command, string message, string raw)
        {
            return new ShellResponse { Command = command, Raw = raw ?? "", OutputLines = new List<string>(), Failure = message };
        }

        /// <param name="stripPrompts">false: istem gibi görünen satırları atma (dosya içeriği görüntülerken).</param>
        public static ShellResponse Parse(string command, string raw, bool stripPrompts = true)
        {
            var r = new ShellResponse { Command = command, Raw = raw ?? "", OutputLines = new List<string>(), Preamble = "" };
            Match us = UndefinedSymbolLine.Match(r.Raw);
            if (us.Success) r.UndefinedSymbol = us.Groups[1].Value;
            List<string> lines = r.Raw.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

            // Dönüş değeri: SON "value = N" satırı. Ondan sonrası (istem) çıktıya dahil değildir.
            int valueIdx = -1;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                Match m = ValueLine.Match(lines[i]);
                if (m.Success)
                {
                    int rc;
                    if (int.TryParse(m.Groups[1].Value, out rc)) r.ReturnCode = rc;
                    valueIdx = i;
                    break;
                }
            }
            int end = valueIdx >= 0 ? valueIdx : lines.Count;

            // Komutun ekosu nerede? (ilk eşleşen satır). Öncesi başka bir komuta aittir.
            string cmdTrim = command.Trim();
            int echoIdx = -1;
            for (int i = 0; i < end; i++)
            {
                if (LeadingPrompt.Replace(lines[i], "").Trim() == cmdTrim) { echoIdx = i; break; }
            }
            if (echoIdx > 0)
            {
                var pre = lines.Take(echoIdx).ToList();
                // Ekodan hemen önceki istem ("-> ") komutun kendi satırıdır; terminale iade edilecek kısma dahil değil
                string last = pre[pre.Count - 1];
                Match pm = LeadingPrompt.Match(lines[echoIdx]);
                r.Preamble = string.Join("\n", pre) + "\n";
                if (!pm.Success && PromptOnly.IsMatch(last)) r.Preamble = string.Join("\n", pre);
            }

            for (int i = echoIdx + 1; i < end; i++)
            {
                string line = lines[i];
                if (echoIdx < 0 && r.OutputLines.Count == 0 && line.Trim().Length == 0) continue;
                if (stripPrompts && PromptOnly.IsMatch(line)) continue;
                r.OutputLines.Add(line);
            }
            // Sondaki boş satırları at
            while (r.OutputLines.Count > 0 && r.OutputLines[r.OutputLines.Count - 1].Trim().Length == 0)
                r.OutputLines.RemoveAt(r.OutputLines.Count - 1);
            return r;
        }
    }

    public enum FsKind { Device, Folder, File, Unknown }

    public sealed class FsEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        /// <summary>Unknown: düz "ls" çıktısı türü belirtmiyorsa. Açılmaya çalışılınca Folder ya da File'a döner.</summary>
        public FsKind Kind { get; set; }
        /// <summary>Boyut (bayt); yalnız "ll" çıktısında bilinir.</summary>
        public long? Size { get; set; }
        /// <summary>Değiştirilme zamanı, kartın yazdığı gibi (ör. "Jun 12 14:30"); yalnız "ll" çıktısında.</summary>
        public string Modified { get; set; }
        /// <summary>
        /// Yalnız cihazlar için: içine girilmeye çalışıldı ve kart listelemeyi reddetti (dönüş değeri 0 değil).
        /// Cihaz listede kalır, türünde "girilemiyor" yazar.
        /// </summary>
        public bool NotEnterable { get; set; }
        /// <summary>NotEnterable ise kartın döndürdüğü değer (biliniyorsa).</summary>
        public int? NotEnterableCode { get; set; }
        /// <summary>Yalnız cihazlar için: birimin toplam / boş alanı (dosFsShow ile, arka planda öğrenilir — G34).</summary>
        public DeviceSpace Space { get; set; }

        public string SizeText
        {
            get
            {
                if (Kind == FsKind.Device) return Space != null ? Space.Text : "";
                if (!Size.HasValue || Kind == FsKind.Folder) return "";
                long b = Size.Value;
                if (b < 1024) return b + " B";
                if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#") + " KB";
                return (b / (1024.0 * 1024.0)).ToString("0.##") + " MB";
            }
        }

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case FsKind.Device:
                        return NotEnterable
                            ? "Cihaz · girilemiyor" + (NotEnterableCode.HasValue ? " (" + NotEnterableCode.Value + ")" : "")
                            : "Cihaz";
                    case FsKind.Folder: return "Klasör";
                    case FsKind.File: return "Dosya";
                    default: return "Klasör / dosya";
                }
            }
        }
    }

    /// <summary>Birimin kapasitesi: toplam = sektör sayısı × sektör boyutu, kullanılan = toplam − boş (Windows'taki gibi).</summary>
    public sealed class DeviceSpace
    {
        public long Total;
        /// <summary>Boş alan (bayt); kart yazmadıysa -1.</summary>
        public long Free = -1;

        public bool HasFree { get { return Free >= 0; } }
        public long Used { get { return HasFree ? Math.Max(0, Total - Free) : -1; } }
        public double UsedRatio { get { return HasFree && Total > 0 ? Math.Min(1.0, Math.Max(0.0, (double)Used / Total)) : -1; } }

        /// <summary>Boyut sütunu: "226 MB boş / 250 MB".</summary>
        public string Text
        {
            get { return HasFree ? FormatBytes(Free) + " boş / " + FormatBytes(Total) : FormatBytes(Total); }
        }

        /// <summary>Durum satırı: "250 MB · 24 MB kullanılan (%10) · 226 MB boş".</summary>
        public string Detail
        {
            get
            {
                if (!HasFree) return FormatBytes(Total) + " toplam";
                return FormatBytes(Total) + " toplam  ·  " + FormatBytes(Used) + " kullanılan (%" + Math.Round(UsedRatio * 100) + ")  ·  " +
                       FormatBytes(Free) + " boş";
            }
        }

        public static string FormatBytes(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024L * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / (1024.0 * 1024.0)).ToString("0.#") + " MB";
            return (b / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB";
        }
    }

    /// <summary>
    /// dosFsShow çıktısından birimin kapasitesi (G34). Örnek (VxWorks 6.4):
    /// <code>
    ///  - total number of sectors:            512,000
    ///  - bytes per sector:                   512
    ///  - free space on volume:              237,449,216 bytes
    /// </code>
    /// Sayılardaki binlik ayırıcılar (virgül / nokta) yok sayılır. Toplam bulunamazsa null.
    /// </summary>
    public static class DosFsShowParser
    {
        private static readonly Regex Sectors = new Regex(@"total\s+number\s+of\s+sectors\s*:\s*([\d,\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BytesPerSector = new Regex(@"bytes\s+per\s+sector\s*:\s*([\d,\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FreeSpace = new Regex(@"free\s+space\s+on\s+volume\s*:\s*([\d,\.]+)\s*bytes", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static DeviceSpace Parse(IEnumerable<string> lines)
        {
            if (lines == null) return null;
            string text = string.Join("\n", lines);
            long sectors = Number(Sectors, text), bps = Number(BytesPerSector, text), free = Number(FreeSpace, text);
            if (sectors <= 0 || bps <= 0) return null;
            var s = new DeviceSpace { Total = sectors * bps, Free = free };
            if (s.Free > s.Total) s.Free = s.Total;
            return s;
        }

        private static long Number(Regex re, string text)
        {
            Match m = re.Match(text);
            if (!m.Success) return -1;
            string digits = m.Groups[1].Value.Replace(",", "").Replace(".", "");
            long v;
            return long.TryParse(digits, out v) ? v : -1;
        }
    }

    public static class FsPath
    {
        public static string Join(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return name;
            return dir.EndsWith("/") || dir.EndsWith(":") ? dir + name : dir + "/" + name;
        }

        /// <summary>Kabuk için çift tırnaklı dize: ls "/ata0a/dir"</summary>
        public static string Quote(string path)
        {
            return "\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Kart dosya adı için geçersiz karakterler (kabuk tırnağı ve yol ayırıcıları dahil).</summary>
        public static bool IsValidName(string name, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(name)) { reason = "Ad boş olamaz."; return false; }
            if (name.Trim() == "." || name.Trim() == "..") { reason = "\".\" ve \"..\" ad olarak kullanılamaz."; return false; }
            foreach (char c in name)
            {
                if ("/\\\":*?<>|".IndexOf(c) >= 0 || c < 32) { reason = "Ad şu karakterleri içeremez:  / \\ \" : * ? < > |"; return false; }
            }
            if (name != name.Trim()) { reason = "Ad boşlukla başlayamaz ya da bitemez."; return false; }
            return true;
        }

        public static string LastSegment(string path)
        {
            string p = path.TrimEnd('/');
            int i = Math.Max(p.LastIndexOf('/'), p.LastIndexOf(':'));
            return i >= 0 && i < p.Length - 1 ? p.Substring(i + 1) : p;
        }
    }

    /// <summary>
    /// "devs" çıktısını ayrıştırır ve yalnız NAME sütununu (ls ile açılacak cihaz yolu) döndürür.
    /// Desteklenen biçimler:
    ///   drv refs name          drv name
    ///     0 [ 2] /null           0 /null
    ///     8 [ 1] /ata0a          8 /ata0a
    ///     9    3 /ram0
    /// Başlık satırında "name" sütununun yeri bulunur; refs köşeli parantezli ya da düz sayı olabilir.
    /// </summary>
    public static class DevsParser
    {
        private static readonly Regex Header = new Regex(@"^\s*drv\b.*\bname\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // drv  refs  name  (refs: "[ 2]", "[2]" ya da "2")
        private static readonly Regex ThreeColumns = new Regex(@"^\s*(\d+)\s+(?:\[\s*\d+\s*\]|\d+)\s+(\S.*?)\s*$", RegexOptions.Compiled);
        // drv  name
        private static readonly Regex TwoColumns = new Regex(@"^\s*(\d+)\s+(\S.*?)\s*$", RegexOptions.Compiled);

        public static List<string> Parse(IEnumerable<string> lines)
        {
            var result = new List<string>();
            bool? hasRefs = null;      // başlıktan öğrenilir; başlık yoksa satırın biçiminden anlaşılır
            int nameColumn = -1;

            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (line.Trim().Length == 0) continue;

                if (Header.IsMatch(line))
                {
                    hasRefs = Regex.IsMatch(line, @"\brefs\b", RegexOptions.IgnoreCase);
                    nameColumn = line.IndexOf("name", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                string name = null;
                Match m3 = ThreeColumns.Match(line);
                if (hasRefs != false && m3.Success)
                {
                    name = m3.Groups[2].Value;
                }
                else if (hasRefs == true && nameColumn > 0 && line.Length > nameColumn && line[nameColumn - 1] == ' ')
                {
                    name = line.Substring(nameColumn).Trim();        // sütun hizasından oku (son çare)
                }
                else
                {
                    Match m2 = TwoColumns.Match(line);
                    if (m2.Success) name = m2.Groups[2].Value;
                    else if (line.Trim().IndexOf(' ') < 0) name = line.Trim();
                }

                if (!string.IsNullOrEmpty(name) && !result.Contains(name)) result.Add(name);
            }
            return result;
        }
    }

    /// <summary>
    /// "ls" çıktısını ayrıştırır. Desteklenen biçimler: satır başına bir ad, sütunlu liste (2+ boşluk),
    /// tam yol önekli satırlar, "/" ile biten klasörler, "&lt;DIR&gt;" işaretli satırlar ve uzun biçim (drwx... ad).
    /// Tür belirtilmeyen adlar uzantılıysa Dosya, değilse Unknown olur (açılınca kesinleşir).
    /// </summary>
    public static class LsParser
    {
        // ll: "drwxrwxrwx  1 0       0        2048 Jun 12 14:30 BOOT" (gid sütunu olmayabilir)
        private static readonly Regex LongFormatDetailed = new Regex(
            @"^\s*([dl\-])[rwxsStT\-]{9}\S*\s+(\d+)\s+(\d+)\s+(?:(\d+)\s+)?(\d+)\s+([A-Za-z]{3})\s+(\d{1,2})\s+(\d{1,2}:\d{2}|\d{4})\s+(.+?)\s*$",
            RegexOptions.Compiled);
        // ll başlığı: "Listing Directory /ata0a:"
        private static readonly Regex ListingHeader = new Regex(@"^\s*Listing\s+Director", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LongFormat =
            new Regex(@"^\s*([dl\-])[rwxsStT\-]{9}\S*\s+.*?\s(?:\d{1,2}:\d{2}|\d{4})\s+(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex DirMarker = new Regex(@"<DIR>\s+(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Columns = new Regex(@"\s{2,}|\t", RegexOptions.Compiled);
        private static readonly Regex Extension = new Regex(@"[^.]\.[A-Za-z0-9_]{1,5}$", RegexOptions.Compiled);

        public static List<FsEntry> Parse(IEnumerable<string> lines, string dirPath)
        {
            var result = new List<FsEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (line.Trim().Length == 0 || ListingHeader.IsMatch(line)) continue;

                Match ld = LongFormatDetailed.Match(line);
                if (ld.Success)
                {
                    FsEntry e = Add(result, seen, dirPath, ld.Groups[9].Value, ld.Groups[1].Value == "d" ? FsKind.Folder : FsKind.File);
                    if (e != null)
                    {
                        long size;
                        if (long.TryParse(ld.Groups[5].Value, out size)) e.Size = size;
                        e.Modified = ld.Groups[6].Value + " " + ld.Groups[7].Value + " " + ld.Groups[8].Value;
                    }
                    continue;
                }

                Match lm = LongFormat.Match(line);
                if (lm.Success)
                {
                    Add(result, seen, dirPath, lm.Groups[2].Value, lm.Groups[1].Value == "d" ? FsKind.Folder : FsKind.File);
                    continue;
                }
                Match dm = DirMarker.Match(line);
                if (dm.Success)
                {
                    Add(result, seen, dirPath, dm.Groups[1].Value, FsKind.Folder);
                    continue;
                }
                foreach (string token in Columns.Split(line.Trim()))
                    Add(result, seen, dirPath, token, null);
            }
            return result;
        }

        private static FsEntry Add(List<FsEntry> list, HashSet<string> seen, string dirPath, string token, FsKind? kind)
        {
            string name = token.Trim();
            if (name.Length == 0) return null;

            // Tam yol önekini kaldır: "/ata0a/vxWorks" → "vxWorks"
            string prefix = dirPath.EndsWith("/") || dirPath.EndsWith(":") ? dirPath : dirPath + "/";
            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                name = name.Substring(prefix.Length);

            if (name.EndsWith("/"))
            {
                name = name.TrimEnd('/');
                kind = FsKind.Folder;
            }
            if (name.Length == 0 || name == "." || name == ".." || !seen.Add(name)) return null;

            FsKind k = kind ?? (Extension.IsMatch(name) ? FsKind.File : FsKind.Unknown);
            var entry = new FsEntry { Name = name, FullPath = FsPath.Join(dirPath, name), Kind = k };
            list.Add(entry);
            return entry;
        }
    }

    /// <summary>Dosya gezgininde bir konum: kök (cihaz listesi) ya da bir cihaz altındaki yol.</summary>
    public sealed class FsLocation
    {
        public static readonly FsLocation Root = new FsLocation(null, null);

        public FsLocation(string device, string path)
        {
            Device = device;
            Path = path;
        }

        public string Device { get; private set; }
        public string Path { get; private set; }
        public bool IsRoot { get { return Path == null; } }

        public bool Same(FsLocation other)
        {
            return other != null && Device == other.Device && Path == other.Path;
        }
    }

    public sealed class Breadcrumb
    {
        public string Label { get; set; }
        public FsLocation Location { get; set; }
    }

    /// <summary>Dosya Gezgini işleminin sonucu (kullanıcıya gösterilecek Türkçe mesajla).</summary>
    public sealed class OpResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        /// <summary>Komut kartın imajında yoksa adı (uyarı olarak gösterilir).</summary>
        public string MissingCommand { get; set; }
        public ShellResponse Response { get; set; }
        /// <summary>Hata değil bilgi (ör. "bu bir dosya"): bilgi şeridinde gösterilir.</summary>
        public bool IsNotice { get; set; }
        /// <summary>Hata değil uyarı (ör. içine girilemeyen cihaz): uyarı rengiyle gösterilir.</summary>
        public bool IsWarning { get; set; }

        public static OpResult Success(string message) { return new OpResult { Ok = true, Message = message }; }
        public static OpResult Fail(string message) { return new OpResult { Ok = false, Message = message }; }
    }

    /// <summary>
    /// Bir kartın dosya sistemi için gezinme ve işlem modeli (arayüzden bağımsız, test edilebilir).
    /// Komutları verilen yürütücüyle (kartın MEVCUT oturumu üzerinden) çalıştırır; ayrı bağlantı açmaz.
    /// Listeleme "ll" ile yapılır (tür / boyut / tarih); kartın imajında "ll" yoksa "ls"e dönülür.
    /// Her komut için: komut kartta yoksa ("undefined symbol") uyarı, dönüş değeri 0 değilse hata;
    /// hata durumunda görüntülenen içerik DEĞİŞTİRİLMEZ.
    /// </summary>
    public sealed class FileBrowser
    {
        /// <summary>Kısa komutlar için sessizlik zaman aşımı (sn).</summary>
        public const int ShortTimeoutSec = 4;
        /// <summary>copy / xcopy / xdelete gibi uzun sürebilen komutlar için (sn).</summary>
        public const int LongTimeoutSec = 300;

        private readonly Func<string, int, Task<ShellResponse>> _exec;
        private readonly Stack<FsLocation> _back = new Stack<FsLocation>();
        private bool _loaded;
        private string _listCommand = "ll";

        public FileBrowser(Func<string, int, Task<ShellResponse>> exec)
        {
            _exec = exec;
            Current = FsLocation.Root;
            Entries = new List<FsEntry>();
        }

        public FsLocation Current { get; private set; }
        public List<FsEntry> Entries { get; private set; }
        public bool Loaded { get { return _loaded; } }
        public bool Busy { get; private set; }
        /// <summary>Son işlemin hata mesajı (Türkçe); başarılıysa null.</summary>
        public string Error { get; private set; }
        /// <summary>Son işlem komut eksikliğinden başarısız olduysa true (uyarı rengiyle gösterilir).</summary>
        public bool ErrorIsWarning { get; private set; }
        /// <summary>Bilgi mesajı (ör. "klasör oluşturuldu"); yoksa null.</summary>
        public string Notice { get; private set; }
        /// <summary>Şu an çalışan komut / işlem (yükleniyor durumu için).</summary>
        public string PendingCommand { get; private set; }
        public ShellResponse LastResponse { get; private set; }
        public bool CanGoBack { get { return _back.Count > 0; } }
        /// <summary>Listeleme komutu: "ll" (varsayılan) ya da kartta ll yoksa "ls".</summary>
        public string ListCommand { get { return _listCommand; } }
        /// <summary>
        /// İçine girilmeye çalışılıp girilemeyen cihazlar (cihaz → kartın dönüş değeri). Cihazlar listesinde
        /// "girilemiyor" olarak işaretlenir. Kart değişince / Cihazlar'da Yenile ile temizlenir.
        /// Cihazlar önceden denetlenmez (G27): "devs" ile hepsi hemen listelenir, denetim yalnız içine girerken yapılır.
        /// </summary>
        private readonly Dictionary<string, int?> _notEnterable = new Dictionary<string, int?>(StringComparer.Ordinal);

        /// <summary>İçinde bulunulan klasör (kökteyken null).</summary>
        public string CurrentDirectory { get { return Current.IsRoot ? null : Current.Path; } }

        public event Action Changed;

        public List<Breadcrumb> Breadcrumbs
        {
            get
            {
                var list = new List<Breadcrumb> { new Breadcrumb { Label = "Cihazlar", Location = FsLocation.Root } };
                if (Current.IsRoot) return list;
                string dev = Current.Device;
                list.Add(new Breadcrumb { Label = dev, Location = new FsLocation(dev, dev) });
                string rest = Current.Path.Length > dev.Length ? Current.Path.Substring(dev.Length).Trim('/') : "";
                string acc = dev;
                foreach (string seg in rest.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    acc = FsPath.Join(acc, seg);
                    list.Add(new Breadcrumb { Label = seg, Location = new FsLocation(dev, acc) });
                }
                return list;
            }
        }

        // ---------- Gezinme ----------

        public Task<bool> LoadDevicesAsync()
        {
            if (!Busy) { _notEnterable.Clear(); ClearSpace(); }   // kart yeniden seçildi / bağlandı: işaretler ve boyutlar sıfırlanır
            return NavigateAsync(FsLocation.Root, true, null);
        }

        public Task<bool> OpenAsync(FsEntry entry)
        {
            if (entry.Kind == FsKind.File)
            {
                Error = null;
                Notice = "\"" + entry.Name + "\" bir dosya. İçeriğine bakmak için \"Görüntüle\"yi kullanın.";
                Raise();
                return Task.FromResult(false);
            }
            string device = entry.Kind == FsKind.Device ? entry.FullPath : Current.Device;
            return NavigateAsync(new FsLocation(device, entry.FullPath), true, entry);
        }

        public async Task<bool> BackAsync()
        {
            if (_back.Count == 0 || Busy) return false;
            FsLocation target = _back.Pop();
            bool ok = await NavigateAsync(target, false, null);
            if (!ok) _back.Push(target);       // başarısızsa geri yığını bozulmasın
            return ok;
        }

        public Task<bool> RefreshAsync()
        {
            if (Current.IsRoot && !Busy) { _notEnterable.Clear(); ClearSpace(); }   // Cihazlar'da Yenile: işaretler ve boyutlar yeniden
            return NavigateAsync(Current, false, null);
        }

        public Task<bool> GoToAsync(FsLocation target)
        {
            if (target.Same(Current)) return RefreshAsync();
            return NavigateAsync(target, true, null);
        }

        private async Task<bool> NavigateAsync(FsLocation target, bool pushHistory, FsEntry fromEntry)
        {
            if (Busy) return false;
            BeginWork(target.IsRoot ? "devs" : _listCommand + " " + FsPath.Quote(target.Path));
            try
            {
                List<FsEntry> entries;
                OpResult r = await ListAsync(target, fromEntry);
                if (!r.Ok)
                {
                    if (r.IsNotice) Notice = r.Message;
                    else SetError(r);
                    return false;
                }
                if (r.Message != null) Notice = r.Message;
                entries = _lastListing;

                if (pushHistory && _loaded && !Current.Same(target)) _back.Push(Current);
                Current = target;
                Entries = entries;
                _loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("dosya gezgini " + PendingCommand, ex);
                Error = ErrorText.Describe(ex);
                return false;
            }
            finally
            {
                EndWork();
            }
        }

        private List<FsEntry> _lastListing;

        /// <summary>Konumu listeler (_lastListing'e yazar). Başarısızsa Ok=false ve mesaj.</summary>
        private async Task<OpResult> ListAsync(FsLocation target, FsEntry fromEntry)
        {
            _lastListing = null;
            if (target.IsRoot)
            {
                ShellResponse resp = await Exec("devs", ShortTimeoutSec);
                OpResult common = CheckCommon(resp, "devs");
                if (common != null) return common;
                // devs'in dönüş değeri durum kodu değildir (cihaz sayısını döndürür); kontrol edilmez.
                // Cihazlar önceden denetlenmez: hepsi gösterilir, girilip girilemeyeceği içine girilirken anlaşılır (G27).
                List<string> devices = DevsParser.Parse(resp.OutputLines);
                LastResponse = resp;
                _lastListing = devices.Select(d => DeviceEntry(d)).ToList();
                if (devices.Count == 0)
                    return new OpResult { Ok = true, Message = "Kart \"devs\" komutuna cihaz döndürmedi." };
                return new OpResult { Ok = true };
            }

            string cmd = _listCommand + " " + FsPath.Quote(target.Path);
            PendingCommand = cmd;
            ShellResponse lr = await Exec(cmd, ShortTimeoutSec);
            if (lr.UndefinedSymbol != null && _listCommand == "ll")
            {
                // Kartın imajında ll yok: ls ile dene ve bunu hatırla
                _listCommand = "ls";
                cmd = "ls " + FsPath.Quote(target.Path);
                PendingCommand = cmd;
                lr = await Exec(cmd, ShortTimeoutSec);
            }
            bool openingDevice = fromEntry != null && fromEntry.Kind == FsKind.Device;
            OpResult check = openingDevice
                ? CheckReturn(lr, "\"" + target.Path + "\" cihazının içine girilemiyor",
                              "Bu cihaz bir dosya sistemi değil (ör. /null, /tyCo/0) ya da şu an erişilemiyor.")
                : CheckReturn(lr, "\"" + target.Path + "\" açılamadı", "Erişim yok, yol geçersiz ya da bu bir klasör değil.");
            if (check != null)
            {
                if (openingDevice && check.MissingCommand == null && lr.Failure == null && lr.ReturnCode.HasValue)
                {
                    // Cihaz listede kalır ama "girilemiyor" olarak işaretlenir
                    _notEnterable[fromEntry.FullPath] = lr.ReturnCode;
                    fromEntry.NotEnterable = true;
                    fromEntry.NotEnterableCode = lr.ReturnCode;
                    check.IsWarning = true;
                }
                if (fromEntry != null && fromEntry.Kind == FsKind.Unknown && lr.UndefinedSymbol == null && lr.Failure == null)
                    fromEntry.Kind = FsKind.File;
                return check;
            }

            List<FsEntry> entries = LsParser.Parse(lr.OutputLines, target.Path);

            // Bazı kabuklar dosya için listelemede yalnız dosyanın kendisini gösterir: bu bir klasör değildir
            if (fromEntry != null && fromEntry.Kind != FsKind.Device && entries.Count == 1 &&
                entries[0].Name == fromEntry.Name && entries[0].Kind != FsKind.Folder &&
                lr.OutputLines.Any(l => l.Trim() == fromEntry.Name || l.Trim() == fromEntry.FullPath || l.TrimEnd().EndsWith(" " + fromEntry.Name)))
            {
                fromEntry.Kind = FsKind.File;
                return new OpResult { Ok = false, IsNotice = true, Message = "\"" + fromEntry.Name + "\" bir dosya. İçeriğine bakmak için \"Görüntüle\"yi kullanın." };
            }
            if (fromEntry != null && fromEntry.Kind == FsKind.Unknown) fromEntry.Kind = FsKind.Folder;
            if (openingDevice && fromEntry.NotEnterable)
            {
                // Daha önce girilemeyen cihaz artık açılabiliyor: işaret kaldırılır
                _notEnterable.Remove(fromEntry.FullPath);
                fromEntry.NotEnterable = false;
                fromEntry.NotEnterableCode = null;
            }
            _lastListing = entries;
            return new OpResult { Ok = true };
        }

        // ---------- Cihaz boyutları (G34) ----------

        private const int SpaceTimeoutSec = 20;   // dosFsShow boş alanı hesaplamak için FAT'i tarayabilir
        private readonly Dictionary<string, DeviceSpace> _space = new Dictionary<string, DeviceSpace>(StringComparer.Ordinal);
        private readonly HashSet<string> _spaceTried = new HashSet<string>(StringComparer.Ordinal);
        private bool _spaceRunning;

        /// <summary>Boyutu sorgulanan cihazlar: yalnız /ata… ve /bd… (diskler).</summary>
        public static bool WantsSpace(string device)
        {
            return device != null && (device.StartsWith("/ata", StringComparison.OrdinalIgnoreCase) ||
                                      device.StartsWith("/bd", StringComparison.OrdinalIgnoreCase));
        }

        private void ClearSpace()
        {
            _space.Clear();
            _spaceTried.Clear();
        }

        /// <summary>Yolun bulunduğu cihazın kapasitesi (biliniyorsa), ör. "/ata0a/logs" → /ata0a.</summary>
        public DeviceSpace SpaceForPath(string path, out string device)
        {
            device = null;
            if (string.IsNullOrEmpty(path)) return null;
            DeviceSpace best = null;
            foreach (KeyValuePair<string, DeviceSpace> kv in _space)
            {
                if (path.StartsWith(kv.Key, StringComparison.Ordinal) &&
                    (path.Length == kv.Key.Length || path[kv.Key.Length] == '/') &&
                    (device == null || kv.Key.Length > device.Length))
                {
                    device = kv.Key;
                    best = kv.Value;
                }
            }
            return best;
        }

        /// <summary>
        /// Arka planda bir cihazın boyutunu öğrenir (dosFsShow). Liste hemen gösterilir; boyutlar geldikçe "Boyut"
        /// sütununa yazılır. Yalnız Cihazlar görünürken ve başka işlem yokken, her çağrıda en fazla bir cihaz; her cihaz
        /// bir kez denenir (Yenile / kart değişince yeniden). Kullanıcının komutları kartın komut sırasında bunu bekler,
        /// reddedilmez. "Son cevap" panelini değiştirmez. Bir şey yapıldıysa true.
        /// </summary>
        public async Task<bool> QueryNextSpaceAsync()
        {
            if (_spaceRunning || Busy || !Loaded || !Current.IsRoot) return false;
            string dev = Entries.Where(e => e.Kind == FsKind.Device && WantsSpace(e.FullPath) && !_spaceTried.Contains(e.FullPath))
                                .Select(e => e.FullPath).FirstOrDefault();
            if (dev == null) return false;
            _spaceTried.Add(dev);
            _spaceRunning = true;
            try
            {
                ShellResponse r = await _exec("dosFsShow " + FsPath.Quote(dev), SpaceTimeoutSec);
                if (r != null && r.Failure == null)
                {
                    DeviceSpace sp = DosFsShowParser.Parse(r.OutputLines);
                    if (sp != null)
                    {
                        _space[dev] = sp;
                        foreach (FsEntry e in Entries) if (e.Kind == FsKind.Device && e.FullPath == dev) e.Space = sp;
                        if (_lastListing != null)
                            foreach (FsEntry e in _lastListing) if (e.Kind == FsKind.Device && e.FullPath == dev) e.Space = sp;
                        Raise();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("dosFsShow " + dev, ex);
            }
            finally
            {
                _spaceRunning = false;
            }
            return true;
        }

        private FsEntry DeviceEntry(string device)
        {
            int? code;
            bool bad = _notEnterable.TryGetValue(device, out code);
            DeviceSpace space;
            _space.TryGetValue(device, out space);
            return new FsEntry { Name = device, FullPath = device, Kind = FsKind.Device, NotEnterable = bad, NotEnterableCode = bad ? code : null, Space = space };
        }

        // ---------- Dosya / klasör işlemleri ----------

        public Task<OpResult> CreateFolderAsync(string name)
        {
            string reason;
            if (Current.IsRoot) return Task.FromResult(Reject("Klasör oluşturmak için önce bir cihaz açın."));
            if (!FsPath.IsValidName(name, out reason)) return Task.FromResult(Reject(reason));
            if (Entries.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(Reject("\"" + name + "\" adında bir öğe zaten var."));
            string path = FsPath.Join(Current.Path, name);
            return RunAsync("mkdir " + FsPath.Quote(path), ShortTimeoutSec,
                "\"" + name + "\" klasörü oluşturulamadı", "\"" + name + "\" klasörü oluşturuldu.");
        }

        // ---------- Korunan öğeler ----------

        /// <summary>Kartın açılışı / çalışması için gerekli öğe: bu adlı dosya ya da klasör ve içindekiler silinemez, taşınamaz, yeniden adlandırılamaz.</summary>
        public const string ProtectedName = "EFI";

        public static string ProtectedMessage(string name)
        {
            return "\"" + name + "\" korumalıdır: \"" + ProtectedName + "\" kartın çalışması (açılışı) için gereklidir; " +
                   "silinemez, taşınamaz ve yeniden adlandırılamaz.";
        }

        /// <summary>Ad tam olarak EFI mi (büyük / küçük harf duyarsız; klasörlerin sonundaki / yok sayılır)?</summary>
        public static bool IsProtectedName(string name)
        {
            if (name == null) return false;
            return string.Equals(name.Trim().TrimEnd('/'), ProtectedName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Yol EFI'nin kendisi ya da içinde bir öğe mi (yolun herhangi bir parçası EFI)?</summary>
        public static bool IsProtectedPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;
            foreach (string part in fullPath.Split('/'))
                if (IsProtectedName(part)) return true;
            return false;
        }

        /// <summary>Korumalı öğe için uyarıyı şeritte gösterir (karta komut gönderilmez).</summary>
        public OpResult RejectProtected(FsEntry e)
        {
            return Reject(ProtectedMessage(e == null ? ProtectedName : e.Name));
        }

        public static bool IsProtected(FsEntry e)
        {
            return e != null && e.Kind != FsKind.Device && (IsProtectedName(e.Name) || IsProtectedPath(e.FullPath));
        }

        /// <summary>
        /// Klasörün içinde (alt klasörler dahil) EFI var mı? Silme (xdelete) ve taşıma öncesi denetlenir: klasörler listeleme
        /// komutuyla tek tek açılır. Bulunursa yolunu, bulunamazsa null döndürür; çok büyük ağaçta denetim tamamlanamazsa
        /// güvenli tarafta kalınır ("denetlenemedi" metni döner).
        /// </summary>
        private async Task<string> FindProtectedInsideAsync(string folder)
        {
            const int MaxFolders = 300;
            var queue = new Queue<string>();
            queue.Enqueue(folder);
            int visited = 0;
            BeginWork("EFI denetimi  ·  " + _listCommand + " " + FsPath.Quote(folder));
            try
            {
                while (queue.Count > 0)
                {
                    if (++visited > MaxFolders) return "(" + MaxFolders + " klasörden fazlası var, EFI denetimi tamamlanamadı)";
                    string dir = queue.Dequeue();
                    string cmd = _listCommand + " " + FsPath.Quote(dir);
                    PendingCommand = "EFI denetimi  ·  " + cmd;
                    Raise();
                    ShellResponse r = await Exec(cmd, ShortTimeoutSec);
                    if (r.Failure != null || !r.IsSuccess) continue;   // açılamayan klasör: içinde ne olduğu bilinemez, atlanır
                    foreach (FsEntry child in LsParser.Parse(r.OutputLines, dir))
                    {
                        if (IsProtectedName(child.Name)) return child.FullPath;
                        if (child.Kind == FsKind.Folder) queue.Enqueue(child.FullPath);
                    }
                }
                return null;
            }
            finally
            {
                EndWork();
            }
        }

        /// <summary>Öğe ya da (klasörse) içindekiler korumalıysa reddeder; değilse null.</summary>
        private async Task<OpResult> CheckProtectedAsync(FsEntry entry, bool scanInside)
        {
            if (IsProtected(entry)) return Reject(ProtectedMessage(entry.Name));
            if (scanInside && entry.Kind != FsKind.File)
            {
                if (Busy) return Reject("Başka bir işlem sürüyor.");
                string found = await FindProtectedInsideAsync(entry.FullPath);
                if (found != null)
                    return Reject("\"" + entry.Name + "\" silinemez / taşınamaz: içinde " + found + " var. " +
                                  "\"" + ProtectedName + "\" kartın çalışması (açılışı) için gereklidir.");
            }
            return null;
        }

        /// <param name="recursive">Klasör içindekilerle birlikte silinsin (xdelete). false: yalnız boş klasör (rmdir).</param>
        public async Task<OpResult> DeleteAsync(FsEntry entry, bool recursive)
        {
            if (entry.Kind == FsKind.Device) return Reject("Cihazlar silinemez.");
            // EFI korumalı: kendisi, içindekiler ve EFI içeren klasörler (xdelete) silinemez. rmdir yalnız boş klasörü siler.
            OpResult blocked = await CheckProtectedAsync(entry, recursive);
            if (blocked != null) return blocked;
            string cmd;
            int timeout = ShortTimeoutSec;
            if (entry.Kind == FsKind.File) cmd = "rm " + FsPath.Quote(entry.FullPath);
            else if (recursive) { cmd = "xdelete " + FsPath.Quote(entry.FullPath); timeout = LongTimeoutSec; }
            else cmd = "rmdir " + FsPath.Quote(entry.FullPath);

            string failHint = entry.Kind == FsKind.File ? null
                : recursive ? null : "Klasör boş değilse \"İçindekilerle birlikte sil\"i kullanın.";
            return await RunAsync(cmd, timeout, "\"" + entry.Name + "\" silinemedi", "\"" + entry.Name + "\" silindi.", failHint);
        }

        public async Task<OpResult> RenameAsync(FsEntry entry, string newName)
        {
            string reason;
            if (entry.Kind == FsKind.Device) return Reject("Cihazlar yeniden adlandırılamaz.");
            if (IsProtected(entry)) return Reject(ProtectedMessage(entry.Name));
            if (!FsPath.IsValidName(newName, out reason)) return Reject(reason);
            if (newName == entry.Name) return Reject("Yeni ad eskisiyle aynı.");
            if (Entries.Any(e => e != entry && string.Equals(e.Name, newName, StringComparison.OrdinalIgnoreCase)))
                return Reject("\"" + newName + "\" adında bir öğe zaten var.");
            // EFI içeren bir klasörün adı değişirse EFI'nin yolu da değişir: o da engellenir
            if (entry.Kind != FsKind.File)
            {
                OpResult blocked = await CheckProtectedAsync(entry, true);
                if (blocked != null) return blocked;
            }
            string dst = FsPath.Join(ParentOf(entry), newName);
            return await RunAsync("mv " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst), ShortTimeoutSec,
                "\"" + entry.Name + "\" yeniden adlandırılamadı", "\"" + entry.Name + "\" → \"" + newName + "\"");
        }

        /// <summary>Öğeyi kart içinde başka bir klasöre taşır (mv) ya da kopyalar (dosya: copy, klasör: xcopy).</summary>
        public async Task<OpResult> TransferAsync(FsEntry entry, string destDir, bool copy)
        {
            if (entry.Kind == FsKind.Device) return Reject("Cihazlar taşınamaz / kopyalanamaz.");
            if (string.IsNullOrWhiteSpace(destDir)) return Reject("Hedef klasör belirtilmedi.");
            destDir = destDir.TrimEnd('/');
            if (destDir == ParentOf(entry).TrimEnd('/'))
                return Reject("Öğe zaten bu klasörde.");
            if (destDir == entry.FullPath || destDir.StartsWith(entry.FullPath + "/"))
                return Reject("Bir klasör kendi içine taşınamaz / kopyalanamaz.");
            if (!copy)
            {
                // EFI taşınırsa (yerinden çıkarsa) kart açılamaz: kendisi, içindekiler ve EFI içeren klasörler taşınamaz
                OpResult blocked = await CheckProtectedAsync(entry, true);
                if (blocked != null) return blocked;
            }

            string dst = FsPath.Join(destDir, entry.Name);
            string verb = copy ? "kopyalandı" : "taşındı";
            string cmd;
            if (!copy) cmd = "mv " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            else if (entry.Kind == FsKind.File) cmd = "copy " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            else cmd = "xcopy " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            return await RunAsync(cmd, copy ? LongTimeoutSec : ShortTimeoutSec,
                "\"" + entry.Name + "\" " + (copy ? "kopyalanamadı" : "taşınamadı"),
                "\"" + entry.Name + "\" → " + destDir + " " + verb + ".");
        }

        /// <summary>
        /// Metin dosyasının içeriğini okur: tek argümanlı "copy" dosyayı kabuğun çıktısına basar.
        /// Dönüş: Ok ise Message = içerik.
        /// </summary>
        public async Task<OpResult> ReadTextAsync(FsEntry entry)
        {
            if (entry.Kind != FsKind.File && entry.Kind != FsKind.Unknown) return Reject("Yalnız dosyalar görüntülenebilir.");
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            string cmd = "copy " + FsPath.Quote(entry.FullPath);
            BeginWork(cmd);
            try
            {
                ShellResponse resp = await Exec(cmd, LongTimeoutSec);
                OpResult bad = CheckReturn(resp, "\"" + entry.Name + "\" okunamadı", null);
                if (bad != null) { SetError(bad); return bad; }
                // Dosya içeriği: istem gibi görünen satırlar da korunur
                ShellResponse raw = ShellResponse.Parse(cmd, resp.Raw, false);
                return new OpResult { Ok = true, Message = string.Join("\n", raw.OutputLines), Response = resp };
            }
            finally
            {
                EndWork();
            }
        }

        /// <summary>
        /// Bilgisayardan karta yükleme: dosyalar önce kartın erişebildiği FTP sunucusuna gönderilir,
        /// sonra kartta FTP konumundan içinde bulunulan klasöre kopyalanır. Dosya: tek "copy". Klasör: xcopy
        /// KULLANILMAZ (FTP üzerinden özyinelemeli xcopy kartın kabuğunu / telnet oturumunu düşürüyordu); klasör
        /// yapısı bilgisayarda bilindiği için kartta her alt klasör "mkdir" ile açılır, her dosya ayrı "copy" ile kopyalanır.
        /// </summary>
        /// <param name="destDir">Hedef klasör; null ise içinde bulunulan klasör.</param>
        /// <param name="tracker">Kartın FTP'de bıraktığı kopyalar: her öğe FTP'ye gönderilmeye başlarken eklenir
        /// ("FTP kopyalarını sil" düğmesi hemen etkinleşir), yalnız gerçekten silinince çıkarılır.</param>
        public async Task<OpResult> UploadAsync(IList<string> localPaths, IFileUploader uploader, TransferSettings settings, IProgress<string> progress, string destDir = null, FtpCopyTracker tracker = null)
        {
            if (Current.IsRoot && destDir == null) return Reject("Yüklemek için önce kartta bir klasör açın.");
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            if (settings == null || !settings.IsConfigured) return Reject("Kartın FTP bilgisi yok: FTP sunucusu / kullanıcı / parola karttan (version) alınamadı.");
            destDir = destDir ?? Current.Path;
            int ok = 0;
            var failures = new List<string>();
            BeginWork("yükleme");
            try
            {
                string modeNote = null;
                // Dosyalar FTP'nin kendisine değil, içindeki Vision çalışma klasörüne gönderilir (yarım kalan kopyalar
                // sunucudaki diğer dosyalarla karışmasın); klasör yoksa oluşturulur
                PendingCommand = "FTP'de " + TransferSettings.WorkFolderName + " klasörü hazırlanıyor";
                Raise();
                try { await uploader.EnsureWorkFolderAsync(); }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("FTP çalışma klasörü", ex);
                    var r = OpResult.Fail("FTP'de \"" + TransferSettings.WorkFolderName + "\" klasörü oluşturulamadı: " + FtpErrorText(ex));
                    SetError(r);
                    return r;
                }
                for (int i = 0; i < localPaths.Count; i++)
                {
                    string local = localPaths[i].TrimEnd('\\', '/');
                    string name = System.IO.Path.GetFileName(local);
                    bool isDir = System.IO.Directory.Exists(local);
                    string step = "(" + (i + 1) + "/" + localPaths.Count + ") " + name;

                    // Yerel öğe gerçekten var mı ve adı kartın kabuğunda yazılabilir mi? (FTP'ye hiç gönderilmeden denetlenir)
                    string nameProblem = UploadNameProblem(local, name, isDir);
                    FolderPlan plan = null;
                    if (nameProblem == null && isDir)
                    {
                        plan = FolderPlan.Build(local);
                        nameProblem = plan.Problem;
                    }
                    if (nameProblem != null) { failures.Add(name + ": " + nameProblem); continue; }
                    if (tracker != null) tracker.Add(name, isDir);

                    // 1) FTP sunucusuna gönder
                    PendingCommand = "FTP'ye gönderiliyor " + step;
                    Raise();
                    if (progress != null) progress.Report(PendingCommand);
                    try
                    {
                        if (isDir) await uploader.UploadDirectoryAsync(local, name);
                        else await uploader.UploadFileAsync(local, name);
                    }
                    catch (Exception ex)
                    {
                        ErrorText.LogTechnical("FTP yükleme " + name, ex);
                        failures.Add(name + ": FTP'ye gönderilemedi (" + FtpErrorText(ex) + ")");
                        continue;
                    }
                    var ftp = uploader as FtpUploader;
                    if (ftp != null && ftp.ModeNote != null) modeNote = ftp.ModeNote;

                    // 2) Kartta FTP konumundan hedef klasöre kopyala
                    string src = FsPath.Join(settings.CardPathPrefix, name);
                    string dst = FsPath.Join(destDir, name);
                    if (isDir)
                    {
                        string dirFailure = await CopyFolderFromFtpAsync(plan, src, dst, step, progress);
                        if (dirFailure != null)
                        {
                            failures.Add(name + ": " + dirFailure);
                            if (Error != null) break;      // eksik komut / bağlantı koptu: sonraki öğeler denenmez
                            continue;
                        }
                        ok++;
                        if (settings.DeleteAfterCopy)
                        {
                            try { await uploader.DeleteAsync(name, true); if (tracker != null) tracker.Remove(name); }
                            catch (Exception ex) { ErrorText.LogTechnical("FTP silme " + name, ex); }
                        }
                        continue;
                    }
                    string cmd = "copy " + FsPath.Quote(src) + ", " + FsPath.Quote(dst);
                    PendingCommand = "Kartta kopyalanıyor " + step + "  ·  " + cmd;
                    Raise();
                    if (progress != null) progress.Report(PendingCommand);
                    ShellResponse resp = await Exec(cmd, LongTimeoutSec);
                    OpResult bad = CheckReturn(resp, name + ": FTP'ye gönderildi ama kartta kopyalanamadı (kaynak " + src + ")",
                        "Kart bu yolu göremiyor olabilir: FTP ayarlarında \"Bağlantıyı sına\" kartın FTP klasörünü görüp görmediğini denetler.");
                    if (bad != null) { failures.Add(bad.Message); if (bad.MissingCommand != null) { SetError(bad); break; } continue; }
                    ok++;

                    // 3) İsteğe bağlı: FTP'deki geçici kopyayı sil
                    if (settings.DeleteAfterCopy)
                    {
                        try { await uploader.DeleteAsync(name, isDir); if (tracker != null) tracker.Remove(name); }
                        catch (Exception ex) { ErrorText.LogTechnical("FTP silme " + name, ex); }
                    }
                }

                await RefreshListingQuietly();
                if (failures.Count == 0)
                {
                    Notice = ok + " öğe karta yüklendi → " + destDir + (modeNote != null ? "  ·  " + modeNote : "");
                    return OpResult.Success(Notice);
                }
                if (Error == null)
                {
                    Error = (ok > 0 ? ok + " öğe yüklendi; " : "") + failures.Count + " öğe yüklenemedi: " + string.Join(" · ", failures);
                    ErrorIsWarning = false;
                }
                return OpResult.Fail(Error);
            }
            finally
            {
                EndWork();
            }
        }

        /// <summary>
        /// Kart dosyasının bilgisayarda bir KOPYASINI oluşturur ("Birlikte aç" için; kart dosyası değişmez).
        /// FTP ayarlıysa: kartta copy ile FTP konumuna geçici adla kopyalanır, FTP'den indirilir, geçici kopya silinir
        /// (ikili dosyalar da bozulmadan gelir). FTP ayarlı değilse ve textFallback true ise içerik telnet üzerinden
        /// metin olarak okunup UTF-8 yazılır. Dönüş: Ok ise Message = kullanılan yöntem ("FTP" / "metin").
        /// </summary>
        public async Task<OpResult> FetchCopyAsync(FsEntry entry, string localPath, IFileUploader ftp, TransferSettings settings, bool textFallback)
        {
            if (entry.Kind != FsKind.File && entry.Kind != FsKind.Unknown) return Reject("Yalnız dosyalar açılabilir.");
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            bool useFtp = ftp != null && settings != null && settings.IsConfigured;
            if (!useFtp && !textFallback) return Reject("Bu dosyayı açmak için önce \"FTP ayarları\"nı girin.");

            if (!useFtp)
            {
                OpResult text = await ReadTextAsync(entry);
                if (!text.Ok) return text;
                try { System.IO.File.WriteAllText(localPath, (text.Message ?? "").Replace("\n", "\r\n"), new System.Text.UTF8Encoding(false)); }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("salt okunur kopya yazma", ex);
                    var w = OpResult.Fail("\"" + entry.Name + "\" bilgisayara yazılamadı: " + ErrorText.Describe(ex));
                    SetError(w); Raise();
                    return w;
                }
                return new OpResult { Ok = true, Message = "metin", Response = text.Response };
            }

            string temp = "_tkt_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + entry.Name;
            string cmd = "copy " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(FsPath.Join(settings.CardPathPrefix, temp));
            BeginWork("Kartta FTP konumuna kopyalanıyor  ·  " + cmd);
            bool onFtp = false;
            try
            {
                // Geçici kopya da FTP'deki Vision klasörüne alınır: kart oraya yazabilsin diye klasör önce oluşturulur
                try { await ftp.EnsureWorkFolderAsync(); }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("FTP çalışma klasörü", ex);
                    var r = OpResult.Fail("FTP'de \"" + TransferSettings.WorkFolderName + "\" klasörü oluşturulamadı: " + FtpErrorText(ex));
                    SetError(r);
                    return r;
                }
                ShellResponse resp = await Exec(cmd, LongTimeoutSec);
                OpResult bad = CheckReturn(resp, "\"" + entry.Name + "\" FTP konumuna kopyalanamadı", null);
                if (bad != null) { SetError(bad); return bad; }
                onFtp = true;

                PendingCommand = "FTP'den indiriliyor  ·  " + entry.Name;
                Raise();
                try { await ftp.DownloadFileAsync(temp, localPath); }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("FTP indirme " + temp, ex);
                    var r = OpResult.Fail("\"" + entry.Name + "\" FTP'den indirilemedi (" + FtpErrorText(ex) + ").");
                    SetError(r);
                    return r;
                }
                return new OpResult { Ok = true, Message = "FTP", Response = resp };
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("dosya gezgini " + cmd, ex);
                var r = OpResult.Fail("\"" + entry.Name + "\" açılamadı: " + ErrorText.Describe(ex));
                SetError(r);
                return r;
            }
            finally
            {
                // Geçici kopya bizim: ayardan bağımsız her zaman silinir
                if (onFtp)
                {
                    try { await ftp.DeleteAsync(temp, false); }
                    catch (Exception ex) { ErrorText.LogTechnical("FTP geçici silme " + temp, ex); }
                }
                EndWork();
            }
        }

        /// <summary>Kart klasörünün içeriği (bilgisayara indirmek için): yollar klasöre göre, "\\" ile.</summary>
        public sealed class FolderTree
        {
            public readonly List<string> Folders = new List<string>();
            public readonly List<KeyValuePair<string, FsEntry>> Files = new List<KeyValuePair<string, FsEntry>>();
        }

        /// <summary>
        /// Klasörü alt klasörleriyle birlikte tarar (her klasör listeleme komutuyla açılır) — G31. Karttan bilgisayara
        /// klasör sürüklenince / kaydedilince hangi dosyaların indirileceği buradan çıkar. Açılamayan klasör hata sayılır
        /// (eksik kopya oluşmasın). Çok büyük ağaçlar reddedilir.
        /// </summary>
        public async Task<OpResult> CollectTreeAsync(FsEntry folder, FolderTree tree)
        {
            const int MaxFolders = 300, MaxFiles = 3000;
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            var queue = new Queue<KeyValuePair<string, string>>();   // kart yolu, bilgisayardaki göreli yol
            queue.Enqueue(new KeyValuePair<string, string>(folder.FullPath, ""));
            int visited = 0;
            BeginWork("Klasör taranıyor  ·  " + folder.FullPath);
            try
            {
                while (queue.Count > 0)
                {
                    if (++visited > MaxFolders)
                    {
                        var big = OpResult.Fail("\"" + folder.Name + "\" çok büyük (" + MaxFolders + " klasörden fazla); alt klasörleri ayrı ayrı alın.");
                        SetError(big);
                        return big;
                    }
                    KeyValuePair<string, string> item = queue.Dequeue();
                    string cmd = _listCommand + " " + FsPath.Quote(item.Key);
                    PendingCommand = "Klasör taranıyor  ·  " + cmd;
                    Raise();
                    ShellResponse r = await Exec(cmd, ShortTimeoutSec);
                    OpResult bad = CheckReturn(r, "\"" + item.Key + "\" listelenemedi", null);
                    if (bad != null) { SetError(bad); return bad; }
                    foreach (FsEntry child in LsParser.Parse(r.OutputLines, item.Key))
                    {
                        string rel = item.Value.Length == 0 ? ReadOnlyCopies.SafeSegment(child.Name)
                                                            : item.Value + "\\" + ReadOnlyCopies.SafeSegment(child.Name);
                        if (child.Kind == FsKind.Folder)
                        {
                            tree.Folders.Add(rel);
                            queue.Enqueue(new KeyValuePair<string, string>(child.FullPath, rel));
                        }
                        else
                        {
                            tree.Files.Add(new KeyValuePair<string, FsEntry>(rel, child));
                            if (tree.Files.Count > MaxFiles)
                            {
                                var many = OpResult.Fail("\"" + folder.Name + "\" çok fazla dosya içeriyor (" + MaxFiles + "+); alt klasörleri ayrı ayrı alın.");
                                SetError(many);
                                return many;
                            }
                        }
                    }
                }
                return new OpResult { Ok = true };
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("klasör tarama " + folder.FullPath, ex);
                var r = OpResult.Fail("\"" + folder.Name + "\" taranamadı: " + ErrorText.Describe(ex));
                SetError(r);
                return r;
            }
            finally
            {
                EndWork();
            }
        }

        /// <summary>Arayüzün bilgi şeridinde göstermek istediği iletiyi ayarlar.</summary>
        public void ShowNotice(string message)
        {
            Notice = message;
            Error = null;
            ErrorIsWarning = false;
            Raise();
        }

        /// <summary>Arayüzden gelen hatayı/uyarıyı şeritte gösterir.</summary>
        public void ShowWarning(string message)
        {
            Error = message;
            ErrorIsWarning = true;
            Notice = null;
            Raise();
        }

        /// <summary>FTP hatasının Türkçe açıklaması: kısa neden + (varsa) sunucunun cevabı ve adres.</summary>
        private static string FtpErrorText(Exception ex)
        {
            var fo = ex as FtpOperationException;
            Exception inner = fo != null && fo.InnerException != null ? fo.InnerException : ex;
            var we = inner as System.Net.WebException;
            var resp = we != null ? we.Response as System.Net.FtpWebResponse : null;
            string text;
            if (resp != null && resp.StatusCode != System.Net.FtpStatusCode.Undefined)
            {
                switch (resp.StatusCode)
                {
                    case System.Net.FtpStatusCode.NotLoggedIn: text = "kullanıcı adı ya da parola hatalı"; break;
                    case System.Net.FtpStatusCode.ActionNotTakenFileUnavailable:
                        text = "FTP klasörüne yazılamadı: kullanıcının bu klasörde yazma izni yok ya da klasör yok"; break;
                    default: text = FtpDiagnostics.Describe(inner); break;
                }
            }
            else if (we != null && we.Status == System.Net.WebExceptionStatus.ConnectFailure) text = "FTP sunucusuna bağlanılamadı";
            else if (we != null && we.Status == System.Net.WebExceptionStatus.NameResolutionFailure) text = "FTP sunucusu adresi bulunamadı";
            else if (fo != null && fo.InnerException == null) text = fo.Message;
            else if (inner is System.IO.IOException) text = "yerel dosya okunamadı";
            else text = FtpDiagnostics.Describe(inner);
            if (fo != null)
            {
                if (!string.IsNullOrEmpty(fo.ServerMessage) && text.IndexOf(fo.ServerMessage, StringComparison.Ordinal) < 0) text += " — sunucu: " + fo.ServerMessage;
                if (!string.IsNullOrEmpty(fo.Url)) text += " · " + fo.Url;
            }
            return text;
        }

        /// <summary>
        /// Bilgisayardaki bir klasörün kartta yeniden kurulacak yapısı: alt klasörler (üstten alta) ve dosyalar,
        /// klasöre göre "/" ile ayrılmış göreli yollarla. Adı kartın kabuğunda yazılamayan öğe varsa Problem dolu olur.
        /// </summary>
        public sealed class FolderPlan
        {
            public readonly List<string> Dirs = new List<string>();
            public readonly List<string> Files = new List<string>();
            public string Problem;

            public static FolderPlan Build(string localDir)
            {
                var plan = new FolderPlan();
                var bad = new List<string>();
                Walk(localDir, "", plan, bad);
                if (bad.Count > 0)
                    plan.Problem = "klasörün içinde adı kartın kabuğunda yazılamayan öğeler var (Türkçe / özel karakter ya da çift tırnak): " +
                                   string.Join(", ", bad.Take(5)) + (bad.Count > 5 ? " … (+" + (bad.Count - 5) + ")" : "") +
                                   ". Bu öğeleri İngilizce karakterlerle yeniden adlandırın";
                return plan;
            }

            private static void Walk(string dir, string rel, FolderPlan plan, List<string> bad)
            {
                foreach (string d in System.IO.Directory.GetDirectories(dir))
                {
                    string n = System.IO.Path.GetFileName(d);
                    string r = rel.Length == 0 ? n : rel + "/" + n;
                    if (!ShellSafeName(n)) { bad.Add(r); continue; }
                    plan.Dirs.Add(r);
                    Walk(d, r, plan, bad);
                }
                foreach (string f in System.IO.Directory.GetFiles(dir))
                {
                    string n = System.IO.Path.GetFileName(f);
                    string r = rel.Length == 0 ? n : rel + "/" + n;
                    if (!ShellSafeName(n)) { bad.Add(r); continue; }
                    plan.Files.Add(r);
                }
            }

            private static bool ShellSafeName(string n)
            {
                return !string.IsNullOrEmpty(n) && n.IndexOf('"') < 0 && !n.Any(c => c > 126 || c < 32);
            }
        }

        /// <summary>
        /// FTP'ye gönderilmiş klasörü kartta, xcopy kullanmadan kurar: hedef klasör ve alt klasörler "mkdir",
        /// her dosya ayrı "copy". Klasör zaten varsa mkdir hatası yok sayılır (dosyalar yine kopyalanır).
        /// Bir dosya kopyalanamazsa diğerleri denenmeye devam eder; bağlantı koparsa ya da komut kartta yoksa durur.
        /// Dönüş: sorun yoksa null, varsa açıklama.
        /// </summary>
        private async Task<string> CopyFolderFromFtpAsync(FolderPlan plan, string srcRoot, string dstRoot, string step, IProgress<string> progress)
        {
            var dirs = new List<string> { "" };
            dirs.AddRange(plan.Dirs);
            foreach (string rel in dirs)
            {
                string dst = rel.Length == 0 ? dstRoot : FsPath.Join(dstRoot, rel);
                string cmd = "mkdir " + FsPath.Quote(dst);
                PendingCommand = "Kartta klasör açılıyor " + step + "  ·  " + cmd;
                Raise();
                if (progress != null) progress.Report(PendingCommand);
                ShellResponse r = await Exec(cmd, ShortTimeoutSec);
                OpResult common = CheckCommon(r, cmd);
                if (common != null && (r.Failure != null || common.MissingCommand != null))
                {
                    SetError(common);
                    return "kartta " + dst + " klasörü açılamadı (" + common.Message + ")";
                }
                // Dönüş değeri 0 değilse klasör büyük olasılıkla zaten var: devam edilir, dosya kopyası sonucu belirler
            }

            var failed = new List<string>();
            for (int k = 0; k < plan.Files.Count; k++)
            {
                string rel = plan.Files[k];
                string src = FsPath.Join(srcRoot, rel);
                string dst = FsPath.Join(dstRoot, rel);
                string cmd = "copy " + FsPath.Quote(src) + ", " + FsPath.Quote(dst);
                PendingCommand = "Kartta kopyalanıyor " + step + "  ·  dosya " + (k + 1) + "/" + plan.Files.Count + "  ·  " + cmd;
                Raise();
                if (progress != null) progress.Report(PendingCommand);
                ShellResponse resp = await Exec(cmd, LongTimeoutSec);
                OpResult bad = CheckReturn(resp, rel, null);
                if (bad == null) continue;
                if (resp.Failure != null || bad.MissingCommand != null)
                {
                    SetError(bad);
                    return "kartta kopyalama durdu (" + bad.Message + "); " + k + "/" + plan.Files.Count + " dosya kopyalanmıştı";
                }
                failed.Add(rel + (resp.ReturnCode.HasValue ? " (" + resp.ReturnCode.Value + ")" : ""));
            }
            if (failed.Count == 0) return null;
            return "FTP'ye gönderildi ama kartta " + failed.Count + "/" + plan.Files.Count + " dosya kopyalanamadı: " +
                   string.Join(", ", failed.Take(5)) + (failed.Count > 5 ? " …" : "") + " (kaynak " + srcRoot + ")";
        }

        /// <summary>Yüklenecek öğenin adı / varlığıyla ilgili sorun (yoksa null). Kartın kabuğu ASCII dışı adları güvenle işleyemez.</summary>
        public static string UploadNameProblem(string localPath, string name, bool isDir)
        {
            if (string.IsNullOrEmpty(name)) return "geçersiz yol";
            if (!isDir && !System.IO.File.Exists(localPath)) return "bilgisayarda bulunamadı (" + localPath + ")";
            if (name.IndexOf('"') >= 0) return "adında çift tırnak (\") var; kartın kabuğu bu adı işleyemez";
            if (name.Any(c => c > 126 || c < 32))
                return "adında Türkçe ya da özel karakter var (" + new string(name.Where(c => c > 126 || c < 32).Distinct().ToArray()) +
                       "); kartın kabuğu bu adı doğru yazamaz. Dosyayı İngilizce karakterlerle yeniden adlandırın";
            return null;
        }

        // ---------- Kart başına FTP ----------

        /// <summary>
        /// Kartın FTP bilgisini (sunucu, kullanıcı, parola) kartın kendisinden alır (version). Ham cevap
        /// tanılama paneline parolası gizlenmiş olarak yazılır.
        /// </summary>
        public async Task<CardFtpResult> ObtainFtpAsync()
        {
            if (Busy) return new CardFtpResult { Error = "Başka bir işlem sürüyor." };
            BeginWork(CardFtpConfigurator.Command + "  ·  kartın FTP bilgisi alınıyor");
            try
            {
                CardFtpResult r = await CardFtpConfigurator.ObtainAsync((c, t) => Exec(c, t));
                if (r.MaskedResponse != null) LastResponse = r.MaskedResponse;   // parola hiçbir zaman panelde görünmez
                if (!r.Ok)
                {
                    Error = r.Error;
                    ErrorIsWarning = r.MissingCommand != null;
                }
                return r;
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("version (FTP bilgisi)", ex);
                var r = new CardFtpResult { Error = "Kartın FTP bilgisi alınamadı: " + ErrorText.Describe(ex) };
                Error = r.Error;
                return r;
            }
            finally
            {
                EndWork();
            }
        }

        public sealed class FtpTestStep
        {
            public string Name;
            public bool Ok;
            public bool Skipped;
            public string Detail;
        }

        /// <summary>
        /// FTP'nin gerçekten kullanılabildiğini aşama aşama sınar (yalnız bağlantı değil): oturum + listeleme,
        /// FTP'ye yazma, KARTIN aynı dosyayı görmesi ve silme. Bağlantı sınaması başarılı olsa da yükleme
        /// başarısız olabildiği için yazma ve kart tarafı ayrıca denenir.
        /// </summary>
        public async Task<List<FtpTestStep>> TestFtpAsync(IFileUploader up, TransferSettings eff, IProgress<string> progress)
        {
            var steps = new List<FtpTestStep>();
            string probe = "_vision_sinama_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt";
            Func<string, Func<Task<string>>, Task<bool>> run = async (name, op) =>
            {
                if (progress != null) progress.Report(name + "…");
                var st = new FtpTestStep { Name = name };
                steps.Add(st);
                try { st.Detail = await op(); st.Ok = true; }
                catch (Exception ex) { st.Detail = FtpErrorText(ex); ErrorText.LogTechnical("FTP sınama: " + name, ex); }
                return st.Ok;
            };

            if (!await run("FTP oturumu ve " + TransferSettings.WorkFolderName + " çalışma klasörü",
                    async () => { await up.EnsureWorkFolderAsync(); await up.TestAsync(); return "ftp://" + eff.FtpHost + eff.NormalizedRemoteDir; }))
                return steps;
            if (!await run("FTP'ye yazma (deneme dosyası)", async () => { await up.UploadBytesAsync(System.Text.Encoding.ASCII.GetBytes("Vision FTP sinamasi\r\n"), probe); return probe; }))
                return steps;

            // Kart aynı dosyayı görüyor mu? (yükleme sonrası kartta copy için şart)
            string cardDir = eff.CardPathPrefix;
            if (Busy)
                steps.Add(new FtpTestStep { Name = "Kartın dosyayı görmesi", Skipped = true, Detail = "başka bir işlem sürüyor" });
            else
            {
                BeginWork("FTP sınaması: kart " + cardDir + " klasörünü listeliyor");
                try
                {
                    string cmd = _listCommand + " " + FsPath.Quote(cardDir);
                    ShellResponse lr = await Exec(cmd, ShortTimeoutSec);
                    if (lr.UndefinedSymbol != null && _listCommand == "ll") { _listCommand = "ls"; cmd = "ls " + FsPath.Quote(cardDir); lr = await Exec(cmd, ShortTimeoutSec); }
                    var st = new FtpTestStep { Name = "Kartın dosyayı görmesi (" + cardDir + ")" };
                    if (lr.Failure != null) st.Detail = lr.Failure;
                    else if (!lr.IsSuccess) st.Detail = "kart bu klasörü listeleyemedi (" + cmd + (lr.ReturnCode.HasValue ? " → " + lr.ReturnCode.Value : "") + "). Kartın gördüğü yol yanlış olabilir.";
                    else if (!lr.OutputLines.Any(l => l.Contains(probe))) st.Detail = "kart klasörü listeledi ama deneme dosyasını göremedi: FTP klasörü ile kartın gördüğü yol aynı yer değil.";
                    else { st.Ok = true; st.Detail = FsPath.Join(cardDir, probe); }
                    steps.Add(st);
                }
                finally { EndWork(); }
            }

            await run("Deneme dosyasını FTP'den silme", async () => { await up.DeleteAsync(probe, false); return probe; });
            return steps;
        }

        /// <summary>Kartın FTP'de bıraktığı kopyaları siler (kullanıcı "FTP kopyalarını sil" deyince).</summary>
        public async Task<OpResult> DeleteFtpCopiesAsync(IFileUploader up, FtpCopyTracker tracker)
        {
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            var items = tracker.Items;
            if (items.Count == 0) return Reject("FTP'de silinecek kopya yok.");
            BeginWork("FTP kopyaları siliniyor");
            int ok = 0;
            var failed = new List<string>();
            try
            {
                foreach (var it in items)
                {
                    PendingCommand = "FTP'den siliniyor: " + it.Name;
                    Raise();
                    try { await up.DeleteAsync(it.Name, it.IsDirectory); tracker.Remove(it.Name); ok++; }
                    catch (Exception ex)
                    {
                        var fo = ex as FtpOperationException;
                        var we = (fo != null ? fo.InnerException : ex) as System.Net.WebException;
                        var r = we != null ? we.Response as System.Net.FtpWebResponse : null;
                        if (r != null && r.StatusCode == System.Net.FtpStatusCode.ActionNotTakenFileUnavailable)
                        {
                            tracker.Remove(it.Name); ok++;   // zaten yok (ör. elle silinmiş): listeden çıkar
                            continue;
                        }
                        ErrorText.LogTechnical("FTP kopyası silme " + it.Name, ex);
                        failed.Add(it.Name + " (" + FtpErrorText(ex) + ")");
                    }
                }
                if (failed.Count == 0) { Notice = ok + " FTP kopyası silindi."; return OpResult.Success(Notice); }
                var res = OpResult.Fail((ok > 0 ? ok + " kopya silindi; " : "") + failed.Count + " kopya silinemedi: " + string.Join(" · ", failed));
                SetError(res);
                return res;
            }
            finally { EndWork(); }
        }

        // ---------- Ortak çalıştırma ----------

        private async Task<OpResult> RunAsync(string cmd, int timeoutSec, string failPrefix, string successMessage, string failHint = null)
        {
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            BeginWork(cmd);
            try
            {
                ShellResponse resp = await Exec(cmd, timeoutSec);
                OpResult bad = CheckReturn(resp, failPrefix, failHint);
                if (bad != null) { SetError(bad); return bad; }
                await RefreshListingQuietly();
                Notice = successMessage;
                return new OpResult { Ok = true, Message = successMessage, Response = resp };
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("dosya gezgini " + cmd, ex);
                var r = OpResult.Fail(failPrefix + ": " + ErrorText.Describe(ex));
                SetError(r);
                return r;
            }
            finally
            {
                EndWork();
            }
        }

        /// <summary>İşlemden sonra mevcut klasörü yeniden listeler (geçmişe eklemez; başarısızsa eski liste kalır).</summary>
        private async Task RefreshListingQuietly()
        {
            OpResult r = await ListAsync(Current, null);
            if (r.Ok && _lastListing != null) Entries = _lastListing;
        }

        private async Task<ShellResponse> Exec(string cmd, int timeoutSec)
        {
            ShellResponse resp = await _exec(cmd, timeoutSec);
            LastResponse = resp;
            return resp;
        }

        /// <summary>Gönderim hatası / zaman aşımı / eksik komut. Sorun yoksa null.</summary>
        private static OpResult CheckCommon(ShellResponse resp, string what)
        {
            if (resp.Failure != null) return new OpResult { Ok = false, Message = resp.Failure, Response = resp };
            if (resp.UndefinedSymbol != null)
                return new OpResult
                {
                    Ok = false,
                    MissingCommand = resp.UndefinedSymbol,
                    Response = resp,
                    Message = "Bu işlem için gereken \"" + resp.UndefinedSymbol + "\" komutu kartın imajında (sembol tablosunda) yok. " +
                              "Kartta hiçbir değişiklik yapılmadı.",
                };
            if (resp.TimedOut && !resp.ReturnCode.HasValue)
                return new OpResult { Ok = false, Response = resp, Message = "Kart zamanında cevap vermedi (\"" + what + "\")." };
            return null;
        }

        /// <summary>CheckCommon + dönüş değeri 0 kontrolü. Sorun yoksa null.</summary>
        private static OpResult CheckReturn(ShellResponse resp, string failPrefix, string hint)
        {
            OpResult common = CheckCommon(resp, resp.Command);
            if (common != null)
            {
                if (common.MissingCommand == null) common.Message = failPrefix + ": " + common.Message;
                return common;
            }
            if (!resp.ReturnCode.HasValue)
                return new OpResult { Ok = false, Response = resp, Message = failPrefix + ": kartın cevabında dönüş değeri bulunamadı." };
            if (resp.ReturnCode.Value != 0)
                return new OpResult
                {
                    Ok = false,
                    Response = resp,
                    Message = failPrefix + " (dönüş değeri " + resp.ReturnCode.Value + ")." + (hint != null ? " " + hint : ""),
                };
            return null;
        }

        private static string ParentOf(FsEntry e)
        {
            string p = e.FullPath;
            int i = p.LastIndexOf('/');
            if (i > 0) return p.Substring(0, i);
            int c = p.LastIndexOf(':');
            return c >= 0 ? p.Substring(0, c + 1) : "/";
        }

        private OpResult Reject(string message)
        {
            Error = message;
            ErrorIsWarning = true;
            Notice = null;
            Raise();
            return OpResult.Fail(message);
        }

        private void SetError(OpResult r)
        {
            Error = r.Message;
            ErrorIsWarning = r.MissingCommand != null || r.IsWarning;
        }

        private void BeginWork(string pending)
        {
            Busy = true;
            Error = null;
            ErrorIsWarning = false;
            Notice = null;
            PendingCommand = pending;
            Raise();
        }

        private void EndWork()
        {
            Busy = false;
            PendingCommand = null;
            Raise();
        }

        private void Raise()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
