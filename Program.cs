using System;
using System.Windows.Forms;

namespace TelnetCards
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ReadOnlyCopies.CleanupStale(TimeSpan.FromHours(12));   // önceki oturumlardan kalan salt okunur kopyalar
            try { Application.Run(new MainForm()); }
            finally { ReadOnlyCopies.CleanupSession(); }
        }
    }
}
