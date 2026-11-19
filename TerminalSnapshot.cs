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
    /// Dosya adı mevcut log adlandırmasını izler: logs\TelnetLoglari\Grup_IP[_Ad]_anlik_yyyyMMdd_HHmmss_fff.log
    /// Seri pencerelerin kayıtları ayrı klasöre gider (logs\SeriKanalLoglari; 1.10 — loglar karışmasın).
    /// </summary>
    public static class TerminalSnapshot
    {
        /// <summary>Vision'ın log klasörü (hatalar.log, olaylar\, TelnetLoglari\, SeriKanalLoglari\).</summary>
        public static string DefaultFolder
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"); }
        }

        /// <summary>Telnet kart ekranlarının "Dosyaya kaydet" kayıtları (kart başına ya da genel). Yoksa ilk kayıtta oluşur.</summary>
        public static string TelnetFolder { get { return System.IO.Path.Combine(DefaultFolder, "TelnetLoglari"); } }

        /// <summary>Seri pencerelerin "Dosyaya kaydet" kayıtları. Yoksa ilk kayıtta oluşur.</summary>
        public static string SerialFolder { get { return System.IO.Path.Combine(DefaultFolder, "SeriKanalLoglari"); } }

        private static string SafeName(string id)
        {
            return new string(id.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c).ToArray());
        }

        /// <summary>Seri pencere kaydının dosya adı: Port[_Ad]_anlik_yyyyMMdd_HHmmss_fff.log (ör. COM7_Kart_3_anlik_…).</summary>
        public static string SerialFileNameFor(SerialPortProfile port, DateTime local)
        {
            string id = (port != null ? port.PortName : "seri") + (port != null && !string.IsNullOrWhiteSpace(port.Name) ? "_" + port.Name.Trim() : "");
            return SafeName(id) + "_anlik_" + local.ToString("yyyyMMdd_HHmmss_fff") + ".log";
        }

        public static string FileNameFor(CardConfig card, DateTime local)
        {
            string group = card != null && card.Group != null ? card.Group.Name : "grup";
            string id = group + "_" + (card != null ? card.Ip : "kart") + (card != null && card.HasName ? "_" + card.Name.Trim() : "");
            return SafeName(id) + "_anlik_" + local.ToString("yyyyMMdd_HHmmss_fff") + ".log";
        }

        /// <summary>
        /// İçeriği (çağıran tarafından o an alınmış metin) arka planda dosyaya yazar; arayüzü bekletmez.
        /// Aynı adlı dosya varsa "_2", "_3" … eklenir.
        /// </summary>
        public static Task<SnapshotResult> SaveAsync(CardConfig card, string text, DateTime local, string folder = null)
        {
            return SaveCoreAsync(card, FileNameFor(card, local), text, folder ?? TelnetFolder, card != null ? card.DisplayName : "");
        }

        /// <summary>Seri penceredeki içeriği logs\SeriKanalLoglari altına kaydeder (Telnet kaydıyla aynı kurallar).</summary>
        public static Task<SnapshotResult> SaveSerialAsync(SerialPortProfile port, string text, DateTime local, string folder = null)
        {
            return SaveCoreAsync(null, SerialFileNameFor(port, local), text, folder ?? SerialFolder, port != null ? port.Label : "seri");
        }

        private static Task<SnapshotResult> SaveCoreAsync(CardConfig card, string name, string text, string folder, string what)
        {
            return Task.Run(() =>
            {
                var r = new SnapshotResult { Card = card };
                if (string.IsNullOrEmpty(text)) { r.Error = "ekranda kaydedilecek içerik yok"; return r; }
                try
                {
                    string dir = folder;
                    Directory.CreateDirectory(dir);   // varsa dokunulmaz
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
                    ErrorText.LogTechnical("anlık kayıt " + what, ex);
                    r.Error = ex is UnauthorizedAccessException ? "klasöre yazma izni yok (" + folder + ")"
                            : ex is DirectoryNotFoundException ? "klasör bulunamadı (" + folder + ")"
                            : ex is IOException ? "dosya yazılamadı (" + ex.Message + ")"
                            : ErrorText.Describe(ex);
                }
                return r;
            });
        }
    }
}
