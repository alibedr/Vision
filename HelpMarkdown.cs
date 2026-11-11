using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Vision
{
    /// <summary>
    /// Yardım belgeleri için küçük Markdown → HTML dönüştürücü (dış kütüphane gerektirmez).
    /// Desteklenenler: # / ## / ### başlıklar (otomatik içindekiler), paragraflar, **kalın**, *eğik*, `kod`,
    /// ``` kod blokları, - ve 1. listeler (iki boşluk girintiyle bir alt seviye), > not kutuları,
    /// | tablolar |, --- yatay çizgi ve [bağlantılar](adres).
    /// </summary>
    public static class HelpMarkdown
    {
        private static readonly Regex Heading = new Regex(@"^(#{1,3})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
        private static readonly Regex Bullet = new Regex(@"^(\s*)[-*]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex Numbered = new Regex(@"^(\s*)\d+[.)]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex TableSep = new Regex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$", RegexOptions.Compiled);

        public sealed class TocEntry
        {
            public int Level;
            public string Text;
            public string Id;
        }

        /// <summary>Markdown metnini gövde HTML'ine çevirir; başlıklar için içindekiler listesi de döndürür.</summary>
        public static string ToHtml(string markdown, out List<TocEntry> toc)
        {
            toc = new List<TocEntry>();
            var ids = new HashSet<string>();
            var html = new StringBuilder();
            string[] lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var paragraph = new List<string>();
            var listStack = new Stack<string>();   // "ul" / "ol"
            var listIndent = new Stack<int>();

            Action flushParagraph = () =>
            {
                if (paragraph.Count == 0) return;
                html.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>\n");
                paragraph.Clear();
            };
            Action closeLists = () =>
            {
                while (listStack.Count > 0)
                {
                    html.Append("</li></").Append(listStack.Pop()).Append(">\n");
                    listIndent.Pop();
                }
            };

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // Kod bloğu
                if (trimmed.StartsWith("```"))
                {
                    flushParagraph(); closeLists();
                    var code = new StringBuilder();
                    i++;
                    while (i < lines.Length && !lines[i].Trim().StartsWith("```")) { code.Append(Escape(lines[i])).Append('\n'); i++; }
                    html.Append("<pre><code>").Append(code.ToString().TrimEnd('\n')).Append("</code></pre>\n");
                    continue;
                }

                if (trimmed.Length == 0) { flushParagraph(); closeLists(); continue; }

                // Başlık
                Match h = Heading.Match(line);
                if (h.Success)
                {
                    flushParagraph(); closeLists();
                    int level = h.Groups[1].Value.Length;
                    string text = h.Groups[2].Value;
                    string id = UniqueId(Slug(text), ids);
                    html.Append("<h").Append(level).Append(" id=\"").Append(id).Append("\">").Append(Inline(text)).Append("</h").Append(level).Append(">\n");
                    toc.Add(new TocEntry { Level = level, Text = StripInline(text), Id = id });
                    continue;
                }

                // Yatay çizgi
                if (Regex.IsMatch(trimmed, @"^(-{3,}|\*{3,})$"))
                {
                    flushParagraph(); closeLists();
                    html.Append("<hr>\n");
                    continue;
                }

                // Tablo: başlık satırı + ayırıcı satır
                if (trimmed.StartsWith("|") && i + 1 < lines.Length && TableSep.IsMatch(lines[i + 1]))
                {
                    flushParagraph(); closeLists();
                    html.Append("<table>\n<thead><tr>");
                    foreach (string cell in Cells(line)) html.Append("<th>").Append(Inline(cell)).Append("</th>");
                    html.Append("</tr></thead>\n<tbody>\n");
                    i += 2;
                    while (i < lines.Length && lines[i].Trim().StartsWith("|"))
                    {
                        html.Append("<tr>");
                        foreach (string cell in Cells(lines[i])) html.Append("<td>").Append(Inline(cell)).Append("</td>");
                        html.Append("</tr>\n");
                        i++;
                    }
                    i--;
                    html.Append("</tbody></table>\n");
                    continue;
                }

                // Not kutusu
                if (trimmed.StartsWith(">"))
                {
                    flushParagraph(); closeLists();
                    var note = new List<string>();
                    while (i < lines.Length && lines[i].Trim().StartsWith(">"))
                    {
                        note.Add(lines[i].Trim().Substring(1).Trim());
                        i++;
                    }
                    i--;
                    html.Append("<div class=\"note\">").Append(Inline(string.Join(" ", note))).Append("</div>\n");
                    continue;
                }

                // Listeler
                Match b = Bullet.Match(line);
                Match n = b.Success ? Match.Empty : Numbered.Match(line);
                if (b.Success || n.Success)
                {
                    flushParagraph();
                    Match m = b.Success ? b : n;
                    string kind = b.Success ? "ul" : "ol";
                    int indent = m.Groups[1].Value.Replace("\t", "  ").Length;

                    if (listStack.Count == 0 || indent > listIndent.Peek())
                    {
                        html.Append("<").Append(kind).Append(">\n<li>");
                        listStack.Push(kind);
                        listIndent.Push(indent);
                    }
                    else
                    {
                        while (listStack.Count > 1 && indent < listIndent.Peek())
                        {
                            html.Append("</li></").Append(listStack.Pop()).Append(">\n");
                            listIndent.Pop();
                        }
                        html.Append("</li>\n<li>");
                    }
                    html.Append(Inline(m.Groups[2].Value));
                    continue;
                }

                // Liste öğesinin devam satırı
                if (listStack.Count > 0 && line.StartsWith("  "))
                {
                    html.Append(" ").Append(Inline(trimmed));
                    continue;
                }

                closeLists();
                paragraph.Add(trimmed);
            }
            flushParagraph();
            closeLists();
            return html.ToString();
        }

        private static IEnumerable<string> Cells(string row)
        {
            string r = row.Trim();
            if (r.StartsWith("|")) r = r.Substring(1);
            if (r.EndsWith("|")) r = r.Substring(0, r.Length - 1);
            foreach (string c in r.Split('|')) yield return c.Trim();
        }

        /// <summary>Satır içi biçimler: kod, kalın, eğik, bağlantı (önce HTML kaçışı yapılır).</summary>
        public static string Inline(string text)
        {
            var codes = new List<string>();
            string s = Regex.Replace(text, @"`([^`]+)`", m => { codes.Add(m.Groups[1].Value); return "\u0001" + (codes.Count - 1) + "\u0002"; });
            s = Escape(s);
            s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)\s]+)\)", m => "<a href=\"" + m.Groups[2].Value + "\">" + m.Groups[1].Value + "</a>");
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            s = Regex.Replace(s, @"(?<![\*\w])\*(?!\s)(.+?)(?<!\s)\*(?![\*\w])", "<em>$1</em>");
            s = Regex.Replace(s, "\u0001(\\d+)\u0002", m => "<code>" + Escape(codes[int.Parse(m.Groups[1].Value)]) + "</code>");
            return s;
        }

        private static string StripInline(string text)
        {
            return Regex.Replace(text, @"[`*]|\[([^\]]+)\]\([^)]+\)", m => m.Groups[1].Success ? m.Groups[1].Value : "");
        }

        public static string Escape(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static string Slug(string text)
        {
            string t = StripInline(text).ToLowerInvariant()
                .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i').Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u');
            t = Regex.Replace(t, @"[^a-z0-9]+", "-").Trim('-');
            return t.Length == 0 ? "bolum" : t;
        }

        private static string UniqueId(string id, HashSet<string> used)
        {
            string candidate = id;
            for (int k = 2; !used.Add(candidate); k++) candidate = id + "-" + k;
            return candidate;
        }

        // ---------- Belgeleri bulma ----------

        public const string FolderName = "Yardim";

        /// <summary>
        /// Belgeyi exe'nin içine gömülü kopyadan okur (G32): belge her zaman çalışan sürümle birebir aynıdır.
        /// Exe'nin yanında eski bir Yardim klasörü kalmış olsa bile kullanılmaz (eskimiş kılavuz gösterilmesin).
        /// Belgeler depoda Yardim\*.md olarak durur ve derlemede exe'ye gömülür.
        /// </summary>
        public static string LoadDocument(string fileName, out string source)
        {
            Assembly asm = typeof(HelpMarkdown).Assembly;
            using (Stream st = asm.GetManifestResourceStream(FolderName + "." + fileName))
            {
                if (st != null)
                {
                    using (var reader = new StreamReader(st, Encoding.UTF8))
                    {
                        source = "uygulamaya gömülü";
                        return reader.ReadToEnd();
                    }
                }
            }
            source = null;
            return null;
        }
    }
}

