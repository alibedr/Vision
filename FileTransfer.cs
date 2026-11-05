// FtpWebRequest .NET Framework 4.6.1'de desteklenen yerleşik FTP istemcisidir (.NET 5+ yalnızca "eskimiş" diye işaretler).
#pragma warning disable SYSLIB0014
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Vision
{
    /// <summary>
    /// Bilgisayardan karta yükleme ayarları. Akış: dosya önce kartın erişebildiği FTP sunucusuna gönderilir,
    /// sonra kartta "copy" (dosya) / "xcopy" (klasör) ile FTP konumundan hedef klasöre kopyalanır.
    /// </summary>
    public sealed class TransferSettings
    {
        public TransferSettings()
        {
            FtpPort = 21;
            FtpUser = "anonymous";
            FtpRemoteDir = "/";
            CardPathPrefix = "host:";
            UsePassive = true;
        }

        /// <summary>FTP sunucusunun adresi (IP ya da ad).</summary>
        public string FtpHost { get; set; }
        public int FtpPort { get; set; }
        public string FtpUser { get; set; }
        /// <summary>Parola, Windows kullanıcı hesabına bağlı olarak şifrelenmiş (DPAPI). Düz metin saklanmaz.</summary>
        public string FtpPasswordProtected { get; set; }
        /// <summary>Dosyaların gönderileceği FTP klasörü (ör. "/upload").</summary>
        public string FtpRemoteDir { get; set; }
        /// <summary>Kartın AYNI klasörü gördüğü yol (ör. "host:" ya da "host:/upload"); copy/xcopy kaynağı bununla başlar.</summary>
        public string CardPathPrefix { get; set; }
        public bool UsePassive { get; set; }
        /// <summary>Kartta kopyalama başarılı olunca FTP'deki kopyayı sil.</summary>
        public bool DeleteAfterCopy { get; set; }

        [XmlIgnore]
        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(FtpHost) && !string.IsNullOrWhiteSpace(CardPathPrefix); }
        }

        [XmlIgnore]
        public string Password
        {
            get { return PasswordProtector.Unprotect(FtpPasswordProtected); }
            set { FtpPasswordProtected = PasswordProtector.Protect(value); }
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
        Task UploadFileAsync(string localPath, string remoteName);
        Task UploadDirectoryAsync(string localDir, string remoteName);
        Task DeleteAsync(string remoteName, bool isDirectory);
        /// <summary>FTP klasöründeki dosyayı bilgisayara indirir.</summary>
        Task DownloadFileAsync(string remoteName, string localPath);
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

        private Uri UriFor(string relative)
        {
            string dir = (_s.FtpRemoteDir ?? "/").Replace('\\', '/');
            if (!dir.StartsWith("/")) dir = "/" + dir;
            if (!dir.EndsWith("/")) dir += "/";
            string path = dir + (relative ?? "").TrimStart('/');
            var b = new UriBuilder("ftp", _s.FtpHost.Trim(), _s.FtpPort, path);
            return b.Uri;
        }

        private FtpWebRequest Create(string relative, string method)
        {
            var req = (FtpWebRequest)WebRequest.Create(UriFor(relative));
            req.Method = method;
            req.Credentials = new NetworkCredential(string.IsNullOrEmpty(_s.FtpUser) ? "anonymous" : _s.FtpUser, _s.Password);
            req.UsePassive = _s.UsePassive;
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

        public async Task UploadFileAsync(string localPath, string remoteName)
        {
            FtpWebRequest req = Create(remoteName, WebRequestMethods.Ftp.UploadFile);
            using (FileStream src = File.OpenRead(localPath))
            {
                req.ContentLength = src.Length;
                using (Stream dst = await req.GetRequestStreamAsync().ConfigureAwait(false))
                    await src.CopyToAsync(dst).ConfigureAwait(false);
            }
            await Complete(req).ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string remoteName, string localPath)
        {
            FtpWebRequest req = Create(remoteName, WebRequestMethods.Ftp.DownloadFile);
            using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = File.Create(localPath))
                await src.CopyToAsync(dst).ConfigureAwait(false);
        }

        public async Task UploadDirectoryAsync(string localDir, string remoteName)
        {
            await MakeDirectoryAsync(remoteName).ConfigureAwait(false);
            foreach (string dir in Directory.GetDirectories(localDir))
                await UploadDirectoryAsync(dir, remoteName + "/" + Path.GetFileName(dir)).ConfigureAwait(false);
            foreach (string file in Directory.GetFiles(localDir))
                await UploadFileAsync(file, remoteName + "/" + Path.GetFileName(file)).ConfigureAwait(false);
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

        public async Task TestAsync()
        {
            FtpWebRequest req = Create("", WebRequestMethods.Ftp.ListDirectory);
            using (var resp = (FtpWebResponse)await req.GetResponseAsync().ConfigureAwait(false))
            using (var rd = new StreamReader(resp.GetResponseStream()))
                await rd.ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
