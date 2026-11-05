using System.Reflection;
using System.Runtime.InteropServices;

// Bu bilgiler Vision.exe → sağ tık → Özellikler → Ayrıntılar sekmesinde görünür.
[assembly: AssemblyTitle("Vision")]
[assembly: AssemblyDescription("Vision — kart terminali, çoklu kart izleme ve kart dosya gezgini")]
[assembly: AssemblyCompany("Ali Bedir")]
[assembly: AssemblyProduct("Vision")]
[assembly: AssemblyCopyright("© 2026 Ali Bedir")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace Vision
{
    /// <summary>Uygulamanın adı, sürümü ve geliştiricisi. İsim değişecekse yalnız burası (ve yukarıdaki öznitelikler) değişir.</summary>
    public static class AppInfo
    {
        public const string Name = "Vision";
        /// <summary>
        /// Ayarların tutulduğu %APPDATA% alt klasörü. Uygulamanın eski adından (Telnet Kart Terminali / TelnetCards) kalır:
        /// isim değişse de sabit tutulur ki önceki ayarlar kaybolmasın.
        /// </summary>
        public const string DataFolder = "TelnetCards";
        public const string Tagline = "Kart terminali, çoklu kart izleme ve kart dosya gezgini";
        public const string Version = "1.2";
        public const string Author = "Ali Bedir";
        public const string Year = "2026";

        public static string Signature { get { return Name + " v" + Version + "  ·  Geliştiren: " + Author; } }
        public static string Copyright { get { return "© " + Year + " " + Author + ". Tüm hakları saklıdır."; } }
    }
}