namespace Vision
{
    /// <summary>
    /// Yardım sayfasında "Bu sayfada" vurgusu için etkin bölüm hesabı (arayüzden bağımsız, test edilebilir).
    /// </summary>
    public static class HelpScrollSpy
    {
        /// <summary>Başlığın etkin sayılması için görünür alanın üstünden ne kadar aşağıda olabileceği (px).</summary>
        public const int ActivationOffset = 90;

        /// <param name="tops">Başlıkların belge içindeki üst konumları (px), sayfadaki sırayla.</param>
        /// <param name="scrollTop">Sayfanın kaydırma konumu.</param>
        /// <param name="viewport">Görünür alanın yüksekliği.</param>
        /// <param name="documentHeight">Belgenin toplam yüksekliği.</param>
        /// <returns>Etkin başlığın sırası; başlık yoksa -1.</returns>
        public static int ActiveIndex(IList<int> tops, int scrollTop, int viewport, int documentHeight)
        {
            if (tops == null || tops.Count == 0) return -1;

            // Sayfanın en altı: son başlıklar üste çıkamaz; görünürdeki son başlık etkin olur
            bool atBottom = documentHeight > viewport && scrollTop + viewport >= documentHeight - 2;
            if (atBottom)
            {
                for (int i = tops.Count - 1; i >= 0; i--)
                    if (tops[i] <= scrollTop + viewport - 20) return i;
            }

            // Normal: etkinlik çizgisini (üstten ActivationOffset) en son geçen başlık; en üstte ilk başlık
            int line = scrollTop + ActivationOffset;
            int active = 0;
            for (int i = 0; i < tops.Count; i++)
            {
                if (tops[i] <= line) active = i;
                else break;
            }
            return active;
        }
    }
}
