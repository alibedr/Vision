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
    

        [Test]
        public async Task Klasor_KopyalanamayanlarAtlanirSondaListelenir()
        {
            // 9 dosyalık klasör: f2 FTP'ye gönderilemez, f3–f7 kartta kopyalanamaz, f8 eksik yazılır (boyut tutmaz).
            // Kalanlar (f1, f9 ve alt klasördeki g1) yine de karta yüklenmeli; ekranda ilk 5 + "…", olay günlüğünde hepsi.
            string root = TempDir();
            string dir = Path.Combine(root, "paket");
            Directory.CreateDirectory(Path.Combine(dir, "alt"));
            for (int i = 1; i <= 9; i++) LocalFile(dir, "f" + i + ".bin", Data(1000 + i, i));
            LocalFile(Path.Combine(dir, "alt"), "g1.bin", Data(500, 50));
            var card = new FakeCard();
            var ftp = new FakeFtp(card);
            ftp.FailUploadOf.Add("f2.bin");
            foreach (int i in new[] { 3, 4, 5, 6, 7 }) card.FailCopyOf.Add("/f" + i + ".bin");
            card.TruncateCopyOf.Add("/f8.bin");
            var b = new FileBrowser(card);
            OpResult r = await b.UploadAsync(new[] { dir }, ftp, Settings(), null, "/ata0a", new FtpCopyTracker());

            Check.True(!r.Ok, "kopyalanamayan dosya varsa başarılı sayılmaz");
            foreach (string ok in new[] { "/ata0a/paket/f1.bin", "/ata0a/paket/f9.bin", "/ata0a/paket/alt/g1.bin" })
                Check.True(card.CardFiles.ContainsKey(ok), "kalan dosyalar yüklenmeli: " + ok);
            Check.True(!card.CardFiles.ContainsKey("/ata0a/paket/f2.bin"), "FTP'ye gidemeyen dosya kopyalanmaz");
            Check.True(r.Message.Contains("7/10 dosya karta yüklenemedi"), "sayı: " + r.Message);
            Check.True(r.Message.Contains("f2.bin (FTP'ye gönderilemedi"), "FTP hatası listede: " + r.Message);
            Check.True(r.Message.Contains("… (+2)") && !r.Message.Contains("f8.bin"), "ekranda ilk 5 + …: " + r.Message);
            string log = string.Join("\n", card.Events);
            foreach (string name in new[] { "f2.bin", "f3.bin", "f4.bin", "f5.bin", "f6.bin", "f7.bin", "f8.bin (doğrulama" })
                Check.True(log.Contains(name), "olay günlüğünde tam liste: " + name + " yok — " + log);
            Directory.Delete(root, true);
        }

        // ---------- Aynı dosya / klasör aynı anda birden çok karta (1.10.1) ----------
        // Kartlar aynı FTP sunucusunu kullanır. Bir kartın yüklemesi sürerken (dosyası FTP'ye gitmiş, kartta henüz
        // kopyalanmamışken) başka kartın yüklemesi baştan sona araya girer. Eskiden ikisi de FTP'de aynı adı kullandığı
        // için ikinci yükleme birincinin dosyasının üzerine yazıyor, bitince de siliyordu.

        private static OpResult UploadNow(FakeCard card, FakeFtp ftp, string local)
        {
            return new FileBrowser(card).UploadAsync(new[] { local }, ftp, Settings(), null, "/ata0a", new FtpCopyTracker())
                                        .GetAwaiter().GetResult();
        }

        [Test]
        public async Task Yukleme_AyniAdliDosyaIkiKartaAyniAnda()
        {
            string dirA = TempDir(), dirB = TempDir();
            try
            {
                var ftpFiles = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var cardA = new FakeCard(ftpFiles);
                var cardB = new FakeCard(ftpFiles);
                byte[] dataA = Data(3 * MB, 11), dataB = Data(3 * MB, 12);   // aynı ad, aynı boyut, farklı içerik
                string localA = LocalFile(dirA, "uygulama.bin", dataA);
                string localB = LocalFile(dirB, "uygulama.bin", dataB);
                OpResult rB = null;
                var ftpA = new FakeFtp(cardA);
                ftpA.AfterUpload = n => { if (rB == null) rB = UploadNow(cardB, new FakeFtp(cardB), localB); };
                OpResult rA = await new FileBrowser(cardA).UploadAsync(new[] { localA }, ftpA, Settings(), null, "/ata0a", new FtpCopyTracker());

                Check.True(rB != null && rB.Ok, "araya giren yükleme başarılı olmalı: " + (rB != null ? rB.Message : "çalışmadı"));
                Check.True(rA.Ok, "ilk yükleme başarılı olmalı: " + rA.Message);
                Check.SameBytes(dataA, cardA.CardFiles["/ata0a/uygulama.bin"], "A kartındaki dosya kendi içeriği olmalı");
                Check.SameBytes(dataB, cardB.CardFiles["/ata0a/uygulama.bin"], "B kartındaki dosya kendi içeriği olmalı");
                Check.Equal(0, ftpFiles.Count, "FTP'de geçici kopya kalmamalı (" + string.Join(", ", ftpFiles.Keys) + ")");
                Check.True(cardA.Commands.Any(c => c.StartsWith("copy ") && c.Contains("/_tkt_")), "FTP'de işleme özel geçici ad kullanılmalı");
            }
            finally { Directory.Delete(dirA, true); Directory.Delete(dirB, true); }
        }

        [Test]
        public async Task Yukleme_BuyukDosyaIkiKartaAyniAnda()
        {
            string dir = TempDir();
            try
            {
                var ftpFiles = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var cardA = new FakeCard(ftpFiles);
                var cardB = new FakeCard(ftpFiles);
                byte[] data = Data(70 * MB, 13);                             // 2 parça (64 + 6 MB)
                string local = LocalFile(dir, "imaj.bin", data);
                OpResult rB = null;
                var ftpA = new FakeFtp(cardA);
                ftpA.AfterUpload = n => { if (rB == null) rB = UploadNow(cardB, new FakeFtp(cardB), local); };   // A'nın 1. parçası FTP'deyken
                OpResult rA = await new FileBrowser(cardA).UploadAsync(new[] { local }, ftpA, Settings(), null, "/ata0a", new FtpCopyTracker());

                Check.True(rB != null && rB.Ok, "araya giren yükleme başarılı olmalı: " + (rB != null ? rB.Message : "çalışmadı"));
                Check.True(rA.Ok, "ilk yükleme başarılı olmalı: " + rA.Message);
                Check.SameBytes(data, cardA.CardFiles["/ata0a/imaj.bin"], "A kartındaki dosya");
                Check.SameBytes(data, cardB.CardFiles["/ata0a/imaj.bin"], "B kartındaki dosya");
                Check.Equal(0, ftpFiles.Count, "FTP'de parça kalmamalı (" + string.Join(", ", ftpFiles.Keys) + ")");
                NoLeftovers(cardA);
                NoLeftovers(cardB);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public async Task Klasor_AyniKlasorUcKartaAyniAnda()
        {
            string root = TempDir();
            try
            {
                string dir = Path.Combine(root, "paket");
                Directory.CreateDirectory(Path.Combine(dir, "alt"));
                byte[] f1 = Data(2000, 21), f2 = Data(3000, 22), g1 = Data(500, 23);
                LocalFile(dir, "f1.bin", f1);
                LocalFile(dir, "f2.bin", f2);
                LocalFile(Path.Combine(dir, "alt"), "g1.bin", g1);
                var ftpFiles = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var cards = new[] { new FakeCard(ftpFiles), new FakeCard(ftpFiles), new FakeCard(ftpFiles) };
                // A'nın klasörü FTP'deyken B, B'ninki FTP'deyken C baştan sona yüklenir; biten silerken diğerlerine dokunmamalı
                OpResult rB = null, rC = null;
                var ftpB = new FakeFtp(cards[1]);
                ftpB.AfterUpload = n => { if (rC == null) rC = UploadNow(cards[2], new FakeFtp(cards[2]), dir); };
                var ftpA = new FakeFtp(cards[0]);
                ftpA.AfterUpload = n => { if (rB == null) rB = UploadNow(cards[1], ftpB, dir); };
                OpResult rA = await new FileBrowser(cards[0]).UploadAsync(new[] { dir }, ftpA, Settings(), null, "/ata0a", new FtpCopyTracker());

                Check.True(rC != null && rC.Ok, "üçüncü kart: " + (rC != null ? rC.Message : "çalışmadı"));
                Check.True(rB != null && rB.Ok, "ikinci kart: " + (rB != null ? rB.Message : "çalışmadı"));
                Check.True(rA.Ok, "ilk kart: " + rA.Message);
                foreach (FakeCard c in cards)
                {
                    Check.SameBytes(f1, c.CardFiles["/ata0a/paket/f1.bin"], "f1.bin");
                    Check.SameBytes(f2, c.CardFiles["/ata0a/paket/f2.bin"], "f2.bin");
                    Check.SameBytes(g1, c.CardFiles["/ata0a/paket/alt/g1.bin"], "alt/g1.bin");
                }
                Check.Equal(0, ftpFiles.Count, "FTP'de geçici klasör kalmamalı (" + string.Join(", ", ftpFiles.Keys) + ")");
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
