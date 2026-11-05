using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Koyu temalı küçük pencereler için ortak taban. Pencerenin altındaki düğmeler (alttan 64 px içindekiler) açılışta
    /// ayrı renkli bir alt banda taşınır: içerik ve işlemler görsel olarak ayrılır.
    /// </summary>
    public abstract class DarkDialog : Form
    {
        protected const int FooterHeight = 64;

        protected DarkDialog(string title, Size clientSize)
        {
            ShowIcon = false;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            ClientSize = clientSize;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            BuildFooter();
            base.OnLoad(e);
        }

        private void BuildFooter()
        {
            int top = ClientSize.Height - FooterHeight;
            List<Control> bottom = Controls.Cast<Control>().Where(c => c.Dock == DockStyle.None && c.Top >= top - 2).ToList();
            if (bottom.Count == 0) return;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterHeight, BackColor = Theme.Sidebar };
            footer.Paint += (s, ev) => { using (var p = new Pen(Theme.Border)) ev.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            foreach (Control c in bottom)
            {
                Controls.Remove(c);
                c.Top = c is Button ? (FooterHeight - c.Height) / 2 : c.Top - top + 2;
                if (c is Label) c.BackColor = Color.Transparent;
                footer.Controls.Add(c);
            }
            Controls.Add(footer);
        }

        protected static Label Caption(string text, int x, int y)
        {
            var l = Theme.MakeLabel(text, Theme.SectionHeader, Theme.TextMuted);
            l.Location = new Point(x, y);
            return l;
        }
    }

    /// <summary>Tek satır metin isteyen pencere (yeni klasör adı, yeni ad, hedef klasör).</summary>
    public sealed class InputDialog : DarkDialog
    {
        private readonly TextBox _box = new TextBox();
        private readonly Label _error = Theme.MakeLabel("", Theme.UiSmall, Theme.Danger);
        private readonly Func<string, string> _validate;

        /// <param name="validate">Geçersizse hata metni, geçerliyse null döndürür.</param>
        public InputDialog(string title, string prompt, string initial, Func<string, string> validate)
            : base(title, new Size(460, 190))
        {
            _validate = validate;
            var p = Theme.MakeLabel(prompt, Theme.Ui, Theme.Text);
            p.Location = new Point(20, 18);
            p.MaximumSize = new Size(420, 0);
            Theme.StyleTextBox(_box);
            _box.Font = new Font("Consolas", 10.5f);
            _box.SetBounds(20, 52, 420, 28);
            _box.Text = initial ?? "";
            _error.Location = new Point(20, 88);
            _error.MaximumSize = new Size(420, 0);

            var ok = Theme.MakeButton("Tamam", Theme.ButtonKind.Primary);
            var cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
            ok.SetBounds(340, 140, 100, 32);
            cancel.SetBounds(232, 140, 100, 32);
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) =>
            {
                string err = _validate != null ? _validate(_box.Text) : null;
                if (err != null) { _error.Text = err; _box.BackColor = Color.FromArgb(70, 30, 34); return; }
                DialogResult = DialogResult.OK;
                Close();
            };
            _box.TextChanged += (s, e) => { _error.Text = ""; _box.BackColor = Theme.Input; };
            Controls.AddRange(new Control[] { p, _box, _error, ok, cancel });

            // Dosya adında uzantıdan önceki kısmı seç (yeniden adlandırmada kolaylık)
            Shown += (s, e) =>
            {
                _box.Focus();
                int dot = _box.Text.LastIndexOf('.');
                if (dot > 0) _box.Select(0, dot); else _box.SelectAll();
            };
        }

        public string Value { get { return _box.Text.Trim(); } }

        public static string Ask(IWin32Window owner, string title, string prompt, string initial, Func<string, string> validate)
        {
            using (var d = new InputDialog(title, prompt, initial, validate))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Value : null;
        }
    }

    /// <summary>Birden çok seçenekli onay penceresi. Seçilen düğmenin sırasını döndürür (iptal: -1).</summary>
    public sealed class ChoiceDialog : DarkDialog
    {
        private int _choice = -1;

        private static readonly Font MessageFont = new Font("Segoe UI Semibold", 10f);
        private readonly bool _danger;

        public ChoiceDialog(string title, string message, string detail, string[] buttons, int primaryIndex, int dangerIndex)
            : base(title, new Size(540, 200))
        {
            _danger = dangerIndex >= 0;
            const int textX = 80, textW = 436;
            var msg = Theme.MakeLabel(message, MessageFont, Theme.Text);
            msg.Location = new Point(textX, 22);
            msg.MaximumSize = new Size(textW, 0);
            msg.Size = TextRenderer.MeasureText(message, MessageFont, new Size(textW, 0), TextFormatFlags.WordBreak);
            Controls.Add(msg);
            int bottom = msg.Bottom;
            if (!string.IsNullOrEmpty(detail))
            {
                var det = Theme.MakeLabel(detail, Theme.Ui, Theme.TextMuted);
                det.Location = new Point(textX, msg.Bottom + 8);
                det.MaximumSize = new Size(textW, 0);
                det.Size = TextRenderer.MeasureText(detail, Theme.Ui, new Size(textW, 0), TextFormatFlags.WordBreak);
                Controls.Add(det);
                bottom = det.Bottom;
            }
            // İçeriğe göre yükseklik (simge en az 68 px), altta düğme bandı
            ClientSize = new Size(ClientSize.Width, Math.Max(68, bottom) + 26 + FooterHeight);

            int x = ClientSize.Width - 20;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                Theme.ButtonKind kind = i == dangerIndex ? Theme.ButtonKind.Danger : i == primaryIndex ? Theme.ButtonKind.Primary : Theme.ButtonKind.Secondary;
                var b = Theme.MakeButton(buttons[i], kind);
                int w = Math.Max(92, TextRenderer.MeasureText(buttons[i], Theme.UiBold).Width + 32);
                x -= w;
                b.SetBounds(x, ClientSize.Height - 48, w, 34);
                if (i == primaryIndex) AcceptButton = b;
                x -= 8;
                int index = i;
                b.Click += (s, e) => { _choice = index; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(b);
            }
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { _choice = -1; Close(); } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(_danger ? Color.FromArgb(70, 34, 38) : Color.FromArgb(30, 52, 88)))
                g.FillEllipse(b, 22, 18, 42, 42);
            IconRenderer.Draw(g, _danger ? IconKind.Warning : IconKind.Info, new RectangleF(32, 28, 22, 22), _danger ? Theme.Danger : Theme.Accent);
        }

        public static int Ask(IWin32Window owner, string title, string message, string detail, string[] buttons, int primaryIndex, int dangerIndex)
        {
            using (var d = new ChoiceDialog(title, message, detail, buttons, primaryIndex, dangerIndex))
            {
                d.ShowDialog(owner);
                return d._choice;
            }
        }
    }

    /// <summary>Kart üzerindeki metin dosyasını gösterir (salt okunur); bilgisayara kaydedilebilir.</summary>
    public sealed class TextViewerDialog : Form
    {
        public TextViewerDialog(string path, string content)
        {
            Text = path + "  —  Görüntüleyici";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 640);
            MinimumSize = new Size(500, 300);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            ShowInTaskbar = false;
            if (Theme.AppIcon != null) Icon = Theme.AppIcon;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            int lines = content.Length == 0 ? 0 : content.Split('\n').Length;
            var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Surface };
            var title = Theme.MakeLabel(path, new Font("Consolas", 10.5f, FontStyle.Bold), Theme.Text);
            title.Location = new Point(16, 8);
            var info = Theme.MakeLabel(lines + " satır  ·  " + content.Length + " karakter  ·  salt okunur", Theme.UiSmall, Theme.TextMuted);
            info.Location = new Point(17, 28);
            var wrap = new ToggleSwitch { Text = "Satır kaydır", Checked = false };
            Theme.StyleCheck(wrap);
            var save = Theme.MakeButton("Bilgisayara kaydet…", Theme.ButtonKind.Secondary);
            save.Width = 150;
            top.Controls.AddRange(new Control[] { title, info, wrap, save });
            top.Resize += (s, e) =>
            {
                save.Location = new Point(top.Width - save.Width - 14, 9);
                wrap.Location = new Point(save.Left - wrap.Width - 14, 15);
            };

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Input,
                ForeColor = Theme.Terminal,
                Font = Theme.Mono,
                Text = content.Replace("\n", Environment.NewLine),
            };
            Theme.ApplyDarkScrollBars(box);
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(10, 8, 4, 4) };
            host.Controls.Add(box);
            Controls.Add(host);
            Controls.Add(top);

            wrap.CheckedChanged += (s, e) =>
            {
                box.WordWrap = wrap.Checked;
                box.ScrollBars = wrap.Checked ? ScrollBars.Vertical : ScrollBars.Both;
            };
            save.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog { FileName = FsPath.LastSegment(path), Filter = "Tüm dosyalar|*.*" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    try { File.WriteAllText(dlg.FileName, box.Text, new UTF8Encoding(false)); }
                    catch (Exception ex)
                    {
                        ErrorText.LogTechnical("görüntüleyici kaydet", ex);
                        ChoiceDialog.Ask(this, "Kaydet", "Dosya kaydedilemedi.", "Klasöre yazma izni olduğundan ve dosyanın başka bir programda açık olmadığından emin olun.", new[] { "Tamam" }, 0, -1);
                    }
                }
            };
            Shown += (s, e) => { box.SelectionStart = 0; box.SelectionLength = 0; };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        /// <summary>İçerik metin değil (ikili) gibi görünüyor mu? Kontrol karakterlerinin oranına bakar.</summary>
        public static bool LooksBinary(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            int sample = Math.Min(content.Length, 8192), bad = 0;
            for (int i = 0; i < sample; i++)
            {
                char c = content[i];
                if (c < 32 && c != '\n' && c != '\t' && c != '\r') bad++;
            }
            return bad * 20 > sample;   // %5'ten fazla kontrol karakteri
        }
    }

    /// <summary>
    /// Kartın FTP bağlantısı. Sunucu, kullanıcı ve parola KARTTAN (version komutunun boot satırı) otomatik alınır ve yalnız
    /// gösterilir (parola gösterilmez). Kullanıcı yalnız tüm kartlar için ortak olan klasör / kip ayarlarını değiştirir.
    /// "Bağlantıyı sına" yalnız bağlanmayı değil, yazmayı ve kartın dosyayı görmesini de dener.
    /// </summary>
    public sealed class FtpSettingsDialog : DarkDialog
    {
        private readonly TransferSettings _s;
        private readonly CardConfig _card;
        private readonly CardSession _session;
        private readonly Func<bool, Task<CardFtpCredentials>> _ensure;
        private readonly Func<TransferSettings, IProgress<string>, Task<List<FileBrowser.FtpTestStep>>> _test;
        private readonly Func<bool> _canOperate;

        private readonly TextBox _dir = new TextBox(), _prefix = new TextBox();
        private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535 };
        private readonly CheckBox _passive = new ToggleSwitch { Text = "Pasif kip (çoğu ağda gerekli)" };
        private readonly CheckBox _delete = new ToggleSwitch { Text = "Kartta kopyalama başarılı olunca FTP'deki kopyayı hemen sil" };
        private readonly Label _hostValue = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _userValue = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _passValue = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _deviceValue = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _cardStatus = new GlyphLabel { Font = Theme.UiSmall, ForeColor = Theme.TextMuted, AutoSize = true, BackColor = Color.Transparent };
        private readonly Label _mapping = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Panel _steps = new Panel { BackColor = Color.Transparent };
        private readonly Button _refreshCard = Theme.MakeButton("Karttan yeniden al", IconKind.Refresh, Theme.ButtonKind.Secondary);
        private readonly Button _testBtn = Theme.MakeButton("Bağlantıyı sına", Theme.ButtonKind.Secondary);

        public FtpSettingsDialog(TransferSettings settings, CardConfig card, CardSession session,
                                 Func<bool, Task<CardFtpCredentials>> ensure,
                                 Func<TransferSettings, IProgress<string>, Task<List<FileBrowser.FtpTestStep>>> test,
                                 Func<bool> canOperate)
            : base("FTP — " + (card != null ? card.Label : "kart seçilmedi"), new Size(580, 600))
        {
            _s = settings; _card = card; _session = session; _ensure = ensure; _test = test; _canOperate = canOperate;

            var intro = Theme.MakeLabel(
                "FTP sunucusu, kullanıcı ve parola her kart için kartın kendisinden (version komutundaki boot satırı: h= / u= / pw=) otomatik alınır; elle girilmez. " +
                "Bilgiler yalnız bu oturumda bellekte tutulur, kaydedilmez.", Theme.Ui, Theme.TextMuted);
            intro.Location = new Point(20, 14);
            intro.MaximumSize = new Size(540, 0);
            Controls.Add(intro);

            // --- Kartın FTP bilgisi (salt okunur)
            int y = 64;
            var box = new Panel { BackColor = Theme.SurfaceHigh, Location = new Point(20, y), Size = new Size(540, 150) };
            box.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, box.Width - 1, box.Height - 1); };
            var cap = Caption("BU KARTIN FTP BİLGİSİ  ·  " + (card != null ? card.Label : "—"), 14, 12);
            box.Controls.Add(cap);
            string[] names = { "FTP sunucusu (h)", "Kullanıcı (u)", "Parola (pw)", "Kartın FTP yolu" };
            Label[] values = { _hostValue, _userValue, _passValue, _deviceValue };
            for (int i = 0; i < names.Length; i++)
            {
                var n = Theme.MakeLabel(names[i], Theme.Ui, Theme.TextMuted);
                n.Location = new Point(14, 36 + i * 22);
                values[i].Location = new Point(160, 36 + i * 22);
                values[i].BackColor = Color.Transparent; n.BackColor = Color.Transparent;
                box.Controls.Add(n); box.Controls.Add(values[i]);
            }
            _refreshCard.SetBounds(box.Width - 14 - 170, 32, 170, 30);
            _cardStatus.Location = new Point(14, 126);
            _cardStatus.MaximumSize = new Size(510, 0);
            box.Controls.Add(_refreshCard);
            box.Controls.Add(_cardStatus);
            Controls.Add(box);

            // --- Ortak ayarlar
            foreach (TextBox t in new[] { _dir, _prefix }) { Theme.StyleTextBox(t); t.Font = new Font("Segoe UI", 10f); }
            Theme.StyleNumeric(_port);
            Theme.StyleCheck(_passive);
            Theme.StyleCheck(_delete);
            y = 230;
            Controls.Add(Caption("FTP KLASÖRÜ (oturum açılan klasöre göre; boş = kök)", 20, y)); Controls.Add(Caption("PORT", 450, y));
            _dir.SetBounds(20, y + 18, 420, 28); _port.SetBounds(450, y + 18, 110, 28);
            y += 58;
            Controls.Add(Caption("KARTIN AYNI KLASÖRÜ GÖRDÜĞÜ YOL (boş = otomatik)", 20, y));
            _prefix.SetBounds(20, y + 18, 540, 28);
            _mapping.Location = new Point(20, y + 50);
            _mapping.MaximumSize = new Size(540, 0);
            y += 76;
            _passive.Location = new Point(20, y);
            _delete.Location = new Point(20, y + 26);
            _steps.SetBounds(20, y + 58, 540, 96);
            Controls.AddRange(new Control[] { _dir, _port, _prefix, _mapping, _passive, _delete, _steps });

            var ok = Theme.MakeButton("Kaydet", Theme.ButtonKind.Primary);
            var cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
            _testBtn.SetBounds(20, ClientSize.Height - 50, 150, 34);
            ok.SetBounds(ClientSize.Width - 120, ClientSize.Height - 50, 100, 34);
            cancel.SetBounds(ClientSize.Width - 228, ClientSize.Height - 50, 100, 34);
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { _testBtn, ok, cancel });

            _port.Value = Math.Max(1, Math.Min(65535, settings.FtpPort));
            _dir.Text = settings.FtpRemoteDir ?? "/";
            _prefix.Text = settings.CardPathPrefix ?? "";
            _passive.Checked = settings.UsePassive;
            _delete.Checked = settings.DeleteAfterCopy;
            ShowCredentials(session != null ? session.Ftp : null, null);
            Theme.SetCueBanner(_prefix, "Otomatik: /host.<kartın aygıtı> + FTP klasörü (ör. /host.host)");

            _dir.TextChanged += (s, e) => UpdateMapping();
            _prefix.TextChanged += (s, e) => UpdateMapping();
            ok.Click += (s, e) =>
            {
                Apply(_s);
                _s.ClearLegacyCredentials();          // eski sürümlerden kalan elle girilmiş parola dosyada kalmasın
                DialogResult = DialogResult.OK;
                Close();
            };
            _refreshCard.Click += async (s, e) => await ObtainAsync(true);
            _testBtn.Click += async (s, e) => await TestAsync();
            Shown += async (s, e) => { if (_session != null && _session.Ftp == null && _canOperate()) await ObtainAsync(false); };
        }

        private void ShowCredentials(CardFtpCredentials c, string error)
        {
            _hostValue.Text = c != null ? c.Host : "—";
            _userValue.Text = c != null ? c.User : "—";
            _passValue.Text = c != null ? CardFtpConfigurator.Mask + "  (gösterilmez)" : "—";
            _deviceValue.Text = c != null ? c.CardFtpRoot + "   (aygıt " + c.CardDevice + ")" : "—";
            if (error != null) { _cardStatus.ForeColor = Theme.Danger; _cardStatus.Text = "⚠  " + error; }
            else if (c != null) { _cardStatus.ForeColor = Theme.Success; _cardStatus.Text = "✓  Karttan alındı (" + c.ObtainedAtUtc.ToLocalTime().ToString("HH:mm:ss") + ")"; }
            else if (_card == null) { _cardStatus.ForeColor = Theme.TextMuted; _cardStatus.Text = "Dosya Gezgini'nde bir kart seçin."; }
            else { _cardStatus.ForeColor = Theme.TextMuted; _cardStatus.Text = "Henüz alınmadı — kart bağlıyken ilk FTP işleminde otomatik alınır."; }
            _refreshCard.Text = c != null ? "Karttan yeniden al" : "Karttan al";
            _refreshCard.Enabled = _card != null && _canOperate();
            UpdateMapping();
        }

        private void UpdateMapping()
        {
            var tmp = new TransferSettings();
            Apply(tmp);
            CardFtpCredentials c = _session != null ? _session.Ftp : null;
            string cardDir = tmp.CardFolderFor(c);
            _mapping.Text = "FTP'de  " + tmp.NormalizedRemoteDir + "  klasörüne gönderilen dosyayı kart  " + cardDir + "  yolundan kopyalayacak.";
        }

        private async Task ObtainAsync(bool force)
        {
            _refreshCard.Enabled = _testBtn.Enabled = false;
            _cardStatus.ForeColor = Theme.TextMuted;
            _cardStatus.Text = "Karttan alınıyor (" + CardFtpConfigurator.Command + ")…";
            CardFtpCredentials c = null;
            try { c = await _ensure(force); }
            catch (Exception ex) { ErrorText.LogTechnical("FTP bilgisi", ex); }
            ShowCredentials(c, c == null ? "Kartın FTP bilgisi alınamadı. Ayrıntı Dosya Gezgini'ndeki uyarı şeridinde ve \"Ham cevap\" panelinde." : null);
            _testBtn.Enabled = true;
        }

        private void Apply(TransferSettings t)
        {
            t.FtpPort = (int)_port.Value;
            t.FtpRemoteDir = _dir.Text.Trim().Length == 0 ? "/" : _dir.Text.Trim();
            t.CardPathPrefix = _prefix.Text.Trim();
            t.UsePassive = _passive.Checked;
            t.DeleteAfterCopy = _delete.Checked;
        }

        private async Task TestAsync()
        {
            _steps.Controls.Clear();
            if (_card == null || _session == null || !_canOperate()) { AddStep("⚠  Kart bağlı değil ya da Dosya Gezgini meşgul.", Theme.Warning); return; }
            _testBtn.Enabled = _refreshCard.Enabled = false;
            try
            {
                CardFtpCredentials c = _session.Ftp ?? await _ensure(false);
                ShowCredentials(c, c == null ? "Kartın FTP bilgisi alınamadı." : null);
                if (c == null) { AddStep("✕  Kartın FTP bilgisi alınamadı (version).", Theme.Danger); return; }
                var tmp = new TransferSettings();
                Apply(tmp);
                TransferSettings eff = tmp.ForCard(c);
                var progress = new Progress<string>(m => { _testBtn.Text = "Sınanıyor…"; });
                List<FileBrowser.FtpTestStep> steps = await _test(eff, progress);
                foreach (var st in steps)
                    AddStep((st.Skipped ? "ℹ  " : st.Ok ? "✓  " : "✕  ") + st.Name + (st.Detail != null ? " — " + st.Detail : ""),
                            st.Skipped ? Theme.TextMuted : st.Ok ? Theme.Success : Theme.Danger);
                if (steps.Count > 0 && steps.All(x => x.Ok)) AddStep("Yükleme için her şey hazır.", Theme.Success);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("FTP sınama", ex);
                AddStep("✕  " + ErrorText.Describe(ex), Theme.Danger);
            }
            finally
            {
                _testBtn.Text = "Bağlantıyı sına";
                _testBtn.Enabled = true;
                _refreshCard.Enabled = _card != null && _canOperate();
            }
        }

        private void AddStep(string text, Color color)
        {
            int y = _steps.Controls.Cast<Control>().Select(c => c.Bottom).DefaultIfEmpty(0).Max();
            var l = new GlyphLabel { Text = text, Font = Theme.UiSmall, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, MaximumSize = new Size(540, 0) };
            l.Location = new Point(0, y + 2);
            _steps.Controls.Add(l);
            new DarkToolTip().SetToolTip(l, text);
        }
    }
}
