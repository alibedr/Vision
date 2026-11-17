using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vision;

namespace VisionTests
{
    /// <summary>
    /// Kart ile bilgisayar arasındaki aktarım, sahte kartla (G48): parçalı yükleme / indirme, iptal, bağlantı kopması,
    /// doğrulama. Gerçek kart gerekmez.
    /// </summary>
    public sealed class TransferTests
    {
        private const long MB = 1024 * 1024;

        private static TransferSettings Settings()
        {
            return new TransferSettings { FtpHost = "10.0.0.1", FtpUser = "vision", CardPathPrefix = FakeCard.FtpPrefix, DeleteAfterCopy = true };
        }

        private static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "VisionTests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(d);
            return d;
        }

        private static byte[] Data(long size, int seed)
        {
            var b = new byte[size];
            new Random(seed).NextBytes(b);
            return b;
        }

        private static string LocalFile(string dir, string name, byte[] data)
        {
            string p = Path.Combine(dir, name);
            File.WriteAllBytes(p, data);
            return p;
        }

        private static void NoLeftovers(FakeCard card)
        {
            Check.Equal(0, card.OpenFileCount, "kartta açık dosya kalmamalı");
            Check.Equal(0, card.BufferCount, "kartta ayrılmış ara bellek kalmamalı");
            Check.Equal(0, card.Cleanups.Count, "temizlik kaydı kalmamalı (" + string.Join(", ", card.Cleanups.Values) + ")");
            Check.True(!card.FtpFiles.Keys.Any(k => k.Contains(".part")), "FTP'de parça kalmamalı");
            Check.Equal(0, card.TransfersActive, "aktarım bayrağı kapanmalı");
        }

