using System;
using System.Linq;
using Vision;

namespace VisionTests
{
    /// <summary>Kartın çıktısını okuyan ayrıştırıcılar (Ornekler klasöründeki örnek çıktılarla).</summary>
    public sealed class ParserTests
    {
        // ---------- Komut cevabı ("value = …") ----------

        [Test]
        public void Cevap_DonusDegeriVeCikti()
        {
            ShellResponse r = ShellResponse.Parse("ls \"/ata0a\"", "-> ls \"/ata0a\"\nvxWorks\napp.bin\nvalue = 0 = 0x0\n-> ");
            Check.Equal(0, r.ReturnCode, "dönüş değeri");
            Check.True(r.IsSuccess, "başarılı sayılmalı");
            Check.True(r.OutputLines.Contains("vxWorks") && r.OutputLines.Contains("app.bin"), "çıktı satırları");
            Check.True(!r.OutputLines.Any(l => l.Contains("value =")), "dönüş satırı çıktıya karışmamalı");
        }

        [Test]
        public void Cevap_NegatifDegerVeAdres()
        {
            ShellResponse r = ShellResponse.Parse("malloc 8388608", "malloc 8388608\nvalue = -2130706432 = 0x81000000\n-> ");
            Check.Equal(-2130706432, r.ReturnCode, "işaretli dönüş değeri");
            Check.Equal("0x81000000", r.ReturnHex, "adres (onaltılık)");
            ShellResponse e = ShellResponse.Parse("open \"/yok\", 0, 0", "open \"/yok\", 0, 0\nvalue = -1 = 0xffffffff\n-> ");
            Check.Equal(-1, e.ReturnCode, "hata dönüşü");
            Check.True(!e.IsSuccess, "-1 başarı sayılmamalı");
        }

        [Test]
        public void Cevap_EksikKomut()
        {
            ShellResponse r = ShellResponse.Parse("xdelete \"/ata0a/x\"", "xdelete \"/ata0a/x\"\nundefined symbol: xdelete\n-> ");
            Check.Equal("xdelete", r.UndefinedSymbol, "eksik komut adı");
            Check.True(!r.IsSuccess, "eksik komut başarı sayılmamalı");
        }

        [Test]
        public void Cevap_EkodanOncekiGecikmisCiktiAyrilir()
        {
            ShellResponse r = ShellResponse.Parse("ls", "eski log satiri\n-> ls\nbir\nvalue = 0 = 0x0\n-> ");
            Check.True(r.Preamble.Contains("eski log satiri"), "ekodan önceki metin Preamble'a ayrılmalı");
            Check.True(!r.OutputLines.Contains("eski log satiri"), "komutun çıktısına karışmamalı");
        }

        // ---------- devs ----------

        [Test]
        public void Devs_RefsSutunlu()
        {
            var names = DevsParser.Parse(Check.Sample("devs_refs.txt"));
            Check.True(names.Contains("/ata0a") && names.Contains("/bd0") && names.Contains("host.host:"), "cihaz adları: " + string.Join(", ", names));
            Check.True(!names.Any(n => n.Contains("[")), "refs sütunu ada karışmamalı");
        }

        [Test]
        public void Devs_EskiBicim()
        {
            var names = DevsParser.Parse(Check.Sample("devs_eski.txt"));
            Check.True(names.Contains("/ata0a") && names.Contains("/null"), "cihaz adları: " + string.Join(", ", names));
        }

        // ---------- dosFsShow ----------

        [Test]
        public void DosFsShow_BosVeToplamAlan()
        {
            DeviceSpace s = DosFsShowParser.Parse(Check.Sample("dosFsShow_ata0a.txt"));
            Check.NotNull(s, "alan bilgisi");
            Check.Equal(7815024L * 512, s.Total, "toplam = sektör × sektör boyutu");
            Check.Equal(3701858304L, s.Free, "boş alan");
        }

        [Test]
        public void DosFsShow_BilgiYoksaNull()
        {
            Check.Null(DosFsShowParser.Parse(new[] { "undefined symbol: dosFsShow" }), "ayrıştırılamayan çıktı");
        }

        // ---------- ls / ll ----------

