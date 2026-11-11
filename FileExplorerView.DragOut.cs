using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Karttan bilgisayara (G31): kart dosyası / klasörü Windows Gezgini'ne (masaüstü ya da herhangi bir klasör)
    /// sürüklenip bırakılabilir; ayrıca sağ tık "Bilgisayara kaydet…". Yükleme yolunun tersi kullanılır: kartta copy ile
    /// FTP konumuna geçici kopya, FTP'den indirme, geçici kopyanın silinmesi (ikili dosyalar bozulmaz).
    ///
    /// Sürükle-bırak "geciktirilmiş" çalışır: sürükleme başlarken hiçbir şey indirilmez; Windows Gezgini'ne bırakıldığı
    /// AN dosyalar geçici klasöre indirilir (ilerleme penceresi açılır) ve Gezgin onları bırakılan yere kopyalar.
    /// Vision'ın kendi listesine bırakmak eskisi gibi kart içinde taşır / kopyalar; indirme yapılmaz.
    /// </summary>
    public sealed partial class FileExplorerView
    {
        // ---------- Sürükleme verisi ----------

        /// <summary>
        /// Sürüklenen kart öğesi. İç biçim (Vision.FsEntry) her zaman vardır; Windows'a "dosya" (FileDrop) olarak da
        /// görünür ama dosyalar yalnız bırakma anında ve Vision'ın DIŞINDAKİ bir pencereye bırakılınca üretilir.
        /// </summary>
        private sealed class CardDragData : DataObject
        {
            private readonly Func<string[]> _render;
            private bool _rendered;
            private string[] _files;

            public CardDragData(FsEntry entry, Func<string[]> render)
            {
                SetData(InternalDragFormat, entry);
                _render = render;
            }

            /// <summary>Fare bırakıldı (sürükleme döngüsü bunu bildirir); dosyalar ancak o zaman üretilir.</summary>
            public bool Dropping { get; set; }

            public override bool GetDataPresent(string format, bool autoConvert)
            {
                return format == DataFormats.FileDrop || base.GetDataPresent(format, autoConvert);
            }

            public override string[] GetFormats(bool autoConvert)
            {
                return base.GetFormats(autoConvert).Concat(new[] { DataFormats.FileDrop }).Distinct().ToArray();
            }

            public override object GetData(string format, bool autoConvert)
            {
                if (format != DataFormats.FileDrop) return base.GetData(format, autoConvert);
                // Sürüklerken (Gezgin yalnız bakıyor) ya da Vision'ın kendi penceresine bırakılırken indirme yapılmaz
                if (!Dropping || IsOverOwnWindow()) return null;
                if (!_rendered)
                {
                    _rendered = true;
                    try { _files = _render(); }
                    catch (Exception ex) { ErrorText.LogTechnical("karttan sürükle-bırak", ex); _files = null; }
                }
                return _files;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT p);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /// <summary>Fare Vision'ın bir penceresinin üzerinde mi?</summary>
        private static bool IsOverOwnWindow()
        {
            try
            {
                Point c = Cursor.Position;
                IntPtr h = WindowFromPoint(new POINT { X = c.X, Y = c.Y });
                if (h == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                return pid == (uint)Process.GetCurrentProcess().Id;
            }
            catch
            {
                return false;
            }
        }

        private CardDragData _activeDrag;

        private void WireDragOut()
        {
            // Fare bırakılınca sürükleme döngüsü bunu sorar: bırakma anı buradan anlaşılır
            _list.QueryContinueDrag += (s, e) =>
            {
                if (_activeDrag != null && e.Action == DragAction.Drop && !e.EscapePressed) _activeDrag.Dropping = true;
            };
        }

        /// <summary>Listeden sürükleme başladı: kart içi taşıma + Windows'a sürükleme için veri.</summary>
        private void StartCardDrag(FsEntry entry)
        {
            bool canExport = entry.Kind == FsKind.File || entry.Kind == FsKind.Folder || entry.Kind == FsKind.Unknown;
            DataObject data;
            if (canExport)
            {
                _activeDrag = new CardDragData(entry, () => RenderDragOut(entry));
                data = _activeDrag;
            }
            else
            {
                data = new DataObject();
                data.SetData(InternalDragFormat, entry);
            }
            try { _list.DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy); }
            finally { _activeDrag = null; }
        }

        /// <summary>
        /// Windows Gezgini'ne bırakıldı: öğe geçici klasöre indirilir, yolu Gezgin'e verilir (Gezgin bırakılan yere
        /// kopyalar). Bu sırada Gezgin bekler; ilerleme Vision'da gösterilir, İptal ile durdurulabilir.
        /// </summary>
        private string[] RenderDragOut(FsEntry entry)
        {
            if (!CanOperate) return null;
            string dir;
            try { dir = DragOutCopies.NewDir(_card != null ? _card.Ip : null); }
            catch (Exception ex)
            {
                _browser.ShowWarning("Geçici klasör oluşturulamadı: " + ErrorText.Describe(ex));
                return null;
            }
            string result = RunTransferDialog(entry, dir);
            if (result == null) { ReadOnlyCopies.DeleteTree(dir); return null; }
            _browser.ShowNotice("\"" + entry.Name + "\" karttan bilgisayara alındı (Windows Gezgini bırakılan yere kopyalıyor).");
            return new[] { result };
        }

        // ---------- Bilgisayara kaydet (sağ tık) ----------

        private async Task SaveToComputerAsync(FsEntry entry)
        {
            if (!CanOperate || entry == null) return;
            string folder;
            using (var dlg = new FolderBrowserDialog
            {
                Description = "\"" + entry.Name + "\" bilgisayarda hangi klasöre kaydedilsin?",
                ShowNewFolderButton = true,
                SelectedPath = _lastSaveFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                folder = dlg.SelectedPath;
            }
            _lastSaveFolder = folder;
            string target = Path.Combine(folder, ReadOnlyCopies.SafeSegment(entry.Name));
            if (File.Exists(target) || Directory.Exists(target))
            {
                int c = ChoiceDialog.Ask(FindForm(), "Üzerine yaz", "\"" + Path.GetFileName(target) + "\" bu klasörde zaten var.",
                    folder + "\nVar olan " + (Directory.Exists(target) ? "klasördeki aynı adlı dosyaların" : "dosyanın") + " üzerine yazılacak.",
                    new[] { "İptal", "Üzerine yaz" }, -1, 1);
                if (c != 1) return;
            }
            await Task.Yield();
            string result = RunTransferDialog(entry, folder);
            if (result == null) return;
            _browser.ShowNotice("\"" + entry.Name + "\" bilgisayara kaydedildi  ·  " + result);
            try { ReadOnlyCopies.ShowInExplorer(result); }
            catch (Exception ex) { ErrorText.LogTechnical("gezginde göster " + result, ex); }
        }

        private string _lastSaveFolder;

        // ---------- İndirme ----------

        /// <summary>İlerleme penceresiyle indirir; başarılıysa bilgisayardaki yol, değilse (hata / iptal) null.</summary>
        private string RunTransferDialog(FsEntry entry, string targetDir)
        {
            using (var d = new DownloadProgressDialog(entry.Name, (progress, cancelled) => DownloadToAsync(entry, targetDir, progress, cancelled)))
            {
                d.ShowDialog(FindForm());
                return d.Result;
            }
        }

        /// <summary>
        /// Kart öğesini bilgisayardaki klasöre indirir (klasörse alt klasörleriyle). FTP bilgisi bu karttan alınır.
        /// Dönüş: indirilen dosya / klasörün yolu; hata ya da iptalde null (neden Dosya Gezgini şeridinde).
        /// </summary>
        private async Task<string> DownloadToAsync(FsEntry entry, string targetDir, IProgress<string> progress, Func<bool> cancelled)
        {
            FileBrowser b = _browser;
            progress.Report("Kartın FTP bilgisi hazırlanıyor…");
            CardFtpCredentials creds = await EnsureCardFtpAsync(false);
            if (creds == null || b != _browser) return null;
            TransferSettings eff = CardTransfer(creds);
            var ftp = new FtpUploader(eff);
            string top = Path.Combine(targetDir, ReadOnlyCopies.SafeSegment(entry.Name));

            if (entry.Kind != FsKind.Folder)
            {
                progress.Report(entry.Name);
                OpResult r = await b.FetchCopyAsync(entry, top, ftp, eff, false);
                return r.Ok ? top : null;
            }

            progress.Report("Klasör taranıyor…");
            var tree = new FileBrowser.FolderTree();
            OpResult scan = await b.CollectTreeAsync(entry, tree);
            if (!scan.Ok || cancelled()) return null;
            Directory.CreateDirectory(top);
            foreach (string f in tree.Folders) Directory.CreateDirectory(Path.Combine(top, f));
            int i = 0;
            foreach (KeyValuePair<string, FsEntry> f in tree.Files)
            {
                if (cancelled()) return null;
                i++;
                progress.Report(i + " / " + tree.Files.Count + "  ·  " + f.Key);
                OpResult r = await b.FetchCopyAsync(f.Value, Path.Combine(top, f.Key), ftp, eff, false);
                if (!r.Ok) return null;
            }
            return top;
        }

        /// <summary>Karttan indirme ilerlemesi; İptal o anki dosya bitince durdurur.</summary>
        private sealed class DownloadProgressDialog : DarkDialog
        {
            private readonly Func<IProgress<string>, Func<bool>, Task<string>> _work;
            private readonly Label _what = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
            private readonly ProgressBar _bar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            private readonly Button _cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
            private bool _cancelled, _done;

            public DownloadProgressDialog(string name, Func<IProgress<string>, Func<bool>, Task<string>> work)
                : base("Karttan bilgisayara", new Size(460, 190))
            {
                _work = work;
                ControlBox = false;
                var title = Theme.MakeLabel("\"" + name + "\" karttan alınıyor…", Theme.UiBold, Theme.Text);
                title.Location = new Point(20, 18);
                title.MaximumSize = new Size(420, 0);
                _what.AutoSize = false;
                _what.SetBounds(20, 46, 420, 20);
                _what.AutoEllipsis = true;
                _bar.SetBounds(20, 72, 420, 10);
                _cancel.Width = 90;
                _cancel.Location = new Point(ClientSize.Width - 20 - _cancel.Width, ClientSize.Height - FooterHeight + 18);
                _cancel.Click += (s, e) => { _cancelled = true; _cancel.Enabled = false; _what.Text = "İptal ediliyor (o anki dosya bitince)…"; };
                Controls.AddRange(new Control[] { title, _what, _bar, _cancel });
                Shown += async (s, e) => await RunAsync();
                FormClosing += (s, e) => { if (!_done) e.Cancel = true; };   // iş bitmeden kapanmaz (İptal ile durdurulur)
            }

            public string Result { get; private set; }

            private async Task RunAsync()
            {
                var progress = new Progress<string>(t => { if (!_cancelled) _what.Text = t; });
                try { Result = await _work(progress, () => _cancelled); }
                catch (Exception ex) { ErrorText.LogTechnical("karttan indirme", ex); Result = null; }
                if (_cancelled) Result = null;
                _done = true;
                Close();
            }
        }
    }

    /// <summary>
    /// Sürükle-bırak için karttan indirilen geçici kopyalar: %TEMP%\TelnetCards\Surukle\&lt;oturum&gt;\&lt;kart&gt;\&lt;n&gt;\.
    /// Windows Gezgini bunları bırakılan yere kopyalar; kopyalar uygulama kapanırken, kalanlar sonraki açılışta silinir.
    /// </summary>
    public static class DragOutCopies
    {
        public static string Root = Path.Combine(Path.GetTempPath(), AppInfo.DataFolder, "Surukle");
        private static string _sessionDir;
        private static int _counter;

        public static string NewDir(string cardIp)
        {
            if (_sessionDir == null)
                _sessionDir = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Process.GetCurrentProcess().Id);
            string dir = Path.Combine(_sessionDir, ReadOnlyCopies.SafeSegment(cardIp ?? "kart"),
                System.Threading.Interlocked.Increment(ref _counter).ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void CleanupSession()
        {
            if (_sessionDir != null) ReadOnlyCopies.DeleteTree(_sessionDir);
        }

        public static void CleanupStale(TimeSpan olderThan)
        {
            try
            {
                if (!Directory.Exists(Root)) return;
                foreach (string d in Directory.GetDirectories(Root))
                    if (DateTime.Now - Directory.GetCreationTime(d) >= olderThan) ReadOnlyCopies.DeleteTree(d);
            }
            catch { }
        }
    }
}
