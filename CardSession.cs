using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Vision
{
    /// <summary>
    /// Disconnected: kullanıcı bağlamadı / kesti.  Connecting: kullanıcı bağlanıyor.  Connected: bağlı ve sağlıklı.
    /// Error: bağlanılamadı (ya da otomatik yeniden bağlanma kapalıyken koptu).
    /// Recovering: bağlıyken koptu; izleyici, kart ulaşılabilir olunca kendiliğinden yeniden bağlar.
    /// </summary>
    public enum CardState { Disconnected, Connecting, Connected, Error, Recovering }

    /// <summary>
    /// Bir kartın bağlantısı + gelen veri geçmişi. Görünümden (CardView) bağımsızdır:
    /// kart görünümü kapatılsa da bağlantı ve veri akışı sürer; tekrar açınca geçmiş geri gelir.
    /// Haberleşmenin kendisi değiştirilmeden TelnetClient üzerinden yapılır.
    /// </summary>
    public sealed class CardSession : IDisposable
    {
        // Satır sınırına ek güvenlik: satır sonu içermeyen çok uzun akışlarda bellek sınırı
        private const int MaxHistoryChars = 4000000;
        private int _historyLines;   // _history içindeki '\n' sayısı

        private static readonly Regex Ansi =
            new Regex(@"\x1B\[[0-9;?]*[ -/]*[@-~]|\x1B[@-Z\\-_]", RegexOptions.Compiled);

        private readonly TelnetClient _client = new TelnetClient();
        private readonly AppSettings _settings;
        private readonly object _lock = new object();
        private readonly StringBuilder _history = new StringBuilder();
        private readonly StringBuilder _viewPending = new StringBuilder();
        private bool _viewAttached;
        private bool _atLineStart = true;
        private StreamWriter _log;
        private CardState _state = CardState.Disconnected;
        private int _connectGeneration;   // bağlanırken "Kes" denirse geç gelen bağlantıyı iptal etmek için
        private volatile bool _disposed;
        private int _recoverBusy;         // aynı kart için çakışan yeniden bağlanma denemesi olmasın (0/1)
        private int _recoverFailures;
        private DateTime _nextRecoverUtc;
        private readonly LineNormalizer _normalizer = new LineNormalizer();
        private readonly EchoFilter _echoFilter = new EchoFilter();
        private DateTime? _connectedSinceUtc;
        private readonly CommandHistory _commandHistory = new CommandHistory();
        private readonly ResponseSplitter _responses = new ResponseSplitter();   // dönüş değerleri (kilit içinde kullanılır)

        // Komutlar (terminal + dosya gezgini) bu kapıdan sırayla geçer: bir yakalama sürerken terminal
        // komutu beklemeye alınır, böylece iki komutun cevabı birbirine karışmaz.
        private readonly SemaphoreSlim _commandGate = new SemaphoreSlim(1, 1);
        private Capture _capture;
        private readonly List<TerminalTap> _taps = new List<TerminalTap>();   // gelen verinin kopyası (ör. boot ayarları penceresi)

        private sealed class Capture
        {
            public readonly StringBuilder Buffer = new StringBuilder();
            public DateTime LastDataUtc;
        }

        public CardSession(CardConfig card, AppSettings settings)
        {
            Card = card;
            _settings = settings;
            _client.DataReceived += OnData;
            _client.Disconnected += OnClientDisconnected;
        }

        public CardConfig Card { get; private set; }

        /// <summary>
        /// Bu kartın FTP bilgisi (karttan version komutuyla alınır; yalnız bellekte). Başka kartla paylaşılmaz.
        /// Bağlantı kopunca / kesilince silinir: yeniden bağlanınca karttan tekrar alınır (kart değişmiş olabilir).
        /// </summary>
        public CardFtpCredentials Ftp
        {
            get { lock (_lock) return _ftp; }
            set { lock (_lock) _ftp = value; }
        }
        private CardFtpCredentials _ftp;

        /// <summary>Bu kartın yüklemeleri sırasında FTP sunucusunda bırakılan kopyalar ("FTP kopyalarını sil" düğmesi).</summary>
        public FtpCopyTracker FtpCopies { get; } = new FtpCopyTracker();

        /// <summary>Kartın içindeki Sentinel web sunucusunun durumu (port 8080 denemesiyle izlenir).</summary>
        public SentinelTracker Sentinel { get; } = new SentinelTracker();
        public string LastError { get; private set; }

        /// <summary>Kullanıcı ekranında kırmızı gösterilecek uyarı satırlarının ön eki.</summary>
        public const string AlertPrefix = "!!! ";

        /// <summary>
        /// Bağlıyken bağlantı beklenmedik şekilde koptuysa kopma anı (UTC). Kullanıcı "Kes" dediyse,
        /// ya da hiç bağlanılamadıysa null. Yeniden bağlanınca temizlenir.
        /// </summary>
        public DateTime? DroppedAtUtc
        {
            get { lock (_lock) return _droppedAtUtc; }
        }
        private DateTime? _droppedAtUtc;

        /// <summary>
        /// Komut geçmişi: liste tüm kartlar ve genel komut çubuğuyla ortak ve kalıcıdır (CommandHistoryStore);
        /// ↑/↓ gezinme konumu bu karta aittir.
        /// </summary>
        public CommandHistory CommandHistory { get { return _commandHistory; } }

        /// <summary>
        /// Bu kartın terminal ekranındaki renklendirme kuralları (sağ tık → Renklendir). Kart penceresi kapatılıp yeniden
        /// açılsa da korunur; yalnız bu kartı etkiler. Arayüz iş parçacığından kullanılır.
        /// </summary>
        public HighlightRules Highlights { get { return _highlights; } }
        private readonly HighlightRules _highlights = new HighlightRules();

        /// <summary>Komut / dönüş değeri listesinin sürümü (değişince artar).</summary>
        public int ResponsesVersion { get { lock (_lock) return _responses.Version; } }

        /// <summary>Bu kartın komutları ve dönüş değerleri (eskiden yeniye, kopya).</summary>
        public List<CommandResponse> GetResponses()
        {
            lock (_lock) return _responses.Snapshot();
        }

        public void ClearResponses()
        {
            lock (_lock) _responses.ClearLog();
        }

        /// <summary>
        /// Bağlantının kurulduğu an (UTC). Bağlı değilken null. Görünümün açılış zamanından bağımsızdır;
        /// bağlantı kopunca sıfırlanır, yeniden bağlanınca baştan başlar.
        /// </summary>
        public DateTime? ConnectedSinceUtc
        {
            get { lock (_lock) return _connectedSinceUtc; }
        }

        /// <summary>Bağlı ise bağlantı süresi, değilse null.</summary>
        public TimeSpan? ConnectedDuration(DateTime nowUtc)
        {
            DateTime? since = ConnectedSinceUtc;
            if (since == null) return null;
            TimeSpan d = nowUtc - since.Value;
            return d < TimeSpan.Zero ? TimeSpan.Zero : d;
        }

        /// <summary>Süreyi SS:DD:ss biçiminde verir (24 saati aşınca saat büyümeye devam eder).</summary>
        public static string FormatDuration(TimeSpan d)
        {
            return string.Format("{0:00}:{1:00}:{2:00}", (long)d.TotalHours, d.Minutes, d.Seconds);
        }

        /// <summary>Son veri alma / gönderme zamanı (aktivite göstergeleri için).</summary>
        public DateTime LastRxUtc { get; private set; }
        public DateTime LastTxUtc { get; private set; }

        public CardState State
        {
            get { lock (_lock) return _state; }
        }

        private void ResetResponses(string note)
        {
            string late;
            lock (_lock) late = _responses.Reset(note, DateTime.UtcNow);
            if (late != null) Append(late);
        }

        /// <summary>Ayar kapatılınca: açık komutlar kapanır, bekletilen metin terminale verilir.</summary>
        public void SetResponseSeparation(bool on)
        {
            if (!on) ResetResponses("ayırma kapatıldı");
        }

        /// <summary>Durum değişince tetiklenir. Arka plan iş parçacığından gelebilir.</summary>
        public event Action<CardSession> StateChanged;

        private void SetState(CardState s, string error)
        {
            lock (_lock)
            {
                if (_state == s && error == null) return;
                _state = s;
                if (error != null) LastError = error;
                if (s != CardState.Connected) _ftp = null;   // FTP bilgisi bağlantıya aittir
            }
            var h = StateChanged;
            if (h != null) h(this);
        }

        // ---------- Bağlantı ----------

        public async Task ConnectAsync()
        {
            if (State == CardState.Connecting || State == CardState.Connected) return;

            string ip = Card.Ip;
            int port = Card.Group != null ? Card.Group.Port : 23;
            int gen;
            lock (_lock)
            {
                gen = ++_connectGeneration;
                _connectedSinceUtc = null;
                _droppedAtUtc = null;
                _normalizer.Reset();
                _echoFilter.Clear();
            }
            ResetResponses("yeni bağlantı");
            SetState(CardState.Connecting, null);
            AppendSystem("Bağlanılıyor: " + ip + ":" + port + " ...");
            try
            {
                _client.KeepAlive = Monitor.ToKeepAlive();   // sağlık yoklaması mevcut sokette çalışır
                await _client.ConnectAsync(ip, port, Math.Max(1000, _settings.ConnectTimeoutMs)).ConfigureAwait(false);
                bool cancelled;
                lock (_lock) cancelled = gen != _connectGeneration;
                if (cancelled) { _client.Disconnect(); return; }
                AppendSystem("Bağlandı.");
                lock (_lock) _connectedSinceUtc = DateTime.UtcNow;   // süre gerçek bağlantı anından başlar
                SetState(CardState.Connected, null);
            }
            catch (Exception ex)
            {
                bool cancelled;
                lock (_lock) cancelled = gen != _connectGeneration;
                if (cancelled) return;
                string tr = ErrorText.Describe(ex);          // kullanıcıya Türkçe
                ErrorText.LogTechnical(Card.DisplayName + " bağlanma", ex);   // teknik ayrıntı log'a
                AppendSystem("Bağlanılamadı: " + tr);
                SetState(CardState.Error, tr);
            }
        }

        public void Disconnect()
        {
            if (State == CardState.Disconnected) return;
            lock (_lock)
            {
                _connectGeneration++;
                _connectedSinceUtc = null;
                _droppedAtUtc = null;
            }
            _client.Disconnect();
            ResetResponses("bağlantı kesildi");
            AppendSystem("Bağlantı kapatıldı.");
            SetState(CardState.Disconnected, null);
        }

        private ConnectionMonitorOptions Monitor
        {
            get { return _settings.Monitor ?? (_settings.Monitor = new ConnectionMonitorOptions()); }
        }

        private void OnClientDisconnected(string reason)
        {
            // TelnetClient bu olayı yalnızca KURULU bir bağlantı beklenmedik şekilde kopunca tetikler:
            // kart kapattı (FIN), sıfırladı (RST) ya da sağlık yoklamaları (keepalive) art arda cevapsız kaldı.
            if (_disposed) return;
            DateTime now = DateTime.UtcNow;
            bool auto = Monitor.AutoReconnect;
            lock (_lock)
            {
                _connectedSinceUtc = null;
                _droppedAtUtc = now;
                _recoverFailures = 0;
                _nextRecoverUtc = now + TimeSpan.FromMilliseconds(Monitor.ReconnectMinMs);
            }
            ResetResponses("bağlantı koptu");
            Exception cause = _client.LastError;
            string tr = ErrorText.DescribeDrop(cause);
            if (cause != null) ErrorText.LogTechnical(Card.DisplayName + " bağlantı koptu", cause);

            // Uyarı her kopmada YALNIZ BİR KEZ yazılır; yeniden bağlanma denemeleri sessizdir
            AppendLine(AlertPrefix + "BAĞLANTI KESİLDİ (" + now.ToLocalTime().ToString("HH:mm:ss") + "): " + tr);
            if (auto) AppendSystem("Kart yeniden ulaşılabilir olduğunda bağlantı otomatik olarak kurulacak.");
            SetState(auto ? CardState.Recovering : CardState.Error, tr);
        }

        // ---------- Bağlantı izleme (ConnectionMonitor'ın tek zamanlayıcısından çağrılır) ----------

        /// <summary>Kopmadan bu yana başarısız yeniden bağlanma denemesi sayısı.</summary>
        public int RecoverFailures { get { lock (_lock) return _recoverFailures; } }

        /// <summary>
        /// İzleme adımı. Hiç beklemez (bloklamaz): gerekirse işi arka planda başlatır, böylece bir kart
        /// diğerlerini geciktirmez. Aynı kart için bir deneme sürerken yenisi başlatılmaz.
        /// </summary>
        public void MonitorTick(DateTime nowUtc)
        {
            if (_disposed) return;
            CardState st = State;
            ConnectionMonitorOptions o = Monitor;

            if (st == CardState.Recovering && o.AutoReconnect)
            {
                DateTime due;
                lock (_lock) due = _nextRecoverUtc;
                if (nowUtc < due) return;
                if (Interlocked.CompareExchange(ref _recoverBusy, 1, 0) != 0) return;
                RunRecoverAsync();
            }
            else if (st == CardState.Connected && o.TelnetNopHeartbeat)
            {
                DateTime lastTraffic = LastRxUtc > LastTxUtc ? LastRxUtc : LastTxUtc;
                if ((nowUtc - lastTraffic).TotalMilliseconds < o.HeartbeatIntervalMs) return;
                if (!_commandGate.Wait(0)) return;   // komut sürüyorsa yoklama gönderme (eşzamanlı yazma yok)
                SendHeartbeatAsync();
            }
        }

        private async void RunRecoverAsync()
        {
            try { await TryRecoverAsync().ConfigureAwait(false); }
            catch (Exception ex) { ErrorText.LogTechnical(Card.DisplayName + " izleme", ex); }
            finally { Interlocked.Exchange(ref _recoverBusy, 0); }
        }

        private async void SendHeartbeatAsync()
        {
            try
            {
                LastTxUtc = DateTime.UtcNow;
                await _client.SendNopAsync().ConfigureAwait(false);
            }
            catch { /* hata varsa okuma döngüsü kopmayı zaten bildirir */ }
            finally { _commandGate.Release(); }
        }

        /// <summary>
        /// Kopmuş kartı aynı oturum (aynı TelnetClient) üzerinden yeniden bağlamayı dener; ek bağlantı açmaz.
        /// Başarısız denemeler sessizdir (terminale yazılmaz, durum değişmez → arayüz gereksiz güncellenmez).
        /// </summary>
        public async Task<bool> TryRecoverAsync()
        {
            if (_disposed || State != CardState.Recovering) return false;
            ConnectionMonitorOptions o = Monitor;
            int gen;
            lock (_lock)
            {
                gen = ++_connectGeneration;
                _normalizer.Reset();
                _echoFilter.Clear();
            }
            try
            {
                _client.KeepAlive = o.ToKeepAlive();
                await _client.ConnectAsync(Card.Ip, Card.Group != null ? Card.Group.Port : 23, o.ReconnectTimeoutMs).ConfigureAwait(false);

                DateTime now = DateTime.UtcNow;
                TimeSpan outage;
                lock (_lock)
                {
                    if (gen != _connectGeneration || _disposed)
                    {
                        _client.Disconnect();            // bu arada kullanıcı "Kes" dedi
                        return false;
                    }
                    outage = _droppedAtUtc.HasValue ? now - _droppedAtUtc.Value : TimeSpan.Zero;
                    _connectedSinceUtc = now;           // bağlantı süresi yeni bağlantıdan başlar
                    _droppedAtUtc = null;
                    _recoverFailures = 0;
                }
                AppendSystem("Bağlantı yeniden kuruldu (" + now.ToLocalTime().ToString("HH:mm:ss") +
                             ") — kesinti süresi " + FormatDuration(outage) + ".");
                SetState(CardState.Connected, null);
                return true;
            }
            catch (Exception ex)
            {
                int failures;
                lock (_lock)
                {
                    if (gen != _connectGeneration) return false;
                    failures = ++_recoverFailures;
                    _nextRecoverUtc = DateTime.UtcNow + o.ReconnectDelay(failures);
                }
                if (failures == 1) ErrorText.LogTechnical(Card.DisplayName + " yeniden bağlanma", ex);
                return false;
            }
        }

        // ---------- Gönderme ----------

        /// <param name="source">Komutun nereden geldiği (dönüş değerleri panelinde gösterilir).</param>
        public async Task SendCommandAsync(string cmd, string source = "komut satırı")
        {
            if (State != CardState.Connected) throw new InvalidOperationException("Kart bağlı değil.");
            await _commandGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (State != CardState.Connected) throw new InvalidOperationException("Kart bağlı değil.");
                await SendTerminalCommandAsync(cmd, source).ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        private async Task SendTerminalCommandAsync(string cmd, string source)
        {
            // Dönüş değeri bu komutla eşleşsin: komut karta gitmeden ÖNCE kaydedilir (cevap çok hızlı gelebilir)
            if (_settings.SeparateReturnValues && (cmd ?? "").Trim().Length > 0)
                lock (_lock) _responses.CommandSent(cmd.Trim(), source, DateTime.UtcNow);
            if (_settings.LocalEcho)
            {
                // Komut, gerçek bir terminaldeki gibi kartın istem satırına (ör. "SHELL> ") yazılır
                // ve tek bir satır sonuyla biter. Kartın geri ekoladığı Enter / komut EchoFilter ile ayıklanır;
                // böylece aynı satır sonu iki kez basılmaz. Eko, gönderimden ÖNCE beklenmeye başlanır
                // (cihaz çok hızlı ekolarsa da yakalanır).
                string prefix;
                lock (_lock)
                {
                    prefix = _atLineStart ? "> " : "";
                    _echoFilter.Expect(cmd, DateTime.UtcNow);
                }
                Append(prefix + cmd + "\n");
            }
            LastTxUtc = DateTime.UtcNow;
            await _client.SendAsync(cmd + _settings.LineEndingChars).ConfigureAwait(false);
        }

        /// <summary>
        /// Satırı kartın beklediği girişe (ör. bspBootChange alanı) gönderir: satır sonu eklenir, komut geçmişine ve dönüş
        /// değerleri paneline yazılmaz. Kartın ekosu terminalde görünür.
        /// </summary>
        public Task SendInputLineAsync(string text)
        {
            if (State != CardState.Connected) throw new InvalidOperationException("Kart bağlı değil.");
            LastTxUtc = DateTime.UtcNow;
            return _client.SendAsync((text ?? "") + _settings.LineEndingChars);
        }

        /// <summary>
        /// Kart etkileşimli bir girişte (ör. bspBootChange alanları) bekliyor: dosya gezgini gibi arka plan komutları
        /// gönderilmez (gönderilseydi alanın değeri olarak yazılırdı).
        /// </summary>
        public bool InteractiveInputActive { get; set; }

        /// <summary>Kartın gönderdiği verinin (ANSI ayıklanmış, satır sonları "\n") kopyasını almaya başlar.</summary>
        public TerminalTap OpenTap()
        {
            var t = new TerminalTap(this);
            lock (_lock) _taps.Add(t);
            return t;
        }

        internal void CloseTap(TerminalTap t)
        {
            lock (_lock) _taps.Remove(t);
        }

        public Task SendRawAsync(string raw)
        {
            LastTxUtc = DateTime.UtcNow;
            return _client.SendAsync(raw);
        }

        // ---------- Komut çalıştırıp cevabı yakalama (dosya gezgini) ----------

        /// <summary>
        /// Komutu kartın MEVCUT bağlantısı üzerinden gönderir ve cevabını yakalar; yeni bağlantı açmaz.
        /// Yakalanan cevap terminal geçmişine yazılmaz (terminalin görünümü etkilenmez).
        /// Cevap, kabuğun dönüş değeri satırı ("value = N = 0x...") gelince tamamlanmış sayılır;
        /// gelmezse veri kesildikten sonra zaman aşımına uğrar.
        /// </summary>
        /// <param name="idleTimeoutSec">Dönüş satırı gelmeden önce bu kadar saniye veri gelmezse zaman aşımı
        /// (copy / xcopy gibi sessiz çalışan uzun işlemler için büyütülür).</param>
        public async Task<ShellResponse> ExecuteAsync(string command, int idleTimeoutSec = 4)
        {
            if (State != CardState.Connected)
                return ShellResponse.Failed(command, "Kart bağlı değil. Dosya gezgini ayrı bağlantı açmaz.", "");
            if (InteractiveInputActive)
                return ShellResponse.Failed(command, "Kartta boot ayarları (bspBootChange) düzenleniyor; komut gönderilmedi. " +
                                                     "Boot ayarları penceresi bitince tekrar deneyin.", "");

            await _commandGate.WaitAsync().ConfigureAwait(false);
            var cap = new Capture { LastDataUtc = DateTime.UtcNow };
            try
            {
                if (State != CardState.Connected)
                    return ShellResponse.Failed(command, "Kart bağlı değil. Dosya gezgini ayrı bağlantı açmaz.", "");

                // Önceki (terminal) komutun cevabı bitsin: hat kısa süre sessizleşene kadar bekle (en çok 2 sn)
                DateTime settleStart = DateTime.UtcNow;
                while ((DateTime.UtcNow - settleStart).TotalSeconds < 2 &&
                       ((DateTime.UtcNow - LastRxUtc).TotalMilliseconds < 200 || (DateTime.UtcNow - LastTxUtc).TotalMilliseconds < 200))
                {
                    await Task.Delay(20).ConfigureAwait(false);
                }

                lock (_lock)
                {
                    _capture = cap;
                    _echoFilter.Clear();
                }
                LastTxUtc = DateTime.UtcNow;
                try
                {
                    await _client.SendAsync(command + _settings.LineEndingChars).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical(Card.DisplayName + " " + command, ex);
                    return ShellResponse.Failed(command, "Komut gönderilemedi: " + ErrorText.Describe(ex), "");
                }

                DateTime start = DateTime.UtcNow;
                DateTime? valueSeenAt = null;
                while (true)
                {
                    await Task.Delay(20).ConfigureAwait(false);
                    string snapshot;
                    DateTime last;
                    lock (_lock)
                    {
                        snapshot = cap.Buffer.ToString();
                        last = cap.LastDataUtc;
                    }
                    DateTime now = DateTime.UtcNow;

                    if (State != CardState.Connected)
                        return ShellResponse.Failed(command, ErrorText.ConnectionLost + " — cevap tamamlanamadı.", snapshot);

                    // Bitiş işareti: dönüş satırı, ya da kabuk hatası (eksik komut: "value =" basılmaz)
                    if (valueSeenAt == null && (ShellResponse.HasReturnValue(snapshot) || ShellResponse.HasShellError(snapshot)))
                        valueSeenAt = now;
                    // Son çare: çıktı kabuk istemiyle bitti ve yarım saniyedir sessiz → kabuk yeni komut bekliyor
                    if (valueSeenAt == null && ShellResponse.EndsAtPrompt(snapshot) && (now - last).TotalMilliseconds >= 500)
                        valueSeenAt = now;

                    // Dönüş satırı geldi: ardından gelen istemi de yutmak için kısa bir sessizlik bekle
                    if (valueSeenAt != null &&
                        ((now - last).TotalMilliseconds >= 150 || (now - valueSeenAt.Value).TotalMilliseconds >= 600))
                    {
                        ShellResponse done = ShellResponse.Parse(command, snapshot);
                        ReturnPreambleToTerminal(done);
                        return done;
                    }

                    // Dönüş satırı hiç gelmedi: veri idleTimeoutSec kadar kesildiyse ya da toplam süre aşıldıysa vazgeç
                    double maxTotal = Math.Max(20, idleTimeoutSec * 2);
                    if (valueSeenAt == null && ((now - last).TotalSeconds >= idleTimeoutSec || (now - start).TotalSeconds >= maxTotal))
                    {
                        ShellResponse r = ShellResponse.Parse(command, snapshot);
                        r.TimedOut = true;
                        ReturnPreambleToTerminal(r);
                        return r;
                    }
                }
            }
            finally
            {
                lock (_lock)
                {
                    if (_capture == cap) _capture = null;
                }
                _commandGate.Release();
            }
        }

        /// <summary>Yakalamaya karışmış başka komut çıktısını (ör. terminalin gecikmiş cevabı) terminale geri ver.</summary>
        private void ReturnPreambleToTerminal(ShellResponse r)
        {
            if (string.IsNullOrEmpty(r.Preamble)) return;
            string text = r.Preamble;
            // Terminal komutunun gecikmiş cevabı: dönüş değeri varsa o da ayrılır (terminale karışmaz)
            if (_settings.SeparateReturnValues) lock (_lock) text = _responses.Process(text, DateTime.UtcNow);
            if (text.Length > 0) Append(text);
        }

        // ---------- Gelen veri ----------

        private void OnData(string raw)
        {
            DateTime now = DateTime.UtcNow;
            LastRxUtc = now;
            string text = Ansi.Replace(raw, "");
            lock (_lock)
            {
                text = _normalizer.Process(text);           // tüm satır sonu varyantları → tek '\n'
                foreach (TerminalTap tap in _taps) tap.Append(text);
                if (_capture != null)
                {
                    // Dosya gezgini gibi bir yakalama sürüyor: cevap terminale değil yakalayıcıya gider
                    _capture.Buffer.Append(text);
                    _capture.LastDataUtc = now;
                    return;
                }
                text = _echoFilter.Filter(text, now);       // yerel eko açıksa cihazın tekrar ekosunu ayıkla
                // Komut dönüş değerleri ("value = …") canlı loglardan ayrılır; yalnız açık bir komut varken
                if (_settings.SeparateReturnValues) text = _responses.Process(text, now);
            }
            if (text.Length > 0) Append(text);
        }

        private void AppendSystem(string message)
        {
            AppendLine("*** " + message);
        }

        private void AppendLine(string line)
        {
            bool needBreak;
            lock (_lock) needBreak = !_atLineStart;
            Append((needBreak ? "\n" : "") + line + "\n");
        }

        private void Append(string text)
        {
            lock (_lock)
            {
                if (_settings.Timestamp) text = AddTimestamps(text);
                else _atLineStart = text[text.Length - 1] == '\n';

                _history.Append(text);
                _historyLines += CountNewlines(text);
                TrimHistory(false);
                if (_viewAttached) _viewPending.Append(text);

                if (_settings.LogToFile) WriteLog(text);
                else if (_log != null) { _log.Dispose(); _log = null; }
            }
        }

        /// <summary>Terminal satır sınırı (ayardan; tüm kartlar için ortak).</summary>
        public int MaxLines
        {
            get { return Math.Max(AppSettings.MinTerminalLines, _settings.MaxTerminalLines); }
        }

        private static int CountNewlines(string s)
        {
            int n = 0;
            foreach (char c in s) if (c == '\n') n++;
            return n;
        }

        /// <summary>
        /// Geçmişi satır sınırında tutar: en eski satırlar silinir, en yeniler kalır (hiçbir zaman tümü silinmez).
        /// Görünüm kapalıyken de bellek sınırlı kalır. exact=false iken sık kırpmamak için %10 pay bırakılır
        /// (görünüm açılırken zaten tam sınıra kırpılır). Kilit içinde çağrılmalı.
        /// </summary>
        private void TrimHistory(bool exact)
        {
            int limit = MaxLines;
            int allowed = exact ? limit : limit + Math.Max(20, limit / 10);
            // Görünümle aynı kural: satır sonu gelmemiş son satır (ör. "-> " istemi) da bir satır sayılır
            bool partial = _history.Length > 0 && _history[_history.Length - 1] != '\n';
            int count = _historyLines + (partial ? 1 : 0);
            if (count > allowed)
            {
                int remove = count - limit;             // tam sınıra in
                int idx = 0, seen = 0;
                for (int i = 0; i < _history.Length && seen < remove; i++)
                {
                    if (_history[i] == '\n') { seen++; idx = i + 1; }
                }
                _history.Remove(0, idx);
                _historyLines -= seen;
            }
            if (_history.Length > MaxHistoryChars)
            {
                int cut = _history.Length - MaxHistoryChars / 2;
                _historyLines -= CountNewlines(_history.ToString(0, cut));
                _history.Remove(0, cut);
            }
        }

        /// <summary>Satır sınırı değişince çağrılır: geçmiş hemen yeni sınıra kırpılır.</summary>
        public void ApplyLineLimit()
        {
            lock (_lock) TrimHistory(true);
        }

        private string AddTimestamps(string text)
        {
            var sb = new StringBuilder(text.Length + 32);
            foreach (char c in text)
            {
                if (_atLineStart)
                {
                    sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("] ");
                    _atLineStart = false;
                }
                sb.Append(c);
                if (c == '\n') _atLineStart = true;
            }
            return sb.ToString();
        }

        private void WriteLog(string text)
        {
            try
            {
                if (_log == null)
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(dir);
                    string group = Card.Group != null ? Card.Group.Name : "grup";
                    string name = string.Join("_", (group + "_" + Card.Ip).Split(Path.GetInvalidFileNameChars()));
                    string path = Path.Combine(dir, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                    _log = new StreamWriter(path, true, Encoding.UTF8) { AutoFlush = true };
                }
                _log.Write(text);
            }
            catch { }
        }

        // ---------- Görünüm bağlantısı ----------

        /// <summary>Görünüm açıldığında çağrılır: tüm geçmişi döndürür, sonraki veriler TakePending ile alınır.</summary>
        public string AttachView()
        {
            lock (_lock)
            {
                _viewAttached = true;
                _viewPending.Clear();
                TrimHistory(true);
                return _history.ToString();
            }
        }

        public void DetachView()
        {
            lock (_lock)
            {
                _viewAttached = false;
                _viewPending.Clear();
            }
        }

        /// <summary>Görünümün son çağrıdan bu yana basmadığı metin (yoksa null).</summary>
        public string TakePending()
        {
            string late;
            lock (_lock) late = _responses.FlushIfStale(DateTime.UtcNow);   // bekletilen yarım satır dönüş değeri çıkmadı
            if (late != null) Append(late);
            lock (_lock)
            {
                if (_viewPending.Length == 0) return null;
                string s = _viewPending.ToString();
                _viewPending.Clear();
                return s;
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
                _historyLines = 0;
                _viewPending.Clear();
            }
        }

        public void Dispose()
        {
            _disposed = true;                                   // izleyici bu kartı artık denemez
            lock (_lock) _connectGeneration++;                  // süren yeniden bağlanma sonucu yok sayılır
            _client.DataReceived -= OnData;
            _client.Disconnected -= OnClientDisconnected;
            _client.Dispose();
            lock (_lock)
            {
                if (_log != null) { _log.Dispose(); _log = null; }
            }
        }
    }
}
