using System.Windows.Forms;

namespace Vision
{
    /// <summary>Uygulama turunun vurguladığı kart penceresi bölümleri (G36).</summary>
    public sealed partial class CardView
    {
        internal Control TourHeader { get { return _header; } }
        internal Control[] TourTitle { get { return new Control[] { _title, _subtitle }; } }
        internal Control TourUptime { get { return _uptime; } }
        internal Control[] TourRxTx { get { return new Control[] { _rx, _tx }; } }
        internal Control TourConnect { get { return _connectBtn; } }
        internal Control TourClear { get { return _clearBtn; } }
        internal Control TourMax { get { return _maxBtn; } }
        internal Control TourClose { get { return _closeBtn; } }
        internal Control TourStateStrip { get { return _stateStrip; } }
        internal Control TourOutput { get { return _outputHost; } }
        internal Control TourInput { get { return _field; } }
        internal Control TourSend { get { return _sendBtn; } }
        internal Control TourHistory { get { return _historyBtn; } }
        internal Control TourSnapshot { get { return _snapBtn; } }
        internal Control TourSentinel { get { return _sentinelBtn; } }
        internal Control TourQuick { get { return _quickBtn; } }
        internal Control TourReboot { get { return _rebootBtn; } }
        internal Control[] TourResponses { get { return new Control[] { _respSummary, _respToggle, _respClear }; } }
        internal Control[] TourActions { get { return new Control[] { _sentinelBtn, _quickBtn, _rebootBtn }; } }
    }
}
