using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Komut Kütüphanesi: komutu karta aktarma ve hazır (VxWorks 6.4) komutları ekleme.
    /// "Karta aktar" seçilen komutu istenen kartın komut satırına YAZAR (göndermez): kullanıcı argümanları düzenleyip
    /// Enter'a basar. Kart bağlı değilse önce bağlanılır, bağlanınca yazılır.
    /// </summary>
    public sealed partial class CommandLibraryView
    {
        private readonly IconButton _transferBtn = new IconButton(IconKind.Send, IconTone.Ghost) { Label = "Karta aktar", IconSize = 14 };
        private readonly ContextMenuStrip _transferMenu = new ContextMenuStrip();
        private readonly IconButton _bundledBtn = new IconButton(IconKind.Book, IconTone.Secondary) { Label = "Hazır komutlar" };
        private readonly List<Image> _menuImages = new List<Image>();

        /// <summary>Aktarılabilecek kartlar (gruplarıyla). Ayarlanmazsa "Karta aktar" gizlenir.</summary>
        public Func<IList<CardGroup>> TransferGroups { get; set; }
        /// <summary>Kartın bağlantı durumu.</summary>
        public Func<CardConfig, CardState> TransferStateOf { get; set; }
        /// <summary>Komutu kartın komut satırına yazar (kart Telnet sekmesinde açılır, gerekirse bağlanılır).</summary>
        public Action<CardConfig, string> TransferToCard { get; set; }
        /// <summary>Komutu genel komut çubuğuna (ekrandaki tüm kartlar) yazar.</summary>
        public Action<string> TransferToAll { get; set; }

        private bool CanTransfer { get { return TransferGroups != null && TransferToCard != null; } }

        /// <summary>Karta aktarmayı etkinleştirir (ana pencere kurulurken bir kez çağrılır).</summary>
        public void EnableTransfer(Func<IList<CardGroup>> groups, Func<CardConfig, CardState> stateOf,
                                   Action<CardConfig, string> toCard, Action<string> toAll)
        {
            TransferGroups = groups;
            TransferStateOf = stateOf;
            TransferToCard = toCard;
            TransferToAll = toAll;
            if (!_editing) ApplyMode();
        }

        /// <summary>Yazılış başlığının sağındaki düğmeler: [Karta aktar] [Kopyala].</summary>
        private Control UsageActions()
        {
            var p = new Panel { Size = new Size(232, 26), BackColor = Theme.Window, Margin = new Padding(0) };
            _transferBtn.Font = Theme.UiSmall;
            _transferBtn.SetBounds(0, 0, 124, 26);
            _transferBtn.SetTip(_tips, "Karta aktar — komutu seçtiğiniz kartın komut satırına yazar (GÖNDERMEZ; düzenleyip Enter'a basın).\n" +
                                       "Yazılıştan bir satır seçerseniz o satır, seçmezseniz ilk satır aktarılır. Kart bağlı değilse önce bağlanılır.");
            _copyUsageBtn.Location = new Point(128, 0);
            p.Controls.Add(_transferBtn);
            p.Controls.Add(_copyUsageBtn);
            _transferMenu.Renderer = new DarkMenuRenderer();
            _transferBtn.Click += (s, e) => ShowTransferMenu();
            return p;
        }

        private void BuildBundledButton(Panel top)
        {
            _bundledBtn.Size = new Size(140, 34);
            _bundledBtn.SetTip(_tips, "Hazır komutlar — Vision ile gelen VxWorks 6.4 komutlarından kütüphanede EKSİK olanları ekler.\n" +
                                      "Var olan ve sizin düzenlediğiniz komutlar değiştirilmez.");
            _bundledBtn.Click += (s, e) => ImportBundled();
            top.Controls.Add(_bundledBtn);
        }

        // ---------- Karta aktar ----------

        private void ShowTransferMenu()
        {
            if (_current == null || !CanTransfer) return;
            string selection = _usage.SelectionLength > 0 ? _usage.SelectedText : null;
            string cmd = CommandLibrary.TransferText(_current, selection);
            _transferMenu.Items.Clear();
            BuildTransferItems(_transferMenu.Items, cmd);
            _transferMenu.Show(_transferBtn, new Point(0, _transferBtn.Height));
        }

        /// <summary>Kart listesi: gruplar başlık olarak, kartlar durum noktasıyla; en altta "ekrandaki tüm kartlar".</summary>
        private void BuildTransferItems(ToolStripItemCollection items, string cmd)
        {
            foreach (Image img in _menuImages) img.Dispose();
            _menuImages.Clear();

            var head = new ToolStripMenuItem("\"" + (cmd.Length > 50 ? cmd.Substring(0, 47) + "…" : cmd) + "\"  →  komut satırına yazılacak kart:")
            {
                Enabled = false,
                ForeColor = Theme.TextMuted,
            };
            items.Add(head);
            if (cmd.Length == 0) return;

            IList<CardGroup> groups = TransferGroups();
            bool any = false;
            foreach (CardGroup g in groups)
            {
                if (g.Cards.Count == 0) continue;
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem(g.Name + "  ·  port " + g.Port) { Enabled = false, ForeColor = Theme.TextMuted, Font = Theme.SectionHeader });
                foreach (CardConfig c in g.Cards)
                {
                    CardState st = TransferStateOf != null ? TransferStateOf(c) : CardState.Disconnected;
                    bool connected = st == CardState.Connected;
                    var it = new ToolStripMenuItem(c.Label + (connected ? "" : "   (" + Theme.StateText(st).ToLowerInvariant() + " — bağlanıp yazar)"))
                    {
                        ForeColor = Theme.Text,
                        Image = Dot(Theme.StateColor(st)),
                    };
                    CardConfig card = c;
                    it.Click += (s, e) => TransferToCard(card, cmd);
                    items.Add(it);
                    any = true;
                }
            }
            if (!any)
            {
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem("Henüz kart eklenmedi (Ctrl+N ile kart ekleyin)") { Enabled = false });
            }
            if (TransferToAll != null)
            {
                items.Add(new ToolStripSeparator());
                var all = new ToolStripMenuItem("Ekrandaki tüm kartlar (genel komut çubuğuna yaz)") { ForeColor = Theme.Text };
                all.Click += (s, e) => TransferToAll(cmd);
                items.Add(all);
            }
        }

        private Image Dot(Color c)
        {
            var bmp = new Bitmap(12, 12);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, 2, 2, 8, 8);
            }
            _menuImages.Add(bmp);
            return bmp;
        }

        /// <summary>Ağaçtaki komutun sağ tık menüsüne "Karta aktar" alt menüsü.</summary>
        private void AddTransferSubmenu(ToolStripItemCollection items, CommandDoc doc)
        {
            if (!CanTransfer || doc == null) return;
            var sub = new ToolStripMenuItem("Karta aktar") { ForeColor = Theme.Text };
            BuildTransferItems(sub.DropDownItems, CommandLibrary.TransferText(doc, null));
            sub.DropDown.BackColor = Theme.SurfaceHigh;
            sub.DropDown.Renderer = _menu.Renderer;
            items.Add(sub);
        }

        // ---------- Hazır komutlar ----------

        /// <summary>İlk açılış: kütüphane tamamen boşsa (dosya yok ya da boş şablon) hazır komutlarla doldurulur.</summary>
        private void SeedBundledIfEmpty()
        {
            if (_library.LoadError != null || !_library.IsEmpty) return;
            CommandLibraryFile bundled = CommandLibrary.LoadBundled();
            if (bundled == null) return;
            string error;
            int n = _library.ImportMissing(bundled, out error);
            if (n < 0) { ShowBanner("⚠  Hazır komutlar eklenemedi: " + error, true); return; }
            if (n == 0) return;
            RebuildList(null, null);
            int i = FirstCommandIndex();
            if (i >= 0) _list.SelectedIndex = i;
            ShowBanner("ℹ  Hazır VxWorks 6.4 komutları yüklendi: " + n + " komut, " + _library.Modules.Count + " modül. " +
                       "Komut ekleyebilir, düzenleyebilir ve modüller arasında taşıyabilirsiniz.", false);
        }

        private void ImportBundled()
        {
            if (_editing) return;
            CommandLibraryFile bundled = CommandLibrary.LoadBundled();
            string error;
            int n = _library.ImportMissing(bundled, out error);
            if (n < 0) { ShowBanner("⚠  " + error, true); return; }
            RebuildList(_current, _current == null ? _currentModuleKey : null);
            if (_current == null && _currentModuleKey == null)
            {
                int i = FirstCommandIndex();
                if (i >= 0) _list.SelectedIndex = i;
            }
            ShowBanner(n > 0
                ? "✓  " + n + " hazır komut eklendi. Mevcut komutlarınız değiştirilmedi."
                : "ℹ  Hazır komutların hepsi zaten kütüphanede (aynı adlı komutlarınız korunur).", false);
        }
    }
}
