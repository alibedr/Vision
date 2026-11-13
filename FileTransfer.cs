// FtpWebRequest .NET Framework 4.6.1'de desteklenen yerleşik FTP istemcisidir (.NET 5+ yalnızca "eskimiş" diye işaretler).
#pragma warning disable SYSLIB0014
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Vision
{
    /// <summary>
    /// Bilgisayardan karta yükleme ayarları. Akış: dosya önce kartın erişebildiği FTP sunucusuna gönderilir,
    /// sonra kartta "copy" (dosya) / "xcopy" (klasör) ile FTP konumundan hedef klasöre kopyalanır.
    /// FTP sunucusu, kullanıcı ve parola KARTTAN alınır (version komutunun boot satırı; bkz. CardFtpConfigurator) ve kart başına
    /// yalnız bellekte tutulur. Burada kalanlar tüm kartlar için ortak, parola içermeyen ayarlardır.
    /// </summary>
    public sealed class TransferSettings
    {
        public TransferSettings()
        {
            FtpPort = 21;
            FtpUser = "anonymous";
            FtpRemoteDir = "/";
            CardPathPrefix = "";
            UsePassive = true;
            DeleteAfterCopy = true;   // varsayılan açık (eski ayar dosyalarına Normalize → ApplyNewDefaults ile bir kez uygulanır)
        }

        /// <summary>FTP sunucusunun adresi. Artık karttan alınır; eski ayar dosyalarıyla uyum için alan korunur (kullanılmaz).</summary>
        public string FtpHost { get; set; }
        public int FtpPort { get; set; }
        public string FtpUser { get; set; }
        /// <summary>Parola, Windows kullanıcı hesabına bağlı olarak şifrelenmiş (DPAPI). Düz metin saklanmaz.</summary>
        public string FtpPasswordProtected { get; set; }
        /// <summary>Dosyaların gönderileceği FTP klasörü (ör. "/upload").</summary>
        public string FtpRemoteDir { get; set; }
        /// <summary>
        /// Kartın AYNI klasörü gördüğü yol. Boş (önerilen): otomatik = kartın FTP aygıtı + FTP klasörü (ör. "host:upload").
        /// Yalnız aygıt adı ("host:") yazılırsa FTP klasörü otomatik eklenir. Tam yol yazılırsa olduğu gibi kullanılır.
        /// </summary>
        [XmlIgnore]   // kaydedilmez: kartın yolu her bağlantıda karttan alınır (eski ayar dosyalarındaki değer de yok sayılır)
        public string CardPathPrefix { get; set; }
        public bool UsePassive { get; set; }
        /// <summary>Kartta kopyalama başarılı olunca FTP'deki kopyayı sil (varsayılan: açık).</summary>
        public bool DeleteAfterCopy { get; set; }

        /// <summary>
        /// "Kopyayı hemen sil" varsayılanının (açık) bu ayar dosyasına uygulanıp uygulanmadığı. Önceki sürümlerin
        /// dosyalarında bu alan yoktur ve DeleteAfterCopy=false yazılıdır; ilk açılışta bir kez açık yapılır,
        /// sonra kullanıcının seçimi korunur.
        /// </summary>
        public bool DeleteAfterCopyDefaultApplied { get; set; }

        /// <summary>Eski ayar dosyalarına yeni varsayılanları bir kez uygular.</summary>
        public void ApplyNewDefaults()
        {
            if (!DeleteAfterCopyDefaultApplied)
            {
                DeleteAfterCopy = true;
                DeleteAfterCopyDefaultApplied = true;
            }
        }

        /// <summary>Kartın FTP köküne eriştiği yol: "/host." + aygıt adı (ör. "/host.host", "/host.ali").</summary>
        public static string CardRootFor(string deviceName)
        {
            string d = (deviceName ?? "").Trim().TrimEnd(':');
            return "/host." + (d.Length == 0 ? "host" : d);
        }

        [XmlIgnore]
        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(FtpHost) && !string.IsNullOrWhiteSpace(CardPathPrefix); }
        }

        /// <summary>Karttan alınan parola (yalnız bellekte; dosyaya yazılmaz).</summary>
        [XmlIgnore]
        public string RuntimePassword { get; set; }

        [XmlIgnore]
        public string Password
        {
            get { return RuntimePassword ?? PasswordProtector.Unprotect(FtpPasswordProtected); }
            set { FtpPasswordProtected = PasswordProtector.Protect(value); }
        }

        /// <summary>"upload", "/upload/", "\upload" → "/upload"; boş → "/".</summary>
        [XmlIgnore]
        public string NormalizedRemoteDir
        {
            get
            {
                string d = (FtpRemoteDir ?? "").Trim().Replace('\\', '/').Trim('/');
                return d.Length == 0 ? "/" : "/" + d;
            }
        }

        /// <summary>
        /// Kartın, FTP klasörünü gördüğü yol. Boşsa (otomatik) "/host." + kartın aygıt adı (version boot satırında
        /// parantezden sonra ':' işaretine kadar olan kelime) + FTP klasörü:
        /// "gei(0,0)host:" → "/host.host" (kök) ya da "/host.host/upload"; "gei(1,0)ali:" → "/host.ali".
        /// Yalnız aygıt adı ("host:") yazılırsa eski biçim ("host:upload") kullanılır; başka bir yol yazılırsa olduğu gibi.
        /// </summary>
        /// <summary>
        /// Kartın FTP klasörünü gördüğü yol, karttan otomatik: "/host." + kartın aygıt adı (version boot satırından) +
        /// FTP klasörü. Ör. "motetsec(0,0)mkozdem:" ve FTP klasörü "upload" → "/host.mkozdem/upload".
        /// </summary>
        public string AutoCardFolderFor(CardFtpCredentials c)
        {
            string rel = NormalizedRemoteDir.TrimStart('/');
            string root = c != null ? c.CardFtpRoot : CardRootFor("host");
            return rel.Length == 0 ? root : root + "/" + rel;
        }

        /// <summary>
        /// Kartın gördüğü yol: kullanıcı bu kart için (bu bağlantıda) elle bir yol yazdıysa o, yazmadıysa karttan otomatik.
        /// Elle yazılan yol ":" ile bitiyorsa (ör. "host:") eski biçim: aygıt + FTP klasörü ("host:upload").
        /// </summary>
        public string CardFolderFor(CardFtpCredentials c)
        {
            return CardFolderFor(c, c != null ? c.PathOverride : null);
        }

        public string CardFolderFor(CardFtpCredentials c, string manualPath)
        {
            string p = (manualPath ?? "").Trim();
            if (p.Length == 0) return AutoCardFolderFor(c);
            if (!p.EndsWith(":")) return p;                   // kullanıcı tam yol yazmış
            string rel = NormalizedRemoteDir.TrimStart('/');
            return rel.Length == 0 ? p : p + rel;
        }

        /// <summary>Bu kartın bilgileriyle (yalnız bellekte) çalışan ayar kopyası.</summary>
        /// <summary>
        /// Vision'ın FTP'deki çalışma klasörü. Karta yüklenecek dosyalar / klasörler, "Birlikte aç" geçici kopyaları ve
        /// bağlantı sınaması dosyası FTP klasörünün kendisine değil, onun içindeki bu klasöre gider: bir aktarım yarıda
        /// kalıp FTP'de kopya bırakırsa sunucudaki diğer dosya ve klasörlerle karışmaz. Klasör yoksa ilk aktarımda oluşturulur.
        /// </summary>
        public const string WorkFolderName = "Vision";

        /// <summary>
        /// Bu kart için geçerli ayarlar: kartın FTP bilgisi + çalışma klasörü. FtpRemoteDir ve CardPathPrefix aynı
        /// klasörü (…/Vision) gösterir; aktarımlarda kullanılan tüm adlar bu klasöre görelidir.
        /// </summary>
        public TransferSettings ForCard(CardFtpCredentials c)
        {
            var t = (TransferSettings)MemberwiseClone();
            t.FtpHost = c.Host;
            t.FtpUser = c.User;
            t.FtpPasswordProtected = null;
            t.RuntimePassword = c.Password ?? "";
            t.CardPathPrefix = FsPath.Join(CardFolderFor(c), WorkFolderName);
            t.FtpRemoteDir = NormalizedRemoteDir.TrimEnd('/') + "/" + WorkFolderName;
            return t;
        }

        /// <summary>Parola gibi artık kullanılmayan eski alanları temizler (ayar dosyasında parola kalmasın).</summary>
        public void ClearLegacyCredentials()
        {
            FtpHost = null;
            FtpUser = null;
            FtpPasswordProtected = null;
        }
    }

    /// <summary>Parolayı Windows DPAPI ile (yalnız bu kullanıcı çözebilir) şifreler.</summary>
    public static class PasswordProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TelnetCards.FTP");   // eski ad: kayıtlı FTP parolaları çözülebilsin diye değiştirilmedi

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            byte[] data = Encoding.UTF8.GetBytes(plain);
            try
            {
                byte[] enc = System.Security.Cryptography.ProtectedData.Protect(data, Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return "dpapi:" + Convert.ToBase64String(enc);
            }
            catch (PlatformNotSupportedException)
            {
                return "b64:" + Convert.ToBase64String(data);   // yalnız Windows dışı test ortamı
            }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try
            {
                if (stored.StartsWith("dpapi:"))
                {
                    byte[] dec = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(6)), Entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(dec);
                }
                if (stored.StartsWith("b64:")) return Encoding.UTF8.GetString(Convert.FromBase64String(stored.Substring(4)));
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("FTP parolası çözülemedi", ex);
            }
            return "";
        }
    }

    /// <summary>Kartın erişebildiği aktarım konumuna dosya gönderen bileşen (test için değiştirilebilir).</summary>
    public interface IFileUploader
    {
        /// <summary>FTP'deki çalışma klasörünü (…/Vision) yoksa oluşturur; varsa bir şey yapmaz.</summary>
        Task EnsureWorkFolderAsync();
        /// <param name="sent">İsteğe bağlı: bu aktarımda o ana kadar gönderilen bayt (ilerleme penceresi için).</param>
        /// <param name="ct">İptal: aktarım hemen kesilir (OperationCanceledException).</param>
        Task UploadFileAsync(string localPath, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken));
        /// <summary>Dosyanın bir bölümünü (offset'ten length bayt) ayrı bir dosya olarak gönderir (büyük dosya parçaları).</summary>
        Task UploadFileRangeAsync(string localPath, long offset, long length, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken));
        /// <param name="sent">İsteğe bağlı: klasördeki tüm dosyalar için toplam gönderilen bayt.</param>
        /// <param name="include">İsteğe bağlı: false dönen yerel dosyalar gönderilmez (büyük dosyalar ayrıca parça parça gider).</param>
        Task UploadDirectoryAsync(string localDir, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken),
                                  Func<string, bool> include = null);
        Task DeleteAsync(string remoteName, bool isDirectory);
        /// <summary>FTP çalışma klasöründeki adlar (NLST).</summary>
        Task<IList<string>> ListNamesAsync();
        /// <summary>FTP klasöründeki dosyayı bilgisayara indirir (append: yerel dosyanın sonuna ekler — parçalı indirme).</summary>
        Task DownloadFileAsync(string remoteName, string localPath, Action<long> got = null, CancellationToken ct = default(CancellationToken), bool append = false);
        /// <summary>Küçük bir veriyi dosya olarak gönderir (bağlantı sınamasında yazma denemesi).</summary>
        Task UploadBytesAsync(byte[] data, string remoteName);
        /// <summary>Ayarları sınar (bağlan, klasörü listele). Hata varsa istisna fırlatır.</summary>
        Task TestAsync();
    }

    /// <summary>.NET Framework'ün yerleşik FtpWebRequest'i ile FTP aktarımı (ek paket gerekmez).</summary>
    public sealed class FtpUploader : IFileUploader
    {
        private readonly TransferSettings _s;

        public FtpUploader(TransferSettings settings)
        {
            _s = settings;
        }

        /// <summary>Son aktarımda pasif / etkin kip değiştirilerek başarı sağlandıysa açıklaması (yoksa null).</summary>
        public string ModeNote { get; private set; }

        /// <summary>
        /// FTP adresi. Her yol parçası ayrı kodlanır: boşluk, #, %, Türkçe karakter gibi karakterler adresi bozmaz
        /// (önceden "#" ya da "%" içeren adlar adresi kesiyor / yanlış dosyaya gidiyordu). Yol, oturum açılan klasöre göredir.
        /// </summary>
        private Uri UriFor(string relative)
        {
            var segs = new List<string>();
            foreach (string part in (_s.NormalizedRemoteDir + "/" + (relative ?? "")).Replace('\\', '/').Split('/'))
                if (part.Length > 0) segs.Add(Uri.EscapeDataString(part));
            string host = (_s.FtpHost ?? "").Trim();
            if (host.Contains(":") && !host.StartsWith("[")) host = "[" + host + "]";   // IPv6
            string url = "ftp://" + host + ":" + _s.FtpPort + "/" + string.Join("/", segs);
            if (((relative ?? "").EndsWith("/") || string.IsNullOrEmpty(relative)) && !url.EndsWith("/")) url += "/";
            return new Uri(url);
        }

        /// <summary>Parolasız, okunur adres (hata mesajları için).</summary>
        public string DisplayUrl(string relative)
        {
            try { return Uri.UnescapeDataString(UriFor(relative).AbsoluteUri); } catch { return relative; }
        }

        private FtpWebRequest Create(string relative, string method, bool? passive = null)
        {
            var req = (FtpWebRequest)WebRequest.Create(UriFor(relative));
            req.Method = method;
            req.Credentials = new NetworkCredential(string.IsNullOrEmpty(_s.FtpUser) ? "anonymous" : _s.FtpUser, _s.Password);
            req.UsePassive = passive ?? _s.UsePassive;
            req.UseBinary = true;
            req.KeepAlive = false;
            req.Timeout = 15000;
            req.ReadWriteTimeout = 60000;
            return req;
        }

        private static async Task Complete(FtpWebRequest req)
        {
            using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false)) { }
        }

        /// <summary>
        /// Oturumun klasörünü (ayarlardaki FTP klasörü + /Vision) oluşturur. Sunucu "zaten var" (550 vb.) derse sorun
        /// sayılmaz; gerçekten oluşturulamadıysa ardından gelen gönderme / kopyalama açık hatayla başarısız olur.
        /// </summary>
        public async Task EnsureWorkFolderAsync()
        {
            var segs = new List<string>();
            foreach (string part in _s.NormalizedRemoteDir.Split('/'))
                if (part.Length > 0) segs.Add(Uri.EscapeDataString(part));
            if (segs.Count == 0) return;
            string host = (_s.FtpHost ?? "").Trim();
            if (host.Contains(":") && !host.StartsWith("[")) host = "[" + host + "]";
            var req = (FtpWebRequest)WebRequest.Create(new Uri("ftp://" + host + ":" + _s.FtpPort + "/" + string.Join("/", segs)));
            req.Method = WebRequestMethods.Ftp.MakeDirectory;
            req.Credentials = new NetworkCredential(string.IsNullOrEmpty(_s.FtpUser) ? "anonymous" : _s.FtpUser, _s.Password);
            req.UsePassive = _s.UsePassive;
            req.KeepAlive = false;
            req.Timeout = 15000;
            try
            {
                await Complete(req).ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse) return;   // sunucu cevap verdi: klasör büyük olasılıkla zaten var
                throw new FtpOperationException("çalışma klasörü (" + TransferSettings.WorkFolderName + ") oluşturma", DisplayUrl(""), null, ex);
            }
        }

        public async Task UploadFileAsync(string localPath, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken))
        {
            if (!File.Exists(localPath)) throw new FtpOperationException("gönderme", DisplayUrl(remoteName), "Yerel dosya bulunamadı: " + localPath, null);
            await UploadFileRangeAsync(localPath, 0, new FileInfo(localPath).Length, remoteName, sent, ct).ConfigureAwait(false);
        }

        public async Task UploadFileRangeAsync(string localPath, long offset, long length, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken))
        {
            if (!File.Exists(localPath)) throw new FtpOperationException("gönderme", DisplayUrl(remoteName), "Yerel dosya bulunamadı: " + localPath, null);
            await WithModeFallback("gönderme", remoteName, ct, async passive =>
            {
                ct.ThrowIfCancellationRequested();
                FtpWebRequest req = Create(remoteName, WebRequestMethods.Ftp.UploadFile, passive);
                using (ct.Register(req.Abort))
                {
                if (sent != null) sent(0);
                // Dosya başka programda açık olsa da okunabilsin (FileShare.ReadWrite)
                using (var src = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    src.Seek(offset, SeekOrigin.Begin);
                    req.ContentLength = length;
                    var buffer = new byte[1024 * 1024];
                    using (Stream dst = await req.GetRequestStreamAsync().ConfigureAwait(false))
                    {
                        long left = length;
                        while (left > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            int n = await src.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, left)).ConfigureAwait(false);
                            if (n <= 0) throw new IOException("Dosya beklenenden kısa: " + localPath);
                            await dst.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                            left -= n;
                            if (sent != null) sent(length - left);
                        }
                    }
                }
                await Complete(req).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        public async Task UploadBytesAsync(byte[] data, string remoteName)
        {
            await WithModeFallback("yazma denemesi", remoteName, async passive =>
            {
                FtpWebRequest req = Create(remoteName, WebRequestMethods.Ftp.UploadFile, passive);
                req.ContentLength = data.Length;
                using (Stream dst = await req.GetRequestStreamAsync().ConfigureAwait(false))
                    await dst.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                await Complete(req).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string remoteName, string localPath, Action<long> got = null, CancellationToken ct = default(CancellationToken), bool append = false)
        {
            long startLength = append && File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
            await WithModeFallback("indirme", remoteName, ct, async passive =>
            {
                ct.ThrowIfCancellationRequested();
                FtpWebRequest req = Create(remoteName, WebRequestMethods.Ftp.DownloadFile, passive);
                using (ct.Register(req.Abort))
                using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                using (Stream src = resp.GetResponseStream())
                using (FileStream dst = new FileStream(localPath, append ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    // Kip değiştirilip yeniden denenirse yarım kalan kısım atılır
                    dst.SetLength(startLength);
                    dst.Seek(startLength, SeekOrigin.Begin);
                    if (got != null) got(0);
                    var buffer = new byte[1024 * 1024];
                    long total = 0;
                    int n;
                    while ((n = await src.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        await dst.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                        total += n;
                        if (got != null) got(total);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Veri bağlantısı (pasif / etkin kip) kurulamadıysa işlemi bir kez diğer kiple dener. Oturum açma ya da
        /// izin hatalarında yeniden denenmez. Hata, aşama + adres + sunucu cevabıyla FtpOperationException olur.
        /// </summary>
        private Task WithModeFallback(string stage, string remoteName, Func<bool, Task> op)
        {
            return WithModeFallback(stage, remoteName, CancellationToken.None, op);
        }

        private async Task WithModeFallback(string stage, string remoteName, CancellationToken ct, Func<bool, Task> op)
        {
            ModeNote = null;
            try
            {
                await op(_s.UsePassive).ConfigureAwait(false);
                return;
            }
            // İptal: istek kesildi (Abort); diğer kiple yeniden denenmez
            catch (WebException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
            catch (IOException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
            catch (WebException ex) when (IsDataConnectionProblem(ex))
            {
                try
                {
                    await op(!_s.UsePassive).ConfigureAwait(false);
                    ModeNote = (_s.UsePassive ? "Pasif" : "Etkin") + " kipte veri bağlantısı kurulamadı; " +
                               (_s.UsePassive ? "etkin" : "pasif") + " kiple gönderildi. FTP ayarlarında kipi değiştirmeniz önerilir.";
                    return;
                }
                catch (WebException ex2)
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
                    throw new FtpOperationException(stage, DisplayUrl(remoteName), null, ex2);
                }
            }
            catch (WebException ex) { throw new FtpOperationException(stage, DisplayUrl(remoteName), null, ex); }
            catch (IOException ex) { throw new FtpOperationException(stage, DisplayUrl(remoteName), "Yerel dosya okunamadı/yazılamadı: " + ex.Message, ex); }
        }

        private static bool IsDataConnectionProblem(WebException ex)
        {
            var r = ex.Response as FtpWebResponse;
            if (r != null)
            {
                int code = (int)r.StatusCode;
                return code == 425 || code == 426;   // veri bağlantısı açılamadı / kapandı
            }
            return ex.Status == WebExceptionStatus.ConnectFailure && ex.Message.IndexOf("227", StringComparison.Ordinal) >= 0
                || ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ReceiveFailure;
        }

        public async Task UploadDirectoryAsync(string localDir, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken),
                                               Func<string, bool> include = null)
        {
            await UploadDirectoryAsync(localDir, remoteName, sent, new long[1], ct, include).ConfigureAwait(false);
        }

        private async Task UploadDirectoryAsync(string localDir, string remoteName, Action<long> sent, long[] doneBefore, CancellationToken ct,
                                                Func<string, bool> include)
        {
            ct.ThrowIfCancellationRequested();
            await MakeDirectoryAsync(remoteName).ConfigureAwait(false);
            foreach (string dir in Directory.GetDirectories(localDir))
                await UploadDirectoryAsync(dir, remoteName + "/" + Path.GetFileName(dir), sent, doneBefore, ct, include).ConfigureAwait(false);
            foreach (string file in Directory.GetFiles(localDir))
            {
                if (include != null && !include(file)) continue;
                long before = doneBefore[0];
                Action<long> fileSent = null;
                if (sent != null) fileSent = n => sent(before + n);
                await UploadFileAsync(file, remoteName + "/" + Path.GetFileName(file), fileSent, ct).ConfigureAwait(false);
                try { doneBefore[0] = before + new FileInfo(file).Length; } catch { }
            }
        }

        private async Task MakeDirectoryAsync(string remoteName)
        {
            try
            {
                await Complete(Create(remoteName, WebRequestMethods.Ftp.MakeDirectory)).ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                // Klasör zaten varsa sunucu 550 döndürür: sorun değil
                var r = ex.Response as FtpWebResponse;
                if (r == null || r.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable) throw;
            }
        }

        public async Task DeleteAsync(string remoteName, bool isDirectory)
        {
            if (!isDirectory)
            {
                await Complete(Create(remoteName, WebRequestMethods.Ftp.DeleteFile)).ConfigureAwait(false);
                return;
            }
            // Klasör: içini (listeleyerek) silip sonra klasörü kaldır
            FtpWebRequest list = Create(remoteName + "/", WebRequestMethods.Ftp.ListDirectoryDetails);
            var lines = new System.Collections.Generic.List<string>();
            using (var resp = (FtpWebResponse)await list.GetResponseAsync().ConfigureAwait(false))
            using (var rd = new StreamReader(resp.GetResponseStream()))
            {
                string l;
                while ((l = rd.ReadLine()) != null) if (l.Trim().Length > 0) lines.Add(l);
            }
            foreach (string l in lines)
            {
                string[] parts = l.Split(new[] { ' ' }, 9, StringSplitOptions.RemoveEmptyEntries);
                string name = parts.Length >= 9 ? parts[8] : parts[parts.Length - 1];
                if (name == "." || name == "..") continue;
                await DeleteAsync(remoteName + "/" + name, l.StartsWith("d")).ConfigureAwait(false);
            }
            await Complete(Create(remoteName, WebRequestMethods.Ftp.RemoveDirectory)).ConfigureAwait(false);
        }

        public async Task<IList<string>> ListNamesAsync()
        {
            var names = new List<string>();
            await WithModeFallback("listeleme", "", async passive =>
            {
                names.Clear();
                FtpWebRequest req = Create("", WebRequestMethods.Ftp.ListDirectory, passive);
                using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                using (var rd = new StreamReader(resp.GetResponseStream()))
                {
                    string line;
                    while ((line = await rd.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        string n = line.Trim();
                        int slash = n.LastIndexOf('/');
                        if (slash >= 0) n = n.Substring(slash + 1);
                        if (n.Length > 0) names.Add(n);
                    }
                }
            }).ConfigureAwait(false);
            return names;
        }

        public async Task TestAsync()
        {
            await WithModeFallback("oturum açma / listeleme", "", async passive =>
            {
                FtpWebRequest req = Create("", WebRequestMethods.Ftp.ListDirectory, passive);
                using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
                using (var rd = new StreamReader(resp.GetResponseStream()))
                    await rd.ReadToEndAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }

    /// <summary>FTP işleminin hangi aşamada, hangi adreste ve sunucunun hangi cevabıyla başarısız olduğu.</summary>
    public sealed class FtpOperationException : Exception
    {
        public FtpOperationException(string stage, string url, string detail, Exception inner)
            : base(detail ?? FtpDiagnostics.Describe(inner), inner)
        {
            Stage = stage;
            Url = url;
            var we = inner as WebException;
            var r = we != null ? we.Response as FtpWebResponse : null;
            if (r != null && r.StatusCode != FtpStatusCode.Undefined)
            {
                StatusCode = (int)r.StatusCode;
                ServerMessage = (r.StatusDescription ?? "").Trim();
            }
        }

        public string Stage { get; private set; }
        public string Url { get; private set; }
        public int? StatusCode { get; private set; }
        public string ServerMessage { get; private set; }

        /// <summary>Kullanıcıya: "FTP gönderme başarısız (ftp://…/a.bin): sunucu yazmaya izin vermedi — 550 Permission denied"</summary>
        public string UserText
        {
            get
            {
                string s = Message;
                if (!string.IsNullOrEmpty(ServerMessage) && s.IndexOf(ServerMessage, StringComparison.Ordinal) < 0) s += " — sunucu: " + ServerMessage;
                return "FTP " + Stage + " başarısız (" + Url + "): " + s;
            }
        }
    }

    /// <summary>FTP hatalarının Türkçe açıklaması (aşama ve olası çözümle).</summary>
    public static class FtpDiagnostics
    {
        public static string Describe(Exception ex)
        {
            var fo = ex as FtpOperationException;
            if (fo != null) return fo.UserText;
            var we = ex as WebException;
            var resp = we != null ? we.Response as FtpWebResponse : null;
            if (resp != null && resp.StatusCode != FtpStatusCode.Undefined)
            {
                int code = (int)resp.StatusCode;
                string server = (resp.StatusDescription ?? "").Trim();
                switch (code)
                {
                    case 530: return "kullanıcı adı ya da parola kabul edilmedi (" + server + ")";
                    case 550: return "dosya / klasör bulunamadı ya da bu kullanıcının o klasöre yazma izni yok (" + server + "). FTP sunucusunda kullanıcının klasör izinlerine (yazma / Write) bakın.";
                    case 553: return "dosya adı ya da yolu sunucuca kabul edilmedi (" + server + ")";
                    case 552: return "sunucuda yer kalmadı ya da kota doldu (" + server + ")";
                    case 425: case 426: return "veri bağlantısı kurulamadı (" + server + "). Pasif / etkin kip ayarını ve güvenlik duvarını denetleyin.";
                    case 421: return "sunucu bağlantıyı kapattı (" + server + ")";
                    default: return "FTP sunucusu hatası " + code + " (" + server + ")";
                }
            }
            if (we != null)
            {
                switch (we.Status)
                {
                    case WebExceptionStatus.ConnectFailure: return "FTP sunucusuna bağlanılamadı (sunucu kapalı, adres yanlış ya da güvenlik duvarı engelliyor)";
                    case WebExceptionStatus.NameResolutionFailure: return "FTP sunucusu adresi bulunamadı";
                    case WebExceptionStatus.Timeout: return "FTP sunucusu zamanında cevap vermedi";
                    case WebExceptionStatus.ReceiveFailure:
                    case WebExceptionStatus.ConnectionClosed: return "FTP bağlantısı aktarım sırasında koptu";
                }
            }
            if (ex is IOException) return "yerel dosya okunamadı (" + ex.Message + ")";
            return ErrorText.Describe(ex);
        }
    }
}
