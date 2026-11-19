using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Vision;

namespace VisionTests
{
    /// <summary>Seri sekmesi: ekran işleme, satır yakalama, oturum (sahte port), ayarlar.</summary>
    public sealed class SerialTests
    {
        /// <summary>Bellekte sahte COM portu: yazılanları tutar, istenince "karttan" veri gönderir, açma hatası / kaybolma taklidi.</summary>
        private sealed class FakeSerialIo : ISerialIo
        {
            public readonly List<byte> Written = new List<byte>();
            public readonly List<DateTime> WriteTimes = new List<DateTime>();
            public string OpenError;
            public bool Present = true;
            public SerialPortProfile OpenedWith;
            public bool IsOpen { get; private set; }
            public event Action<byte[]> Received;
            public event Action<string> Lost;

            public void Open(SerialPortProfile p)
            {
                if (OpenError != null) throw new SerialOpenException(OpenError, null);
                OpenedWith = p;
                IsOpen = true;
            }

            public void Write(byte[] data, int offset, int count)
            {
                lock (Written)
                {
                    for (int i = 0; i < count; i++) { Written.Add(data[offset + i]); WriteTimes.Add(DateTime.UtcNow); }
                }
            }

            public void CardSends(string s) { CardSendsBytes(Encoding.UTF8.GetBytes(s)); }
            public void CardSendsBytes(byte[] b) { var h = Received; if (h != null) h(b); }
            public void Unplug(string why) { var h = Lost; if (h != null) h(why); }
            public string Sent { get { lock (Written) return Encoding.UTF8.GetString(Written.ToArray()); } }
            public int SentBytes { get { lock (Written) return Written.Count; } }
            public bool StillPresent() { return Present; }
            public void Close() { IsOpen = false; }
            public void Dispose() { Close(); }
        }

        private static async Task WaitSent(FakeSerialIo io, int length, int ms = 3000)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
            while (io.Sent.Length < length)
            {
                if (DateTime.UtcNow > end) throw new CheckFailedException("Gönderim tamamlanmadı: \"" + io.Sent + "\"");
                await Task.Delay(10);
            }
        }

        private static string Screen(SerialScreen s, string screen, string raw)
        {
            int line = screen.Length - (screen.LastIndexOf('\n') + 1);
            return SerialScreen.Apply(screen, s.Process(raw, line));
        }

        // ---------- Ekran ----------

        [Test]
        public void Seri_EkranSatirSonlariVeBackspace()
        {
            var s = new SerialScreen();
            Check.Equal("-> ls\nabc\n", Screen(s, "", "-> ls\r\nabc\r\n"), "CR LF tek satır sonu");
            Check.Equal("-> ad", Screen(new SerialScreen(), "", "-> abc\b \b\b \bd"), "kart \"\\b \\b\" ile siler");
            // Backspace önceki parçada gelen karakteri de siler, ama satır başını geçmez
            var s2 = new SerialScreen();
            string scr = Screen(s2, "", "satir1\n-> xy");
            scr = Screen(s2, scr, "\b\b\b\b\bz");
            Check.Equal("satir1\nz", scr, "önceki parçadaki karakterler silinir, önceki satıra geçilmez");
        }

        [Test]
        public void Seri_EkranKacisDizileriVeDenetimKarakterleriAtilir()
        {
            var s = new SerialScreen();
            Check.Equal("renk normal\n", Screen(s, "", "\u001b[1;32mrenk\u001b[0m normal\a\r\n"), "ANSI renk ve zil atılır");
            Check.Equal("ab", Screen(new SerialScreen(), "", "a\u001b[K\u001b7b"), "CSI ve tek karakterlik ESC dizisi");
            var parted = new SerialScreen();
            string scr = Screen(parted, "", "x\u001b[");
            scr = Screen(parted, scr, "31my");
            Check.Equal("xy", scr, "parçalara bölünmüş kaçış dizisi");
        }

        [Test]
        public void Seri_SatirBasinaDonusUstuneYazar()
        {
            var s = new SerialScreen();
            Check.Equal("yükleniyor 20%\n", Screen(s, "", "yükleniyor 10%\ryükleniyor 20%\r\n"), "CR sonrası satır baştan yazılır");
            var s2 = new SerialScreen();
            string scr = Screen(s2, "", "abc\r");
            scr = Screen(s2, scr, "\nX");
            Check.Equal("abc\nX", scr, "parçalar arasında CR LF yine satır sonu");
            var s3 = new SerialScreen();
            scr = Screen(s3, "", "-> abc\r");
            scr = Screen(s3, scr, "-> xy");
            Check.Equal("-> xy", scr, "önceki parçada ekrana yazılmış satır da silinir");
            Check.Equal("a\n\nb", Screen(new SerialScreen(), "", "a\n\rb".Replace("b", "\nb")), "LF CR sırası da çalışır");
        }

