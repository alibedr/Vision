using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Kart penceresindeki Sentinel düğmesi (G28): yalnız simge (nabız çizgisi), açıklaması ipucunda.
    /// Sunucu durmuşken tıklamak karta SentinelWebServerBaslat, çalışırken SentinelWebServerBitir gönderir.
    /// Simgenin rengi sunucunun durumunu gösterir: soluk = çalışmıyor, sarı = başlatılıyor / durduruluyor, yeşil = çalışıyor.
    /// Sağ tık: Sentinel sekmesinde aç.
    /// </summary>
    public sealed partial class CardView
    {
        private static readonly RepeatGuard SentinelGuard = new RepeatGuard(TimeSpan.FromSeconds(1));

        private readonly IconButton _sentinelBtn = new IconButton(IconKind.Pulse, IconTone.Secondary) { IconSize = 15 };
        private readonly Panel _sentinelGap = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.Transparent };
        private readonly ContextMenuStrip _sentinelMenu = new ContextMenuStrip();
        private int _shownSentinelVersion = -1;
        private SentinelStatus _shownSentinelStatus = SentinelStatus.Stopped;
        private bool _shownSentinelConnected;
        private Action _toastAction;   // bildirime tıklanınca (ör. Sentinel sekmesini aç)

        /// <summary>Kullanıcı bu kartın Sentinel sayfasını görmek istedi (Sentinel sekmesinde açılır).</summary>
        public event Action<CardView> SentinelRequested;

        private void BuildSentinel()
        {
            _sentinelBtn.Dock = DockStyle.Right;
            _sentinelBtn.Width = 30;
            _sentinelBtn.Height = 24;
            _sentinelBtn.Click += async (s, e) => await ToggleSentinelAsync();
            _sentinelBtn.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowSentinelMenu(); };
            _sentinelMenu.Renderer = new DarkMenuRenderer();
            _sentinelMenu.ShowImageMargin = false;
            RefreshSentinelButton(true);
        }

        /// <summary>Durum değiştiyse düğmeyi günceller (Tick'ten çağrılır).</summary>
        private void RefreshSentinelButton(bool force = false)
        {
            SentinelTracker t = Session.Sentinel;
            int version = t.Version;
            bool connected = Session.State == CardState.Connected;
            if (!force && version == _shownSentinelVersion && connected == _shownSentinelConnected) return;
            SentinelStatus before = _shownSentinelStatus;
            SentinelStatus st = t.Status;
            bool first = _shownSentinelVersion < 0;
            _shownSentinelVersion = version;
            _shownSentinelConnected = connected;
            _shownSentinelStatus = st;

            string url = SentinelTracker.UrlFor(Session.Card.Ip);
            switch (st)
            {
                case SentinelStatus.Running:
                    _sentinelBtn.IconColor = Theme.Success;
                    _sentinelBtn.SetTip(_tips, "Sentinel çalışıyor — " + url + "\nTıklayınca durdurur (karta " + SentinelTracker.StopCommand + " gönderilir).\nSağ tık: Sentinel sekmesinde aç");
                    break;
                case SentinelStatus.Starting:
                    _sentinelBtn.IconColor = Theme.Warning;
                    _sentinelBtn.SetTip(_tips, "Sentinel başlatılıyor… (port " + SentinelTracker.Port + " bekleniyor)");
                    break;
                case SentinelStatus.Stopping:
                    _sentinelBtn.IconColor = Theme.Warning;
                    _sentinelBtn.SetTip(_tips, "Sentinel durduruluyor…");
                    break;
                default:
                    _sentinelBtn.IconColor = Color.Empty;
                    _sentinelBtn.SetTip(_tips, connected
                        ? "Sentinel'i başlat — karta " + SentinelTracker.StartCommand + " gönderir.\nKartın sağlık sayfası (CPU, task, socket) " + url + " adresinde açılır ve Sentinel sekmesinde (Ctrl+4) görüntülenir."
                        : "Sentinel — kart bağlı değil");
                    break;
            }
            _sentinelBtn.AccessibleName = st == SentinelStatus.Running ? "Sentinel'i durdur" : "Sentinel'i başlat";
            bool busy = st == SentinelStatus.Starting || st == SentinelStatus.Stopping;
            bool enabled = connected && !busy;
            if (_sentinelBtn.Enabled != enabled) _sentinelBtn.Enabled = enabled;
            _sentinelBtn.Invalidate();

            if (first || !connected) return;
            // Geçiş bildirimleri
            string message = t.Message;
            if (before == SentinelStatus.Starting && st == SentinelStatus.Running)
                ShowActionToast("✓  Sentinel çalışıyor — görüntülemek için tıklayın (Sentinel sekmesi)", true, RequestSentinel);
            else if (before == SentinelStatus.Stopping && st == SentinelStatus.Stopped)
                ShowToast("✓  Sentinel durduruldu.", true, null);
            else if (message != null && (before == SentinelStatus.Starting || before == SentinelStatus.Stopping))
                ShowToast("⚠  " + message, false, null);
        }

        private async Task ToggleSentinelAsync()
        {
            if (Session.State != CardState.Connected) { ShowToast("⚠  Kart bağlı değil — Sentinel komutu gönderilmedi.", false, null); return; }
            SentinelTracker t = Session.Sentinel;
            SentinelStatus st = t.Status;
            if (st == SentinelStatus.Starting || st == SentinelStatus.Stopping) return;
            if (!SentinelGuard.TryEnter(Session, DateTime.UtcNow)) return;   // çift tıklama iki komut göndermesin

            if (st == SentinelStatus.Running)
            {
                t.BeginStop(DateTime.UtcNow);
                RefreshSentinelButton(true);
                await SendActionCommandAsync(SentinelTracker.StopCommand, "sentinel");
            }
            else
            {
                t.BeginStart(DateTime.UtcNow);
                RefreshSentinelButton(true);
                await SendActionCommandAsync(SentinelTracker.StartCommand, "sentinel");
            }
        }

        private void ShowSentinelMenu()
        {
            _sentinelMenu.Items.Clear();
            var open = new ToolStripMenuItem("Sentinel sekmesinde aç") { ForeColor = Theme.Text };
            open.Click += (s, e) => RequestSentinel();
            _sentinelMenu.Items.Add(open);
            _sentinelMenu.Items.Add(new ToolStripMenuItem(SentinelTracker.UrlFor(Session.Card.Ip)) { Enabled = false });
            _sentinelMenu.Show(_sentinelBtn, new Point(0, _sentinelBtn.Height));
        }

        private void RequestSentinel()
        {
            var h = SentinelRequested;
            if (h != null) h(this);
        }

        /// <summary>Tıklanınca verilen işi yapan bildirim (ShowToast'ın dosya yolu yerine eylemli hali).</summary>
        private void ShowActionToast(string text, bool ok, Action action)
        {
            ShowToast(text, ok, null);
            _toastAction = action;
        }
    }
}
