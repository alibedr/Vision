using System.Reflection;
using System.Runtime.InteropServices;

// Bu bilgiler TelnetCards.exe → sağ tık → Özellikler → Ayrıntılar sekmesinde görünür.
[assembly: AssemblyTitle("Telnet Kart Terminali")]
[assembly: AssemblyDescription("Kart terminali, çoklu kart izleme ve kart dosya gezgini")]
[assembly: AssemblyCompany("Ali Bedir")]
[assembly: AssemblyProduct("Telnet Kart Terminali")]
[assembly: AssemblyCopyright("© 2026 Ali Bedir")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TelnetCards
{
    /// <summary>Uygulamanın adı, sürümü ve geliştiricisi. İsim değişecekse yalnız burası (ve yukarıdaki öznitelikler) değişir.</summary>
    public static class AppInfo
    {
        public const string Name = "Telnet Kart Terminali";
        /// <summary>Ayarların tutulduğu %APPDATA% alt klasörü (isim değişse de sabit kalır, ayarlar kaybolmaz).</summary>
        public const string DataFolder = "TelnetCards";
        public const string Tagline = "Kart terminali, çoklu kart izleme ve kart dosya gezgini";
        public const string Version = "1.0";
        public const string Author = "Ali Bedir";
        public const string Year = "2026";

        public static string Signature { get { return Name + " v" + Version + "  ·  Geliştiren: " + Author; } }
        public static string Copyright { get { return "© " + Year + " " + Author + ". Tüm hakları saklıdır."; } }
    }
}
