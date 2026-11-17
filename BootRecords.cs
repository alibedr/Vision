using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Vision
{
    // =====================================================================
    // Boot ayarı kayıtları (G51): kartın boot ayarlarını adıyla saklama, karşılaştırma, geri yükleme
    // =====================================================================

    /// <summary>Kayıttaki tek boot alanı.</summary>
    public sealed class BootRecordField
    {
        [XmlAttribute("Ad")] public string Label { get; set; }
        [XmlAttribute("Deger")] public string Value { get; set; }
    }

    /// <summary>Kullanıcının adıyla kaydettiği boot ayarları (kart başına en fazla <see cref="BootRecordLogic.MaxPerCard"/>).</summary>
    public sealed class BootRecord
    {
        public BootRecord() { Fields = new List<BootRecordField>(); }

        [XmlAttribute("Ad")] public string Name { get; set; }
        [XmlAttribute("Tarih")] public DateTime Created { get; set; }
        /// <summary>Kayıt hangi komutla okundu (bspBootChange / bootChange); geri yüklemede aynısı kullanılır.</summary>
        [XmlAttribute("Komut")] public string Command { get; set; }
        /// <summary>Kaydın alındığı kartın IP'si (başka karta uygulanırken gösterilir).</summary>
        [XmlAttribute("Kart")] public string SourceIp { get; set; }
        [XmlElement("Alan")] public List<BootRecordField> Fields { get; set; }

        public string ValueOf(string label)
        {
            string key = BootRecordLogic.LabelKey(label);
            BootRecordField f = Fields.FirstOrDefault(x => BootRecordLogic.LabelKey(x.Label) == key);
            return f != null ? (f.Value ?? "") : null;
        }
    }

    /// <summary>Karşılaştırma / geri yükleme satırının durumu.</summary>
    public enum BootPlanKind
    {
        /// <summary>Kayıttaki ile karttaki aynı: dokunulmaz.</summary>
        Same,
        /// <summary>Kayıttaki değer yazılacak.</summary>
        Change,
        /// <summary>Kayıtta boş: alan temizlenecek (".").</summary>
        Clear,
        /// <summary>Uygulanmaz (başka karta uygulanırken kartın kendi adresi).</summary>
        Excluded,
        /// <summary>Kayıtta var, kart bu alanı sormadı.</summary>
        MissingOnCard,
        /// <summary>Kart soruyor, kayıtta yok: dokunulmaz.</summary>
        NotInRecord,
    }

    public sealed class BootPlanRow
    {
        public string Label;
        /// <summary>Karttaki değer (bilinmiyorsa null).</summary>
        public string Current;
        /// <summary>Kayıttaki değer (kayıtta yoksa null).</summary>
        public string Saved;
        public BootPlanKind Kind;
        /// <summary>Uygulanmama nedeni (Excluded için).</summary>
        public string Note;

        public bool WillWrite { get { return Kind == BootPlanKind.Change || Kind == BootPlanKind.Clear; } }
    }

    public static class BootRecordLogic
    {
        public const int MaxPerCard = 5;
        public const int MaxNameLength = 40;
        public static readonly string[] Commands = { "bspBootChange", "bootChange" };

        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex Parens = new Regex(@"\([^()]*\)", RegexOptions.Compiled);

        /// <summary>Alan adının karşılaştırma anahtarı (boşluklar sadeleşir, küçük harf).</summary>
        public static string LabelKey(string label)
        {
            return Spaces.Replace(label ?? "", " ").Trim().ToLowerInvariant();
        }

        private static string Bare(string label)
        {
            return Spaces.Replace(Parens.Replace(label ?? "", " "), " ").Trim().ToLowerInvariant();
        }

        /// <summary>Kartın kendi IP adresi alanları (inet on ethernet (e), inet on backplane (b)): başka karta uygulanmaz.</summary>
        public static bool IsCardAddress(string label)
        {
            string b = Bare(label);
            return b.StartsWith("inet on ethernet") || b.StartsWith("inet on backplane");
        }

        /// <summary>"bspBootChange()" / "bootChange ;" gibi yazımları komut adına indirger.</summary>
        public static string NormalizeCommand(string cmd)
        {
            string c = (cmd ?? "").Trim().TrimEnd(';', ' ');
            int p = c.IndexOf('(');
            if (p >= 0) c = c.Substring(0, p);
            c = c.Trim();
            return Commands.FirstOrDefault(x => x == c) ?? c;
        }

        public static string ValidateName(string name, IEnumerable<BootRecord> existing, BootRecord self)
        {
            string n = (name ?? "").Trim();
            if (n.Length == 0) return "Kayda bir ad verin.";
            if (n.Length > MaxNameLength) return "Ad en fazla " + MaxNameLength + " karakter olabilir.";
            foreach (char c in n) if (c < 32) return "Ad tek satır olmalı.";
            if (existing != null && existing.Any(r => r != self && string.Compare((r.Name ?? "").Trim(), n, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), System.Globalization.CompareOptions.IgnoreCase) == 0))
                return "Bu kartta \"" + n + "\" adında bir kayıt zaten var.";
            return null;
        }

        /// <summary>Okunan alanlardan kayıt oluşturur: kartın sorduğu her alan, FTP parolası dahil, olduğu gibi yazılır.</summary>
        public static BootRecord FromFields(string name, string command, string sourceIp, IEnumerable<BootField> fields, DateTime now)
        {
            var r = new BootRecord { Name = (name ?? "").Trim(), Command = command, SourceIp = sourceIp, Created = now };
            foreach (BootField f in fields)
            {
                r.Fields.Add(new BootRecordField { Label = f.Label.Trim(), Value = (f.EffectiveValue ?? "").Trim() });
            }
            return r;
        }

        /// <summary>
        /// Kayıt ile kartın şu anki alanlarını karşılaştırır. <paramref name="current"/> null ise kart okunmamıştır
        /// (yalnız kayıt gösterilir). <paramref name="otherCard"/>: kayıt başka bir kartın — adres alanları uygulanmaz.
        /// </summary>
        public static List<BootPlanRow> Plan(BootRecord record, IList<BootField> current, bool otherCard)
        {
            var rows = new List<BootPlanRow>();
            var used = new HashSet<string>();
            if (current != null)
            {
                foreach (BootField f in current)
                {
                    string key = LabelKey(f.Label);
                    used.Add(key);
                    string cur = f.Uncertain ? null : (f.EffectiveValue ?? "").Trim();
                    string saved = record.ValueOf(f.Label);
                    rows.Add(Row(f.Label, cur, saved, otherCard, true));
                }
            }
            foreach (BootRecordField rf in record.Fields)
            {
                if (used.Contains(LabelKey(rf.Label))) continue;
                rows.Add(Row(rf.Label, null, rf.Value ?? "", otherCard, current == null));
            }
            return rows;
        }

        private static BootPlanRow Row(string label, string cur, string saved, bool otherCard, bool onCard)
        {
            var row = new BootPlanRow { Label = label.Trim(), Current = cur, Saved = saved };
            if (saved == null) row.Kind = BootPlanKind.NotInRecord;
            else if (otherCard && IsCardAddress(label)) { row.Kind = BootPlanKind.Excluded; row.Note = "kartın kendi adresi, başka karta uygulanmaz"; }
            else if (!onCard) row.Kind = BootPlanKind.MissingOnCard;
            else if (cur != null && cur == saved.Trim()) row.Kind = BootPlanKind.Same;
            else row.Kind = saved.Trim().Length == 0 ? BootPlanKind.Clear : BootPlanKind.Change;
            return row;
        }

        /// <summary>
        /// Geri yüklemede kartın sorduğu alana ne gönderileceği: "" değiştirmeden geç, "." temizle, değilse yeni değer.
        /// Kararı kartın o an gösterdiği değere göre verir (önizlemeden sonra kart değişmiş olsa da doğru yazar).
        /// </summary>
        public static string AnswerFor(BootRecord record, BootField field, bool otherCard)
        {
            if (otherCard && IsCardAddress(field.Label)) return "";
            string saved = record.ValueOf(field.Label);
            if (saved == null) return "";
            saved = saved.Trim();
            if (saved == BootChangeParser.ClearToken || saved == BootChangeParser.PreviousToken) return "";   // kartın girişiyle yazılamaz
            string cur = field.Uncertain ? null : (field.CardValue ?? "").Trim();
            if (cur != null && cur == saved) return "";
            return saved.Length == 0 ? (cur == "" ? "" : BootChangeParser.ClearToken) : saved;
        }
    }

    // =====================================================================
    // bootChange'i pencere açmadan yürütme
    // =====================================================================

    /// <summary>Boot komutunun yürütüldüğü kart konsolu (gerçekte <see cref="SessionBootConsole"/>, testte sahte kart).</summary>
    public interface IBootConsole
    {
        bool Connected { get; }
        /// <summary>Kartın çıktısını izlemeye başlar; bu sürede başka komut gönderilmez.</summary>
        void Begin();
        void End();
        Task SendCommandAsync(string command);
        Task SendLineAsync(string text);
        Task SendQuitAsync();
        /// <summary>Son çağrıdan bu yana gelen parçalar (yoksa null).</summary>
        List<TerminalTap.Chunk> Take();
    }

    public sealed class BootRunResult
    {
        public bool Ok;
        public string Error;
        public List<BootField> Fields = new List<BootField>();
        /// <summary>Kartın gösterdiği değer güvenle okunamayan alanlar (araya log girdi).</summary>
        public List<string> Uncertain = new List<string>();
    }

    /// <summary>
    /// bspBootChange / bootChange'i kullanıcı yerine yürütür (G51): her istemde <c>answer</c> ne derse gönderilir
    /// ("" değiştirmeden geç). Ayrıştırma Boot ayarları penceresiyle aynıdır (<see cref="BootChangeParser"/>): araya giren
    /// loglar değer sayılmaz. İstem gelince değerin ayrı parçada gelme ihtimaline karşı kısa süre beklenir.
    /// </summary>
    public static class BootChangeRunner
    {
        /// <summary>İstem görüldükten sonra cevap vermeden önce beklenen süre (değer ayrı parçada gelebilir).</summary>
        public static int PromptSettleMs = 300;
        public const int MaxSteps = 80;

        public static async Task<BootRunResult> RunAsync(IBootConsole console, string command, Func<BootField, string> answer,
                                                         CancellationToken token, int idleTimeoutMs = 15000)
        {
            var result = new BootRunResult();
            var p = new BootChangeParser();
            if (!console.Connected) { result.Error = "Kart bağlı değil."; return result; }
            console.Begin();
            try
            {
                try { await console.SendCommandAsync(command).ConfigureAwait(false); }
                catch (Exception ex) { result.Error = ErrorText.Describe(ex); return result; }

                DateTime lastProgress = DateTime.UtcNow;
                int lastVersion = -1, answered = -1, seenSerial = -1, steps = 0;
                DateTime seenAt = DateTime.MinValue;
                while (true)
                {
                    List<TerminalTap.Chunk> chunks = console.Take();
                    if (chunks != null) foreach (TerminalTap.Chunk c in chunks) p.Feed(c.Text, c.Utc);
                    DateTime now = DateTime.UtcNow;
                    p.Tick(now);
                    if (!p.IsOver && !console.Connected) p.Fail("Kartın bağlantısı koptu.");
                    if (p.Version != lastVersion) { lastVersion = p.Version; lastProgress = now; }
                    if (p.IsOver) break;

                    if (token.IsCancellationRequested)
                    {
                        if (p.Phase == BootChangePhase.Prompting) { try { await console.SendQuitAsync().ConfigureAwait(false); } catch { } }
                        p.Fail("İptal edildi.");
                        break;
                    }

                    if (p.AwaitingInput && p.PromptSerial != answered)
                    {
                        if (p.PromptSerial != seenSerial) { seenSerial = p.PromptSerial; seenAt = now; }
                        if ((now - seenAt).TotalMilliseconds >= PromptSettleMs)
                        {
                            if (++steps > MaxSteps) { p.Fail("Çok fazla alan soruldu; durduruldu."); break; }
                            answered = p.PromptSerial;
                            string v = answer != null ? (answer(p.Current) ?? "") : "";
                            p.MarkSent(v);
                            try { await console.SendLineAsync(v).ConfigureAwait(false); }
                            catch (Exception ex) { p.Fail("Gönderilemedi: " + ErrorText.Describe(ex)); break; }
                            lastProgress = now;
                        }
                    }
                    else if ((now - lastProgress).TotalMilliseconds > idleTimeoutMs)
                    {
                        bool prompting = p.Phase == BootChangePhase.Prompting;
                        p.Fail(prompting ? "Kart " + idleTimeoutMs / 1000 + " sn içinde bir sonraki alanı sormadı."
                                         : "Kart " + idleTimeoutMs / 1000 + " sn içinde boot alanlarını sormadı (komut kartta yok ya da kart meşgul).");
                        if (prompting) { try { await console.SendQuitAsync().ConfigureAwait(false); } catch { } }
                        break;
                    }
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }
            finally
            {
                console.End();
            }
            result.Fields = p.Fields;
            result.Uncertain = p.Fields.Where(f => f.Uncertain).Select(f => f.Label).ToList();
            result.Ok = p.Phase == BootChangePhase.Finished;
            if (!result.Ok) result.Error = p.FailMessage ?? "Komut tamamlanamadı.";
            return result;
        }

        /// <summary>
        /// Kartın şu anki boot ayarlarını okur: tüm alanlar değiştirilmeden geçilir (kartta hiçbir şey değişmez). Araya giren
        /// log yüzünden okunamayan alan olursa bir kez daha denenir.
        /// </summary>
        public static async Task<BootRunResult> ReadAsync(IBootConsole console, string command, CancellationToken token)
        {
            BootRunResult r = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                r = await RunAsync(console, command, f => "", token).ConfigureAwait(false);
                if (!r.Ok || r.Uncertain.Count == 0) return r;
            }
            return r;
        }

        /// <summary>
        /// Kaydı karta yazar: yalnız farklı olan alanlar gönderilir (başka karta uygulanırken kartın adres
        /// alanları atlanır), ardından kart yeniden okunup doğrulanır. Doğrulamada tutmayan alanlar <c>mismatches</c>'e yazılır.
        /// </summary>
        /// <param name="command">Kartın boot komutu (bspBootChange / bootChange); null ise kaydın alındığı komut.</param>
        public static async Task<BootApplyResult> ApplyAsync(IBootConsole console, BootRecord record, string command, bool otherCard, CancellationToken token)
        {
            var res = new BootApplyResult();
            if (string.IsNullOrWhiteSpace(command)) command = string.IsNullOrWhiteSpace(record.Command) ? BootRecordLogic.Commands[0] : record.Command;
            BootRunResult w = await RunAsync(console, command, f =>
            {
                string a = BootRecordLogic.AnswerFor(record, f, otherCard);
                if (a.Length > 0) res.Written.Add(f.Label + ": \"" + (f.Uncertain ? "?" : (f.CardValue ?? "").Trim()) + "\" → \"" +
                                                   (a == BootChangeParser.ClearToken ? "" : a) + "\"");
                return a;
            }, token).ConfigureAwait(false);
            res.Write = w;
            if (!w.Ok) { res.Error = w.Error; return res; }

            // Doğrulama: kart yeniden okunur, yazılması gereken her alan kayıttaki değerde mi?
            BootRunResult v = await ReadAsync(console, command, token).ConfigureAwait(false);
            res.Verify = v;
            if (!v.Ok) { res.Error = "Yazıldı ama doğrulama için okunamadı: " + v.Error; return res; }
            foreach (BootPlanRow row in BootRecordLogic.Plan(record, v.Fields, otherCard))
            {
                if (row.Kind == BootPlanKind.Change || row.Kind == BootPlanKind.Clear)
                    res.Mismatches.Add(row.Label + ": kartta \"" + (row.Current ?? "okunamadı") + "\", kayıtta \"" + row.Saved + "\"");
                else if (row.Kind == BootPlanKind.MissingOnCard)
                    res.Missing.Add(row.Label);
            }
            res.Ok = res.Mismatches.Count == 0;
            if (!res.Ok) res.Error = res.Mismatches.Count + " alan kayıttaki değere getirilemedi.";
            return res;
        }
    }

    public sealed class BootApplyResult
    {
        public bool Ok;
        public string Error;
        public BootRunResult Write;
        public BootRunResult Verify;
        /// <summary>Karta yazılan alanlar ("alan: eski → yeni").</summary>
        public List<string> Written = new List<string>();
        public List<string> Mismatches = new List<string>();
        /// <summary>Kayıtta olup kartın sormadığı alanlar.</summary>
        public List<string> Missing = new List<string>();
    }

    /// <summary>Kartın oturumu üzerinden boot komutu: terminal ve komut geçmişi akmaya devam eder, kullanıcı komutları bekletilir.</summary>
    public sealed class SessionBootConsole : IBootConsole
    {
        private readonly CardSession _s;
        private TerminalTap _tap;
        private IDisposable _busy;

        public SessionBootConsole(CardSession session) { _s = session; }

        public bool Connected { get { return _s.State == CardState.Connected; } }

        public void Begin()
        {
            _tap = _s.OpenTap();
            _busy = _s.BeginBootAutomation();
            _s.InteractiveInputActive = true;
        }

        public void End()
        {
            _s.InteractiveInputActive = false;
            if (_busy != null) { _busy.Dispose(); _busy = null; }
            if (_tap != null) { _tap.Dispose(); _tap = null; }
        }

        public Task SendCommandAsync(string command) { return _s.SendAutomationCommandAsync(command, "boot ayarı kaydı"); }
        public Task SendLineAsync(string text) { return _s.SendInputLineAsync(text); }
        public Task SendQuitAsync() { return _s.SendRawAsync(BootChangeParser.QuitChar); }
        public List<TerminalTap.Chunk> Take() { return _tap != null ? _tap.Take() : null; }
    }
}
