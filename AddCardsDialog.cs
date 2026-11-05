using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// "Kart Ekle" penceresi: grup adı, port, başlangıç IP'si ve kart sayısı.
    /// Kart sayısı kadar IP alanı dinamik oluşturulur; başlangıç IP'sinden sıralı doldurulur, elle düzenlenebilir.
    /// Var olan bir gruba kart eklemek için de kullanılır (grup adı ve port kilitli gelir).
    /// </summary>
    public sealed class AddCardsDialog : Form
    {
        private const int MaxCards = 128;

        private readonly TextBox _groupName = new TextBox();
        private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 23 };
        private readonly TextBox _startIp = new TextBox { Text = "192.168.1.10" };
        private readonly NumericUpDown _count = new NumericUpDown { Minimum = 1, Maximum = MaxCards, Value = 1 };
        private readonly Button _fillBtn = Theme.MakeButton("Sıralı doldur", Theme.ButtonKind.Secondary);
        private readonly FlowLayoutPanel _ipList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Input,
            Padding = new Padding(8, 6, 8, 6),
        };
        private readonly Label _ipHeader = Theme.MakeLabel("", Theme.SectionHeader, Theme.TextMuted);
        private readonly Label _error = Theme.MakeLabel("", Theme.Ui, Theme.Danger);
        private readonly Button _okBtn = Theme.MakeButton("Grubu Oluştur", Theme.ButtonKind.Primary);
        private readonly Button _cancelBtn = Theme.MakeButton("İptal", Theme.ButtonKind.Secondary);

        private readonly List<TextBox> _ipBoxes = new List<TextBox>();
        private readonly ICollection<string> _existingGroupNames;
        private readonly CardGroup _targetGroup;

        /// <param name="existingGroupNames">Ad çakışmasını önlemek için mevcut grup adları.</param>
        /// <param name="targetGroup">Doluysa kartlar bu gruba eklenir (ad ve port değiştirilemez).</param>
        public AddCardsDialog(ICollection<string> existingGroupNames, CardGroup targetGroup)
        {
            _existingGroupNames = existingGroupNames;
            _targetGroup = targetGroup;

            Text = targetGroup == null ? "Kart Ekle" : "Gruba Kart Ekle";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            ClientSize = new Size(520, 600);
            AcceptButton = _okBtn;
            CancelButton = _cancelBtn;

            BuildLayout();

            if (targetGroup != null)
            {
                _groupName.Text = targetGroup.Name;
                _groupName.ReadOnly = true;
                _port.Value = targetGroup.Port;
                _port.Enabled = false;
                _okBtn.Text = "Kartları Ekle";
                string last = targetGroup.Cards.Count > 0 ? targetGroup.Cards[targetGroup.Cards.Count - 1].Ip : null;
                string next = last != null ? IpUtil.Increment(last, 1) : null;
                if (next != null) _startIp.Text = next;
            }

            _count.ValueChanged += (s, e) => SyncIpRows(false);
            _fillBtn.Click += (s, e) => SyncIpRows(true);
            _startIp.TextChanged += (s, e) => { MarkValid(_startIp, true); };
            _okBtn.Click += OkBtn_Click;

            SyncIpRows(true);
        }

        /// <summary>Onaylandıysa oluşturulan / güncellenen grup bilgisi.</summary>
        public string ResultGroupName { get; private set; }
        public int ResultPort { get; private set; }
        public List<string> ResultIps { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_targetGroup == null) _groupName.Focus();
            else _startIp.Focus();
        }

        // ---------- Yerleşim ----------

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 16),
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme.Surface,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));     // başlık
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // form alanları
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));     // IP başlığı
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // IP listesi
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // hata

            // Başlık
            var head = new Panel { Dock = DockStyle.Fill, Height = 52, BackColor = Theme.Surface, Margin = new Padding(0) };
            var title = Theme.MakeLabel(_targetGroup == null ? "Yeni kart grubu" : "\"" + _targetGroup.Name + "\" grubuna kart ekle",
                                        new Font("Segoe UI Semibold", 13f), Theme.Text);
            title.Location = new Point(0, 0);
            var desc = Theme.MakeLabel("Aynı port üzerinden haberleşen kartları bir grupta toplayın.", Theme.Ui, Theme.TextMuted);
            desc.Location = new Point(1, 28);
            head.Controls.Add(title);
            head.Controls.Add(desc);
            root.Controls.Add(head, 0, 0);

            // Form alanları (2 sütun)
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, AutoSize = true, Margin = new Padding(0, 8, 0, 4), BackColor = Theme.Surface };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Theme.StyleTextBox(_groupName);
            Theme.StyleNumeric(_port);
            Theme.StyleTextBox(_startIp);
            Theme.StyleNumeric(_count);
            foreach (Control c in new Control[] { _groupName, _port, _startIp, _count })
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 2, 10, 10);
                c.Font = new Font("Segoe UI", 10f);
            }

            grid.Controls.Add(FieldLabel("GRUP ADI"), 0, 0);
            grid.Controls.Add(FieldLabel("PORT"), 1, 0);
            grid.Controls.Add(_groupName, 0, 1);
            grid.Controls.Add(_port, 1, 1);
            grid.Controls.Add(FieldLabel("BAŞLANGIÇ IP ADRESİ"), 0, 2);
            grid.Controls.Add(FieldLabel("KART SAYISI"), 1, 2);
            grid.Controls.Add(_startIp, 0, 3);
            grid.Controls.Add(_count, 1, 3);
            root.Controls.Add(grid, 0, 1);

            // IP listesi başlığı + "Sıralı doldur"
            var ipHead = new Panel { Dock = DockStyle.Fill, Height = 34, BackColor = Theme.Surface, Margin = new Padding(0, 4, 0, 4) };
            _ipHeader.Location = new Point(0, 10);
            _fillBtn.Width = 110;
            _fillBtn.Height = 28;
            _fillBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ipHead.Controls.Add(_ipHeader);
            ipHead.Controls.Add(_fillBtn);
            ipHead.Resize += (s, e) => _fillBtn.Location = new Point(ipHead.Width - _fillBtn.Width - 10, 2);
            new DarkToolTip().SetToolTip(_fillBtn, "Tüm IP alanlarını başlangıç IP'sinden itibaren sırayla doldur");
            root.Controls.Add(ipHead, 0, 2);

            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Theme.Border, Margin = new Padding(0, 0, 10, 0) };
            listHost.Controls.Add(_ipList);
            Theme.ApplyDarkScrollBars(_ipList);
            root.Controls.Add(listHost, 0, 3);

            // Hata satırı
            _error.Margin = new Padding(0, 8, 0, 0);
            _error.MaximumSize = new Size(470, 0);
            root.Controls.Add(_error, 0, 4);

            // Düğmeler
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 20, 0), BackColor = Theme.Sidebar };
            _okBtn.Width = 130;
            _cancelBtn.Width = 90;
            _okBtn.Height = _cancelBtn.Height = 34;
            _cancelBtn.Margin = new Padding(0, 0, 8, 0);
            buttons.Controls.Add(_okBtn);
            buttons.Controls.Add(_cancelBtn);
            // Alt bant: işlemler içerikten ayrı (diğer pencerelerle aynı görünüm)
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Theme.Sidebar };
            footer.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            footer.Controls.Add(buttons);

            Controls.Add(root);
            Controls.Add(footer);
        }

        private static Label FieldLabel(string text)
        {
            var l = Theme.MakeLabel(text, Theme.SectionHeader, Theme.TextMuted);
            l.Margin = new Padding(0, 4, 0, 2);
            return l;
        }

        // ---------- Dinamik IP alanları ----------

        /// <summary>IP satırı sayısını kart sayısına eşitler. overwrite=true ise tümünü başlangıç IP'sinden doldurur.</summary>
        private void SyncIpRows(bool overwrite)
        {
            int target = (int)_count.Value;
            string start = _startIp.Text.Trim();
            bool startValid = IpUtil.IsValidIPv4(start);
            if (overwrite && !startValid)
            {
                MarkValid(_startIp, false);
                ShowError("Sıralı doldurmak için geçerli bir başlangıç IP adresi girin (ör. 192.168.1.10).");
                return;
            }

            _ipList.SuspendLayout();
            while (_ipBoxes.Count > target)
            {
                TextBox box = _ipBoxes[_ipBoxes.Count - 1];
                _ipBoxes.RemoveAt(_ipBoxes.Count - 1);
                _ipList.Controls.Remove(box.Parent);
                box.Parent.Dispose();
            }
            while (_ipBoxes.Count < target)
            {
                int index = _ipBoxes.Count;
                _ipBoxes.Add(CreateIpRow(index));
            }
            for (int i = 0; i < _ipBoxes.Count; i++)
            {
                TextBox box = _ipBoxes[i];
                if (startValid && (overwrite || box.Text.Length == 0))
                    box.Text = IpUtil.Increment(start, i) ?? "";
            }
            _ipList.ResumeLayout(true);

            _ipHeader.Text = "KART IP ADRESLERİ  (" + target + ")";
            if (overwrite) ShowError("");
        }

        private TextBox CreateIpRow(int index)
        {
            var row = new Panel { Width = 440, Height = 34, BackColor = Theme.Input, Margin = new Padding(0) };
            var label = Theme.MakeLabel("Kart " + (index + 1), Theme.Ui, Theme.TextMuted);
            label.Location = new Point(2, 8);
            var box = new TextBox { Location = new Point(80, 4), Width = 230 };
            Theme.StyleTextBox(box);
            box.Font = new Font("Consolas", 10f);
            box.BackColor = Theme.Surface;
            box.TextChanged += (s, e) => MarkValid(box, true);
            row.Controls.Add(label);
            row.Controls.Add(box);
            _ipList.Controls.Add(row);
            return box;
        }

        // ---------- Doğrulama ----------

        private void OkBtn_Click(object sender, EventArgs e)
        {
            string error = ValidateInput(out List<string> ips);
            if (error != null)
            {
                ShowError(error);
                DialogResult = DialogResult.None;   // pencere kapanmasın
                return;
            }

            ResultGroupName = _groupName.Text.Trim();
            ResultPort = (int)_port.Value;
            ResultIps = ips;
            DialogResult = DialogResult.OK;
            Close();
        }

        private string ValidateInput(out List<string> ips)
        {
            ips = new List<string>();
            var problems = new List<string>();

            string name = _groupName.Text.Trim();
            if (_targetGroup == null)
            {
                if (name.Length == 0) { problems.Add("Grup adı boş olamaz."); MarkValid(_groupName, false); }
                else if (_existingGroupNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                {
                    problems.Add("\"" + name + "\" adında bir grup zaten var. Var olan gruba eklemek için gruba sağ tıklayıp \"Gruba kart ekle\"yi kullanın.");
                    MarkValid(_groupName, false);
                }
                else MarkValid(_groupName, true);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var already = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_targetGroup != null) foreach (var c in _targetGroup.Cards) already.Add(c.Ip);

            int empty = 0, invalid = 0, dup = 0, exists = 0;
            foreach (TextBox box in _ipBoxes)
            {
                string ip = box.Text.Trim();
                bool ok = true;
                if (ip.Length == 0) { empty++; ok = false; }
                else if (!IpUtil.IsValidIPv4(ip)) { invalid++; ok = false; }
                else if (!seen.Add(ip)) { dup++; ok = false; }
                else if (already.Contains(ip)) { exists++; ok = false; }
                MarkValid(box, ok);
                if (ok) ips.Add(ip);
            }

            if (empty > 0) problems.Add(empty + " IP alanı boş.");
            if (invalid > 0) problems.Add(invalid + " IP adresi geçersiz (ör. 192.168.1.10 biçiminde olmalı).");
            if (dup > 0) problems.Add(dup + " IP adresi listede birden fazla kez geçiyor.");
            if (exists > 0) problems.Add(exists + " IP adresi bu grupta zaten kayıtlı.");

            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        private static void MarkValid(Control c, bool valid)
        {
            c.BackColor = valid ? (c.Parent != null && c.Parent.BackColor == Theme.Input ? Theme.Surface : Theme.Input)
                                : Color.FromArgb(70, 30, 34);
        }

        private void ShowError(string message)
        {
            _error.Text = message;
            _error.Visible = message.Length > 0;
        }
    }
}
