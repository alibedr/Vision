using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Ana pencerenin altındaki genel komut çubuğu: yazılan komutu çalışma alanında AÇIK olan
    /// tüm kartlara gönderir. Kart komut satırlarından görsel olarak ayrışır (vurgu renkli şerit ve etiket).
    /// Kendi komut geçmişi vardır (yukarı / aşağı ok).
    /// </summary>
    public sealed class GlobalCommandBar : UserControl
    {
        private static Color BarBack { get { return Theme.CommandBarBack; } }
        private static Color FieldBack { get { return Theme.CommandFieldBack; } }
        private static Color FieldBorder { get { return Theme.CommandFieldBorder; } }

        private readonly Func<IList<CardSession>> _targets;
        private readonly CommandHistory _history = new CommandHistory();
        private InputAutoComplete _autoComplete;

        private readonly Label _title = new GlyphLabel { Text = "⇶  TÜM AÇIK KARTLARA", Font = Theme.SectionHeader, ForeColor = Theme.Accent, AutoSize = true, BackColor = Color.Transparent };
        private readonly Label _targetInfo = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Panel _fieldBorder = new Panel { BackColor = FieldBorder, Padding = new Padding(1) };
        private readonly Panel _field = new Panel { Dock = DockStyle.Fill, BackColor = FieldBack, Padding = new Padding(10, 7, 8, 4) };
        private readonly TextBox _input = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
        private readonly IconButton _sendBtn = new IconButton(IconKind.SendAll, IconTone.Primary);   // "Hepsine Gönder"
        private readonly Label _result = new GlyphLabel { Font = Theme.UiSmall, ForeColor = Theme.TextMuted, AutoSize = true, BackColor = Color.Transparent };
        private readonly ToolTip _tips = new DarkToolTip();
        private bool _sending;
        private int _targetCount;

        // Hızlı komut, reboot / reboot 2 ve anlık kayıt (tüm açık kartlar)
        private static readonly RepeatGuard Guard = new RepeatGuard(TimeSpan.FromSeconds(4));
        private readonly AppSettings _settings;
        private readonly Action _saveSettings;
        private readonly IconButton _quickBtn = new IconButton(IconKind.Bolt, IconTone.Secondary) { IconSize = 14 };
        private readonly SplitActionButton _rebootBtn = new SplitActionButton { MainIcon = IconKind.Power };
        private readonly IconButton _saveBtn = new IconButton(IconKind.Save, IconTone.Secondary);
        private readonly IconButton _clearAllBtn = new IconButton(IconKind.Clear, IconTone.Secondary);
        private readonly ContextMenuStrip _quickMenu = new ContextMenuStrip();
        private readonly Timer _rebootCooldown = new Timer { Interval = 4000 };

        /// <summary>"Dosyaya kaydet": ekrandaki tüm kartların o anki terminal içeriği ayrı dosyalara kaydedilmeli.</summary>
        public event Action SaveSnapshotsRequested;

        /// <summary>"Tümünü temizle": ekrandaki tüm kartların terminal ekranı temizlenmeli.</summary>
        public event Action ClearAllRequested;

        public GlobalCommandBar(Func<IList<CardSession>> openSessions, AppSettings settings, Action saveSettings)
        {
            _targets = openSessions;
            _settings = settings;
            _saveSettings = saveSettings;
            Height = 58;
            BackColor = BarBack;
            DoubleBuffered = true;

            _input.BackColor = FieldBack;
            _input.ForeColor = Theme.Text;
            _input.Font = new Font("Consolas", 10.5f);
            Theme.SetCueBanner(_input, "Komut yazıp Enter'a basın — ekrandaki tüm kartlara aynı anda gönderilir");
            _field.Controls.Add(_input);
            _fieldBorder.Controls.Add(_field);

            _sendBtn.Width = 40;
            _sendBtn.Height = 34;
            _sendBtn.IconSize = 18;
            _result.AutoSize = false;
            _result.AutoEllipsis = true;
            _result.TextAlign = ContentAlignment.MiddleLeft;

            _quickBtn.Font = Theme.Ui;
            _quickBtn.Height = 34;
            _rebootBtn.Height = 34;
            _saveBtn.Size = new Size(36, 34);
            _clearAllBtn.Size = new Size(36, 34);
            _quickMenu.Renderer = new DarkMenuRenderer();
            _quickMenu.ShowImageMargin = false;
            _saveBtn.SetTip(_tips, "Dosyaya kaydet — ekrandaki HER kartın terminalinde şu an görünen içeriği\nkartın kendi dosyasına kaydeder (kart başına ayrı dosya; sürekli kayıt başlatmaz)");
            _clearAllBtn.SetTip(_tips, "Tümünü temizle — ekrandaki HER kartın terminal ekranını ve geçmişini temizler\n(kartlardaki silgi düğmesiyle aynı işi tüm kartlara bir kerede yapar; karta komut gönderilmez)");
            _clearAllBtn.AccessibleName = "Tüm kartların ekranını temizle";
            Controls.AddRange(new Control[] { _title, _targetInfo, _fieldBorder, _sendBtn, _quickBtn, _rebootBtn, _saveBtn, _clearAllBtn, _result });
            _quickBtn.Click += async (s, e) => await RunQuickAsync();
            _quickBtn.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowQuickMenu(); };
            _rebootBtn.MainClick += async (s, e) => await RunRebootAsync();
            _rebootBtn.ToggleClick += (s, e) => ToggleReboot();
            _rebootCooldown.Tick += (s, e) => { _rebootCooldown.Stop(); UpdateTargets(); };
            _saveBtn.Click += (s, e) => { var h = SaveSnapshotsRequested; if (h != null) h(); };
            _clearAllBtn.Click += (s, e) => { var h = ClearAllRequested; if (h != null) h(); };
            RefreshQuickButton();
            RefreshRebootButton();

            _sendBtn.SetTip(_tips, "Hepsine Gönder (Enter)\nYalnızca Telnet sekmesinde açık olan kartlara gönderilir.\nBağlı olmayan açık kartlar atlanır ve raporlanır.");
            _sendBtn.AccessibleName = "Hepsine Gönder";
            _input.KeyDown += Input_KeyDown;
            _autoComplete = new InputAutoComplete(_input, () => _history);   // kendi geçmişinden tamamlama
            _sendBtn.Click += async (s, e) => await SendAsync();
            _field.Click += (s, e) => _input.Focus();

            UpdateTargets();
        }

        public void FocusInput()
        {
            _input.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int h = Height;
            int left = 18;
            _title.Location = new Point(left, 13);
            _targetInfo.Location = new Point(left, 31);
            int fieldX = left + 178;
            int resultW = Width >= 1300 ? 300 : 180;   // sonuç her zaman görünür kalır
            int y = (h - 34) / 2 + 1;
            // Sağdan sola: [sonuç] [temizle] [kaydet] [reboot ⇄] [hızlı komut] [Hepsine Gönder] [komut alanı]
            int x = Width - 16 - resultW;
            _result.SetBounds(x, y, resultW, 34);
            x -= 12 + _clearAllBtn.Width; _clearAllBtn.Location = new Point(x, y);
            x -= 6 + _saveBtn.Width; _saveBtn.Location = new Point(x, y);
            _rebootBtn.Width = _rebootBtn.GetPreferredSize(Size.Empty).Width;
            x -= 6 + _rebootBtn.Width; _rebootBtn.Location = new Point(x, y);
            x -= 6 + _quickBtn.Width; _quickBtn.Location = new Point(x, y);
            x -= 6 + _sendBtn.Width; _sendBtn.Location = new Point(x, y);
            _fieldBorder.SetBounds(fieldX, y, Math.Max(120, x - 10 - fieldX), 34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Kart komut satırlarından ayırt etmek için üstte vurgu çizgisi ve solda şerit
            using (var top = new SolidBrush(Theme.Accent))
            {
                e.Graphics.FillRectangle(top, 0, 0, Width, 2);
                e.Graphics.FillRectangle(top, 0, 0, 4, Height);
            }
        }

        /// <summary>Hedef kart sayısını günceller (görünümler veya bağlantılar değişince çağrılır).</summary>
        public void UpdateTargets()
        {
            IList<CardSession> targets = _targets();
            _targetCount = targets.Count;
            int connected = targets.Count(t => t.State == CardState.Connected);
            string info = _targetCount == 0 ? "Ekranda kart yok" : _targetCount + " kart ekranda  ·  " + connected + " bağlı";
            if (_targetInfo.Text != info) _targetInfo.Text = info;
            _targetInfo.ForeColor = _targetCount > 0 && connected < _targetCount ? Theme.Warning : Theme.TextMuted;

            bool canSend = _targetCount > 0 && connected > 0 && !_sending;
            if (_sendBtn.Enabled != canSend) _sendBtn.Enabled = canSend;
            if (_quickBtn.Enabled != canSend) _quickBtn.Enabled = canSend;
            bool canReboot = canSend && !_rebootCooldown.Enabled;
            if (_rebootBtn.Enabled != canReboot) _rebootBtn.Enabled = canReboot;
            bool canSave = _targetCount > 0;
            if (_saveBtn.Enabled != canSave) _saveBtn.Enabled = canSave;
            if (_clearAllBtn.Enabled != canSave) _clearAllBtn.Enabled = canSave;
        }

        // ---------- Genel hızlı komut ----------

        private void RefreshQuickButton()
        {
            string cmd = (_settings.GlobalQuickCommand ?? "").Trim();
            _quickBtn.Label = cmd.Length == 0 ? "Hızlı komut" : cmd;
            Size ts = TextRenderer.MeasureText(_quickBtn.Label, _quickBtn.Font, Size.Empty, TextFormatFlags.NoPadding);
            _quickBtn.Width = Math.Min(170, Math.Max(104, ts.Width + 14 + 8 + 22));
            _quickBtn.SetTip(_tips, cmd.Length == 0
                ? "Genel hızlı komut ayarlanmadı — tıklayıp bir komut atayın"
                : "Genel hızlı komut: " + cmd + "\nTıklayınca ekrandaki tüm BAĞLI kartlara birer kez gönderilir.  ·  Sağ tık: düzenle / kaldır");
            _quickBtn.AccessibleName = cmd.Length == 0 ? "Genel hızlı komut ayarla" : "Genel hızlı komut: " + cmd;
            OnResize(EventArgs.Empty);
        }

        private void ShowQuickMenu()
        {
            _quickMenu.Items.Clear();
            var edit = new ToolStripMenuItem(string.IsNullOrWhiteSpace(_settings.GlobalQuickCommand) ? "Genel hızlı komut ata…" : "Genel hızlı komutu düzenle…") { ForeColor = Theme.Text };
            edit.Click += (s, e) => EditQuick();
            _quickMenu.Items.Add(edit);
            if (!string.IsNullOrWhiteSpace(_settings.GlobalQuickCommand))
            {
                var clear = new ToolStripMenuItem("Genel hızlı komutu kaldır") { ForeColor = Theme.Text };
                clear.Click += (s, e) => { _settings.GlobalQuickCommand = null; RefreshQuickButton(); if (_saveSettings != null) _saveSettings(); };
                _quickMenu.Items.Add(clear);
            }
            _quickMenu.Show(_quickBtn, new Point(0, _quickBtn.Height));
        }

        private void EditQuick()
        {
            string v = InputDialog.Ask(FindForm(), "Genel hızlı komut",
                "Tıklayınca ekrandaki tüm bağlı kartlara gönderilecek komut:", _settings.GlobalQuickCommand ?? "", QuickCommandValidator.Validate);
            if (v == null) return;
            _settings.GlobalQuickCommand = v.Trim().Length == 0 ? null : v.Trim();
            RefreshQuickButton();
            if (_saveSettings != null) _saveSettings();
        }

        private async Task RunQuickAsync()
        {
            string cmd = (_settings.GlobalQuickCommand ?? "").Trim();
            if (cmd.Length == 0) { EditQuick(); return; }
            await BroadcastAsync(cmd, "genel hızlı komut", false);
        }

        // ---------- Genel reboot / reboot 2 ----------

        private void RefreshRebootButton()
        {
            string cmd = RebootCommands.CommandFor(_settings.GlobalRebootOption);
            _rebootBtn.MainText = cmd;
            _tips.SetToolTip(_rebootBtn, "Sol: \"" + cmd + "\" komutunu ekrandaki tüm bağlı kartlara birer kez gönderir (kartlar yeniden başlar).\n" +
                                         "Sağdaki ⇄: reboot ↔ reboot 2 seçimini değiştirir (hiçbir şey göndermez).");
            OnResize(EventArgs.Empty);
        }

        private void ToggleReboot()
        {
            _settings.GlobalRebootOption = RebootCommands.Toggle(_settings.GlobalRebootOption);   // yalnız seçim
            RefreshRebootButton();
            if (_saveSettings != null) _saveSettings();
        }

        private async Task RunRebootAsync()
        {
            if (!Guard.TryEnter(this, DateTime.UtcNow)) return;      // art arda tıklama: 4 sn içinde ikinci kez gitmez
            _rebootCooldown.Stop();
            _rebootCooldown.Start();
            UpdateTargets();
            await BroadcastAsync(RebootCommands.CommandFor(_settings.GlobalRebootOption), "genel reboot", false);
        }

        /// <summary>Komutu ekrandaki kartlara (her birine bir kez) gönderir ve sonucu gösterir.</summary>
        private async Task BroadcastAsync(string cmd, string source, bool fromInput)
        {
            if (_sending) return;
            IList<CardSession> targets = _targets();
            if (targets.Count == 0) { ShowResult("Ekranda açık kart yok — komut gönderilmedi.", Theme.Warning, null); return; }
            _sending = true;
            UpdateTargets();
            BroadcastResult r;
            try { r = await CommandBroadcaster.SendToAllAsync(targets, cmd, source); }
            finally { _sending = false; UpdateTargets(); }
            ReportBroadcast(cmd, r, fromInput);
        }

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (_autoComplete.HandleKeyDown(e)) return;   // Tab / ↓ / → / Esc
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                await SendAsync();
            }
            else if (e.KeyCode == Keys.Up && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string prev = _history.Previous(_input.Text);
                if (prev != null) _autoComplete.SetTextQuiet(prev);
            }
            else if (e.KeyCode == Keys.Down && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                string next = _history.Next();
                if (next != null) _autoComplete.SetTextQuiet(next);
            }
        }

        private async Task SendAsync()
        {
            if (_sending) return;
            IList<CardSession> targets = _targets();
            string cmd = _input.Text;

            if (targets.Count == 0)
            {
                ShowResult("Ekranda açık kart yok — komut gönderilmedi.", Theme.Warning, null);
                return;
            }

            _sending = true;
            UpdateTargets();
            BroadcastResult r;
            try
            {
                r = await CommandBroadcaster.SendToAllAsync(targets, cmd);
            }
            finally
            {
                _sending = false;
                UpdateTargets();
            }
            ReportBroadcast(cmd, r, true);
        }

        private void ReportBroadcast(string cmd, BroadcastResult r, bool fromInput)
        {
            if (r.Sent.Count > 0 && fromInput)
            {
                _history.Add(cmd);
                _input.Clear();
            }
            string prefix = fromInput ? "" : "\"" + cmd + "\": ";

            string failedList = string.Join(", ", r.Failed.Select(f => f.Key.Card.Ip + " (" + f.Value + ")"));
            if (r.Failed.Count == 0)
                ShowResult("✓  " + prefix + r.Sent.Count + "/" + r.Total + " karta gönderildi", Theme.Success, null);
            else if (r.Sent.Count == 0)
                ShowResult("✕  " + prefix + "Hiçbir karta gönderilemedi: " + failedList, Theme.Danger, failedList);
            else
                ShowResult("⚠  " + prefix + r.Sent.Count + "/" + r.Total + " karta gönderildi · gönderilemedi: " + failedList, Theme.Warning, failedList);
        }

        public void ShowResult(string text, Color color, string tooltip)
        {
            _result.Text = text;
            _result.ForeColor = color;
            _tips.SetToolTip(_result, tooltip ?? text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _tips.Dispose(); _quickMenu.Dispose(); _rebootCooldown.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