        // ---------- Ekran görünümü (VT) ----------

        private static string Feed(VtScreen vt, string text)
        {
            var log = new StringBuilder();
            vt.Feed(text, log);
            return log.ToString();
        }

        [Test]
        public void Vt_BiosMenusuEkranaCizilirGunlugeGitmez()
        {
            var vt = new VtScreen();
            string log = Feed(vt, "POST tamam\r\n");
            Check.Equal("POST tamam\r\n", log, "akan yazı günlüğe");
            log = Feed(vt, "\u001b[2J\u001b[1;1HBoot Manager\u001b[5;10H\u001b[7mUEFI: Disk 1\u001b[0m\u001b[6;10HSetup");
            Check.Equal(SerialViewMode.Screen, vt.Mode, "ekran temizlenince Ekran görünümü");
            Check.True(!log.Contains("Boot Manager") && log.Contains("tam ekran"), "günlüğe çizim değil bilgi satırı gider: " + log);
            Check.Equal("Boot Manager", vt.RowText(0), "1. satır");
            Check.Equal("         UEFI: Disk 1", vt.RowText(4), "5. satır 10. sütun");
            Check.True(vt[4, 9].Reverse && !vt[5, 9].Reverse, "seçili satır ters renk");
            Check.Equal("         Setup", vt.RowText(5), "6. satır");
        }

        [Test]
        public void Vt_AkanYaziyaDonunceGunlugeDoner()
        {
            var vt = new VtScreen();
            Feed(vt, "\u001b[2J\u001b[HMenu");
            Check.Equal(SerialViewMode.Screen, vt.Mode, "ekran");
            var sb = new StringBuilder();
            for (int i = 0; i < 30; i++) sb.Append("Loading kernel " + i + "\r\n");
            string log = Feed(vt, sb.ToString());
            Check.Equal(SerialViewMode.Log, vt.Mode, "ekranın bir boyu kadar akan satırdan sonra günlük");
            Check.True(log.Contains("gösterimden çıktı") && log.Contains("Loading kernel 0") && log.Contains("Loading kernel 29"),
                       "akan kısım günlüğe aktarılır: " + log);
        }

        [Test]
        public void Vt_DonunceEkraninSonHaliGunlugeEklenir()
        {
            var vt = new VtScreen();
            Feed(vt, "\u001b[2J\u001b[1;1HBoot Manager\u001b[3;5HUEFI: Disk 1\u001b[25;1H");
            var sb = new StringBuilder();
            for (int i = 0; i < 26; i++) sb.Append("Loading " + i + "\r\n");
            string log = Feed(vt, sb.ToString());
            Check.Equal(SerialViewMode.Log, vt.Mode, "günlüğe dönüldü");
            int menu = log.IndexOf("Boot Manager", StringComparison.Ordinal), disk = log.IndexOf("    UEFI: Disk 1", StringComparison.Ordinal);
            int first = log.IndexOf("Loading 0", StringComparison.Ordinal);
            Check.True(menu >= 0 && disk > menu && first > disk, "önce ekranın son hâli, sonra akan satırlar: " + log);
            Check.Equal(1, CountOf(log, "Loading 0"), "akan satır iki kez yazılmaz");
            Check.Equal(1, CountOf(log, "Loading 25"), "son satır da bir kez");

            // Menüden çıkarken BIOS ekranı temizlerse: temizlenmeden önceki ekran aktarılır
            var vt2 = new VtScreen();
            Feed(vt2, "\u001b[2J\u001b[1;1HSetup Utility\u001b[2;1HExit\u001b[3;1H");
            Feed(vt2, "\u001b[2J");
            string log2 = Feed(vt2, sb.ToString());
            Check.True(log2.Contains("Setup Utility") && log2.Contains("Loading 25"), "temizlenen menü de günlüğe: " + log2);
        }

