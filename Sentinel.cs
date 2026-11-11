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

    /// <summary>Port denemesinin sonucu.</summary>
    internal enum SentinelProbe
    {
        /// <summary>Bağlantı kuruldu: sunucu çalışıyor.</summary>
        Open,
        /// <summary>Kart bağlantıyı reddetti (portu dinleyen yok): sunucu durmuş olabilir.</summary>
        Refused,
        /// <summary>Süre içinde cevap yok: sunucu meşgul olabilir — durum değiştirilmez.</summary>
        NoAnswer,
    }

    /// <summary>
    /// Kartın içindeki Sentinel web sunucusunun durumu (G28). Komutlar dönüş değeri basmadığı (void) için sunucunun çalışıp
    /// çalışmadığı, kartın 8080 portuna bağlanmayı deneyerek anlaşılır: bağlantı kurulursa çalışıyor. Denemede istek
    /// gönderilmez (sunucu sayfa üretmez); bağlantı hemen kapatılır.
    /// Normalde 5 sn'de bir, başlatma / durdurma sonrasında 1 sn'de bir denenir. Kart reboot olursa ya da sunucu
    /// kendiliğinden kapanırsa durum kendiliğinden düzelir. Yalnız bağlı kartlar denenir.
    /// Çalışırken "durdu" demek için kesin kanıt aranır (G28.5): cevapsız deneme (sayfa sunarken meşgul sunucu) sayılmaz;
    /// yalnız kartın bağlantıyı REDDETMESİ sayılır ve bu en az 3 denemede, 8 sn boyunca, arada başarılı deneme olmadan
    /// sürmelidir. Böylece sayfa gösterilirken sunucu meşgul diye sayfa kapanmaz.
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
        private static readonly TimeSpan RefusedInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RefusedFor = TimeSpan.FromSeconds(8);
        private const int RefusedCount = 3;
        private const int ConnectTimeoutMs = 3000;

        private readonly object _lock = new object();
        private SentinelStatus _status = SentinelStatus.Stopped;
        private DateTime _transitionUtc;
        private DateTime _nextProbeUtc;
        private bool _probing;
        private bool _connected;
        private int _refused;              // çalışırken art arda reddedilen deneme sayısı
        private DateTime _firstRefusedUtc;
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
                _refused = 0;
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
                        _refused = 0;
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
                SentinelProbe result = await ProbeAsync(target).ConfigureAwait(false);
                Apply(result, DateTime.UtcNow);
            });
        }

        private void Apply(SentinelProbe result, DateTime nowUtc)
        {
            lock (_lock)
            {
                _probing = false;
                if (!_connected) return;
                SentinelStatus before = _status;
                bool open = result == SentinelProbe.Open;
                if (open) _refused = 0;
                switch (_status)
                {
                    case SentinelStatus.Starting:
                        if (open) { _status = SentinelStatus.Running; _message = null; }
                        break;
                    case SentinelStatus.Stopping:
                        // Kullanıcı durdurdu: kartın reddetmesi yeterli kanıt
                        if (result == SentinelProbe.Refused) { _status = SentinelStatus.Stopped; _message = null; }
                        break;
                    case SentinelStatus.Running:
                        // Cevapsız deneme sayılmaz (sunucu sayfa sunarken meşgul olabilir); yalnız sürekli ret "durdu" demektir
                        if (result == SentinelProbe.Refused)
                        {
                            if (_refused == 0) _firstRefusedUtc = nowUtc;
                            _refused++;
                            if (_refused >= RefusedCount && nowUtc - _firstRefusedUtc >= RefusedFor)
                            {
                                _status = SentinelStatus.Stopped;
                                _refused = 0;
                            }
                        }
                        break;
                    default:
                        if (open) _status = SentinelStatus.Running;   // terminalden başlatılmış olabilir
                        break;
                }
                TimeSpan next = _status == SentinelStatus.Starting || _status == SentinelStatus.Stopping ? FastInterval
                              : _status == SentinelStatus.Running && _refused > 0 ? RefusedInterval
                              : IdleInterval;
                _nextProbeUtc = nowUtc + next;
                if (before != _status) _version++;
            }
        }

        /// <summary>
        /// Kartın 8080 portuna bağlanmayı dener (istek gönderilmez, bağlantı hemen kapatılır). Ret (portu dinleyen yok)
        /// ile cevapsızlık (meşgul / ulaşılamıyor) ayrılır.
        /// </summary>
        private static async Task<SentinelProbe> ProbeAsync(string ip)
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
                        return SentinelProbe.NoAnswer;
                    }
                    await connect.ConfigureAwait(false);
                    return client.Connected ? SentinelProbe.Open : SentinelProbe.NoAnswer;
                }
            }
            catch (Exception ex)
            {
                return IsRefused(ex) ? SentinelProbe.Refused : SentinelProbe.NoAnswer;
            }
        }

        private static bool IsRefused(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var se = e as SocketException;
                if (se != null) return se.SocketErrorCode == SocketError.ConnectionRefused;
                var ae = e as AggregateException;
                if (ae != null && ae.InnerExceptions.Count > 0) return IsRefused(ae.InnerExceptions[0]);
            }
            return false;
        }
    }
}