        [Test]
        public async Task Yukleme_BuyukDosyaParcaliVeBirebirAyni()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                byte[] data = Data(150 * MB + 12345, 1);                 // 3 parça (64 + 64 + 22 MB)
                string local = LocalFile(dir, "imaj.bin", data);
                var b = new FileBrowser(card);
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), Settings(), null, "/ata0a", new FtpCopyTracker());
                Check.True(r.Ok, "yükleme başarılı olmalı: " + r.Message);
                Check.SameBytes(data, card.CardFiles["/ata0a/imaj.bin"], "karttaki dosya");
                Check.True(card.Commands.Any(c => c.StartsWith("lseek")), "konum doğrulaması yapılmalı");
                NoLeftovers(card);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Yukleme_KucukDosyaTekCopy()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                byte[] data = Data(3 * MB, 2);
                string local = LocalFile(dir, "ayar.cfg", data);
                var b = new FileBrowser(card);
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), Settings(), null, "/ata0a", new FtpCopyTracker());
                Check.True(r.Ok, "yükleme başarılı olmalı: " + r.Message);
                Check.SameBytes(data, card.CardFiles["/ata0a/ayar.cfg"], "karttaki dosya");
                Check.True(card.Commands.Any(c => c.StartsWith("copy ")), "küçük dosya tek copy ile gitmeli");
                Check.True(!card.FtpFiles.ContainsKey("ayar.cfg"), "kopyadan sonra FTP'deki geçici kopya silinmeli");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Indirme_BuyukDosyaParcaliVeBirebirAyni()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                byte[] data = Data(130 * MB + 7, 3);
                card.CardFiles["/ata0a/kayit.bin"] = data;
                var entry = new FsEntry { Name = "kayit.bin", FullPath = "/ata0a/kayit.bin", Kind = FsKind.File, Size = data.Length };
                string local = Path.Combine(dir, "kayit.bin");
                var b = new FileBrowser(card);
                OpResult r = await b.FetchCopyAsync(entry, local, new FakeFtp(card), Settings(), false);
                Check.True(r.Ok, "indirme başarılı olmalı: " + r.Message);
                Check.SameBytes(data, File.ReadAllBytes(local), "bilgisayardaki dosya");
                NoLeftovers(card);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Indirme_KucukDosya()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                byte[] data = Data(1 * MB + 3, 4);
                card.CardFiles["/ata0a/log.txt"] = data;
                var entry = new FsEntry { Name = "log.txt", FullPath = "/ata0a/log.txt", Kind = FsKind.File, Size = data.Length };
                string local = Path.Combine(dir, "log.txt");
                OpResult r = await new FileBrowser(card).FetchCopyAsync(entry, local, new FakeFtp(card), Settings(), false);
                Check.True(r.Ok, "indirme başarılı olmalı: " + r.Message);
                Check.SameBytes(data, File.ReadAllBytes(local), "bilgisayardaki dosya");
                Check.True(!card.FtpFiles.Keys.Any(k => k.StartsWith("_tkt_")), "FTP'deki geçici kopya silinmeli");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Yukleme_IptalYarimDosyayiSiler()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                string local = LocalFile(dir, "imaj.bin", Data(150 * MB, 5));
                var tp = new TransferProgress(TransferDirection.ToCard);
                int writes = 0;
                card.AfterCommand = c => { if (c.StartsWith("write ") && ++writes == 10) tp.Cancel(); };   // 2. parçanın ortası
                var b = new FileBrowser(card);
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), Settings(), null, "/ata0a", new FtpCopyTracker(), tp);
                Check.True(!r.Ok, "iptal edilen yükleme başarılı sayılmamalı");
                Check.True(!card.CardFiles.ContainsKey("/ata0a/imaj.bin"), "kartta yarım dosya kalmamalı");
                NoLeftovers(card);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Yukleme_BaglantiKoparsaTemizlikKaydiKalir()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard();
                string local = LocalFile(dir, "imaj.bin", Data(100 * MB, 6));
                var b = new FileBrowser(card);
                int writes = 0;
                card.AfterCommand = c => { if (c.StartsWith("write ") && ++writes == 3) card.DropAfterCommands = card.Commands.Count; };
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), Settings(), null, "/ata0a", new FtpCopyTracker());
                Check.True(!r.Ok, "bağlantı koptuysa başarılı sayılmamalı");
                // Yeniden bağlanınca çalıştırılacaklar: açık dosyalar kapatılır, ara bellek bırakılır, yarım dosya silinir (G42.8)
                Check.True(card.Cleanups.Values.Any(v => v.StartsWith("close ")), "açık dosyayı kapatma kaydı");
                Check.True(card.Cleanups.Values.Any(v => v.StartsWith("free ")), "ara belleği bırakma kaydı");
                Check.True(card.Cleanups.Values.Any(v => v == "rm \"/ata0a/imaj.bin\""), "yarım dosyayı silme kaydı");
                Check.Equal(0, card.TransfersActive, "aktarım bayrağı kapanmalı");
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>
        /// G42.11: araya giren bir cevap okunan bayt sayısını yanlış gösterirse dosya sessizce bozulmamalı — konum
        /// doğrulaması yakalamalı, yarım dosya silinmeli.
        /// </summary>
        [Test]
        public async Task Yukleme_YanlisOkumaSayisiDogrulamadaYakalanir()
        {
            string dir = TempDir();
            try
            {
                var card = new FakeCard { LieOnReadNumber = 2 };
                string local = LocalFile(dir, "imaj.bin", Data(70 * MB, 7));
                var b = new FileBrowser(card);
                OpResult r = await b.UploadAsync(new[] { local }, new FakeFtp(card), Settings(), null, "/ata0a", new FtpCopyTracker());
                Check.True(!r.Ok, "bozuk aktarım başarılı sayılmamalı");
                Check.True((r.Message ?? "").Contains("doğrulama"), "neden doğrulama olmalı: " + r.Message);
                Check.True(!card.CardFiles.ContainsKey("/ata0a/imaj.bin"), "bozuk dosya silinmeli");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task KartIci_BuyukKopyaVeTasima()
        {
            var card = new FakeCard();
            byte[] data = Data(90 * MB, 8);
            card.CardFiles["/ata0a/a.bin"] = data;
            var b = new FileBrowser(card);
            var entry = new FsEntry { Name = "a.bin", FullPath = "/ata0a/a.bin", Kind = FsKind.File, Size = data.Length };
            OpResult r = await b.TransferAsync(entry, "/bd0", true);
            Check.True(r.Ok, "kopya başarılı: " + r.Message);
            Check.SameBytes(data, card.CardFiles["/bd0/a.bin"], "kopya");
            Check.True(card.CardFiles.ContainsKey("/ata0a/a.bin"), "kopyalamada kaynak kalır");
            OpResult m = await b.TransferAsync(new FsEntry { Name = "a.bin", FullPath = "/bd0/a.bin", Kind = FsKind.File, Size = data.Length }, "/ata0a/yedek", false);
            Check.True(m.Ok, "taşıma başarılı: " + m.Message);
            Check.True(!card.CardFiles.ContainsKey("/bd0/a.bin"), "taşımada kaynak silinir");
            Check.SameBytes(data, card.CardFiles["/ata0a/yedek/a.bin"], "taşınan");
            NoLeftovers(card);
        }
    }
}
