using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vision
{
    public enum TransferDirection { ToCard, FromCard, InCard }

    /// <summary>
    /// Kart ile bilgisayar arasındaki aktarımın ilerlemesi (G40 / G41). Aktarım farklı iş parçacıklarından yazar, ilerleme
    /// penceresi zamanlayıcıyla okur. Her bayt iki aşamadan geçer: FTP aşaması (bilgisayar ↔ FTP sunucusu, bayt bayt
    /// izlenir) ve kart aşaması (kart ↔ FTP, 8 MB'lık bloklarla ya da adım bitince ilerler). Toplam ilerleme bu iki
    /// aşamanın ortalamasıdır. Hızlar o an ölçülür (FTP: son birkaç saniye; kart: son bloklar / son parça) ve
    /// saklanmaz; kalan süre bu anlık hızlarla hesaplanır.
    /// </summary>
    public sealed class TransferProgress
    {
        private const double RateWindowSec = 6;

        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Queue<KeyValuePair<DateTime, long>> _ftpSamples = new Queue<KeyValuePair<DateTime, long>>();
        private readonly Queue<KeyValuePair<DateTime, long>> _cardSamples = new Queue<KeyValuePair<DateTime, long>>();
        private long _total, _ftpBase, _cardBase, _stepDone, _stepTotal, _itemFtp, _itemCard;
        private double _ftpRate, _cardRate;
        private string _item = "", _stage = "";
        private bool _onCard, _ftpStep, _cardStep;
        private int _fileCount;
        private DateTime _startUtc = DateTime.UtcNow, _stepStartUtc = DateTime.UtcNow;

        public TransferProgress(TransferDirection direction) { Direction = direction; }

        public TransferDirection Direction { get; private set; }
        public volatile bool Finished;

        public CancellationToken Token { get { return _cts.Token; } }
        public bool CancelRequested { get { return _cts.IsCancellationRequested; } }

        /// <summary>İptal: FTP aktarımı hemen kesilir, kartta en geç bir blok sonra durulur.</summary>
        public void Cancel()
        {
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        public sealed class Snapshot
        {
            public TransferDirection Direction;
            public long Total, FtpDone, CardDone, StepDone, StepTotal;
            public string Item, Stage;
            public bool OnCard, StepHasBytes;
            public int FileCount;
            public TimeSpan Elapsed;
            /// <summary>Anlık hızlar (bayt/sn); henüz ölçülmediyse 0.</summary>
            public double FtpRate, CardRate;
            /// <summary>Anlık hızlarla kalan süre; hesaplanamıyorsa null.</summary>
            public TimeSpan? Remaining;

            /// <summary>0..1: FTP ve kart aşamalarının ortalaması (kart içi işlemde yalnız kart).</summary>
            public double Fraction
            {
                get
                {
                    if (Total <= 0) return 0;
                    if (Direction == TransferDirection.InCard) return Math.Min(1.0, CardDone / (double)Total);
                    return Math.Min(1.0, (FtpDone + CardDone) / (2.0 * Total));
                }
            }
        }

        public void Begin(long totalBytes, int fileCount)
        {
            lock (_lock) { _total = Math.Max(0, totalBytes); _fileCount = fileCount; _startUtc = DateTime.UtcNow; }
        }

        /// <summary>Yeni öğe (dosya / klasör) başlıyor.</summary>
        public void BeginItem(string text)
        {
            lock (_lock)
            {
                _item = text ?? "";
                _itemFtp = _ftpBase;
                _itemCard = _cardBase;
                _stage = "";
                _stepDone = _stepTotal = 0;
                _onCard = _ftpStep = _cardStep = false;
            }
        }

        /// <summary>Öğe bitti (başarılı da olsa başarısız da olsa): öğenin baytları iki aşamada da tamamlanmış sayılır.</summary>
        public void CompleteItem(long bytes)
        {
            lock (_lock)
            {
                _ftpBase = _itemFtp + bytes;
                _cardBase = _itemCard + bytes;
                _stepDone = _stepTotal = 0;
                _onCard = _ftpStep = _cardStep = false;
            }
        }

        public void StartFtp(string stage, long bytes)
        {
            lock (_lock) { _stage = stage ?? ""; _stepTotal = bytes; _stepDone = 0; _onCard = _cardStep = false; _ftpStep = true; _stepStartUtc = DateTime.UtcNow; }
        }

        /// <summary>FTP aşamasında bu adımda o ana kadar aktarılan bayt.</summary>
        public void FtpBytes(long done)
        {
            lock (_lock)
            {
                if (!_ftpStep) return;
                _stepDone = Math.Min(Math.Max(0, done), _stepTotal);
                _ftpRate = Sample(_ftpSamples, _ftpBase + _stepDone, _ftpRate);
            }
        }

        public void EndFtp()
        {
            lock (_lock)
            {
                if (_ftpStep) { _ftpBase += _stepTotal; _ftpRate = Sample(_ftpSamples, _ftpBase, _ftpRate); }
                _stepDone = _stepTotal = 0;
                _ftpStep = false;
            }
        }

        public void StartCard(string stage, long bytes)
        {
            lock (_lock) { _stage = stage ?? ""; _stepTotal = bytes; _stepDone = 0; _onCard = _cardStep = true; _ftpStep = false; _stepStartUtc = DateTime.UtcNow; }
        }

        /// <summary>Kart aşamasında bu adımda o ana kadar aktarılan bayt (8 MB'lık bloklar).</summary>
        public void CardBytes(long done)
        {
            lock (_lock)
            {
                if (!_cardStep) return;
                _stepDone = Math.Min(Math.Max(0, done), _stepTotal);
                _cardRate = Sample(_cardSamples, _cardBase + _stepDone, _cardRate);
            }
        }

        public void EndCard()
        {
            lock (_lock)
            {
                if (_cardStep)
                {
                    // Ara bilgi vermeyen adımda (copy) hız, adımın tamamından ölçülür
                    double sec = (DateTime.UtcNow - _stepStartUtc).TotalSeconds;
                    if (_stepDone == 0 && _stepTotal > 0 && sec > 0.5) _cardRate = _stepTotal / sec;
                    _cardBase += _stepTotal;
                    if (_stepDone > 0) _cardRate = Sample(_cardSamples, _cardBase, _cardRate);
                }
                _stepDone = _stepTotal = 0;
                _onCard = _cardStep = false;
            }
        }

        /// <summary>Kart tarafında baytı olmayan kısa bir işlem (dosya açma vb.).</summary>
        public void Note(string stage)
        {
            lock (_lock) { _stage = stage ?? ""; _stepDone = _stepTotal = 0; _onCard = true; _ftpStep = _cardStep = false; }
        }

        /// <summary>Kayan pencereye örnek ekler, penceredeki hızı döndürür (pencere çok kısaysa önceki hız kalır).</summary>
        private static double Sample(Queue<KeyValuePair<DateTime, long>> q, long cumulative, double previous)
        {
            DateTime now = DateTime.UtcNow;
            q.Enqueue(new KeyValuePair<DateTime, long>(now, cumulative));
            while (q.Count > 2 && (now - q.Peek().Key).TotalSeconds > RateWindowSec) q.Dequeue();
            KeyValuePair<DateTime, long> first = q.Peek();
            double sec = (now - first.Key).TotalSeconds;
            if (sec < 1.0 || cumulative <= first.Value) return previous;
            return (cumulative - first.Value) / sec;
        }

        public Snapshot Read()
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;
                long ftpDone = Math.Min(_total, _ftpBase + (_ftpStep ? _stepDone : 0));
                long cardDone = Math.Min(_total, _cardBase + (_cardStep ? _stepDone : 0));
                var s = new Snapshot
                {
                    Direction = Direction,
                    Total = _total,
                    FtpDone = ftpDone,
                    CardDone = cardDone,
                    StepDone = _stepDone,
                    StepTotal = _stepTotal,
                    StepHasBytes = _ftpStep || (_cardStep && _stepDone > 0),
                    Item = _item,
                    Stage = _stage,
                    OnCard = _onCard,
                    FileCount = _fileCount,
                    Elapsed = now - _startUtc,
                    FtpRate = _ftpRate,
                    CardRate = _cardRate,
                };
                // Kalan süre: kalan FTP baytı / anlık FTP hızı + kalan kart baytı / anlık kart hızı
                long ftpLeft = Direction == TransferDirection.InCard ? 0 : _total - ftpDone;
                long cardLeft = _total - cardDone;
                if (_cardStep && _stepDone == 0 && _cardRate > 0)   // ara bilgi vermeyen kart adımı: geçen süreyle tahmin
                    cardLeft -= Math.Min((long)((now - _stepStartUtc).TotalSeconds * _cardRate), (long)(_stepTotal * 0.95));
                bool known = (ftpLeft <= 0 || _ftpRate > 0) && (cardLeft <= 0 || _cardRate > 0) && s.Elapsed.TotalSeconds >= 3;
                if (known)
                {
                    double sec = (ftpLeft > 0 ? ftpLeft / _ftpRate : 0) + (cardLeft > 0 ? cardLeft / _cardRate : 0);
                    s.Remaining = TimeSpan.FromSeconds(Math.Min(sec, 7 * 24 * 3600));
                }
                return s;
            }
        }
    }

    /// <summary>İlerleme görünümü: toplam çubuğu, aktarılan / toplam, anlık hızlar, kalan süre, o anki adım.</summary>
    internal sealed class TransferProgressPanel : Panel
    {
        private readonly Label _item = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Label _overall = Theme.MakeLabel("", Theme.Ui, Theme.Text);
        private readonly Label _speed = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Label _stage = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly ProgressBar _totalBar = new ProgressBar { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };
        private readonly ProgressBar _stepBar = new ProgressBar { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };

        public TransferProgressPanel(string title, int width)
        {
            BackColor = Color.Transparent;
            var head = Theme.MakeLabel(title, Theme.UiBold, Theme.Text);
            foreach (Label l in new[] { head, _item, _overall, _speed, _stage })
            {
                l.AutoSize = false;
                l.AutoEllipsis = true;
            }
            head.SetBounds(0, 0, width, 22);
            _item.SetBounds(0, 26, width, 20);
            _totalBar.SetBounds(0, 52, width, 16);
            _overall.SetBounds(0, 72, width, 20);
            _speed.SetBounds(0, 94, width, 20);
            _stepBar.SetBounds(0, 124, width, 8);
            _stage.SetBounds(0, 136, width, 20);
            Size = new Size(width, 158);
            Controls.AddRange(new Control[] { head, _item, _totalBar, _overall, _speed, _stepBar, _stage });
        }

        public string CancelText
        {
            set { _stage.Text = value; }
        }

        public void UpdateView(TransferProgress.Snapshot s, bool cancelling)
        {
            double f = s.Fraction;
            _totalBar.Value = Math.Max(0, Math.Min(1000, (int)(f * 1000)));
            bool toCard = s.Direction == TransferDirection.ToCard;
            bool inCard = s.Direction == TransferDirection.InCard;
            string speed;
            if (inCard)
            {
                _overall.Text = "%" + Math.Floor(f * 100) + "  ·  kopyalanan " + DeviceSpace.FormatBytes(s.CardDone) + " / " + DeviceSpace.FormatBytes(s.Total);
                speed = "Anlık hız: kart " + Rate(s.CardRate) + "  ·  geçen " + Duration(s.Elapsed);
            }
            else
            {
                // Karta yüklemede son aşama kart, karttan almada son aşama FTP'den indirme
                long finished = toCard ? s.CardDone : s.FtpDone;
                long middle = toCard ? s.FtpDone : s.CardDone;
                _overall.Text = "%" + Math.Floor(f * 100) + "  ·  " + (toCard ? "kartta " : "bilgisayarda ") + DeviceSpace.FormatBytes(finished) +
                                " / " + DeviceSpace.FormatBytes(s.Total) + "  ·  " + (toCard ? "FTP'ye gönderilen " : "kartta FTP'ye yazılan ") +
                                DeviceSpace.FormatBytes(middle);
                speed = "Anlık hız: FTP " + Rate(s.FtpRate) + "  ·  kart " + Rate(s.CardRate) + "  ·  geçen " + Duration(s.Elapsed);
            }
            speed += "  ·  kalan " + (s.Remaining.HasValue ? "~" + Duration(s.Remaining.Value) : "hesaplanıyor…");
            _speed.Text = speed;
            _item.Text = s.Item;
            if (cancelling) return;
            if (s.OnCard && !s.StepHasBytes)
            {
                if (_stepBar.Style != ProgressBarStyle.Marquee) { _stepBar.Style = ProgressBarStyle.Marquee; _stepBar.MarqueeAnimationSpeed = 30; }
                _stage.Text = s.Stage + (s.StepTotal > 0 ? "  ·  " + DeviceSpace.FormatBytes(s.StepTotal) : "");
            }
            else
            {
                if (_stepBar.Style != ProgressBarStyle.Continuous) _stepBar.Style = ProgressBarStyle.Continuous;
                _stepBar.Value = s.StepTotal > 0 ? (int)Math.Min(1000, s.StepDone * 1000 / s.StepTotal) : 0;
                _stage.Text = s.Stage + (s.StepTotal > 0 ? "  ·  " + DeviceSpace.FormatBytes(s.StepDone) + " / " + DeviceSpace.FormatBytes(s.StepTotal) : "");
            }
        }

        private static string Rate(double bytesPerSec)
        {
            return bytesPerSec > 0 ? DeviceSpace.FormatBytes((long)bytesPerSec) + "/s" : "—";
        }

        internal static string Duration(TimeSpan t)
        {
            if (t.TotalSeconds < 60) return Math.Max(0, (int)t.TotalSeconds) + " sn";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " dk " + t.Seconds + " sn";
            return (int)t.TotalHours + " sa " + t.Minutes + " dk";
        }
    }

    /// <summary>
    /// Karta yükleme ilerleme penceresi (G40): kalıcı (modal) değildir. "Arka planda sürdür" ile kapatılır, yükleme sürer.
    /// "İptal": FTP gönderimi hemen kesilir, kartta en geç bir blok (8 MB) sonra durulur.
    /// </summary>
    public sealed class UploadProgressDialog : DarkDialog
    {
        /// <summary>Bu boyuttan (ya da bu sayıda dosyadan) küçük yüklemelerde pencere açılmaz.</summary>
        public const long ShowFromBytes = 50L * 1024 * 1024;
        public const int ShowFromFiles = 20;

        private readonly TransferProgress _p;
        private readonly TransferProgressPanel _panel;
        private readonly Button _hide = Theme.MakeButton("Arka planda sürdür", Theme.ButtonKind.Secondary);
        private readonly Button _cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 300 };

        private UploadProgressDialog(string caption, string title, TransferProgress p)
            : base(caption, new Size(600, 270))
        {
            _p = p;
            ControlBox = false;
            ShowInTaskbar = false;
            _panel = new TransferProgressPanel(title, 560) { Location = new Point(20, 16) };
            _cancel.Width = 90;
            _hide.Width = 150;
            int y = ClientSize.Height - FooterHeight + 18;
            _cancel.Location = new Point(ClientSize.Width - 20 - _cancel.Width, y);
            _hide.Location = new Point(_cancel.Left - 8 - _hide.Width, y);
            _hide.Click += (s, e) => Close();
            _cancel.Click += (s, e) =>
            {
                _p.Cancel();
                _cancel.Enabled = false;
                _panel.CancelText = "İptal ediliyor…";
            };
            Controls.AddRange(new Control[] { _panel, _hide, _cancel });
            _timer.Tick += (s, e) => UpdateView();
            Shown += (s, e) => { UpdateView(); _timer.Start(); };
            FormClosed += (s, e) => _timer.Dispose();
        }

        /// <summary>
        /// Yüklemeyi izler: toplam boyut belli olup yükleme büyükse (≥ 50 MB ya da ≥ 20 dosya) pencere açılır,
        /// yükleme bitince kendiliğinden kapanır.
        /// </summary>
        public static void Watch(Form owner, string title, TransferProgress p)
        {
            Watch(owner, "Karta yükleme", title, p);
        }

        /// <summary>Başlıklı izleme (ör. "Kart içinde kopyalama").</summary>
        public static void Watch(Form owner, string caption, string title, TransferProgress p)
        {
            var t = new System.Windows.Forms.Timer { Interval = 250 };
            UploadProgressDialog dlg = null;
            bool decided = false;
            t.Tick += (s, e) =>
            {
                if (p.Finished || owner == null || owner.IsDisposed)
                {
                    t.Stop();
                    t.Dispose();
                    if (dlg != null && !dlg.IsDisposed) dlg.Close();
                    return;
                }
                if (decided) return;
                TransferProgress.Snapshot s0 = p.Read();
                if (s0.Total <= 0 && s0.FileCount <= 0) return;      // boyut henüz hesaplanmadı
                decided = true;
                if (s0.Total < ShowFromBytes && s0.FileCount < ShowFromFiles) return;
                dlg = new UploadProgressDialog(caption, title, p);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Show(owner);
            };
            t.Start();
        }

        private void UpdateView()
        {
            if (IsDisposed) return;
            _panel.UpdateView(_p.Read(), _p.CancelRequested);
        }
    }

    /// <summary>
    /// Karttan bilgisayara alma ilerleme penceresi (G41). Sürükle-bırak bırakmayı bu pencere bitene kadar beklettiği için
    /// kalıcıdır (modal). "İptal": FTP indirmesi hemen kesilir, kartta en geç bir blok (8 MB) sonra durulur.
    /// </summary>
    public sealed class DownloadProgressDialog : DarkDialog
    {
        private readonly Func<TransferProgress, Task<string>> _work;
        private readonly TransferProgress _p = new TransferProgress(TransferDirection.FromCard);
        private readonly TransferProgressPanel _panel;
        private readonly Button _cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 300 };
        private bool _done;

        public DownloadProgressDialog(string name, Func<TransferProgress, Task<string>> work)
            : base("Karttan bilgisayara", new Size(600, 270))
        {
            _work = work;
            ControlBox = false;
            _panel = new TransferProgressPanel("\"" + name + "\" karttan alınıyor…", 560) { Location = new Point(20, 16) };
            _cancel.Width = 90;
            _cancel.Location = new Point(ClientSize.Width - 20 - _cancel.Width, ClientSize.Height - FooterHeight + 18);
            _cancel.Click += (s, e) => { _p.Cancel(); _cancel.Enabled = false; _panel.CancelText = "İptal ediliyor…"; };
            Controls.AddRange(new Control[] { _panel, _cancel });
            _timer.Tick += (s, e) => { if (!IsDisposed) _panel.UpdateView(_p.Read(), _p.CancelRequested); };
            Shown += async (s, e) => { _timer.Start(); await RunAsync(); };
            FormClosing += (s, e) => { if (!_done) e.Cancel = true; };   // iş bitmeden kapanmaz (İptal ile durdurulur)
            FormClosed += (s, e) => _timer.Dispose();
        }

        public string Result { get; private set; }

        private async Task RunAsync()
        {
            try { Result = await _work(_p); }
            catch (Exception ex) { ErrorText.LogTechnical("karttan indirme", ex); Result = null; }
            if (_p.CancelRequested) Result = null;
            _p.Finished = true;
            _done = true;
            Close();
        }
    }
}
