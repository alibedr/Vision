using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace VisionTests
{
    /// <summary>Testlerin işaretlendiği öznitelik.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    /// <summary>
    /// Küçük test çalıştırıcı (G48): ek paket gerektirmez. [Test] işaretli tüm yöntemleri çalıştırır (async Task da
    /// olabilir), sonuçları yazar; başarısız test varsa çıkış kodu 1 olur ve GitHub Actions derlemesi kırmızıya döner.
    /// Kullanım: VisionTests.exe [ad-filtresi]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : null;
            var tests = typeof(Program).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                                  .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                                  .Select(m => new { Type = t, Method = m }))
                .Where(x => filter == null || (x.Type.Name + "." + x.Method.Name).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Type.Name).ThenBy(x => x.Method.Name)
                .ToList();

            int failed = 0;
            var total = Stopwatch.StartNew();
            foreach (var t in tests)
            {
                string name = t.Type.Name + "." + t.Method.Name;
                var sw = Stopwatch.StartNew();
                try
                {
                    object target = t.Method.IsStatic ? null : Activator.CreateInstance(t.Type);
                    object r = t.Method.Invoke(target, null);
                    var task = r as Task;
                    if (task != null) task.GetAwaiter().GetResult();
                    Console.WriteLine("  GEÇTİ   " + name + "  (" + sw.ElapsedMilliseconds + " ms)");
                }
                catch (Exception ex)
                {
                    failed++;
                    Exception e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    Console.WriteLine("  KALDI   " + name);
                    Console.WriteLine("          " + e.GetType().Name + ": " + e.Message);
                    if (!(e is CheckFailedException)) Console.WriteLine(e.StackTrace);
                    // GitHub Actions'ta hata olarak işaretlensin
                    Console.WriteLine("::error::Test kaldı: " + name + " — " + e.Message.Replace("\n", " "));
                }
            }
            Console.WriteLine();
            Console.WriteLine(tests.Count + " test, " + (tests.Count - failed) + " geçti, " + failed + " kaldı  (" + total.ElapsedMilliseconds + " ms)");
            return failed == 0 ? 0 : 1;
        }
    }
}
