using System;
using System.Collections.Generic;
using System.Threading;

namespace Vision
{
    /// <summary>
    /// Tüm kartların bağlantısını periyodik izler. Kart başına iş parçacığı ya da döngü açmaz:
    /// tek bir zamanlayıcı her adımda kartların MonitorTick'ini çağırır; her kart kendi işini bloklamadan
    /// başlatır, böylece bir kartın yavaşlığı diğerlerini geciktirmez.
    ///
    /// Sağlık kontrolünün kendisi bağlantı katmanındadır (TCP keepalive, mevcut soket): kopma algılanınca
    /// oturum Recovering'e geçer; bu izleyici kartı ulaşılabilir olunca aynı oturum üzerinden yeniden bağlar.
    /// </summary>
    public sealed class ConnectionMonitor : IDisposable
    {
        private readonly Func<IList<CardSession>> _sessions;
        private readonly Timer _timer;
        private int _ticking;
        private volatile bool _disposed;

        public ConnectionMonitor(Func<IList<CardSession>> sessions, int tickMs)
        {
            _sessions = sessions;
            int period = Math.Max(200, tickMs);
            _timer = new Timer(Tick, null, period, period);
        }

        private void Tick(object state)
        {
            if (_disposed) return;
            if (Interlocked.Exchange(ref _ticking, 1) == 1) return;   // adımlar çakışmasın
            try
            {
                DateTime now = DateTime.UtcNow;
                foreach (CardSession s in _sessions())
                {
                    try { s.MonitorTick(now); }
                    catch (Exception ex) { ErrorText.LogTechnical("izleme " + s.Card.DisplayName, ex); }
                }
            }
            catch (Exception ex)
            {
                ErrorText.LogTechnical("izleme", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _ticking, 0);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}
