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

        public string SizeText
        {
            get
            {
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
                    case FsKind.Device: return "Cihaz";
                    case FsKind.Folder: return "Klasör";
                    case FsKind.File: return "Dosya";
                    default: return "Klasör / dosya";
                }
            }
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
            HiddenDevices = new List<string>();
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
        /// "devs"in listelediği ama içine girilemeyen (listeleme komutu 0 dışında değer döndüren) cihazlar.
        /// Bunlar Cihazlar listesinde gösterilmez (ör. /null, /tyCo/0).
        /// </summary>
        public List<string> HiddenDevices { get; private set; }

        /// <summary>Cihaz denetiminin önbelleği (cihaz → içine girilebiliyor mu). Yenile / ilk yükleme temizler.</summary>
        private readonly Dictionary<string, bool> _deviceProbe = new Dictionary<string, bool>();

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
            if (!Busy) _deviceProbe.Clear();   // kart yeniden seçildi / bağlandı: cihazlar yeniden denetlenir
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
            if (Current.IsRoot && !Busy) _deviceProbe.Clear();   // Cihazlar'da Yenile: cihazlar yeniden denetlenir
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
                List<string> devices = DevsParser.Parse(resp.OutputLines);
                if (devices.Count == 0)
                {
                    _lastListing = new List<FsEntry>();
                    HiddenDevices = new List<string>();
                    return new OpResult { Ok = true, Message = "Kart \"devs\" komutuna cihaz döndürmedi." };
                }
                // Yalnız içine girilebilen cihazlar: her cihaz listeleme komutuyla denenir, dönüş değeri 0 olanlar gösterilir
                ProbeResult probe = await ProbeDevicesAsync(devices);
                if (probe.Error != null) return probe.Error;
                LastResponse = resp;   // ham cevap panelinde devs görünsün
                if (probe.CannotProbe)
                {
                    // Kartta listeleme komutu (ll / ls) hiç yok: süzülemez, tüm cihazlar gösterilir
                    HiddenDevices = new List<string>();
                    _lastListing = devices.Select(d => new FsEntry { Name = d, FullPath = d, Kind = FsKind.Device }).ToList();
                    return new OpResult { Ok = true, Message = "Kartın imajında listeleme komutu (ll / ls) yok; cihazların içine girilip girilemeyeceği denetlenemedi, tümü gösteriliyor." };
                }
                HiddenDevices = devices.Where(d => !probe.Usable.Contains(d)).ToList();
                _lastListing = probe.Usable.Select(d => new FsEntry { Name = d, FullPath = d, Kind = FsKind.Device }).ToList();
                if (_lastListing.Count == 0)
                    return new OpResult { Ok = true, Message = "Kartta içine girilebilen cihaz bulunamadı (" + devices.Count + " cihaz denendi: " + string.Join(", ", devices) + ")." };
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
            OpResult check = CheckReturn(lr, "\"" + target.Path + "\" açılamadı", "Erişim yok, yol geçersiz ya da bu bir klasör değil.");
            if (check != null)
            {
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
            _lastListing = entries;
            return new OpResult { Ok = true };
        }

        private sealed class ProbeResult
        {
            public readonly List<string> Usable = new List<string>();
            public bool CannotProbe;
            /// <summary>Denetim sırasında bağlantı sorunu: listeleme hatası olarak gösterilir.</summary>
            public OpResult Error;
        }

        /// <summary>
        /// Cihazları sırayla listeleme komutuyla (ll, yoksa ls) dener; dönüş değeri 0 olanlar içine girilebilir sayılır.
        /// Sonuç önbelleğe alınır: Cihazlar'a her dönüşte kart yeniden denenmez (Yenile ve kart değişince yenilenir).
        /// </summary>
        private async Task<ProbeResult> ProbeDevicesAsync(List<string> devices)
        {
            var result = new ProbeResult();
            for (int i = 0; i < devices.Count; i++)
            {
                string dev = devices[i];
                bool ok;
                if (_deviceProbe.TryGetValue(dev, out ok)) { if (ok) result.Usable.Add(dev); continue; }

                string cmd = _listCommand + " " + FsPath.Quote(dev);
                PendingCommand = "Cihazlar denetleniyor (" + (i + 1) + "/" + devices.Count + ")  ·  " + cmd;
                Raise();
                ShellResponse pr = await Exec(cmd, ShortTimeoutSec);
                if (pr.UndefinedSymbol != null && _listCommand == "ll")
                {
                    _listCommand = "ls";   // kartın imajında ll yok: ls ile dene ve bunu hatırla
                    cmd = "ls " + FsPath.Quote(dev);
                    PendingCommand = "Cihazlar denetleniyor (" + (i + 1) + "/" + devices.Count + ")  ·  " + cmd;
                    Raise();
                    pr = await Exec(cmd, ShortTimeoutSec);
                }
                if (pr.UndefinedSymbol != null) { result.CannotProbe = true; return result; }
                if (pr.Failure != null) { result.Error = CheckCommon(pr, cmd) ?? OpResult.Fail(pr.Failure); return result; }
                ok = pr.IsSuccess;   // zaman aşımı ya da 0 dışı dönüş değeri: içine girilemiyor
                _deviceProbe[dev] = ok;
                if (ok) result.Usable.Add(dev);
            }
            return result;
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

        /// <param name="recursive">Klasör içindekilerle birlikte silinsin (xdelete). false: yalnız boş klasör (rmdir).</param>
        public Task<OpResult> DeleteAsync(FsEntry entry, bool recursive)
        {
            if (entry.Kind == FsKind.Device) return Task.FromResult(Reject("Cihazlar silinemez."));
            string cmd;
            int timeout = ShortTimeoutSec;
            if (entry.Kind == FsKind.File) cmd = "rm " + FsPath.Quote(entry.FullPath);
            else if (recursive) { cmd = "xdelete " + FsPath.Quote(entry.FullPath); timeout = LongTimeoutSec; }
            else cmd = "rmdir " + FsPath.Quote(entry.FullPath);

            string failHint = entry.Kind == FsKind.File ? null
                : recursive ? null : "Klasör boş değilse \"İçindekilerle birlikte sil\"i kullanın.";
            return RunAsync(cmd, timeout, "\"" + entry.Name + "\" silinemedi", "\"" + entry.Name + "\" silindi.", failHint);
        }

        public Task<OpResult> RenameAsync(FsEntry entry, string newName)
        {
            string reason;
            if (entry.Kind == FsKind.Device) return Task.FromResult(Reject("Cihazlar yeniden adlandırılamaz."));
            if (!FsPath.IsValidName(newName, out reason)) return Task.FromResult(Reject(reason));
            if (newName == entry.Name) return Task.FromResult(Reject("Yeni ad eskisiyle aynı."));
            if (Entries.Any(e => e != entry && string.Equals(e.Name, newName, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(Reject("\"" + newName + "\" adında bir öğe zaten var."));
            string dst = FsPath.Join(ParentOf(entry), newName);
            return RunAsync("mv " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst), ShortTimeoutSec,
                "\"" + entry.Name + "\" yeniden adlandırılamadı", "\"" + entry.Name + "\" → \"" + newName + "\"");
        }

        /// <summary>Öğeyi kart içinde başka bir klasöre taşır (mv) ya da kopyalar (dosya: copy, klasör: xcopy).</summary>
        public Task<OpResult> TransferAsync(FsEntry entry, string destDir, bool copy)
        {
            if (entry.Kind == FsKind.Device) return Task.FromResult(Reject("Cihazlar taşınamaz / kopyalanamaz."));
            if (string.IsNullOrWhiteSpace(destDir)) return Task.FromResult(Reject("Hedef klasör belirtilmedi."));
            destDir = destDir.TrimEnd('/');
            if (destDir == ParentOf(entry).TrimEnd('/'))
                return Task.FromResult(Reject("Öğe zaten bu klasörde."));
            if (destDir == entry.FullPath || destDir.StartsWith(entry.FullPath + "/"))
                return Task.FromResult(Reject("Bir klasör kendi içine taşınamaz / kopyalanamaz."));

            string dst = FsPath.Join(destDir, entry.Name);
            string verb = copy ? "kopyalandı" : "taşındı";
            string cmd;
            if (!copy) cmd = "mv " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            else if (entry.Kind == FsKind.File) cmd = "copy " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            else cmd = "xcopy " + FsPath.Quote(entry.FullPath) + ", " + FsPath.Quote(dst);
            return RunAsync(cmd, copy ? LongTimeoutSec : ShortTimeoutSec,
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
        /// sonra kartta copy (dosya) / xcopy (klasör) ile FTP konumundan içinde bulunulan klasöre kopyalanır.
        /// </summary>
        /// <param name="destDir">Hedef klasör; null ise içinde bulunulan klasör.</param>
        public async Task<OpResult> UploadAsync(IList<string> localPaths, IFileUploader uploader, TransferSettings settings, IProgress<string> progress, string destDir = null)
        {
            if (Current.IsRoot && destDir == null) return Reject("Yüklemek için önce kartta bir klasör açın.");
            if (Busy) return Reject("Başka bir işlem sürüyor.");
            if (settings == null || !settings.IsConfigured) return Reject("Yükleme için önce \"FTP ayarları\"nı girin.");
            destDir = destDir ?? Current.Path;
            int ok = 0;
            var failures = new List<string>();
            BeginWork("yükleme");
            try
            {
                for (int i = 0; i < localPaths.Count; i++)
                {
                    string local = localPaths[i].TrimEnd('\\', '/');
                    string name = System.IO.Path.GetFileName(local);
                    bool isDir = System.IO.Directory.Exists(local);
                    string step = "(" + (i + 1) + "/" + localPaths.Count + ") " + name;

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

                    // 2) Kartta FTP konumundan hedef klasöre kopyala
                    string src = FsPath.Join(settings.CardPathPrefix, name);
                    string dst = FsPath.Join(destDir, name);
                    string cmd = (isDir ? "xcopy " : "copy ") + FsPath.Quote(src) + ", " + FsPath.Quote(dst);
                    PendingCommand = "Kartta kopyalanıyor " + step + "  ·  " + cmd;
                    Raise();
                    if (progress != null) progress.Report(PendingCommand);
                    ShellResponse resp = await Exec(cmd, LongTimeoutSec);
                    OpResult bad = CheckReturn(resp, name + ": kartta kopyalanamadı", null);
                    if (bad != null) { failures.Add(bad.Message); if (bad.MissingCommand != null) { SetError(bad); break; } continue; }
                    ok++;

                    // 3) İsteğe bağlı: FTP'deki geçici kopyayı sil
                    if (settings.DeleteAfterCopy)
                    {
                        try { await uploader.DeleteAsync(name, isDir); }
                        catch (Exception ex) { ErrorText.LogTechnical("FTP silme " + name, ex); }
                    }
                }

                await RefreshListingQuietly();
                if (failures.Count == 0)
                {
                    Notice = ok + " öğe karta yüklendi → " + destDir;
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

        private static string FtpErrorText(Exception ex)
        {
            var we = ex as System.Net.WebException;
            var resp = we != null ? we.Response as System.Net.FtpWebResponse : null;
            if (resp != null && resp.StatusCode != System.Net.FtpStatusCode.Undefined)
            {
                switch (resp.StatusCode)
                {
                    case System.Net.FtpStatusCode.NotLoggedIn: return "kullanıcı adı ya da parola hatalı";
                    case System.Net.FtpStatusCode.ActionNotTakenFileUnavailable: return "FTP klasörüne yazılamadı (yol ya da izin)";
                    default: return "FTP sunucusu hatası " + (int)resp.StatusCode;
                }
            }
            if (we != null && we.Status == System.Net.WebExceptionStatus.ConnectFailure) return "FTP sunucusuna bağlanılamadı";
            if (we != null && we.Status == System.Net.WebExceptionStatus.NameResolutionFailure) return "FTP sunucusu adresi bulunamadı";
            if (ex is System.IO.IOException) return "yerel dosya okunamadı";
            return ErrorText.Describe(ex);
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
            ErrorIsWarning = r.MissingCommand != null;
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
