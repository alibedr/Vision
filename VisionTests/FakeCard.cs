using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Vision;

namespace VisionTests
{
    /// <summary>
    /// Bellekte çalışan sahte kart (G48): Dosya Gezgini'nin kullandığı VxWorks kabuk komutlarını (open, read, write,
    /// lseek, close, malloc, free, rm, copy, tickGet, dosFsShow) taklit eder. "/host.host/Vision/…" yolları sahte FTP
    /// klasörüdür (kartın FTP sürücüsü gibi: okumada dosya açılınca tamamı alınır, yazmada kapatılınca FTP'ye yazılır).
    /// Hata enjeksiyonu: bağlantı kopması, yanlış okuma sayısı, aktarım ortasında iptal.
    /// </summary>
    public sealed class FakeCard : ITransferHost
    {
        public const string FtpPrefix = "/host.host/Vision";

        public readonly Dictionary<string, byte[]> CardFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, byte[]> FtpFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Commands = new List<string>();
        /// <summary>Hedefi bu metinle biten copy -1 döndürür (kartta kopyalanamayan dosya).</summary>
        public readonly List<string> FailCopyOf = new List<string>();
        /// <summary>Hedefi bu metinle biten copy dosyayı bir bayt eksik yazar (boyut doğrulaması testi).</summary>
        public readonly List<string> TruncateCopyOf = new List<string>();
        public readonly Dictionary<string, string> Cleanups = new Dictionary<string, string>();
        public readonly List<string> Events = new List<string>();
        public int TransfersActive;

        /// <summary>Bu kadar komuttan sonra bağlantı kopar (sonraki her komut başarısız).</summary>
        public int DropAfterCommands = int.MaxValue;
        /// <summary>Bu sıradaki read komutu okuduğundan 1 bayt fazla bildirir (araya giren cevap taklidi).</summary>
        public int LieOnReadNumber = -1;
        /// <summary>Her komuttan sonra çağrılır (ör. belirli bir anda iptal).</summary>
        public Action<string> AfterCommand;

        private sealed class OpenFile
        {
            public string Path;
            public bool Ftp;
            public MemoryStream Data;
            public bool Write;
        }

        private readonly Dictionary<int, OpenFile> _fds = new Dictionary<int, OpenFile>();
        private readonly Dictionary<string, byte[]> _buffers = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private int _nextFd = 3;
        private uint _nextAddr = 0x81000000;
        private int _reads;
        private bool _dropped;

        public int OpenFileCount { get { return _fds.Count; } }
        public int BufferCount { get { return _buffers.Count; } }

        // ---------- ITransferHost ----------

        public Task<ShellResponse> ExecuteAsync(string command, int idleTimeoutSec, bool chained)
        {
            Commands.Add(command);
            ShellResponse r;
            if (_dropped || Commands.Count > DropAfterCommands)
            {
                _dropped = true;
                r = ShellResponse.Failed(command, "Bağlantı kesildi — cevap tamamlanamadı.", "");
            }
            else r = Run(command);
            if (AfterCommand != null) AfterCommand(command);
            return Task.FromResult(r);
        }

        public IDisposable BeginTransfer()
        {
            Interlocked.Increment(ref TransfersActive);
            return new Scope(() => Interlocked.Decrement(ref TransfersActive));
        }

        public void AddCleanup(string key, string command, int order, bool sameBootOnly, uint? bootTick) { Cleanups[key] = command; }
        public void RemoveCleanup(string key) { Cleanups.Remove(key); }
        public void LogEvent(string category, string message) { Events.Add(category + ": " + message); }

        private sealed class Scope : IDisposable
        {
            private Action _a;
            public Scope(Action a) { _a = a; }
            public void Dispose() { Action a = _a; _a = null; if (a != null) a(); }
        }

        // ---------- Komutlar ----------

        private static readonly Regex Str = new Regex("\"((?:\\\\.|[^\"\\\\])*)\"");

        private static string Unquote(string s)
        {
            return Regex.Replace(s, @"\\(.)", "$1");
        }

        private static List<string> Strings(string cmd)
        {
            return Str.Matches(cmd).Cast<Match>().Select(m => Unquote(m.Groups[1].Value)).ToList();
        }

        private static ShellResponse Reply(string cmd, int value, string output = "")
        {
            string raw = cmd + "\n" + (output.Length > 0 ? output.TrimEnd('\n') + "\n" : "") +
                         "value = " + value + " = 0x" + unchecked((uint)value).ToString("x") + "\n-> ";
            return ShellResponse.Parse(cmd, raw);
        }

