using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Vision
{
    // =====================================================================
    // Seri port (COM) bağlantısı. Telnet altyapısından tamamen ayrıdır:
    // CardSession / TelnetClient kullanılmaz, onlara dokunulmaz.
    // =====================================================================

    public enum SerialParity { None, Odd, Even }
    public enum SerialFlow { None, XonXoff, Hardware }

    /// <summary>Bir COM portunun kullanıcı ayarları (config.xml, &lt;SeriPortlar&gt;). Kartın seri ayarlarıyla birebir aynı olmalı.</summary>
    public sealed class SerialPortProfile
    {
        public static readonly int[] BaudRates = { 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
        public const int DefaultBaud = 115200;

        public SerialPortProfile()
        {
            BaudRate = DefaultBaud;
            DataBits = 8;
            Parity = SerialParity.None;
            StopBits = 1;
            Flow = SerialFlow.None;
        }

        [XmlAttribute("Port")] public string PortName { get; set; }
        /// <summary>Kullanıcının verdiği ad (ör. "Kart 3"); boşsa port adı gösterilir.</summary>
        [XmlAttribute("Ad")] public string Name { get; set; }
        [XmlAttribute("Baud")] public int BaudRate { get; set; }
        [XmlAttribute("Data")] public int DataBits { get; set; }
        [XmlAttribute("Parity")] public SerialParity Parity { get; set; }
        [XmlAttribute("Stop")] public int StopBits { get; set; }
        [XmlAttribute("Akis")] public SerialFlow Flow { get; set; }
        /// <summary>Gönderirken her karakterden sonra bekleme (ms).</summary>
        [XmlAttribute("KarakterGecikme")] public int CharDelayMs { get; set; }
        /// <summary>Gönderirken her satırdan (Enter) sonra bekleme (ms).</summary>
        [XmlAttribute("SatirGecikme")] public int LineDelayMs { get; set; }

        public string Label { get { return string.IsNullOrWhiteSpace(Name) ? PortName : Name.Trim(); } }

        /// <summary>"115200 8N1" gibi kısa özet (akış denetimi varsa eklenir).</summary>
        public string Summary
        {
            get
            {
                char p = Parity == SerialParity.Odd ? 'O' : Parity == SerialParity.Even ? 'E' : 'N';
                string s = BaudRate + " " + DataBits + p + StopBits;
                if (Flow == SerialFlow.XonXoff) s += " Xon/Xoff";
                else if (Flow == SerialFlow.Hardware) s += " RTS/CTS";
                return s;
            }
        }

        /// <summary>Geçerli değilse Türkçe hata.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(PortName)) return "Port seçilmedi.";
            if (!BaudRates.Contains(BaudRate)) return "Baud rate listedeki değerlerden biri olmalı.";
            if (DataBits != 7 && DataBits != 8) return "Data 7 ya da 8 olmalı.";
            if (StopBits != 1 && StopBits != 2) return "Stop 1 ya da 2 olmalı.";
            if (CharDelayMs < 0 || CharDelayMs > 1000 || LineDelayMs < 0 || LineDelayMs > 5000)
                return "Gecikme 0–1000 ms/karakter, 0–5000 ms/satır olmalı.";
            if (Name != null && Name.Length > 40) return "Ad en fazla 40 karakter olabilir.";
            return null;
        }

        public SerialPortProfile Clone() { return (SerialPortProfile)MemberwiseClone(); }
    }

    /// <summary>Bilgisayarda görünen bir COM portu.</summary>
    public sealed class SerialPortInfo
    {
        public string PortName;
        /// <summary>Windows'taki adı (ör. "USB Serial Port (COM7)"); bulunamazsa port adı.</summary>
        public string Description;
    }

    /// <summary>COM portlarını listeler. Windows adları WMI'dan okunur (yavaş olabilir; arka planda çağrılmalı).</summary>
    public static class SerialPorts
    {
        public static string[] Names()
        {
            try { return SerialPort.GetPortNames().Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().OrderBy(PortNumber).ToArray(); }
            catch { return new string[0]; }
        }

        public static List<SerialPortInfo> List()
        {
            string[] names = Names();
            var desc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        string n = o["Name"] as string;
                        if (n == null) continue;
                        Match m = Regex.Match(n, @"\((COM\d+)\)");
                        if (m.Success) desc[m.Groups[1].Value] = n;
                    }
            }
            catch { }
            return names.Select(n => new SerialPortInfo { PortName = n, Description = desc.ContainsKey(n) ? desc[n] : n }).ToList();
        }

        /// <summary>"COM12" → 12 (sıralama için); sayı yoksa büyük değer.</summary>
        public static int PortNumber(string name)
        {
            Match m = Regex.Match(name ?? "", @"(\d+)$");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) ? n : int.MaxValue;
        }
    }

    // ---------------------------------------------------------------------
    // Port erişimi (testte sahte port)
    // ---------------------------------------------------------------------

    public interface ISerialIo : IDisposable
    {
        /// <summary>Açar; olmazsa anlaşılır Türkçe iletili istisna (<see cref="SerialOpenException"/>).</summary>
        void Open(SerialPortProfile profile);
        bool IsOpen { get; }
        void Write(byte[] data, int offset, int count);
        /// <summary>Gelen bayt (arka plan iş parçacığından).</summary>
        event Action<byte[]> Received;
        /// <summary>Port kendiliğinden kapandı / kayboldu (ör. USB çevirici çıkarıldı).</summary>
        event Action<string> Lost;
        void Close();
        /// <summary>Port hâlâ bilgisayarda görünüyor mu (USB çıkarıldı mı denetimi)?</summary>
        bool StillPresent();
    }

    public sealed class SerialOpenException : Exception
    {
        public SerialOpenException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Gerçek COM portu (System.IO.Ports.SerialPort).</summary>
    public sealed class SerialPortIo : ISerialIo
    {
        private SerialPort _port;
        private string _name;
        public event Action<byte[]> Received;
        public event Action<string> Lost;

        public bool IsOpen { get { var p = _port; return p != null && p.IsOpen; } }

        public void Open(SerialPortProfile pr)
        {
            Close();
            _name = pr.PortName;
            var p = new SerialPort(pr.PortName, pr.BaudRate,
                pr.Parity == SerialParity.Odd ? Parity.Odd : pr.Parity == SerialParity.Even ? Parity.Even : Parity.None,
                pr.DataBits, pr.StopBits == 2 ? StopBits.Two : StopBits.One)
            {
                Handshake = pr.Flow == SerialFlow.XonXoff ? Handshake.XOnXOff : pr.Flow == SerialFlow.Hardware ? Handshake.RequestToSend : Handshake.None,
                Encoding = Encoding.GetEncoding(28591),      // bayt ↔ karakter birebir
                ReadBufferSize = 64 * 1024,
                WriteTimeout = 3000,
                DtrEnable = true,
                RtsEnable = pr.Flow != SerialFlow.Hardware,
            };
            try
            {
                p.Open();
            }
            catch (UnauthorizedAccessException ex)
            {
                p.Dispose();
                throw new SerialOpenException(pr.PortName + " açılamadı: port başka bir programda açık (TeraTerm olabilir) ya da erişim izni yok.", ex);
            }
            catch (IOException ex)
            {
                p.Dispose();
                throw new SerialOpenException(pr.PortName + " açılamadı: port bulunamadı ya da bu ayarlar desteklenmiyor (" + ex.Message.Trim() + ").", ex);
            }
            catch (ArgumentException ex)
            {
                p.Dispose();
                throw new SerialOpenException(pr.PortName + " açılamadı: geçersiz port ya da ayar (" + ex.Message.Trim() + ").", ex);
            }
            p.DataReceived += OnData;
            p.ErrorReceived += (s, e) => { };   // çerçeve / parity hataları: veri yine okunur
            _port = p;
        }

        private void OnData(object sender, SerialDataReceivedEventArgs e)
        {
            var p = _port;
            if (p == null) return;
            try
            {
                int n = p.BytesToRead;
                if (n <= 0) return;
                var buf = new byte[n];
                int read = p.Read(buf, 0, n);
                if (read <= 0) return;
                if (read < n) Array.Resize(ref buf, read);
                var h = Received;
                if (h != null) h(buf);
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
                    RaiseLost("Port kayboldu (USB çevirici çıkarılmış olabilir): " + ex.Message.Trim());
            }
        }

        private void RaiseLost(string why)
        {
            var h = Lost;
            if (h != null) h(why);
        }

        public void Write(byte[] data, int offset, int count)
        {
            var p = _port;
            if (p == null || !p.IsOpen) throw new InvalidOperationException("Port açık değil.");
            p.Write(data, offset, count);
        }

        public bool StillPresent()
        {
            return SerialPorts.Names().Any(n => string.Equals(n, _name, StringComparison.OrdinalIgnoreCase));
        }

        public void Close()
        {
            SerialPort p = Interlocked.Exchange(ref _port, null);
            if (p == null) return;
            p.DataReceived -= OnData;
            try { if (p.IsOpen) p.Close(); } catch { }   // USB çıkarılmışsa Close istisna atabilir
            try { p.Dispose(); } catch { }
        }

        public void Dispose() { Close(); }
    }

    // ---------------------------------------------------------------------
    // Oturum
    // ---------------------------------------------------------------------

    public enum SerialState { Closed, Open, Error }

    /// <summary>
    /// Bir COM portunun bağlantısı: açma / kapama, gelen veriyi ekrana hazırlama, gönderme kuyruğu (ayardaki gecikmelerle),
    /// yazılan satırları komut geçmişine yakalama. Telnet'ten farklı olarak kartın yeniden başlaması bağlantıyı koparmaz;
    /// port açık kaldıkça açılış çıktısı akar. Port kaybolursa (USB çıkarıldı) kapanır ve neden yazılır.
    /// </summary>
    public sealed class SerialSession : IDisposable
    {
        public const int MaxLines = 20000;

        private readonly Func<ISerialIo> _ioFactory;
        private readonly object _lock = new object();
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        private readonly SerialLineCapture _capture = new SerialLineCapture();
        private ISerialIo _io;
        private StringBuilder _pending = new StringBuilder();             // görünümün henüz almadığı ham metin
        private CancellationTokenSource _sendCts;
        private SerialState _state = SerialState.Closed;

        public SerialSession(SerialPortProfile profile, Func<ISerialIo> ioFactory = null)
        {
            Profile = profile;
            _ioFactory = ioFactory ?? (() => new SerialPortIo());
        }

        public SerialPortProfile Profile { get; private set; }
        public SerialState State { get { lock (_lock) return _state; } }
        public string LastError { get; private set; }
        public DateTime? OpenedUtc { get; private set; }
        public DateTime LastRxUtc { get; private set; }
        public DateTime LastTxUtc { get; private set; }

        /// <summary>Durum değişti (arka plan iş parçacığından da gelebilir).</summary>
        public event Action StateChanged;
        /// <summary>Kullanıcının yazıp Enter'a bastığı satır (komut geçmişi için).</summary>
        public event Action<string> LineEntered;

        public void UpdateProfile(SerialPortProfile p) { Profile = p; }

        public void Open()
        {
            lock (_lock) if (_state == SerialState.Open) return;
            ISerialIo io = _ioFactory();
            try
            {
                io.Open(Profile);
            }
            catch (SerialOpenException ex)
            {
                io.Dispose();
                SetState(SerialState.Error, ex.Message);
                AppendSystem("✕ " + ex.Message);
                return;
            }
            catch (Exception ex)
            {
                io.Dispose();
                ErrorText.LogTechnical("seri port açma " + Profile.PortName, ex);
                string m = Profile.PortName + " açılamadı: " + ErrorText.Describe(ex);
                SetState(SerialState.Error, m);
                AppendSystem("✕ " + m);
                return;
            }
            io.Received += OnReceived;
            io.Lost += OnLost;
            var cts = new CancellationTokenSource();
            lock (_lock)
            {
                _io = io;
                _sendCts = cts;
            }
            OpenedUtc = DateTime.UtcNow;
            _capture.Reset();
            AppendSystem("● " + Profile.PortName + " açıldı (" + Profile.Summary + ").");
            SetState(SerialState.Open, null);
            Task.Run(() => SendLoopAsync(io, cts.Token));
        }

        public void Close() { CloseCore(null); }

        private void CloseCore(string error)
        {
            ISerialIo io;
            CancellationTokenSource cts;
            lock (_lock)
            {
                io = _io; _io = null;
                cts = _sendCts; _sendCts = null;
                if (io == null && _state != SerialState.Open) return;
            }
            if (cts != null) { cts.Cancel(); _sendSignal.Release(); }
            byte[] drop;
            while (_sendQueue.TryDequeue(out drop)) { }
            if (io != null)
            {
                io.Received -= OnReceived;
                io.Lost -= OnLost;
                try { io.Dispose(); } catch { }
            }
            OpenedUtc = null;
            if (error != null)
            {
                AppendSystem("✕ " + error);
                SetState(SerialState.Error, error);
            }
            else
            {
                AppendSystem("○ " + Profile.PortName + " kapatıldı.");
                SetState(SerialState.Closed, null);
            }
        }

        private void OnLost(string why) { CloseCore(why); }

        /// <summary>Port açıkken bilgisayardan kayboldu mu (USB çıkarıldı)? Görünümün zamanlayıcısı çağırır.</summary>
        public void CheckPresent()
        {
            ISerialIo io;
            lock (_lock) io = _io;
            if (io != null && !io.StillPresent())
                CloseCore(Profile.PortName + " bilgisayarda artık görünmüyor (USB çevirici çıkarılmış olabilir).");
        }

        private void SetState(SerialState s, string error)
        {
            lock (_lock) _state = s;
            LastError = error;
            var h = StateChanged;
            if (h != null) h();
        }

        // ---------- Gelen veri ----------

        private void OnReceived(byte[] data)
        {
            LastRxUtc = DateTime.UtcNow;
            string text = Encoding.GetEncoding(28591).GetString(data);
            lock (_lock) _pending.Append(text);
        }

        /// <summary>Kendi yazdığı sistem satırı ("● COM7 açıldı" gibi); ekranda ayrı satırda görünür.</summary>
        private void AppendSystem(string line)
        {
            lock (_lock) _pending.Append("\u0001" + line + "\u0002");
        }

        /// <summary>Görünümün henüz almadığı metin (ham: işlem görünümde <see cref="SerialScreen"/> ile yapılır).</summary>
        public string TakePending()
        {
            lock (_lock)
            {
                if (_pending.Length == 0) return null;
                string s = _pending.ToString();
                _pending = new StringBuilder();
                return s;
            }
        }

        // ---------- Gönderme ----------

        /// <summary>Klavyeden gelen karakter(ler): olduğu gibi karta gider; satır yakalama da beslenir.</summary>
        public void SendKeys(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                string line = _capture.Feed(c);
                if (line != null)
                {
                    var h = LineEntered;
                    if (h != null) h(line);
                }
            }
            Enqueue(text);
        }

        /// <summary>Yapıştırılan metin: satır sonları CR'ye çevrilir (Enter gibi), yakalanır ve gönderilir.</summary>
        public void Paste(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string t = text.Replace("\r\n", "\r").Replace('\n', '\r');
            SendKeys(t);
        }

        /// <summary>Geçmişten seçilen komut: yazılır (Enter'sız ya da Enter'lı).</summary>
        public void TypeCommand(string command, bool withEnter)
        {
            SendKeys(command + (withEnter ? "\r" : ""));
        }

        private void Enqueue(string text)
        {
            if (State != SerialState.Open) return;
            _sendQueue.Enqueue(Encoding.GetEncoding(28591).GetBytes(text));
            _sendSignal.Release();
        }

        private async Task SendLoopAsync(ISerialIo io, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await _sendSignal.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                byte[] data;
                while (!token.IsCancellationRequested && _sendQueue.TryDequeue(out data))
                {
                    try
                    {
                        int charDelay = Profile.CharDelayMs, lineDelay = Profile.LineDelayMs;
                        if (charDelay <= 0 && lineDelay <= 0)
                        {
                            io.Write(data, 0, data.Length);
                        }
                        else
                        {
                            for (int i = 0; i < data.Length && !token.IsCancellationRequested; i++)
                            {
                                io.Write(data, i, 1);
                                if (charDelay > 0) await Task.Delay(charDelay, token).ConfigureAwait(false);
                                if (data[i] == (byte)'\r' && lineDelay > 0) await Task.Delay(lineDelay, token).ConfigureAwait(false);
                            }
                        }
                        LastTxUtc = DateTime.UtcNow;
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        ErrorText.LogTechnical("seri port yazma " + Profile.PortName, ex);
                        CloseCore(Profile.PortName + " yazılamadı (port kaybolmuş olabilir): " + ErrorText.Describe(ex));
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            CloseCore(null);
        }
    }

    // ---------------------------------------------------------------------
    // Ekran işleme ve satır yakalama (saf: testlenebilir)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Gelen ham metni ekrana uygun hale getirir: satır sonları tek '\n' olur, Backspace önceki karakteri siler
    /// (kart "\b \b" ile siler), ANSI kaçış dizileri ve diğer denetim karakterleri atılır. Sonuç parçalar halinde
    /// verilir: önce silinecek karakter sayısı, sonra eklenecek metin (görünüm RichTextBox'a böyle uygular).
    /// </summary>
    public sealed class SerialScreen
    {
        public struct Op
        {
            /// <summary>Ekranın sonundan silinecek karakter sayısı (satır başına kadar).</summary>
            public int Erase;
            public string Text;
        }

        private readonly LineNormalizer _normalizer = new LineNormalizer();
        private int _escape;              // 0: yok, 1: ESC görüldü, 2: CSI ("ESC [") içinde
        private bool _inSystem;

        /// <summary>
        /// <paramref name="lineLength"/>: ekranda son satırın şu anki uzunluğu (Backspace satır başını geçmesin).
        /// Sistem satırları \u0001 … \u0002 arasında gelir ve kendi satırında gösterilir.
        /// </summary>
        public List<Op> Process(string raw, int lineLength)
        {
            var ops = new List<Op>();
            var sb = new StringBuilder();
            int line = lineLength;
            foreach (char c in raw)
            {
                if (c == '\u0001') { _inSystem = true; if (line > 0 || EndsWithText(sb)) { sb.Append('\n'); line = 0; } continue; }
                if (c == '\u0002') { _inSystem = false; sb.Append('\n'); line = 0; continue; }
                if (_inSystem) { sb.Append(c); line++; continue; }

                if (_escape == 1)
                {
                    _escape = c == '[' ? 2 : 0;          // ESC [ → CSI; ESC x → tek karakterlik dizi, atılır
                    continue;
                }
                if (_escape == 2)
                {
                    if (c >= '@' && c <= '~') _escape = 0; // CSI sonu
                    continue;
                }
                if (c == '\u001b') { _escape = 1; continue; }

                if (c == '\b' || c == '\u007f')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '\n') { sb.Length--; line--; }
                    else if (sb.Length == 0 && line > 0) { Flush(ops, sb, 1); line--; }
                    continue;
                }
                if (c == '\r' || c == '\n')
                {
                    string n = _normalizer.Process(c.ToString());
                    if (n.Length > 0) { sb.Append('\n'); line = 0; }
                    continue;
                }
                string t = _normalizer.Process(c.ToString());   // satır sonu çifti durumunu sıfırlar
                foreach (char ch in t)
                {
                    if (ch == '\t') { sb.Append(ch); line++; continue; }
                    if (ch < ' ') continue;                      // zil vb. denetim karakterleri
                    sb.Append(ch);
                    line++;
                }
            }
            Flush(ops, sb, 0);
            return ops;
        }

        private static bool EndsWithText(StringBuilder sb) { return sb.Length > 0 && sb[sb.Length - 1] != '\n'; }

        private static void Flush(List<Op> ops, StringBuilder sb, int eraseAfter)
        {
            if (sb.Length > 0) { ops.Add(new Op { Text = sb.ToString() }); sb.Clear(); }
            if (eraseAfter > 0)
            {
                if (ops.Count > 0 && ops[ops.Count - 1].Text == null) { var o = ops[ops.Count - 1]; o.Erase += eraseAfter; ops[ops.Count - 1] = o; }
                else ops.Add(new Op { Erase = eraseAfter });
            }
        }

        /// <summary>Test / basit kullanım: işlemleri bir metne uygular.</summary>
        public static string Apply(string screen, List<Op> ops)
        {
            var sb = new StringBuilder(screen);
            foreach (Op o in ops)
            {
                for (int i = 0; i < o.Erase && sb.Length > 0 && sb[sb.Length - 1] != '\n'; i++) sb.Length--;
                if (o.Text != null) sb.Append(o.Text);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Doğrudan yazmada karta giden tuşlardan komut satırını toplar (komut geçmişi için): yazılabilir karakterler eklenir,
    /// Backspace siler, Enter (CR) satırı bitirir. Ctrl+C / Ctrl+U ve ok / ESC tuşları satırı geçersiz kılar (kartın kendi
    /// düzenlemesi Vision'ın bilmediği bir satır üretebilir; yanlış komut kaydetmemek için o satır kaydedilmez).
    /// </summary>
    public sealed class SerialLineCapture
    {
        public const int MaxLength = 1000;
        private readonly StringBuilder _line = new StringBuilder();
        private bool _invalid;

        public void Reset() { _line.Clear(); _invalid = false; }

        /// <summary>Tamamlanan satır (Enter'da, boş değilse ve geçerliyse); yoksa null.</summary>
        public string Feed(char c)
        {
            if (c == '\r' || c == '\n')
            {
                string s = _invalid ? null : _line.ToString().Trim();
                Reset();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            if (c == '\b' || c == '\u007f') { if (_line.Length > 0) _line.Length--; return null; }
            if (c == '\u0003' || c == '\u0015' || c == '\u001b') { _line.Clear(); _invalid = c == '\u001b'; return null; }
            if (c < ' ') return null;
            if (_line.Length < MaxLength) _line.Append(c);
            return null;
        }
    }

    /// <summary>Seri komut geçmişi: %APPDATA%\TelnetCards\SeriKomutGecmisi.txt (telnet geçmişinden ayrı dosya).</summary>
    public static class SerialHistoryStore
    {
        public const string FileName = "SeriKomutGecmisi.txt";

        private static List<string> _items;
        private static CommandHistory _history;
        private static bool _saveErrorLogged;

        public static string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.DataFolder, FileName);

        /// <summary>Tüm seri portların ortak geçmişi.</summary>
        public static CommandHistory History
        {
            get
            {
                if (_history == null)
                {
                    _items = CommandHistoryStore.Load(FilePath);
                    _history = new CommandHistory(_items, Save);
                }
                return _history;
            }
        }

        private static void Save()
        {
            try { CommandHistoryStore.Write(FilePath, _items); }
            catch (Exception ex)
            {
                if (!_saveErrorLogged) ErrorText.LogTechnical("seri komut geçmişi kaydetme " + FilePath, ex);
                _saveErrorLogged = true;
            }
        }
    }
}
