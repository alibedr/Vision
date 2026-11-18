using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Genel komut çubuğundan birden çok karta bspBootChange / bootChange gönderilince açılan tek pencere (1.9, beta geri
    /// bildirimi: her kart için ayrı pencere açılıyordu). Komut bütün kartlara aynı anda gider. Solda kartlar ızgaradaki
    /// sırayla (grup, sonra IP) durumlarıyla listelenir; sağda seçilen kartın boot ekranı (<see cref="BootChangePanel"/>,
    /// tek kart penceresiyle aynı) görünür. Bir kart bitince sıradaki bitmemiş karta geçilir. Pencere yarıda kapatılırsa
    /// bitmemiş kartların kalan alanları değiştirilmeden geçilir: hiçbir kart boot ekranında bırakılmaz.
    /// </summary>
    public sealed class BootChangeMultiDialog : DarkDialog
    {
        private static BootChangeMultiDialog _open;
        private const int CloseWaitSeconds = 30;

        private readonly CardList _list = new CardList();
        private readonly Panel _host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        private readonly Label _summary = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly List<BootChangePanel> _panels = new List<BootChangePanel>();
        private readonly Timer _closeTimer = new Timer { Interval = 300 };
        private BootChangePanel _selected;
        private string _command;
        private bool _closing, _forceClose;
        private DateTime _closeDeadlineUtc;

        private BootChangeMultiDialog(string command)
            : base("Boot ayarları", new Size(1060, 640))
        {
            _command = command;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(880, 480);
            KeyPreview = true;
            if (Theme.AppIcon != null) { Icon = Theme.AppIcon; ShowIcon = true; }

            // --- Sol: kartlar
            var left = new Panel { Dock = DockStyle.Left, Width = 270, BackColor = Theme.Sidebar };
            left.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, left.Width - 1, 0, left.Width - 1, left.Height); };
            var caption = Theme.MakeLabel("KARTLAR", Theme.SectionHeader, Theme.TextMuted);
            caption.Location = new Point(14, 12);
            caption.BackColor = Color.Transparent;
            var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(6, 34, 7, 0) };
            _list.Dock = DockStyle.Fill;
            listHost.Controls.Add(_list);
            listHost.Controls.Add(caption);
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Theme.Sidebar };
            bottom.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
            _summary.AutoSize = false;
            _summary.BackColor = Color.Transparent;
            _summary.SetBounds(12, 8, 246, 58);
            bottom.Controls.Add(_summary);
            bottom.Resize += (s, e) => _summary.Width = bottom.ClientSize.Width - 22;
            left.Controls.Add(listHost);
            left.Controls.Add(bottom);
            Theme.ApplyDarkScrollBars(_list);

            Controls.Add(_host);
            Controls.Add(left);

            _list.SelectedIndexChanged += (s, e) =>
            {
                var p = _list.SelectedItem as BootChangePanel;
                if (p != null && p != _selected) ShowPanel(p, true);
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
                else if (_selected != null) _selected.HandleKey(e);
            };
            FormClosing += OnClosingAsk;
            FormClosed += (s, e) =>
            {
                _closeTimer.Stop();
                foreach (BootChangePanel p in _panels) p.Detach();
                if (_open == this) _open = null;
            };
            _closeTimer.Tick += (s, e) => CheckCloseWhenDone();
        }

        /// <summary>
        /// Genel komut çubuğu, komutu göndermeden HEMEN ÖNCE çağırır. Kendi Boot ayarları penceresi açık olan kart orada
        /// yeniden başlar; kalanlar bu pencerede toplanır (açıksa eklenir). Tek kart kalırsa tek kart penceresi açılır.
        /// </summary>
        public static void Begin(IList<CardSession> sessions, IWin32Window owner, string command)
        {
            var forMulti = new List<CardSession>();
            int cascade = 0;
            foreach (CardSession s in sessions.Where(x => x != null).Distinct())
            {
                BootChangePanel existing = BootChangePanel.Find(s);
                if (existing != null && existing.FindForm() is BootChangeDialog) BootChangeDialog.Begin(s, owner, command, cascade++);
                else forMulti.Add(s);
            }
            if (forMulti.Count == 0) return;

            BootChangeMultiDialog d = _open != null && !_open.IsDisposed ? _open : null;
            if (d == null && forMulti.Count == 1) { BootChangeDialog.Begin(forMulti[0], owner, command, cascade); return; }

            bool created = d == null;
            if (created)
            {
                d = new BootChangeMultiDialog(command);
                _open = d;
                var ownerForm = owner as Form;
                if (ownerForm != null)
                {
                    d.StartPosition = FormStartPosition.Manual;
                    Rectangle area = Screen.FromControl(ownerForm).WorkingArea;
                    int x = ownerForm.Left + (ownerForm.Width - d.Width) / 2, y = ownerForm.Top + (ownerForm.Height - d.Height) / 2;
                    d.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - d.Width)), Math.Max(area.Top, Math.Min(y, area.Bottom - d.Height)));
                }
            }
            d._command = command;
            BootChangePanel first = null;
            foreach (CardSession s in forMulti)
            {
                BootChangePanel p = d._panels.FirstOrDefault(x => x.Session == s);
                if (p != null) p.Start(command);
                else p = d.AddCard(s, command);
                if (first == null) first = p;
            }
            d.RefreshTitle();
            d.RefreshSummary();
            d.ShowPanel(first, false);
            if (created) { if (owner != null) d.Show(owner); else d.Show(); }
            else
            {
                if (d.WindowState == FormWindowState.Minimized) d.WindowState = FormWindowState.Normal;
                d.Activate();
            }
        }

        /// <summary>Kart bu penceredeyse komutu orada yeniden başlatır ve karta geçer (kart terminalinden yeniden yazılınca).</summary>
        internal static bool Restart(BootChangePanel panel, string command)
        {
            var d = panel.FindForm() as BootChangeMultiDialog;
            if (d == null || d.IsDisposed) return false;
            panel.Start(command);
            d.ShowPanel(panel, true);
            if (d.WindowState == FormWindowState.Minimized) d.WindowState = FormWindowState.Normal;
            d.Activate();
            return true;
        }

        private BootChangePanel AddCard(CardSession s, string command)
        {
            var p = new BootChangePanel(s, command) { Dock = DockStyle.Fill, Visible = false };
            p.StateChanged += x => { _list.InvalidatePanel(x); RefreshSummary(); };
            p.Finished += OnPanelFinished;
            p.CloseRequested += x => BeginInvoke(new Action(Close));
            _panels.Add(p);
            _host.Controls.Add(p);
            _list.Items.Add(p);
            return p;
        }

        private void ShowPanel(BootChangePanel p, bool focus)
        {
            if (p == null) return;
            _selected = p;
            foreach (BootChangePanel x in _panels) x.Visible = x == p;
            p.BringToFront();
            if (_list.SelectedItem != p) _list.SelectedItem = p;
            if (focus) p.FocusInput();
        }

        /// <summary>Gösterilen kart bitince sıradaki bitmemiş karta geçilir (önce aşağıdakiler, sonra baştan).</summary>
        private void OnPanelFinished(BootChangePanel p)
        {
            _list.InvalidatePanel(p);
            RefreshSummary();
            if (_closing) { CheckCloseWhenDone(); return; }
            if (p != _selected) return;
            int i = _panels.IndexOf(p);
            for (int k = 1; k < _panels.Count; k++)
            {
                BootChangePanel next = _panels[(i + k) % _panels.Count];
                if (!next.IsOver) { BeginInvoke(new Action(() => ShowPanel(next, ContainsFocus))); return; }
            }
        }

        private void RefreshTitle()
        {
            Text = "Boot ayarları  —  " + _command + "  —  " + _panels.Count + " kart";
        }

        private void RefreshSummary()
        {
            int done = _panels.Count(p => p.Succeeded);
            int failed = _panels.Count(p => p.IsOver && !p.Succeeded);
            int waiting = _panels.Count(p => !p.IsOver && p.AwaitingUser);
            int running = _panels.Count - done - failed - waiting;
            var parts = new List<string> { _panels.Count + " kart", done + " tamamlandı" };
            if (waiting > 0) parts.Add(waiting + " giriş bekliyor");
            if (running > 0) parts.Add(running + " sürüyor");
            if (failed > 0) parts.Add(failed + " yapılamadı");
            _summary.Text = string.Join("  ·  ", parts) + "\n" +
                            (_closing ? "Kalan alanlar değiştirilmeden geçiliyor; bitince pencere kapanır…"
                                      : "Bir karta tıklayıp ona geçin. Biten kartın ardından sıradakine geçilir.");
            _summary.ForeColor = _closing ? Theme.Warning : Theme.TextMuted;
        }

        // ---------- Kapatma ----------

        private void OnClosingAsk(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing || _forceClose) return;   // uygulama kapanıyor / bitirildi
            if (_closing) { e.Cancel = true; return; }                             // kalanlar geçiliyor: bitince kapanır
            List<BootChangePanel> unfinished = _panels.Where(p => p.IsUnfinished).ToList();
            if (unfinished.Count == 0) return;
            string names = string.Join(", ", unfinished.Take(6).Select(p => p.Session.Card.Label)) + (unfinished.Count > 6 ? " …" : "");
            int c = ChoiceDialog.Ask(this, "Boot ayarları sürüyor",
                (unfinished.Count == 1 ? "1 kartta" : unfinished.Count + " kartta") + " boot ayarları bitmedi.",
                names + "\n\nBu kartlarda kalan alanlar değiştirilmeden geçilip komut bitirilir; o ana kadar gönderdiğiniz değerler " +
                "kaydedilir. Böylece hiçbir kart boot ekranında beklemez. Bitince pencere kendiliğinden kapanır.",
                new[] { "Kalanları değiştirmeden bitir ve kapat", "Vazgeç" }, 0, -1);
            e.Cancel = true;
            if (c != 0) return;
            _closing = true;
            _closeDeadlineUtc = DateTime.UtcNow.AddSeconds(CloseWaitSeconds);
            foreach (BootChangePanel p in unfinished) p.FinishRest();
            RefreshSummary();
            _closeTimer.Start();
        }

        /// <summary>Kapatma istendikten sonra: bütün kartlar bitince (ya da en geç 30 sn sonra) pencere kapanır.</summary>
        private void CheckCloseWhenDone()
        {
            if (!_closing || IsDisposed) return;
            if (_panels.Any(p => p.IsUnfinished) && DateTime.UtcNow < _closeDeadlineUtc) return;
            _closeTimer.Stop();
            _forceClose = true;
            BeginInvoke(new Action(Close));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _closeTimer.Dispose();
            base.Dispose(disposing);
        }

        // =====================================================================

        /// <summary>Kart listesi: durum noktası, kartın adı / IP'si ve kısa durum.</summary>
        private sealed class CardList : ListBox
        {
            private readonly Font _bold = new Font("Segoe UI Semibold", 9.75f);

            public CardList()
            {
                BorderStyle = BorderStyle.None;
                DrawMode = DrawMode.OwnerDrawFixed;
                IntegralHeight = false;
                ItemHeight = 50;
                BackColor = Theme.Sidebar;
                ForeColor = Theme.Text;
                Font = Theme.Ui;
            }

            public void InvalidatePanel(BootChangePanel p)
            {
                int i = Items.IndexOf(p);
                if (i >= 0) Invalidate(GetItemRectangle(i));
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                var p = (BootChangePanel)Items[e.Index];
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Graphics g = e.Graphics;
                using (var b = new SolidBrush(selected ? Theme.ListSelectedStrong : BackColor)) g.FillRectangle(b, e.Bounds);
                Color dot = p.Session.State != CardState.Connected && !p.IsOver ? Theme.TextFaint : p.ListColor;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(dot)) g.FillEllipse(b, e.Bounds.X + 8, e.Bounds.Y + 11, 9, 9);
                Color fore = selected ? Theme.ListSelectedText : Theme.Text;
                Color sub = selected ? Theme.ListSelectedText : p.ListColor == Theme.TextMuted ? Theme.TextMuted : p.ListColor;
                const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(g, p.Session.Card.Label, _bold, new Rectangle(e.Bounds.X + 24, e.Bounds.Y + 6, e.Bounds.Width - 30, 20), fore, f);
                TextRenderer.DrawText(g, p.ListStatus, Theme.UiSmall, new Rectangle(e.Bounds.X + 24, e.Bounds.Y + 27, e.Bounds.Width - 30, 16), sub, f);
                using (var pen = new Pen(Theme.RowDivider)) g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); Invalidate(); }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _bold.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
