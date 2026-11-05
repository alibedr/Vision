using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TelnetCards
{
    /// <summary>
    /// Minimal, asenkron telnet istemcisi.
    /// - IAC (0xFF) seçenek pazarlıklarını yakalar ve hepsini reddeder (WONT/DONT),
    ///   böylece kart "ham" metin modunda konuşur.
    /// - Gelen veriyi DataReceived olayıyla metin olarak verir (arka plan iş parçacığından).
    /// - TCP_NODELAY açık: kısa komutlar beklemeden (Nagle olmadan) gider.
    /// </summary>
    public sealed class TelnetClient : IDisposable
    {
        private const byte IAC = 255, DONT = 254, DO = 253, WONT = 252, WILL = 251, SB = 250, SE = 240;

        private enum ParseState { Data, Iac, Option, Sub, SubIac }

        private readonly object _sync = new object();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private TcpClient _tcp;
        private NetworkStream _stream;
        private ParseState _state = ParseState.Data;
        private byte _pendingCmd;

        public TelnetClient()
        {
            Encoding = Encoding.ASCII;
        }

        public Encoding Encoding { get; set; }

        /// <summary>Bağlantıyı koparan son hata (karşı taraf düzgün kapattıysa null). Tanılama ve çeviri için.</summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Boşta bağlantı denetimi (TCP keepalive). null ise kapalıdır (eski davranış).
        /// Kart kapanır ya da kablo çekilirse kart hiçbir paket göndermez; okuma sonsuza dek bekler.
        /// Keepalive'da işletim sistemi MEVCUT sokete boş yoklama paketleri gönderir; bunları kartın
        /// kabuğu değil TCP yığını cevaplar. Ardışık cevapsız yoklamalardan sonra soket hata verir ve
        /// okuma döngüsü kopmayı normal yoldan (Disconnected olayı) bildirir.
        /// </summary>
        public KeepAliveOptions KeepAlive { get; set; }

        public sealed class KeepAliveOptions
        {
            public int IdleMs { get; set; }               // son trafikten sonra ilk yoklamaya kadar
            public int IntervalMs { get; set; }           // yoklamalar arası = yoklama başına zaman aşımı
            public int ProbeCount { get; set; }           // kopma için ardışık cevapsız yoklama sayısı
            public int RetransmitTimeoutSec { get; set; } // bekleyen veri hiç onaylanmazsa (Windows TCP_MAXRT)
        }

        public bool IsConnected
        {
            get
            {
                lock (_sync) return _stream != null && _tcp != null && _tcp.Connected;
            }
        }

        /// <summary>Karttan gelen metin. Arka plan iş parçacığında tetiklenir.</summary>
        public event Action<string> DataReceived;

        /// <summary>Bağlantı beklenmedik şekilde koptuğunda tetiklenir (kullanıcı kesince tetiklenmez).</summary>
        public event Action<string> Disconnected;

        public async Task ConnectAsync(string host, int port, int timeoutMs = 5000)
        {
            Disconnect();

            var tcp = new TcpClient { NoDelay = true, ReceiveBufferSize = 64 * 1024 };
            Task connectTask = tcp.ConnectAsync(host, port);
            Task finished = await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (finished != connectTask)
            {
                tcp.Close();
                // Geç biten bağlantı denemesinin hatası "gözlemlenmemiş" kalmasın
                _ = connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException(host + ":" + port + " adresine " + timeoutMs + " ms içinde bağlanılamadı.");
            }

            try
            {
                await connectTask.ConfigureAwait(false);   // bağlantı hatası varsa burada fırlar
            }
            catch
            {
                tcp.Close();
                throw;
            }

            LastError = null;
            if (KeepAlive != null) ApplyKeepAlive(tcp.Client, KeepAlive);
            NetworkStream stream = tcp.GetStream();
            lock (_sync)
            {
                _tcp = tcp;
                _stream = stream;
                _state = ParseState.Data;
            }

            _ = Task.Run(() => ReadLoopAsync(stream));
        }

        // Ham seçenek numaraları (.NET 4.6.1'de adları yok): Windows değerleriyle aynıdır; .NET Core'da platforma çevrilir
        private const SocketOptionName TcpKeepIdle = (SocketOptionName)3;    // TCP_KEEPALIVE / TCP_KEEPIDLE (sn)
        private const SocketOptionName TcpKeepCount = (SocketOptionName)16;  // TCP_KEEPCNT
        private const SocketOptionName TcpKeepInterval = (SocketOptionName)17; // TCP_KEEPINTVL (sn)
        private const SocketOptionName TcpMaxRetransmitTime = (SocketOptionName)5; // TCP_MAXRT (sn, yalnız Windows)

        private static void ApplyKeepAlive(Socket socket, KeepAliveOptions o)
        {
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }
            catch { return; }

            bool timesSet = false;
            try
            {
                // Windows 10 1709+ ve Linux: saniye cinsinden
                socket.SetSocketOption(SocketOptionLevel.Tcp, TcpKeepIdle, Math.Max(1, o.IdleMs / 1000));
                socket.SetSocketOption(SocketOptionLevel.Tcp, TcpKeepInterval, Math.Max(1, o.IntervalMs / 1000));
                timesSet = true;
            }
            catch { }

            if (!timesSet)
            {
                // Daha eski Windows: SIO_KEEPALIVE_VALS (açık, boşta süre ms, aralık ms). Yoklama sayısı burada sabit 10'dur.
                try
                {
                    var vals = new byte[12];
                    BitConverter.GetBytes(1u).CopyTo(vals, 0);
                    BitConverter.GetBytes((uint)o.IdleMs).CopyTo(vals, 4);
                    BitConverter.GetBytes((uint)o.IntervalMs).CopyTo(vals, 8);
                    socket.IOControl(IOControlCode.KeepAliveValues, vals, null);
                }
                catch { }
            }

            try { socket.SetSocketOption(SocketOptionLevel.Tcp, TcpKeepCount, Math.Max(1, o.ProbeCount)); }
            catch { }   // Windows 10 1703 öncesi desteklemez: sistem varsayılanı kullanılır

            if (o.RetransmitTimeoutSec > 0 && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Kapalı karta veri gönderilirse yeniden gönderimler dakikalarca sürmesin
                try { socket.SetSocketOption(SocketOptionLevel.Tcp, TcpMaxRetransmitTime, o.RetransmitTimeoutSec); }
                catch { }
            }
        }

        /// <summary>
        /// İsteğe bağlı telnet NOP (IAC 241) gönderir: RFC 854'e göre sunucu yok sayar, kabuğa ulaşmaz.
        /// Varsayılan izlemede kullanılmaz (keepalive yeterli); yapılandırmayla açılabilir.
        /// </summary>
        public Task SendNopAsync()
        {
            return WriteRawAsync(new byte[] { IAC, 241 });
        }

        private async Task ReadLoopAsync(NetworkStream stream)
        {
            var buf = new byte[16 * 1024];
            var data = new List<byte>(16 * 1024);
            string reason = "Bağlantı karşı taraftan kapatıldı.";

            try
            {
                while (true)
                {
                    int n = await stream.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    if (n == 0) break;

                    data.Clear();
                    List<byte> reply = Process(buf, n, data);
                    if (reply.Count > 0) await WriteRawAsync(reply.ToArray()).ConfigureAwait(false);

                    if (data.Count > 0)
                    {
                        var handler = DataReceived;
                        if (handler != null)
                        {
                            // Abonedeki bir hata bağlantıyı koparmasın
                            try { handler(Encoding.GetString(data.ToArray())); }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex;
                reason = "Bağlantı hatası: " + ex.Message;
            }

            // Kullanıcı Disconnect() dediyse _stream artık bu akış değildir; olay tetiklenmez.
            bool unexpected;
            lock (_sync) unexpected = ReferenceEquals(_stream, stream);
            if (unexpected)
            {
                Disconnect();
                var handler = Disconnected;
                if (handler != null) handler(reason);
            }
        }

        /// <summary>Telnet komutlarını ayıklar; düz veriyi 'data'ya ekler, gönderilecek cevapları döndürür.</summary>
        private List<byte> Process(byte[] buf, int n, List<byte> data)
        {
            var reply = new List<byte>();
            for (int i = 0; i < n; i++)
            {
                byte b = buf[i];
                switch (_state)
                {
                    case ParseState.Data:
                        if (b == IAC) _state = ParseState.Iac;
                        else data.Add(b);
                        break;

                    case ParseState.Iac:
                        if (b == IAC) { data.Add(IAC); _state = ParseState.Data; }   // kaçışlı 0xFF
                        else if (b == DO || b == DONT || b == WILL || b == WONT) { _pendingCmd = b; _state = ParseState.Option; }
                        else if (b == SB) _state = ParseState.Sub;
                        else _state = ParseState.Data;                               // NOP, GA vb.
                        break;

                    case ParseState.Option:
                        // Her şeyi reddet: DO -> WONT, WILL -> DONT. DONT/WONT'a cevap gerekmez.
                        if (_pendingCmd == DO) reply.AddRange(new byte[] { IAC, WONT, b });
                        else if (_pendingCmd == WILL) reply.AddRange(new byte[] { IAC, DONT, b });
                        _state = ParseState.Data;
                        break;

                    case ParseState.Sub:
                        if (b == IAC) _state = ParseState.SubIac;
                        break;

                    case ParseState.SubIac:
                        _state = b == SE ? ParseState.Data : ParseState.Sub;
                        break;
                }
            }
            return reply;
        }

        public Task SendAsync(string text)
        {
            byte[] bytes = Encoding.GetBytes(text);

            // Veri içindeki 0xFF baytlarını telnet kuralına göre çiftle
            if (Array.IndexOf(bytes, IAC) >= 0)
            {
                var list = new List<byte>(bytes.Length + 4);
                foreach (byte b in bytes)
                {
                    list.Add(b);
                    if (b == IAC) list.Add(IAC);
                }
                bytes = list.ToArray();
            }
            return WriteRawAsync(bytes);
        }

        private async Task WriteRawAsync(byte[] bytes)
        {
            NetworkStream s;
            lock (_sync) s = _stream;
            if (s == null) throw new InvalidOperationException("Bağlı değil.");

            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await s.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await s.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Disconnect()
        {
            TcpClient tcp;
            NetworkStream stream;
            lock (_sync)
            {
                tcp = _tcp; stream = _stream;
                _tcp = null; _stream = null;
            }
            // Akışı kapatmak bekleyen ReadAsync'i hata ile sonlandırır; döngü sessizce biter.
            try { if (stream != null) stream.Close(); } catch { }
            try { if (tcp != null) tcp.Close(); } catch { }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
