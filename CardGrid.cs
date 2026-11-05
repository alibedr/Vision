using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TelnetCards
{
    /// <summary>
    /// Açık kart görünümlerini pencere boyutuna göre dinamik bir ızgarada döşer.
    /// Sütun sayısı, kartlar alanın en-boy oranına en iyi uyacak şekilde seçilir.
    /// Bir kart büyütüldüğünde yalnız o gösterilir. Kartlar çok küçülecekse alan kaydırılabilir olur.
    /// </summary>
    public sealed class CardGrid : Panel
    {
        private const int Gap = 10;
        private const int MinCardWidth = 340;
        private const int MinCardHeight = 220;

        private readonly List<CardView> _views = new List<CardView>();
        private CardView _maximized;
        private CardView _selected;
        private readonly Label _empty;

        public CardGrid()
        {
            DoubleBuffered = true;
            BackColor = Theme.Window;
            Padding = new Padding(Gap);
            AutoScroll = true;

            _empty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.TextFaint,
                Font = new Font("Segoe UI", 10.5f),
                Text = "Ekranda kart yok\n\nSoldaki Kart Gezgini'nde kartları işaretleyip \"Seçilenleri Göster\"e basın\n" +
                       "ya da bir karta çift tıklayın.\n\nBağlantılar ekrandan bağımsızdır: \"Bağlantı Kur\" ile bağlanan kartlar\n" +
                       "burada gösterilmese de bağlı kalır.",
            };
            Controls.Add(_empty);
        }

        public IList<CardView> Views { get { return _views.AsReadOnly(); } }

        public CardView SelectedView { get { return _selected; } }

        public event EventHandler ViewsChanged;

        public CardView FindView(CardSession session)
        {
            foreach (var v in _views) if (v.Session == session) return v;
            return null;
        }

        /// <summary>Aynı anda gösterilebilecek en fazla kart (sabit uygulama sınırı).</summary>
        public const int MaxViews = SelectionLogic.MaxVisibleCards;

        public int FreeSlots { get { return Math.Max(0, MaxViews - _views.Count); } }

        /// <summary>Kartı açar; zaten açıksa öne getirip seçer. Sınır doluysa açmaz ve null döner.</summary>
        public CardView Open(CardSession session)
        {
            CardView existing = FindView(session);
            if (existing != null)
            {
                if (_maximized != null && _maximized != existing) SetMaximized(null);
                Select(existing);
                ScrollControlIntoView(existing);
                return existing;
            }
            if (_views.Count >= MaxViews) return null;   // en fazla 12 kart; açık kartlara dokunulmaz

            var view = CreateView(session);
            if (_maximized != null) SetMaximized(null);
            _empty.Visible = false;

            PerformLayout();
            Select(view);
            RaiseChanged();
            return view;
        }

        /// <summary>
        /// Çalışma alanında TAM OLARAK verilen oturumları, verilen sırayla gösterir:
        /// listede olmayan görünümler kapatılır, eksik olanlar açılır.
        /// Yalnız görünüm değişir; hiçbir bağlantı kurulmaz ya da kesilmez.
        /// </summary>
        /// <returns>Sınır (12) nedeniyle gösterilemeyen oturumlar.</returns>
        public List<CardSession> ShowExactly(IList<CardSession> requested)
        {
            var shown = new List<CardSession>();
            foreach (var v in _views) shown.Add(v.Session);

            List<CardSession> rejected;
            IList<CardSession> sessions = SelectionLogic.LimitExact(shown, requested, MaxViews, out rejected);

            List<CardSession> toHide, toShow;
            SelectionLogic.DiffDisplay(shown, sessions, out toHide, out toShow);

            SuspendLayout();
            foreach (CardSession s in toHide)
            {
                CardView v = FindView(s);
                _views.Remove(v);
                if (_selected == v) _selected = null;
                Controls.Remove(v);
                v.Dispose();               // görünümü kapatır; CardSession (bağlantı) yaşamaya devam eder
            }
            foreach (CardSession s in toShow) CreateView(s);

            // Izgara sırası seçim sırasını izlesin
            var ordered = new List<CardView>();
            foreach (CardSession s in sessions)
            {
                CardView v = FindView(s);
                if (v != null && !ordered.Contains(v)) ordered.Add(v);
            }
            _views.Clear();
            _views.AddRange(ordered);

            _maximized = null;
            foreach (var v in _views) { v.IsMaximized = false; v.Visible = true; }
            _empty.Visible = _views.Count == 0;
            ResumeLayout(false);
            PerformLayout();

            if (_selected == null && _views.Count > 0) Select(_views[0]);
            RaiseChanged();
            return rejected;
        }

        /// <summary>Kartların görünen adı değişince başlıkları günceller.</summary>
        public void RefreshTitles()
        {
            foreach (var v in _views) v.RefreshTitle();
        }

        private CardView CreateView(CardSession session)
        {
            var view = new CardView(session);
            view.CloseRequested += v => Close(v);
            view.MaximizeToggled += v => SetMaximized(_maximized == v ? null : v);
            view.Activated += v => Select(v);
            _views.Add(view);
            Controls.Add(view);
            return view;
        }

        public void Close(CardView view)
        {
            if (!_views.Remove(view)) return;
            if (_maximized == view) _maximized = null;
            if (_selected == view) _selected = null;
            Controls.Remove(view);
            view.Dispose();
            _empty.Visible = _views.Count == 0;
            PerformLayout();
            if (_selected == null && _views.Count > 0) Select(_views[_views.Count - 1]);
            RaiseChanged();
        }

        public void CloseAll()
        {
            foreach (var v in new List<CardView>(_views)) Close(v);
        }

        public void CloseSession(CardSession session)
        {
            CardView v = FindView(session);
            if (v != null) Close(v);
        }

        public void SetMaximized(CardView view)
        {
            _maximized = view;
            foreach (var v in _views)
            {
                v.IsMaximized = v == view;
                v.Visible = view == null || v == view;
            }
            PerformLayout();
            if (view != null) Select(view);
        }

        public void Select(CardView view)
        {
            if (_selected == view) return;
            if (_selected != null) _selected.Selected = false;
            _selected = view;
            if (view != null) view.Selected = true;
        }

        /// <summary>Her zamanlayıcı adımında açık görünümleri günceller.</summary>
        public void Tick()
        {
            foreach (var v in _views) v.Tick();
        }

        private void RaiseChanged()
        {
            var h = ViewsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutCards();
        }

        private void LayoutCards()
        {
            if (_views.Count == 0) return;

            // Kaydırma konumunu koru, düzeni sıfır kökenli hesapla
            Point scroll = AutoScrollPosition;
            Rectangle area = new Rectangle(Padding.Left, Padding.Top,
                Math.Max(0, ClientSize.Width - Padding.Horizontal),
                Math.Max(0, ClientSize.Height - Padding.Vertical));

            SuspendLayout();
            if (_maximized != null)
            {
                AutoScrollMinSize = Size.Empty;
                _maximized.Bounds = area;
                ResumeLayout(false);
                return;
            }

            int n = _views.Count;
            int cols = BestColumnCount(n, area.Width, area.Height);
            int rows = (n + cols - 1) / cols;

            int cellW = Math.Max(MinCardWidth, (area.Width - Gap * (cols - 1)) / cols);
            int cellH = Math.Max(MinCardHeight, (area.Height - Gap * (rows - 1)) / rows);

            int totalW = cellW * cols + Gap * (cols - 1);
            int totalH = cellH * rows + Gap * (rows - 1);
            AutoScrollMinSize = (totalW > area.Width || totalH > area.Height)
                ? new Size(totalW + Padding.Horizontal, totalH + Padding.Vertical)
                : Size.Empty;

            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                // Son satırdaki eksik kartlar genişlesin, boşluk kalmasın
                int itemsInRow = (r == rows - 1) ? n - r * cols : cols;
                int w = itemsInRow == cols ? cellW : Math.Max(MinCardWidth, (Math.Max(totalW, area.Width) - Gap * (itemsInRow - 1)) / itemsInRow);
                int x = area.Left + c * (w + Gap) + scroll.X;
                int y = area.Top + r * (cellH + Gap) + scroll.Y;
                _views[i].Visible = true;
                _views[i].Bounds = new Rectangle(x, y, w, cellH);
            }
            ResumeLayout(false);
        }

        /// <summary>Hücreleri olabildiğince kareye yakın (kart için ~1.6:1) tutan sütun sayısı.</summary>
        internal static int BestColumnCount(int n, int width, int height)
        {
            if (n <= 1) return 1;
            int best = 1;
            double bestScore = double.MaxValue;
            for (int cols = 1; cols <= n; cols++)
            {
                int rows = (n + cols - 1) / cols;
                double w = Math.Max(1, (width - Gap * (cols - 1)) / (double)cols);
                double h = Math.Max(1, (height - Gap * (rows - 1)) / (double)rows);
                double ratio = w / h;
                double score = Math.Abs(Math.Log(ratio / 1.6));
                if (w < MinCardWidth) score += 2;               // çok dar hücreleri cezalandır
                score += (rows * cols - n) * 0.15;              // boş hücreleri cezalandır
                if (score < bestScore) { bestScore = score; best = cols; }
            }
            return best;
        }
    }
}
