using System.Windows.Forms;

namespace Vision
{
    /// <summary>Uygulama turunun vurguladığı kart penceresi bölümleri (G36).</summary>
    public sealed partial class CardView
    {
        internal Control TourHeader { get { return _header; } }
        internal Control TourOutput { get { return _outputHost; } }
        internal Control TourInput { get { return _field; } }
        internal Control TourHistory { get { return _historyBtn; } }
        internal Control TourSnapshot { get { return _snapBtn; } }
        internal Control[] TourActions { get { return new Control[] { _sentinelBtn, _quickBtn, _rebootBtn }; } }
    }
}
