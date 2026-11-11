using System;
using System.Windows.Forms;

namespace Vision
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ReadOnlyCopies.CleanupStale(TimeSpan.FromHours(12));   // önceki oturumlardan kalan salt okunur kopyalar
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
            finally { ReadOnlyCopies.CleanupSession(); }
        }
    }
}
