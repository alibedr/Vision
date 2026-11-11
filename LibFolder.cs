using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Vision
{
    /// <summary>
    /// Vision'ın yanında gelen DLL'ler exe ile aynı yerde değil, "lib" klasöründe durur (G30.3):
    /// <code>
    /// Vision\
    ///   Vision.exe
    ///   lib\Microsoft.Web.WebView2.Core.dll, Microsoft.Web.WebView2.WinForms.dll
    ///   lib\runtimes\win-x86|win-x64|win-arm64\native\WebView2Loader.dll
    /// </code>
    /// .NET yalnız exe'nin yanına baktığı için yönetilen DLL'ler buradan yüklenir (AssemblyResolve) ve Edge'in yerel
    /// yükleyicisi (WebView2Loader.dll) işlemin mimarisine göre önceden yüklenir. lib klasörü yoksa (ör. Visual Studio'dan
    /// çalıştırılırken DLL'ler exe'nin yanındadır) hiçbir şey değişmez.
    /// Program açılırken, WebView2 türlerine dokunulmadan ÖNCE bir kez çağrılmalıdır.
    /// </summary>
    internal static class LibFolder
    {
        public const string Name = "lib";

        private static string _dir;

        public static void Setup()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string dir = Path.Combine(exeDir, Name);
                if (!Directory.Exists(dir)) return;
                _dir = dir;
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                PreloadWebView2Loader(dir);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("lib klasörü", ex);
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                string path = Path.Combine(_dir, name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);

        /// <summary>
        /// WebView2Loader.dll'yi işlemin mimarisine göre lib\runtimes\…\native'den yükler. Windows aynı adlı DLL'i
        /// tekrar aramaz; WebView2 kitaplığı daha sonra bu yüklenmiş olanı kullanır.
        /// </summary>
        private static void PreloadWebView2Loader(string dir)
        {
            string arch = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant();
            string rid = arch == "ARM64" ? "win-arm64" : Environment.Is64BitProcess ? "win-x64" : "win-x86";
            string path = Path.Combine(dir, "runtimes", rid, "native", "WebView2Loader.dll");
            if (File.Exists(path)) LoadLibrary(path);
        }
    }
}
