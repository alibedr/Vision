using System.Linq;
using System.Windows.Forms;

namespace Vision
{
    // Uygulama turunun (G36) vurguladığı denetimler. Yalnız okunur erişim; hiçbir davranışı değiştirmez.

    public sealed partial class AddCardsDialog
    {
        /// <summary>Şu an açık (görünen) Kart Ekle penceresi; yoksa null.</summary>
        internal static AddCardsDialog TourOpen
        {
            get { return Application.OpenForms.OfType<AddCardsDialog>().FirstOrDefault(d => !d.IsDisposed && d.Visible); }
        }

        internal Control TourGroupName { get { return _groupName; } }
        internal Control TourPort { get { return _port; } }
        internal Control TourStartIp { get { return _startIp; } }
        internal Control TourCount { get { return _count; } }
        internal Control TourFill { get { return _fillBtn; } }
        internal Control[] TourIpList { get { return new Control[] { _ipHeader, _ipList }; } }
        internal Control[] TourButtons { get { return new Control[] { _okBtn, _cancelBtn }; } }
    }

    public sealed partial class BootChangeDialog
    {
        /// <summary>Kartın açık (görünen) boot ayarları penceresi; yoksa null.</summary>
        internal static BootChangeDialog TourOpenFor(CardSession session)
        {
            BootChangeDialog d;
            return session != null && OpenDialogs.TryGetValue(session, out d) && !d.IsDisposed && d.Visible ? d : null;
        }

        internal Control[] TourStatus { get { return new Control[] { _title, _status }; } }
        internal Control[] TourCurrent { get { return new Control[] { _currentCaption, _currentLabel, _currentValue }; } }
        internal Control[] TourInput { get { return new Control[] { _input, _send }; } }
        internal Control[] TourActions { get { return new Control[] { _keep, _edit, _clear, _prev, _skipRest, _quit }; } }
        internal Control TourList { get { return _list; } }
        internal Control TourLogs { get { return _logBar; } }
        internal Control TourClose { get { return _close; } }
    }

    public sealed partial class FileExplorerView
    {
        internal Control[] TourCardBar { get { return new Control[] { _cardCombo, _connectBtn }; } }
        internal Control TourBack { get { return _backBtn; } }
        internal Control TourRefresh { get { return _refreshBtn; } }
        internal Control TourNewFolder { get { return _newFolderBtn; } }
        internal Control TourUpload { get { return _uploadBtn; } }
        internal Control[] TourFtp { get { return new Control[] { _ftpBtn, _ftpCopiesBtn }; } }
        internal Control TourRaw { get { return _rawToggle; } }
        internal ContextMenuStrip TourItemMenu { get { return _itemMenu; } }
    }

    public sealed partial class CommandLibraryView
    {
        internal Control TourSearch { get { return _search.Parent ?? _search; } }
        internal Control TourTree { get { return _list; } }
        internal Control TourDetail { get { return _detail; } }
        internal Control TourTransfer { get { return _transferBtn; } }
    }

    public sealed partial class SentinelView
    {
        internal Control TourTop { get { return _top; } }
        internal Control TourCards { get { return _left; } }
        internal Control TourBar { get { return _pageBar; } }
        internal Control TourPage { get { return _pageHost; } }
    }
}
