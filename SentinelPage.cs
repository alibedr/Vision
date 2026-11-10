using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Vision
{
    /// <summary>
    /// Sentinel sayfasını uygulama içinde gösteren görüntüleyici. Önce Edge (WebView2, Chromium) kullanılır: sayfadaki
    /// modern JavaScript (veri çekme, düğmeler, kendi yenilemesi) çalışır. Bilgisayarda Edge WebView2 Runtime yoksa
    /// Windows'un eski görüntüleyicisine (WebBrowser, IE11 kipi) dönülür; orada JavaScript sınırlı çalıştığı için sayfa
    /// Vision tarafından düzenli yeniden yüklenir (G28.4).
    /// </summary>
    internal abstract class SentinelPage : IDisposable
    {
        /// <summary>Sayfa yüklenemedi (sunucu henüz hazır değil vb.): görünüm kısa süre sonra yeniden dener.</summary>
        public Action LoadFailed;

        public abstract Control Control { get; }
        public abstract bool IsModern { get; }
        public abstract void Navigate(string url);
        public abstract void Reload();
        public abstract void Blank();
        public virtual void Start() { }
        public abstract void Dispose();

        protected void RaiseLoadFailed()
        {
            Action h = LoadFailed;
            if (h != null) h();
        }
    }

    /// <summary>Edge WebView2 ile görüntüleyici. Başlatılamazsa <see cref="InitFailed"/> çağrılır.</summary>
    internal sealed class EdgeSentinelPage : SentinelPage
    {
        private readonly WebView2 _web = new WebView2 { Dock = DockStyle.Fill, Visible = false };
        private bool _started, _ready;
        private string _pending;

        /// <summary>Edge görüntüleyicisi başlatılamadı (Runtime yok vb.); neden.</summary>
        public Action<Exception> InitFailed;

        public override Control Control { get { return _web; } }
        public override bool IsModern { get { return true; } }

        /// <summary>Denetim forma eklendikten sonra bir kez çağrılır.</summary>
        public override async void Start()
        {
            if (_started) return;
            _started = true;
            try
            {
                // Tarayıcı verileri (önbellek vb.) kullanıcının yerel klasöründe; exe'nin yanına yazılmaz
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                             AppInfo.DataFolder, "WebView2");
                Directory.CreateDirectory(folder);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, folder);
                await _web.EnsureCoreWebView2Async(env);
                CoreWebView2 core = _web.CoreWebView2;
                core.NewWindowRequested += (s, e) => e.Handled = true;   // sayfa dış tarayıcı / yeni pencere açamaz
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDefaultScriptDialogsEnabled = true;
                _web.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) RaiseLoadFailed();
                };
                _ready = true;
                if (_pending != null)
                {
                    string p = _pending;
                    _pending = null;
                    core.Navigate(p);
                }
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("Sentinel Edge WebView2", ex);
                Action<Exception> h = InitFailed;
                if (h != null) h(ex);
            }
        }

        public override void Navigate(string url)
        {
            if (!_ready) { _pending = url; return; }
            try { _web.CoreWebView2.Navigate(url); }
            catch (Exception ex) { ErrorText.LogTechnical("Sentinel " + url, ex); RaiseLoadFailed(); }
        }

        public override void Reload()
        {
            if (!_ready) return;
            try { _web.CoreWebView2.Reload(); } catch (Exception ex) { ErrorText.LogTechnical("Sentinel yenile", ex); }
        }

        public override void Blank()
        {
            _pending = null;
            if (!_ready) return;
            try { _web.CoreWebView2.Navigate("about:blank"); } catch { }
        }

        public override void Dispose()
        {
            try { _web.Dispose(); } catch { }
        }
    }

    /// <summary>Yedek: Windows'un yerleşik görüntüleyicisi (IE11 kipi). Modern JavaScript sınırlıdır.</summary>
    internal sealed class LegacySentinelPage : SentinelPage
    {
        private readonly WebBrowser _browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            ScriptErrorsSuppressed = true,        // sayfadaki betik hataları iletişim kutusu açmasın
            AllowWebBrowserDrop = false,          // sürüklenen dosya sayfanın yerine açılmasın
            IsWebBrowserContextMenuEnabled = true,
            WebBrowserShortcutsEnabled = true,
            Visible = false,
        };
        private string _url;

        public LegacySentinelPage()
        {
            _browser.NewWindow += (s, e) => e.Cancel = true;
            _browser.DocumentCompleted += (s, e) =>
            {
                // Windows'un "sayfa görüntülenemiyor" sayfası
                if (e.Url != null && e.Url.Scheme == "res") RaiseLoadFailed();
            };
        }

        public override Control Control { get { return _browser; } }
        public override bool IsModern { get { return false; } }

        public override void Navigate(string url)
        {
            _url = url;
            try { _browser.Navigate(url, null, null, "Cache-Control: no-cache\r\nPragma: no-cache\r\n"); }
            catch (Exception ex) { ErrorText.LogTechnical("Sentinel " + url, ex); RaiseLoadFailed(); }
        }

        public override void Reload()
        {
            if (_url != null) Navigate(_url);
        }

        public override void Blank()
        {
            _url = null;
            try { _browser.Navigate("about:blank"); } catch { }
        }

        public override void Dispose()
        {
            try { _browser.Dispose(); } catch { }
        }
    }
}
