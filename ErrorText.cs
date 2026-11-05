using System;
using System.IO;
using System.Net.Sockets;

namespace TelnetCards
{
    /// <summary>
    /// Bağlantı hatalarını kullanıcıya gösterilecek Türkçe mesajlara çevirir.
    /// İngilizce .NET / Windows hata metinleri kullanıcıya gösterilmez; teknik ayrıntı (hata türü, kod
    /// ve özgün metin) Technical() ile ayrıca alınır ve log dosyasına yazılır.
    /// </summary>
    public static class ErrorText
    {
        public const string ConnectionLost = "Bağlantı Kesildi";
        public const string RemoteClosed = "Bağlantı kart tarafından kapatıldı";
        public const string NotConnected = "Kart bağlı değil";

        /// <summary>Kullanıcıya gösterilecek Türkçe açıklama.</summary>
        public static string Describe(Exception ex)
        {
            if (ex == null) return ConnectionLost;

            SocketException se = FindSocketException(ex);
            if (se != null) return Describe(se.SocketErrorCode);

            if (ex is TimeoutException) return "Bağlantı zaman aşımına uğradı (kart cevap vermedi)";
            if (ex is ObjectDisposedException) return ConnectionLost;
            if (ex is IOException) return ConnectionLost;
            if (ex is InvalidOperationException) return NotConnected;
            if (ex is ArgumentException) return "Geçersiz adres veya port";
            return "Bağlantı hatası";
        }

        public static string Describe(SocketError code)
        {
            switch (code)
            {
                case SocketError.ConnectionRefused: return "Bağlantı reddedildi (kart bu portta dinlemiyor olabilir)";
                case SocketError.TimedOut: return "Bağlantı zaman aşımına uğradı (kart cevap vermedi)";
                case SocketError.HostUnreachable: return "Karta ulaşılamıyor (IP adresini ve kabloyu kontrol edin)";
                case SocketError.NetworkUnreachable: return "Ağa ulaşılamıyor (ağ bağlantısını kontrol edin)";
                case SocketError.NetworkDown: return "Ağ bağlantısı yok";
                case SocketError.HostDown: return "Kart kapalı ya da cevap vermiyor";
                case SocketError.ConnectionReset: return "Bağlantı kart tarafından sıfırlandı";
                case SocketError.ConnectionAborted: return ConnectionLost;
                case SocketError.NetworkReset: return "Ağ bağlantısı sıfırlandı";
                case SocketError.Shutdown: return ConnectionLost;
                case SocketError.NotConnected: return NotConnected;
                case SocketError.HostNotFound:
                case SocketError.NoData:
                case SocketError.TryAgain: return "Adres bulunamadı";
                case SocketError.AddressNotAvailable: return "Geçersiz IP adresi";
                case SocketError.AddressAlreadyInUse: return "Yerel port kullanımda";
                case SocketError.AccessDenied: return "Bağlantıya izin verilmedi (güvenlik duvarını kontrol edin)";
                case SocketError.NoBufferSpaceAvailable:
                case SocketError.TooManyOpenSockets: return "Sistem kaynakları yetersiz (çok fazla açık bağlantı)";
                default: return "Ağ hatası";
            }
        }

        /// <summary>KURULU bir bağlantının kopma nedeni (bağlantı kurma hatalarından farklı ifade edilir).</summary>
        public static string DescribeDrop(Exception ex)
        {
            if (ex == null) return RemoteClosed;
            SocketException se = FindSocketException(ex);
            if (se != null)
            {
                switch (se.SocketErrorCode)
                {
                    case SocketError.TimedOut:
                    case SocketError.HostUnreachable:
                    case SocketError.NetworkUnreachable:
                    case SocketError.HostDown:
                        return "Kart yanıt vermiyor (kapatılmış ya da ağ bağlantısı kopmuş olabilir)";
                }
            }
            return Describe(ex);
        }

        /// <summary>Log için teknik ayrıntı: hata türü, soket kodu ve özgün (çevrilmemiş) metin.</summary>
        public static string Technical(Exception ex)
        {
            if (ex == null) return "";
            SocketException se = FindSocketException(ex);
            string code = se != null ? " [SocketError." + se.SocketErrorCode + " / " + se.ErrorCode + "]" : "";
            return ex.GetType().Name + code + ": " + ex.Message;
        }

        private static readonly object LogLock = new object();

        /// <summary>Teknik ayrıntıyı logs\\hatalar.log dosyasına yazar (kullanıcıya gösterilmez).</summary>
        public static void LogTechnical(string source, Exception ex)
        {
            if (ex == null) return;
            try
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                System.IO.Directory.CreateDirectory(dir);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + source + "  " + Technical(ex) + Environment.NewLine;
                lock (LogLock) System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "hatalar.log"), line, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private static SocketException FindSocketException(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var se = e as SocketException;
                if (se != null) return se;
            }
            return null;
        }
    }
}
