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
        private static readonly Color BarBack = Color.FromArgb(22, 32, 50);
        private static readonly Color FieldBack = Color.FromArgb(12, 18, 30);
        private static readonly Color FieldBorder = Color.FromArgb(52, 92, 160);

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

        public GlobalCommandBar(Func<IList<CardSession>> openSessions)
        {
            _targets = openSessions;
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

            Controls.AddRange(new Control[] { _title, _targetInfo, _fieldBorder, _sendBtn, _result });

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
            int resultW = Width >= 1200 ? 320 : 190;   // sonuç her zaman görünür kalır
            int btnX = Width - 16 - resultW - 12 - _sendBtn.Width;
            _fieldBorder.SetBounds(fieldX, (h - 34) / 2 + 1, Math.Max(120, btnX - 10 - fieldX), 34);
            _sendBtn.Location = new Point(btnX, (h - 34) / 2 + 1);
            _result.SetBounds(Width - 16 - resultW, (h - 34) / 2 + 1, resultW, 34);
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

            if (r.Sent.Count > 0)
            {
                _history.Add(cmd);
                _input.Clear();
            }

            string failedList = string.Join(", ", r.Failed.Select(f => f.Key.Card.Ip + " (" + f.Value + ")"));
            if (r.Failed.Count == 0)
                ShowResult("✓  " + r.Sent.Count + "/" + r.Total + " karta gönderildi", Theme.Success, null);
            else if (r.Sent.Count == 0)
                ShowResult("✕  Hiçbir karta gönderilemedi: " + failedList, Theme.Danger, failedList);
            else
                ShowResult("⚠  " + r.Sent.Count + "/" + r.Total + " karta gönderildi · gönderilemedi: " + failedList, Theme.Warning, failedList);
        }

        private void ShowResult(string text, Color color, string tooltip)
        {
            _result.Text = text;
            _result.ForeColor = color;
            _tips.SetToolTip(_result, tooltip ?? text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
