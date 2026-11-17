using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Boot ayarı kayıtları penceresi (G51). Kartın boot ayarları kullanıcı istediğinde adıyla kaydedilir (otomatik kayıt
    /// yok, kart başına en fazla 5), kartın şu anki ayarlarıyla karşılaştırılır ve önizleme + onayla karta geri yüklenir.
    /// Başka kartın kaydı da uygulanabilir; o zaman kartın kendi adres alanları (inet on ethernet / backplane) atlanır.
    /// FTP parolası kaydedilmez, geri yüklemede karttaki korunur. Uygulamadan sonra kart yeniden okunup doğrulanır ve
    /// yeniden başlatma önerilir. Kart Gezgini'nde ve terminalde sağ tık → "Boot ayarı kayıtları…".
    /// </summary>
    public sealed class BootRecordsDialog : DarkDialog
    {
        public static Func<IList<CardConfig>> CardSource;
        public static Func<CardConfig, CardSession> SessionOf;
        public static Action SaveConfig;

        private static readonly Dictionary<CardConfig, BootRecordsDialog> Open = new Dictionary<CardConfig, BootRecordsDialog>();

        private readonly CardConfig _card;
        private readonly Label _title = Theme.MakeLabel("", Theme.Title, Theme.Text);
        private readonly Label _status = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly DarkComboBox _command = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _read = Theme.MakeButton("Kartı oku", Theme.ButtonKind.Secondary);
        private readonly RecordList _records = new RecordList();
        private readonly Button _save = Theme.MakeButton("Karttan oku ve kaydet…", Theme.ButtonKind.Primary);
        private readonly Button _rename = Theme.MakeButton("Yeniden adlandır…", Theme.ButtonKind.Secondary);
        private readonly Button _delete = Theme.MakeButton("Sil", Theme.ButtonKind.Secondary);
        private readonly Label _compareCaption = Theme.MakeLabel("KARŞILAŞTIRMA", Theme.SectionHeader, Theme.TextMuted);
        private readonly Label _summary = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly PlanList _plan = new PlanList();
        private readonly Label _empty = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Button _apply = Theme.MakeButton("Karta uygula…", Theme.ButtonKind.Primary);
        private readonly Button _cancel = Theme.MakeButton("Durdur", Theme.ButtonKind.Secondary);
        private readonly Button _close = Theme.MakeButton("Kapat", Theme.ButtonKind.Secondary);
        private readonly Label _hint = Theme.MakeLabel("Kayıtlar otomatik alınmaz  ·  kart başına en fazla " + BootRecordLogic.MaxPerCard +
                                                       "  ·  FTP parolası kaydedilmez", Theme.UiSmall, Theme.TextFaint);
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        private readonly ToolTip _tips = new DarkToolTip();

        private List<BootField> _current;      // kartın en son okunan ayarları (okunmadıysa null)
        private DateTime _currentAt;
        private CancellationTokenSource _cts;
        private string _message;
        private Color _messageColor;

        private sealed class Item
        {
            public CardConfig Owner;
            public BootRecord Record;
        }

        private BootRecordsDialog(CardConfig card)
            : base("Boot ayarı kayıtları  —  " + card.Label, new Size(940, 600))
        {
            _card = card;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(760, 460);
            KeyPreview = true;
            if (Theme.AppIcon != null) { Icon = Theme.AppIcon; ShowIcon = true; }

            // --- Üst
            var top = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Surface };
            top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };
            _title.Text = "Boot ayarı kayıtları  —  " + card.Label;
            _title.Location = new Point(18, 12);
            _status.Location = new Point(18, 44);
            _status.AutoSize = false;
            _status.Height = 20;
            var cmdCap = Theme.MakeLabel("KOMUT", Theme.SectionHeader, Theme.TextMuted);
            foreach (string c in BootRecordLogic.Commands) _command.Items.Add(c);
            string cmd = string.IsNullOrWhiteSpace(card.BootCommand) ? BootRecordLogic.Commands[0] : card.BootCommand;
            _command.SelectedIndex = Math.Max(0, Array.IndexOf(BootRecordLogic.Commands, cmd));
            _command.Width = 150;
            _read.Width = 100;
            top.Controls.AddRange(new Control[] { _title, _status, cmdCap, _command, _read });
            top.Resize += (s, e) =>
            {
                int x = top.ClientSize.Width - 18 - _read.Width;
                _read.Location = new Point(x, 30);
                x -= 8 + _command.Width;
                _command.Location = new Point(x, 33);
                cmdCap.Location = new Point(x, 12);
                _status.Width = Math.Max(100, x - 30);
            };
            _tips.SetToolTip(_command, "Bu kartın boot ayarları komutu. Kayıt bu komutla okunur ve uygulanır.");
            _tips.SetToolTip(_read, "Kartın şu anki boot ayarlarını okur (tüm alanlar değiştirilmeden geçilir; kartta hiçbir şey değişmez).\n" +
                                    "Okuma sırasında terminalde komut ve alanlar görünür.");

            // --- Sol: kayıtlar
            var left = new Panel { Dock = DockStyle.Left, Width = 310, BackColor = Theme.Sidebar, Padding = new Padding(12, 34, 12, 0) };
            var recCap = Theme.MakeLabel("KAYITLAR", Theme.SectionHeader, Theme.TextMuted);
            recCap.Location = new Point(14, 12);
            recCap.BackColor = Color.Transparent;
            _records.Dock = DockStyle.Fill;
            var leftButtons = new Panel { Dock = DockStyle.Bottom, Height = 92, BackColor = Theme.Sidebar };
            _save.SetBounds(0, 10, 286, 34);
            _rename.SetBounds(0, 50, 160, 30);
            _delete.SetBounds(168, 50, 118, 30);
            leftButtons.Controls.AddRange(new Control[] { _save, _rename, _delete });
            leftButtons.Resize += (s, e) =>
            {
                int w = leftButtons.ClientSize.Width;
                _save.Width = w;
                _rename.Width = (w - 8) * 3 / 5;
                _delete.SetBounds(_rename.Right + 8, 50, w - _rename.Width - 8, 30);
            };
            left.Controls.Add(_records);
            left.Controls.Add(leftButtons);
            left.Controls.Add(recCap);
            _tips.SetToolTip(_save, "Kartın şu anki boot ayarlarını okur ve verdiğiniz adla kaydeder.");

            // --- Sağ: karşılaştırma
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(10, 34, 6, 6) };
            _compareCaption.Location = new Point(14, 12);
            _summary.Location = new Point(130, 13);
            _plan.Dock = DockStyle.Fill;
            _empty.AutoSize = false;
            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.BackColor = Theme.Input;
            var header = new PlanHeader { Dock = DockStyle.Top, Height = 24 };
            right.Controls.Add(_empty);
            right.Controls.Add(_plan);
            right.Controls.Add(header);
            right.Controls.Add(_compareCaption);
            right.Controls.Add(_summary);
            Theme.ApplyDarkScrollBars(_plan);

            // --- Alt bant
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            _hint.BackColor = Color.Transparent;
            _apply.Width = 150;
            _cancel.Width = 90;
            _close.Width = 90;
            _cancel.Visible = false;
            footer.Controls.AddRange(new Control[] { _hint, _apply, _cancel, _close });
            footer.Resize += (s, e) =>
            {
                int y = (FooterHeight - _close.Height) / 2;
                int x = footer.ClientSize.Width - 16 - _close.Width;
                _close.Location = new Point(x, y);
                x -= 8 + _cancel.Width; _cancel.Location = new Point(x, y);
                x -= 8 + _apply.Width; _apply.Location = new Point(x, y);
                _hint.Location = new Point(16, (FooterHeight - _hint.PreferredHeight) / 2);
                _hint.Visible = _hint.Left + _hint.PreferredWidth < _apply.Left - 8;
            };
            _tips.SetToolTip(_apply, "Seçili kaydı karta yazar: önce kart okunur ve değişecek alanlar gösterilir, onaydan sonra\n" +
                                     "yalnız farklı alanlar yazılır, kart yeniden okunup doğrulanır.");

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(top);
            Controls.Add(footer);

            _read.Click += async (s, e) => await ReadCurrentAsync();
            _save.Click += async (s, e) => await SaveFromCardAsync();
            _rename.Click += (s, e) => RenameSelected();
            _delete.Click += (s, e) => DeleteSelected();
            _apply.Click += async (s, e) => await ApplySelectedAsync();
            _cancel.Click += (s, e) => { if (_cts != null) _cts.Cancel(); };
            _close.Click += (s, e) => Close();
            _records.SelectedIndexChanged += (s, e) => RefreshPlan();
            _command.SelectedIndexChanged += (s, e) =>
            {
                string c = (string)_command.SelectedItem;
                if (_card.BootCommand == c) return;
                _card.BootCommand = c;
                Save();
            };
            _timer.Tick += (s, e) => RefreshState();
            FormClosing += (s, e) =>
            {
                if (_cts == null) return;
                e.Cancel = true;
                SetMessage("İşlem sürüyor; bitmesini bekleyin ya da Durdur'a basın.", Theme.Warning);
            };
            FormClosed += (s, e) => { _timer.Stop(); BootRecordsDialog d; if (Open.TryGetValue(_card, out d) && d == this) Open.Remove(_card); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && _cts == null) Close(); };

            FillRecords(null);
            RefreshState();
            _timer.Start();
        }

        /// <summary>Kartın kayıt penceresini açar (açıksa öne getirir).</summary>
        public static void ShowFor(IWin32Window owner, CardConfig card)
        {
            BootRecordsDialog d;
            if (Open.TryGetValue(card, out d) && !d.IsDisposed)
            {
                if (d.WindowState == FormWindowState.Minimized) d.WindowState = FormWindowState.Normal;
                d.Activate();
                return;
            }
            d = new BootRecordsDialog(card);
            Open[card] = d;
            if (owner != null) d.Show(owner); else d.Show();
        }

        private CardSession Session { get { return SessionOf != null ? SessionOf(_card) : null; } }
        private string Command { get { return (string)_command.SelectedItem ?? BootRecordLogic.Commands[0]; } }
        private bool Busy { get { return _cts != null; } }

        private List<BootRecord> OwnRecords
        {
            get { return _card.BootRecords ?? (_card.BootRecords = new List<BootRecord>()); }
        }

        private Item Selected { get { return _records.SelectedItem as Item; } }

        // ---------- Liste ----------

        private void FillRecords(BootRecord select)
        {
            var items = new List<Item>();
            foreach (BootRecord r in OwnRecords.OrderByDescending(x => x.Created)) items.Add(new Item { Owner = _card, Record = r });
            IList<CardConfig> all = CardSource != null ? CardSource() : null;
            if (all != null)
                foreach (CardConfig c in all.Where(c => c != _card && c.BootRecords != null))
                    foreach (BootRecord r in c.BootRecords.OrderByDescending(x => x.Created)) items.Add(new Item { Owner = c, Record = r });
            _records.Own = _card;
            _records.BeginUpdate();
            _records.Items.Clear();
            foreach (Item it in items) _records.Items.Add(it);
            _records.EndUpdate();
            int index = select != null ? items.FindIndex(i => i.Record == select) : (items.Count > 0 ? 0 : -1);
            if (index >= 0) _records.SelectedIndex = index;
            RefreshPlan();
            RefreshState();
        }

        private void RefreshPlan()
        {
            Item it = Selected;
            List<BootPlanRow> rows;
            if (it != null) rows = BootRecordLogic.Plan(it.Record, _current, it.Owner != _card);
            else if (_current != null)
                rows = _current.Select(f => new BootPlanRow
                {
                    Label = f.Label, Current = f.Uncertain ? null : (f.CardValue ?? "").Trim(), Saved = null,
                    Kind = BootRecordLogic.IsPassword(f.Label) ? BootPlanKind.Excluded : BootPlanKind.NotInRecord,
                    Note = BootRecordLogic.IsPassword(f.Label) ? "parola kaydedilmez" : null,
                }).ToList();
            else rows = new List<BootPlanRow>();
            _plan.CardRead = _current != null;
            _plan.BeginUpdate();
            _plan.Items.Clear();
            foreach (BootPlanRow r in rows) _plan.Items.Add(r);
            _plan.EndUpdate();
            _plan.Invalidate();

            _empty.Visible = rows.Count == 0;
            _empty.Text = OwnRecords.Count == 0 && _records.Items.Count == 0
                ? "Bu kart için kayıt yok.\n\"Karttan oku ve kaydet…\" ile kartın şu anki boot ayarlarını kaydedin."
                : "Kartın şu anki ayarlarını görmek için \"Kartı oku\".";
            if (it == null) _summary.Text = _current != null ? "Kartın şu anki ayarları (" + _currentAt.ToString("HH:mm:ss") + ")" : "";
            else if (_current == null) _summary.Text = "\"" + it.Record.Name + "\"  ·  kartla karşılaştırmak için \"Kartı oku\"";
            else
            {
                int change = rows.Count(r => r.WillWrite), excluded = rows.Count(r => r.Kind == BootPlanKind.Excluded),
                    missing = rows.Count(r => r.Kind == BootPlanKind.MissingOnCard);
                _summary.Text = "\"" + it.Record.Name + "\" ↔ kart (" + _currentAt.ToString("HH:mm:ss") + ")  ·  " +
                                (change == 0 ? "fark yok" : change + " alan farklı") +
                                (excluded > 0 ? "  ·  " + excluded + " alan uygulanmaz" : "") +
                                (missing > 0 ? "  ·  " + missing + " alanı kart sormuyor" : "");
            }
            _summary.Size = _summary.PreferredSize;
            RefreshState();
        }

        private void RefreshState()
        {
            CardSession s = Session;
            bool connected = s != null && s.State == CardState.Connected;
            Item it = Selected;
            _read.Enabled = connected && !Busy;
            _save.Enabled = connected && !Busy;
            _apply.Enabled = connected && !Busy && it != null;
            _rename.Enabled = !Busy && it != null && it.Owner == _card;
            _delete.Enabled = !Busy && it != null && it.Owner == _card;
            _command.Enabled = !Busy;
            _cancel.Visible = Busy;
            _close.Enabled = !Busy;
            if (_message != null) { _status.Text = _message; _status.ForeColor = _messageColor; }
            else if (!connected) { _status.Text = "Kart bağlı değil — kayıtlar görülebilir; okumak ve uygulamak için karta bağlanın."; _status.ForeColor = Theme.Warning; }
            else { _status.Text = "Kart bağlı  ·  " + OwnRecords.Count + " / " + BootRecordLogic.MaxPerCard + " kayıt"; _status.ForeColor = Theme.TextMuted; }
        }

        private void SetMessage(string text, Color color)
        {
            _message = text;
            _messageColor = color;
            RefreshState();
        }

        private void Save()
        {
            var h = SaveConfig;
            if (h != null) h();
        }

        // ---------- Okuma ----------

        /// <summary>Kartı okumaya uygun mu? Değilse nedeni.</summary>
        private string CannotRun()
        {
            CardSession s = Session;
            if (s == null || s.State != CardState.Connected) return "Kart bağlı değil.";
            if (s.TransferActive) return CardSession.TransferBusyMessage;
            if (s.InteractiveInputActive || s.BootAutomationActive)
                return "Kartta boot ayarları zaten açık (Boot ayarları penceresi ya da başka bir işlem). Bitince tekrar deneyin.";
            return null;
        }

        private async Task<BootRunResult> RunReadAsync(string what)
        {
            string why = CannotRun();
            if (why != null) { SetMessage("✕  " + why, Theme.Danger); return null; }
            _cts = new CancellationTokenSource();
            SetMessage(what + " — kart " + Command + " ile okunuyor (alanlar değiştirilmeden geçiliyor)…", Theme.Accent);
            try
            {
                BootRunResult r = await BootChangeRunner.ReadAsync(new SessionBootConsole(Session), Command, _cts.Token);
                if (r.Ok)
                {
                    _current = r.Fields;
                    _currentAt = DateTime.Now;
                }
                return r;
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(_card.DisplayName + " boot ayarı okuma", ex);
                return new BootRunResult { Error = ErrorText.Describe(ex) };
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                RefreshState();
            }
        }

        private async Task ReadCurrentAsync()
        {
            BootRunResult r = await RunReadAsync("Okunuyor");
            if (r == null) return;
            if (!r.Ok) SetMessage("✕  Kart okunamadı: " + r.Error, Theme.Danger);
            else if (r.Uncertain.Count > 0) SetMessage("⚠  Okundu; " + r.Uncertain.Count + " alan araya giren log yüzünden okunamadı: " + string.Join(", ", r.Uncertain), Theme.Warning);
            else SetMessage("✓  Kartın şu anki ayarları okundu (" + r.Fields.Count + " alan).", Theme.Success);
            RefreshPlan();
        }

        // ---------- Kaydetme / ad / silme ----------

        private async Task SaveFromCardAsync()
        {
            BootRecord replace = null;
            if (OwnRecords.Count >= BootRecordLogic.MaxPerCard)
            {
                BootRecord oldest = OwnRecords.OrderBy(r => r.Created).First();
                int c = ChoiceDialog.Ask(this, "Kayıt sınırı",
                    "Bu kartta en fazla " + BootRecordLogic.MaxPerCard + " kayıt tutulur.",
                    "Yeni kayıt en eski kaydın (\"" + oldest.Name + "\", " + oldest.Created.ToString("dd.MM.yyyy HH:mm") + ") yerine alınabilir. " +
                    "İsterseniz önce başka bir kaydı silin.",
                    new[] { "Vazgeç", "En eskisinin yerine kaydet" }, 0, 1);
                if (c != 1) return;
                replace = oldest;
            }
            string name = InputDialog.Ask(this, "Boot ayarlarını kaydet", "Kayıt adı (ör. \"Fabrika ayarı\", \"Test sahası\"):",
                                          "Kayıt " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                                          n => BootRecordLogic.ValidateName(n, OwnRecords.Where(r => r != replace), null));
            if (name == null) return;

            BootRunResult r0 = await RunReadAsync("Kaydediliyor");
            if (r0 == null) return;
            if (!r0.Ok) { SetMessage("✕  Kaydedilmedi — kart okunamadı: " + r0.Error, Theme.Danger); RefreshPlan(); return; }
            if (r0.Uncertain.Count > 0)
            {
                SetMessage("✕  Kaydedilmedi — " + string.Join(", ", r0.Uncertain) + " alanı araya giren log yüzünden güvenle okunamadı. Tekrar deneyin.", Theme.Danger);
                RefreshPlan();
                return;
            }
            BootRecord rec = BootRecordLogic.FromFields(name, Command, _card.Ip, r0.Fields, DateTime.Now);
            if (replace != null) OwnRecords.Remove(replace);
            OwnRecords.Add(rec);
            _card.BootCommand = Command;
            Save();
            LogEvent(CardEventLog.Boot, "Boot ayarları \"" + rec.Name + "\" adıyla kaydedildi (" + rec.Fields.Count + " alan" +
                                        (replace != null ? "; en eski kayıt \"" + replace.Name + "\" yerine" : "") + ").");
            SetMessage("✓  \"" + rec.Name + "\" kaydedildi (" + rec.Fields.Count + " alan; FTP parolası kaydedilmez).", Theme.Success);
            FillRecords(rec);
        }

        private void RenameSelected()
        {
            Item it = Selected;
            if (it == null || it.Owner != _card) return;
            string name = InputDialog.Ask(this, "Kaydı yeniden adlandır", "Yeni ad:", it.Record.Name,
                                          n => BootRecordLogic.ValidateName(n, OwnRecords, it.Record));
            if (name == null || name == it.Record.Name) return;
            string old = it.Record.Name;
            it.Record.Name = name.Trim();
            Save();
            LogEvent(CardEventLog.Boot, "Boot ayarı kaydı \"" + old + "\" → \"" + it.Record.Name + "\" olarak yeniden adlandırıldı.");
            FillRecords(it.Record);
        }

        private void DeleteSelected()
        {
            Item it = Selected;
            if (it == null || it.Owner != _card) return;
            int c = ChoiceDialog.Ask(this, "Kaydı sil", "\"" + it.Record.Name + "\" kaydı silinsin mi?",
                                     "Yalnız Vision'daki kayıt silinir; kartın boot ayarları değişmez.", new[] { "Vazgeç", "Sil" }, 0, 1);
            if (c != 1) return;
            OwnRecords.Remove(it.Record);
            Save();
            LogEvent(CardEventLog.Boot, "Boot ayarı kaydı \"" + it.Record.Name + "\" silindi.");
            SetMessage("Kayıt silindi.", Theme.TextMuted);
            FillRecords(null);
        }

        // ---------- Geri yükleme ----------

        private async Task ApplySelectedAsync()
        {
            Item it = Selected;
            if (it == null) return;
            bool other = it.Owner != _card;
            BootRecord rec = it.Record;

            // 1) Kartın şu anki hali okunur ve değişecekler gösterilir
            BootRunResult now = await RunReadAsync("Önizleme");
            if (now == null) return;
            RefreshPlan();
            if (!now.Ok) { SetMessage("✕  Uygulanmadı — kart okunamadı: " + now.Error, Theme.Danger); return; }
            List<BootPlanRow> plan = BootRecordLogic.Plan(rec, now.Fields, other);
            if (!plan.Any(r => r.WillWrite))
            {
                SetMessage("✓  Kart zaten \"" + rec.Name + "\" kaydındaki ayarlarda; yazılacak bir şey yok.", Theme.Success);
                return;
            }
            if (!BootApplyPreview.Confirm(this, _card, it.Owner, rec, plan, Command)) { SetMessage("Uygulanmadı (vazgeçildi).", Theme.TextMuted); return; }

            // 2) Yaz + doğrula
            string why = CannotRun();
            if (why != null) { SetMessage("✕  " + why, Theme.Danger); return; }
            _cts = new CancellationTokenSource();
            SetMessage("\"" + rec.Name + "\" karta yazılıyor (" + Command + "), ardından doğrulanacak…", Theme.Accent);
            BootApplyResult res;
            try
            {
                res = await BootChangeRunner.ApplyAsync(new SessionBootConsole(Session), rec, Command, other, _cts.Token);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(_card.DisplayName + " boot ayarı uygulama", ex);
                res = new BootApplyResult { Error = ErrorText.Describe(ex) };
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }
            if (res.Verify != null && res.Verify.Ok) { _current = res.Verify.Fields; _currentAt = DateTime.Now; }
            else if (res.Write != null && res.Write.Ok) { _current = null; }
            RefreshPlan();

            string source = other ? " (" + (rec.SourceIp ?? it.Owner.Ip) + " kartının kaydı; adres alanları atlandı)" : "";
            string written = res.Written.Count > 0 ? string.Join("; ", res.Written) : "hiçbir alan yazılmadı";
            if (res.Ok)
            {
                LogEvent(CardEventLog.Boot, "\"" + rec.Name + "\" boot ayarı kaydı uygulandı" + source + " — " + res.Written.Count + " alan: " + written +
                                            ". Doğrulama: tamam.");
                SetMessage("✓  \"" + rec.Name + "\" uygulandı ve doğrulandı (" + res.Written.Count + " alan).", Theme.Success);
                OfferReboot(rec);
            }
            else
            {
                LogEvent(CardEventLog.Boot, "\"" + rec.Name + "\" boot ayarı kaydı uygulanamadı" + source + ": " + res.Error +
                                            (res.Mismatches.Count > 0 ? "  Tutmayan: " + string.Join("; ", res.Mismatches) : "") +
                                            "  Yazılan: " + written);
                SetMessage("✕  " + res.Error, Theme.Danger);
                ChoiceDialog.Ask(this, "Boot ayarı kaydı uygulanamadı", "\"" + rec.Name + "\" karta tam uygulanamadı.",
                    res.Error + (res.Mismatches.Count > 0 ? "\n\nKayıttaki değere gelmeyen alanlar:\n• " + string.Join("\n• ", res.Mismatches) : "") +
                    "\n\nYazılan alanlar: " + written + "\n\nKartın şu anki ayarlarını karşılaştırma listesinde görebilirsiniz.",
                    new[] { "Tamam" }, 0, -1);
            }
        }

        private void OfferReboot(BootRecord rec)
        {
            CardSession s = Session;
            if (s == null || s.State != CardState.Connected) return;
            string cmd = RebootCommands.CommandFor(_card.RebootOption);
            int c = ChoiceDialog.Ask(this, "Kartı yeniden başlat",
                "\"" + rec.Name + "\" uygulandı. Kart şimdi yeniden başlatılsın mı?",
                "Yeni boot ayarları genellikle kart yeniden başlayınca geçerli olur. Kart yeniden başlarken bağlantı kopar; " +
                "kart açılınca Vision kendiliğinden yeniden bağlanır.",
                new[] { "Sonra", "\"" + cmd + "\" ile yeniden başlat" }, 0, 1);
            if (c != 1) return;
            Task.Run(async () =>
            {
                try
                {
                    s.LogEvent(CardEventLog.Reboot, "\"" + cmd + "\" gönderildi (boot ayarı kaydı uygulandıktan sonra).");
                    await s.SendCommandAsync(cmd, "boot ayarı kaydı");
                }
                catch (Exception ex)
                {
                    ErrorText.LogTechnical(_card.DisplayName + " reboot", ex);
                    BeginInvoke(new Action(() => SetMessage("✕  " + cmd + " gönderilemedi: " + ErrorText.Describe(ex), Theme.Danger)));
                }
            });
            SetMessage("✓  Uygulandı; \"" + cmd + "\" gönderildi. Kart açılınca Vision yeniden bağlanır.", Theme.Success);
        }

        private void LogEvent(string category, string message)
        {
            CardSession s = Session;
            if (s != null) s.LogEvent(category, message);
            else CardEventLog.Write(_card, category, message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }

        // =====================================================================
        // Çizim
        // =====================================================================

        private sealed class RecordList : ListBox
        {
            private readonly Font _bold = new Font("Segoe UI Semibold", 9.75f);
            public CardConfig Own;

            public RecordList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = Theme.Ui;
                ItemHeight = 46;
                BackColor = Theme.Sidebar;
                ForeColor = Theme.Text;
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var it = (Item)Items[e.Index];
                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool own = it.Owner == Own;
                Graphics g = e.Graphics;
                using (var b = new SolidBrush(selected ? Theme.ListSelectedStrong : BackColor)) g.FillRectangle(b, e.Bounds);
                Color fore = selected ? Theme.ListSelectedText : own ? Theme.Text : Theme.TextMuted;
                Color muted = selected ? Theme.ListSelectedText : Theme.TextMuted;
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(g, it.Record.Name, _bold, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 14, 20), fore, f);
                string sub = it.Record.Created.ToString("dd.MM.yyyy HH:mm") + "  ·  " + it.Record.Fields.Count + " alan" +
                             (own ? "" : "  ·  " + it.Owner.Label + " kartının");
                TextRenderer.DrawText(g, sub, Theme.UiSmall, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 26, e.Bounds.Width - 14, 16), muted, f);
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _bold.Dispose();
                base.Dispose(disposing);
            }
        }

        internal const int LabelColumn = 210;

        private sealed class PlanHeader : Control
        {
            public PlanHeader()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var b = new SolidBrush(Theme.Input)) e.Graphics.FillRectangle(b, ClientRectangle);
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
                int col = (Width - LabelColumn - 16) / 2;
                TextRenderer.DrawText(e.Graphics, "ALAN", Theme.SectionHeader, new Rectangle(8, 0, LabelColumn, Height), Theme.TextMuted, f);
                TextRenderer.DrawText(e.Graphics, "KAYITTAKİ", Theme.SectionHeader, new Rectangle(8 + LabelColumn, 0, col, Height), Theme.TextMuted, f);
                TextRenderer.DrawText(e.Graphics, "KARTTAKİ (ŞU AN)", Theme.SectionHeader, new Rectangle(8 + LabelColumn + col, 0, col, Height), Theme.TextMuted, f);
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }

            protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        }

        /// <summary>Karşılaştırma satırları: farklı alanlar vurgulu, uygulanmayanlar soluk ve nedeniyle.</summary>
        internal sealed class PlanList : ListBox
        {
            private readonly Font _mono = new Font("Consolas", 9.75f);
            public bool CardRead;

            public PlanList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                Font = Theme.Ui;
                ItemHeight = 30;
                BackColor = Theme.Input;
                ForeColor = Theme.Text;
                SelectionMode = SelectionMode.One;
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var r = (BootPlanRow)Items[e.Index];
                Graphics g = e.Graphics;
                // Seçim gösterilmez (liste yalnız bilgi): değişecek satırlar vurgulu
                Color back = r.WillWrite && CardRead ? Theme.AccentSurface : BackColor;
                using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                          TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                int col = (e.Bounds.Width - LabelColumn - 16) / 2;
                int x = e.Bounds.X + 8;
                bool faint = r.Kind == BootPlanKind.Excluded || r.Kind == BootPlanKind.NotInRecord;
                Color text = faint ? Theme.TextMuted : Theme.Text;
                TextRenderer.DrawText(g, r.Label, Font, new Rectangle(x, e.Bounds.Y, LabelColumn - 8, e.Bounds.Height), text, f);
                x += LabelColumn;

                string saved = r.Kind == BootPlanKind.Excluded && r.Saved == null ? "—" : r.Saved == null ? "(kayıtta yok)" : Show(r.Saved);
                if (r.Kind == BootPlanKind.Excluded) saved = (r.Saved != null ? Show(r.Saved) + "  " : "") + "(" + r.Note + ")";
                TextRenderer.DrawText(g, saved, r.Saved == null || r.Kind == BootPlanKind.Excluded ? Font : _mono,
                                      new Rectangle(x, e.Bounds.Y, col - 8, e.Bounds.Height),
                                      r.WillWrite && CardRead ? Theme.Accent : faint ? Theme.TextMuted : text, f);
                x += col;

                string cur = !CardRead ? "" : r.Kind == BootPlanKind.MissingOnCard ? "(kart bu alanı sormuyor)"
                           : r.Current == null ? "(okunamadı)" : Show(r.Current);
                Color curColor = r.Kind == BootPlanKind.MissingOnCard ? Theme.Warning : r.WillWrite ? Theme.Text : Theme.TextMuted;
                TextRenderer.DrawText(g, cur, r.Current == null ? Font : _mono, new Rectangle(x, e.Bounds.Y, col - 8, e.Bounds.Height), curColor, f);
                using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            private static string Show(string v) { return v.Length == 0 ? "(boş)" : v; }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _mono.Dispose();
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>Geri yüklemeden önce önizleme: yazılacak alanlar (eski → yeni), uygulanmayanlar ve kartın sormadıkları.</summary>
    internal sealed class BootApplyPreview : DarkDialog
    {
        private BootApplyPreview(CardConfig card, CardConfig owner, BootRecord rec, List<BootPlanRow> plan, string command)
            : base("Boot ayarı kaydını uygula", new Size(720, 480))
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(560, 360);
            bool other = owner != card;
            int writes = plan.Count(r => r.WillWrite);
            var top = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Surface };
            var msg = Theme.MakeLabel("\"" + rec.Name + "\" " + card.Label + " kartına uygulansın mı?", new Font("Segoe UI Semibold", 10.5f), Theme.Text);
            msg.Location = new Point(18, 14);
            string detail = writes + " alan değişecek (vurgulu satırlar); yalnız bunlar " + command + " ile yazılır, diğer alanlara dokunulmaz." +
                            (other ? "\nKayıt " + (rec.SourceIp ?? owner.Ip) + " kartının: bu kartın kendi adres alanları uygulanmaz." : "") +
                            "\nFTP parolası kaydedilmediği için karttaki korunur. Yazdıktan sonra kart yeniden okunup doğrulanır.";
            var det = Theme.MakeLabel(detail, Theme.UiSmall, Theme.TextMuted);
            det.Location = new Point(18, 40);
            det.MaximumSize = new Size(680, 0);
            top.Controls.AddRange(new Control[] { msg, det });

            var list = new BootRecordsDialog.PlanList { Dock = DockStyle.Fill, CardRead = true };
            foreach (BootPlanRow r in plan.OrderBy(r => r.WillWrite ? 0 : r.Kind == BootPlanKind.MissingOnCard ? 1 : r.Kind == BootPlanKind.Excluded ? 2 : 3))
                list.Items.Add(r);
            var middle = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(8, 6, 4, 6) };
            middle.Controls.Add(list);
            Theme.ApplyDarkScrollBars(list);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            var cancel = Theme.MakeButton("Vazgeç", Theme.ButtonKind.Secondary);
            var ok = Theme.MakeButton(writes + " alanı yaz", Theme.ButtonKind.Danger);
            cancel.Width = 100;
            ok.Width = 130;
            footer.Controls.AddRange(new Control[] { cancel, ok });
            footer.Resize += (s, e) =>
            {
                int y = (FooterHeight - ok.Height) / 2;
                ok.Location = new Point(footer.ClientSize.Width - 16 - ok.Width, y);
                cancel.Location = new Point(ok.Left - 8 - cancel.Width, y);
            };
            Controls.Add(middle);
            Controls.Add(top);
            Controls.Add(footer);
            AcceptButton = cancel;   // varsayılan: vazgeç
            CancelButton = cancel;
            cancel.DialogResult = DialogResult.Cancel;
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
        }

        public static bool Confirm(IWin32Window owner, CardConfig card, CardConfig recordOwner, BootRecord rec, List<BootPlanRow> plan, string command)
        {
            using (var d = new BootApplyPreview(card, recordOwner, rec, plan, command))
                return d.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
