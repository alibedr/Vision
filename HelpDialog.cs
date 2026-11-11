using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Vision
{
    public enum HelpPage { Guide, About }

    /// <summary>
    /// Yardım penceresi (F1): Kullanım Kılavuzu ve Hakkında. Gereksinimler belgesi yalnız depoda durur (geliştirici için, G33.2).
    /// Belgeler exe'nin içine gömülüdür (G32); her sürüm kendi kılavuzunu gösterir, ayrı Yardim klasörü gerekmez.
    /// </summary>
    public sealed class HelpDialog : Form
    {
        public const string GuideFile = "KullanimKilavuzu.md";

        private readonly Panel _nav = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Theme.Sidebar };
        private readonly FlowLayoutPanel _toc = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Sidebar };
        private readonly WebBrowser _browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            ScriptErrorsSuppressed = true,
            AllowWebBrowserDrop = false,
            IsWebBrowserContextMenuEnabled = true,
            WebBrowserShortcutsEnabled = true,
        };
        private readonly Label _source = Theme.MakeLabel("", Theme.UiSmall, Theme.TextFaint);
        private readonly Dictionary<HelpPage, Button> _pageButtons = new Dictionary<HelpPage, Button>();
        private HelpPage _page;

        // "Bu sayfada" vurgusu
        private readonly List<LinkLabel> _tocLinks = new List<LinkLabel>();
        private readonly List<string> _tocIds = new List<string>();
        private readonly List<int> _tocLevels = new List<int>();
        private int _activeToc = -1;
        private string _clickedId;        // tıklanan bölüm: kullanıcı kendisi kaydırana kadar etkin kalır
        private int _clickedScrollTop = -1;

        public HelpDialog(HelpPage page)
        {
            Text = "Yardım  —  " + AppInfo.Name;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 760);
            MinimumSize = new Size(760, 480);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            ShowInTaskbar = false;
            if (Theme.AppIcon != null) Icon = Theme.AppIcon;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            BuildNav();
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(1, 0, 0, 0) };
            content.Controls.Add(_browser);
            Controls.Add(content);
            Controls.Add(_nav);

            _browser.Navigating += Browser_Navigating;
            _browser.DocumentCompleted += (s, e) => AttachScrollTracking();
            ShowPage(page);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        // ---------- Sol menü ----------

        private void BuildNav()
        {
            var title = Theme.MakeLabel("YARDIM", Theme.SectionHeader, Theme.TextMuted);
            title.Location = new Point(18, 18);
            _nav.Controls.Add(title);

            int y = 42;
            foreach (var p in new[] { HelpPage.Guide, HelpPage.About })
            {
                string text = p == HelpPage.Guide ? "Kullanım Kılavuzu" : "Hakkında";
                var b = Theme.MakeButton(text, Theme.ButtonKind.Ghost);
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Padding = new Padding(8, 0, 0, 0);
                b.SetBounds(10, y, 230, 34);
                b.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                HelpPage page = p;
                b.Click += (s, e) => ShowPage(page);
                _pageButtons[p] = b;
                _nav.Controls.Add(b);
                y += 38;
            }

            var tocTitle = Theme.MakeLabel("BU SAYFADA", Theme.SectionHeader, Theme.TextMuted);
            tocTitle.Location = new Point(18, y + 14);
            _nav.Controls.Add(tocTitle);
            _toc.Location = new Point(6, y + 36);
            _toc.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _nav.Controls.Add(_toc);
            Theme.ApplyDarkScrollBars(_toc);

            _source.AutoSize = false;
            _source.AutoEllipsis = true;
            _source.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _nav.Controls.Add(_source);

            _nav.Resize += (s, e) =>
            {
                _source.SetBounds(14, _nav.Height - 36, 222, 30);
                _toc.Size = new Size(240, Math.Max(40, _source.Top - _toc.Top - 10));
            };
            _nav.Paint += (s, e) => { using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height); };
        }

        // ---------- Sayfalar ----------

        public void ShowPage(HelpPage page)
        {
            _page = page;
            foreach (var kv in _pageButtons)
            {
                bool active = kv.Key == page;
                kv.Value.BackColor = active ? Theme.Selection : Theme.Sidebar;
                kv.Value.ForeColor = active ? Theme.Text : Theme.TextMuted;
                kv.Value.Font = active ? Theme.UiBold : Theme.Ui;
            }

            string body;
            List<HelpMarkdown.TocEntry> toc;
            if (page == HelpPage.About)
            {
                body = AboutHtml();
                toc = new List<HelpMarkdown.TocEntry>();
                _source.Text = "";
            }
            else
            {
                string file = GuideFile;
                string source;
                string md = HelpMarkdown.LoadDocument(file, out source);
                if (md == null)
                {
                    body = "<h1>Belge bulunamadı</h1><p><code>" + HelpMarkdown.Escape(file) +
                           "</code> uygulamaya gömülü değil (derleme hatası).</p>";
                    toc = new List<HelpMarkdown.TocEntry>();
                    _source.Text = "Belge bulunamadı";
                }
                else
                {
                    body = HelpMarkdown.ToHtml(md, out toc);
                    _source.Text = "Kaynak: " + source;
                }
            }
            new DarkToolTip().SetToolTip(_source, _source.Text);
            BuildToc(toc);
            _browser.DocumentText = Page(body);
        }

        private void BuildToc(List<HelpMarkdown.TocEntry> toc)
        {
            _toc.SuspendLayout();
            foreach (Control c in new List<Control>(_toc.Controls.Cast<Control>())) c.Dispose();
            _tocLinks.Clear();
            _tocIds.Clear();
            _tocLevels.Clear();
            foreach (HelpMarkdown.TocEntry t in toc)
            {
                if (t.Level > 2) continue;   // yalnız ana başlıklar
                var link = new LinkLabel
                {
                    Text = t.Text,
                    AutoSize = false,
                    AutoEllipsis = true,
                    Width = 222,
                    Height = t.Level == 1 ? 24 : 22,
                    Margin = new Padding(t.Level == 1 ? 8 : 18, 0, 0, 0),
                    Font = t.Level == 1 ? Theme.UiBold : Theme.Ui,
                    LinkColor = t.Level == 1 ? Theme.Text : Theme.TextMuted,
                    ActiveLinkColor = Theme.Accent,
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    BackColor = Theme.Sidebar,
                };
                string id = t.Id;
                link.LinkClicked += (s, e) => ScrollTo(id);
                _toc.Controls.Add(link);
                _tocLinks.Add(link);
                _tocIds.Add(id);
                _tocLevels.Add(t.Level);
            }
            _toc.ResumeLayout();
            _activeToc = -1;
            _clickedId = null;
            if (_tocLinks.Count > 0) SetActiveToc(0);
        }

        // ---------- Etkin bölüm vurgusu (tıklama ve kaydırma) ----------

        private void AttachScrollTracking()
        {
            try
            {
                if (_browser.Document == null || _browser.Document.Window == null) return;
                _browser.Document.Window.Scroll += (s, e) => UpdateActiveFromScroll();
                _browser.Document.Window.Resize += (s, e) => UpdateActiveFromScroll();
                UpdateActiveFromScroll();
            }
            catch { }
        }

        private HtmlElement Root
        {
            get
            {
                HtmlDocument d = _browser.Document;
                if (d == null) return null;
                HtmlElementCollection html = d.GetElementsByTagName("html");
                return html.Count > 0 ? html[0] : d.Body;
            }
        }

        private int ScrollTop()
        {
            HtmlElement root = Root;
            if (root == null) return 0;
            int top = root.ScrollTop;
            if (top == 0 && _browser.Document.Body != null) top = _browser.Document.Body.ScrollTop;   // quirks kipi
            return top;
        }

        private static int AbsoluteTop(HtmlElement el)
        {
            int y = 0;
            for (HtmlElement e = el; e != null; e = e.OffsetParent) y += e.OffsetRectangle.Top;
            return y;
        }

        /// <summary>Kaydırma konumundan etkin bölümü hesaplar ve sol listede vurgular.</summary>
        private void UpdateActiveFromScroll()
        {
            try
            {
                HtmlElement root = Root;
                if (root == null || _tocIds.Count == 0) return;
                int scrollTop = ScrollTop();

                // Tıklamadan sonra gelen kaydırma olayı: tıklanan bölüm etkin kalsın (yakın başlıklar / sayfa sonu)
                if (_clickedId != null)
                {
                    if (Math.Abs(scrollTop - _clickedScrollTop) <= 2) return;
                    _clickedId = null;   // kullanıcı kendisi kaydırdı
                }

                var tops = new List<int>();
                foreach (string id in _tocIds)
                {
                    HtmlElement el = _browser.Document.GetElementById(id);
                    tops.Add(el != null ? AbsoluteTop(el) : int.MaxValue);
                }
                int viewport = root.ClientRectangle.Height;
                int docHeight = Math.Max(root.ScrollRectangle.Height, _browser.Document.Body != null ? _browser.Document.Body.ScrollRectangle.Height : 0);
                int index = HelpScrollSpy.ActiveIndex(tops, scrollTop, viewport, docHeight);
                if (index >= 0) SetActiveToc(index);
            }
            catch { }
        }

        private void SetActiveToc(int index)
        {
            if (index == _activeToc || index < 0 || index >= _tocLinks.Count) return;
            if (_activeToc >= 0 && _activeToc < _tocLinks.Count) StyleTocLink(_activeToc, false);
            _activeToc = index;
            StyleTocLink(index, true);
            _toc.ScrollControlIntoView(_tocLinks[index]);
        }

        private void StyleTocLink(int i, bool active)
        {
            LinkLabel l = _tocLinks[i];
            bool top = _tocLevels[i] == 1;
            l.BackColor = active ? Theme.Selection : Theme.Sidebar;
            l.LinkColor = active ? (Theme.IsDark ? Color.White : Theme.Text) : (top ? Theme.Text : Theme.TextMuted);
            l.Font = active || top ? Theme.UiBold : Theme.Ui;
        }

        private void ScrollTo(string id)
        {
            try
            {
                if (_browser.Document == null) return;
                HtmlElement el = _browser.Document.GetElementById(id);
                if (el == null) return;
                el.ScrollIntoView(true);
                // Tıklanan bölüm vurgulanır; sayfa sonuna yakın başlıklar üste çıkamasa da doğru bölüm seçili kalır
                _clickedId = id;
                _clickedScrollTop = ScrollTop();
                int i = _tocIds.IndexOf(id);
                if (i >= 0) SetActiveToc(i);
            }
            catch { }
        }

        /// <summary>Sayfa içi (#) bağlantılar kaydırır; dış bağlantılar varsayılan tarayıcıda açılır.</summary>
        private void Browser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            string url = e.Url.ToString();
            if (url == "about:blank") return;
            e.Cancel = true;
            int hash = url.IndexOf('#');
            if (hash >= 0) { ScrollTo(url.Substring(hash + 1)); return; }
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                try { Process.Start(url); } catch { }
            }
        }

        private static string AboutHtml()
        {
            return "<div class=\"about\">" +
                   "<div class=\"badge\">" + HelpMarkdown.Escape(AppInfo.Name) + "</div>" +
                   "<h1>" + HelpMarkdown.Escape(AppInfo.Name) + "</h1>" +
                   "<p class=\"lead\">" + HelpMarkdown.Escape(AppInfo.Tagline) + "</p>" +
                   "<table><tbody>" +
                   "<tr><th>Sürüm</th><td>" + HelpMarkdown.Escape(AppInfo.Version) + "</td></tr>" +
                   "<tr><th>Geliştiren</th><td><strong>" + HelpMarkdown.Escape(AppInfo.Author) + "</strong></td></tr>" +
                   "<tr><th>Platform</th><td>Windows · .NET Framework 4.6.1 · WinForms</td></tr>" +
                   "</tbody></table>" +
                   "<p class=\"muted\">" + HelpMarkdown.Escape(AppInfo.Copyright) + "</p>" +
                   "</div>";
        }

        /// <summary>Temaya uygun (koyu / açık) HTML iskeleti (WebBrowser IE11 kipinde açılır).</summary>
        private static string Page(string body)
        {
            // Koyu ve açık tema için ayrı renkler (sayfa her açılışta o anki temayla oluşturulur)
            var css = new StringBuilder();
            if (Theme.IsDark)
            {
                css.Append("html,body{background:#111318;color:#e2e6ed;margin:0;}");
                css.Append("body{font-family:'Segoe UI',Tahoma,sans-serif;font-size:14px;line-height:1.6;padding:28px 40px 60px 36px;max-width:920px;}");
                css.Append("h1{font-size:26px;font-weight:600;margin:4px 0 10px;color:#fff;}");
                css.Append("h2{font-size:19px;font-weight:600;margin:34px 0 10px;padding-bottom:6px;border-bottom:1px solid #3a414e;color:#fff;}");
                css.Append("h3{font-size:15px;font-weight:600;margin:22px 0 6px;color:#cfe0ff;}");
                css.Append("p{margin:8px 0;} a{color:#5498ff;} strong{color:#fff;}");
                css.Append("code{font-family:Consolas,monospace;font-size:13px;background:#1c2027;border:1px solid #3a414e;border-radius:4px;padding:1px 5px;color:#cdd6e0;}");
                css.Append("pre{background:#0e1014;border:1px solid #3a414e;border-radius:6px;padding:12px 14px;overflow:auto;}");
                css.Append("pre code{background:none;border:none;padding:0;}");
                css.Append("ul,ol{margin:6px 0 10px;padding-left:26px;} li{margin:3px 0;}");
                css.Append("table{border-collapse:collapse;margin:10px 0 14px;width:100%;}");
                css.Append("th,td{border:1px solid #3a414e;padding:7px 10px;text-align:left;vertical-align:top;}");
                css.Append("th{background:#1c2027;color:#a3abb8;font-weight:600;font-size:12px;text-transform:uppercase;}");
                css.Append("tr:nth-child(even) td{background:#151820;}");
                css.Append(".note{background:#16233a;border-left:4px solid #3884ff;border-radius:4px;padding:10px 14px;margin:12px 0;color:#d6e4ff;}");
                css.Append("hr{border:none;border-top:1px solid #3a414e;margin:26px 0;}");
                css.Append(".about{padding-top:20px;} .lead{color:#a3abb8;font-size:15px;} .muted{color:#a3abb8;margin-top:24px;}");
                css.Append(".badge{display:inline-block;background:#2563eb;color:#fff;font-weight:600;border-radius:8px;padding:6px 14px;margin-bottom:10px;}");
                css.Append(".about table{width:auto;} .about th{width:130px;}");
            }
            else
            {
                css.Append("html,body{background:#f2f4f7;color:#1b1f26;margin:0;}");
                css.Append("body{font-family:'Segoe UI',Tahoma,sans-serif;font-size:14px;line-height:1.6;padding:28px 40px 60px 36px;max-width:920px;}");
                css.Append("h1{font-size:26px;font-weight:600;margin:4px 0 10px;color:#0f1218;}");
                css.Append("h2{font-size:19px;font-weight:600;margin:34px 0 10px;padding-bottom:6px;border-bottom:1px solid #c8ced8;color:#0f1218;}");
                css.Append("h3{font-size:15px;font-weight:600;margin:22px 0 6px;color:#1d4fa8;}");
                css.Append("p{margin:8px 0;} a{color:#1d5fd7;} strong{color:#0f1218;}");
                css.Append("code{font-family:Consolas,monospace;font-size:13px;background:#eef1f5;border:1px solid #c8ced8;border-radius:4px;padding:1px 5px;color:#20262e;}");
                css.Append("pre{background:#ffffff;border:1px solid #c8ced8;border-radius:6px;padding:12px 14px;overflow:auto;}");
                css.Append("pre code{background:none;border:none;padding:0;}");
                css.Append("ul,ol{margin:6px 0 10px;padding-left:26px;} li{margin:3px 0;}");
                css.Append("table{border-collapse:collapse;margin:10px 0 14px;width:100%;}");
                css.Append("th,td{border:1px solid #c8ced8;padding:7px 10px;text-align:left;vertical-align:top;}");
                css.Append("th{background:#eef1f5;color:#505866;font-weight:600;font-size:12px;text-transform:uppercase;}");
                css.Append("tr:nth-child(even) td{background:#f7f8fa;}");
                css.Append(".note{background:#e3edfd;border-left:4px solid #1d5fd7;border-radius:4px;padding:10px 14px;margin:12px 0;color:#1b2a44;}");
                css.Append("hr{border:none;border-top:1px solid #c8ced8;margin:26px 0;}");
                css.Append(".about{padding-top:20px;} .lead{color:#505866;font-size:15px;} .muted{color:#505866;margin-top:24px;}");
                css.Append(".badge{display:inline-block;background:#2563eb;color:#fff;font-weight:600;border-radius:8px;padding:6px 14px;margin-bottom:10px;}");
                css.Append(".about table{width:auto;} .about th{width:130px;}");
            }

            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                   "<style>" + css + "</style></head><body>" + body + "</body></html>";
        }
    }
}
