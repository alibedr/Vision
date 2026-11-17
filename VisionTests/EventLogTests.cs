using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vision;

namespace VisionTests
{
    /// <summary>Kart olay günlüğü (G49): yazma / okuma, 5 MB sınırında en eskilerin silinmesi, CSV, dosya işlemlerinin kaydı.</summary>
    public sealed class EventLogTests
    {
        private static string UseTempFolder()
        {
            string d = Path.Combine(Path.GetTempPath(), "VisionOlay_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            CardEventLog.FolderOverride = d;
            return d;
        }

        private static void Release(string dir)
        {
            CardEventLog.FolderOverride = null;
            try { Directory.Delete(dir, true); } catch { }
        }

        [Test]
        public void Gunluk_YazilanOkunur_SekmeVeSatirSonuTemizlenir()
        {
            string dir = UseTempFolder();
            try
            {
                var t = new DateTime(2026, 11, 17, 9, 30, 5);
                CardEventLog.Write("10.0.0.5", CardEventLog.Connection, "Bağlandı (10.0.0.5:23).", t);
                CardEventLog.Write("10.0.0.5", CardEventLog.Boot, "satır1\r\nsatır2\tsekme", t.AddSeconds(1));
                List<CardEvent> list = CardEventLog.Read("10.0.0.5");
                Check.Equal(2, list.Count, "kayıt sayısı");
                Check.Equal(t, list[0].Time, "zaman");
                Check.Equal(CardEventLog.Connection, list[0].Category, "tür");
                Check.Equal("satır1 satır2 sekme", list[1].Message, "çok satırlı mesaj tek satıra iner");
                Check.Equal(0, CardEventLog.Read("10.0.0.6").Count, "başka kartın günlüğü ayrı");
            }
            finally { Release(dir); }
        }

        [Test]
        public void Gunluk_KartAnahtariPortuIcerir()
        {
            Check.Equal("10.0.0.5", CardEventLog.KeyFor("10.0.0.5", 23), "varsayılan port eklenmez");
            Check.Equal("10.0.0.5_2323", CardEventLog.KeyFor(" 10.0.0.5 ", 2323), "farklı port eklenir");
        }

        [Test]
        public void Gunluk_SinirAsilincaEnEskilerSilinir()
        {
            string dir = UseTempFolder();
            try
            {
                // Sınırın hemen üstünde bir günlük hazırla (her satır ~100 bayt)
                Directory.CreateDirectory(dir);
                string path = CardEventLog.PathFor("kart");
                var sb = new StringBuilder();
                int i = 0;
                string pad = new string('x', 60);
                while (sb.Length < CardEventLog.MaxBytes)
                {
                    sb.Append("2026-01-01 00:00:00\tDosya\tolay ").Append(i.ToString("D6")).Append(' ').Append(pad).Append("\r\n");
                    i++;
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                CardEventLog.Write("kart", CardEventLog.Files, "en yeni olay", new DateTime(2026, 2, 1));

                long size = new FileInfo(path).Length;
                Check.True(size <= CardEventLog.MaxBytes, "günlük 5 MB'ı geçmemeli (" + size + ")");
                Check.True(size >= CardEventLog.TrimToBytes - 200, "gereğinden fazla silinmemeli (" + size + ")");
                List<CardEvent> list = CardEventLog.Read("kart");
                Check.Equal("en yeni olay", list[list.Count - 1].Message, "en yeni olay korunur");
                Check.True(!list.Any(e => e.Message.StartsWith("olay 000000 ")), "en eski olay silinir");
                Check.True(list.Any(e => e.Message.StartsWith("olay " + (i - 1).ToString("D6"))), "sondan önceki olay korunur");
            }
            finally { Release(dir); }
        }

        [Test]
        public void Gunluk_CsvAlanlariKacirilir()
        {
            string csv = CardEventLog.ToCsv(new[]
            {
                new CardEvent { Time = new DateTime(2026, 11, 17, 10, 0, 0), Category = "Dosya", Message = "\"a;b\" silindi." },
            });
            string[] lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Check.Equal("Zaman;Tür;Olay", lines[0], "başlık");
            Check.Equal("2026-11-17 10:00:00;Dosya;\"\"\"a;b\"\" silindi.\"", lines[1], "noktalı virgül ve tırnak kaçırılır");
        }

        [Test]
        public void Gunluk_BozukSatirAtlanir()
        {
            Check.Null(CardEventLog.Parse("bozuk satır"), "ayraçsız satır");
            Check.Null(CardEventLog.Parse("dün\tDosya\tx"), "tarihsiz satır");
            Check.NotNull(CardEventLog.Parse("2026-11-17 10:00:00\tDosya\tx\ty"), "mesajdaki sekme bölünmez");
        }

        [Test]
        public async Task Gunluk_DosyaIslemleriVeAktarimKaydedilir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "VisionTests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            try
            {
                var card = new FakeCard();
                string local = Path.Combine(dir, "ayar.cfg");
                File.WriteAllBytes(local, new byte[2048]);
                var settings = new TransferSettings { FtpHost = "10.0.0.1", FtpUser = "vision", CardPathPrefix = FakeCard.FtpPrefix, DeleteAfterCopy = true };
                var b = new FileBrowser(card);
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), settings, null, "/ata0a", new FtpCopyTracker());
                Check.True(r.Ok, "yükleme: " + r.Message);
                Check.True(card.Events.Any(e => e.StartsWith(CardEventLog.Transfer + ": ") && e.Contains("ayar.cfg")),
                           "yükleme olay günlüğüne yazılmalı: " + string.Join(" | ", card.Events));

                // Başarısız iş de yazılır; başlamadan reddedilen istek yazılmaz
                card.Events.Clear();
                var missing = new FsEntry { Name = "yok.bin", FullPath = "/ata0a/yok.bin", Kind = FsKind.File, Size = 10 };
                await b.DeleteAsync(missing, false);
                Check.True(card.Events.Any(e => e.Contains("Başarısız") && e.Contains("yok.bin")),
                           "başarısız silme yazılmalı: " + string.Join(" | ", card.Events));
                card.Events.Clear();
                await b.DeleteAsync(new FsEntry { Name = "ata0a", FullPath = "/ata0a", Kind = FsKind.Device }, false);
                Check.Equal(0, card.Events.Count, "reddedilen istek yazılmamalı");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
