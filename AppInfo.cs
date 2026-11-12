using System.Reflection;
using System.Runtime.InteropServices;

// Bu bilgiler Vision.exe → sağ tık → Özellikler → Ayrıntılar sekmesinde görünür.
[assembly: AssemblyTitle("Vision")]
[assembly: AssemblyDescription("Vision — kart terminali, çoklu kart izleme ve kart dosya gezgini")]
[assembly: AssemblyCompany("Ali Bedir")]
[assembly: AssemblyProduct("Vision")]
[assembly: AssemblyCopyright("© 2026 Ali Bedir")]
[assembly: ComVisible(false)]
// Sürümleme: ilk resmî yayın 1.0 (etkileşimli uygulama turu ile). Bundan sonra main'e her birleştirmede küçük sürüm
// bir artırılır (1.1, 1.2, …): aşağıdaki üç öznitelik ve AppInfo.Version birlikte değişir; birleştirmeden sonra
// main derlemesi "vX.Y" etiketiyle kendiliğinden yayınlanır (.github/workflows/build-exe.yml, G38).
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1")]
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
        /// <summary>Kullanıcıya görünen sürüm (main'e her birleştirmede küçük sürüm bir artar).</summary>
        public const string Version = "1.1";

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
