using System;
using System.Drawing;
using System.Windows.Forms;

namespace TelnetCards
{
    /// <summary>
    /// Komut satırına geçmişe dayalı otomatik tamamlama ekler. Mevcut CommandHistory kullanılır (ayrı geçmiş tutulmaz).
    /// Yazılanla başlayan en son komutun eksik kısmı, metnin hemen sağında soluk renkle gösterilir:
    ///   Tab  ·  ↓ (geçmişte gezinilmiyorsa)  ·  → (imleç sondaysa)  → öneri kabul edilir (komut ÇALIŞTIRILMAZ)
    ///   Esc  → öneri gizlenir;  yazmaya devam etmek öneriyi günceller;  Enter yalnız yazılanı gönderir.
    /// </summary>
    public sealed class InputAutoComplete
    {
        private readonly TextBox _box;
        private readonly Func<CommandHistory> _history;
        private readonly GhostText _ghost = new GhostText();
        private bool _suppress;
        private bool _dismissed;
        private string _suggestion;

        public InputAutoComplete(TextBox box, Func<CommandHistory> history)
        {
            _box = box;
            _history = history;
            _ghost.Font = box.Font;
            _ghost.ForeColor = Theme.TextFaint;
            _ghost.BackColor = box.BackColor;
            _ghost.Visible = false;
            box.Controls.Add(_ghost);

            box.TextChanged += (s, e) => { if (!_suppress) _dismissed = false; Refresh(); };
            box.KeyUp += (s, e) => Refresh();
            box.MouseUp += (s, e) => Refresh();
            box.GotFocus += (s, e) => Refresh();
            box.LostFocus += (s, e) => Refresh();
            box.Resize += (s, e) => Refresh();
            box.FontChanged += (s, e) => { _ghost.Font = box.Font; Refresh(); };
            box.BackColorChanged += (s, e) => _ghost.BackColor = box.BackColor;
            box.ReadOnlyChanged += (s, e) => Refresh();
            // Tab normalde odağı taşır; öneri görünürken kabul tuşu olsun
            box.PreviewKeyDown += (s, e) => { if (e.KeyCode == Keys.Tab && !e.Shift && _suggestion != null) e.IsInputKey = true; };
            _ghost.MouseDown += (s, e) => { box.Focus(); box.SelectionStart = box.TextLength; };
        }

        /// <summary>Şu an gösterilen öneri (tam komut); yoksa null.</summary>
        public string Suggestion { get { return _suggestion; } }

        /// <summary>
        /// Sahibin KeyDown işleyicisinin EN BAŞINDA çağrılır. Tuş öneriyle ilgiliyse işler ve true döner
        /// (sahip başka bir şey yapmamalı). Enter'a hiç dokunmaz.
        /// </summary>
        public bool HandleKeyDown(KeyEventArgs e)
        {
            if (_suggestion == null || e.Alt || e.Control) return false;
            bool atEnd = _box.SelectionLength == 0 && _box.SelectionStart == _box.TextLength;
            CommandHistory h = _history();

            bool accept = (e.KeyCode == Keys.Tab && !e.Shift) ||
                          (e.KeyCode == Keys.Right && atEnd && !e.Shift) ||
                          (e.KeyCode == Keys.Down && !e.Shift && (h == null || !h.IsNavigating));
            if (accept)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                Accept();
                return true;
            }
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                _dismissed = true;
                Refresh();
                return true;
            }
            return false;
        }

        /// <summary>Metni programdan ayarlar (ör. ↑/↓ geçmiş): kullanıcı yeniden yazana kadar öneri gösterilmez.</summary>
        public void SetTextQuiet(string text)
        {
            _suppress = true;
            try
            {
                _box.Text = text;
                _box.SelectionStart = _box.TextLength;
            }
            finally
            {
                _suppress = false;
            }
            _dismissed = true;
            Refresh();
        }

        private void Accept()
        {
            string s = _suggestion;
            _suppress = true;
            try
            {
                _box.Text = s;
                _box.SelectionStart = _box.TextLength;
            }
            finally
            {
                _suppress = false;
            }
            _dismissed = false;
            Refresh();
        }

        /// <summary>Öneriyi yeniden hesaplar ve soluk metni yazılanın hemen sağına yerleştirir.</summary>
        public void Refresh()
        {
            string typed = _box.Text;
            CommandHistory h = _history();
            bool atEnd = _box.SelectionLength == 0 && _box.SelectionStart == typed.Length;
            string s = null;
            if (!_dismissed && atEnd && _box.Focused && !_box.ReadOnly && typed.Length > 0 && h != null && !h.IsNavigating)
                s = h.Suggest(typed);
            _suggestion = s;

            if (s == null)
            {
                if (_ghost.Visible) _ghost.Visible = false;
                return;
            }

            // Son karakterin konumu + genişliği = soluk metnin başlangıcı
            Point p = _box.GetPositionFromCharIndex(typed.Length - 1);
            int w = TextRenderer.MeasureText(typed.Substring(typed.Length - 1), _box.Font, Size.Empty,
                                             TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            int x = p.X + w;
            if (x >= _box.ClientSize.Width - 6) { _ghost.Visible = false; return; }   // metin kutuya sığmıyor

            _ghost.Text = s.Substring(typed.Length);
            Size size = TextRenderer.MeasureText(_ghost.Text, _box.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            _ghost.Bounds = new Rectangle(x, Math.Max(0, p.Y), Math.Min(size.Width + 2, _box.ClientSize.Width - x), Math.Max(size.Height, _box.ClientSize.Height - Math.Max(0, p.Y)));
            _ghost.Invalidate();
            if (!_ghost.Visible) _ghost.Visible = true;
        }

        /// <summary>Kenar boşluğu olmadan metin çizen hafif denetim (Label'ın iç boşluğu hizayı bozardı).</summary>
        private sealed class GhostText : Control
        {
            public GhostText()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
                Cursor = Cursors.IBeam;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var bg = new SolidBrush(BackColor)) e.Graphics.FillRectangle(bg, ClientRectangle);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Point(0, 0), ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Invalidate();
            }
        }
    }
}
