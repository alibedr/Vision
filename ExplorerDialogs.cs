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

    /// <summary>Bilgisayardan karta yükleme için FTP ayarları.</summary>
    public sealed class FtpSettingsDialog : DarkDialog
    {
        private readonly TransferSettings _s;
        private readonly TextBox _host = new TextBox(), _user = new TextBox(), _pass = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox _dir = new TextBox(), _prefix = new TextBox();
        private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535 };
        private readonly CheckBox _passive = new ToggleSwitch { Text = "Pasif kip (çoğu ağda gerekli)" };
        private readonly CheckBox _delete = new ToggleSwitch { Text = "Kartta kopyalama başarılı olunca FTP'deki kopyayı sil" };
        private readonly Label _testResult = new GlyphLabel { Font = Theme.UiSmall, ForeColor = Theme.TextMuted, AutoSize = true, BackColor = Color.Transparent };

        public FtpSettingsDialog(TransferSettings settings)
            : base("FTP Ayarları — Karta Yükleme", new Size(560, 470))
        {
            _s = settings;
            var intro = Theme.MakeLabel(
                "Dosyalar önce kartın erişebildiği FTP sunucusuna gönderilir, sonra kartta copy (dosya) / xcopy (klasör) " +
                "ile o konumdan hedef klasöre kopyalanır.", Theme.Ui, Theme.TextMuted);
            intro.Location = new Point(20, 14);
            intro.MaximumSize = new Size(520, 0);
            Controls.Add(intro);

            foreach (TextBox t in new[] { _host, _user, _pass, _dir, _prefix }) { Theme.StyleTextBox(t); t.Font = new Font("Segoe UI", 10f); }
            Theme.StyleNumeric(_port);
            Theme.StyleCheck(_passive);
            Theme.StyleCheck(_delete);

            int y = 62;
            Controls.Add(Caption("FTP SUNUCUSU", 20, y)); Controls.Add(Caption("PORT", 420, y));
            _host.SetBounds(20, y + 18, 390, 28); _port.SetBounds(420, y + 18, 120, 28);
            y += 56;
            Controls.Add(Caption("KULLANICI ADI", 20, y)); Controls.Add(Caption("PAROLA", 285, y));
            _user.SetBounds(20, y + 18, 255, 28); _pass.SetBounds(285, y + 18, 255, 28);
            y += 56;
            Controls.Add(Caption("FTP KLASÖRÜ (dosyaların gönderileceği yer)", 20, y));
            _dir.SetBounds(20, y + 18, 520, 28);
            y += 56;
            Controls.Add(Caption("KARTIN AYNI KLASÖRÜ GÖRDÜĞÜ YOL (copy / xcopy kaynağı)", 20, y));
            _prefix.SetBounds(20, y + 18, 520, 28);
            var example = Theme.MakeLabel("Örnek: FTP klasörü  /upload   →   kartın gördüğü yol  host:/upload", Theme.UiSmall, Theme.TextFaint);
            example.Location = new Point(20, y + 50);
            y += 74;
            _passive.Location = new Point(20, y);
            _delete.Location = new Point(20, y + 26);
            Controls.AddRange(new Control[] { _host, _port, _user, _pass, _dir, _prefix, example, _passive, _delete });

            var test = Theme.MakeButton("Bağlantıyı sına", Theme.ButtonKind.Secondary);
            var ok = Theme.MakeButton("Kaydet", Theme.ButtonKind.Primary);
            var cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
            test.SetBounds(20, ClientSize.Height - 50, 140, 32);
            _testResult.Location = new Point(168, ClientSize.Height - 42);
            _testResult.MaximumSize = new Size(180, 0);
            ok.SetBounds(ClientSize.Width - 120, ClientSize.Height - 50, 100, 32);
            cancel.SetBounds(ClientSize.Width - 228, ClientSize.Height - 50, 100, 32);
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { test, _testResult, ok, cancel });

            _host.Text = settings.FtpHost ?? "";
            _port.Value = Math.Max(1, Math.Min(65535, settings.FtpPort));
            _user.Text = settings.FtpUser ?? "";
            _pass.Text = settings.Password;
            _dir.Text = settings.FtpRemoteDir ?? "/";
            _prefix.Text = settings.CardPathPrefix ?? "host:";
            _passive.Checked = settings.UsePassive;
            _delete.Checked = settings.DeleteAfterCopy;

            ok.Click += (s, e) =>
            {
                if (_host.Text.Trim().Length == 0 || _prefix.Text.Trim().Length == 0)
                {
                    _testResult.ForeColor = Theme.Danger;
                    _testResult.Text = "FTP sunucusu ve kartın gördüğü yol boş olamaz.";
                    return;
                }
                Apply(_s);
                DialogResult = DialogResult.OK;
                Close();
            };
            test.Click += async (s, e) => await TestAsync(test);
        }

        private void Apply(TransferSettings t)
        {
            t.FtpHost = _host.Text.Trim();
            t.FtpPort = (int)_port.Value;
            t.FtpUser = _user.Text.Trim();
            t.Password = _pass.Text;
            t.FtpRemoteDir = _dir.Text.Trim().Length == 0 ? "/" : _dir.Text.Trim();
            t.CardPathPrefix = _prefix.Text.Trim();
            t.UsePassive = _passive.Checked;
            t.DeleteAfterCopy = _delete.Checked;
        }

        private async Task TestAsync(Button test)
        {
            var tmp = new TransferSettings();
            Apply(tmp);
            if (string.IsNullOrWhiteSpace(tmp.FtpHost)) { _testResult.ForeColor = Theme.Danger; _testResult.Text = "FTP sunucusu boş."; return; }
            test.Enabled = false;
            _testResult.ForeColor = Theme.TextMuted;
            _testResult.Text = "Sınanıyor…";
            try
            {
                await new FtpUploader(tmp).TestAsync();
                _testResult.ForeColor = Theme.Success;
                _testResult.Text = "✓ Bağlanıldı, klasör okunabildi.";
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("FTP sına", ex);
                _testResult.ForeColor = Theme.Danger;
                var we = ex as System.Net.WebException;
                var r = we != null ? we.Response as System.Net.FtpWebResponse : null;
                _testResult.Text = r != null && r.StatusCode == System.Net.FtpStatusCode.NotLoggedIn ? "Kullanıcı adı ya da parola hatalı."
                    : r != null ? "Klasöre erişilemedi (FTP " + (int)r.StatusCode + ")."
                    : "FTP sunucusuna bağlanılamadı.";
            }
            finally
            {
                test.Enabled = true;
            }
        }
    }
}