        private static int CountOf(string s, string part)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(part, i, StringComparison.Ordinal)) >= 0) { n++; i += part.Length; }
            return n;
        }

        [Test]
        public void Vt_BoyutDegisinceIcerikKorunur()
        {
            var vt = new VtScreen();
            Feed(vt, "\u001b[2J\u001b[1;1HBoot\u001b[25;80HX");
            vt.Resize(120, 40);
            Check.Equal(120, vt.Cols, "genişledi");
            Check.Equal("Boot", vt.RowText(0), "sol üst korunur");
            Check.Equal('X', vt[24, 79].Ch, "80×25 içindeki her şey yerinde");
            vt.Resize(60, 10);
            Check.True(vt.Cols == 80 && vt.Rows == 25, "en az 80×25 kalır");
        }

        [Test]
        public void Vt_TekTukImlecKomutuGorunumuDegistirmez()
        {
            DateTime t = new DateTime(2026, 11, 19, 12, 0, 0);
            var vt = new VtScreen(now: () => t);
            Feed(vt, "-> \u001b[1;1H");
            t = t.AddSeconds(5);
            Feed(vt, "\u001b[3;1H");
            t = t.AddSeconds(5);
            Feed(vt, "\u001b[4;1H");
            Check.Equal(SerialViewMode.Log, vt.Mode, "seyrek konumlama: günlükte kalınır");
            Feed(vt, "\u001b[1;1Ha\u001b[2;1Hb\u001b[3;1Hc");
            Check.Equal(SerialViewMode.Screen, vt.Mode, "kısa sürede art arda 3 konumlama: ekran çizimi");
        }

        [Test]
        public void Vt_TemelTerminalDavranisi()
        {
            var vt = new VtScreen();
            Feed(vt, "abc\rX");
            Check.Equal("Xbc", vt.RowText(0), "CR satır başına döner, üstüne yazar");
            Feed(vt, "\r\n\u001b(0lqk\u001b(B ok");
            Check.Equal("┌─┐ ok", vt.RowText(1), "DEC çizgi karakterleri");
            Feed(vt, "\u001b[?1h");
            Check.True(vt.AppCursorKeys, "uygulama oku tuşları (DECCKM)");
            Feed(vt, "\u001b[3;7H\u001b[6n");
            Check.Equal("\u001b[3;7R", vt.Responses.Dequeue(), "imleç konumu sorusuna cevap");
            Feed(vt, "\u001b[1;1H\u001b[2K");
            Check.Equal("", vt.RowText(0), "satır silme");
            Feed(vt, "\u001b[1;37;44mM\u001b[0m");
            Check.True(vt[0, 0].Fg == 7 && vt[0, 0].Bg == 4 && vt[0, 0].Bold, "renk: beyaz / mavi, kalın");
            var vt2 = new VtScreen(10, 3);
            Feed(vt2, "1\r\n2\r\n3\r\n4");
            Check.Equal("2|3|4", vt2.RowText(0) + "|" + vt2.RowText(1) + "|" + vt2.RowText(2), "alttan taşınca yukarı kayar");
            Feed(vt2, "\r\n0123456789AB");
            Check.Equal("0123456789|AB", vt2.RowText(1) + "|" + vt2.RowText(2), "satır sonunda alt satıra geçer");
        }

        [Test]
        public void Seri_SistemSatiriKendiSatirinda()
        {
            var s = new SerialScreen();
            Check.Equal("boot...\n● COM7 açıldı\n", Screen(s, "", "boot...\u0001● COM7 açıldı\u0002"), "yarım satırdan sonra yeni satırda");
        }

        // ---------- Satır yakalama ----------

        [Test]
        public void Seri_SatirYakalama()
        {
            var c = new SerialLineCapture();
            var lines = new List<string>();
            foreach (char ch in "lss\b\r  i \rdevs\r\r") { string l = c.Feed(ch); if (l != null) lines.Add(l); }
            Check.Equal("ls|i|devs", string.Join("|", lines), "Backspace, boş satır ve boşluklar");
            lines.Clear();
            foreach (char ch in "abc\u0003ls\r") { string l = c.Feed(ch); if (l != null) lines.Add(l); }
            Check.Equal("ls", string.Join("|", lines), "Ctrl+C satırı siler");
            lines.Clear();
            foreach (char ch in "\u001b[A\r") { string l = c.Feed(ch); if (l != null) lines.Add(l); }
            Check.Equal(0, lines.Count, "ok tuşuyla kartın geçmişinden gelen satır kaydedilmez");
        }

        // ---------- Oturum ----------

        [Test]
        public async Task Seri_OturumGonderirVeGecmiseYazar()
        {
            var io = new FakeSerialIo();
            var s = new SerialSession(new SerialPortProfile { PortName = "COM7" }, () => io);
            var lines = new List<string>();
            s.LineEntered += lines.Add;
            s.Open();
            Check.Equal(SerialState.Open, s.State, "açıldı");
            Check.Equal(115200, io.OpenedWith.BaudRate, "varsayılan baud");
            s.SendKeys("ls");
            s.SendKeys("\r");
            s.Paste("i\r\ndevs\n");
            s.TypeCommand("version", false);
            await WaitSent(io, "ls\ri\rdevs\rversion".Length);
            Check.Equal("ls\ri\rdevs\rversion", io.Sent, "tuşlar ve yapıştırma CR ile");
            Check.Equal("ls|i|devs", string.Join("|", lines), "Enter'lı satırlar geçmişe");

            io.CardSends("-> ");
            string pending = s.TakePending();
            Check.True(pending.Contains("COM7 açıldı") && pending.EndsWith("-> "), "gelen veri ve sistem satırı: " + pending);
            s.Close();
            Check.Equal(SerialState.Closed, s.State, "kapandı");
            s.SendKeys("x");
            await Task.Delay(100);
            Check.True(!io.Sent.EndsWith("x"), "kapalı porta gönderilmez");
        }

        [Test]
        public async Task Seri_GonderimGecikmesi()
        {
            var io = new FakeSerialIo();
            var s = new SerialSession(new SerialPortProfile { PortName = "COM3", CharDelayMs = 20, LineDelayMs = 100 }, () => io);
            s.Open();
            DateTime t0 = DateTime.UtcNow;
            s.SendKeys("ab\rc");
            await WaitSent(io, 4);
            double ms = (io.WriteTimes[3] - t0).TotalMilliseconds;
            Check.True(ms >= 140, "3 × 20 ms karakter + 100 ms satır gecikmesi uygulanmalı (" + ms + " ms)");
            s.Dispose();
        }

        [Test]
        public async Task Seri_Utf8ParcaliGelsedeDogruBirlesir()
        {
            var io = new FakeSerialIo();
            var s = new SerialSession(new SerialPortProfile { PortName = "COM5" }, () => io);
            s.Open();
            s.TakePending();
            byte[] b = Encoding.UTF8.GetBytes("ağ: çalışıyor ─");
            io.CardSendsBytes(b.Take(2).ToArray());          // "a" + "ğ"nin ilk baytı
            io.CardSendsBytes(b.Skip(2).ToArray());
            Check.Equal("ağ: çalışıyor ─", s.TakePending(), "bölünmüş UTF-8 karakter birleşir");
            s.SendKeys("şİ");
            await WaitSent(io, 2);
            Check.Equal(4, io.SentBytes, "gönderilen UTF-8 (2 karakter = 4 bayt)");
            Check.Equal("şİ", io.Sent, "gönderilen metin");
            s.Dispose();
        }

        [Test]
        public async Task Seri_TusKoduGecikmeyleBolunmez()
        {
            var io = new FakeSerialIo();
            var s = new SerialSession(new SerialPortProfile { PortName = "COM3", CharDelayMs = 30 }, () => io);
            s.Open();
            s.SendKeys("\u001b[21~");   // F10
            s.SendKeys("ab");
            await WaitSent(io, 7);
            Check.Equal("\u001b[21~ab", io.Sent, "sıra korunur");
            double inside = (io.WriteTimes[4] - io.WriteTimes[0]).TotalMilliseconds;
            Check.True(inside < 15, "tuş kodu tek seferde yazılır (" + inside + " ms)");
            double between = (io.WriteTimes[6] - io.WriteTimes[5]).TotalMilliseconds;
            Check.True(between >= 25, "düz yazıda karakter gecikmesi sürer (" + between + " ms)");
            s.Dispose();
        }

        [Test]
        public void Seri_AcmaHatasiVeKaybolma()
        {
            var io = new FakeSerialIo { OpenError = "COM7 açılamadı: port başka bir programda açık (TeraTerm olabilir) ya da erişim izni yok." };
            var s = new SerialSession(new SerialPortProfile { PortName = "COM7" }, () => io);
            s.Open();
            Check.Equal(SerialState.Error, s.State, "açılamadı");
            Check.True(s.LastError.Contains("TeraTerm"), "neden: " + s.LastError);

            var io2 = new FakeSerialIo();
            var s2 = new SerialSession(new SerialPortProfile { PortName = "COM8" }, () => io2);
            s2.Open();
            io2.Present = false;
            s2.CheckPresent();
            Check.Equal(SerialState.Error, s2.State, "port kayboldu");
            Check.True(s2.LastError.Contains("USB"), "neden: " + s2.LastError);

            var io3 = new FakeSerialIo();
            var s3 = new SerialSession(new SerialPortProfile { PortName = "COM9" }, () => io3);
            s3.Open();
            io3.Unplug("Port kayboldu (USB çevirici çıkarılmış olabilir)");
            Check.Equal(SerialState.Error, s3.State, "okuma sırasında kayboldu");
            Check.True(!io3.IsOpen, "port kapatıldı");
        }

        // ---------- Kayıt klasörleri ----------

        [Test]
        public async Task Kayit_TelnetVeSeriAyriKlasorlerde()
        {
            Check.Equal("TelnetLoglari", Path.GetFileName(TerminalSnapshot.TelnetFolder), "telnet klasörü");
            Check.Equal("SeriKanalLoglari", Path.GetFileName(TerminalSnapshot.SerialFolder), "seri klasörü");
            Check.Equal(TerminalSnapshot.DefaultFolder, Path.GetDirectoryName(TerminalSnapshot.SerialFolder), "ikisi de logs altında");
            string name = TerminalSnapshot.SerialFileNameFor(new SerialPortProfile { PortName = "COM7", Name = "Kart 3" }, new DateTime(2026, 11, 19, 11, 40, 5, 123));
            Check.Equal("COM7_Kart_3_anlik_20261119_114005_123.log", name, "seri dosya adı");

            string root = Path.Combine(Path.GetTempPath(), "vision-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string dir = Path.Combine(root, "SeriKanalLoglari");
                SnapshotResult r = await TerminalSnapshot.SaveSerialAsync(new SerialPortProfile { PortName = "COM3" }, "satir1\nsatir2", DateTime.Now, dir);
                Check.True(r.Ok, "kaydedildi: " + r.Error);
                Check.Equal(dir, Path.GetDirectoryName(r.Path), "klasör yoksa oluşturulur, dosya içinde");
                SnapshotResult r2 = await TerminalSnapshot.SaveSerialAsync(new SerialPortProfile { PortName = "COM3" }, "x", DateTime.Now, dir);
                Check.True(r2.Ok && r2.Path != r.Path, "klasör varken ikinci kayıt ayrı dosya");
                Check.Equal("satir1\r\nsatir2", File.ReadAllText(r.Path), "CRLF");
                SnapshotResult empty = await TerminalSnapshot.SaveSerialAsync(new SerialPortProfile { PortName = "COM3" }, "", DateTime.Now, dir);
                Check.True(!empty.Ok, "boş ekran kaydedilmez");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        // ---------- Ayarlar ----------

        [Test]
        public void Seri_AyarOzetiVeDogrulama()
        {
            var p = new SerialPortProfile { PortName = "COM7" };
            Check.Equal("115200 8N1", p.Summary, "varsayılan özet");
            Check.Null(p.Validate(), "varsayılanlar geçerli");
            p.Parity = SerialParity.Even; p.DataBits = 7; p.StopBits = 2; p.Flow = SerialFlow.Hardware;
            Check.Equal("115200 7E2 RTS/CTS", p.Summary, "özet");
            Check.NotNull(new SerialPortProfile { PortName = "COM1", BaudRate = 110 }.Validate(), "listede olmayan baud");
            Check.True(!SerialPortProfile.BaudRates.Contains(300) && SerialPortProfile.BaudRates.Contains(921600), "baud listesi");
        }

        [Test]
        public void Seri_AyarlarYapilandirmadaSaklanir()
        {
            var cfg = new AppConfig();
            cfg.SerialPorts.Add(new SerialPortProfile { PortName = "COM7", Name = "Kart 3", BaudRate = 9600, Flow = SerialFlow.XonXoff, CharDelayMs = 2 });
            var xs = new XmlSerializer(typeof(AppConfig));
            var sw = new StringWriter();
            xs.Serialize(sw, cfg);
            var back = (AppConfig)xs.Deserialize(new StringReader(sw.ToString()));
            SerialPortProfile p = back.SerialPorts.Single();
            Check.Equal("Kart 3", p.Label, "ad");
            Check.Equal(9600, p.BaudRate, "baud");
            Check.Equal(SerialFlow.XonXoff, p.Flow, "akış");
            Check.Equal(2, p.CharDelayMs, "gecikme");
            var empty = new StringWriter();
            xs.Serialize(empty, new AppConfig());
            Check.True(!empty.ToString().Contains("SeriPortlar"), "boşsa yazılmaz");
        }
    }
}
