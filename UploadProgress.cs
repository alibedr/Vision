using System;
using System.Drawing;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Karta yüklemenin ilerlemesi (G40). Yükleme (FileBrowser.UploadAsync) farklı iş parçacıklarından yazar, ilerleme
    /// penceresi zamanlayıcıyla okur. Her bayt iki aşamadan geçer: bilgisayardan FTP'ye gönderim (bayt bayt izlenir) ve
    /// kartta FTP'den hedefe kopyalama (kart bitirene kadar ara bilgi vermez: parça / dosya bitince ilerler).
    /// Toplam ilerleme bu iki aşamanın ortalamasıdır.
    /// </summary>
    public sealed class TransferProgress
    {
        private readonly object _lock = new object();
        private long _total, _sentBase, _copied, _stepDone, _stepTotal, _itemSent, _itemCopied;
        private string _item = "", _stage = "";
        private bool _onCard, _ftpStep;
        private int _fileCount;
        private DateTime _startUtc = DateTime.UtcNow;

        public volatile bool CancelRequested;
        public volatile bool Finished;

        public sealed class Snapshot
        {
            public long Total, Sent, Copied, StepDone, StepTotal;
            public string Item, Stage;
            public bool OnCard;
            public int FileCount;
            public TimeSpan Elapsed;

            /// <summary>0..1: FTP'ye gönderim ve kartta kopyalama ortalaması.</summary>
            public double Fraction { get { return Total > 0 ? Math.Min(1.0, (Sent + Copied) / (2.0 * Total)) : 0; } }
        }

        public void Begin(long totalBytes, int fileCount)
        {
            lock (_lock) { _total = Math.Max(0, totalBytes); _fileCount = fileCount; _startUtc = DateTime.UtcNow; }
        }

        /// <summary>Yeni öğe (dosya / klasör) başlıyor.</summary>
        public void BeginItem(string text)
        {
            lock (_lock) { _item = text ?? ""; _itemSent = _sentBase; _itemCopied = _copied; _stage = ""; _stepDone = _stepTotal = 0; _onCard = _ftpStep = false; }
        }

        /// <summary>Öğe bitti (başarılı da olsa başarısız da olsa): öğenin baytları iki aşamada da tamamlanmış sayılır.</summary>
        public void CompleteItem(long bytes)
        {
            lock (_lock) { _sentBase = _itemSent + bytes; _copied = _itemCopied + bytes; _stepDone = _stepTotal = 0; _onCard = _ftpStep = false; }
        }

        public void StartFtp(string stage, long bytes)
        {
            lock (_lock) { _stage = stage ?? ""; _stepTotal = bytes; _stepDone = 0; _onCard = false; _ftpStep = true; }
        }

        /// <summary>FTP'ye gönderilen bayt (bu adımda o ana kadar).</summary>
        public void FtpBytes(long done)
        {
            lock (_lock) { if (_ftpStep) _stepDone = Math.Min(Math.Max(0, done), _stepTotal); }
        }

        public void EndFtp()
        {
            lock (_lock) { if (_ftpStep) _sentBase += _stepTotal; _stepDone = _stepTotal = 0; _ftpStep = false; }
        }

        public void StartCard(string stage, long bytes)
        {
            lock (_lock) { _stage = stage ?? ""; _stepTotal = bytes; _stepDone = 0; _onCard = true; _ftpStep = false; }
        }

        public void EndCard()
        {
            lock (_lock) { if (_onCard) _copied += _stepTotal; _stepDone = _stepTotal = 0; _onCard = false; }
        }

        /// <summary>Kart tarafında baytı olmayan kısa bir işlem (hedef dosyayı açma vb.).</summary>
        public void Note(string stage)
        {
            lock (_lock) { _stage = stage ?? ""; _stepDone = _stepTotal = 0; _onCard = true; _ftpStep = false; }
        }

        public Snapshot Read()
        {
            lock (_lock)
            {
                return new Snapshot
                {
                    Total = _total,
                    Sent = Math.Min(_total, _sentBase + (_ftpStep ? _stepDone : 0)),
                    Copied = Math.Min(_total, _copied),
                    StepDone = _stepDone,
                    StepTotal = _stepTotal,
                    Item = _item,
                    Stage = _stage,
                    OnCard = _onCard,
                    FileCount = _fileCount,
                    Elapsed = DateTime.UtcNow - _startUtc,
                };
            }
        }
    }

    /// <summary>
    /// Karta yükleme ilerleme penceresi (G40): toplam ilerleme (yüzde, aktarılan / toplam, kalan süre tahmini) ve o anki
    /// adım (FTP'ye gönderim bayt bayt; kartta kopyalama kart bitirene kadar hareketli çubuk). Pencere kalıcı değildir:
    /// "Arka planda sürdür" ile kapatılır, yükleme sürer. "İptal" o anki parça / dosya bitince durdurur.
    /// </summary>
    public sealed class UploadProgressDialog : DarkDialog
    {
        /// <summary>Bu boyuttan (ya da bu sayıda dosyadan) küçük yüklemelerde pencere açılmaz.</summary>
        public const long ShowFromBytes = 50L * 1024 * 1024;
        public const int ShowFromFiles = 20;

        private readonly TransferProgress _p;
        private readonly Label _item = Theme.MakeLabel("", Theme.UiBold, Theme.Text);
        private readonly Label _overall = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly Label _stage = Theme.MakeLabel("", Theme.Ui, Theme.TextMuted);
        private readonly ProgressBar _totalBar = new ProgressBar { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };
        private readonly ProgressBar _stepBar = new ProgressBar { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };
        private readonly Button _hide = Theme.MakeButton("Arka planda sürdür", Theme.ButtonKind.Secondary);
        private readonly Button _cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
        private readonly Timer _timer = new Timer { Interval = 300 };

        private UploadProgressDialog(string title, TransferProgress p)
            : base("Karta yükleme", new Size(540, 250))
        {
            _p = p;
            ControlBox = false;
            ShowInTaskbar = false;
            var head = Theme.MakeLabel(title, Theme.UiBold, Theme.Text);
            head.AutoSize = false;
            head.AutoEllipsis = true;
            head.SetBounds(20, 16, 500, 22);
            foreach (Label l in new[] { _item, _overall, _stage })
            {
                l.AutoSize = false;
                l.AutoEllipsis = true;
            }
            _item.Font = Theme.Ui;
            _item.ForeColor = Theme.TextMuted;
            _item.SetBounds(20, 42, 500, 20);
            _totalBar.SetBounds(20, 68, 500, 16);
            _overall.SetBounds(20, 88, 500, 20);
            _stepBar.SetBounds(20, 122, 500, 8);
            _stage.SetBounds(20, 134, 500, 20);
            _cancel.Width = 90;
            _hide.Width = 150;
            int y = ClientSize.Height - FooterHeight + 18;
            _cancel.Location = new Point(ClientSize.Width - 20 - _cancel.Width, y);
            _hide.Location = new Point(_cancel.Left - 8 - _hide.Width, y);
            _hide.Click += (s, e) => Close();
            _cancel.Click += (s, e) =>
            {
                _p.CancelRequested = true;
                _cancel.Enabled = false;
                _stage.Text = "İptal ediliyor (o anki parça / dosya bitince)…";
            };
            Controls.AddRange(new Control[] { head, _item, _totalBar, _overall, _stepBar, _stage, _hide, _cancel });
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
            var t = new Timer { Interval = 250 };
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
                dlg = new UploadProgressDialog(title, p);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Show(owner);
            };
            t.Start();
        }

        private void UpdateView()
        {
            if (IsDisposed) return;
            TransferProgress.Snapshot s = _p.Read();
            double f = s.Fraction;
            _totalBar.Value = Math.Max(0, Math.Min(1000, (int)(f * 1000)));
            string text = "%" + Math.Floor(f * 100) + "  ·  kartta " + DeviceSpace.FormatBytes(s.Copied) + " / " + DeviceSpace.FormatBytes(s.Total) +
                          "  ·  FTP'ye gönderilen " + DeviceSpace.FormatBytes(s.Sent) + "  ·  geçen " + Duration(s.Elapsed);
            if (f > 0.02 && s.Elapsed.TotalSeconds >= 5)
            {
                double remain = s.Elapsed.TotalSeconds * (1 - f) / f;
                text += "  ·  kalan ~" + Duration(TimeSpan.FromSeconds(remain));
            }
            _overall.Text = text;
            _item.Text = s.Item;
            if (_p.CancelRequested) return;
            if (s.OnCard)
            {
                if (_stepBar.Style != ProgressBarStyle.Marquee) { _stepBar.Style = ProgressBarStyle.Marquee; _stepBar.MarqueeAnimationSpeed = 30; }
                _stage.Text = s.Stage + (s.StepTotal > 0 ? "  ·  " + DeviceSpace.FormatBytes(s.StepTotal) + " (kart bitirince ilerler)" : "");
            }
            else
            {
                if (_stepBar.Style != ProgressBarStyle.Continuous) _stepBar.Style = ProgressBarStyle.Continuous;
                _stepBar.Value = s.StepTotal > 0 ? (int)Math.Min(1000, s.StepDone * 1000 / s.StepTotal) : 0;
                _stage.Text = s.Stage + (s.StepTotal > 0 ? "  ·  " + DeviceSpace.FormatBytes(s.StepDone) + " / " + DeviceSpace.FormatBytes(s.StepTotal) : "");
            }
        }

        private static string Duration(TimeSpan t)
        {
            if (t.TotalSeconds < 60) return Math.Max(0, (int)t.TotalSeconds) + " sn";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " dk " + t.Seconds + " sn";
            return (int)t.TotalHours + " sa " + t.Minutes + " dk";
        }
    }
}
