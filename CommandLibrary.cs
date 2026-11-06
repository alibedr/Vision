using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Vision
{
    /// <summary>Kütüphanedeki bir komutun açıklaması.</summary>
    public sealed class CommandDoc
    {
        /// <summary>Komutun adı (ör. "devs"). Kütüphanede benzersizdir (büyük/küçük harf duyarsız).</summary>
        [XmlAttribute("Ad")] public string Name { get; set; }

        /// <summary>Son değişiklik zamanı ("yyyy-MM-dd HH:mm").</summary>
        [XmlAttribute("Degistirilme")] public string Modified { get; set; }

        [XmlElement("Aciklama")] public string Description { get; set; }

        /// <summary>Komutun yazılışı / örnek kullanımı (isteğe bağlı).</summary>
        [XmlElement("Yazilis")] public string Usage { get; set; }

        /// <summary>Örnek çıktı (isteğe bağlı).</summary>
        [XmlElement("OrnekCikti")] public string ExampleOutput { get; set; }

        /// <summary>
        /// Komutun bağlı olduğu modülün adı; "" = modülsüz. Dosyada ayrı alan olarak değil, komutun hangi
        /// &lt;Modul&gt; öğesinin içinde durduğuyla saklanır. Upsert'te null: düzenlenen komutun modülü korunur.
        /// </summary>
        [XmlIgnore] public string Module { get; set; }

        public CommandDoc Clone()
        {
            return (CommandDoc)MemberwiseClone();
        }

        /// <summary>Listede gösterilecek kısa açıklama: açıklamanın ilk dolu satırı.</summary>
        public string Summary
        {
            get
            {
                foreach (string line in (Description ?? "").Split('\n'))
                    if (line.Trim().Length > 0) return line.Trim();
                return "";
            }
        }

        public bool SameContent(CommandDoc other)
        {
            return other != null && Norm(Name) == Norm(other.Name) && Norm(Description) == Norm(other.Description) &&
                   Norm(Usage) == Norm(other.Usage) && Norm(ExampleOutput) == Norm(other.ExampleOutput) &&
                   (Module == null || CommandLibrary.SameName(Module, other.Module ?? ""));
        }

        private static string Norm(string s)
        {
            return CommandLibrary.NormalizeText(s);
        }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Komutları gruplayan modül (ör. "Task komutları", "Bellek ve ağ").</summary>
    public sealed class CommandModule
    {
        public CommandModule()
        {
            Commands = new List<CommandDoc>();
        }

        /// <summary>Modülün adı; kütüphanede benzersizdir (büyük/küçük harf duyarsız).</summary>
        [XmlAttribute("Ad")] public string Name { get; set; }
        [XmlAttribute("Degistirilme")] public string Modified { get; set; }
        [XmlElement("Aciklama")] public string Description { get; set; }

        /// <summary>Yalnız dosyaya yazma / dosyadan okuma sırasında kullanılır; bellekte komutlar düz listede tutulur.</summary>
        [XmlElement("Komut")] public List<CommandDoc> Commands { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    [XmlRoot("KomutKutuphanesi")]
    public sealed class CommandLibraryFile
    {
        public CommandLibraryFile()
        {
            Version = 2;
            Modules = new List<CommandModule>();
            Commands = new List<CommandDoc>();
        }

        [XmlAttribute("Surum")] public int Version { get; set; }
        /// <summary>Modüller ve içlerindeki komutlar.</summary>
        [XmlElement("Modul")] public List<CommandModule> Modules { get; set; }
        /// <summary>Modülsüz komutlar (Sürüm 1 dosyalarındaki komutların hepsi buradadır).</summary>
        [XmlElement("Komut")] public List<CommandDoc> Commands { get; set; }
    }

    /// <summary>
    /// Komut Kütüphanesi: exe'nin yanındaki Komutlar\Komutlar.xml dosyasında saklanır (uygulamayla birlikte gelir).
    /// Komutlar modüllerde gruplanabilir (dosyada &lt;Modul&gt; öğesinin içinde); modülsüz komutlar kökte durur.
    /// Sürüm 1 dosyaları (modülsüz) aynen okunur.
    /// Arayüzden bağımsızdır (test edilebilir). Her değişiklik dosyaya hemen yazılır; önceki sürüm .yedek olarak tutulur.
    /// Kayıt başarısız olursa bellekteki liste de değişmez (dosyayla tutarlı kalır).
    /// </summary>
    public sealed class CommandLibrary
    {
        public const string FolderName = "Komutlar";
        public const string FileName = "Komutlar.xml";
        public const int MaxNameLength = 80;
        public const int MaxModuleNameLength = 60;
        /// <summary>Modülü olmayan komutların arayüzdeki grup adı.</summary>
        public const string NoModuleName = "Modülsüz";

        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");
        private List<CommandDoc> _items = new List<CommandDoc>();
        private List<CommandModule> _modules = new List<CommandModule>();

        public CommandLibrary(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Exe'ye gömülü, SALT OKUNUR kütüphane: içerik yalnız Vision'ın yeni sürümleriyle değişir; uygulamadan
        /// ekleme / düzenleme / silme / taşıma yapılamaz ve diskteki bir dosya okunmaz ya da yazılmaz.
        /// </summary>
        public static CommandLibrary Embedded()
        {
            return new CommandLibrary(null) { IsReadOnly = true };
        }

        /// <summary>Gömülü, değiştirilemez kütüphane mi?</summary>
        public bool IsReadOnly { get; private set; }

        public static string DefaultPath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName, FileName); }
        }

        public string Path { get; private set; }
        public IList<CommandDoc> Items { get { return _items.AsReadOnly(); } }
        /// <summary>Modüller (alfabetik).</summary>
        public IList<CommandModule> Modules { get { return _modules.AsReadOnly(); } }
        /// <summary>Son yüklemede dosyanın değiştirilme zamanı (dışarıdan düzenleme algılamak için).</summary>
        public DateTime LoadedWriteTimeUtc { get; private set; }
        /// <summary>Son yükleme sorunu (bozuk dosya vb.); yoksa null.</summary>
        public string LoadError { get; private set; }

        // ---------- Yükle / kaydet ----------

        public void Load()
        {
            LoadError = null;
            _items = new List<CommandDoc>();
            _modules = new List<CommandModule>();
            LoadedWriteTimeUtc = DateTime.MinValue;
            if (IsReadOnly)
            {
                CommandLibraryFile embedded = LoadBundled();
                if (embedded == null) { LoadError = "Gömülü komut kütüphanesi okunamadı."; return; }
                foreach (CommandModule m in embedded.Modules ?? new List<CommandModule>())
                {
                    string mn = (m.Name ?? "").Trim();
                    if (mn.Length == 0 || SameName(mn, NoModuleName)) mn = "";
                    CommandModule target = mn.Length == 0 ? null : _modules.FirstOrDefault(x => SameName(x.Name, mn));
                    if (mn.Length > 0 && target == null)
                    {
                        target = new CommandModule { Name = mn, Description = NormalizeText(m.Description), Modified = m.Modified };
                        _modules.Add(target);
                    }
                    foreach (CommandDoc d in m.Commands ?? new List<CommandDoc>()) AddLoaded(d, target != null ? target.Name : "");
                }
                foreach (CommandDoc d in embedded.Commands ?? new List<CommandDoc>()) AddLoaded(d, "");
                foreach (CommandModule m in _modules) m.Commands = new List<CommandDoc>();
                Sort(_items);
                SortModules(_modules);
                return;
            }
            if (!File.Exists(Path)) return;
            try
            {
                LoadedWriteTimeUtc = File.GetLastWriteTimeUtc(Path);
                CommandLibraryFile f;
                using (var fs = File.OpenRead(Path))
                    f = (CommandLibraryFile)new XmlSerializer(typeof(CommandLibraryFile)).Deserialize(fs);
                foreach (CommandModule m in f.Modules ?? new List<CommandModule>())
                {
                    string mn = (m.Name ?? "").Trim();
                    if (mn.Length == 0 || SameName(mn, NoModuleName)) mn = "";      // adsız modülün komutları modülsüz sayılır
                    CommandModule target = mn.Length == 0 ? null : _modules.FirstOrDefault(x => SameName(x.Name, mn));
                    if (mn.Length > 0 && target == null)
                    {
                        target = new CommandModule { Name = mn, Description = NormalizeText(m.Description), Modified = m.Modified };
                        _modules.Add(target);
                    }
                    foreach (CommandDoc d in m.Commands ?? new List<CommandDoc>()) AddLoaded(d, target != null ? target.Name : "");
                }
                foreach (CommandDoc d in f.Commands ?? new List<CommandDoc>()) AddLoaded(d, "");
                foreach (CommandModule m in _modules) m.Commands = new List<CommandDoc>();
                Sort(_items);
                SortModules(_modules);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("komut kütüphanesi yükleme", ex);
                try { File.Copy(Path, Path + ".bozuk", true); } catch { }
                LoadError = "Komut dosyası okunamadı (bozuk olabilir). Bir kopyası \"" + FileName + ".bozuk\" olarak saklandı.";
                _items = new List<CommandDoc>();
                _modules = new List<CommandModule>();
            }
        }

        private void AddLoaded(CommandDoc d, string module)
        {
            if (string.IsNullOrWhiteSpace(d.Name)) return;
            d.Name = d.Name.Trim();
            d.Description = NormalizeText(d.Description);
            d.Usage = NormalizeText(d.Usage);
            d.ExampleOutput = NormalizeText(d.ExampleOutput);
            d.Module = module;
            if (_items.Any(x => SameName(x.Name, d.Name))) return;   // elle düzenlemede çift kayıt
            _items.Add(d);
        }

        /// <summary>Dosya, yüklendikten sonra dışarıdan (metin düzenleyiciyle) değiştirildi mi?</summary>
        public bool ChangedOnDisk()
        {
            if (IsReadOnly) return false;
            try
            {
                return File.Exists(Path) && File.GetLastWriteTimeUtc(Path) != LoadedWriteTimeUtc;
            }
            catch
            {
                return false;
            }
        }

        private void Save(List<CommandDoc> items, List<CommandModule> modules)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var file = new CommandLibraryFile { Commands = items.Where(d => string.IsNullOrEmpty(d.Module)).ToList() };
            foreach (CommandModule m in modules)
                file.Modules.Add(new CommandModule
                {
                    Name = m.Name,
                    Description = string.IsNullOrEmpty(m.Description) ? null : m.Description,
                    Modified = m.Modified,
                    Commands = items.Where(d => SameName(d.Module, m.Name)).ToList(),
                });
            string tmp = Path + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(CommandLibraryFile)).Serialize(fs, file);
            if (File.Exists(Path))
            {
                File.Copy(Path, Path + ".yedek", true);   // bir önceki sürüm
                File.Delete(Path);
            }
            File.Move(tmp, Path);
            LoadedWriteTimeUtc = File.GetLastWriteTimeUtc(Path);
        }

        // ---------- Doğrulama ----------

        /// <summary>Taslak geçerliyse null, değilse Türkçe hata. original: düzenlenen kayıt (yeni ise null).</summary>
        public string Validate(CommandDoc draft, CommandDoc original)
        {
            string name = (draft.Name ?? "").Trim();
            if (name.Length == 0) return "Komut adı boş olamaz.";
            if (name.Length > MaxNameLength) return "Komut adı en fazla " + MaxNameLength + " karakter olabilir.";
            if (name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0) return "Komut adı tek satır olmalı.";
            CommandDoc clash = _items.FirstOrDefault(x => x != original && SameName(x.Name, name));
            if (clash != null)
                return "\"" + name + "\" adında bir komut zaten var" +
                       (string.IsNullOrEmpty(clash.Module) ? "." : " (\"" + clash.Module + "\" modülünde).");
            if (!string.IsNullOrEmpty(draft.Module) && FindModule(draft.Module) == null)
                return "\"" + draft.Module + "\" modülü bulunamadı.";
            return null;
        }

        /// <summary>Modül adı geçerliyse null, değilse Türkçe hata. original: yeniden adlandırılan modül (yeni ise null).</summary>
        public string ValidateModule(string name, CommandModule original)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "Modül adı boş olamaz.";
            if (name.Length > MaxModuleNameLength) return "Modül adı en fazla " + MaxModuleNameLength + " karakter olabilir.";
            if (name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0) return "Modül adı tek satır olmalı.";
            if (SameName(name, NoModuleName)) return "\"" + NoModuleName + "\" adı modülü olmayan komutlar için ayrılmıştır.";
            if (_modules.Any(x => x != original && SameName(x.Name, name))) return "\"" + name + "\" adında bir modül zaten var.";
            return null;
        }

        public CommandModule FindModule(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _modules.FirstOrDefault(m => SameName(m.Name, name));
        }

        /// <summary>Modüldeki komutlar (alfabetik). module null / "": modülsüz komutlar.</summary>
        public List<CommandDoc> CommandsIn(string module)
        {
            return _items.Where(d => string.IsNullOrEmpty(module) ? string.IsNullOrEmpty(d.Module) : SameName(d.Module, module)).ToList();
        }

        // ---------- Değişiklikler ----------

        /// <summary>
        /// Yeni komut ekler (original == null) ya da var olanı günceller ve dosyaya yazar.
        /// Hata varsa (doğrulama ya da yazma) mesaj döner ve hiçbir şey değişmez. Başarılıysa kaydedilen kayıt döner.
        /// </summary>
        public CommandDoc Upsert(CommandDoc original, CommandDoc edited, out string error)
        {
            error = Validate(edited, original);
            if (error != null) return null;
            string module = edited.Module != null ? edited.Module.Trim() : original != null ? original.Module ?? "" : "";
            CommandModule mod = FindModule(module);
            module = mod != null ? mod.Name : "";

            var saved = new CommandDoc
            {
                Name = edited.Name.Trim(),
                Description = NormalizeText(edited.Description),
                Usage = NormalizeText(edited.Usage),
                ExampleOutput = NormalizeText(edited.ExampleOutput),
                Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Module = module,
            };
            var next = new List<CommandDoc>(_items);
            if (original != null)
            {
                int i = next.IndexOf(original);
                if (i < 0) { error = "Düzenlenen komut kütüphanede bulunamadı (dosya dışarıdan değişmiş olabilir)."; return null; }
                next[i] = saved;
            }
            else next.Add(saved);
            Sort(next);

            if (!TrySave(next, _modules, out error)) return null;
            _items = next;
            return saved;
        }

        public bool Delete(CommandDoc doc, out string error)
        {
            error = null;
            var next = new List<CommandDoc>(_items);
            if (!next.Remove(doc)) { error = "Komut kütüphanede bulunamadı."; return false; }
            if (!TrySave(next, _modules, out error)) return false;
            _items = next;
            return true;
        }

        // ---------- Modüller ----------

        /// <summary>
        /// Yeni modül ekler (original == null) ya da adını / açıklamasını değiştirir. Ad değişirse içindeki komutlar
        /// yeni ada taşınır. Hata varsa mesaj döner ve hiçbir şey değişmez.
        /// </summary>
        public CommandModule UpsertModule(CommandModule original, string name, string description, out string error)
        {
            error = ValidateModule(name, original);
            if (error != null) return null;
            if (original != null && !_modules.Contains(original)) { error = "Modül kütüphanede bulunamadı (dosya dışarıdan değişmiş olabilir)."; return null; }

            var saved = new CommandModule
            {
                Name = name.Trim(),
                Description = NormalizeText(description),
                Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            var modules = new List<CommandModule>(_modules);
            List<CommandDoc> items = _items;
            if (original != null)
            {
                modules[modules.IndexOf(original)] = saved;
                items = _items.Select(d => SameName(d.Module, original.Name) ? WithModule(d, saved.Name) : d).ToList();
            }
            else modules.Add(saved);
            SortModules(modules);

            if (!TrySave(items, modules, out error)) return null;
            _modules = modules;
            _items = items;
            return saved;
        }

        /// <summary>
        /// Modülü siler. deleteCommands: true ise içindeki komutlar da silinir, false ise "Modülsüz"e taşınır.
        /// </summary>
        public bool DeleteModule(CommandModule module, bool deleteCommands, out string error)
        {
            error = null;
            if (module == null || !_modules.Contains(module)) { error = "Modül kütüphanede bulunamadı."; return false; }
            var modules = new List<CommandModule>(_modules);
            modules.Remove(module);
            List<CommandDoc> items = deleteCommands
                ? _items.Where(d => !SameName(d.Module, module.Name)).ToList()
                : _items.Select(d => SameName(d.Module, module.Name) ? WithModule(d, "") : d).ToList();
            if (!TrySave(items, modules, out error)) return false;
            _modules = modules;
            _items = items;
            return true;
        }

        /// <summary>
        /// Komutları başka bir modüle taşır (module null / "": modülsüz). Taşınan komutların yeni kayıtlarını döndürür;
        /// hata varsa null döner ve hiçbir şey değişmez.
        /// </summary>
        public List<CommandDoc> MoveCommands(IList<CommandDoc> docs, string module, out string error)
        {
            error = null;
            CommandModule mod = FindModule(module);
            if (!string.IsNullOrEmpty(module) && mod == null) { error = "\"" + module + "\" modülü bulunamadı."; return null; }
            string target = mod != null ? mod.Name : "";
            if (docs == null || docs.Count == 0) { error = "Taşınacak komut seçilmedi."; return null; }
            if (docs.Any(d => !_items.Contains(d))) { error = "Komut kütüphanede bulunamadı (dosya dışarıdan değişmiş olabilir)."; return null; }

            var moved = new List<CommandDoc>();
            var items = _items.Select(d =>
            {
                if (!docs.Contains(d) || SameName(d.Module ?? "", target)) return d;
                CommandDoc n = WithModule(d, target);
                moved.Add(n);
                return n;
            }).ToList();
            if (moved.Count == 0) return docs.ToList();   // zaten oradalar
            if (!TrySave(items, _modules, out error)) return null;
            _items = items;
            return items.Where(d => docs.Any(o => SameName(o.Name, d.Name))).ToList();
        }

        private static CommandDoc WithModule(CommandDoc d, string module)
        {
            CommandDoc n = d.Clone();
            n.Module = module;
            return n;
        }

        private bool TrySave(List<CommandDoc> next, List<CommandModule> modules, out string error)
        {
            error = null;
            if (IsReadOnly)
            {
                error = "Komut kütüphanesi Vision'a gömülüdür ve değiştirilemez; yeni komutlar sonraki sürümlerle gelir.";
                return false;
            }
            try
            {
                Save(next, modules);
                return true;
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("komut kütüphanesi kaydı", ex);
                error = "Komut dosyası kaydedilemedi (" + Path + "). Klasöre yazma izni olduğundan emin olun.";
                return false;
            }
        }

        // ---------- Hazır komutlar ----------

        /// <summary>Exe'ye gömülü hazır komut kütüphanesinin (VxWorks 6.4 komutları) kaynak adı.</summary>
        public const string BundledResourceName = "Komutlar.Varsayilan.xml";

        /// <summary>Kütüphanede hiç komut ve modül yok mu (ilk açılışta hazır komutlarla doldurmak için)?</summary>
        public bool IsEmpty { get { return _items.Count == 0 && _modules.Count == 0; } }

        /// <summary>Exe'ye gömülü hazır kütüphaneyi okur; okunamazsa null.</summary>
        public static CommandLibraryFile LoadBundled()
        {
            try
            {
                using (Stream st = typeof(CommandLibrary).Assembly.GetManifestResourceStream(BundledResourceName))
                {
                    if (st == null) return null;
                    return (CommandLibraryFile)new XmlSerializer(typeof(CommandLibraryFile)).Deserialize(st);
                }
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("hazır komut kütüphanesi", ex);
                return null;
            }
        }

        /// <summary>
        /// Verilen kütüphanedeki EKSİK modülleri ve komutları ekler; aynı adlı mevcut komut / modül hiç değiştirilmez
        /// (kullanıcının düzenlemeleri korunur). Komut, kaynağındaki modüle eklenir (modül yoksa oluşturulur).
        /// Eklenen komut sayısını döndürür; kayıt hatasında -1 döner ve hiçbir şey değişmez.
        /// </summary>
        public int ImportMissing(CommandLibraryFile source, out string error)
        {
            error = null;
            if (source == null) { error = "Hazır komutlar okunamadı."; return -1; }
            var modules = new List<CommandModule>(_modules);
            var items = new List<CommandDoc>(_items);
            int added = 0;
            bool modulesChanged = false;

            Action<CommandDoc, string> addDoc = (d, module) =>
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Name)) return;
                string name = d.Name.Trim();
                if (items.Any(x => SameName(x.Name, name))) return;          // mevcut komut korunur
                items.Add(new CommandDoc
                {
                    Name = name,
                    Description = NormalizeText(d.Description),
                    Usage = NormalizeText(d.Usage),
                    ExampleOutput = NormalizeText(d.ExampleOutput),
                    Modified = d.Modified,
                    Module = module,
                });
                added++;
            };

            foreach (CommandModule m in source.Modules ?? new List<CommandModule>())
            {
                string mn = (m.Name ?? "").Trim();
                string target = "";
                if (mn.Length > 0 && !SameName(mn, NoModuleName))
                {
                    CommandModule existing = modules.FirstOrDefault(x => SameName(x.Name, mn));
                    if (existing == null)
                    {
                        existing = new CommandModule { Name = mn, Description = NormalizeText(m.Description), Modified = m.Modified };
                        modules.Add(existing);
                        modulesChanged = true;
                    }
                    target = existing.Name;
                }
                foreach (CommandDoc d in m.Commands ?? new List<CommandDoc>()) addDoc(d, target);
            }
            foreach (CommandDoc d in source.Commands ?? new List<CommandDoc>()) addDoc(d, "");

            if (added == 0 && !modulesChanged) return 0;
            Sort(items);
            SortModules(modules);
            if (!TrySave(items, modules, out error)) return -1;
            _items = items;
            _modules = modules;
            return added;
        }

        // ---------- Karta aktarma ----------

        /// <summary>
        /// Komutun karta aktarılacak tek satırı: seçili metin varsa onun ilk dolu satırı, yoksa yazılışın ilk dolu
        /// satırı, o da yoksa komutun adı. Satır sonundaki " // açıklama" kısmı atılır.
        /// </summary>
        public static string TransferText(CommandDoc doc, string selection)
        {
            string line = FirstLine(selection);
            if (line == null && doc != null) line = FirstLine(doc.Usage);
            if (line == null && doc != null) line = (doc.Name ?? "").Trim();
            return line ?? "";
        }

        private static string FirstLine(string text)
        {
            foreach (string raw in NormalizeText(text).Split('\n'))
            {
                string l = StripComment(raw).Trim();
                if (l.Length > 0) return l;
            }
            return null;
        }

        /// <summary>"komut   // açıklama" → "komut". Yalnız boşluktan sonra gelen // açıklama sayılır ("host://" bozulmaz).</summary>
        public static string StripComment(string line)
        {
            if (line == null) return "";
            for (int i = 1; i + 1 < line.Length; i++)
                if (line[i] == '/' && line[i + 1] == '/' && char.IsWhiteSpace(line[i - 1]) && !InQuotes(line, i))
                    return line.Substring(0, i).TrimEnd();
            return line;
        }

        private static bool InQuotes(string line, int index)
        {
            bool q = false;
            for (int i = 0; i < index; i++) if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) q = !q;
            return q;
        }

        // ---------- Arama ----------

        /// <summary>
        /// Ad, açıklama, yazılış ve örnek çıktıda arar (Türkçe büyük/küçük harf duyarsız).
        /// Sıra: adı aramayla başlayanlar, adında geçenler, sonra yalnız metninde geçenler; her grup kendi içinde alfabetik.
        /// </summary>
        public List<CommandDoc> Filter(string query)
        {
            string q = (query ?? "").Trim();
            if (q.Length == 0) return new List<CommandDoc>(_items);
            CompareInfo ci = Turkish.CompareInfo;
            const CompareOptions opt = CompareOptions.IgnoreCase;
            Func<string, bool> has = t => !string.IsNullOrEmpty(t) && ci.IndexOf(t, q, opt) >= 0;
            return _items
                .Where(d => has(d.Name) || has(d.Description) || has(d.Usage) || has(d.ExampleOutput) || has(d.Module))
                .OrderBy(d => ci.IsPrefix(d.Name ?? "", q, opt) ? 0 : has(d.Name) ? 1
                            : has(d.Description) || has(d.Usage) || has(d.ExampleOutput) ? 2 : 3)
                .ThenBy(d => d.Name, StringComparer.Create(Turkish, true))
                .ToList();
        }

        // ---------- Yardımcılar ----------

        public static bool SameName(string a, string b)
        {
            return string.Compare((a ?? "").Trim(), (b ?? "").Trim(), Turkish, CompareOptions.IgnoreCase) == 0;
        }

        /// <summary>Satır sonlarını '\n'e indirger, sondaki boş satırları kaldırır (kaydedilen biçim).</summary>
        public static string NormalizeText(string s)
        {
            if (s == null) return "";
            return s.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n', ' ', '\t');
        }

        /// <summary>Metin kutusunda gösterim için Windows satır sonları.</summary>
        public static string ToTextBox(string s)
        {
            return NormalizeText(s).Replace("\n", "\r\n");
        }

        private static void SortModules(List<CommandModule> list)
        {
            list.Sort((x, y) => string.Compare(x.Name, y.Name, Turkish, CompareOptions.IgnoreCase));
        }

        private static void Sort(List<CommandDoc> list)
        {
            list.Sort((x, y) => string.Compare(x.Name, y.Name, Turkish, CompareOptions.IgnoreCase));
        }
    }
}
