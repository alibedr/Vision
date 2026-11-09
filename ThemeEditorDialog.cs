using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Tema düzenleyici (Temalar → Temayı özelleştir…): hazır temalardan birini seçip 10 temel rengini değiştirme.
    /// Her değişiklik ANINDA tüm uygulamaya uygulanır (anlık önizleme); pencerede ayrıca küçük bir önizleme ve okunabilirlik
    /// (kontrast) uyarısı vardır. Kaydet: seçili tema ve değişiklikler config.xml'e yazılır. Varsayılana dön: görüntülenen
    /// temanın renkleri hazır değerlerine döner. İptal / kapat: pencere açılmadan önceki tema ve renkler geri gelir.
    /// </summary>
    public sealed class ThemeEditorDialog : DarkDialog
    {
        private readonly AppSettings _settings;
        private readonly Action _save;
        private readonly string _originalId;
        private readonly Color[] _originalColors;
        private readonly Dictionary<string, Color[]> _working = new Dictionary<string, Color[]>(StringComparer.OrdinalIgnoreCase);
        private string _currentId;
        private bool _saved;

        private readonly DarkComboBox _themeCombo = new DarkComboBox();
        private readonly Label _themeDesc = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly ColorRows _rows;
        private readonly Panel _preview = new BufferedPanel();
        private readonly Label _contrast = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Button _resetBtn = Theme.MakeButton("Varsayılana dön", Theme.ButtonKind.Secondary);
        private readonly DarkToolTip _tips = new DarkToolTip();

        public ThemeEditorDialog(AppSettings settings, Action save)
            : base("Temayı özelleştir", new Size(780, 560))
        {
            _settings = settings;
            _save = save;
            _originalId = Theme.CurrentThemeId;
            _originalColors = Theme.CurrentColors;
            _currentId = _originalId;
            _working[_currentId] = (Color[])_originalColors.Clone();   // kaydedilmiş değişiklikler zaten uygulanmış durumda

            Controls.Add(Caption("TEMA", 20, 18));
            _themeCombo.SetBounds(20, 38, 360, 26);
            foreach (ThemeDefinition d in ThemeCatalog.All) _themeCombo.Items.Add(new ThemeItem(this, d));
            Controls.Add(_themeCombo);
            _themeDesc.AutoSize = false;
            _themeDesc.SetBounds(20, 68, 360, 18);
            Controls.Add(_themeDesc);

            Controls.Add(Caption("RENKLER  ·  değiştirmek için tıklayın", 20, 96));
            _rows = new ColorRows(this) { Location = new Point(20, 116), Size = new Size(360, 10 * ColorRows.RowHeight) };
            Controls.Add(_rows);

            Controls.Add(Caption("ÖNİZLEME  ·  değişiklikler tüm uygulamada anında görünür", 400, 18));
            _preview.SetBounds(400, 38, 360, 330);
            _preview.Paint += (s, e) => PaintPreview(e.Graphics, _preview.ClientRectangle);
            Controls.Add(_preview);
            _contrast.AutoSize = false;
            _contrast.SetBounds(400, 376, 360, 74);
            Controls.Add(_contrast);

            // Alt şerit: [Varsayılana dön] ……… [İptal] [Kaydet]
            int y = ClientSize.Height - 48;
            _resetBtn.SetBounds(20, y, 150, 34);
            _tips.SetToolTip(_resetBtn, "Görüntülenen temanın tüm renklerini Vision'ın hazır değerlerine döndürür (Kaydet ile kalıcı olur)");
            var cancel = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);
            var ok = Theme.MakeButton("Kaydet", Theme.ButtonKind.Primary);
            ok.SetBounds(ClientSize.Width - 20 - 110, y, 110, 34);
            cancel.SetBounds(ok.Left - 8 - 100, y, 100, 34);
            Controls.AddRange(new Control[] { _resetBtn, cancel, ok });
            AcceptButton = ok;
            CancelButton = cancel;
            cancel.DialogResult = DialogResult.Cancel;

            _resetBtn.Click += (s, e) => ResetToDefaults();
            ok.Click += (s, e) => SaveAndClose();
            _themeCombo.SelectedIndexChanged += (s, e) =>
            {
                var it = _themeCombo.SelectedItem as ThemeItem;
                if (it != null && !string.Equals(it.Def.Id, _currentId, StringComparison.OrdinalIgnoreCase)) ShowTheme(it.Def.Id);
            };

            SelectComboItem(_currentId);
            RefreshUi();
        }

        /// <summary>Düzenleyiciyi açar; kaydedildiyse true.</summary>
        public static bool Edit(IWin32Window owner, AppSettings settings, Action save)
        {
            using (var d = new ThemeEditorDialog(settings, save))
            {
                d.ShowDialog(owner);
                return d._saved;
            }
        }

        private static Label Caption(string text, int x, int y)
        {
            var l = Theme.MakeLabel(text, Theme.SectionHeader, Theme.TextMuted);
            l.Location = new Point(x, y);
            return l;
        }

        // ---------- Durum ----------

        private ThemeDefinition CurrentDef { get { return ThemeCatalog.FindOrDefault(_currentId); } }

        private Color[] Working(string id)
        {
            Color[] c;
            if (!_working.TryGetValue(id, out c))
            {
                c = ThemeCatalog.EffectiveColors(ThemeCatalog.FindOrDefault(id), _settings.ThemeCustomizations);
                _working[id] = c;
            }
            return c;
        }

        private bool IsModified(string id)
        {
            ThemeDefinition def = ThemeCatalog.FindOrDefault(id);
            Color[] c = _working.ContainsKey(id) ? _working[id]
                      : ThemeCatalog.EffectiveColors(def, _settings.ThemeCustomizations);
            for (int i = 0; i < ThemeColorInfo.Count; i++)
                if (c[i].ToArgb() != def.Defaults[i].ToArgb()) return true;
            return false;
        }

        private void ShowTheme(string id)
        {
            _currentId = ThemeCatalog.FindOrDefault(id).Id;
            Theme.Apply(_currentId, Working(_currentId));   // anlık önizleme: tüm uygulama
            RefreshUi();
        }

        private void SetColor(ThemeColorKey key, Color color)
        {
            Color[] c = Working(_currentId);
            c[(int)key] = Color.FromArgb(255, color);
            Theme.Apply(_currentId, c);
            RefreshUi();
        }

        private void ResetToDefaults()
        {
            _working[_currentId] = CurrentDef.CopyDefaults();
            Theme.Apply(_currentId, _working[_currentId]);
            RefreshUi();
        }

        private void SaveAndClose()
        {
            if (_settings.ThemeCustomizations == null) _settings.ThemeCustomizations = new List<ThemeCustomization>();
            foreach (var kv in _working)
                ThemeCatalog.StoreCustomization(_settings.ThemeCustomizations, ThemeCatalog.FindOrDefault(kv.Key), kv.Value);
            _settings.UiThemeId = _currentId;
            _settings.UiTheme = Theme.Mode;
            _saved = true;
            if (_save != null) _save();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Kaydedilmeden kapandı: açılmadan önceki tema ve renkler geri gelir
            if (!_saved) Theme.Apply(_originalId, _originalColors);
            base.OnFormClosed(e);
        }

        private void SelectComboItem(string id)
        {
            for (int i = 0; i < _themeCombo.Items.Count; i++)
                if (string.Equals(((ThemeItem)_themeCombo.Items[i]).Def.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    if (_themeCombo.SelectedIndex != i) _themeCombo.SelectedIndex = i;
                    return;
                }
        }

        private void RefreshUi()
        {
            _themeDesc.Text = CurrentDef.Description;
            _resetBtn.Enabled = IsModified(_currentId);
            _themeCombo.Invalidate();
            _rows.Invalidate();
            _preview.Invalidate();
            UpdateContrast();
        }

        /// <summary>Okunabilirlik: yazı / zemin çiftlerinin kontrastı (WCAG; yazı için en az 4.5:1 önerilir).</summary>
        private void UpdateContrast()
        {
            Color[] c = Working(_currentId);
            var issues = new List<string>();
            Action<string, ThemeColorKey, ThemeColorKey> check = (what, fore, back) =>
            {
                double r = Theme.ContrastRatio(c[(int)fore], c[(int)back]);
                if (r < 4.5) issues.Add(what + " " + r.ToString("0.0") + ":1");
            };
            check("Yazı / Arka plan", ThemeColorKey.Text, ThemeColorKey.Window);
            check("Yazı / Paneller", ThemeColorKey.Text, ThemeColorKey.Surface);
            check("Terminal yazısı / zemini", ThemeColorKey.Terminal, ThemeColorKey.Input);
            if (issues.Count == 0)
            {
                _contrast.ForeColor = Theme.Success;
                _contrast.Text = "✓  Okunabilirlik iyi: yazı ile zemin arasındaki kontrast yeterli.";
            }
            else
            {
                _contrast.ForeColor = Theme.Warning;
                _contrast.Text = "⚠  Kontrast düşük, yazılar zor okunabilir (en az 4.5:1 önerilir):\n" + string.Join("  ·  ", issues);
            }
        }

        private void PickColor(ThemeColorKey key)
        {
            Color[] c = Working(_currentId);
            using (var d = new ColorDialog { FullOpen = true, AnyColor = true, Color = c[(int)key] })
            {
                // Özel renkler kutusunda temanın 10 rengi hazır gelir
                var custom = new int[16];
                for (int i = 0; i < ThemeColorInfo.Count; i++) custom[i] = ColorTranslator.ToWin32(c[i]);
                d.CustomColors = custom;
                if (d.ShowDialog(this) == DialogResult.OK) SetColor(key, d.Color);
            }
        }

        // ---------- Önizleme ----------

        private void PaintPreview(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Window)) g.FillRectangle(b, r);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);

            // Kart: başlık + terminal + komut satırı
            var card = new Rectangle(14, 14, r.Width - 28, 206);
            using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, card);
            var head = new Rectangle(card.X, card.Y, card.Width, 30);
            using (var b = new SolidBrush(Theme.Success)) g.FillEllipse(b, head.X + 10, head.Y + 11, 8, 8);
            TextRenderer.DrawText(g, "Kart 1  ·  192.168.1.10", Theme.UiBold, new Point(head.X + 24, head.Y + 7), Theme.Text, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "bağlı · 12 dk", Theme.UiSmall, new Point(head.Right - 84, head.Y + 9), Theme.TextMuted, TextFormatFlags.NoPrefix);

            var term = new Rectangle(card.X, head.Bottom, card.Width, 132);
            using (var b = new SolidBrush(Theme.Input)) g.FillRectangle(b, term);
            using (var mono = new Font(Theme.Mono.FontFamily, 8.5f))
            using (var monoBold = new Font(Theme.Mono.FontFamily, 8.5f, FontStyle.Bold))
            {
                int ly = term.Y + 6, lh = 17;
                Action<string, Color, Font> line = (t, col, f) =>
                {
                    TextRenderer.DrawText(g, t, f, new Point(term.X + 8, ly), col, TextFormatFlags.NoPrefix);
                    ly += lh;
                };
                line("-> version", Theme.Terminal, mono);
                line("VxWorks (for MPC8548) version 6.4.", Theme.Terminal, mono);
                line("value = 41 = 0x29", Theme.Terminal, mono);
                line("-> moduleShow", Theme.Terminal, mono);
                line("app.out   0x1d58e10   2", Theme.Terminal, mono);
                line("*** Bağlantı koptu: kart cevap vermiyor", Theme.AlertText, monoBold);
                line("-> _", Theme.Terminal, mono);
            }
            var input = new Rectangle(card.X, term.Bottom, card.Width, card.Bottom - term.Bottom);
            using (var b = new SolidBrush(Theme.SurfaceHigh)) g.FillRectangle(b, input);
            var field = new Rectangle(input.X + 8, input.Y + 8, input.Width - 16, input.Height - 16);
            using (var b = new SolidBrush(Theme.Input)) g.FillRectangle(b, field);
            using (var p = new Pen(Theme.Accent)) g.DrawRectangle(p, field);
            TextRenderer.DrawText(g, "Komut yazıp Enter'a basın", Theme.UiSmall, new Point(field.X + 6, field.Y + 6), Theme.TextFaint, TextFormatFlags.NoPrefix);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, card);

            // Düğmeler ve durum etiketleri
            int y = card.Bottom + 16;
            var primary = new Rectangle(14, y, 120, 30);
            using (var b = new SolidBrush(Theme.AccentFill)) g.FillRectangle(b, primary);
            TextRenderer.DrawText(g, "Bağlantı Kur", Theme.UiBold, primary, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            var secondary = new Rectangle(primary.Right + 8, y, 90, 30);
            using (var b = new SolidBrush(Theme.SurfaceHigh)) g.FillRectangle(b, secondary);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, secondary);
            TextRenderer.DrawText(g, "İptal", Theme.Ui, secondary, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            y += 42;
            int x = 14;
            x = Chip(g, x, y, "✓ Bağlı", Theme.Success, Theme.SuccessBanner);
            x = Chip(g, x, y, "Bağlanıyor", Theme.Warning, Theme.WarningBanner);
            Chip(g, x, y, "Hata", Theme.Danger, Theme.ErrorBanner);
            TextRenderer.DrawText(g, "İkincil yazı · ipucu", Theme.UiSmall, new Point(14, y + 30), Theme.TextMuted, TextFormatFlags.NoPrefix);
        }

        private static int Chip(Graphics g, int x, int y, string text, Color fore, Color back)
        {
            Size sz = TextRenderer.MeasureText(text, Theme.UiSmall);
            var rect = new Rectangle(x, y, sz.Width + 14, 22);
            using (var b = new SolidBrush(back)) g.FillRectangle(b, rect);
            using (var p = new Pen(fore)) g.DrawRectangle(p, rect);
            TextRenderer.DrawText(g, text, Theme.UiSmall, rect, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return rect.Right + 8;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }

        // ---------- Yardımcı sınıflar ----------

        /// <summary>Açılır listedeki tema: adı + bu pencerede ya da kayıtta değiştirildiyse işaret.</summary>
        private sealed class ThemeItem
        {
            private readonly ThemeEditorDialog _owner;
            public ThemeItem(ThemeEditorDialog owner, ThemeDefinition def) { _owner = owner; Def = def; }
            public ThemeDefinition Def { get; private set; }
            public override string ToString() { return Def.Name + (_owner.IsModified(Def.Id) ? "   (özelleştirildi)" : ""); }
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        /// <summary>10 renk satırı: renk örneği, ad, açıklama, #RRGGBB; değiştirilmiş renkte nokta. Tıklayınca renk seçici.</summary>
        private sealed class ColorRows : Control
        {
            public const int RowHeight = 34;
            private readonly ThemeEditorDialog _owner;
            private int _hover = -1;

            public ColorRows(ThemeEditorDialog owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                ResizeRedraw = true;
                Cursor = Cursors.Hand;
                TabStop = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int h = e.Y / RowHeight;
                if (h >= ThemeColorInfo.Count) h = -1;
                if (h != _hover) { _hover = h; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = -1;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int i = e.Y / RowHeight;
                if (e.Button == MouseButtons.Left && i >= 0 && i < ThemeColorInfo.Count) _owner.PickColor((ThemeColorKey)i);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(Theme.Surface);
                Color[] colors = _owner.Working(_owner._currentId);
                ThemeDefinition def = _owner.CurrentDef;
                for (int i = 0; i < ThemeColorInfo.Count; i++)
                {
                    var key = (ThemeColorKey)i;
                    var row = new Rectangle(0, i * RowHeight, Width, RowHeight);
                    if (i == _hover) using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, row);

                    var sw = new Rectangle(row.X + 6, row.Y + 7, 30, 20);
                    using (var b = new SolidBrush(colors[i])) g.FillRectangle(b, sw);
                    using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, sw);

                    TextRenderer.DrawText(g, ThemeColorInfo.Label(key), Theme.UiBold, new Point(sw.Right + 10, row.Y + 2), Theme.Text, TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, ThemeColorInfo.Description(key), Theme.UiSmall, new Point(sw.Right + 10, row.Y + 17), Theme.TextMuted, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                    string hex = ThemeCatalog.ToHex(colors[i]);
                    bool changed = colors[i].ToArgb() != def.Defaults[i].ToArgb();
                    Size hs = TextRenderer.MeasureText(hex, Theme.Mono);
                    TextRenderer.DrawText(g, hex, Theme.Mono, new Point(row.Right - hs.Width - 6, row.Y + 9), changed ? Theme.Accent : Theme.TextMuted, TextFormatFlags.NoPrefix);
                    if (changed)
                        using (var b = new SolidBrush(Theme.Accent))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.FillEllipse(b, row.Right - hs.Width - 18, row.Y + 14, 6, 6);
                            g.SmoothingMode = SmoothingMode.Default;
                        }
                    using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, row.X, row.Bottom - 1, row.Right, row.Bottom - 1);
                }
            }
        }
    }
}
