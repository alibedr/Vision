using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            InstallCrashHandlers();
            LibFolder.Setup();   // yanında gelen DLL'ler "lib" klasöründen yüklensin (WebView2 türlerine dokunmadan önce)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ReadOnlyCopies.CleanupStale(TimeSpan.FromHours(12));   // önceki oturumlardan kalan salt okunur kopyalar
            DragOutCopies.CleanupStale(TimeSpan.FromHours(12));    // karttan sürüklemede kalan geçici kopyalar
            // Tema, hiçbir pencere / denetim oluşturulmadan önce seçilmeli (denetimler renklerini oluşurken alır)
            try
            {
                bool fallback;
                string path = ConfigStore.ResolvePath(out fallback);
                ConfigStore.MigrateIfMissing(path);
                AppSettings st = ConfigStore.Load(path).Settings;
                st.Normalize();
                Theme.Initialize(st.UiThemeId, st.EffectiveThemeColors());
            }
            catch { Theme.Initialize(ThemeMode.Dark); }
            SentinelView.EnableModernPageMode();   // Sentinel sayfası uygulama içinde IE11 kipinde gösterilsin
            try { Application.Run(new MainForm()); }
            finally { ReadOnlyCopies.CleanupSession(); DragOutCopies.CleanupSession(); }
        }

        // ---------- Beklenmeyen hatalar (G43) ----------

        private static DateTime _lastCrashNoticeUtc = DateTime.MinValue;
        private static bool _showingCrashNotice;

        /// <summary>
        /// Öngörülmemiş hatalar için genel yakalayıcı: hata yığın iziyle logs\\hatalar.log'a yazılır, kullanıcıya Türkçe kısa
        /// bir pencere gösterilir ve (arayüz hatasıysa) Vision çalışmaya devam eder. Önceden .NET'in İngilizce penceresi
        /// çıkıyor ya da Vision sessizce kapanıyor, log'a bir şey yazılmıyordu.
        /// </summary>
        private static void InstallCrashHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => OnUiException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                ErrorText.LogCrash(e.IsTerminating ? "arka plan (Vision kapanıyor)" : "arka plan", ex);
                if (e.IsTerminating)
                {
                    try
                    {
                        MessageBox.Show("Vision beklenmeyen bir hata nedeniyle kapanmak zorunda.\n\n" +
                                        (ex != null ? ex.Message + "\n\n" : "") +
                                        "Ayrıntı şu dosyaya yazıldı:\n" + ErrorText.LogPath,
                                        "Vision", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch { }
                }
            };
            // Beklenmeyen görev hataları: yalnız log'a (Vision'ı kapatmasın)
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                ErrorText.LogCrash("gözlenmeyen görev", e.Exception);
                e.SetObserved();
            };
        }

        private static void OnUiException(Exception ex)
        {
            ErrorText.LogCrash("arayüz", ex);
            // Art arda gelen hatalarda pencere yağmuru olmasın: 10 sn içinde yalnız bir pencere, diğerleri yalnız log'a
            if (_showingCrashNotice || (DateTime.UtcNow - _lastCrashNoticeUtc).TotalSeconds < 10) return;
            _lastCrashNoticeUtc = DateTime.UtcNow;
            _showingCrashNotice = true;
            try
            {
                MessageBox.Show("Vision'da beklenmeyen bir hata oluştu; Vision çalışmaya devam ediyor.\n\n" +
                                (ex != null ? ex.Message + "\n\n" : "") +
                                "Ayrıntı şu dosyaya yazıldı:\n" + ErrorText.LogPath + "\n\n" +
                                "Sorun sürerse Vision'ı yeniden başlatın.",
                                "Vision — beklenmeyen hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
            finally { _showingCrashNotice = false; }
        }
    }
}
