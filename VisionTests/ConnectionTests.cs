using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Vision;

namespace VisionTests
{
    /// <summary>
    /// Bağlantı kopması ve yeniden bağlanma (G52), bilgisayardaki sahte telnet sunucusuyla: planlı reboot kırmızı uyarı
    /// vermez; tutmayan (bağlanıp hemen kopan) bağlantılar ekrana ve olay günlüğüne tekrar tekrar yazılmaz; kalıcı
    /// bağlantıda toplam kesinti süresi bir kez yazılır.
    /// </summary>
    public sealed class ConnectionTests
    {
        /// <summary>Bilgisayarda çalışan sahte kart: bağlantıları kabul eder, istendiğinde kapatır.</summary>
        private sealed class FakeServer : IDisposable
        {
            private TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly List<TcpClient> _clients = new List<TcpClient>();
            private int _port;
            private bool _disposed;
            /// <summary>Bu kadar bağlantı kabul edilip hemen kapatılır (tutmayan bağlantı).</summary>
            public int DropNext;
            public int Accepted;

            public FakeServer()
            {
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                Loop(_listener);
            }

            public int Port { get { return _port; } }

            /// <summary>Kart yeniden başlıyor: bağlantılar kesilir, port <paramref name="ms"/> boyunca kapalı kalır.</summary>
            public async Task RebootAsync(int ms)
            {
                _listener.Stop();
                CloseAll();
                await Task.Delay(ms);
                if (_disposed) return;
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Start();
                Loop(_listener);
            }

            private async void Loop(TcpListener listener)
            {
                while (true)
                {
                    TcpClient c;
                    try { c = await listener.AcceptTcpClientAsync(); }
                    catch { return; }
                    lock (_clients)
                    {
                        Accepted++;
                        if (DropNext > 0) { DropNext--; c.Close(); continue; }
                        _clients.Add(c);
                    }
                    Drain(c);
                    try { await c.GetStream().WriteAsync(new byte[] { (byte)'-', (byte)'>', (byte)' ' }, 0, 3); } catch { }
                }
            }

            private static async void Drain(TcpClient c)
            {
                var buf = new byte[256];
                try { while (await c.GetStream().ReadAsync(buf, 0, buf.Length) > 0) { } } catch { }
            }

            /// <summary>Açık bağlantıları kart kapatmış gibi keser.</summary>
            public void CloseAll()
            {
                lock (_clients)
                {
                    foreach (TcpClient c in _clients) { try { c.Close(); } catch { } }
                    _clients.Clear();
                }
            }

            public void Dispose()
            {
                _disposed = true;
                _listener.Stop();
                CloseAll();
            }
        }

        private static CardSession Session(FakeServer server, out string folder)
        {
            folder = Path.Combine(Path.GetTempPath(), "VisionBag_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            CardEventLog.FolderOverride = folder;
            var group = new CardGroup { Name = "Test", Port = server.Port };
            var card = new CardConfig { Ip = "127.0.0.1", Group = group };
            group.Cards.Add(card);
            var settings = new AppSettings();
            // Yeniden bağlanma aralıkları testte kısaltılır (gerçekte 2 sn / 30 sn / 15 sn, G53)
            CardSession.OwnRebootRetryMs = 150;
            CardSession.UnplannedFirstDelayMs = 300;
            CardSession.UnplannedRetryMs = 200;
            settings.Monitor.ReconnectTimeoutMs = 1000;
            settings.Monitor.StableAfterMs = 1500;
            settings.LogToFile = false;
            var s = new CardSession(card, settings);
            s.AttachView();
            return s;
        }

        private static async Task WaitFor(CardSession s, Func<bool> cond, int ms, string what)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
            while (!cond())
            {
                if (DateTime.UtcNow > end) throw new CheckFailedException("Beklenen durum gelmedi: " + what + " (durum: " + s.State + ")");
                s.MonitorTick(DateTime.UtcNow);
                await Task.Delay(40);
            }
        }

        private static int Count(string text, string part)
        {
            int n = 0, i = 0;
            while ((i = text.IndexOf(part, i, StringComparison.Ordinal)) >= 0) { n++; i += part.Length; }
            return n;
        }

