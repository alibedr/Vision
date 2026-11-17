using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Hızlı komut listesi (G46): en fazla <see cref="Max"/> komut, biri seçili. Kart penceresinin ve genel komut
    /// çubuğunun hızlı komut düğmeleri aynı davranır: sol bölme seçili komutu gönderir, ▾ / sağ tık listeyi açar
    /// (komuta tıklamak onu seçer, göndermez; ekle / düzenle / yukarı-aşağı taşı / kaldır).
    /// </summary>
    internal sealed class QuickCommandSet
    {
        public const int Max = CardConfig.MaxQuickCommands;

        private readonly Func<List<string>> _list;
        private readonly Func<string> _getActive;
        private readonly Action<string> _setActive;
        private readonly Action _changed;
        private readonly Func<string, string, Func<string, string>, string> _ask;   // (başlık, mevcut, doğrulama) → yeni değer / null

        public QuickCommandSet(Func<List<string>> list, Func<string> getActive, Action<string> setActive, Action changed,
                               Func<string, string, Func<string, string>, string> ask)
        {
            _list = list;
            _getActive = getActive;
            _setActive = setActive;
            _changed = changed;
            _ask = ask;
        }

        public List<string> Items { get { return _list(); } }
        public string Active { get { return (_getActive() ?? "").Trim(); } }

        /// <summary>Listeyi düzenler (boş / yinelenen / fazla atılır; seçili listede değilse eklenir ya da ilki seçilir).</summary>
        public static void Normalize(ref List<string> list, ref string active)
        {
            var clean = new List<string>();
            if (list != null)
                foreach (string c in list)
                {
                    string t = (c ?? "").Trim();
                    if (t.Length > 0 && !clean.Contains(t) && clean.Count < Max) clean.Add(t);
                }
            string a = (active ?? "").Trim();
            if (a.Length > 0 && !clean.Contains(a))
            {
                if (clean.Count < Max) clean.Insert(0, a);
                else a = "";
            }
            list = clean;
            active = a.Length > 0 ? a : (clean.Count > 0 ? clean[0] : null);
        }

        public void ShowMenu(ContextMenuStrip menu, Control anchor)
        {
            menu.Items.Clear();
            List<string> list = Items;
            string active = Active;
            if (list.Count > 0)
            {
                menu.Items.Add(new ToolStripMenuItem("Seçili komut (tıklayınca seçilir, gönderilmez)") { Enabled = false });
                foreach (string c in list)
                {
                    string cmd = c;
                    var item = new ToolStripMenuItem((cmd == active ? "●  " : "○  ") + cmd) { ForeColor = Theme.Text };
                    item.Click += (s, e) => Select(cmd);
                    menu.Items.Add(item);
                }
                menu.Items.Add(new ToolStripSeparator());
            }
            if (list.Count < Max)
                Add(menu, list.Count == 0 ? "Hızlı komut ekle…" : "Komut ekle… (" + list.Count + " / " + Max + ")", AddCommand);
            if (active.Length > 0)
            {
                int idx = list.IndexOf(active);
                Add(menu, "Seçili komutu düzenle…", EditActive);
                if (idx > 0) Add(menu, "Seçili komutu yukarı taşı", () => Move(-1));
                if (idx >= 0 && idx < list.Count - 1) Add(menu, "Seçili komutu aşağı taşı", () => Move(+1));
                Add(menu, "Seçili komutu kaldır", RemoveActive);
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private static void Add(ContextMenuStrip menu, string text, Action action)
        {
            var item = new ToolStripMenuItem(text) { ForeColor = Theme.Text };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        public void Select(string cmd)
        {
            _setActive(cmd);
            _changed();
        }

        private string Ask(string title, string current)
        {
            List<string> list = Items;
            return _ask(title, current, v =>
            {
                string err = QuickCommandValidator.Validate(v);
                if (err != null) return err;
                string t = (v ?? "").Trim();
                if (t.Length > 0 && t != current && list.Contains(t)) return "Bu komut listede zaten var.";
                return null;
            });
        }

        public void AddCommand()
        {
            List<string> list = Items;
            if (list.Count >= Max) return;
            string value = Ask("Hızlı komut ekle", "");
            if (value == null || value.Trim().Length == 0) return;
            list.Add(value.Trim());
            _setActive(value.Trim());      // eklenen komut seçili olur
            _changed();
        }

        public void EditActive()
        {
            List<string> list = Items;
            string current = Active;
            if (current.Length == 0) { AddCommand(); return; }
            string value = Ask("Hızlı komutu düzenle", current);
            if (value == null) return;
            string t = value.Trim();
            if (t.Length == 0) { RemoveActive(); return; }
            int idx = list.IndexOf(current);
            if (idx >= 0) list[idx] = t; else list.Add(t);
            _setActive(t);
            _changed();
        }

        public void Move(int delta)
        {
            List<string> list = Items;
            int idx = list.IndexOf(Active);
            int to = idx + delta;
            if (idx < 0 || to < 0 || to >= list.Count) return;
            string c = list[idx];
            list.RemoveAt(idx);
            list.Insert(to, c);
            _changed();
        }

        public void RemoveActive()
        {
            List<string> list = Items;
            int idx = list.IndexOf(Active);
            if (idx >= 0) list.RemoveAt(idx);
            _setActive(list.Count > 0 ? list[Math.Min(Math.Max(idx, 0), list.Count - 1)] : null);
            _changed();
        }

        /// <summary>Düğme ipucu.</summary>
        public string Tip(string target)
        {
            string cmd = Active;
            int n = Items.Count;
            if (cmd.Length == 0) return "Hızlı komut ayarlanmadı — tıklayıp bir komut ekleyin (en fazla " + Max + ")";
            return "Hızlı komut: " + cmd + "\nSol: " + target + "  ·  ▾ ya da sağ tık: " +
                   (n > 1 ? n + " komut arasından seç, " : "") + "ekle / düzenle / sırala / kaldır (en fazla " + Max + ")";
        }
    }
}
