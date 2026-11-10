using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Kart penceresinin eylemleri: hızlı komut, reboot / reboot 2, anlık terminal kaydı, komut dönüş değerleri
    /// paneli ve ortak yakınlaştırma. Hepsi YALNIZ bu kartı etkiler.
    /// </summary>
    public sealed partial class CardView
    {
        private static readonly RepeatGuard RebootGuard = new RepeatGuard(TimeSpan.FromSeconds(4));
        private static readonly RepeatGuard QuickGuard = new RepeatGuard(TimeSpan.FromMilliseconds(600));

        // Hızlı komut + reboot (komut satırının sağında)
        private readonly IconButton _quickBtn = new IconButton(IconKind.Bolt, IconTone.Secondary) { IconSize = 14 };
        private readonly SplitActionButton _rebootBtn = new SplitActionButton { MainIcon = IconKind.Power };
        private readonly ContextMenuStrip _quickMenu = new ContextMenuStrip();
        private readonly Timer _rebootCooldown = new Timer { Interval = 4000 };

        // Başlıktaki "Dosyaya kaydet" (anlık görüntü)
        private readonly IconButton _snapBtn = new IconButton(IconKind.Save, IconTone.Ghost);
        private readonly GlyphLabel _toast = new GlyphLabel { Visible = false, AutoSize = true, Padding = new Padding(10, 5, 12, 5), Font = Theme.UiSmall, Cursor = Cursors.Hand };
        private readonly Timer _toastTimer = new Timer { Interval = 5000 };
        private string _toastPath;

        // Komut dönüş değerleri (terminalden ayrılan "value = …" satırları)
        private readonly Panel _respBar = new Panel { Dock = DockStyle.Bottom, Height = 26, Visible = false, Cursor = Cursors.Hand };
        private readonly GlyphLabel _respSummary = new GlyphLabel { AutoSize = false, Font = Theme.UiSmall, ForeColor = Theme.TextMuted, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
        private readonly IconButton _respToggle = new IconButton(IconKind.ChevronUp, IconTone.Ghost) { IconSize = 12, TabStop = false };
        private readonly IconButton _respClear = new IconButton(IconKind.Clear, IconTone.Ghost) { IconSize = 12, TabStop = false };
        private readonly Panel _respPanel = new Panel { Dock = DockStyle.Bottom, Height = 132, Visible = false, Padding = new Padding(8, 4, 4, 4) };
        private readonly TextBox _respText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
        private int _shownRespVersion = -1;
        private bool _respExpanded;

        private float _zoom = AppSettings.DefaultTerminalZoom;

        /// <summary>Kartın kalıcı ayarı değişti (hızlı komut, reboot seçimi): yapılandırma kaydedilmeli.</summary>
        public event Action<CardView> ConfigChanged;

        /// <summary>Terminalde Ctrl + fare tekerleği: +1 büyüt / -1 küçült. Ortak yakınlaştırmayı değiştirir (tüm kartlar).</summary>
        public event Action<int> ZoomWheel;

        // =====================================================================
        // Oluşturma
        // =====================================================================

        private void BuildActions()
        {
            // --- Hızlı komut ve reboot (komut satırında)
            _quickBtn.Font = Theme.Ui;
            _quickBtn.Height = 24;
            _quickBtn.Click += async (s, e) => await RunQuickCommandAsync();
            _quickBtn.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowQuickMenu(); };
            _quickMenu.Renderer = new DarkMenuRenderer();
            _quickMenu.ShowImageMargin = false;

            _rebootBtn.Height = 24;
            _rebootBtn.MainClick += async (s, e) => await RunRebootAsync();
            _rebootBtn.ToggleClick += (s, e) => ToggleReboot();
            _rebootCooldown.Tick += (s, e) => { _rebootCooldown.Stop(); RefreshActionButtons(); };
            RefreshQuickButton();
            RefreshRebootButton();

            // --- Anlık kayıt
            _snapBtn.SetTip(_tips, "Dosyaya kaydet — terminalde şu an görünen içeriği bu kart için ayrı bir dosyaya kaydeder\n(sürekli kayıt başlatmaz; her tıklama yeni dosya)");
            _snapBtn.Click += async (s, e) => await SaveSnapshotAsync();
            _toast.Click += (s, e) =>
            {
                Action action = _toastAction;
                HideToast();
                if (action != null) action();
                else if (_toastPath != null) ShowInExplorer(_toastPath);
            };
            _toastTimer.Tick += (s, e) => HideToast();
            _outputHost.Controls.Add(_toast);
            _toast.BringToFront();

            // --- Dönüş değerleri çubuğu + paneli
            _respBar.BackColor = Theme.Surface;
            _respBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, _respBar.Width, 0); };
            _respToggle.Size = new Size(24, 22);
            _respClear.Size = new Size(24, 22);
            _respToggle.SetTip(_tips, "Komut dönüş değerlerini göster / gizle");
            _respClear.SetTip(_tips, "Dönüş değerleri listesini temizle (terminale dokunmaz)");
            _respBar.Controls.AddRange(new Control[] { _respSummary, _respToggle, _respClear });
            _respBar.Resize += (s, e) => LayoutRespBar();
            _respBar.Click += (s, e) => ToggleResponses();
            _respSummary.Click += (s, e) => ToggleResponses();
            _respToggle.Click += (s, e) => ToggleResponses();
            _respClear.Click += (s, e) => { Session.ClearResponses(); _shownRespVersion = -1; RefreshResponses(); };
            _tips.SetToolTip(_respSummary, "Komutların dönüş değerleri (\"value = …\") canlı loglardan ayrı burada tutulur.\nTıklayınca açılır / kapanır.");

            _respPanel.BackColor = Theme.Input;
            _respText.BackColor = Theme.Input;
            _respText.ForeColor = Theme.Terminal;
            _respText.Font = new Font("Consolas", 9f);
            Theme.ApplyDarkScrollBars(_respText);
            _respPanel.Controls.Add(_respText);
            _respPanel.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, _respPanel.Width, 0); };
        }

        private void LayoutRespBar()
        {
            int w = _respBar.Width;
            _respClear.Location = new Point(w - 4 - _respClear.Width, 2);
            _respToggle.Location = new Point(_respClear.Left - 2 - _respToggle.Width, 2);
            _respSummary.SetBounds(8, 0, Math.Max(40, _respToggle.Left - 12), _respBar.Height);
        }

        /// <summary>Komut satırı çubuğu daralınca: önce hızlı komut yalnız simge olur, sonra hızlı komut ve reboot gizlenir.</summary>
        private void LayoutInputActions()
        {
            int w = _inputBar.Width;
            bool show = w >= 330;
            _quickBtn.Visible = _rebootBtn.Visible = show;
            _quickGap.Visible = _rebootGap.Visible = show;
            _sentinelBtn.Visible = _sentinelGap.Visible = show;
            bool compact = w < 470;
            string cmd = (Session.Card.QuickCommand ?? "").Trim();
            _quickBtn.Label = compact ? "" : (cmd.Length == 0 ? "Hızlı komut" : cmd);
            Size ts = TextRenderer.MeasureText(_quickBtn.Label, _quickBtn.Font, Size.Empty, TextFormatFlags.NoPadding);
            _quickBtn.Width = compact ? 30 : Math.Min(150, Math.Max(90, ts.Width + 14 + 8 + 20));
            _rebootBtn.Width = _rebootBtn.GetPreferredSize(Size.Empty).Width;
        }

        // =====================================================================
        // Hızlı komut
        // =====================================================================

        private void RefreshQuickButton()
        {
            string cmd = (Session.Card.QuickCommand ?? "").Trim();
            _quickBtn.Icon = IconKind.Bolt;
            _quickBtn.SetTip(_tips, cmd.Length == 0
                ? "Hızlı komut ayarlanmadı — tıklayıp bu kart için bir komut atayın"
                : "Hızlı komut: " + cmd + "\nTıklayınca YALNIZ bu karta gönderilir.  ·  Sağ tık: düzenle / kaldır");
            _quickBtn.AccessibleName = cmd.Length == 0 ? "Hızlı komut ayarla" : "Hızlı komut: " + cmd;
            LayoutInputActions();
        }

        private void ShowQuickMenu()
        {
            _quickMenu.Items.Clear();
            var edit = new ToolStripMenuItem(string.IsNullOrWhiteSpace(Session.Card.QuickCommand) ? "Hızlı komut ata…" : "Hızlı komutu düzenle…") { ForeColor = Theme.Text };
            edit.Click += (s, e) => EditQuickCommand();
            _quickMenu.Items.Add(edit);
            if (!string.IsNullOrWhiteSpace(Session.Card.QuickCommand))
            {
                var clear = new ToolStripMenuItem("Hızlı komutu kaldır") { ForeColor = Theme.Text };
                clear.Click += (s, e) => { Session.Card.QuickCommand = null; RefreshQuickButton(); RaiseConfigChanged(); };
                _quickMenu.Items.Add(clear);
            }
            _quickMenu.Show(_quickBtn, new Point(0, _quickBtn.Height));
        }

        private void EditQuickCommand()
        {
            string current = Session.Card.QuickCommand ?? "";
            string value = InputDialog.Ask(FindForm(), "Hızlı komut — " + Session.Card.Label,
                "Bu kartın hızlı komut düğmesine tıklayınca YALNIZ bu karta gönderilecek komut:", current, QuickCommandValidator.Validate);
            if (value == null) return;
            Session.Card.QuickCommand = value.Trim().Length == 0 ? null : value.Trim();
            RefreshQuickButton();
            RaiseConfigChanged();
        }

        private async Task RunQuickCommandAsync()
        {
            string cmd = (Session.Card.QuickCommand ?? "").Trim();
            if (cmd.Length == 0) { EditQuickCommand(); return; }          // ayarlanmamışsa önce ata
            if (Session.State != CardState.Connected) { ShowToast("⚠  Kart bağlı değil — hızlı komut gönderilmedi.", false, null); return; }
            if (!QuickGuard.TryEnter(this, DateTime.UtcNow)) return;      // çift tıklamayla iki kez gitmesin
            await SendActionCommandAsync(cmd, "hızlı komut");
        }

        // =====================================================================
        // reboot / reboot 2
        // =====================================================================

        private void RefreshRebootButton()
        {
            string cmd = RebootCommands.CommandFor(Session.Card.RebootOption);
            _rebootBtn.MainText = cmd;
            _tips.SetToolTip(_rebootBtn, "Sol: \"" + cmd + "\" komutunu YALNIZ bu karta gönderir (kart yeniden başlar).\n" +
                                         "Sağdaki ⇄: reboot ↔ reboot 2 seçimini değiştirir (hiçbir şey göndermez).");
            LayoutInputActions();
        }

        private void ToggleReboot()
        {
            Session.Card.RebootOption = RebootCommands.Toggle(Session.Card.RebootOption);   // yalnız seçim; komut gönderilmez
            RefreshRebootButton();
            RaiseConfigChanged();
        }

        private async Task RunRebootAsync()
        {
            if (Session.State != CardState.Connected) { ShowToast("⚠  Kart bağlı değil — " + RebootCommands.CommandFor(Session.Card.RebootOption) + " gönderilmedi.", false, null); return; }
            // Yanlışlıkla art arda tıklama: 4 sn içinde ikinci kez çalışmaz, düğme bu sürede pasif kalır
            if (!RebootGuard.TryEnter(Session, DateTime.UtcNow)) return;
            string cmd = RebootCommands.CommandFor(Session.Card.RebootOption);
            _rebootCooldown.Stop();
            _rebootCooldown.Start();
            RefreshActionButtons();
            await SendActionCommandAsync(cmd, "reboot");
            ShowToast("✓  \"" + cmd + "\" gönderildi.", true, null);
        }

        private async Task SendActionCommandAsync(string cmd, string source)
        {
            Session.CommandHistory.Add(cmd);
            ScrollToEnd();
            HideNewLinesPill();
            OpenBootChangeIfNeeded(cmd);
            try
            {
                await Session.SendCommandAsync(cmd, source);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(Session.Card.DisplayName + " " + source, ex);
                ShowToast("✕  Gönderilemedi: " + ErrorText.Describe(ex), false, null);
            }
        }

        /// <summary>Bağlantıya ve reboot bekleme süresine göre düğmeleri etkinleştirir.</summary>
        private void RefreshActionButtons()
        {
            bool connected = Session.State == CardState.Connected;
            if (_quickBtn.Enabled != connected) _quickBtn.Enabled = connected;
            bool reboot = connected && !_rebootCooldown.Enabled;
            if (_rebootBtn.Enabled != reboot) _rebootBtn.Enabled = reboot;
        }

        private void RaiseConfigChanged()
        {
            var h = ConfigChanged;
            if (h != null) h(this);
        }

        // =====================================================================
        // Anlık terminal kaydı
        // =====================================================================

        /// <summary>Terminalde şu an görünen (satır sınırında tutulan) içerik. UI iş parçacığında çağrılmalı.</summary>
        public string CaptureTerminalText()
        {
            return _output.Text;
        }

        private async Task SaveSnapshotAsync()
        {
            string text = CaptureTerminalText();          // tıklama anındaki içerik (sonra gelen satırlar dahil değil)
            DateTime at = DateTime.Now;
            _snapBtn.Enabled = false;
            try
            {
                SnapshotResult r = await TerminalSnapshot.SaveAsync(Session.Card, text, at);
                if (r.Ok) ShowToast("✓  Kaydedildi: " + Path.GetFileName(r.Path) + "  (klasörü açmak için tıklayın)", true, r.Path);
                else ShowToast("✕  Kaydedilemedi: " + r.Error, false, null);
            }
            finally
            {
                _snapBtn.Enabled = true;
            }
        }

        public void ShowToast(string text, bool ok, string path)
        {
            _toastPath = path;
            _toastAction = null;
            _toast.Text = text;
            _toast.BackColor = ok ? Theme.SuccessBanner : Theme.ErrorBanner;
            _toast.ForeColor = ok ? Theme.SuccessBannerText : Color.White;
            _toast.MaximumSize = new Size(Math.Max(120, _outputHost.ClientSize.Width - 24), 0);
            _toast.Visible = true;
            _toast.BringToFront();
            PlaceToast();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            _toast.Visible = false;
        }

        private void PlaceToast()
        {
            if (!_toast.Visible) return;
            _toast.Location = new Point(Math.Max(4, _outputHost.ClientSize.Width - _toast.Width - 12), 8);
        }

        private static void ShowInExplorer(string path)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { ErrorText.LogTechnical("klasör aç", ex); }
        }

        // =====================================================================
        // Komut dönüş değerleri
        // =====================================================================

        private void ToggleResponses()
        {
            _respExpanded = !_respExpanded;
            _respPanel.Visible = _respExpanded && _respBar.Visible;
            _respToggle.Icon = _respExpanded ? IconKind.ChevronDown : IconKind.ChevronUp;
            if (_respExpanded) ScrollResponsesToEnd();
        }

        /// <summary>Oturumdaki dönüş değeri listesi değiştiyse çubuğu ve paneli günceller (Tick'ten).</summary>
        private void RefreshResponses()
        {
            int v = Session.ResponsesVersion;
            if (v == _shownRespVersion) return;
            _shownRespVersion = v;
            List<CommandResponse> list = Session.GetResponses();
            bool any = list.Count > 0;
            if (_respBar.Visible != any) { _respBar.Visible = any; _respPanel.Visible = any && _respExpanded; }
            if (!any) return;

            CommandResponse last = list[list.Count - 1];
            string summary = last.Command + "  →  " + (last.ReturnValue ?? (last.Completed ? "(" + (last.Note ?? "dönüş değeri yok") + ")" : "(bekleniyor…)"));
            _respSummary.Text = "ℹ  Dönüş (" + list.Count + "):  " + summary + "   ·  " + last.SentAtUtc.ToLocalTime().ToString("HH:mm:ss");
            _respSummary.ForeColor = last.ReturnValue != null ? Theme.Text : Theme.TextMuted;

            var sb = new StringBuilder();
            foreach (CommandResponse r in list.Skip(Math.Max(0, list.Count - 100)))
            {
                sb.Append(r.SentAtUtc.ToLocalTime().ToString("HH:mm:ss")).Append("  › ").Append(r.Command);
                if (r.Source != null && r.Source != "komut satırı") sb.Append("   [").Append(r.Source).Append("]");
                sb.Append("\r\n");
                foreach (string line in r.Lines) sb.Append("            ").Append(line).Append("\r\n");
                if (!r.Completed) sb.Append("            (dönüş bekleniyor…)\r\n");
                else if (r.Lines.Count == 0) sb.Append("            (").Append(r.Note ?? "dönüş değeri yok").Append(")\r\n");
                else if (r.Note != null) sb.Append("            (").Append(r.Note).Append(")\r\n");
            }
            _respText.Text = sb.ToString();
            ScrollResponsesToEnd();
        }

        private void ScrollResponsesToEnd()
        {
            if (!_respText.IsHandleCreated) return;
            _respText.SelectionStart = _respText.TextLength;
            _respText.SelectionLength = 0;
            _respText.ScrollToCaret();
        }

        // =====================================================================
        // Ortak yakınlaştırma
        // =====================================================================

        /// <summary>Terminal yazı boyutunu ortak yakınlaştırma oranına getirir (tüm kartlar aynı orandadır).</summary>
        public void SetZoom(float zoom)
        {
            zoom = AppSettings.ClampZoom(zoom);
            if (Math.Abs(zoom - _zoom) < 0.001f && _output.Font.Size > 0 && Math.Abs(_output.Font.Size - Theme.Mono.Size * zoom) < 0.05f) return;
            _zoom = zoom;
            bool follow = IsAtBottom();
            var font = new Font(Theme.Mono.FontFamily, Theme.Mono.Size * zoom, Theme.Mono.Style);
            // Yazı boyutu değişir, kalın kırmızı uyarı satırları ve renklendirmeler yeni boyutla yeniden boyanır; ekran oynamaz
            RecolorKeepingView(() =>
            {
                Font old = _output.Font;
                _output.Font = font;                         // RichTextBox: tüm metnin yazı boyutu, renkler korunur
                if (old != null && !ReferenceEquals(old, Theme.Mono)) old.Dispose();
                if (_alertFont != null) { _alertFont.Dispose(); _alertFont = null; }
            });
            if (follow) ScrollToEnd();
        }

        private void OnTerminalZoomWheel(int delta)
        {
            var h = ZoomWheel;
            if (h != null) h(delta > 0 ? 1 : -1);
        }

        /// <summary>
        /// Terminal kutusu: Ctrl + fare tekerleği Windows'un kart başına yakınlaştırması yerine ortak yakınlaştırmayı
        /// değiştirir (tüm kartlar birlikte büyür / küçülür).
        /// </summary>
        private sealed class TerminalBox : RichTextBox
        {
            private const int WM_MOUSEWHEEL = 0x020A;
            public Action<int> ZoomWheel;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEWHEEL && (ModifierKeys & Keys.Control) != 0)
                {
                    int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                    var h = ZoomWheel;
                    if (h != null && delta != 0) h(delta);
                    return;                                   // yerleşik (kart başına) yakınlaştırma çalışmasın
                }
                base.WndProc(ref m);
            }
        }
    }

    /// <summary>Hızlı komut doğrulaması (kart ve genel).</summary>
    public static class QuickCommandValidator
    {
        public const int MaxLength = 200;

        /// <summary>Geçerliyse null (boş = kaldır), değilse Türkçe hata.</summary>
        public static string Validate(string value)
        {
            string v = (value ?? "").Trim();
            if (v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0) return "Komut tek satır olmalı.";
            if (v.Length > MaxLength) return "Komut en fazla " + MaxLength + " karakter olabilir.";
            return null;
        }
    }
}
