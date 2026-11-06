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
                Theme.Initialize(ConfigStore.Load(path).Settings.UiTheme);
            }
            catch { Theme.Initialize(ThemeMode.Dark); }
            try { Application.Run(new MainForm()); }
            finally { ReadOnlyCopies.CleanupSession(); }
        }
    }
}
