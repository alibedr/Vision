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
        /// <summary>Karttan tek seferde gelen veri parçası ve geliş zamanı.</summary>
        public struct Chunk
        {
            public string Text;
            public DateTime Utc;
        }

        private readonly CardSession _session;
        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly object _lock = new object();
        private int _length;
        private bool _closed;

        internal TerminalTap(CardSession session) { _session = session; }

        internal void Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            lock (_lock)
            {
                if (_closed) return;
                _chunks.Add(new Chunk { Text = text, Utc = DateTime.UtcNow });
                _length += text.Length;
                // okunmazsa bellek şişmesin
                while (_length > 1000000 && _chunks.Count > 1) { _length -= _chunks[0].Text.Length; _chunks.RemoveAt(0); }
            }
        }

        /// <summary>
        /// Son çağrıdan bu yana gelen parçalar, geldikleri gibi (yoksa null). Parça sınırları korunur: kartın istemi
        /// ile ardından gelen log ayrı parçalarda gelir; ayrıştırıcı istemi ilk parçadan okur.
        /// </summary>
        public List<Chunk> Take()
        {
            lock (_lock)
            {
                if (_chunks.Count == 0) return null;
                var list = new List<Chunk>(_chunks);
                _chunks.Clear();
                _length = 0;
                return list;
            }
        }

        public void Dispose()
        {
            lock (_lock) { _closed = true; _chunks.Clear(); _length = 0; }
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
        /// <summary>
        /// Kartın gösterdiği değer güvenle okunamadı (istemle aynı anda araya log girdi). Değer yerine "okunamadı"
        /// gösterilir; yanlış bir değer gösterip "kartın ayarı bu mu?" diye korkutmasın (G27.3).
        /// </summary>
        public bool Uncertain { get; set; }

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
    /// İstem, kartın onu gönderdiği veri parçasından okunur (parça sınırları korunur): istemden SONRA gelen parçalar
    /// (araya giren loglar) alanın değeri sayılmaz. Boş alanın değeri ayrı parçada gelebileceği için, istemden kısa süre
    /// içinde gelen tek sözcüklük parça değer olarak kabul edilir. Emin olunamayan değer "okunamadı" olarak işaretlenir.
    /// </summary>
    public sealed class BootChangeParser
    {
        /// <summary>VxWorks bootChange: "." alanı temizler, "-" önceki alana döner, Ctrl+D çıkar.</summary>
        public const string ClearToken = ".";
        public const string PreviousToken = "-";
        public const string QuitChar = "\x04";

        /// <summary>Son satır bu kadar süre değişmezse (kart girişi bekliyor) değerlendirilir.</summary>
        public static readonly TimeSpan TailSettle = TimeSpan.FromMilliseconds(120);
        /// <summary>Boş görünen istemin değeri ayrı parçada gelirse ancak bu süre içinde kabul edilir.</summary>
        public static readonly TimeSpan ValueWindow = TimeSpan.FromMilliseconds(250);
        /// <summary>İstemden bu kadar kısa sürede gelen değer, kartın aynı yazışının devamıdır (kesin kabul edilir).</summary>
        public static readonly TimeSpan SameWriteWindow = TimeSpan.FromMilliseconds(40);
        private static readonly Regex SingleTokenThenSpace = new Regex(@"^\S+ $", RegexOptions.Compiled);

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
        private string _promptText;          // şu anki alanın istem metni (aynı satıra eklenen eko / log ayrılsın)
        private int _promptLine = -1;        // şu anki istemin satır numarası
        private DateTime _promptAtUtc;       // şu anki istemin geldiği an
        private bool _valueOpen;             // istem boş değerle okundu; değer hemen ardından ayrı parçada gelebilir
        private bool _valueFromLateChunk;    // değer istemden sonraki parçadan okundu (ardından log gelirse şüpheli)
        private bool _promptAddedField;      // istem bu alanı ilk kez gösterdi (ilk değeri de güncellenir)
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

        /// <summary>Karttan gelen bir veri parçası (parçalar geldikleri sırayla, ayrı ayrı verilmelidir).</summary>
        public void Feed(string text, DateTime arrivalUtc)
        {
            if (string.IsNullOrEmpty(text) || IsOver) return;
            _lastDataUtc = arrivalUtc;
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
            // Parça bitti: kart istemi bastıysa (satır sonu yok, boşlukla biter) şimdi okunur — sonra gelen loglar karışmasın
            if (_tail.Length > 0) ExamineTail(_tail.ToString().Replace("\t", " "), arrivalUtc, false);
        }

        /// <summary>Düzenli çağrılır: veri kısa süredir kesildiyse kabuk istemi (komut bitti) denetlenir.</summary>
        public void Tick(DateTime nowUtc)
        {
            if (IsOver || _tail.Length == 0) return;
            if (nowUtc - _lastDataUtc < TailSettle) return;
            ExamineTail(_tail.ToString().Replace("\t", " "), _lastDataUtc, true);
        }

        /// <summary>Kullanıcı şu anki alana bir şey gönderdi ("" = değiştirmeden geç, "." = temizle, "-" = önceki alan).</summary>
        public void MarkSent(string text)
        {
            BootField f = Current;
            if (f != null && text != PreviousToken && text.Length > 0) f.Entered = text;
            _lastSent = text;
            _awaitingNext = true;
            _valueOpen = false;
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

            // Şu anki istemin satırı tamamlandı (kullanıcı girişi ya da araya giren log): sonuna eklenen eko ve loglar ayrılır.
            // Değer istemin parçasından okunduğu için sonradan gelen metin değeri değiştirmez.
            if (number == _promptLine)
            {
                if (_promptText != null && line.StartsWith(_promptText, StringComparison.Ordinal))
                {
                    string extra = StripEcho(line.Substring(_promptText.Length));
                    if (extra.Trim().Length > 0 && _valueFromLateChunk && !_awaitingNext && Current != null)
                    {
                        // Ayrı parçada gelen "değer" aslında logun başı olabilir: emin değiliz
                        MarkUncertain(Current);
                    }
                    AddLog(extra);
                }
                _valueOpen = false;
                return;
            }

            string label, rest;
            if (MatchField(line, out label, out rest))
            {
                // İstem, aynı parçada gelen bir logla birlikte tamamlandı (istem tek başına görülemedi).
                // Değer yalnız tek sözcükse kabul edilir; sonrasında başka metin varsa (log) değer okunamadı sayılır.
                string all = rest.Trim();
                bool single = all.IndexOf(' ') < 0 && !all.EndsWith(":");
                string value = single ? all : "";
                if (_awaitingNext)
                {
                    SetCurrent(label, value, line.Substring(0, line.Length - rest.Length) + (value.Length > 0 ? value + " " : ""), number, _lastDataUtc);
                    _valueOpen = false;
                    if (!single && all.Length > 0)
                    {
                        MarkUncertain(Current);
                        AddLog(all);
                    }
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

        private void ExamineTail(string tail, DateTime arrivalUtc, bool settled)
        {
            if (settled && Phase == BootChangePhase.Prompting && ShellPromptOnly.IsMatch(tail)) { Finish(); return; }

            if (_lineNo == _promptLine)
            {
                // Şu anki istemin satırı sürüyor: boş görünen değer hemen ardından ayrı parçada gelmiş olabilir
                // ("alan : " + "değer "). İstemden kısa süre içinde gelen TEK sözcük + boşluk değer kabul edilir;
                // bunun dışında satıra eklenen her şey (eko, log) değeri değiştirmez.
                if (_valueOpen && !_awaitingNext && Current != null && _promptText != null &&
                    arrivalUtc - _promptAtUtc <= ValueWindow && tail.StartsWith(_promptText, StringComparison.Ordinal))
                {
                    string extra = tail.Substring(_promptText.Length);
                    if (SingleTokenThenSpace.IsMatch(extra))
                    {
                        BootField f = Current;
                        string value = extra.Trim();
                        f.CardValue = value;
                        if (_promptAddedField) f.OriginalValue = value;
                        _promptText = tail;
                        _valueOpen = false;
                        _valueFromLateChunk = arrivalUtc - _promptAtUtc > SameWriteWindow;
                        Changed();
                    }
                }
                return;
            }

            // Yeni istem her zaman boşlukla biter ("alan : " ya da "alan : değer "); yarım gelmiş istem beklenir
            if (!tail.EndsWith(" ") && !tail.EndsWith(":")) return;
            string label, rest;
            if (!MatchField(tail, out label, out rest)) return;
            if (Phase == BootChangePhase.Starting) Phase = BootChangePhase.Prompting;

            string v = rest.Trim();
            SetCurrent(label, v, tail, _lineNo, arrivalUtc);
            _valueOpen = v.Length == 0;
        }

        private void SetCurrent(string label, string value, string promptText, int line, DateTime atUtc)
        {
            int before = Fields.Count;
            CurrentIndex = Register(label, value);
            _promptAddedField = Fields.Count > before;
            _promptText = promptText;
            _promptLine = line;
            _promptAtUtc = atUtc;
            _valueFromLateChunk = false;
            _valueOpen = false;
            _awaitingNext = false;
            _lastSent = null;
            PromptSerial++;
            Changed();
        }

        private void MarkUncertain(BootField f)
        {
            if (f == null) return;
            f.Uncertain = true;
            f.CardValue = "";
            if (_promptAddedField) f.OriginalValue = "";
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
            f0.Uncertain = false;   // alan yeniden soruldu ve temiz okundu
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
    }
}