        private static void Release(CardSession s, string folder)
        {
            CardSession.OwnRebootRetryMs = 2000;
            CardSession.UnplannedFirstDelayMs = 30000;
            CardSession.UnplannedRetryMs = 15000;
            s.Dispose();
            CardEventLog.FolderOverride = null;
            try { Directory.Delete(folder, true); } catch { }
        }

        [Test]
        public async Task Baglanti_KopmaBirKezYazilirTutmayanBaglantilarSessiz()
        {
            using (var server = new FakeServer())
            {
                string folder;
                CardSession s = Session(server, out folder);
                try
                {
                    await s.ConnectAsync();
                    Check.Equal(CardState.Connected, s.State, "bağlandı");
                    server.DropNext = 3;                     // kart açılırken 3 kez bağlanıp hemen kopsun
                    server.CloseAll();
                    await WaitFor(s, () => s.State == CardState.Recovering, 5000, "kopma algılandı");
                    await WaitFor(s, () => server.Accepted >= 5 && s.State == CardState.Connected, 15000, "kalıcı bağlantı");
                    await WaitFor(s, () => !s.InOutage, 5000, "kesinti kapandı");

                    string text = s.AttachView();
                    Check.Equal(1, Count(text, CardSession.AlertPrefix), "kırmızı uyarı yalnız bir kez: " + text);
                    Check.Equal(1, Count(text, "Bağlantı yeniden kuruldu"), "yeniden kuruldu bir kez");
                    Check.True(text.Contains("kesinti süresi"), "toplam kesinti süresi yazılır");
                    List<CardEvent> ev = CardEventLog.Read(CardEventLog.KeyFor(s.Card));
                    Check.Equal(1, ev.Count(e => e.Message.StartsWith("Bağlantı koptu")), "olay günlüğünde kopma bir kez");
                    Check.Equal(1, ev.Count(e => e.Message.StartsWith("Bağlantı yeniden kuruldu")), "olay günlüğünde yeniden kuruldu bir kez");
                    Check.True(ev.Any(e => e.Message.Contains("hemen koptu")), "tutmayan bağlantılar özette");
                }
                finally { Release(s, folder); }
            }
        }

        [Test]
        public async Task Baglanti_PlanliRebootKirmiziUyariVermez()
        {
            using (var server = new FakeServer())
            {
                string folder;
                CardSession s = Session(server, out folder);
                try
                {
                    await s.ConnectAsync();
                    await s.SendCommandAsync("reboot 2", "test");
                    await Task.Delay(100);
                    server.CloseAll();
                    await WaitFor(s, () => s.State == CardState.Rebooting || s.State == CardState.Connected && s.InOutage, 5000, "planlı kopma");
                    await WaitFor(s, () => s.State == CardState.Connected && !s.InOutage, 15000, "kart açıldı");

                    string text = s.AttachView();
                    Check.Equal(0, Count(text, CardSession.AlertPrefix), "planlı rebootta kırmızı uyarı yok: " + text);
                    Check.True(text.Contains("Kart yeniden başlatılıyor"), "yeniden başlatılıyor yazılır");
                    Check.True(text.Contains("Kart yeniden başladı"), "açılınca bir kez yazılır");
                    List<CardEvent> ev = CardEventLog.Read(CardEventLog.KeyFor(s.Card));
                    Check.True(!ev.Any(e => e.Message.StartsWith("Bağlantı koptu")), "olay günlüğünde kopma (hata) yok");
                    Check.True(ev.Any(e => e.Category == CardEventLog.Reboot && e.Message.Contains("yeniden başladı")), "yeniden başladı olayı");
                }
                finally { Release(s, folder); }
            }
        }

        [Test]
        public async Task Baglanti_RebootSonrasiGelmezseKirmiziUyari()
        {
            using (var server = new FakeServer())
            {
                string folder;
                CardSession s = Session(server, out folder);
                try
                {
                    await s.ConnectAsync();
                    await s.SendCommandAsync("reboot", "test");
                    // Sunucu kapanır: kart hiç geri gelmez
                    server.Dispose();
                    await WaitFor(s, () => s.State == CardState.Rebooting, 5000, "planlı kopma");
                    // Süre sınırını kısaltıp denetle
                    await Task.Delay(200);
                    SetRebootReturn(s, 300);
                    await WaitFor(s, () => s.State == CardState.Recovering, 5000, "süre doldu, kırmızı uyarı");
                    string text = s.AttachView();
                    Check.Equal(1, Count(text, CardSession.AlertPrefix), "bir kez kırmızı uyarı");
                    Check.True(text.Contains("açılmadı"), "neden yazılır");
                }
                finally { Release(s, folder); }
            }
        }

