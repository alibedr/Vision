using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vision
{
    /// <summary>Kullanıcının gönderdiği bir komut ve kabuğun ona verdiği dönüş satırları.</summary>
    public sealed class CommandResponse
    {
        public string Command;
        /// <summary>Komutun nereden gönderildiği: "komut satırı", "hızlı komut", "genel komut", "reboot".</summary>
        public string Source;
        public DateTime SentAtUtc;
        public DateTime? CompletedAtUtc;
        /// <summary>Terminalden ayrılan satırlar (ör. "task spawned: …", "value = 522 = 0x20a").</summary>
        public readonly List<string> Lines = new List<string>();
        /// <summary>Son dönüş değeri satırı ("value = …"); yoksa null.</summary>
        public string ReturnValue;
        /// <summary>Tamamlanma notu (ör. "dönüş değeri yok", "bağlantı kesildi"); normal tamamlanmada null.</summary>
        public string Note;

        public bool Completed { get { return CompletedAtUtc.HasValue; } }
    }

    /// <summary>
    /// Komut dönüş değerlerini canlı terminal akışından ayırır (kart başına bir tane; CardSession içinde).
    ///
    /// Telnet akışında komut cevabıyla zaman uyumsuz loglar karışık gelir; "komuttan sonraki her satır cevaptır"
    /// varsayımı yanlıştır. Bu yüzden yalnız iki koşul birlikte sağlanınca satır ayrılır:
    ///  1) Kullanıcı bu uygulamadan bir komut göndermiş ve kabuk henüz yeni istem ("-> ") basmamış (komut sürüyor);
    ///  2) satır, VxWorks kabuğunun dönüş biçimine TAM uyuyor: "value = N = 0x…", "task spawned: id = …",
    ///     "new symbol "x" added to … symbol table".
    /// Diğer TÜM satırlar (loglar, komutun normal çıktısı, hata mesajları) terminalde kalır; hiçbir satır kaybolmaz.
    /// Ayrılan satırlar komutla eşleştirilir: kabuk komutları sırayla çalıştırır, her yeni istem en eski açık
    /// komutu tamamlar (FIFO). Parça parça gelen satırlar (ör. "val" + "ue = 3 …") birleştirilir; kalıba
    /// benzemeyen yarım satır hiç bekletilmez, benzeyen en çok ~300 ms bekletilir.
    /// </summary>
    public sealed class ResponseSplitter
    {
        public const int MaxEntries = 200;
        private static readonly TimeSpan CarryTimeout = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(120);

        private static readonly Regex ResponseLine = new Regex(
            @"^\s*(value = \S.*|task spawned: id = .*|new symbol "".*"" added to .*symbol table\.?)\s*$", RegexOptions.Compiled);
        private static readonly Regex ValueLine = new Regex(@"^\s*value = \S.*$", RegexOptions.Compiled);
        private static readonly string[] Starts = { "value = ", "task spawned:", "new symbol \"" };

        private readonly LinkedList<CommandResponse> _pending = new LinkedList<CommandResponse>();
        private readonly List<CommandResponse> _log = new List<CommandResponse>();
        private string _carry = "";
        private DateTime _carrySince;

        /// <summary>Her değişiklikte artar (arayüz yalnız değişince yeniden çizer).</summary>
        public int Version { get; private set; }

        public int PendingCount { get { return _pending.Count; } }

        /// <summary>Kullanıcı komut gönderdi (karta gitmeden hemen önce çağrılır).</summary>
        public CommandResponse CommandSent(string command, string source, DateTime nowUtc)
        {
            var r = new CommandResponse { Command = command, Source = source, SentAtUtc = nowUtc };
            _pending.AddLast(r);
            _log.Add(r);
            if (_log.Count > MaxEntries) _log.RemoveRange(0, _log.Count - MaxEntries);
            Version++;
            return r;
        }

        /// <summary>
        /// Gelen metni işler (satır sonları '\n'e normalize edilmiş olmalı). Terminale gidecek metni döndürür;
        /// dönüş satırları ilgili komuta eklenir.
        /// </summary>
        public string Process(string text, DateTime nowUtc)
        {
            ExpireOld(nowUtc);
            if (_pending.Count == 0 && _carry.Length == 0) return text;   // açık komut yok: hiçbir şeye dokunma

            string s = _carry + text;
            _carry = "";
            var output = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                int nl = s.IndexOf('\n', i);
                if (nl < 0)
                {
                    string tail = s.Substring(i);
                    if (IsPrompt(tail)) CompleteOldest(nowUtc, null);
                    if (_pending.Count > 0 && CouldBecomeResponse(tail)) { _carry = tail; _carrySince = nowUtc; }
                    else output.Append(tail);
                    break;
                }
                string line = s.Substring(i, nl - i);
                if (_pending.Count > 0 && ResponseLine.IsMatch(line))
                {
                    CommandResponse head = _pending.First.Value;
                    head.Lines.Add(line.Trim());
                    if (ValueLine.IsMatch(line)) head.ReturnValue = line.Trim();
                    Version++;
                }
                else
                {
                    if (IsPrompt(line)) CompleteOldest(nowUtc, null);   // "-> " ve hemen ardından gelen eko aynı satırda
                    output.Append(line).Append('\n');
                }
                i = nl + 1;
            }
            return output.ToString();
        }

        /// <summary>Bekletilen yarım satır yeterince beklediyse (ya da force) terminale geri verir.</summary>
        public string FlushIfStale(DateTime nowUtc, bool force = false)
        {
            ExpireOld(nowUtc);
            if (_carry.Length == 0) return null;
            if (!force && nowUtc - _carrySince < CarryTimeout) return null;
            string c = _carry;
            _carry = "";
            return c;
        }

        /// <summary>Bağlantı koptu / kesildi: açık komutlar not ile kapanır, bekletilen metin geri verilir.</summary>
        public string Reset(string note, DateTime nowUtc)
        {
            while (_pending.Count > 0) CompleteOldest(nowUtc, note);
            string c = _carry;
            _carry = "";
            return c.Length > 0 ? c : null;
        }

        /// <summary>Son komutlar ve dönüşleri (eskiden yeniye, kopya).</summary>
        public List<CommandResponse> Snapshot()
        {
            return _log.Select(Copy).ToList();
        }

        public void ClearLog()
        {
            _log.RemoveAll(r => r.Completed);
            Version++;
        }

        private void CompleteOldest(DateTime nowUtc, string note)
        {
            if (_pending.Count == 0) return;
            CommandResponse r = _pending.First.Value;
            _pending.RemoveFirst();
            r.CompletedAtUtc = nowUtc;
            r.Note = note ?? (r.Lines.Count == 0 ? "dönüş değeri yok" : null);
            Version++;
        }

        private void ExpireOld(DateTime nowUtc)
        {
            while (_pending.Count > 0 && nowUtc - _pending.First.Value.SentAtUtc > PendingTimeout)
                CompleteOldest(nowUtc, "kabuk istemi görülmedi (zaman aşımı)");
        }

        /// <summary>Kabuk istemi: satır "->" ile başlar.</summary>
        private static bool IsPrompt(string s)
        {
            string t = s.TrimStart();
            return t.StartsWith("->");
        }

        /// <summary>Yarım satır, devamı gelince bir dönüş satırına dönüşebilir mi?</summary>
        private static bool CouldBecomeResponse(string tail)
        {
            string t = tail.TrimStart();
            if (t.Length == 0) return false;
            foreach (string st in Starts)
                if (st.StartsWith(t, StringComparison.Ordinal) || t.StartsWith(st, StringComparison.Ordinal)) return true;
            return false;
        }

        private static CommandResponse Copy(CommandResponse r)
        {
            var c = new CommandResponse
            {
                Command = r.Command, Source = r.Source, SentAtUtc = r.SentAtUtc, CompletedAtUtc = r.CompletedAtUtc,
                ReturnValue = r.ReturnValue, Note = r.Note,
            };
            c.Lines.AddRange(r.Lines);
            return c;
        }
    }
}
