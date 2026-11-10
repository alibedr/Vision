using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Vision
{
    public enum SentinelStatus
    {
        /// <summary>Sunucu çalışmıyor (ya da kart bağlı değil).</summary>
        Stopped,
        /// <summary>Başlatma komutu gönderildi; port 8080'in açılması bekleniyor.</summary>
        Starting,
        /// <summary>Kartın 8080 portu cevap veriyor.</summary>
        Running,
        /// <summary>Durdurma komutu gönderildi; portun kapanması bekleniyor.</summary>
        Stopping,
    }

    /// <summary>
    /// Kartın içindeki Sentinel web sunucusunun durumu (G28). Komutlar dönüş değeri basmadığı (void) için sunucunun çalışıp
    /// çalışmadığı, kartın 8080 portuna bağlanmayı deneyerek anlaşılır: bağlantı kurulursa çalışıyor. Denemede istek
    /// gönderilmez (sunucu sayfa üretmez); bağlantı hemen kapatılır.
    /// Normalde 5 sn'de bir, başlatma / durdurma sonrasında 1 sn'de bir denenir. Kart reboot olursa ya da sunucu
    /// kendiliğinden kapanırsa durum kendiliğinden düzelir. Yalnız bağlı kartlar denenir.
    /// </summary>
    public sealed class SentinelTracker
    {
        public const int Port = 8080;
        public const string StartCommand = "SentinelWebServerBaslat";
        public const string StopCommand = "SentinelWebServerBitir";

        private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan FastInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);
        private const int ConnectTimeoutMs = 1500;

        private readonly object _lock = new object();
        private SentinelStatus _status = SentinelStatus.Stopped;
        private DateTime _transitionUtc;
        private DateTime _nextProbeUtc;
        private bool _probing;
        private bool _connected;
        private int _failures;
        private string _message;
        private int _version;

        public SentinelStatus Status { get { lock (_lock) return _status; } }

        /// <summary>Son geçişle ilgili uyarı (ör. "20 sn içinde açılmadı"); yoksa null.</summary>
        public string Message { get { lock (_lock) return _message; } }

        /// <summary>Durum ya da mesaj her değiştiğinde artar (arayüz yalnız değişince yenilenir).</summary>
        public int Version { get { lock (_lock) return _version; } }

        public static string UrlFor(string ip) { return "http://" + (ip ?? "").Trim() + ":" + Port + "/"; }

        /// <summary>Başlatma komutu gönderilirken çağrılır.</summary>
        public void BeginStart(DateTime nowUtc)
        {
            lock (_lock)
            {
                _status = SentinelStatus.Starting;
                _transitionUtc = nowUtc;
                _nextProbeUtc = nowUtc + FastInterval;
                _failures = 0;
                _message = null;
                _version++;
            }
        }

        /// <summary>Durdurma komutu gönderilirken çağrılır.</summary>
        public void BeginStop(DateTime nowUtc)
        {
            lock (_lock)
            {
                _status = SentinelStatus.Stopping;
                _transitionUtc = nowUtc;
                _nextProbeUtc = nowUtc + FastInterval;
                _message = null;
                _version++;
            }
        }

        /// <summary>Ana pencerenin zamanlayıcısından sık çağrılır; zamanı gelince arka planda port denemesi başlatır.</summary>
        public void Tick(DateTime nowUtc, bool connected, string ip)
        {
            lock (_lock)
            {
                if (!connected)
                {
                    // Bağlı olmayan kart denenmez; yeniden bağlanınca hemen denenir
                    if (_connected || _status != SentinelStatus.Stopped)
                    {
                        _status = SentinelStatus.Stopped;
                        _failures = 0;
                        _version++;
                    }
                    _connected = false;
                    _nextProbeUtc = nowUtc;
                    return;
                }
                _connected = true;

                if (_status == SentinelStatus.Starting && nowUtc - _transitionUtc > StartTimeout)
                {
                    _status = SentinelStatus.Stopped;
                    _message = "Sentinel " + (int)StartTimeout.TotalSeconds + " sn içinde açılmadı (port " + Port + " cevap vermiyor). " +
                               "Komutun kartta tanımlı olduğunu terminalden kontrol edin.";
                    _version++;
                }
                else if (_status == SentinelStatus.Stopping && nowUtc - _transitionUtc > StopTimeout)
                {
                    _status = SentinelStatus.Running;
                    _message = "Sentinel " + (int)StopTimeout.TotalSeconds + " sn içinde kapanmadı (port " + Port + " hâlâ açık).";
                    _version++;
                }

                if (_probing || nowUtc < _nextProbeUtc || string.IsNullOrWhiteSpace(ip)) return;
                _probing = true;
            }
            string target = ip.Trim();
            Task.Run(async () =>
            {
                bool open = await ProbeAsync(target).ConfigureAwait(false);
                Apply(open, DateTime.UtcNow);
            });
        }

        private void Apply(bool open, DateTime nowUtc)
        {
            lock (_lock)
            {
                _probing = false;
                if (!_connected) return;
                SentinelStatus before = _status;
                switch (_status)
                {
                    case SentinelStatus.Starting:
                        if (open) { _status = SentinelStatus.Running; _message = null; }
                        break;
                    case SentinelStatus.Stopping:
                        if (!open) { _status = SentinelStatus.Stopped; _message = null; }
                        break;
                    case SentinelStatus.Running:
                        // Meşgul sunucu yüzünden tek seferlik başarısızlıkta durum titremesin: art arda iki kez
                        if (open) _failures = 0;
                        else if (++_failures >= 2) { _status = SentinelStatus.Stopped; _failures = 0; }
                        break;
                    default:
                        if (open) { _status = SentinelStatus.Running; _failures = 0; }   // terminalden başlatılmış olabilir
                        break;
                }
                bool fast = _status == SentinelStatus.Starting || _status == SentinelStatus.Stopping ||
                            (_status == SentinelStatus.Running && _failures > 0);
                _nextProbeUtc = nowUtc + (fast ? FastInterval : IdleInterval);
                if (before != _status) _version++;
            }
        }

        /// <summary>Kartın 8080 portuna bağlanılabiliyor mu? (İstek gönderilmez, bağlantı hemen kapatılır.)</summary>
        private static async Task<bool> ProbeAsync(string ip)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    Task connect = client.ConnectAsync(ip, Port);
                    Task done = await Task.WhenAny(connect, Task.Delay(ConnectTimeoutMs)).ConfigureAwait(false);
                    if (done != connect)
                    {
                        var ignored = connect.ContinueWith(t => { var e = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        return false;
                    }
                    await connect.ConfigureAwait(false);
                    return client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
