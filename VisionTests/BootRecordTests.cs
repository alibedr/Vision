using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Vision;

namespace VisionTests
{
    /// <summary>
    /// Sahte kartın bspBootChange / bootChange'i (G51 testleri): alanları VxWorks gibi tek tek sorar, girişi ekolar,
    /// "." temizler, "-" geri gider; istenirse değeri ayrı parçada gönderir ya da istemden sonra log basar.
    /// </summary>
    public sealed class FakeBootCard : IBootConsole
    {
        public sealed class Field
        {
            public string Label;
            public string Value;
            /// <summary>Kart bu alanı değiştirmez (doğrulama hatasını sınamak için).</summary>
            public bool Locked;
        }

        private struct Pending { public string Text; public DateTime At; }

        public readonly List<Field> Fields = new List<Field>();
        public readonly List<string> Sent = new List<string>();
        public readonly List<string> CommandsRun = new List<string>();
        public string CommandName = "bspBootChange";
        public bool Connected { get; set; }
        /// <summary>Bu alanın değeri istemden 80 ms sonra ayrı parçada gelir.</summary>
        public string SplitValueOn;
        /// <summary>Bu alanın isteminden hemen sonra (bir kez) log basılır.</summary>
        public string LogAfterPromptOn;
        public int Begins, Ends;

        private readonly List<Pending> _out = new List<Pending>();
        private readonly object _lock = new object();
        private int _index = -1;
        private bool _logged;

        public FakeBootCard()
        {
            Connected = true;
            Add("boot device", "motetsec0");
            Add("processor number", "0");
            Add("host name", "host");
            Add("file name", "/ata0a/vxWorks");
            Add("inet on ethernet (e)", "10.0.0.5:ffffff00");
            Add("host inet (h)", "10.0.0.1");
            Add("user (u)", "target");
            Add("ftp password (pw) (blank = use rsh)", "gizli");
            Add("flags (f)", "0x0");
            Add("target name (tn)", "kart5");
            Add("other (o)", "");
        }

        public void Add(string label, string value) { Fields.Add(new Field { Label = label, Value = value }); }
        public string Value(string labelStart) { return Fields.First(f => f.Label.StartsWith(labelStart)).Value; }
        public void Set(string labelStart, string value) { Fields.First(f => f.Label.StartsWith(labelStart)).Value = value; }

        private void Emit(string text, int delayMs = 0)
        {
            lock (_lock) _out.Add(new Pending { Text = text, At = DateTime.UtcNow.AddMilliseconds(delayMs) });
        }

        private void Prompt()
        {
            Field f = Fields[_index];
            string head = (f.Label.Length < 21 ? f.Label.PadRight(21) : f.Label) + ": ";
            if (f.Label.StartsWith(SplitValueOn ?? "\0") && f.Value.Length > 0)
            {
                Emit(head);
                Emit(f.Value + " ", 80);
            }
            else Emit(head + (f.Value.Length > 0 ? f.Value + " " : ""));
            if (!_logged && f.Label.StartsWith(LogAfterPromptOn ?? "\0"))
            {
                _logged = true;
                // Logun ilk sözcüğü ayrı parçada gelir: boş alanın değeri gibi görünür, satır tamamlanınca şüpheli sayılmalı
                Emit("tNetTask: ", 100);
                Emit("link up\n", 150);
            }
        }

        public void Begin() { Begins++; }
        public void End() { Ends++; }

        public Task SendCommandAsync(string command)
        {
            CommandsRun.Add(command);
            Emit(command + "\n");
            if (command != CommandName) { Emit("undefined symbol: " + command + "\nvalue = -1 = 0xffffffff\n-> "); return Task.FromResult(0); }
            Emit("\n'.' = clear field;  '-' = go to previous field;  ^D = quit\n\n");
            _index = 0;
            Prompt();
            return Task.FromResult(0);
        }

