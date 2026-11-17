using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Kart Dosya Gezgini'nde çoklu seçim (G47): Ctrl+A tümünü, Ctrl+tık tek tek, Shift+tık aralık seçer. Seçili
    /// öğeler birlikte silinir, bilgisayara kaydedilir, karta / kart içinde bir klasöre ve Windows Gezgini'ne
    /// sürüklenir, taşınır / kopyalanır. Yeniden adlandırma, görüntüleme ve birlikte açma tek öğede kalır.
    /// </summary>
    public sealed partial class FileExplorerView
    {
        /// <summary>Seçili öğeler (cihazlar hariç), listedeki sırayla.</summary>
        private List<FsEntry> SelectedEntries
        {
            get
            {
                return _list.SelectedItems.Cast<ListViewItem>()
                            .OrderBy(i => i.Index)
                            .Select(i => i.Tag as FsEntry)
                            .Where(e => e != null && e.Kind != FsKind.Device)
                            .ToList();
            }
        }

        private int SelectedCount { get { return _list.SelectedItems.Count; } }

        private void SelectAll()
        {
            _list.BeginUpdate();
            foreach (ListViewItem it in _list.Items) it.Selected = true;
            _list.EndUpdate();
        }

        /// <summary>Durum çubuğu için seçim özeti ("3 öğe seçili · 12 MB"); birden azsa boş.</summary>
        private string SelectionSummary()
        {
            if (SelectedCount < 2) return "";
            List<FsEntry> sel = SelectedEntries;
            long bytes = sel.Where(e => e.Kind == FsKind.File).Sum(e => e.Size ?? 0);
            int folders = sel.Count(e => e.Kind == FsKind.Folder);
            return SelectedCount + " öğe seçili" + (bytes > 0 ? "  ·  dosyalar " + DeviceSpace.FormatBytes(bytes) : "") +
                   (folders > 0 ? "  ·  " + folders + " klasör" : "");
        }

        private static string NameList(IList<FsEntry> entries, int max)
        {
            return string.Join(", ", entries.Take(max).Select(e => e.Name)) + (entries.Count > max ? " … (+" + (entries.Count - max) + ")" : "");
        }

        // ---------- Sağ tık (çoklu) ----------

        private void BuildMultiItemMenu(List<FsEntry> sel)
        {
            var head = new ToolStripMenuItem(sel.Count + " öğe seçili") { Enabled = false };
            _itemMenu.Items.Add(head);
            _itemMenu.Items.Add(new ToolStripSeparator());
            AddMenu("Bilgisayara kaydet… (" + sel.Count + " öğe)", async () => await SaveManyToComputerAsync(sel), null);
            ToolStripItem mov = AddMenu("Taşı… (" + sel.Count + " öğe)", async () => await TransferManyWithDialogAsync(sel, false), null);
            AddMenu("Kopyala… (" + sel.Count + " öğe)", async () => await TransferManyWithDialogAsync(sel, true), null);
            _itemMenu.Items.Add(new ToolStripSeparator());
            ToolStripItem del = AddMenu("Sil… (" + sel.Count + " öğe)", async () => await DeleteManyAsync(sel), "Delete");
            del.ForeColor = Theme.Danger;
            int locked = sel.Count(FileBrowser.IsProtected);
            if (locked > 0)
            {
                if (locked == sel.Count) mov.Enabled = del.Enabled = false;
                var info = new ToolStripMenuItem(locked + " korumalı öğe (" + FileBrowser.ProtectedName + ") silinmez / taşınmaz, atlanır")
                { Enabled = false, ForeColor = Theme.Warning };
                _itemMenu.Items.Add(info);
            }
            _itemMenu.Items.Add(new ToolStripSeparator());
            var one = new ToolStripMenuItem("Yeniden adlandır, Görüntüle, Birlikte aç: tek öğe seçin") { Enabled = false };
            _itemMenu.Items.Add(one);
            _itemMenu.Items.Add(new ToolStripSeparator());
        }

        // ---------- Silme ----------

        private async Task DeleteManyAsync(List<FsEntry> sel)
        {
            if (!CanOperate || sel.Count == 0) return;
            FileBrowser b = _browser;
            var allowed = sel.Where(e => !FileBrowser.IsProtected(e)).ToList();
            int skipped = sel.Count - allowed.Count;
            if (allowed.Count == 0) { b.RejectProtected(sel[0]); return; }
            string detail = NameList(allowed, 8) + "\nBu işlem geri alınamaz." +
                            (skipped > 0 ? "\n" + skipped + " korumalı öğe (" + FileBrowser.ProtectedName + ") atlanacak." : "");
            bool anyFolder = allowed.Any(e => e.Kind == FsKind.Folder);
            int c;
            if (!anyFolder)
            {
                c = ChoiceDialog.Ask(FindForm(), "Öğeleri sil", allowed.Count + " öğe kalıcı olarak silinsin mi?", detail,
                                     new[] { "İptal", "Sil" }, 0, 1);
                if (c == 1) await b.DeleteManyAsync(allowed, false);
                return;
            }
            c = ChoiceDialog.Ask(FindForm(), "Öğeleri sil", allowed.Count + " öğe silinsin mi? (" + allowed.Count(e => e.Kind == FsKind.Folder) + " klasör)",
                detail + "\n\n\"Klasörler yalnız boşsa\" dolu klasörleri silmez (dosyalar silinir).\n" +
                "\"İçindekilerle birlikte\" klasörleri tüm içerikleriyle siler.",
                new[] { "İptal", "Klasörler yalnız boşsa", "İçindekilerle birlikte" }, 1, 2);
            if (c == 1) await b.DeleteManyAsync(allowed, false);
            else if (c == 2) await b.DeleteManyAsync(allowed, true);
        }

        // ---------- Taşı / kopyala (kart içinde) ----------

        private async Task TransferManyWithDialogAsync(List<FsEntry> sel, bool copy)
        {
            if (!CanOperate || sel.Count == 0) return;
            string target = InputDialog.Ask(FindForm(), copy ? "Kopyala" : "Taşı",
                sel.Count + " öğe hangi klasöre " + (copy ? "kopyalansın" : "taşınsın") + "? (kart üzerindeki tam yol)",
                _browser.Current.Path,
                v => string.IsNullOrWhiteSpace(v) ? "Hedef klasör boş olamaz." : v.IndexOf('"') >= 0 ? "Yol tırnak içeremez." : null);
            if (target != null) await InCardTransferManyAsync(sel, target.Trim(), copy);
        }

        /// <summary>Öğeleri sırayla kart içinde taşır / kopyalar; bağlantı koparsa durur. Sonuç şeritte özetlenir.</summary>
        private async Task InCardTransferManyAsync(IList<FsEntry> entries, string destDir, bool copy)
        {
            if (entries.Count == 1) { await InCardTransferAsync(entries[0], destDir, copy); return; }
            FileBrowser b = _browser;
            int ok = 0;
            var failed = new List<string>();
            foreach (FsEntry e in entries)
            {
                if (b != _browser || !CanOperate) { failed.Add(e.Name + " (yapılmadı)"); continue; }
                if (!copy && FileBrowser.IsProtected(e)) { failed.Add(e.Name + " (korumalı)"); continue; }
                int slash = e.FullPath.LastIndexOf('/');
                string parent = slash > 0 ? e.FullPath.Substring(0, slash) : "/";
                if (string.Equals(parent, destDir, StringComparison.OrdinalIgnoreCase) && !copy) { failed.Add(e.Name + " (zaten orada)"); continue; }
                if (e.Kind == FsKind.Folder && (destDir + "/").StartsWith(e.FullPath + "/", StringComparison.OrdinalIgnoreCase))
                { failed.Add(e.Name + " (kendi içine)"); continue; }
                await InCardTransferAsync(e, destDir, copy);
                if (b.Error != null)
                {
                    failed.Add(e.Name);
                    // Bağlantı koptu ya da gereken komut imajda yok: kalanlar denenmez
                    if (b.LastResponse == null || b.LastResponse.Failure != null || b.Error.Contains("imajında")) break;
                }
                else ok++;
            }
            string msg = ok + " öğe " + destDir + " klasörüne " + (copy ? "kopyalandı" : "taşındı") +
                         (failed.Count > 0 ? "; " + failed.Count + " öğe yapılamadı: " + string.Join(", ", failed.Take(6)) + (failed.Count > 6 ? " …" : "") : "") + ".";
            if (failed.Count > 0) b.ShowWarning(msg); else b.ShowNotice(msg);
        }

        // ---------- Bilgisayara kaydet (çoklu) ----------

        private async Task SaveManyToComputerAsync(List<FsEntry> sel)
        {
            if (!CanOperate || sel.Count == 0) return;
            if (sel.Count == 1) { await SaveToComputerAsync(sel[0]); return; }
            string folder;
            using (var dlg = new FolderBrowserDialog
            {
                Description = sel.Count + " öğe bilgisayarda hangi klasöre kaydedilsin?",
                ShowNewFolderButton = true,
                SelectedPath = _lastSaveFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                folder = dlg.SelectedPath;
            }
            _lastSaveFolder = folder;
            var clash = sel.Where(e => File.Exists(Path.Combine(folder, ReadOnlyCopies.SafeSegment(e.Name))) ||
                                       Directory.Exists(Path.Combine(folder, ReadOnlyCopies.SafeSegment(e.Name)))).ToList();
            if (clash.Count > 0)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Üzerine yaz", clash.Count + " öğe bu klasörde zaten var.",
                    folder + "\n" + NameList(clash, 6) + "\nVar olanların üzerine yazılacak.", new[] { "İptal", "Üzerine yaz" }, -1, 1);
                if (c != 1) return;
            }
            await Task.Yield();
            List<string> result = RunTransferDialogMany(sel, folder);
            if (result == null) return;
            _browser.ShowNotice(sel.Count + " öğe bilgisayara kaydedildi  ·  " + folder);
            try { ReadOnlyCopies.ShowInExplorer(result[0]); }
            catch (Exception ex) { ErrorText.LogTechnical("gezginde göster " + folder, ex); }
        }

        /// <summary>Birden çok öğeyi tek ilerleme penceresiyle indirir; başarılıysa yollar, değilse (hata / iptal) null.</summary>
        private List<string> RunTransferDialogMany(IList<FsEntry> entries, string targetDir)
        {
            List<string> paths = null;
            string title = entries.Count == 1 ? entries[0].Name : entries.Count + " öğe";
            using (var d = new DownloadProgressDialog(title, async tp =>
            {
                paths = await DownloadManyToAsync(entries, targetDir, tp);
                return paths != null ? targetDir : null;
            }))
            {
                d.ShowDialog(FindForm());
                return d.Result != null ? paths : null;
            }
        }

        /// <summary>
        /// Öğeleri (dosya / klasör) bilgisayardaki klasöre indirir; toplam boyut önce bulunur (klasörler taranır), yer
        /// denetlenir, sonra hepsi tek ilerlemeyle alınır. Hata ya da iptalde null.
        /// </summary>
        private async Task<List<string>> DownloadManyToAsync(IList<FsEntry> entries, string targetDir, TransferProgress tp)
        {
            FileBrowser b = _browser;
            tp.Note("Kartın FTP bilgisi hazırlanıyor…");
            CardFtpCredentials creds = await EnsureCardFtpAsync(false);
            if (creds == null || b != _browser) return null;
            TransferSettings eff = CardTransfer(creds);
            var ftp = new FtpUploader(eff);

            // Plan: (kart öğesi, bilgisayardaki yol) çiftleri
            var plan = new List<KeyValuePair<FsEntry, string>>();
            var folders = new List<string>();
            var tops = new List<string>();
            foreach (FsEntry e in entries)
            {
                if (tp.CancelRequested) return null;
                string top = Path.Combine(targetDir, ReadOnlyCopies.SafeSegment(e.Name));
                tops.Add(top);
                if (e.Kind != FsKind.Folder) { plan.Add(new KeyValuePair<FsEntry, string>(e, top)); continue; }
                tp.Note("Klasör taranıyor: " + e.Name);
                var tree = new FileBrowser.FolderTree();
                OpResult scan = await b.CollectTreeAsync(e, tree);
                if (!scan.Ok) return null;
                folders.Add(top);
                foreach (string f in tree.Folders) folders.Add(Path.Combine(top, f));
                foreach (KeyValuePair<string, FsEntry> f in tree.Files) plan.Add(new KeyValuePair<FsEntry, string>(f.Value, Path.Combine(top, f.Key)));
            }
            long total = plan.Sum(p => p.Key.Size ?? 0);
            if (!EnoughLocalSpace(targetDir, total, entries.Count + " öğe")) return null;
            tp.Begin(total, plan.Count);
            foreach (string f in folders) Directory.CreateDirectory(f);
            int i = 0;
            foreach (KeyValuePair<FsEntry, string> p in plan)
            {
                if (tp.CancelRequested) return null;
                i++;
                tp.BeginItem(i + " / " + plan.Count + "  ·  " + p.Key.FullPath);
                OpResult r = await b.FetchCopyAsync(p.Key, p.Value, ftp, eff, false, tp);
                tp.CompleteItem(p.Key.Size ?? 0);
                if (!r.Ok) return null;
            }
            return tops;
        }

        // ---------- Sürükleme yardımcıları ----------

        /// <summary>Sürüklenen kart öğeleri (tek öğe ya da çoklu seçim).</summary>
        private static FsEntry[] DraggedEntries(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(InternalDragFormat)) return null;
            object o = data.GetData(InternalDragFormat);
            var many = o as FsEntry[];
            if (many != null) return many;
            var one = o as FsEntry;
            return one != null ? new[] { one } : null;
        }

        /// <summary>Öğeler bu klasöre bırakılabilir mi (klasörün kendisi ya da bir alt klasörü değil)?</summary>
        private static bool CanDropInto(FsEntry[] dragged, FsEntry folder)
        {
            foreach (FsEntry d in dragged)
            {
                if (d == folder || string.Equals(d.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase)) return false;
                if ((folder.FullPath + "/").StartsWith(d.FullPath + "/", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
    }
}