        [Test]
        public void Ll_DosyaKlasorVeBoyut()
        {
            var list = LsParser.Parse(Check.Sample("ll_ata0a.txt"), "/ata0a");
            FsEntry efi = list.FirstOrDefault(e => e.Name == "EFI");
            FsEntry app = list.FirstOrDefault(e => e.Name == "app.bin");
            FsEntry big = list.FirstOrDefault(e => e.Name == "kara.bin");
            Check.NotNull(efi, "EFI");
            Check.Equal(FsKind.Folder, efi.Kind, "EFI türü");
            Check.NotNull(app, "app.bin");
            Check.Equal(FsKind.File, app.Kind, "app.bin türü");
            Check.Equal(12345678L, app.Size, "app.bin boyutu");
            Check.Equal("/ata0a/app.bin", app.FullPath, "tam yol");
            Check.Equal(2684354560L, big.Size, "2 GB üstü boyut (64 bit)");
            Check.True(!list.Any(e => e.Name.StartsWith("Listing")), "başlık satırı öğe sayılmamalı");
        }

        [Test]
        public void Ls_SatirBasinaAd()
        {
            var list = LsParser.Parse(new[] { "/ata0a/vxWorks", "/ata0a/logs/", "notlar.txt" }, "/ata0a");
            Check.True(list.Any(e => e.Name == "vxWorks"), "tam yol öneki kaldırılmalı");
            Check.Equal(FsKind.Folder, list.First(e => e.Name == "logs").Kind, "/ ile biten klasör");
            Check.Equal(FsKind.File, list.First(e => e.Name == "notlar.txt").Kind, "uzantılı ad dosya");
        }

        // ---------- bootChange ----------

        [Test]
        public void BootChange_AlanlarSirayla()
        {
            var p = new BootChangeParser();
            DateTime t = DateTime.UtcNow;
            string[] lines = Check.Sample("bootChange_oturum.txt");
            // Başlık satır sonlarıyla, her alan istemi satır sonu olmadan ayrı parçada gelir (kart girişi bekler)
            p.Feed(lines[0] + "\n\n", t);
            int fields = 0;
            foreach (string line in lines.Skip(1))
            {
                if (line.Trim().Length == 0 || line.StartsWith("value") || line.StartsWith("->")) continue;
                t = t.AddMilliseconds(500);
                p.Feed(line, t);
                Check.NotNull(p.Current, "istem okunmalı: " + line);
                p.MarkSent("");                    // değiştirmeden geç
                t = t.AddMilliseconds(500);
                p.Feed("\n", t);
                fields++;
            }
            p.Feed("value = 0 = 0x0\n-> ", t.AddMilliseconds(500));
            p.Tick(t.AddSeconds(2));
            Check.Equal(BootChangePhase.Finished, p.Phase, "komut bitti");
            Check.Equal(fields, p.Fields.Count, "alan sayısı");
            BootField host = p.Fields.First(f => f.Label.StartsWith("host inet"));
            Check.Equal("10.0.0.1", host.CardValue.Trim(), "host inet değeri");
            BootField other = p.Fields.First(f => f.Label.StartsWith("other"));
            Check.Equal("", (other.CardValue ?? "").Trim(), "boş alan");
        }

        /// <summary>
        /// G27.3'teki hata: değeri boş bir alanın isteminden SONRA araya giren log, alanın değeri gibi görünüyordu.
        /// </summary>
        [Test]
        public void BootChange_BosAlandanSonraGelenLogDegerSayilmaz()
        {
            var p = new BootChangeParser();
            DateTime t = DateTime.UtcNow;
            p.Feed("'.' = clear field;  '-' = go to previous field;  ^D = quit\n\n", t);
            p.Feed("other (o)            : ", t.AddMilliseconds(100));
            p.Feed("linkUp ", t.AddMilliseconds(100 + 400));          // istemden 400 ms sonra gelen ayrı parça
            p.Feed("motetsec0 hazir\n", t.AddMilliseconds(100 + 420));
            BootField f = p.Fields.FirstOrDefault(x => x.Label.StartsWith("other"));
            Check.NotNull(f, "alan");
            string v = (f.CardValue ?? "").Trim();
            Check.True(v.Length == 0 || f.Uncertain, "log kelimesi değer gibi gösterilmemeli (değer: \"" + v + "\")");
        }

        [Test]
        public void BootChange_KomutTanima()
        {
            Check.True(BootChangeParser.IsBootChangeCommand("bootChange"), "bootChange");
            Check.True(BootChangeParser.IsBootChangeCommand("bspBootChange()"), "bspBootChange()");
            Check.True(!BootChangeParser.IsBootChangeCommand("bootchange"), "büyük / küçük harf duyarlı");
            Check.True(!BootChangeParser.IsBootChangeCommand("bootChange 1"), "parametreli değil");
        }
    }
}
