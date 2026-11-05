using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>Komut Kütüphanesi: sağ taraftaki komut sayfası (görüntüle / düzenle) ve modül sayfası, işlemler.</summary>
    public sealed partial class CommandLibraryView
    {
        // ---------- Komut sayfası ----------
        private readonly TableLayoutPanel _form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Window, Padding = new Padding(28, 18, 28, 10) };
        private readonly TextBox _name = new TextBox();
        private readonly Panel _metaRow = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Margin = new Padding(0) };
        private readonly IconButton _moduleChip = new IconButton(IconKind.Module, IconTone.Ghost) { IconSize = 14, TabStop = false };
        private readonly Label _meta = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Label _moduleCaption = Theme.MakeLabel("MODÜL", Theme.SectionHeader, Theme.TextMuted);
        private readonly ComboBox _moduleCombo = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _description = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        private readonly TextBox _usage = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
        private readonly TextBox _output = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
        private readonly IconButton _copyUsageBtn = new IconButton(IconKind.Copy, IconTone.Ghost) { Label = "Kopyala", IconSize = 14 };
        private readonly IconButton _editBtn = new IconButton(IconKind.Edit, IconTone.Secondary) { Label = "Düzenle" };
        private readonly IconButton _deleteBtn = new IconButton(IconKind.Trash, IconTone.Danger) { Label = "Sil" };
        private readonly IconButton _saveBtn = new IconButton(IconKind.Check, IconTone.Primary) { Label = "Kaydet" };
        private readonly IconButton _cancelBtn = new IconButton(IconKind.None, IconTone.Secondary) { Label = "İptal" };
        private readonly Label _error = Theme.MakeLabel("", Theme.Ui, Theme.Danger);
        private readonly Label _keysHint = Theme.MakeLabel("Ctrl+S: kaydet  ·  Esc: iptal", Theme.UiSmall, Theme.TextMuted);
        private readonly Panel _buttons = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Margin = new Padding(0) };
        private readonly Timer _copiedTimer = new Timer { Interval = 1400 };
        private string _newFromModuleKey;   // yeni komut taslağı hangi modül sayfasından başlatıldı (iptalde dönmek için)

        // ---------- Modül sayfası ----------
        private readonly Panel _modulePage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(28, 22, 28, 18), Visible = false };
        private readonly Panel _modHeader = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Theme.Window };
        private readonly Label _modTitle = Theme.MakeLabel("", new Font("Segoe UI Semibold", 15f), Theme.Text);
        private readonly Label _modStats = Theme.MakeLabel("", Theme.UiSmall, Theme.TextMuted);
        private readonly Label _modDesc = new Label { Dock = DockStyle.Top, AutoSize = false, Font = new Font("Segoe UI", 10f), ForeColor = Theme.Text, BackColor = Theme.Window, Padding = new Padding(2, 6, 0, 6) };
        private readonly IconButton _modAddBtn = new IconButton(IconKind.Plus, IconTone.Primary) { Label = "Komut ekle" };
        private readonly IconButton _modEditBtn = new IconButton(IconKind.Edit, IconTone.Secondary) { Label = "Düzenle" };
        private readonly IconButton _modDeleteBtn = new IconButton(IconKind.Trash, IconTone.Danger) { Label = "Sil" };
        private readonly ListBox _modList = new ListBox { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 50, IntegralHeight = false };
        private readonly Label _modEmpty = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, BackColor = Theme.Input, Visible = false };
        private int _modHover = -1;

        // =====================================================================
        // Komut sayfası
        // =====================================================================

        private void BuildCommandPage()
        {
            _name.Font = new Font("Consolas", 16f, FontStyle.Bold);
            _description.Font = new Font("Segoe UI", 10f);
            _usage.Font = new Font("Consolas", 10f);
            _output.Font = new Font("Consolas", 10f);
            foreach (TextBox t in new[] { _name, _description, _usage, _output })
            {
                t.Dock = DockStyle.Fill;
                t.Margin = new Padding(0, 0, 0, 4);
                Theme.ApplyDarkScrollBars(t);
            }

            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // ad
            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // modül + meta
            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // AÇIKLAMA
            _form.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // YAZILIŞ
            _form.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // ÖRNEK ÇIKTI
            _form.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            _form.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));   // düğmeler
            _form.RowCount = 9;

            _form.Controls.Add(_name, 0, 0);

            // Modül çipi (görüntüleme) / modül seçimi (düzenleme) + son değişiklik
            _moduleChip.Font = Theme.Ui;
            _moduleChip.Height = 26;
            _moduleChip.SetTip(_tips, "Modülün sayfasına git");
            Theme.StyleCombo(_moduleCombo);
            _moduleCombo.Width = 260;
            _tips.SetToolTip(_moduleCombo, "Komutun bulunacağı modül");
            _metaRow.Controls.AddRange(new Control[] { _moduleChip, _meta, _moduleCaption, _moduleCombo });
            _metaRow.Resize += (s, e) => LayoutMetaRow();
            _form.Controls.Add(_metaRow, 0, 1);

            _form.Controls.Add(SectionCaption("AÇIKLAMA", null), 0, 2);
            _form.Controls.Add(Framed(_description), 0, 3);
            _copyUsageBtn.Font = Theme.UiSmall;
            _copyUsageBtn.Size = new Size(100, 26);
            _copyUsageBtn.SetTip(_tips, "Yazılışı panoya kopyala (terminale yapıştırmak için)");
            _form.Controls.Add(SectionCaption("YAZILIŞ / ÖRNEK KULLANIM", _copyUsageBtn), 0, 4);
            _form.Controls.Add(Framed(_usage), 0, 5);
            _form.Controls.Add(SectionCaption("ÖRNEK ÇIKTI", null), 0, 6);
            _form.Controls.Add(Framed(_output), 0, 7);

            _editBtn.Size = new Size(112, 34);
            _deleteBtn.Size = new Size(84, 34);
            _saveBtn.Size = new Size(108, 34);
            _cancelBtn.Size = new Size(92, 34);
            _editBtn.SetTip(_tips, "Komutu düzenle (F2 / çift tık)");
            _deleteBtn.SetTip(_tips, "Komutu kütüphaneden sil (Delete)");
            _saveBtn.SetTip(_tips, "Kaydet (Ctrl+S)");
            _cancelBtn.SetTip(_tips, "Değişiklikleri bırak (Esc)");
            _error.AutoSize = false;
            _error.AutoEllipsis = true;
            _error.TextAlign = ContentAlignment.MiddleLeft;
            _buttons.Controls.AddRange(new Control[] { _error, _keysHint, _editBtn, _deleteBtn, _saveBtn, _cancelBtn });
            _buttons.Resize += (s, e) => LayoutButtons();
            _buttons.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 6, _buttons.Width, 6); };
            _form.Controls.Add(_buttons, 0, 8);

            _detail.Controls.Add(_form);
        }

        private void WireCommandPage()
        {
            _editBtn.Click += (s, e) => StartEdit();
            _deleteBtn.Click += (s, e) => DeleteCurrent();
            _saveBtn.Click += (s, e) => SaveEdit();
            _cancelBtn.Click += (s, e) => CancelEdit();
            _copyUsageBtn.Click += (s, e) => CopyUsage();
            _copiedTimer.Tick += (s, e) => { _copiedTimer.Stop(); _copyUsageBtn.Icon = IconKind.Copy; _copyUsageBtn.Label = "Kopyala"; };
            _moduleChip.Click += (s, e) => { if (_current != null) GoToModule(_current.Module ?? ""); };
            _moduleCombo.SelectedIndexChanged += (s, e) => { if (_editing) { _error.Text = ""; UpdateButtons(); } };
            foreach (TextBox t in new[] { _name, _description, _usage, _output })
                t.TextChanged += (s, e) => { if (_editing) { _error.Text = ""; UpdateButtons(); } };
        }

        private void LayoutMetaRow()
        {
            int cy = 4;
            if (_editing)
            {
                _moduleCaption.Location = new Point(3, cy + 6);
                _moduleCombo.Location = new Point(_moduleCaption.Right + 10, cy);
            }
            else
            {
                Size ts = TextRenderer.MeasureText(_moduleChip.Label, _moduleChip.Font, Size.Empty, TextFormatFlags.NoPadding);
                _moduleChip.Size = new Size(ts.Width + 14 + 8 + 20, 26);
                _moduleChip.Location = new Point(-4, cy);
                _meta.Location = new Point(_moduleChip.Right + 12, cy + 6);
            }
        }

        /// <summary>Alt düğmeler sağa yaslı; görüntülemede [Sil][Düzenle], düzenlemede [İptal][Kaydet].</summary>
        private void LayoutButtons()
        {
            int y = 16, right = _buttons.Width;
            _saveBtn.Location = new Point(right - _saveBtn.Width, y);
            _cancelBtn.Location = new Point(_saveBtn.Left - 8 - _cancelBtn.Width, y);
            _editBtn.Location = new Point(right - _editBtn.Width, y);
            _deleteBtn.Location = new Point(_editBtn.Left - 8 - _deleteBtn.Width, y);
            int leftEdge = (_editing ? _cancelBtn.Left : _deleteBtn.Left) - 14;
            _keysHint.Location = new Point(Math.Max(0, leftEdge - _keysHint.Width), y + 10);
            int errorRight = _editing ? _keysHint.Left - 12 : leftEdge;
            _error.SetBounds(0, y, Math.Max(40, errorRight), 34);
        }

        private static Control SectionCaption(string text, Control extra)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Margin = new Padding(0) };
            var l = Theme.MakeLabel(text, Theme.SectionHeader, Theme.TextMuted);
            l.Location = new Point(2, p.Height - 20);
            p.Controls.Add(l);
            p.Resize += (s, e) => l.Location = new Point(2, p.Height - 20);
            if (extra != null)
            {
                p.Controls.Add(extra);
                p.Resize += (s, e) => extra.Location = new Point(p.Width - extra.Width + 4, p.Height - extra.Height - 2);
            }
            return p;
        }

        /// <summary>Metin kutusunu 1px kenarlıklı bir çerçeveye koyar.</summary>
        private static Control Framed(Control t)
        {
            var tb = t as TextBox;
            if (tb != null) tb.BorderStyle = BorderStyle.None;
            var frame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 2) };
            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input, Padding = new Padding(12, 9, 6, 6) };
            inner.Controls.Add(t);
            frame.Controls.Add(inner);
            inner.Click += (s, e) => t.Focus();
            return frame;
        }

        private sealed class ModuleChoice
        {
            public string Key;
            public string Text;
            public override string ToString() { return Text; }
        }

        private void FillModuleCombo(string selectKey)
        {
            _moduleCombo.Items.Clear();
            foreach (CommandModule m in _library.Modules) _moduleCombo.Items.Add(new ModuleChoice { Key = m.Name, Text = m.Name });
            _moduleCombo.Items.Add(new ModuleChoice { Key = "", Text = "(" + CommandLibrary.NoModuleName + ")" });
            int idx = 0;
            for (int i = 0; i < _moduleCombo.Items.Count; i++)
                if (CommandLibrary.SameName(((ModuleChoice)_moduleCombo.Items[i]).Key, selectKey ?? "")) { idx = i; break; }
            if (string.IsNullOrEmpty(selectKey)) idx = _moduleCombo.Items.Count - 1;
            _moduleCombo.SelectedIndex = idx;
        }

        private string ComboModule()
        {
            var c = _moduleCombo.SelectedItem as ModuleChoice;
            return c != null ? c.Key : "";
        }

        private void ShowDoc(CommandDoc d)
        {
            _current = d;
            _currentModuleKey = null;
            _editing = false;
            _isNew = false;
            _error.Text = "";
            if (d != null)
            {
                _name.Text = d.Name;
                _description.Text = CommandLibrary.ToTextBox(d.Description);
                _usage.Text = CommandLibrary.ToTextBox(d.Usage);
                _output.Text = CommandLibrary.ToTextBox(d.ExampleOutput);
                _meta.Text = string.IsNullOrEmpty(d.Modified) ? "" : "Son değişiklik: " + d.Modified;
                bool noModule = string.IsNullOrEmpty(d.Module);
                _moduleChip.Label = noModule ? CommandLibrary.NoModuleName : d.Module;
                _moduleChip.Icon = noModule ? IconKind.Folder : IconKind.Module;
            }
            ApplyMode();
            UpdateEmpty();
        }

        /// <summary>Görüntüleme / düzenleme kipine göre alanları ve düğmeleri ayarlar.</summary>
        private void ApplyMode()
        {
            bool edit = _editing;
            foreach (TextBox t in new[] { _name, _description, _usage, _output })
            {
                t.ReadOnly = !edit;
                t.TabStop = edit;
            }
            _name.BackColor = edit ? Theme.Input : Theme.Window;
            _name.BorderStyle = edit ? BorderStyle.FixedSingle : BorderStyle.None;
            _name.ForeColor = Theme.Text;
            _description.ForeColor = Theme.Text;
            _usage.ForeColor = Theme.Terminal;
            _output.ForeColor = Theme.Terminal;
            foreach (TextBox t in new[] { _description, _usage, _output }) t.BackColor = Theme.Input;
            if (edit) Theme.SetCueBanner(_name, "Komut adı (ör. taskSpawn)");

            _moduleChip.Visible = !edit && _current != null;
            _meta.Visible = !edit && _current != null && _meta.Text.Length > 0;
            _moduleCaption.Visible = _moduleCombo.Visible = edit;
            LayoutMetaRow();

            _editBtn.Visible = _deleteBtn.Visible = !edit && _current != null;
            _saveBtn.Visible = _cancelBtn.Visible = _keysHint.Visible = edit;
            _copyUsageBtn.Visible = !edit && _current != null && !string.IsNullOrEmpty(_current.Usage);
            LayoutButtons();
            UpdateButtons();
            _newBtn.Enabled = !_isNew;
            _newModuleBtn.Enabled = !_editing;
            _modAddBtn.Enabled = !_editing;
        }

        private void UpdateButtons()
        {
            if (!_editing) return;
            _saveBtn.Enabled = IsDirty();
        }

        private CommandDoc Draft()
        {
            return new CommandDoc
            {
                Name = _name.Text,
                Description = _description.Text,
                Usage = _usage.Text,
                ExampleOutput = _output.Text,
                Module = ComboModule(),
            };
        }

        private bool IsDirty()
        {
            if (!_editing) return false;
            CommandDoc d = Draft();
            if (_isNew)
                return (d.Name ?? "").Trim().Length > 0 || CommandLibrary.NormalizeText(d.Description).Length > 0 ||
                       CommandLibrary.NormalizeText(d.Usage).Length > 0 || CommandLibrary.NormalizeText(d.ExampleOutput).Length > 0;
            return !d.SameContent(_current);
        }

        // =====================================================================
        // Modül sayfası
        // =====================================================================

        private void BuildModulePage()
        {
            _modTitle.AutoSize = false;
            _modTitle.AutoEllipsis = true;
            _modTitle.SetBounds(58, 0, 300, 32);
            _modStats.Location = new Point(60, 34);
            _modAddBtn.Size = new Size(124, 34);
            _modEditBtn.Size = new Size(112, 34);
            _modDeleteBtn.Size = new Size(84, 34);
            _modAddBtn.SetTip(_tips, "Bu modüle yeni komut ekle  ·  Ctrl+Shift+N");
            _modEditBtn.SetTip(_tips, "Modülün adını ve açıklamasını değiştir (F2)");
            _modDeleteBtn.SetTip(_tips, "Modülü sil — içindeki komutlar korunabilir ya da birlikte silinebilir");
            _modHeader.Controls.AddRange(new Control[] { _modTitle, _modStats, _modAddBtn, _modEditBtn, _modDeleteBtn });
            _modHeader.Resize += (s, e) => LayoutModuleHeader();
            _modHeader.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                bool none = _currentModuleKey != null && _currentModuleKey.Length == 0;
                using (var path = IconRenderer.RoundRect(new RectangleF(0.5f, 4.5f, 44, 44), 9))
                using (var br = new SolidBrush(none ? Theme.SurfaceHigh : Theme.AccentSurface))
                    g.FillPath(br, path);
                IconRenderer.Draw(g, none ? IconKind.Folder : IconKind.Module, new RectangleF(11, 15, 22, 22), none ? Theme.TextMuted : Theme.Accent);
            };

            var caption = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Window };
            var capLabel = Theme.MakeLabel("KOMUTLAR", Theme.SectionHeader, Theme.TextMuted);
            capLabel.Location = new Point(2, 16);
            var dragHint = Theme.MakeLabel("Komutları soldaki listeden sürükleyip bir modülün üzerine bırakarak taşıyabilirsiniz.", Theme.UiSmall, Theme.TextMuted);
            caption.Controls.Add(capLabel);
            caption.Controls.Add(dragHint);
            caption.Resize += (s, e) => dragHint.Location = new Point(Math.Max(capLabel.Right + 16, caption.Width - dragHint.Width), 16);

            _modList.Dock = DockStyle.Fill;
            _modList.BackColor = Theme.Input;
            Theme.ApplyDarkScrollBars(_modList);
            var listInner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Input };
            listInner.Controls.Add(_modList);
            listInner.Controls.Add(_modEmpty);
            var frame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Padding = new Padding(1) };
            frame.Controls.Add(listInner);

            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border };
            _modulePage.Controls.Add(frame);
            _modulePage.Controls.Add(caption);
            _modulePage.Controls.Add(_modDesc);
            _modulePage.Controls.Add(sep);
            _modulePage.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Theme.Window });
            _modulePage.Controls.Add(_modHeader);
            _modulePage.Resize += (s, e) => SizeModuleDescription();
            _detail.Controls.Add(_modulePage);
        }

        private void WireModulePage()
        {
            _modAddBtn.Click += (s, e) => StartNew(_currentModuleKey ?? "");
            _modEditBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentModuleKey)) EditModule(_currentModuleKey); };
            _modDeleteBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentModuleKey)) DeleteModule(_currentModuleKey); };
            _modList.DrawItem += ModList_DrawItem;
            _modList.MouseMove += (s, e) =>
            {
                int i = _modList.IndexFromPoint(e.Location);
                if (i >= 0 && !_modList.GetItemRectangle(i).Contains(e.Location)) i = -1;
                if (i == _modHover) return;
                int old = _modHover;
                _modHover = i;
                if (old >= 0 && old < _modList.Items.Count) _modList.Invalidate(_modList.GetItemRectangle(old));
                if (i >= 0) _modList.Invalidate(_modList.GetItemRectangle(i));
                _modList.Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            };
            _modList.MouseLeave += (s, e) => { _modHover = -1; _modList.Invalidate(); };
            _modList.MouseClick += (s, e) =>
            {
                int i = _modList.IndexFromPoint(e.Location);
                if (i >= 0 && _modList.GetItemRectangle(i).Contains(e.Location)) OpenCommand((CommandDoc)_modList.Items[i]);
            };
            _modList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _modList.SelectedItem is CommandDoc) { e.SuppressKeyPress = true; OpenCommand((CommandDoc)_modList.SelectedItem); }
            };
        }

        private void LayoutModuleHeader()
        {
            int right = _modHeader.Width;
            int y = 8;
            if (_modDeleteBtn.Visible) { _modDeleteBtn.Location = new Point(right - _modDeleteBtn.Width, y); right = _modDeleteBtn.Left - 8; }
            if (_modEditBtn.Visible) { _modEditBtn.Location = new Point(right - _modEditBtn.Width, y); right = _modEditBtn.Left - 8; }
            _modAddBtn.Location = new Point(right - _modAddBtn.Width, y);
            _modTitle.SetBounds(58, 0, Math.Max(80, _modAddBtn.Left - 16 - 58), 32);
            _modStats.MaximumSize = new Size(Math.Max(80, _modAddBtn.Left - 16 - 60), 0);
        }

        private void SizeModuleDescription()
        {
            int w = Math.Max(100, _modulePage.ClientSize.Width - _modulePage.Padding.Horizontal);
            Size sz = TextRenderer.MeasureText(_modDesc.Text, _modDesc.Font, new Size(w - 4, 0), TextFormatFlags.WordBreak);
            _modDesc.Height = Math.Min(160, sz.Height + _modDesc.Padding.Vertical + 6);
        }

        private void ShowModule(string key)
        {
            _current = null;
            _currentModuleKey = key ?? "";
            _editing = false;
            _isNew = false;
            FillModulePage();
            ApplyMode();
            UpdateEmpty();
        }

        private void FillModulePage()
        {
            string key = _currentModuleKey ?? "";
            CommandModule mod = _library.FindModule(key);
            bool none = key.Length == 0 || mod == null;
            List<CommandDoc> docs = _library.CommandsIn(none ? "" : mod.Name);
            _modTitle.Text = none ? CommandLibrary.NoModuleName + " komutlar" : mod.Name;
            string stats = docs.Count + " komut";
            if (!none && !string.IsNullOrEmpty(mod.Modified)) stats += "  ·  Son değişiklik: " + mod.Modified;
            _modStats.Text = stats;
            if (none)
            {
                _modDesc.Text = "Hiçbir modüle atanmamış komutlar. Bir komutu modüle taşımak için soldaki listede sürükleyip modülün üzerine bırakın ya da sağ tıklayıp \"Modüle taşı\"yı seçin.";
                _modDesc.ForeColor = Theme.TextMuted;
            }
            else if (string.IsNullOrEmpty(mod.Description))
            {
                _modDesc.Text = "Açıklama eklenmemiş. \"Düzenle\" ile bu modülün neyi kapsadığını yazabilirsiniz.";
                _modDesc.ForeColor = Theme.TextMuted;
            }
            else
            {
                _modDesc.Text = mod.Description;
                _modDesc.ForeColor = Theme.Text;
            }
            _modEditBtn.Visible = _modDeleteBtn.Visible = !none;
            LayoutModuleHeader();
            SizeModuleDescription();
            _modHeader.Invalidate();

            _modList.BeginUpdate();
            _modList.Items.Clear();
            foreach (CommandDoc d in docs) _modList.Items.Add(d);
            _modList.EndUpdate();
            _modHover = -1;
            _modEmpty.Text = "Bu modülde henüz komut yok.\n\n\"Komut ekle\" ile yeni komut ekleyin ya da soldaki listeden\nkomutları sürükleyip bu modülün üzerine bırakın.";
            _modEmpty.Visible = docs.Count == 0;
            _modList.Visible = docs.Count > 0;
        }

        private void ModList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _modList.Items.Count) return;
            var d = (CommandDoc)_modList.Items[e.Index];
            Graphics g = e.Graphics;
            Rectangle b = e.Bounds;
            bool hover = e.Index == _modHover;
            bool focused = (e.State & DrawItemState.Selected) != 0 && _modList.Focused;
            using (var bg = new SolidBrush(hover || focused ? Theme.Hover : Theme.Input)) g.FillRectangle(bg, b);
            if (e.Index > 0) using (var p = new Pen(Theme.RowDivider)) g.DrawLine(p, b.X + 14, b.Y, b.Right - 14, b.Y);

            int x = b.X + 16;
            int nameW;
            using (var nf = new Font("Consolas", 10.5f, FontStyle.Bold))
            {
                nameW = Math.Min(b.Width / 3, TextRenderer.MeasureText(d.Name, nf, Size.Empty, TextFormatFlags.NoPadding).Width + 8);
                nameW = Math.Max(nameW, 120);
                TextRenderer.DrawText(g, d.Name, nf, new Rectangle(x, b.Y + 6, nameW, 20), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            string usage = (d.Usage ?? "").Split('\n')[0];
            if (usage.Length > 0)
                using (var uf = new Font("Consolas", 8.5f))
                    TextRenderer.DrawText(g, usage, uf, new Rectangle(x, b.Y + 27, nameW, 17), Theme.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string summary = d.Summary.Length > 0 ? d.Summary : "(açıklama yok)";
            TextRenderer.DrawText(g, summary, Theme.Ui, new Rectangle(x + nameW + 16, b.Y, b.Right - x - nameW - 16 - 34, b.Height),
                d.Summary.Length > 0 ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (hover) IconRenderer.Draw(g, IconKind.ChevronRight, new RectangleF(b.Right - 26, b.Y + b.Height / 2f - 6, 12, 12), Theme.TextMuted);
        }

        /// <summary>Modül sayfasındaki komuta tıklanınca: ağaçta seçip komut sayfasını açar.</summary>
        private void OpenCommand(CommandDoc d)
        {
            if (!ConfirmLeave()) return;
            Expand(d.Module ?? "");
            if (_search.Text.Trim().Length > 0 && !_library.Filter(_search.Text).Contains(d)) _search.Text = "";
            RebuildList(d, null);
            ShowDoc(d);
            _list.Focus();
        }

        /// <summary>Komut sayfasındaki modül çipine tıklanınca modül sayfasına gider.</summary>
        private void GoToModule(string key)
        {
            if (!ConfirmLeave()) return;
            if (key.Length > 0 && _library.FindModule(key) == null) key = "";
            if (_search.Text.Trim().Length > 0) _search.Text = "";
            RebuildList(null, key);
            ShowModule(key);
        }

        // =====================================================================
        // Komut işlemleri
        // =====================================================================

        private void StartNew(string moduleKey)
        {
            if (!ConfirmLeave()) return;
            if (!string.IsNullOrEmpty(moduleKey) && _library.FindModule(moduleKey) == null) moduleKey = "";
            _newFromModuleKey = _current == null ? _currentModuleKey : null;
            _current = null;
            _currentModuleKey = null;
            _isNew = true;
            _editing = true;
            SelectQuietly(-1);
            _name.Text = _description.Text = _usage.Text = _output.Text = "";
            _error.Text = "";
            FillModuleCombo(moduleKey);
            UpdateEmpty();
            ApplyMode();
            _name.Focus();
        }

        private void StartEdit()
        {
            if (_current == null) return;
            _editing = true;
            _isNew = false;
            FillModuleCombo(_current.Module);
            ApplyMode();
            _description.Focus();
            _description.SelectionStart = _description.TextLength;
        }

        private bool SaveEdit()
        {
            if (!_editing) return true;
            string error;
            bool wasNew = _isNew;
            CommandDoc saved = _library.Upsert(_isNew ? null : _current, Draft(), out error);
            if (saved == null)
            {
                _error.Text = "⚠  " + error;
                if (error.StartsWith("Komut adı") || error.Contains("adında bir komut")) { _name.Focus(); _name.SelectAll(); }
                return false;
            }
            _editing = false;
            _isNew = false;
            Expand(saved.Module);
            if (_search.Text.Trim().Length > 0 && !_library.Filter(_search.Text).Contains(saved)) _search.Text = "";   // kayıt görünür kalsın
            RebuildList(saved, null);
            ShowDoc(saved);
            string where = string.IsNullOrEmpty(saved.Module) ? "" : "  →  " + saved.Module;
            ShowBanner("✓  \"" + saved.Name + "\" " + (wasNew ? "eklendi" : "kaydedildi") + where + ".", false);
            _list.Focus();
            return true;
        }

        private void CancelEdit()
        {
            if (!_editing) return;
            bool wasNew = _isNew;
            _editing = false;
            _isNew = false;
            if (!wasNew) { ShowDoc(_current); return; }

            _current = null;
            string back = _newFromModuleKey;
            if (back != null && (back.Length == 0 || _library.FindModule(back) != null))
            {
                RebuildList(null, back);
                ShowModule(back);
                return;
            }
            int i = FirstCommandIndex();
            if (i >= 0)
            {
                SelectQuietly(i);
                LibRow r = _rows[i];
                if (r.Kind == LibRowKind.Command) ShowDoc(r.Doc); else ShowModule(r.ModuleKey);
            }
            else ShowDoc(null);
        }

        private void DeleteCurrent()
        {
            if (_current == null || _editing) return;
            string where = string.IsNullOrEmpty(_current.Module) ? "" : " (\"" + _current.Module + "\" modülü)";
            int c = ChoiceDialog.Ask(FindForm(), "Komutu sil", "\"" + _current.Name + "\"" + where + " kütüphaneden silinsin mi?",
                "Komutun açıklaması, yazılışı ve örnek çıktısı silinir. (Bir önceki sürüm Komutlar.xml.yedek dosyasında kalır.)",
                new[] { "İptal", "Sil" }, 0, 1);
            if (c != 1) return;
            int index = _list.SelectedIndex;
            string name = _current.Name;
            string module = _current.Module ?? "";
            string error;
            if (!_library.Delete(_current, out error)) { ShowBanner("⚠  " + error, true); return; }
            _current = null;
            RebuildList(null, null);
            // Aynı modüldeki bir sonraki komut; yoksa modülün sayfası
            int next = Math.Min(Math.Max(0, index), _rows.Count - 1);
            if (next >= 0 && _rows[next].Kind == LibRowKind.Command && CommandLibrary.SameName(_rows[next].ModuleKey, module))
            {
                SelectQuietly(next);
                ShowDoc(_rows[next].Doc);
            }
            else if (module.Length > 0 || _library.CommandsIn("").Count > 0)
            {
                RebuildList(null, module);
                ShowModule(module);
            }
            else ShowDoc(null);
            ShowBanner("\"" + name + "\" silindi.", false);
        }

        private void CopyUsage()
        {
            if (_current == null || string.IsNullOrEmpty(_current.Usage)) return;
            try
            {
                Clipboard.SetText(CommandLibrary.ToTextBox(_current.Usage));
                _copyUsageBtn.Icon = IconKind.Check;
                _copyUsageBtn.Label = "Kopyalandı";
                _copiedTimer.Stop();
                _copiedTimer.Start();
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("pano", ex);
            }
        }

        /// <summary>Komutu başka modüle taşır (key "": modülsüz). Sürükle-bırak ve sağ tık menüsü kullanır.</summary>
        private void MoveTo(CommandDoc doc, string key)
        {
            if (doc == null || CommandLibrary.SameName(doc.Module ?? "", key ?? "")) return;
            if (!ConfirmLeave()) return;
            string error;
            List<CommandDoc> moved = _library.MoveCommands(new[] { doc }, key, out error);
            if (moved == null) { ShowBanner("⚠  " + error, true); return; }
            CommandDoc n = moved.FirstOrDefault() ?? doc;
            Expand(key);
            if (_search.Text.Trim().Length > 0 && !_library.Filter(_search.Text).Contains(n)) _search.Text = "";
            RebuildList(n, null);
            ShowDoc(n);
            ShowBanner("✓  \"" + n.Name + "\"  →  " + (string.IsNullOrEmpty(key) ? CommandLibrary.NoModuleName : n.Module) + " taşındı.", false);
        }

        // =====================================================================
        // Modül işlemleri
        // =====================================================================

        /// <param name="thenMove">Modül oluşturulunca bu komut yeni modüle taşınır (sağ tık → Modüle taşı → Yeni modül).</param>
        private void NewModule(CommandDoc thenMove = null)
        {
            if (!ConfirmLeave()) return;
            string name, description;
            if (!ModuleDialog.Ask(FindForm(), _library, null, out name, out description)) return;
            string error;
            CommandModule m = _library.UpsertModule(null, name, description, out error);
            if (m == null) { ShowBanner("⚠  " + error, true); return; }
            if (thenMove != null)
            {
                CommandDoc doc = _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, thenMove.Name));
                if (doc != null) { MoveTo(doc, m.Name); return; }
            }
            Expand(m.Name);
            if (_search.Text.Trim().Length > 0) _search.Text = "";
            RebuildList(null, m.Name);
            ShowModule(m.Name);
            ShowBanner("✓  \"" + m.Name + "\" modülü oluşturuldu. \"Komut ekle\" ile komut ekleyin ya da komutları soldan sürükleyip bu modülün üzerine bırakın.", false);
        }

        private void EditModule(string key)
        {
            CommandModule m = _library.FindModule(key);
            if (m == null) return;
            if (!ConfirmLeave()) return;
            string name, description;
            if (!ModuleDialog.Ask(FindForm(), _library, m, out name, out description)) return;
            bool wasCollapsed = _collapsed.Contains(Key(m.Name));
            string currentName = _current != null ? _current.Name : null;
            bool onModulePage = _current == null && _currentModuleKey != null && CommandLibrary.SameName(_currentModuleKey, m.Name);
            string error;
            CommandModule saved = _library.UpsertModule(m, name, description, out error);
            if (saved == null) { ShowBanner("⚠  " + error, true); return; }
            _collapsed.Remove(Key(m.Name));
            if (wasCollapsed) _collapsed.Add(Key(saved.Name));

            CommandDoc cur = currentName != null ? _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, currentName)) : null;
            if (cur != null) { RebuildList(cur, null); ShowDoc(cur); }
            else
            {
                string page = onModulePage ? saved.Name : _currentModuleKey;
                RebuildList(null, page);
                if (page != null) ShowModule(page);
            }
            ShowBanner("✓  \"" + saved.Name + "\" modülü kaydedildi.", false);
        }

        private void DeleteModule(string key)
        {
            CommandModule m = _library.FindModule(key);
            if (m == null || _editing) return;
            int count = _library.CommandsIn(m.Name).Count;
            bool deleteCommands;
            if (count == 0)
            {
                int c = ChoiceDialog.Ask(FindForm(), "Modülü sil", "\"" + m.Name + "\" modülü silinsin mi?",
                    "Modül boş; hiçbir komut silinmez.", new[] { "İptal", "Sil" }, 0, 1);
                if (c != 1) return;
                deleteCommands = false;
            }
            else
            {
                int c = ChoiceDialog.Ask(FindForm(), "Modülü sil", "\"" + m.Name + "\" modülü silinsin mi? (" + count + " komut)",
                    "\"Modülü sil, komutları koru\": komutlar \"" + CommandLibrary.NoModuleName + "\" grubuna taşınır.\n" +
                    "\"Komutlarla birlikte sil\": modüldeki " + count + " komut da kütüphaneden silinir.",
                    new[] { "İptal", "Modülü sil, komutları koru", "Komutlarla birlikte sil" }, 1, 2);
                if (c != 1 && c != 2) return;
                deleteCommands = c == 2;
            }
            string currentName = _current != null ? _current.Name : null;
            string error;
            if (!_library.DeleteModule(m, deleteCommands, out error)) { ShowBanner("⚠  " + error, true); return; }
            _collapsed.Remove(Key(m.Name));

            CommandDoc cur = currentName != null ? _library.Items.FirstOrDefault(d => CommandLibrary.SameName(d.Name, currentName)) : null;
            if (cur != null) { RebuildList(cur, null); ShowDoc(cur); }
            else if (!deleteCommands && count > 0) { RebuildList(null, ""); ShowModule(""); }
            else
            {
                _current = null;
                _currentModuleKey = null;
                RebuildList(null, null);
                int i = FirstCommandIndex();
                if (i >= 0)
                {
                    SelectQuietly(i);
                    if (_rows[i].Kind == LibRowKind.Command) ShowDoc(_rows[i].Doc); else ShowModule(_rows[i].ModuleKey);
                }
                else ShowDoc(null);
            }
            ShowBanner("\"" + m.Name + "\" modülü silindi" + (count == 0 ? "." : deleteCommands ? " (" + count + " komutla birlikte)." : "; komutları " + CommandLibrary.NoModuleName + " grubuna taşındı."), false);
        }

        // =====================================================================
        // Ayrılma onayı
        // =====================================================================

        /// <summary>
        /// Kaydedilmemiş değişiklik varsa sorar. true: devam edilebilir (kaydedildi ya da vazgeçildi), false: kullanıcı iptal etti.
        /// Sekmeden ya da uygulamadan çıkarken de çağrılır.
        /// </summary>
        public bool ConfirmLeave()
        {
            if (!_editing || !IsDirty()) { if (_editing) CancelEditSilently(); return true; }
            string what = _isNew ? "Yeni komut" : "\"" + _current.Name + "\"";
            int c = ChoiceDialog.Ask(FindForm(), "Kaydedilmemiş değişiklik", what + " üzerindeki değişiklikler kaydedilsin mi?",
                "Kaydetmezseniz yaptığınız değişiklikler kaybolur.", new[] { "İptal", "Kaydetme", "Kaydet" }, 2, 1);
            if (c == 2) return SaveEdit();
            if (c == 1) { CancelEditSilently(); return true; }
            return false;
        }

        /// <summary>Düzenlemeyi bırakır ve alanları kayıtlı hâline döndürür (liste seçimine dokunmaz).</summary>
        private void CancelEditSilently()
        {
            bool wasNew = _isNew;
            _editing = false;
            _isNew = false;
            if (wasNew)
            {
                _current = null;
                ApplyMode();
                UpdateEmpty();
            }
            else ShowDoc(_current);
        }
    }

    /// <summary>Modül oluşturma / düzenleme penceresi: ad + isteğe bağlı açıklama.</summary>
    public sealed class ModuleDialog : DarkDialog
    {
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _description = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        private readonly Label _error = Theme.MakeLabel("", Theme.UiSmall, Theme.Danger);
        private readonly CommandLibrary _library;
        private readonly CommandModule _original;

        public ModuleDialog(CommandLibrary library, CommandModule original)
            : base(original == null ? "Yeni modül" : "Modülü düzenle", new Size(500, 318))
        {
            _library = library;
            _original = original;

            var intro = Theme.MakeLabel(original == null
                    ? "Modül, birbiriyle ilgili komutları bir arada tutar (ör. \"Task komutları\", \"Bellek ve ağ\")."
                    : "Modülün adı değişirse içindeki komutlar yeni adla birlikte taşınır.",
                Theme.Ui, Theme.TextMuted);
            intro.Location = new Point(20, 16);
            intro.MaximumSize = new Size(460, 0);

            var nameCap = Caption("MODÜL ADI", 20, 52);
            Theme.StyleTextBox(_name);
            _name.Font = new Font("Segoe UI", 11f);
            _name.SetBounds(20, 72, 460, 30);
            _name.Text = original != null ? original.Name : "";
            _name.MaxLength = CommandLibrary.MaxModuleNameLength;

            var descCap = Caption("AÇIKLAMA  (isteğe bağlı)", 20, 114);
            Theme.StyleTextBox(_description);
            _description.SetBounds(20, 134, 460, 86);
            _description.Text = original != null ? CommandLibrary.ToTextBox(original.Description) : "";
            Theme.ApplyDarkScrollBars(_description);

            _error.Location = new Point(20, 228);
            _error.MaximumSize = new Size(460, 0);

            var ok = new IconButton(IconKind.Check, IconTone.Primary) { Label = original == null ? "Oluştur" : "Kaydet" };
            var cancel = new IconButton(IconKind.None, IconTone.Secondary) { Label = "İptal" };
            ok.SetBounds(372, 268, 108, 34);
            cancel.SetBounds(272, 268, 92, 34);
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            ok.Click += (s, e) => Accept();
            _name.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
            _name.TextChanged += (s, e) => { _error.Text = ""; _name.BackColor = Theme.Input; };
            Controls.AddRange(new Control[] { intro, nameCap, _name, descCap, _description, _error, ok, cancel });
            Shown += (s, e) => { _name.Focus(); _name.SelectAll(); };
        }

        private void Accept()
        {
            string err = _library.ValidateModule(_name.Text, _original);
            if (err != null) { _error.Text = "⚠  " + err; _name.BackColor = Theme.DangerSurface; _name.Focus(); return; }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter) || keyData == (Keys.Control | Keys.S)) { Accept(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public static bool Ask(IWin32Window owner, CommandLibrary library, CommandModule original, out string name, out string description)
        {
            using (var d = new ModuleDialog(library, original))
            {
                bool ok = d.ShowDialog(owner) == DialogResult.OK;
                name = ok ? d._name.Text.Trim() : null;
                description = ok ? d._description.Text : null;
                return ok;
            }
        }
    }
}