        private static void SetRebootReturn(CardSession s, int ms)
        {
            var f = typeof(CardSession).GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ((AppSettings)f.GetValue(s)).Monitor.RebootReturnMs = ms;
        }

        [Test]
        public async Task Baglanti_RebootuAtanIlkBaglanir()
        {
            // İki kişi aynı karta bağlı (kartlara birden çok kişi bağlanabilir; loglar ilk bağlanana gider).
            // B reboot atar: kart açılınca önce B'nin Vision'ı bağlanmalı, A'nınki habersiz kopma olarak bekler (G53).
            using (var server = new FakeServer())
            {
                string folder;
                CardSession a = Session(server, out folder);
                CardSession b = Session(server, out folder);
                try
                {
                    CardSession.UnplannedFirstDelayMs = 1500;   // kartın açılmasından (700 ms) uzun
                    CardSession.UnplannedRetryMs = 1000;
                    await a.ConnectAsync();
                    await b.ConnectAsync();
                    await b.SendCommandAsync("reboot", "test");
                    await Task.Delay(100);
                    Task boot = server.RebootAsync(700);
                    DateTime end = DateTime.UtcNow.AddSeconds(15);
                    while (!(a.State == CardState.Connected && b.State == CardState.Connected && !a.InOutage && !b.InOutage))
                    {
                        if (DateTime.UtcNow > end) throw new CheckFailedException("İki oturum da geri gelmedi: A " + a.State + ", B " + b.State);
                        a.MonitorTick(DateTime.UtcNow);
                        b.MonitorTick(DateTime.UtcNow);
                        await Task.Delay(30);
                    }
                    await boot;
                    Check.True(b.ConnectedSinceUtc.Value < a.ConnectedSinceUtc.Value,
                               "reboot'u atan (B) önce bağlanmalı: B " + b.ConnectedSinceUtc.Value.ToString("HH:mm:ss.fff") +
                               ", A " + a.ConnectedSinceUtc.Value.ToString("HH:mm:ss.fff"));
                    Check.Equal(0, Count(b.AttachView(), CardSession.AlertPrefix), "B'de kırmızı uyarı yok");
                    Check.Equal(1, Count(a.AttachView(), CardSession.AlertPrefix), "A için habersiz kopma: bir kez kırmızı uyarı");
                }
                finally { a.Dispose(); Release(b, folder); }
            }
        }

        [Test]
        public async Task Baglanti_HabersizKopmadaIlkDenemeBekler()
        {
            using (var server = new FakeServer())
            {
                string folder;
                CardSession s = Session(server, out folder);
                try
                {
                    CardSession.UnplannedFirstDelayMs = 1500;
                    await s.ConnectAsync();
                    int before = server.Accepted;
                    server.CloseAll();
                    await WaitFor(s, () => s.State == CardState.Recovering, 5000, "kopma algılandı");
                    DateTime dropped = DateTime.UtcNow;
                    while ((DateTime.UtcNow - dropped).TotalMilliseconds < 1000)
                    {
                        s.MonitorTick(DateTime.UtcNow);
                        await Task.Delay(40);
                    }
                    Check.Equal(before, server.Accepted, "habersiz kopmada ilk bekleme süresinde yeniden bağlanılmamalı");
                    await WaitFor(s, () => s.State == CardState.Connected, 5000, "bekleme sonrası bağlandı");
                }
                finally { Release(s, folder); }
            }
        }

        [Test]
        public void Baglanti_RebootKomutlariTaninir()
        {
            foreach (string c in new[] { "reboot", "reboot 2", " reboot(2) ", "reboot()", "FPGAGiveBoardReset", "reboot;" })
                Check.True(CardSession.IsRebootCommand(c), "reboot sayılmalı: " + c);
            foreach (string c in new[] { "rebootLog", "echo reboot", "ls", "reboot 2 3" })
                Check.True(!CardSession.IsRebootCommand(c), "reboot sayılmamalı: " + c);
        }
    }
}
