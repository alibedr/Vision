using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TelnetCards
{
    /// <summary>
    /// "Birlikte aç" için kart dosyalarının SALT OKUNUR yerel kopyaları.
    /// Kopyalar %TEMP%\TelnetCards\SaltOkunur\&lt;oturum&gt;\&lt;kart&gt;\&lt;n&gt;\&lt;özgün ad&gt; altında tutulur:
    /// dosya adı değişmez (uygulama ilişkilendirmesi uzantıya göre çalışır), klasör yolu "SaltOkunur" içerir,
    /// dosyaya Windows "Salt okunur" özniteliği verilir (düzenleyiciler kaydetmeyi engeller / uyarır).
    /// Kopyalar uygulama kapanırken, kapanamayanlar bir sonraki açılışta silinir.
    /// </summary>
    public static class ReadOnlyCopies
    {
        public const string FolderName = "SaltOkunur";

        private static string _sessionDir;
        private static int _counter;

        /// <summary>Kök klasör (test için değiştirilebilir).</summary>
        public static string Root = Path.Combine(Path.GetTempPath(), AppInfo.DataFolder, FolderName);

        public static string SessionDir
        {
            get
            {
                if (_sessionDir == null)
                    _sessionDir = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Process.GetCurrentProcess().Id);
                return _sessionDir;
            }
        }

        /// <summary>Yeni kopya için yol üretir (klasörü oluşturur). Aynı dosya ikinci kez açılsa bile çakışmaz.</summary>
        public static string NewPath(string cardIp, string fileName)
        {
            string dir = Path.Combine(SessionDir, SafeSegment(cardIp ?? "kart"),
                System.Threading.Interlocked.Increment(ref _counter).ToString());
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, SafeSegment(fileName));
        }

        /// <summary>Windows dosya adında geçersiz karakterleri "_" yapar.</summary>
        public static string SafeSegment(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            char[] bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name) sb.Append(bad.Contains(c) ? '_' : c);
            string r = sb.ToString().Trim().TrimEnd('.');
            return r.Length == 0 ? "_" : r;
        }

        public static void MarkReadOnly(string path)
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }

        public static bool IsReadOnly(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
        }

        /// <summary>Varsayılan uygulamayla açar. Uzantının ilişkili uygulaması yoksa "Birlikte aç" penceresi gösterilir.</summary>
        public static void OpenDefault(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1155)   // ERROR_NO_ASSOCIATION
            {
                OpenWith(path);
            }
        }

        /// <summary>Windows'un "Birlikte aç" (uygulama seç) penceresini gösterir.</summary>
        public static void OpenWith(string path)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + path) { UseShellExecute = false });
        }

        /// <summary>Kopyanın bulunduğu klasörü Windows Gezgini'nde dosya seçili olarak açar.</summary>
        public static void ShowInExplorer(string path)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = false });
        }

        /// <summary>Bu oturumun kopyalarını siler (açık kalanlar silinemezse sonraki açılışta silinir).</summary>
        public static void CleanupSession()
        {
            if (_sessionDir != null) DeleteTree(_sessionDir);
        }

        /// <summary>Önceki oturumlardan kalan (en az <paramref name="olderThan"/> önce oluşturulmuş) kopyaları siler.</summary>
        public static void CleanupStale(TimeSpan olderThan)
        {
            try
            {
                if (!Directory.Exists(Root)) return;
                foreach (string d in Directory.GetDirectories(Root))
                {
                    if (_sessionDir != null && string.Equals(d.TrimEnd('\\', '/'), _sessionDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;
                    if (DateTime.Now - Directory.GetCreationTime(d) >= olderThan) DeleteTree(d);
                }
            }
            catch { }
        }

        /// <summary>Salt okunur özniteliğini kaldırarak klasörü siler; silinemeyen dosyalar (açık) atlanır.</summary>
        public static void DeleteTree(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                }
                foreach (string d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                {
                    try { Directory.Delete(d); } catch { }
                }
                try { Directory.Delete(dir); } catch { }
            }
            catch { }
        }
    }
}
