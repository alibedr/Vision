using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision
{
    /// <summary>Bir kartın anlık terminal görüntüsü kaydının sonucu.</summary>
    public sealed class SnapshotResult
    {
        public CardConfig Card;
        public bool Ok;
        public string Path;
        public string Error;
    }

    /// <summary>
    /// Kartın terminalinde O AN görünen içeriği dosyaya yazar ("Dosyaya kaydet" düğmesi). Sürekli kayıt başlatmaz,
    /// terminalin durumunu değiştirmez; her tıklama ayrı dosya oluşturur (aynı adlı dosyanın üzerine yazmaz).
    /// Dosya adı mevcut log adlandırmasını izler: logs\Grup_IP[_Ad]_anlik_yyyyMMdd_HHmmss_fff.log
    /// </summary>
    public static class TerminalSnapshot
    {
        public static string DefaultFolder
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"); }
        }

        public static string FileNameFor(CardConfig card, DateTime local)
        {
            string group = card != null && card.Group != null ? card.Group.Name : "grup";
            string id = group + "_" + (card != null ? card.Ip : "kart") + (card != null && card.HasName ? "_" + card.Name.Trim() : "");
            string safe = new string(id.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c).ToArray());
            return safe + "_anlik_" + local.ToString("yyyyMMdd_HHmmss_fff") + ".log";
        }

        /// <summary>
        /// İçeriği (çağıran tarafından o an alınmış metin) arka planda dosyaya yazar; arayüzü bekletmez.
        /// Aynı adlı dosya varsa "_2", "_3" … eklenir.
        /// </summary>
        public static Task<SnapshotResult> SaveAsync(CardConfig card, string text, DateTime local, string folder = null)
        {
            return Task.Run(() =>
            {
                var r = new SnapshotResult { Card = card };
                if (string.IsNullOrEmpty(text)) { r.Error = "terminalde kaydedilecek içerik yok"; return r; }
                try
                {
                    string dir = folder ?? DefaultFolder;
                    Directory.CreateDirectory(dir);
                    string name = FileNameFor(card, local);
                    // Terminal metni '\n' kullanır; Windows'ta okunur olsun diye CRLF (var olan CR'lar çiftlenmez)
                    string body = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                    for (int n = 1; n < 1000; n++)
                    {
                        string path = System.IO.Path.Combine(dir, n == 1 ? name : System.IO.Path.GetFileNameWithoutExtension(name) + "_" + n + ".log");
                        if (File.Exists(path)) continue;
                        try
                        {
                            // CreateNew: aynı anda iki kayıt aynı adı seçse bile biri diğerinin üzerine yazamaz
                            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                            using (var w = new StreamWriter(fs, new UTF8Encoding(true)))
                                w.Write(body);
                        }
                        catch (IOException) when (File.Exists(path)) { continue; }
                        r.Ok = true;
                        r.Path = path;
                        break;
                    }
                    if (!r.Ok) r.Error = "benzersiz dosya adı bulunamadı";
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical("anlık kayıt " + (card != null ? card.DisplayName : ""), ex);
                    r.Error = ex is UnauthorizedAccessException ? "klasöre yazma izni yok (" + (folder ?? DefaultFolder) + ")"
                            : ex is DirectoryNotFoundException ? "klasör bulunamadı (" + (folder ?? DefaultFolder) + ")"
                            : ex is IOException ? "dosya yazılamadı (" + ex.Message + ")"
                            : ErrorText.Describe(ex);
                }
                return r;
            });
        }
    }
}
