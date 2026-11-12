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
    /// Kart Dosya Gezgini: dosya / klasör işlemleri (yeni klasör, yeniden adlandır, sil, görüntüle, taşı / kopyala),
    /// bilgisayardan karta yükleme ve sürükle-bırak. Komutlar FileBrowser üzerinden kartın mevcut oturumunda çalışır.
    /// </summary>
    public sealed partial class FileExplorerView
    {
        private const string InternalDragFormat = "Vision.FsEntry";

        private readonly IconButton _newFolderBtn = new IconButton(IconKind.NewFolder, IconTone.Ghost);
        private readonly IconButton _uploadBtn = new IconButton(IconKind.Upload, IconTone.Primary);
        private readonly IconButton _ftpBtn = new IconButton(IconKind.Settings, IconTone.Ghost);
        private readonly IconButton _ftpCopiesBtn = new IconButton(IconKind.Trash, IconTone.Ghost);
        private readonly ContextMenuStrip _itemMenu = new ContextMenuStrip();
        private readonly ToolTip _opTips = new DarkToolTip();

        private void BuildOperationButtons()
        {
            foreach (var b in new[] { _newFolderBtn, _uploadBtn, _ftpCopiesBtn, _ftpBtn }) b.Size = new Size(32, 32);
            _ftpCopiesBtn.Enabled = false;
            _ftpCopiesBtn.SetTip(_opTips, "FTP kopyalarını sil — yükleme sırasında FTP sunucusuna gönderilen kopyalar (yalnız bu kart)");
            _newFolderBtn.SetTip(_opTips, "Yeni klasör — bu klasörde oluşturur (mkdir)  ·  Ctrl+Shift+N");
            _uploadBtn.SetTip(_opTips, "Yükle — bilgisayardan dosya / klasör yükler (FTP + kartta copy / xcopy).\nWindows Gezgini'nden listeye sürükleyip bırakabilirsiniz.");
            _ftpBtn.SetTip(_opTips, "FTP — bu kartın FTP bilgisi (karttan otomatik), bağlantı sınaması ve klasör ayarları");
        }

        private void WireOperations()
        {
            _newFolderBtn.Click += async (s, e) => await NewFolderAsync();
            _uploadBtn.Click += async (s, e) => await UploadWithDialogAsync();
            _ftpBtn.Click += (s, e) => ShowFtpSettings();
            _ftpCopiesBtn.Click += async (s, e) => await DeleteFtpCopiesAsync();

            _itemMenu.Renderer = new DarkMenuRenderer();
            _itemMenu.ShowImageMargin = false;
            _itemMenu.Font = Theme.Ui;
            _itemMenu.Opening += (s, e) => { BuildItemMenu(); e.Cancel = _itemMenu.Items.Count == 0; };
            _list.ContextMenuStrip = _itemMenu;

            // Sürükle-bırak: Windows Gezgini'nden bırakma = yükleme; liste içinde klasöre bırakma = taşı (Ctrl: kopyala)
            _list.AllowDrop = true;
            _list.ItemDrag += List_ItemDrag;
            WireDragOut();
            _list.DragEnter += List_DragOver;
            _list.DragOver += List_DragOver;
            _list.DragLeave += (s, e) => SetDropHighlight(-1);
            _list.DragDrop += List_DragDrop;

            // Boş klasörde liste yerine "Bu klasör boş" katmanı görünür: bırakma oraya da yapılabilmeli
            // (önceden katman bırakmayı kabul etmiyordu → boş klasöre sürükle-bırak çalışmıyordu)
            _overlay.AllowDrop = true;
            _overlay.DragEnter += List_DragOver;
            _overlay.DragOver += List_DragOver;
            _overlay.DragDrop += List_DragDrop;
        }

        private bool CanOperate
        {
            get
            {
                return _browser != null && !_browser.Busy && _card != null &&
                       _sessionOf(_card).State == CardState.Connected && _browser.Loaded;
            }
        }

        private bool InFolder { get { return CanOperate && !_browser.Current.IsRoot; } }

        private void UpdateOperationButtons(bool connected, bool busy)
        {
            bool inFolder = connected && !busy && _browser != null && _browser.Loaded && !_browser.Current.IsRoot;
            if (_newFolderBtn.Enabled != inFolder) _newFolderBtn.Enabled = inFolder;
            if (_uploadBtn.Enabled != inFolder) _uploadBtn.Enabled = inFolder;

            // "FTP kopyalarını sil": bu kartın FTP'de kopyası varsa etkin (yükleme başlar başlamaz)
            CardSession session = _card != null ? _sessionOf(_card) : null;
            int copies = session != null ? session.FtpCopies.Count : 0;
            bool canDelete = copies > 0 && connected && !busy;
            if (_ftpCopiesBtn.Enabled != canDelete) _ftpCopiesBtn.Enabled = canDelete;
            string tip = copies == 0
                ? "FTP kopyalarını sil — bu kartın FTP'de kopyası yok"
                : "FTP kopyalarını sil (" + copies + "): " + string.Join(", ", session.FtpCopies.Items.Select(i => i.Name).Take(8)) + (copies > 8 ? " …" : "") +
                  "\nYüklemede FTP sunucusuna gönderilen kopyalar. Yalnız siz bastığınızda silinir.";
            if (_opTips.GetToolTip(_ftpCopiesBtn) != tip) _ftpCopiesBtn.SetTip(_opTips, tip);
        }



        private FsEntry SelectedEntry
        {
            get { return _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as FsEntry : null; }
        }

        // ---------- Klavye ve çift tık ----------

        /// <summary>Çift tık / Enter: klasör ve cihaz açılır, dosya görüntülenir.</summary>
        private async Task ActivateSelectedAsync()
        {
            FsEntry e = SelectedEntry;
            if (e == null || !CanOperate) return;
            if (e.Kind == FsKind.File) await ViewAsync(e);
            else await _browser.OpenAsync(e);
        }

        private async Task HandleOperationKeyAsync(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; await DeleteSelectedAsync(); }
            else if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                FsEntry sel = SelectedEntry;
                if (sel != null && sel.Kind == FsKind.File && CanOperate) await OpenCopyAsync(sel, e.Shift);
            }
            else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; await RenameSelectedAsync(); }
            else if (e.Control && e.Shift && e.KeyCode == Keys.N) { e.SuppressKeyPress = true; await NewFolderAsync(); }
        }

        // ---------- Sağ tık menüsü ----------

        private void BuildItemMenu()
        {
            _itemMenu.Items.Clear();
            if (!CanOperate) return;
            FsEntry e = SelectedEntry;

            if (e != null && e.Kind != FsKind.Device)
            {
                if (e.Kind == FsKind.File)
                {
                    AddMenu("Görüntüle", async () => await ViewAsync(e), "Enter");
                    AddMenu("Varsayılan uygulamayla aç  (salt okunur)", async () => await OpenCopyAsync(e, false), "Ctrl+Enter");
                    AddMenu("Birlikte aç…  (salt okunur)", async () => await OpenCopyAsync(e, true), "Ctrl+Shift+Enter");
                    _itemMenu.Items.Add(new ToolStripSeparator());
                }
                else AddMenu("Aç", async () => await _browser.OpenAsync(e), "Enter");
                bool locked = FileBrowser.IsProtected(e);   // EFI: kartın açılışı için gerekli, silinemez / taşınamaz
                ToolStripItem ren = AddMenu("Yeniden adlandır…", async () => await RenameSelectedAsync(), "F2");
                ToolStripItem mov = AddMenu("Taşı…", async () => await TransferWithDialogAsync(e, false), null);
                AddMenu("Kopyala…", async () => await TransferWithDialogAsync(e, true), null);
                AddMenu("Bilgisayara kaydet…", async () => await SaveToComputerAsync(e), null);
                _itemMenu.Items.Add(new ToolStripSeparator());
                ToolStripItem del = AddMenu("Sil…", async () => await DeleteSelectedAsync(), "Delete");
                del.ForeColor = Theme.Danger;
                if (locked)
                {
                    ren.Enabled = mov.Enabled = del.Enabled = false;
                    var info = new ToolStripMenuItem("Korumalı: " + FileBrowser.ProtectedName + " kartın açılışı için gerekli — silinemez, taşınamaz")
                    { Enabled = false, ForeColor = Theme.Warning };
                    _itemMenu.Items.Add(info);
                }
                _itemMenu.Items.Add(new ToolStripSeparator());
            }
            else if (e != null && e.Kind == FsKind.Device)
            {
                AddMenu("Aç", async () => await _browser.OpenAsync(e), "Enter");
                _itemMenu.Items.Add(new ToolStripSeparator());
            }

            if (!_browser.Current.IsRoot)
            {
                AddMenu("Yeni klasör…", async () => await NewFolderAsync(), "Ctrl+Shift+N");
                AddMenu("Bilgisayardan yükle…", async () => await UploadWithDialogAsync(), null);
            }
            AddMenu("Yenile", async () => await RefreshCurrentAsync(), "F5");
        }

        private ToolStripItem AddMenu(string text, Func<Task> action, string shortcut)
        {
            var item = new ToolStripMenuItem(text) { ForeColor = Theme.Text, ShortcutKeyDisplayString = shortcut };
            item.Click += async (s, e) => await action();
            _itemMenu.Items.Add(item);
            return item;
        }

        // ---------- İşlemler ----------

        private async Task NewFolderAsync()
        {
            if (!InFolder) return;
            FileBrowser b = _browser;
            string name = InputDialog.Ask(FindForm(), "Yeni klasör", b.Current.Path + " içinde oluşturulacak klasörün adı:", "Yeni klasör",
                v =>
                {
                    string reason;
                    if (!FsPath.IsValidName(v, out reason)) return reason;
                    if (b.Entries.Any(x => string.Equals(x.Name, v.Trim(), StringComparison.OrdinalIgnoreCase))) return "Bu adda bir öğe zaten var.";
                    return null;
                });
            if (name != null) await b.CreateFolderAsync(name);
        }

        private async Task RenameSelectedAsync()
        {
            FsEntry e = SelectedEntry;
            if (e == null || e.Kind == FsKind.Device || !CanOperate) return;
            FileBrowser b = _browser;
            if (FileBrowser.IsProtected(e)) { b.RejectProtected(e); return; }   // EFI: uyarı şeritte, komut gönderilmez
            string name = InputDialog.Ask(FindForm(), "Yeniden adlandır", "\"" + e.Name + "\" için yeni ad:", e.Name,
                v =>
                {
                    string reason;
                    if (!FsPath.IsValidName(v, out reason)) return reason;
                    if (v.Trim() == e.Name) return "Yeni ad eskisiyle aynı.";
                    if (b.Entries.Any(x => x != e && string.Equals(x.Name, v.Trim(), StringComparison.OrdinalIgnoreCase))) return "Bu adda bir öğe zaten var.";
                    return null;
                });
            if (name != null) await b.RenameAsync(e, name);
        }

        private async Task DeleteSelectedAsync()
        {
            FsEntry e = SelectedEntry;
            if (e == null || e.Kind == FsKind.Device || !CanOperate) return;
            FileBrowser b = _browser;
            // EFI korumalı: onay sorulmadan reddedilir (uyarı şeritte görünür, karta komut gönderilmez)
            if (FileBrowser.IsProtected(e)) { b.RejectProtected(e); return; }

            if (e.Kind == FsKind.File)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Dosyayı sil", "\"" + e.Name + "\" kalıcı olarak silinsin mi?",
                    e.FullPath + "\nBu işlem geri alınamaz (rm).", new[] { "İptal", "Sil" }, 0, 1);
                if (c == 1) await b.DeleteAsync(e, false);
                return;
            }
            int choice = ChoiceDialog.Ask(FindForm(), "Klasörü sil", "\"" + e.Name + "\" klasörü silinsin mi?",
                e.FullPath + "\n\n\"Yalnız boşsa sil\" (rmdir) klasör doluysa hiçbir şey silmez.\n" +
                "\"İçindekilerle birlikte sil\" (xdelete) tüm alt klasör ve dosyaları siler; geri alınamaz.",
                new[] { "İptal", "Yalnız boşsa sil", "İçindekilerle birlikte sil" }, 1, 2);
            if (choice == 1) await b.DeleteAsync(e, false);
            else if (choice == 2) await b.DeleteAsync(e, true);
        }

        private async Task ViewAsync(FsEntry e)
        {
            if (!CanOperate) return;
            if (e.Size.HasValue && e.Size.Value > 1024 * 1024)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Büyük dosya", "\"" + e.Name + "\" " + e.SizeText + ". Yine de görüntülensin mi?",
                    "Dosya telnet üzerinden aktarılacağı için biraz sürebilir.", new[] { "İptal", "Görüntüle" }, 1, -1);
                if (c != 1) return;
            }
            OpResult r = await _browser.ReadTextAsync(e);
            if (!r.Ok) return;   // hata / uyarı şeridinde gösterilir
            if (TextViewerDialog.LooksBinary(r.Message))
            {
                int c = ChoiceDialog.Ask(FindForm(), "İkili dosya", "\"" + e.Name + "\" bir metin dosyası gibi görünmüyor.",
                    "İçerik okunaksız görünebilir.", new[] { "İptal", "Yine de göster" }, 1, -1);
                if (c != 1) return;
            }
            using (var v = new TextViewerDialog(e.FullPath, r.Message)) v.ShowDialog(FindForm());
        }

        // ---------- Birlikte aç (salt okunur kopya) ----------

        private bool _readOnlyInfoShownThisRun;

        /// <summary>
        /// Kart dosyasının SALT OKUNUR bir kopyasını bilgisayara alır ve Windows uygulamasıyla açar.
        /// Kart dosyası değişmez; kopyada yapılan değişiklikler karta yansımaz.
        /// </summary>
        /// <param name="chooseApp">true: Windows "Birlikte aç" penceresi (uygulama seç); false: varsayılan uygulama.</param>
        private async Task OpenCopyAsync(FsEntry e, bool chooseApp)
        {
            if (!CanOperate || e == null || e.Kind != FsKind.File) return;
            FileBrowser b = _browser;
            CardConfig card = _card;
            // FTP bilgisi bu karttan alınır (version); alınamazsa metin olarak açma önerilir
            CardFtpCredentials creds = await EnsureCardFtpAsync(false);
            if (card != _card || b != _browser) return;
            bool viaFtp = creds != null;
            if (!viaFtp)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Birlikte aç (salt okunur)",
                    "Kartın FTP bilgisi alınamadı; dosya FTP ile bozulmadan alınamıyor.",
                    (b.Error ?? "") + "\n\n\"Metin olarak aç\" içeriği telnet üzerinden okur: .txt, .xml, .cfg gibi metin dosyalarında çalışır, " +
                    "ikili dosyalar (.bin, .out, .o …) bu yolla BOZULUR.",
                    new[] { "İptal", "Metin olarak aç" }, 1, -1);
                if (c != 1) return;
            }

            long limit = viaFtp ? 50L * 1024 * 1024 : 1024 * 1024;
            if (e.Size.HasValue && e.Size.Value > limit)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Büyük dosya", "\"" + e.Name + "\" " + e.SizeText + ". Yine de açılsın mı?",
                    "Dosya önce bilgisayara kopyalanacağı için biraz sürebilir.", new[] { "İptal", "Aç" }, 1, -1);
                if (c != 1) return;
            }

            string local;
            try { local = ReadOnlyCopies.NewPath(card != null ? card.Ip : null, e.Name); }
            catch (Exception ex)
            {
                b.ShowWarning("Geçici klasör oluşturulamadı: " + ErrorText.Describe(ex));
                return;
            }

            TransferSettings eff = viaFtp ? CardTransfer(creds) : new TransferSettings();
            OpResult r = await b.FetchCopyAsync(e, local, viaFtp ? new FtpUploader(eff) : null, eff, !viaFtp);
            if (!r.Ok) { ReadOnlyCopies.DeleteTree(Path.GetDirectoryName(local)); return; }   // hata şeritte gösterilir

            try { ReadOnlyCopies.MarkReadOnly(local); }
            catch (Exception ex) { ErrorText.LogTechnical("salt okunur öznitelik", ex); }

            if (!_settings.HideReadOnlyOpenInfo && !_readOnlyInfoShownThisRun)
            {
                _readOnlyInfoShownThisRun = true;
                int c = ChoiceDialog.Ask(FindForm(), "Salt okunur kopya",
                    "\"" + e.Name + "\" SALT OKUNUR bir kopya olarak açılacak.",
                    "Açılan dosya, karttaki dosyanın bilgisayardaki bir kopyasıdır. Bu kopyada yaptığınız değişiklikler " +
                    "KARTA YANSIMAZ ve kopya Windows'ta \"Salt okunur\" olarak işaretlidir (düzenleyici kaydetmenize izin vermez).\n\n" +
                    "Kartta bir dosyayı değiştirmek için düzenlediğiniz dosyayı bilgisayarınıza kaydedip \"Yükle\" ile karta gönderin.\n" +
                    "Kopyalar uygulama kapanınca silinir.",
                    new[] { "Bir daha gösterme", "Tamam, aç" }, 1, -1);
                if (c == 0) { _settings.HideReadOnlyOpenInfo = true; if (_saveSettings != null) _saveSettings(); }
                else if (c != 1) { ReadOnlyCopies.DeleteTree(Path.GetDirectoryName(local)); return; }
            }

            try
            {
                if (chooseApp) ReadOnlyCopies.OpenWith(local);
                else ReadOnlyCopies.OpenDefault(local);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("birlikte aç " + local, ex);
                b.ShowWarning("\"" + e.Name + "\" açılamadı: " + ErrorText.Describe(ex) + "  ·  Kopya: " + local);
                return;
            }

            b.ShowNotice("SALT OKUNUR KOPYA açıldı: \"" + e.Name + "\"" + (r.Message == "metin" ? " (metin olarak)" : "") +
                         ". Kopyada yapılan değişiklikler karta yansımaz.  ·  " + local);
        }

        private async Task TransferWithDialogAsync(FsEntry e, bool copy)
        {
            if (!CanOperate) return;
            if (!copy && FileBrowser.IsProtected(e)) { _browser.RejectProtected(e); return; }
            string target = InputDialog.Ask(FindForm(), copy ? "Kopyala" : "Taşı",
                "\"" + e.Name + "\" hangi klasöre " + (copy ? "kopyalansın" : "taşınsın") + "? (kart üzerindeki tam yol)",
                _browser.Current.Path,
                v => string.IsNullOrWhiteSpace(v) ? "Hedef klasör boş olamaz." : v.IndexOf('"') >= 0 ? "Yol tırnak içeremez." : null);
            if (target != null) await _browser.TransferAsync(e, target.Trim(), copy);
        }

        // ---------- Kart başına FTP ----------

        /// <summary>
        /// Seçili kartın FTP bilgisini hazırlar: bu bağlantıda henüz alınmadıysa (ya da force) karttan version komutuyla
        /// alınır ve yalnız o kartın oturumunda saklanır. Başarısızsa hata şeritte gösterilir ve null döner.
        /// </summary>
        private async Task<CardFtpCredentials> EnsureCardFtpAsync(bool force)
        {
            if (_card == null || _browser == null) return null;
            CardSession session = _sessionOf(_card);
            if (!force && session.Ftp != null) return session.Ftp;
            if (session.State != CardState.Connected) { _browser.ShowWarning("Kart bağlı değil: FTP bilgisi karttan alınamaz."); return null; }
            CardFtpResult r = await _browser.ObtainFtpAsync();
            if (!r.Ok) return null;                       // neden şeritte (FileBrowser.Error)
            session.Ftp = r.Credentials;
            return r.Credentials;
        }

        /// <summary>Bu kartın bilgileriyle çalışan FTP ayarları (ortak klasör / kip ayarları + kartın sunucusu, kullanıcısı, parolası).</summary>
        private TransferSettings CardTransfer(CardFtpCredentials c)
        {
            return _settings.Transfer.ForCard(c);
        }

        private void ShowFtpSettings()
        {
            CardSession session = _card != null ? _sessionOf(_card) : null;
            using (var d = new FtpSettingsDialog(_settings.Transfer, _card, session,
                       force => EnsureCardFtpAsync(force),
                       (eff, progress) => _browser.TestFtpAsync(new FtpUploader(eff), eff, progress),
                       () => CanOperate))
            {
                if (d.ShowDialog(FindForm()) == DialogResult.OK && _saveSettings != null) _saveSettings();
            }
            RefreshUi();
        }

        private async Task DeleteFtpCopiesAsync()
        {
            if (!CanOperate) return;
            CardSession session = _sessionOf(_card);
            var items = session.FtpCopies.Items;
            if (items.Count == 0) return;
            int c = ChoiceDialog.Ask(FindForm(), "FTP kopyalarını sil",
                _card.Label + " için FTP sunucusundaki " + items.Count + " kopya silinsin mi?",
                string.Join(", ", items.Select(i => i.Name + (i.IsDirectory ? "/" : "")).Take(12)) + (items.Count > 12 ? " …" : "") +
                "\nYalnız FTP'deki kopyalar silinir; karttaki dosyalar değişmez.", new[] { "İptal", "Sil" }, 0, 1);
            if (c != 1) return;
            CardFtpCredentials creds = await EnsureCardFtpAsync(false);
            if (creds == null) return;
            await _browser.DeleteFtpCopiesAsync(new FtpUploader(CardTransfer(creds)), session.FtpCopies);
        }

        // ---------- Yükleme ----------

        private async Task UploadWithDialogAsync()
        {
            if (!InFolder) return;
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Karta yüklenecek dosyaları seçin (" + _browser.Current.Path + ")" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                await UploadPathsAsync(dlg.FileNames, null);
            }
        }

        private async Task UploadPathsAsync(IList<string> paths, string destDir)
        {
            if (!CanOperate || paths == null || paths.Count == 0) return;
            FileBrowser b = _browser;
            CardConfig card = _card;
            string target = destDir ?? b.Current.Path;

            // Aynı adlı öğe varsa sor (yalnız içinde bulunulan klasör için bilinir)
            if (target == b.Current.Path)
            {
                var clash = paths.Select(p => Path.GetFileName(p.TrimEnd('\\', '/')))
                                 .Where(n => b.Entries.Any(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase))).ToList();
                if (clash.Count > 0)
                {
                    int c = ChoiceDialog.Ask(FindForm(), "Üzerine yaz", clash.Count + " öğe hedefte zaten var.",
                        string.Join(", ", clash.Take(6)) + (clash.Count > 6 ? " …" : "") + "\nKarttaki mevcut kopyaların üzerine yazılacak.",
                        new[] { "İptal", "Üzerine yaz" }, -1, 1);
                    if (c != 1) return;
                }
            }
            // FTP bilgisi bu karttan (başka kartın bilgisi kullanılmaz)
            CardFtpCredentials creds = await EnsureCardFtpAsync(false);
            if (creds == null || card != _card || b != _browser) return;
            TransferSettings eff = CardTransfer(creds);
            // Büyük yüklemede ilerleme penceresi (G40): boyut hesaplanınca gerekiyorsa açılır, bitince kapanır
            var tp = new TransferProgress();
            string title = paths.Count == 1 ? "\"" + Path.GetFileName(paths[0].TrimEnd('\\', '/')) + "\" → " + (card.Label ?? card.Ip) + ":" + target
                                            : paths.Count + " öğe → " + (card.Label ?? card.Ip) + ":" + target;
            UploadProgressDialog.Watch(FindForm(), title, tp);
            await b.UploadAsync(paths, new FtpUploader(eff), eff, null, target, _sessionOf(card).FtpCopies, tp);
        }

        // ---------- Sürükle-bırak ----------

        private void List_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var item = e.Item as ListViewItem;
            var entry = item != null ? item.Tag as FsEntry : null;
            if (entry == null || entry.Kind == FsKind.Device || !CanOperate) return;
            // Kart içinde taşıma / kopyalama ve Windows Gezgini'ne (bilgisayara) bırakma — G31
            StartCardDrag(entry);
            SetDropHighlight(-1);
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!CanOperate) return;
            ListViewItem over = ItemAt(e.X, e.Y);
            var overEntry = over != null ? over.Tag as FsEntry : null;

            // Listeden sürüklenen kart öğesi bilgisayara da bırakılabildiği için "dosya" biçimi de taşır: önce iç biçim
            if (!e.Data.GetDataPresent(InternalDragFormat) && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Bilgisayardan: bir klasörün üzerine ya da içinde bulunulan klasöre yükle
                bool onFolder = overEntry != null && overEntry.Kind == FsKind.Folder;
                if (_browser.Current.IsRoot && !onFolder) return;
                e.Effect = DragDropEffects.Copy;
                SetDropHighlight(onFolder ? over.Index : -1);
                return;
            }

            var dragged = e.Data.GetData(InternalDragFormat) as FsEntry;
            if (dragged == null) return;
            if (overEntry != null && overEntry.Kind == FsKind.Folder && overEntry != dragged &&
                !overEntry.FullPath.StartsWith(dragged.FullPath + "/"))
            {
                bool ctrl = (e.KeyState & 8) != 0;
                e.Effect = ctrl ? DragDropEffects.Copy : DragDropEffects.Move;
                SetDropHighlight(over.Index);
            }
            else SetDropHighlight(-1);
        }

        private async void List_DragDrop(object sender, DragEventArgs e)
        {
            SetDropHighlight(-1);
            if (!CanOperate) return;
            ListViewItem over = ItemAt(e.X, e.Y);
            var overEntry = over != null ? over.Tag as FsEntry : null;
            string folderTarget = overEntry != null && overEntry.Kind == FsKind.Folder ? overEntry.FullPath : null;

            var files = e.Data.GetDataPresent(InternalDragFormat) ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                // Bırakma işlemi Windows Gezgini'ni bekletir: soru / ilerleme pencereleri bırakma bittikten sonra açılmalı.
                // Yükleme bu yüzden bir sonraki mesaj döngüsüne ertelenir (Gezgin hemen serbest kalır).
                string[] copy = files.ToArray();
                BeginInvoke(new Action(async () => await UploadPathsAsync(copy, folderTarget)));
                return;
            }
            var dragged = e.Data.GetData(InternalDragFormat) as FsEntry;
            if (dragged != null && folderTarget != null)
                await _browser.TransferAsync(dragged, folderTarget, (e.KeyState & 8) != 0);
        }

        /// <summary>Breadcrumb bölümleri de bırakma hedefidir (üst klasöre taşıma / yükleme).</summary>
        private void MakeCrumbDropTarget(Button btn, FsLocation target)
        {
            if (target.IsRoot) return;
            btn.AllowDrop = true;
            btn.DragEnter += (s, e) =>
            {
                e.Effect = DragDropEffects.None;
                if (!CanOperate || target.Same(_browser.Current)) return;
                if (e.Data.GetDataPresent(InternalDragFormat)) e.Effect = (e.KeyState & 8) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
                else if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            btn.DragDrop += async (s, e) =>
            {
                if (!CanOperate) return;
                var files = e.Data.GetDataPresent(InternalDragFormat) ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    string[] copy = files.ToArray();
                    BeginInvoke(new Action(async () => await UploadPathsAsync(copy, target.Path)));
                    return;
                }
                var dragged = e.Data.GetData(InternalDragFormat) as FsEntry;
                if (dragged != null) await _browser.TransferAsync(dragged, target.Path, (e.KeyState & 8) != 0);
            };
        }

        private ListViewItem ItemAt(int screenX, int screenY)
        {
            Point p = _list.PointToClient(new Point(screenX, screenY));
            return _list.GetItemAt(p.X, p.Y);
        }

        private void SetDropHighlight(int index)
        {
            if (_list.DropHighlightIndex == index) return;
            _list.DropHighlightIndex = index;
            _list.Invalidate();
        }
    }
}
