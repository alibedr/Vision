using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vision
{
    /// <summary>
    /// Kullanıcının yazdığı tüm komutların kalıcı, ortak listesi. %APPDATA%\TelnetCards\KomutGecmisi.txt dosyasında
    /// (UTF-8, satır başına bir komut, eskiden yeniye) tutulur; config.xml ile aynı klasördedir, bu yüzden yeni bir
    /// Vision sürümü başka klasörden çalıştırılsa da geçmiş kaybolmaz. Liste ilk kullanımda dosyadan okunur,
    /// her değişiklikte (yeni komut, silme) hemen kaydedilir. Tüm kartlar ve genel komut çubuğu bu listeyi paylaşır.
    /// Yalnız arayüz iş parçacığından kullanılır.
    /// </summary>
    public static class CommandHistoryStore
    {
        public const string FileName = "KomutGecmisi.txt";

        private static List<string> _items;
        private static bool _saveErrorLogged;

        /// <summary>Kayıt dosyası (test için değiştirilebilir; ilk kullanımdan ÖNCE ayarlanmalı).</summary>
        public static string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.DataFolder, FileName);

        /// <summary>Ortak liste (eskiden yeniye). İlk erişimde dosyadan yüklenir.</summary>
        public static List<string> SharedItems
        {
            get
            {
                if (_items == null) _items = Load(FilePath);
                return _items;
            }
        }

        /// <summary>Dosyadaki komutları okur (boş satırlar ve tekrarlar atlanır, en yeni kopya kalır). Dosya yoksa boş liste.</summary>
        public static List<string> Load(string path)
        {
            var list = new List<string>();
            try
            {
                if (!File.Exists(path)) return list;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                // Sondan başa: aynı komut birden çok kez yazılmışsa en yeni konumu kalır
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string c = lines[i];
                    if (c.Length == 0 || !seen.Add(c)) continue;
                    list.Add(c);
                    if (list.Count >= CommandHistory.MaxItems) break;
                }
                list.Reverse();
            }
            catch (Exception ex)
            {
                // Okunamayan dosya: yedeklenir (üzerine yazılınca eski komutlar tamamen kaybolmasın), boş geçmişle devam edilir
                ErrorText.LogTechnical("komut geçmişi okuma " + path, ex);
                try { File.Copy(path, path + ".bozuk", true); } catch { }
            }
            return list;
        }

        /// <summary>Ortak listeyi dosyaya yazar (önce geçici dosyaya, sonra yerine). Hata uygulamayı durdurmaz.</summary>
        public static void Save()
        {
            if (_items == null) return;
            try
            {
                Write(FilePath, _items);
            }
            catch (Exception ex)
            {
                if (!_saveErrorLogged) ErrorText.LogTechnical("komut geçmişi kaydetme " + FilePath, ex);
                _saveErrorLogged = true;   // her komutta aynı hatayı tekrar tekrar yazma
            }
        }

        public static void Write(string path, IList<string> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            var sb = new StringBuilder();
            foreach (string c in items)
            {
                if (string.IsNullOrEmpty(c) || c.IndexOf('\n') >= 0 || c.IndexOf('\r') >= 0) continue;
                sb.Append(c).Append("\r\n");
            }
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
