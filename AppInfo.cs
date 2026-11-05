using System.Reflection;
using System.Runtime.InteropServices;

// Bu bilgiler Vision.exe → sağ tık → Özellikler → Ayrıntılar sekmesinde görünür.
[assembly: AssemblyTitle("Vision")]
[assembly: AssemblyDescription("Vision — kart terminali, çoklu kart izleme ve kart dosya gezgini")]
[assembly: AssemblyCompany("Ali Bedir")]
[assembly: AssemblyProduct("Vision")]
[assembly: AssemblyCopyright("© 2026 Ali Bedir")]
[assembly: ComVisible(false)]
// Kullanıcıya görünen sürüm resmî yayına kadar 1.0 kalır. Geliştirme derleme numarası yalnız iç kullanım içindir
// (dosya özelliklerinde ve arayüzde gösterilmez); her yeni geliştirme derlemesinde bir artırılır.
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0")]
[assembly: AssemblyMetadata("GelistirmeDerlemesi", "4")]

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
        /// <summary>Kullanıcıya görünen sürüm. Resmî yayın istenene kadar değiştirilmez.</summary>
        public const string Version = "1.0";

        /// <summary>
        /// İç geliştirme derleme numarası (artan). Arayüzde gösterilmez. Geçmiş: 1 = ilk sürüm (v1.0 etiketi),
        /// 2 = Vision adı (v1.1), 3 = cihaz süzme + simge (v1.2), 4 = kart yönetimi iyileştirmeleri.
        /// </summary>
        internal const int DevBuild = 4;
        public const string Author = "Ali Bedir";
        public const string Year = "2026";

        public static string Signature { get { return Name + " v" + Version + "  ·  Geliştiren: " + Author; } }
        public static string Copyright { get { return "© " + Year + " " + Author + ". Tüm hakları saklıdır."; } }
    }
}
