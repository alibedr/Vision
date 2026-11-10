using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vision
{
    /// <summary>
    /// Kartın gönderdiği verinin kopyası (terminal akışı değişmeden sürer). Arayüz Take() ile düzenli olarak okur.
    /// </summary>
    public sealed class TerminalTap : IDisposable
    {
        private readonly CardSession _session;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly object _lock = new object();
        private bool _closed;

        internal TerminalTap(CardSession session) { _session = session; }

        internal void Append(string text)
        {
            lock (_lock)
            {
                if (_closed) return;
                _buffer.Append(text);
                if (_buffer.Length > 1000000) _buffer.Remove(0, _buffer.Length - 500000);   // okunmazsa bellek şişmesin
            }
        }

        /// <summary>Son çağrıdan bu yana gelen metin (yoksa null).</summary>
        public string Take()
        {
            lock (_lock)
            {
                if (_buffer.Length == 0) return null;
                string s = _buffer.ToString();
                _buffer.Clear();
                return s;
            }
        }

        public void Dispose()
        {
            lock (_lock) { _closed = true; _buffer.Clear(); }
            _session.CloseTap(this);
        }
    }

    /// <summary>bspBootChange / bootChange sırasında kartın sorduğu bir boot alanı.</summary>
    public sealed class BootField
    {
        /// <summary>Kartın yazdığı alan adı (ör. "host inet (h)").</summary>
        public string Label { get; set; }
        /// <summary>Komut başladığında kartın gösterdiği değer.</summary>
        public string OriginalValue { get; set; }
        /// <summary>Kartın bu alan için en son gösterdiği değer.</summary>
        public string CardValue { get; set; }
        /// <summary>Kullanıcının bu alan için gönderdiği yeni değer (gönderilmediyse null; "." temizle).</summary>
        public string Entered { get; set; }

        /// <summary>Alanın kartta kalacak değeri (yeni değer gönderildiyse o, yoksa kartın gösterdiği).</summary>
        public string EffectiveValue
        {
            get
            {
                if (Entered == null) return CardValue ?? "";
                return Entered == BootChangeParser.ClearToken ? "" : Entered;
            }
        }

        public bool Changed { get { return Entered != null && EffectiveValue != (OriginalValue ?? ""); } }
    }

    public enum BootChangePhase
    {
        /// <summary>Komut gönderildi; kart henüz bir alan sormadı.</summary>
        Starting,
        /// <summary>Kart alanları soruyor.</summary>
        Prompting,
        /// <summary>Komut bitti (dönüş değeri / kabuk istemi geldi).</summary>
        Finished,
        /// <summary>Komut çalışmadı (kartta yok) ya da bağlantı koptu.</summary>
        Failed,
    }

    /// <summary>
    /// bspBootChange / bootChange etkileşimini kartın ham çıktısından izler (arayüzden bağımsız) — G27.
    /// Kart her alanı "alan adı          : değer " biçiminde sorar ve satır sonu basmadan girişi bekler. Kart çalışırken
    /// gelen loglar bu satırların arasına (hatta aynı satırın sonuna) karışır; burada alan istemleri loglardan ayrılır:
    /// hangi alanın sorulduğu ve değeri <see cref="Fields"/>'ta, araya giren satırlar <see cref="Logs"/>'ta tutulur.
    /// Yarım gelen istemin yanlış okunmaması için son (satır sonu gelmemiş) satır, veri kısa süre kesilince değerlendirilir.
    /// </summary>
    public sealed class BootChangeParser
    {
        /// <summary>VxWorks bootChange: "." alanı temizler, "-" önceki alana döner, Ctrl+D çıkar.</summary>
        public const string ClearToken = ".";
        public const string PreviousToken = "-";
        public const string QuitChar = "\x04";

        /// <summary>Son satır bu kadar süre değişmezse (kart girişi bekliyor) değerlendirilir.</summary>
        public static readonly TimeSpan TailSettle = TimeSpan.FromMilliseconds(120);

        // "boot device          : motetsec0 "  ·  "inet on ethernet (e) : 10.0.0.5:ffffff00 "  ·  "ftp password (pw) (blank = use rsh): x"
        private static readonly Regex FieldLine = new Regex(
            @"^(?<label>[A-Za-z][A-Za-z0-9 _\-\./]{0,44}?(?:\s*\([^()\r\n]{1,24}\))*)(?<pad>\s*):\s?(?<rest>.*)$", RegexOptions.Compiled);
        private static readonly Regex KeyParen = new Regex(@"\(\s*[a-z]{1,3}\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Parens = new Regex(@"\([^()]*\)", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ValueLine = new Regex(@"^\s*value\s*=\s*-?\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ShellPromptOnly = new Regex(@"^\s*->\s*$", RegexOptions.Compiled);
        private static readonly Regex ShellPromptStart = new Regex(@"^\s*->", RegexOptions.Compiled);
        private static readonly Regex ShellError = new Regex(@"undefined\s+symbol|syntax\s+error", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Command = new Regex(@"^\s*(?:bspBootChange|bootChange)\s*(?:\(\s*\))?\s*;?\s*$", RegexOptions.Compiled);

        // VxWorks boot parametrelerinin alan adları (parantezler hariç, küçük harf)
        private static readonly string[] KnownLabels =
        {
            "boot device", "unit number", "processor number", "host name", "file name", "inet on ethernet",
            "inet on backplane", "host inet", "gateway inet", "user", "ftp password", "flags", "target name",
            "startup script", "other",
        };

        private readonly StringBuilder _tail = new StringBuilder();
        private int _lineNo;                 // tamamlanan satır sayısı (son satırın kimliği için)
        private DateTime _lastDataUtc;
        private bool _awaitingNext = true;   // bir sonraki istem "şu anki alan" olacak
        private int _tailHandledLine = -1;   // bu satır numarasındaki son satır işlendi / gönderimde görülüyordu
        private string _tailHandledText;
        private string _promptText;          // şu anki alanın istem metni (aynı satıra eklenen eko / log ayrılsın)
        private int _promptLine = -1;        // şu anki istemin satır numarası
        private string _lastSent;

        public BootChangeParser()
        {
            Fields = new List<BootField>();
            Logs = new List<string>();
            CurrentIndex = -1;
            Phase = BootChangePhase.Starting;
        }

        public List<BootField> Fields { get; private set; }
        /// <summary>Komut sürerken alan istemleri dışında gelen satırlar (kartın logları).</summary>
        public List<string> Logs { get; private set; }
        /// <summary>Kartın şu an sorduğu (ya da en son sorduğu) alan; yoksa -1.</summary>
        public int CurrentIndex { get; private set; }
        public BootChangePhase Phase { get; private set; }
        public string FailMessage { get; private set; }
        /// <summary>Her değişiklikte artar (arayüz yalnız değişince yenilenir).</summary>
        public int Version { get; private set; }
        /// <summary>Her yeni istemde artar (otomatik adımlar her istemde bir kez gönderilir).</summary>
        public int PromptSerial { get; private set; }

        public BootField Current { get { return CurrentIndex >= 0 && CurrentIndex < Fields.Count ? Fields[CurrentIndex] : null; } }

        /// <summary>Kart şu an bir alan için giriş bekliyor (ve bu alana henüz bir şey gönderilmedi).</summary>
        public bool AwaitingInput { get { return Phase == BootChangePhase.Prompting && Current != null && !_awaitingNext; } }

        public bool IsOver { get { return Phase == BootChangePhase.Finished || Phase == BootChangePhase.Failed; } }

        /// <summary>Komut bspBootChange ya da bootChange mi? (VxWorks sembolleri büyük / küçük harfe duyarlıdır.)</summary>
        public static bool IsBootChangeCommand(string cmd)
        {
            return cmd != null && Command.IsMatch(cmd);
        }

        // ---------- Veri ----------

        public void Feed(string text, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(text) || IsOver) return;
            _lastDataUtc = nowUtc;
            foreach (char ch in text)
            {
                if (ch == '\n')
                {
                    string line = _tail.ToString();
                    _tail.Clear();
                    int number = _lineNo++;
                    CompleteLine(line.Replace("\t", " "), number);
                    if (IsOver) return;
                }
                else if (ch != '\r') _tail.Append(ch);
            }
        }

        /// <summary>Düzenli çağrılır: veri kısa süredir kesildiyse son satır (bekleyen istem) değerlendirilir.</summary>
        public void Tick(DateTime nowUtc)
        {
            if (IsOver || _tail.Length == 0) return;
            if (nowUtc - _lastDataUtc < TailSettle) return;
            string tail = _tail.ToString().Replace("\t", " ");
            if (_tailHandledLine == _lineNo && _tailHandledText == tail) return;
            _tailHandledLine = _lineNo;
            _tailHandledText = tail;
            ExamineTail(tail);
        }

        /// <summary>Kullanıcı şu anki alana bir şey gönderdi ("" = değiştirmeden geç, "." = temizle, "-" = önceki alan).</summary>
        public void MarkSent(string text)
        {
            BootField f = Current;
            if (f != null && text != PreviousToken && text.Length > 0) f.Entered = text;
            _lastSent = text;
            _awaitingNext = true;
            // Gönderim anında görünen istem, kart cevap verene kadar "yeni istem" sayılmasın
            _tailHandledLine = _lineNo;
            _tailHandledText = _tail.ToString().Replace("\t", " ");
            Changed();
        }

        public void Fail(string message)
        {
            if (IsOver) return;
            Phase = BootChangePhase.Failed;
            FailMessage = message;
            Changed();
        }

        // ---------- Satırlar ----------

        private void CompleteLine(string line, int number)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) return;

            if (IsHeader(trimmed))
            {
                if (Phase == BootChangePhase.Starting) { Phase = BootChangePhase.Prompting; Changed(); }
                return;
            }

            if (Phase == BootChangePhase.Starting)
            {
                if (ShellError.IsMatch(trimmed)) { Fail("Kart komutu çalıştırmadı: " + trimmed); return; }
                if (ValueLine.IsMatch(trimmed) && Fields.Count == 0) { Finish(); return; }
                string l0, r0;
                if (!MatchField(line, out l0, out r0)) return;   // komut ekosu vb.
                Phase = BootChangePhase.Prompting;
            }

            // Komut bitti: dönüş değeri ya da kabuk istemi
            if (ValueLine.IsMatch(trimmed) || ShellPromptStart.IsMatch(trimmed)) { Finish(); return; }

            // Şu anki istemin satırı tamamlandı (kullanıcı girişi ya da araya giren log): sonuna eklenen eko ve loglar ayrılır
            if (number == _promptLine)
            {
                if (_promptText != null && line.StartsWith(_promptText, StringComparison.Ordinal))
                    AddLog(StripEcho(line.Substring(_promptText.Length)));
                return;
            }

            string label, rest;
            if (MatchField(line, out label, out rest))
            {
                // İstem, son satır değerlendirilemeden aynı satıra gelen bir logla tamamlandı: değer ilk sözcüktür
                string value = FirstToken(rest);
                if (_awaitingNext)
                {
                    SetCurrent(label, value, line.Substring(0, line.Length - rest.Length) + (value.Length > 0 ? value + " " : ""), number);
                    string extra = rest.Trim();
                    if (extra.Length > value.Length) AddLog(extra.Substring(value.Length).Trim());
                }
                else
                {
                    // Kart, bizim göndermediğimiz bir girişle ilerlemiş (ör. terminalden yazıldı)
                    Register(label, value);
                    _awaitingNext = true;
                    Changed();
                }
                return;
            }

            // Kullanıcının girişinin ekosu (istem satırı logla kapandıysa ayrı satırda gelir)
            if (_lastSent != null && trimmed == _lastSent.Trim()) return;
            AddLog(line.TrimEnd());
        }

        private void ExamineTail(string tail)
        {
            if (Phase == BootChangePhase.Prompting && ShellPromptOnly.IsMatch(tail)) { Finish(); return; }
            // İstem her zaman boşlukla biter ("alan : " ya da "alan : değer "); yarım gelmiş istem beklenir
            if (!tail.EndsWith(" ") && !tail.EndsWith(":")) return;

            string label, rest;
            if (!MatchField(tail, out label, out rest)) return;
            if (Phase == BootChangePhase.Starting) Phase = BootChangePhase.Prompting;

            if (!_awaitingNext && _lineNo == _promptLine) return;   // aynı istemin sonuna eko / log eklenmiş

            SetCurrent(label, rest.Trim(), tail, _lineNo);
        }

        private void SetCurrent(string label, string value, string promptText, int line)
        {
            CurrentIndex = Register(label, value);
            _promptText = promptText;
            _promptLine = line;
            _awaitingNext = false;
            _lastSent = null;
            PromptSerial++;
            Changed();
        }

        private int Register(string label, string value)
        {
            int i = Fields.FindIndex(f => SameLabel(f.Label, label));
            if (i < 0)
            {
                Fields.Add(new BootField { Label = label.Trim(), OriginalValue = value, CardValue = value });
                return Fields.Count - 1;
            }
            BootField f0 = Fields[i];
            f0.CardValue = value;
            // "-" ile geri dönülen alan: kart girilen değeri gösteriyorsa "değişti" işareti kalır, göstermiyorsa (kart kabul etmedi) kalkar
            if (f0.Entered != null && f0.EffectiveValue != value) f0.Entered = null;
            return i;
        }

        private void Finish()
        {
            if (Phase == BootChangePhase.Starting && Fields.Count == 0)
            {
                Phase = BootChangePhase.Failed;
                FailMessage = "Kart hiçbir boot alanı sormadan komutu bitirdi.";
            }
            else Phase = BootChangePhase.Finished;
            Changed();
        }

        private void AddLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Logs.Add(line.TrimEnd());
            if (Logs.Count > 2000) Logs.RemoveRange(0, Logs.Count - 2000);
            Changed();
        }

        private string StripEcho(string extra)
        {
            if (_lastSent != null && _lastSent.Length > 0 && extra.StartsWith(_lastSent, StringComparison.Ordinal))
                return extra.Substring(_lastSent.Length);
            return extra;
        }

        private void Changed() { Version++; }

        // ---------- Tanıma ----------

        private static bool IsHeader(string line)
        {
            return line.Contains("clear field") || line.Contains("previous field") || line.Contains("= quit");
        }

        /// <summary>
        /// Satır bir boot alanı istemi mi? Loglardaki "görev: mesaj" satırlarıyla karışmasın diye yalnız şu üç durumda kabul edilir:
        /// bilinen VxWorks alan adı, parantez içinde kısa anahtar ("(h)", "(pw)") ya da hizalama boşluğu ("boot device     :").
        /// </summary>
        public static bool MatchField(string line, out string label, out string rest)
        {
            label = rest = null;
            if (line == null) return false;
            Match m = FieldLine.Match(line.TrimStart());
            if (!m.Success) return false;
            string l = m.Groups["label"].Value.Trim();
            bool padded = m.Groups["pad"].Value.Length >= 2;
            bool keyed = KeyParen.IsMatch(l);
            string norm = Spaces.Replace(Parens.Replace(l, " "), " ").Trim().ToLowerInvariant();
            bool known = KnownLabels.Contains(norm);
            if (!padded && !keyed && !known) return false;
            label = l;
            rest = m.Groups["rest"].Value;
            return true;
        }

        private static bool SameLabel(string a, string b)
        {
            return string.Equals(Spaces.Replace(a ?? "", " ").Trim(), Spaces.Replace(b ?? "", " ").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Logla birleşmiş istem satırında alanın değeri: ilk sözcük. ":" ile biten ilk sözcük bir log etiketidir
        /// ("usbHub: …") — o durumda alan boştur.
        /// </summary>
        private static string FirstToken(string s)
        {
            s = (s ?? "").Trim();
            int i = s.IndexOf(' ');
            string first = i < 0 ? s : s.Substring(0, i);
            return first.EndsWith(":") ? "" : first;
        }
    }
}
