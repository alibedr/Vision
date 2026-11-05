using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TelnetCards
{
    /// <summary>"Bağlantı Kur / Bağlantıyı Kes" düğmesinin ne yapacağı.</summary>
    public sealed class ConnectionAction
    {
        public const string ConnectLabel = "Bağlantı Kur";
        public const string DisconnectLabel = "Bağlantıyı Kes";

        /// <summary>true: seçili kartların bağlantısı kesilecek; false: bağlı olmayanlara bağlanılacak.</summary>
        public bool Disconnect { get; set; }
        public bool Enabled { get; set; }
        public string Label { get { return Disconnect ? DisconnectLabel : ConnectLabel; } }
        public int Selected { get; set; }
        public int Live { get; set; }      // bağlı + bağlanıyor
    }

    /// <summary>
    /// Arayüzden bağımsız seçim kuralları. Kartın üç durumu birbirinden ayrıdır:
    /// yapılandırma (listede var mı), bağlantı (CardState) ve görünüm (çalışma alanında mı).
    /// Buradaki kararlar yalnızca bağlantı durumuna bakar; görünüm durumunu asla kullanmaz.
    /// </summary>
    public static class SelectionLogic
    {
        /// <summary>
        /// Seçili kartların hepsi bağlı (ya da bağlanıyor) ise düğme "Bağlantıyı Kes" olur;
        /// en az biri bağlı değilse "Bağlantı Kur" olur ve yalnız bağlı olmayanlara bağlanır.
        /// </summary>
        public static ConnectionAction DecideConnection(IList<CardState> selectedStates)
        {
            int live = 0;
            foreach (CardState s in selectedStates)
                if (IsLive(s)) live++;

            int n = selectedStates.Count;
            return new ConnectionAction
            {
                Selected = n,
                Live = live,
                Enabled = n > 0,
                Disconnect = n > 0 && live == n,
            };
        }

        public static bool IsLive(CardState s)
        {
            // Recovering: bağlantı koptu ve otomatik yeniden bağlanılıyor → kullanıcı açısından hâlâ "bağlanmak istenen" kart;
            // "Bağlantıyı Kes" ile yeniden bağlanma durdurulabilir.
            return s == CardState.Connected || s == CardState.Connecting || s == CardState.Recovering;
        }

        /// <summary>Aynı anda gösterilebilecek en fazla kart sayısı (uygulama sınırı; kullanıcı değiştiremez).</summary>
        public const int MaxVisibleCards = 12;

        /// <summary>
        /// Kartları EKLEME (çift tık, "Tümünü ekranda göster"): zaten açık olanlar sayılmaz ve yeniden açılmaz;
        /// yeni kartlar boş yer kadar (istek sırasıyla) açılır, kalanlar rejected'a düşer. Açık kartlara dokunulmaz.
        /// </summary>
        public static List<T> LimitAdditive<T>(IList<T> shown, IList<T> requested, int max, out List<T> rejected)
        {
            var cur = new HashSet<T>(shown);
            var open = new List<T>();
            rejected = new List<T>();
            int free = Math.Max(0, max - shown.Count);
            foreach (T x in requested)
            {
                if (cur.Contains(x) || open.Contains(x) || rejected.Contains(x)) continue;
                if (open.Count < free) open.Add(x);
                else rejected.Add(x);
            }
            return open;
        }

        /// <summary>
        /// "Seçilenleri Göster" sınırla: seçim max'ı aşarsa önce ZATEN EKRANDA olan seçili kartlar korunur,
        /// kalan yer seçim sırasıyla doldurulur. Dönüş: gösterilecek kartlar (seçim sırasıyla); rejected: gösterilemeyenler.
        /// </summary>
        public static List<T> LimitExact<T>(IList<T> shown, IList<T> requested, int max, out List<T> rejected)
        {
            var cur = new HashSet<T>(shown);
            var distinct = new List<T>();
            foreach (T x in requested) if (!distinct.Contains(x)) distinct.Add(x);

            var picked = new HashSet<T>();
            foreach (T x in distinct) if (cur.Contains(x) && picked.Count < max) picked.Add(x);
            foreach (T x in distinct) if (!cur.Contains(x) && picked.Count < max) picked.Add(x);

            rejected = distinct.Where(x => !picked.Contains(x)).ToList();
            return distinct.Where(picked.Contains).ToList();
        }

        /// <summary>
        /// "Seçilenleri Göster" için: ekranda olup seçili olmayanlar kapatılır,
        /// seçili olup ekranda olmayanlar açılır; sonuç sırası seçim sırasıdır.
        /// </summary>
        public static void DiffDisplay<T>(IList<T> shown, IList<T> selected, out List<T> toHide, out List<T> toShow)
        {
            var sel = new HashSet<T>(selected);
            var cur = new HashSet<T>(shown);
            toHide = new List<T>();
            toShow = new List<T>();
            foreach (T x in shown) if (!sel.Contains(x)) toHide.Add(x);
            foreach (T x in selected) if (!cur.Contains(x) && !toShow.Contains(x)) toShow.Add(x);
        }
    }

    /// <summary>Genel komut çubuğunun gönderim sonucu.</summary>
    public sealed class BroadcastResult
    {
        public BroadcastResult()
        {
            Sent = new List<CardSession>();
            Failed = new List<KeyValuePair<CardSession, string>>();
        }

        public List<CardSession> Sent { get; private set; }
        public List<KeyValuePair<CardSession, string>> Failed { get; private set; }
        public int Total { get { return Sent.Count + Failed.Count; } }
    }

    /// <summary>
    /// Aynı komutu birden çok karta gönderir. Her kart, kendi oturumunun mevcut gönderim yolunu
    /// (CardSession.SendCommandAsync → TelnetClient) kullanır; yeni bağlantı açılmaz.
    /// Gönderimler paralel ve birbirinden yalıtılmıştır: bir kartın hatası diğerlerini durdurmaz.
    /// </summary>
    public static class CommandBroadcaster
    {
        public static async Task<BroadcastResult> SendToAllAsync(IList<CardSession> targets, string command)
        {
            var result = new BroadcastResult();
            var tasks = targets.Distinct().Select(s => SendOneAsync(s, command)).ToList();
            KeyValuePair<CardSession, string>[] outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var o in outcomes)
            {
                if (o.Value == null) result.Sent.Add(o.Key);
                else result.Failed.Add(o);
            }
            return result;
        }

        // Value == null → başarılı; değilse hata nedeni
        private static async Task<KeyValuePair<CardSession, string>> SendOneAsync(CardSession s, string command)
        {
            if (s.State != CardState.Connected)
                return new KeyValuePair<CardSession, string>(s, "bağlı değil");
            try
            {
                await s.SendCommandAsync(command).ConfigureAwait(false);
                return new KeyValuePair<CardSession, string>(s, null);
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical(s.Card.DisplayName + " genel komut", ex);
                return new KeyValuePair<CardSession, string>(s, ErrorText.Describe(ex));
            }
        }
    }
}