        public Task SendLineAsync(string text)
        {
            Sent.Add(text);
            Emit(text + "\n");
            Field f = Fields[_index];
            if (text == "-") { _index = Math.Max(0, _index - 1); Prompt(); return Task.FromResult(0); }
            if (!f.Locked)
            {
                if (text == ".") f.Value = "";
                else if (text.Length > 0) f.Value = text;
            }
            _index++;
            if (_index < Fields.Count) Prompt();
            else { _index = -1; Emit("value = 0 = 0x0\n-> "); }
            return Task.FromResult(0);
        }

        public Task SendQuitAsync()
        {
            _index = -1;
            Emit("\nvalue = 0 = 0x0\n-> ");
            return Task.FromResult(0);
        }

        public List<TerminalTap.Chunk> Take()
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;
                List<Pending> ready = _out.Where(p => p.At <= now).ToList();
                if (ready.Count == 0) return null;
                foreach (Pending p in ready) _out.Remove(p);
                return ready.Select(p => new TerminalTap.Chunk { Text = p.Text, Utc = p.At }).ToList();
            }
        }
    }

    /// <summary>Boot ayarı kayıtları (G51): okuma, kayıt, karşılaştırma, geri yükleme ve doğrulama — sahte kartla.</summary>
    public sealed class BootRecordTests
    {
        private static BootRecord Record(FakeBootCard card, string name)
        {
            BootRunResult r = BootChangeRunner.ReadAsync(card, card.CommandName, CancellationToken.None).GetAwaiter().GetResult();
            Check.True(r.Ok, "okuma: " + r.Error);
            return BootRecordLogic.FromFields(name, card.CommandName, "10.0.0.5", r.Fields, DateTime.Now);
        }

        [Test]
        public async Task Boot_OkumaHicbirSeyDegistirmez()
        {
            var card = new FakeBootCard();
            BootRunResult r = await BootChangeRunner.ReadAsync(card, card.CommandName, CancellationToken.None);
            Check.True(r.Ok, "okuma başarılı: " + r.Error);
            Check.Equal(card.Fields.Count, r.Fields.Count, "alan sayısı");
            Check.True(card.Sent.All(s => s == ""), "her alan değiştirilmeden geçilmeli: " + string.Join("|", card.Sent));
            Check.Equal("10.0.0.1", r.Fields.First(f => f.Label.StartsWith("host inet")).CardValue.Trim(), "host inet");
            Check.Equal("", (r.Fields.First(f => f.Label.StartsWith("other")).CardValue ?? "").Trim(), "boş alan");
            Check.Equal(1, card.Begins, "konsol bir kez açılır");
            Check.Equal(1, card.Ends, "konsol kapanır");
        }

        [Test]
        public async Task Boot_AyriParcadaGelenDegerOkunur()
        {
            var card = new FakeBootCard { SplitValueOn = "host inet" };
            BootRunResult r = await BootChangeRunner.ReadAsync(card, card.CommandName, CancellationToken.None);
            Check.True(r.Ok, "okuma: " + r.Error);
            Check.Equal("10.0.0.1", r.Fields.First(f => f.Label.StartsWith("host inet")).CardValue.Trim(), "istemden sonra gelen değer");
        }

        [Test]
        public async Task Boot_LogKarisirsaYenidenOkunur()
        {
            var card = new FakeBootCard { LogAfterPromptOn = "other" };
            BootRunResult r = await BootChangeRunner.ReadAsync(card, card.CommandName, CancellationToken.None);
            Check.True(r.Ok, "okuma: " + r.Error);
            Check.Equal(0, r.Uncertain.Count, "ikinci okumada şüpheli alan kalmamalı");
            Check.Equal(2, card.CommandsRun.Count, "şüpheli alan olunca bir kez daha okunur");
        }

        [Test]
        public async Task Boot_KomutKarttaYoksaHata()
        {
            var card = new FakeBootCard { CommandName = "bootChange" };
            BootRunResult r = await BootChangeRunner.ReadAsync(card, "bspBootChange", CancellationToken.None);
            Check.True(!r.Ok, "komut yoksa başarısız");
            Check.True((r.Error ?? "").Contains("çalıştırmadı"), "neden: " + r.Error);
        }

        [Test]
        public void Boot_KayittaHerAlanVarParolaDahil()
        {
            var card = new FakeBootCard();
            BootRecord rec = Record(card, "Fabrika");
            Check.Equal(card.Fields.Count, rec.Fields.Count, "kartın sorduğu her alan kayıtta");
            Check.Equal("gizli", rec.ValueOf("ftp password (pw) (blank = use rsh)"), "FTP parolası da kaydedilir");
            Check.Equal("10.0.0.5:ffffff00", rec.ValueOf("inet on ethernet (e)"), "adres kayıtta");
            Check.Equal("bspBootChange", rec.Command, "komut");
        }

        [Test]
        public async Task Boot_AyniKartaGeriYukleme()
        {
            var card = new FakeBootCard();
            BootRecord rec = Record(card, "Önce");
            // Kart sonradan değişti
            card.Set("file name", "/ata0a/yeni.bin");
            card.Set("inet on ethernet", "10.0.0.99:ffffff00");
            card.Set("other", "deneme");
            card.Set("ftp password", "baska");
            card.Sent.Clear();

            List<BootPlanRow> plan = BootRecordLogic.Plan(rec, (await BootChangeRunner.ReadAsync(card, card.CommandName, CancellationToken.None)).Fields, false);
            Check.Equal(4, plan.Count(p => p.WillWrite), "önizlemede 4 değişiklik: " + string.Join(", ", plan.Where(p => p.WillWrite).Select(p => p.Label)));
            Check.Equal(BootPlanKind.Change, plan.First(p => p.Label.StartsWith("ftp password")).Kind, "parola da geri yüklenir");

            card.Sent.Clear();
            BootApplyResult a = await BootChangeRunner.ApplyAsync(card, rec, null, false, CancellationToken.None);
            Check.True(a.Ok, "geri yükleme: " + a.Error + " " + string.Join("; ", a.Mismatches));
            Check.Equal("/ata0a/vxWorks", card.Value("file name"), "dosya adı geri geldi");
            Check.Equal("10.0.0.5:ffffff00", card.Value("inet on ethernet"), "aynı kartta adres de geri gelir");
            Check.Equal("", card.Value("other"), "boş alan temizlendi");
            Check.Equal("gizli", card.Value("ftp password"), "parola geri geldi");
            Check.Equal(4, a.Written.Count, "yalnız farklı alanlar yazıldı: " + string.Join("; ", a.Written));
            Check.True(card.Sent.Contains("."), "temizleme için \".\" gönderildi");
        }

        [Test]
        public async Task Boot_BaskaKartaUygulamadaAdresAtlanir()
        {
            var source = new FakeBootCard();
            BootRecord rec = Record(source, "Kaynak");
            var target = new FakeBootCard();
            target.Set("inet on ethernet", "10.0.0.6:ffffff00");
            target.Set("file name", "/ata0a/eski.bin");
            BootApplyResult a = await BootChangeRunner.ApplyAsync(target, rec, null, true, CancellationToken.None);
            Check.True(a.Ok, "uygulama: " + a.Error);
            Check.Equal("10.0.0.6:ffffff00", target.Value("inet on ethernet"), "hedef kartın adresi korunur");
            Check.Equal("/ata0a/vxWorks", target.Value("file name"), "diğer alanlar uygulanır");
        }

        [Test]
        public async Task Boot_DogrulamaTutmayanAlaniBildirir()
        {
            var card = new FakeBootCard();
            BootRecord rec = Record(card, "Önce");
            card.Set("flags", "0x8");
            card.Fields.First(f => f.Label.StartsWith("flags")).Locked = true;   // kart bu alanı kabul etmiyor
            BootApplyResult a = await BootChangeRunner.ApplyAsync(card, rec, null, false, CancellationToken.None);
            Check.True(!a.Ok, "tutmayan alan varsa başarılı sayılmamalı");
            Check.True(a.Mismatches.Any(m => m.StartsWith("flags")), "tutmayan alan listelenmeli: " + string.Join("; ", a.Mismatches));
        }

        [Test]
        public void Boot_PenceredenKayitGonderilenDegerleriAlir()
        {
            // Boot ayarları penceresinin "Bu ayarları kaydet…" düğmesi: kartın gösterdiği değer ya da gönderilen yeni değer (G51.9)
            var fields = new List<BootField>
            {
                new BootField { Label = "file name", OriginalValue = "/ata0a/eski", CardValue = "/ata0a/eski", Entered = "/ata0a/yeni" },
                new BootField { Label = "other (o)", OriginalValue = "x", CardValue = "x", Entered = BootChangeParser.ClearToken },
                new BootField { Label = "host inet (h)", OriginalValue = "10.0.0.1", CardValue = "10.0.0.1" },
            };
            BootRecord rec = BootRecordLogic.FromFields("Pencere", "bootChange", "10.0.0.5", fields, DateTime.Now);
            Check.Equal("/ata0a/yeni", rec.ValueOf("file name"), "gönderilen yeni değer");
            Check.Equal("", rec.ValueOf("other (o)"), "temizlenen alan boş");
            Check.Equal("10.0.0.1", rec.ValueOf("host inet (h)"), "değişmeyen alan");
        }

        [Test]
        public void Boot_KarsilastirmaDurumlari()
        {
            var rec = new BootRecord { Name = "x", Command = "bootChange" };
            rec.Fields.Add(new BootRecordField { Label = "boot device", Value = "fei0" });
            rec.Fields.Add(new BootRecordField { Label = "eski alan (z)", Value = "1" });
            rec.Fields.Add(new BootRecordField { Label = "inet on ethernet (e)", Value = "10.0.0.5" });
            var cur = new List<BootField>
            {
                new BootField { Label = "boot device", OriginalValue = "fei0", CardValue = "fei0" },
                new BootField { Label = "inet on ethernet (e)", OriginalValue = "10.0.0.7", CardValue = "10.0.0.7" },
                new BootField { Label = "yeni alan (y)", OriginalValue = "a", CardValue = "a" },
            };
            List<BootPlanRow> p = BootRecordLogic.Plan(rec, cur, true);
            Check.Equal(BootPlanKind.Same, p.First(r => r.Label == "boot device").Kind, "aynı");
            Check.Equal(BootPlanKind.Excluded, p.First(r => r.Label.StartsWith("inet on ethernet")).Kind, "başka kartta adres atlanır");
            Check.Equal(BootPlanKind.NotInRecord, p.First(r => r.Label.StartsWith("yeni alan")).Kind, "kayıtta yok");
            Check.Equal(BootPlanKind.MissingOnCard, p.First(r => r.Label.StartsWith("eski alan")).Kind, "kart sormuyor");
            Check.Equal(BootPlanKind.Change, BootRecordLogic.Plan(rec, cur, false).First(r => r.Label.StartsWith("inet on ethernet")).Kind,
                        "aynı kartta adres de uygulanır");
        }

        [Test]
        public void Boot_KayitlarYapilandirmadaSaklanir()
        {
            var card = new CardConfig { Ip = "10.0.0.5", BootCommand = "bootChange", BootRecords = new List<BootRecord>() };
            var rec = new BootRecord { Name = "Fabrika ayarı", Command = "bootChange", SourceIp = "10.0.0.5", Created = new DateTime(2026, 11, 17, 10, 0, 0) };
            rec.Fields.Add(new BootRecordField { Label = "file name", Value = "/ata0a/vxWorks" });
            rec.Fields.Add(new BootRecordField { Label = "other (o)", Value = "" });
            card.BootRecords.Add(rec);
            var xs = new XmlSerializer(typeof(CardConfig));
            var sw = new StringWriter();
            xs.Serialize(sw, card);
            var back = (CardConfig)xs.Deserialize(new StringReader(sw.ToString()));
            Check.Equal(1, back.BootRecords.Count, "kayıt sayısı");
            Check.Equal("Fabrika ayarı", back.BootRecords[0].Name, "ad");
            Check.Equal("/ata0a/vxWorks", back.BootRecords[0].ValueOf("file name"), "değer");
            Check.Equal("", back.BootRecords[0].ValueOf("other (o)"), "boş değer");
            Check.Equal("bootChange", back.BootCommand, "komut");
            Check.True(BootRecordLogic.ValidateName("fabrika AYARI", back.BootRecords, null) != null, "aynı ad ikinci kez verilemez");
        }
    }
}