        private bool IsFtp(string path) { return path.StartsWith(FtpPrefix + "/", StringComparison.OrdinalIgnoreCase); }
        private string FtpName(string path) { return path.Substring(FtpPrefix.Length + 1); }

        private ShellResponse Run(string cmd)
        {
            string c = cmd.Trim();
            List<string> s = Strings(c);
            Match m;

            if (c.StartsWith("open "))
            {
                string path = s[0];
                string rest = c.Substring(c.LastIndexOf('"') + 1);
                int flags = Convert.ToInt32(rest.Split(',')[1].Trim(), rest.Split(',')[1].Trim().StartsWith("0x") ? 16 : 10);
                bool write = (flags & 3) != 0;
                bool ftp = IsFtp(path);
                byte[] existing;
                bool exists = ftp ? FtpFiles.TryGetValue(FtpName(path), out existing) : CardFiles.TryGetValue(path, out existing);
                if (!write && !exists) return Reply(c, -1);
                var ms = new MemoryStream();
                if (!write) { ms.Write(existing, 0, existing.Length); ms.Position = 0; }
                int fd = _nextFd++;
                _fds[fd] = new OpenFile { Path = path, Ftp = ftp, Data = ms, Write = write };
                if (write && !ftp) CardFiles[path] = new byte[0];   // O_CREAT|O_TRUNC
                return Reply(c, fd);
            }
            if ((m = Regex.Match(c, @"^malloc (\d+)$")).Success)
            {
                string addr = "0x" + _nextAddr.ToString("x");
                _buffers[addr] = new byte[int.Parse(m.Groups[1].Value)];
                int v = unchecked((int)_nextAddr);
                _nextAddr += 0x01000000;
                return Reply(c, v);
            }
            if ((m = Regex.Match(c, @"^free (0x[0-9a-f]+)$")).Success)
            {
                _buffers.Remove(m.Groups[1].Value);
                return Reply(c, 0);
            }
            if ((m = Regex.Match(c, @"^read (\d+), (0x[0-9a-f]+), (\d+)$")).Success)
            {
                OpenFile f; byte[] buf;
                if (!_fds.TryGetValue(int.Parse(m.Groups[1].Value), out f) || !_buffers.TryGetValue(m.Groups[2].Value, out buf)) return Reply(c, -1);
                int n = f.Data.Read(buf, 0, int.Parse(m.Groups[3].Value));
                _reads++;
                if (_reads == LieOnReadNumber && n > 0) return Reply(c, n + 1);
                return Reply(c, n);
            }
            if ((m = Regex.Match(c, @"^write (\d+), (0x[0-9a-f]+), (\d+)$")).Success)
            {
                OpenFile f; byte[] buf;
                if (!_fds.TryGetValue(int.Parse(m.Groups[1].Value), out f) || !_buffers.TryGetValue(m.Groups[2].Value, out buf)) return Reply(c, -1);
                int n = int.Parse(m.Groups[3].Value);
                // Gerçek kart ara belleğin ötesini de yazar (bellekteki çöp): taklitte sıfırla doldurulur
                f.Data.Write(buf, 0, Math.Min(n, buf.Length));
                if (n > buf.Length) f.Data.Write(new byte[n - buf.Length], 0, n - buf.Length);
                return Reply(c, n);
            }
            if ((m = Regex.Match(c, @"^lseek (\d+), 0, 1$")).Success)
            {
                OpenFile f;
                if (!_fds.TryGetValue(int.Parse(m.Groups[1].Value), out f)) return Reply(c, -1);
                return Reply(c, unchecked((int)(uint)(f.Data.Position & 0xFFFFFFFF)));
            }
            if ((m = Regex.Match(c, @"^close (\d+)$")).Success)
            {
                OpenFile f;
                int fd = int.Parse(m.Groups[1].Value);
                if (!_fds.TryGetValue(fd, out f)) return Reply(c, -1);
                _fds.Remove(fd);
                if (f.Write)
                {
                    if (f.Ftp) FtpFiles[FtpName(f.Path)] = f.Data.ToArray();   // kartın FTP sürücüsü kapatınca yazar
                    else CardFiles[f.Path] = f.Data.ToArray();
                }
                return Reply(c, 0);
            }
            if (c.StartsWith("rm "))
            {
                bool had = CardFiles.Remove(s[0]);
                return Reply(c, had ? 0 : -1);
            }
            if (c.StartsWith("copy ") && s.Count == 2)
            {
                byte[] data;
                bool ok = IsFtp(s[0]) ? FtpFiles.TryGetValue(FtpName(s[0]), out data) : CardFiles.TryGetValue(s[0], out data);
                if (!ok) return Reply(c, -1);
                if (FailCopyOf.Any(x => s[1].EndsWith(x, StringComparison.OrdinalIgnoreCase))) return Reply(c, -1);
                byte[] copy = data.ToArray();
                if (TruncateCopyOf.Any(x => s[1].EndsWith(x, StringComparison.OrdinalIgnoreCase)) && copy.Length > 0)
                    Array.Resize(ref copy, copy.Length - 1);          // kopya eksik yazıldı (doğrulama testi)
                if (IsFtp(s[1])) FtpFiles[FtpName(s[1])] = copy; else CardFiles[s[1]] = copy;
                return Reply(c, 0);
            }
            if (c == "tickGet") return Reply(c, 123456);
            if (c.StartsWith("dosFsShow"))
                return Reply(c, 0, string.Join("\n", Check.Sample("dosFsShow_ata0a.txt")));
            if (c.StartsWith("ls") || c.StartsWith("ll"))
            {
                string dir = s.Count > 0 ? s[0] : "/";
                var names = CardFiles.Keys.Where(k => k.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase))
                                          .Select(k => "-rwxrwxrwx  1 0       0  " + CardFiles[k].Length.ToString().PadLeft(12) + " Nov 17 10:00 " + k.Substring(dir.Length + 1));
                return Reply(c, 0, string.Join("\n", names));
            }
            return Reply(c, 0);
        }
    }

    /// <summary>Sahte FTP: kartın FTP klasörüyle aynı sözlük (bilgisayar tarafı).</summary>
    public sealed class FakeFtp : IFileUploader
    {
        private readonly FakeCard _card;
        public FakeFtp(FakeCard card) { _card = card; }

        public Task EnsureWorkFolderAsync() { return Task.FromResult(0); }

        public Task UploadFileAsync(string localPath, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken))
        {
            return UploadFileRangeAsync(localPath, 0, new FileInfo(localPath).Length, remoteName, sent, ct);
        }

        public Task UploadFileRangeAsync(string localPath, long offset, long length, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();
            var data = new byte[length];
            using (var f = File.OpenRead(localPath))
            {
                f.Seek(offset, SeekOrigin.Begin);
                int read = 0;
                while (read < length) { int n = f.Read(data, read, (int)(length - read)); if (n <= 0) break; read += n; }
            }
            _card.FtpFiles[remoteName] = data;
            if (sent != null) sent(length);
            return Task.FromResult(0);
        }

        /// <summary>Bu dosyalar (yerel adı bu metinle bitenler) FTP'ye gönderilemez (klasör yükleme testi).</summary>
        public readonly List<string> FailUploadOf = new List<string>();

        public Task UploadDirectoryAsync(string localDir, string remoteName, Action<long> sent = null, CancellationToken ct = default(CancellationToken),
                                         Func<string, bool> include = null, ICollection<KeyValuePair<string, Exception>> failed = null)
        {
            foreach (string f in Directory.GetFiles(localDir, "*", SearchOption.AllDirectories))
            {
                if (include != null && !include(f)) continue;
                if (FailUploadOf.Any(x => f.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
                {
                    var ex = new IOException("dosya başka bir programda açık");
                    if (failed == null) throw ex;
                    failed.Add(new KeyValuePair<string, Exception>(f, ex));
                    continue;
                }
                string rel = f.Substring(localDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                _card.FtpFiles[remoteName + "/" + rel] = File.ReadAllBytes(f);
            }
            return Task.FromResult(0);
        }

        public Task DeleteAsync(string remoteName, bool isDirectory)
        {
            foreach (string k in _card.FtpFiles.Keys.Where(k => k == remoteName || k.StartsWith(remoteName + "/")).ToList()) _card.FtpFiles.Remove(k);
            return Task.FromResult(0);
        }

        public Task<IList<string>> ListNamesAsync() { return Task.FromResult<IList<string>>(_card.FtpFiles.Keys.ToList()); }

        public Task DownloadFileAsync(string remoteName, string localPath, Action<long> got = null, CancellationToken ct = default(CancellationToken), bool append = false)
        {
            ct.ThrowIfCancellationRequested();
            byte[] data = _card.FtpFiles[remoteName];
            using (var f = new FileStream(localPath, append ? FileMode.Append : FileMode.Create)) f.Write(data, 0, data.Length);
            if (got != null) got(data.Length);
            return Task.FromResult(0);
        }

        public Task UploadBytesAsync(byte[] data, string remoteName) { _card.FtpFiles[remoteName] = data; return Task.FromResult(0); }
        public Task TestAsync() { return Task.FromResult(0); }
    }
}
