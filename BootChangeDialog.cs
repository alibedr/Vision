using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Tek kartın Boot ayarları penceresi (G27): kart terminalinden bspBootChange / bootChange gönderilince açılır.
    /// İçerik <see cref="BootChangePanel"/>'dir (çok kartlı pencereyle aynı ekran). Genel komut çubuğundan birden çok
    /// karta gönderilince bu pencere değil <see cref="BootChangeMultiDialog"/> açılır (1.9).
    /// </summary>
    public sealed partial class BootChangeDialog : DarkDialog
    {
        private static readonly Dictionary<CardSession, BootChangeDialog> OpenDialogs = new Dictionary<CardSession, BootChangeDialog>();

        private readonly CardSession _session;
        private readonly BootChangePanel _panel;
        private bool _closeWhenDone;

        private BootChangeDialog(CardSession session, string command)
            : base("Boot ayarları  —  " + session.Card.Label, new Size(780, 600))
        {
            _session = session;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(620, 460);
            KeyPreview = true;
            if (Theme.AppIcon != null) { Icon = Theme.AppIcon; ShowIcon = true; }

            _panel = new BootChangePanel(session, command) { Dock = DockStyle.Fill };
            Controls.Add(_panel);
            _panel.CloseRequested += p => Close();
            // "Kalanları geçip bitir" ile kapatma istendiyse komut bitince pencere kapanır
            _panel.Finished += p => { if (_closeWhenDone) { _closeWhenDone = false; BeginInvoke(new Action(Close)); } };

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
                else _panel.HandleKey(e);
            };
            FormClosing += OnClosingAsk;
            FormClosed += (s, e) =>
            {
                _panel.Detach();
                BootChangeDialog d;
                if (OpenDialogs.TryGetValue(_session, out d) && d == this) OpenDialogs.Remove(_session);
            };
            Shown += (s, e) => _panel.FocusInput();
        }

        /// <summary>
        /// Kart terminali bspBootChange / bootChange göndermeden HEMEN ÖNCE çağırır: pencere açılır (açıksa yeniden başlar)
        /// ve kartın cevabı izlenmeye başlar. Kart çok kartlı pencerede ise orada yeniden başlar.
        /// </summary>
        public static void Begin(CardSession session, IWin32Window owner, string command, int cascade = 0)
        {
            BootChangePanel other = BootChangePanel.Find(session);
            if (other != null && !(other.FindForm() is BootChangeDialog) && BootChangeMultiDialog.Restart(other, command)) return;

            BootChangeDialog d;
            if (OpenDialogs.TryGetValue(session, out d) && !d.IsDisposed)
            {
                d._closeWhenDone = false;
                d._panel.Start(command);
                if (d.WindowState == FormWindowState.Minimized) d.WindowState = FormWindowState.Normal;
                d.Activate();
                return;
            }
            d = new BootChangeDialog(session, command);
            OpenDialogs[session] = d;
            // Pencere sahibinin ortasında (birden çok pencere: her biri biraz kaydırılarak)
            var ownerForm = owner as Form;
            if (ownerForm != null)
            {
                d.StartPosition = FormStartPosition.Manual;
                int step = 28 * (cascade % 8);
                Rectangle area = Screen.FromControl(ownerForm).WorkingArea;
                int x = ownerForm.Left + (ownerForm.Width - d.Width) / 2 + step;
                int y = ownerForm.Top + (ownerForm.Height - d.Height) / 2 + step;
                d.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - d.Width)), Math.Max(area.Top, Math.Min(y, area.Bottom - d.Height)));
            }
            if (owner != null) d.Show(owner); else d.Show();
        }

        private void OnClosingAsk(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;   // uygulama kapanıyor: sorulmaz
            if (!_panel.NeedsCloseConfirm) return;
            int c = ChoiceDialog.Ask(this, "Boot ayarları sürüyor",
                "Kart hâlâ \"" + _panel.CurrentFieldLabel + "\" alanı için giriş bekliyor.",
                "Kalan alanları değiştirmeden geçip bitirebilir ya da yalnız pencereyi kapatıp terminalden devam edebilirsiniz.",
                new[] { "Kalanları geçip bitir", "Yalnız pencereyi kapat", "Vazgeç" }, 0, -1);
            if (c == 0) { e.Cancel = true; _closeWhenDone = true; _panel.FinishRest(); }
            else if (c != 1) e.Cancel = true;
        }
    }
}
