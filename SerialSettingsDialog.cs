using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    /// <summary>
    /// Seri port ayarları. TeraTerm'deki "Serial port setup" gibi: üstte port seçilir (hangi portun ayarı
    /// değiştirilecek), altında o portun adı, baud rate, data, parity, stop, akış denetimi ve gönderme gecikmeleri.
    /// Ayarlar kartın seri ayarlarıyla aynı olmalı; her portun ayarı ayrı saklanır.
    /// </summary>
    public sealed class SerialSettingsDialog : DarkDialog
    {
        private readonly DarkComboBox _port = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _name = new TextBox();
        private readonly DarkComboBox _baud = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DarkComboBox _data = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DarkComboBox _parity = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DarkComboBox _stop = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DarkComboBox _flow = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown _charDelay = new NumericUpDown { Minimum = 0, Maximum = 1000 };
        private readonly NumericUpDown _lineDelay = new NumericUpDown { Minimum = 0, Maximum = 5000 };
        private readonly Label _error = Theme.MakeLabel("", Theme.UiSmall, Theme.Danger);
        private readonly ToolTip _tips = new DarkToolTip();

        private readonly Dictionary<string, SerialPortProfile> _work = new Dictionary<string, SerialPortProfile>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, SerialPortProfile> _profileOf;
        private readonly HashSet<string> _locked;
        private string _current;

        private sealed class PortItem
        {
            public SerialPortInfo Info;
            public override string ToString() { return Info.Description == Info.PortName ? Info.PortName : Info.Description; }
        }

        /// <param name="ports">Bilgisayarda görünen portlar.</param>
        /// <param name="profileOf">Portun kayıtlı ayarı (yoksa varsayılanlarla yeni).</param>
        /// <param name="locked">Açık (bağlı) portlar: ayarı açıkken değiştirilemez.</param>
        private SerialSettingsDialog(IList<SerialPortInfo> ports, string initial, Func<string, SerialPortProfile> profileOf, HashSet<string> locked)
            : base("Seri port ayarları", new Size(470, 470))
        {
            _profileOf = profileOf;
            _locked = locked ?? new HashSet<string>();

            int y = 18;
            Func<string, string, Control, int> row = (caption, tip, control) =>
            {
                var l = Theme.MakeLabel(caption, Theme.Ui, Theme.TextMuted);
                l.Location = new Point(20, y + 5);
                control.Location = new Point(170, y);
                control.Width = 270;
                Controls.Add(l);
                Controls.Add(control);
                if (tip != null) { _tips.SetToolTip(l, tip); _tips.SetToolTip(control, tip); }
                y += 40;
                return y;
            };

            foreach (SerialPortInfo p in ports) _port.Items.Add(new PortItem { Info = p });
            if (initial != null && !ports.Any(p => string.Equals(p.PortName, initial, StringComparison.OrdinalIgnoreCase)))
                _port.Items.Insert(0, new PortItem { Info = new SerialPortInfo { PortName = initial, Description = initial + " (şu an görünmüyor)" } });
            row("Port", "Hangi portun ayarı değiştirilecek. Bilgisayarda görünen portlar listelenir.", _port);
            Theme.StyleTextBox(_name);
            Theme.SetCueBanner(_name, "ör. Kart 3");
            row("Ad", "Bu porta takılı kartın adı (isteğe bağlı). Listede ve pencere başlığında görünür.", _name);

            foreach (int b in SerialPortProfile.BaudRates) _baud.Items.Add(b.ToString());
            row("Baud rate", "Hız (saniyede bit). Kartın hızıyla aynı olmalı; yanlışsa ekrana anlamsız karakterler gelir.", _baud);
            _data.Items.AddRange(new object[] { "8 bit", "7 bit" });
            row("Data", "Her karakterin bit sayısı. Neredeyse her cihazda 8.", _data);
            _parity.Items.AddRange(new object[] { "none", "odd", "even" });
            row("Parity", "Hata denetim biti. none: yok (kartlarda genelde böyle); odd / even: tek / çift denetim.", _parity);
            _stop.Items.AddRange(new object[] { "1 bit", "2 bit" });
            row("Stop", "Her karakterin sonundaki \"bitti\" işareti. Standart: 1.", _stop);
            _flow.Items.AddRange(new object[] { "none", "Xon/Xoff", "hardware" });
            row("Flow control", "Akış denetimi: karşı taraf yetişemezse \"dur\" diyebilmesi.\nnone: yok (kartlarda genelde böyle); Xon/Xoff: özel karakterlerle;\nhardware: kablodaki RTS/CTS telleriyle.", _flow);

            Theme.StyleNumeric(_charDelay);
            Theme.StyleNumeric(_lineDelay);
            var delayCap = Theme.MakeLabel("Transmit delay", Theme.Ui, Theme.TextMuted);
            delayCap.Location = new Point(20, y + 5);
            _charDelay.SetBounds(170, y, 70, 24);
            var c1 = Theme.MakeLabel("msec/char", Theme.UiSmall, Theme.TextMuted);
            c1.Location = new Point(246, y + 6);
            _lineDelay.SetBounds(316, y, 70, 24);
            var c2 = Theme.MakeLabel("msec/line", Theme.UiSmall, Theme.TextMuted);
            c2.Location = new Point(392, y + 6);
            Controls.AddRange(new Control[] { delayCap, _charDelay, c1, _lineDelay, c2 });
            string delayTip = "Gönderirken bekleme. Kart hızlı gelen yazıyı kaçırıyorsa (ör. bootrom'a uzun komut yapıştırınca harf\n" +
                              "eksik gidiyorsa) 1–5 msec/char verin. Sorun yoksa 0.";
            foreach (Control c in new Control[] { delayCap, _charDelay, c1, _lineDelay, c2 }) _tips.SetToolTip(c, delayTip);
            y += 40;

            _error.AutoSize = false;
            _error.SetBounds(20, y, 430, 34);
            Controls.Add(_error);

            var reset = Theme.MakeButton("Varsayılanlar", Theme.ButtonKind.Secondary);
            var cancel = Theme.MakeButton("Vazgeç", Theme.ButtonKind.Secondary);
            var ok = Theme.MakeButton("Kaydet", Theme.ButtonKind.Primary);
            int by = ClientSize.Height - 46;
            reset.SetBounds(20, by, 120, 32);
            cancel.SetBounds(ClientSize.Width - 20 - 100 - 8 - 100, by, 100, 32);
            ok.SetBounds(ClientSize.Width - 20 - 100, by, 100, 32);
            Controls.AddRange(new Control[] { reset, cancel, ok });
            _tips.SetToolTip(reset, "115200 · 8 · none · 1 · none · 0 / 0");
            AcceptButton = ok;
            CancelButton = cancel;
            cancel.DialogResult = DialogResult.Cancel;
            ok.Click += (s, e) => Accept();
            reset.Click += (s, e) =>
            {
                var d = new SerialPortProfile { PortName = _current, Name = _name.Text };
                ShowProfile(d);
            };
            _port.SelectedIndexChanged += (s, e) => SwitchPort();

            int index = 0;
            for (int i = 0; i < _port.Items.Count; i++)
                if (string.Equals(((PortItem)_port.Items[i]).Info.PortName, initial, StringComparison.OrdinalIgnoreCase)) index = i;
            if (_port.Items.Count > 0) _port.SelectedIndex = index;
            else _error.Text = "Bilgisayarda seri port görünmüyor. USB-seri çeviriciyi takıp pencereyi yeniden açın.";
        }

        /// <summary>Değiştirilen ayarlar (Kaydet'e basılınca; vazgeçilirse null).</summary>
        public List<SerialPortProfile> Result { get; private set; }

        public static List<SerialPortProfile> Edit(IWin32Window owner, IList<SerialPortInfo> ports, string initial,
                                                   Func<string, SerialPortProfile> profileOf, HashSet<string> locked)
        {
            using (var d = new SerialSettingsDialog(ports, initial, profileOf, locked))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Result : null;
        }

        private void SwitchPort()
        {
            if (_current != null) _work[_current] = Read();
            var it = _port.SelectedItem as PortItem;
            if (it == null) return;
            _current = it.Info.PortName;
            SerialPortProfile p;
            if (!_work.TryGetValue(_current, out p)) p = (_profileOf(_current) ?? new SerialPortProfile { PortName = _current }).Clone();
            ShowProfile(p);
            bool locked = _locked.Contains(_current);
            foreach (Control c in new Control[] { _baud, _data, _parity, _stop, _flow, _charDelay, _lineDelay }) c.Enabled = !locked;
            _error.ForeColor = locked ? Theme.Warning : Theme.Danger;
            _error.Text = locked ? _current + " şu an açık: ayarlarını değiştirmek için önce portu kapatın (ad değiştirilebilir)." : "";
        }

        private void ShowProfile(SerialPortProfile p)
        {
            _name.Text = p.Name ?? "";
            int bi = Array.IndexOf(SerialPortProfile.BaudRates, p.BaudRate);
            _baud.SelectedIndex = bi >= 0 ? bi : Array.IndexOf(SerialPortProfile.BaudRates, SerialPortProfile.DefaultBaud);
            _data.SelectedIndex = p.DataBits == 7 ? 1 : 0;
            _parity.SelectedIndex = (int)p.Parity;
            _stop.SelectedIndex = p.StopBits == 2 ? 1 : 0;
            _flow.SelectedIndex = (int)p.Flow;
            _charDelay.Value = Math.Max(0, Math.Min(1000, p.CharDelayMs));
            _lineDelay.Value = Math.Max(0, Math.Min(5000, p.LineDelayMs));
        }

        private SerialPortProfile Read()
        {
            return new SerialPortProfile
            {
                PortName = _current,
                Name = string.IsNullOrWhiteSpace(_name.Text) ? null : _name.Text.Trim(),
                BaudRate = SerialPortProfile.BaudRates[Math.Max(0, _baud.SelectedIndex)],
                DataBits = _data.SelectedIndex == 1 ? 7 : 8,
                Parity = (SerialParity)Math.Max(0, _parity.SelectedIndex),
                StopBits = _stop.SelectedIndex == 1 ? 2 : 1,
                Flow = (SerialFlow)Math.Max(0, _flow.SelectedIndex),
                CharDelayMs = (int)_charDelay.Value,
                LineDelayMs = (int)_lineDelay.Value,
            };
        }

        private void Accept()
        {
            if (_current == null) { DialogResult = DialogResult.Cancel; Close(); return; }
            _work[_current] = Read();
            foreach (SerialPortProfile p in _work.Values)
            {
                string err = p.Validate();
                if (err != null) { _error.ForeColor = Theme.Danger; _error.Text = p.PortName + ": " + err; return; }
            }
            Result = _work.Values.ToList();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
