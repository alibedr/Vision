using System;
using System.Collections.Generic;
using System.Text;

namespace Vision
{
    /// <summary>Seri penceresinin iki görünümü (1.10): akan çıktı (günlük) ve tam ekran terminal (BIOS / boot menüsü).</summary>
    public enum SerialViewMode { Log, Screen }

    /// <summary>
    /// Seri kanal için VT100 / ANSI terminal ekranı (1.10, TeraTerm'deki gibi). Kartın BIOS / boot manager gibi tam ekran
    /// çizdiği gösterimler "şuraya git, ekranı temizle, ters renk yaz" komutlarıyla gelir; günlük görünümü bunları atıp
    /// yazıları uç uca eklediği için ekran sağa kayıyor, menü görünmüyordu. Bu sınıf 80×25'lik bir ekran tutup
    /// komutları uygular; görünüm (SerialScreenView) bunu çizer.
    ///
    /// Aynı akış hem bu ekrana hem günlüğe gider. Kart tam ekran çizmeye başlayınca (ekranı temizleme, alternatif ekran
    /// ya da art arda konumlama) <see cref="Mode"/> Screen olur ve o sırada gelen çizim günlüğe gitmez (yerine tek bir
    /// bilgi satırı); kart yeniden akan yazıya dönünce (ekranın bir boyu kadar satır, konumlama olmadan kayarsa) Log'a
    /// döner ve o akan kısım günlüğe aktarılır. Saf sınıf: Windows Forms kullanmaz, testlenebilir.
    /// </summary>
    public sealed class VtScreen
    {
        public const int DefaultCols = 80, DefaultRows = 25;

        /// <summary>Bir hücre: karakter ve görünüm (renkler 0–15 ANSI; -1 = varsayılan).</summary>
        public struct Cell
        {
            public char Ch;
            public sbyte Fg, Bg;
            public bool Bold, Underline, Reverse;
        }

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int CursorRow { get; private set; }
        public int CursorCol { get; private set; }
        public bool CursorVisible { get; private set; }
        /// <summary>DECCKM: oklar "ESC O A" biçiminde gitmeli (kart istediyse).</summary>
        public bool AppCursorKeys { get; private set; }
        public SerialViewMode Mode { get; private set; }
        /// <summary>Ekran her değiştiğinde artar (görünüm yeniden çizmek için bakar).</summary>
        public int Version { get; private set; }

        private Cell[][] _cells;
        private Cell _pen;                       // yazılacak karakterin görünümü
        private int _top, _bottom;               // kaydırma bölgesi (satır, dahil)
        private bool _wrapPending, _autoWrap = true;
        private int _savedRow, _savedCol;
        private Cell _savedPen;
        private bool _g0Graphics, _g1Graphics, _shiftOut;

        // Ayrıştırıcı
        private enum State { Ground, Esc, Csi, Charset, Osc, OscEsc }
        private State _state;
        private readonly StringBuilder _params = new StringBuilder();
        private char _private;                   // CSI ? / > / =
        private char _charsetTarget;
        private bool _inSystem;

        // Görünüm kararı
        private int _positioning;                // son konumlama komutlarından bu yana sayaç (kısa süreli)
        private DateTime _positioningSince;
        private int _plainScrolls;               // Screen modunda son konumlamadan beri gelen satır sonu sayısı
        private readonly StringBuilder _sinceDraw = new StringBuilder();   // Screen modunda son çizimden beri gelen ham akış
        // Günlüğe dönerken kesintisizlik (1.10): BIOS ekranının son hâli günlüğe metin olarak eklenir, altına akan satırlar
        private string _flowSnapshot;            // akış başladığında (son çizimden sonraki ilk satır sonunda) ekran
        private int _flowSnapshotAt;             // o anda _sinceDraw'ın uzunluğu (ekranda zaten görünen kısım)
        private string _lastClearedScreen;       // ekran temizlenmeden hemen önceki dolu ekran (menüden çıkınca BIOS temizler)
        private const int SinceDrawLimit = 64 * 1024;
        private readonly Func<DateTime> _now;

        /// <summary>Kartın sorduğu (DSR / DA) ve cevaplanması gereken diziler; görünüm karta gönderir.</summary>
        public readonly Queue<string> Responses = new Queue<string>();

        public VtScreen(int cols = DefaultCols, int rows = DefaultRows, Func<DateTime> now = null)
        {
            _now = now ?? (() => DateTime.UtcNow);
            Cols = cols;
            Rows = rows;
            Reset();
        }

        public Cell this[int row, int col] { get { return _cells[row][col]; } }

        private void Reset()
        {
            _cells = new Cell[Rows][];
            _pen = Blank();
            for (int r = 0; r < Rows; r++) _cells[r] = NewRow();
            _top = 0;
            _bottom = Rows - 1;
            CursorRow = CursorCol = 0;
            CursorVisible = true;
            AppCursorKeys = false;
            _autoWrap = true;
            _wrapPending = false;
            _g0Graphics = _g1Graphics = _shiftOut = false;
            Version++;
        }

        private static Cell Blank() { return new Cell { Ch = ' ', Fg = -1, Bg = -1 }; }

        private Cell[] NewRow()
        {
            var row = new Cell[Cols];
            Cell b = Blank();
            b.Bg = _pen.Bg;              // silinen alan geçerli zemin rengini alır (VT davranışı)
            for (int c = 0; c < Cols; c++) row[c] = b;
            return row;
        }

        // =====================================================================
        // Giriş
        // =====================================================================

        /// <summary>
        /// Karttan gelen metni işler. Günlüğe gitmesi gereken kısım (Log modundayken gelenler, sistem satırları ve
        /// Screen'den dönünce aktarılan akan kısım) <paramref name="logOut"/>'a eklenir.
        /// </summary>
        public void Feed(string text, StringBuilder logOut)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                // Vision'ın kendi sistem satırları (\u0001 … \u0002): yalnız günlüğe
                if (c == '\u0001') { _inSystem = true; logOut.Append(c); continue; }
                if (c == '\u0002') { _inSystem = false; logOut.Append(c); continue; }
                if (_inSystem) { logOut.Append(c); continue; }

                SerialViewMode before = Mode;
                Process(c);
                if (Mode == SerialViewMode.Log)
                {
                    if (before == SerialViewMode.Screen)
                    {
                        // Akan yazıya dönüldü: ekranın son hâli ve Screen'de kalırken gelen akan kısım günlüğe (kaybolmasın,
                        // görünüm değişince sayfa değişmiş gibi olmasın)
                        logOut.Append("\u0001● Kart tam ekran gösterimden çıktı — ekranın son hâli:\u0002");
                        string shot = !string.IsNullOrWhiteSpace(_flowSnapshot) ? _flowSnapshot : _lastClearedScreen;
                        int skip = !string.IsNullOrWhiteSpace(_flowSnapshot) ? Math.Min(_flowSnapshotAt, _sinceDraw.Length) : 0;
                        if (!string.IsNullOrWhiteSpace(shot)) logOut.Append(shot.Replace("\n", "\r\n"));
                        logOut.Append(_sinceDraw.ToString(skip, _sinceDraw.Length - skip));
                        _sinceDraw.Clear();
                        _flowSnapshot = _lastClearedScreen = null;
                    }
                    logOut.Append(c);
                }
                else
                {
                    if (before == SerialViewMode.Log)
                        logOut.Append("\u0001● Kart tam ekran çizmeye başladı (BIOS / boot menüsü) — Ekran görünümünde\u0002");
                    if (_sinceDraw.Length < SinceDrawLimit) _sinceDraw.Append(c);
                }
            }
        }

        /// <summary>Elle görünüm değişimi sonrası: otomatik karar sayaçlarını sıfırlar.</summary>
        public void ResetModeCounters()
        {
            _positioning = 0;
            _plainScrolls = 0;
        }

        private void Process(char c)
        {
            switch (_state)
            {
                case State.Esc: ProcessEsc(c); return;
                case State.Csi: ProcessCsi(c); return;
                case State.Charset:
                    bool graphics = c == '0';
                    if (_charsetTarget == '(') _g0Graphics = graphics; else _g1Graphics = graphics;
                    _state = State.Ground;
                    return;
                case State.Osc:
                    if (c == '\u0007') _state = State.Ground;
                    else if (c == '\u001b') _state = State.OscEsc;
                    return;
                case State.OscEsc:
                    _state = c == '\\' ? State.Ground : State.Osc;
                    return;
            }

            switch (c)
            {
                case '\u001b': _state = State.Esc; return;
                case '\r': CursorCol = 0; _wrapPending = false; Touch(); return;
                case '\n': case '\u000b': case '\u000c': LineFeed(); return;
                case '\b': if (CursorCol > 0) CursorCol--; _wrapPending = false; Touch(); return;
                case '\t':
                    CursorCol = Math.Min(Cols - 1, (CursorCol / 8 + 1) * 8);
                    Touch();
                    return;
                case '\u000e': _shiftOut = true; return;
                case '\u000f': _shiftOut = false; return;
            }
            if (c < ' ' || c == '\u007f') return;   // zil, NUL, DEL …
            Put(c);
        }

        private void Put(char c)
        {
            if (_shiftOut ? _g1Graphics : _g0Graphics) c = DecGraphics(c);
            if (_wrapPending)
            {
                _wrapPending = false;
                CursorCol = 0;
                LineFeed();
            }
            Cell cell = _pen;
            cell.Ch = c;
            _cells[CursorRow][CursorCol] = cell;
            if (CursorCol < Cols - 1) CursorCol++;
            else if (_autoWrap) _wrapPending = true;
            Touch();
        }

        private void LineFeed()
        {
            _wrapPending = false;
            if (Mode == SerialViewMode.Screen && _plainScrolls == 0)
            {
                // Son çizimden sonraki ilk satır sonu: akış başlıyor olabilir — ekranın bu hâli günlüğe aktarılmak üzere saklanır
                _flowSnapshot = Text();
                _flowSnapshotAt = _sinceDraw.Length;
            }
            if (CursorRow == _bottom) ScrollUp(_top, _bottom, 1);
            else if (CursorRow < Rows - 1) CursorRow++;
            // Konumlamasız 25 satır (BIOS ekranının bir boyu) geldiyse kart tam ekran çizmeyi bırakmış, akan yazıya dönmüştür
            if (Mode == SerialViewMode.Screen && ++_plainScrolls >= DefaultRows) Mode = SerialViewMode.Log;
            Touch();
        }

        private void ScrollUp(int top, int bottom, int n)
        {
            for (int k = 0; k < n; k++)
            {
                for (int r = top; r < bottom; r++) _cells[r] = _cells[r + 1];
                _cells[bottom] = NewRow();
            }
        }

        private void ScrollDown(int top, int bottom, int n)
        {
            for (int k = 0; k < n; k++)
            {
                for (int r = bottom; r > top; r--) _cells[r] = _cells[r - 1];
                _cells[top] = NewRow();
            }
        }

        private void Touch() { Version++; }

        // ---------- ESC ----------

        private void ProcessEsc(char c)
        {
            _state = State.Ground;
            switch (c)
            {
                case '[': _state = State.Csi; _params.Clear(); _private = '\0'; return;
                case ']': _state = State.Osc; return;
                case '(': case ')': _state = State.Charset; _charsetTarget = c; return;
                case '7': SaveCursor(); return;
                case '8': RestoreCursor(); return;
                case 'D': LineFeed(); return;
                case 'E': CursorCol = 0; LineFeed(); return;
                case 'M':
                    _wrapPending = false;
                    if (CursorRow == _top) ScrollDown(_top, _bottom, 1);
                    else if (CursorRow > 0) CursorRow--;
                    Touch();
                    return;
                case 'c': Reset(); NotePositioning(true); return;
                default: return;   // ESC = / ESC > (tuş takımı modu) ve bilinmeyenler
            }
        }

        // ---------- CSI ----------

        private void ProcessCsi(char c)
        {
            if (c >= '0' && c <= '9' || c == ';' || c == ':') { _params.Append(c); return; }
            if (c == '?' || c == '>' || c == '=' || c == '<') { _private = c; return; }
            if (c == '\u001b') { _state = State.Esc; return; }   // yarım dizi: yenisi başlar
            if (c >= ' ' && c <= '/') return;                 // ara karakterler (nadiren): yok sayılır
            if (c < '@' || c > '~') { if (c < ' ') Process2(c); return; }
            _state = State.Ground;
            int[] p = ParseParams();
            if (_private == '?') { PrivateMode(c, p); return; }
            if (_private != '\0') return;                    // CSI > c (ikincil kimlik) vb.: cevap verilmez

            switch (c)
            {
                case 'A': MoveTo(CursorRow - Arg(p, 0, 1), CursorCol, false); return;
                case 'B': case 'e': MoveTo(CursorRow + Arg(p, 0, 1), CursorCol, false); return;
                case 'C': case 'a': MoveTo(CursorRow, CursorCol + Arg(p, 0, 1), false); return;
                case 'D': MoveTo(CursorRow, CursorCol - Arg(p, 0, 1), false); return;
                case 'E': MoveTo(CursorRow + Arg(p, 0, 1), 0, false); return;
                case 'F': MoveTo(CursorRow - Arg(p, 0, 1), 0, false); return;
                case 'G': case '`': MoveTo(CursorRow, Arg(p, 0, 1) - 1, true); return;
                case 'd': MoveTo(Arg(p, 0, 1) - 1, CursorCol, true); return;
                case 'H': case 'f': MoveTo(Arg(p, 0, 1) - 1, Arg(p, 1, 1) - 1, true); return;
                case 'J': EraseDisplay(Arg(p, 0, 0)); return;
                case 'K': EraseLine(Arg(p, 0, 0)); return;
                case 'L': if (CursorRow >= _top && CursorRow <= _bottom) ScrollDown(CursorRow, _bottom, Math.Min(Arg(p, 0, 1), _bottom - CursorRow + 1)); Touch(); return;
                case 'M': if (CursorRow >= _top && CursorRow <= _bottom) ScrollUp(CursorRow, _bottom, Math.Min(Arg(p, 0, 1), _bottom - CursorRow + 1)); Touch(); return;
                case 'P': DeleteChars(Arg(p, 0, 1)); return;
                case '@': InsertChars(Arg(p, 0, 1)); return;
                case 'X': EraseChars(Arg(p, 0, 1)); return;
                case 'S': ScrollUp(_top, _bottom, Math.Min(Arg(p, 0, 1), Rows)); Touch(); return;
                case 'T': ScrollDown(_top, _bottom, Math.Min(Arg(p, 0, 1), Rows)); Touch(); return;
                case 'r':
                    int t = Arg(p, 0, 1) - 1, b = Arg(p, 1, Rows) - 1;
                    if (t < 0) t = 0;
                    if (b >= Rows) b = Rows - 1;
                    if (t < b) { _top = t; _bottom = b; }
                    MoveTo(0, 0, true);
                    return;
                case 'm': Sgr(p); return;
                case 's': SaveCursor(); return;
                case 'u': RestoreCursor(); return;
                case 'n':
                    if (Arg(p, 0, 0) == 6) Responses.Enqueue("\u001b[" + (CursorRow + 1) + ";" + (CursorCol + 1) + "R");
                    else if (Arg(p, 0, 0) == 5) Responses.Enqueue("\u001b[0n");
                    return;
                case 'c': Responses.Enqueue("\u001b[?1;2c"); return;   // VT100 + gelişmiş video
                case 'h': case 'l': return;   // ANSI modları (ekleme modu vb.) — kullanılmıyor
                default: return;
            }
        }

        // CSI içinde gelen denetim karakteri (ör. CR, BS) VT'de hemen uygulanır
        private void Process2(char c)
        {
            State saved = _state;
            _state = State.Ground;
            Process(c);
            _state = saved;
        }

        private void PrivateMode(char c, int[] p)
        {
            if (c != 'h' && c != 'l') return;
            bool on = c == 'h';
            foreach (int m in p)
            {
                switch (m)
                {
                    case 1: AppCursorKeys = on; break;
                    case 7: _autoWrap = on; break;
                    case 25: CursorVisible = on; Touch(); break;
                    case 47: case 1047: case 1049:
                        // Alternatif ekran: tam ekran uygulama başladı / bitti
                        if (on) { ClearAll(); Mode = SerialViewMode.Screen; _plainScrolls = 0; _sinceDraw.Clear(); }
                        else { ClearAll(); Mode = SerialViewMode.Log; }
                        break;
                }
            }
        }

        private int[] ParseParams()
        {
            if (_params.Length == 0) return new int[0];
            string[] parts = _params.ToString().Split(';', ':');
            var r = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int v;
                r[i] = int.TryParse(parts[i], out v) ? Math.Min(v, 9999) : 0;
            }
            return r;
        }

        private static int Arg(int[] p, int i, int def)
        {
            if (i >= p.Length || p[i] == 0) return def;
            return p[i];
        }

        /// <summary>İmleci taşır; mutlak konumlama (satır / sütun verilerek) tam ekran çizim işareti sayılır.</summary>
        private void MoveTo(int row, int col, bool absolute)
        {
            CursorRow = Math.Max(0, Math.Min(Rows - 1, row));
            CursorCol = Math.Max(0, Math.Min(Cols - 1, col));
            _wrapPending = false;
            if (absolute) NotePositioning(false);
            Touch();
        }

        /// <summary>
        /// Tam ekran çizim işareti. Ekranı temizlemek tek başına yeter; konumlama ise kısa sürede (2 sn) art arda en az 3
        /// kez gelirse (BIOS ekranı çizer gibi). Kabuğun tek tük imleç komutları görünümü değiştirmez.
        /// </summary>
        private void NotePositioning(bool strong)
        {
            _plainScrolls = 0;
            _flowSnapshot = null;
            if (Mode == SerialViewMode.Screen) { _sinceDraw.Clear(); return; }
            DateTime now = _now();
            if (_positioning == 0 || (now - _positioningSince).TotalSeconds > 2) { _positioning = 0; _positioningSince = now; }
            _positioning++;
            if (strong || _positioning >= 3)
            {
                Mode = SerialViewMode.Screen;
                _positioning = 0;
                _sinceDraw.Clear();
            }
        }

        private void EraseDisplay(int mode)
        {
            if (mode == 2 || mode == 3)
            {
                if (Mode == SerialViewMode.Screen)
                {
                    string before = Text();
                    if (!string.IsNullOrWhiteSpace(before)) _lastClearedScreen = before;
                }
                for (int r = 0; r < Rows; r++) _cells[r] = NewRow();
                NotePositioning(true);
            }
            else if (mode == 0)
            {
                EraseLine(0);
                for (int r = CursorRow + 1; r < Rows; r++) _cells[r] = NewRow();
            }
            else if (mode == 1)
            {
                EraseLine(1);
                for (int r = 0; r < CursorRow; r++) _cells[r] = NewRow();
            }
            Touch();
        }

        private void EraseLine(int mode)
        {
            Cell b = Blank();
            b.Bg = _pen.Bg;
            int from = mode == 0 ? CursorCol : 0, to = mode == 1 ? CursorCol : Cols - 1;
            if (mode == 2) { from = 0; to = Cols - 1; }
            for (int c = from; c <= to && c < Cols; c++) _cells[CursorRow][c] = b;
            Touch();
        }

        private void EraseChars(int n)
        {
            Cell b = Blank();
            b.Bg = _pen.Bg;
            for (int c = CursorCol; c < Math.Min(Cols, CursorCol + n); c++) _cells[CursorRow][c] = b;
            Touch();
        }

        private void DeleteChars(int n)
        {
            Cell[] row = _cells[CursorRow];
            n = Math.Min(n, Cols - CursorCol);
            for (int c = CursorCol; c < Cols; c++) row[c] = c + n < Cols ? row[c + n] : Blank();
            Touch();
        }

        private void InsertChars(int n)
        {
            Cell[] row = _cells[CursorRow];
            n = Math.Min(n, Cols - CursorCol);
            for (int c = Cols - 1; c >= CursorCol; c--) row[c] = c - n >= CursorCol ? row[c - n] : Blank();
            Touch();
        }

        private void ClearAll()
        {
            for (int r = 0; r < Rows; r++) _cells[r] = NewRow();
            CursorRow = CursorCol = 0;
            Touch();
        }

        private void SaveCursor() { _savedRow = CursorRow; _savedCol = CursorCol; _savedPen = _pen; }

        private void RestoreCursor()
        {
            CursorRow = Math.Min(_savedRow, Rows - 1);
            CursorCol = Math.Min(_savedCol, Cols - 1);
            _pen = _savedPen.Ch == '\0' ? Blank() : _savedPen;
            _wrapPending = false;
            Touch();
        }

        private void Sgr(int[] p)
        {
            if (p.Length == 0) { _pen = Blank(); return; }
            for (int i = 0; i < p.Length; i++)
            {
                int v = p[i];
                if (v == 0) { _pen = Blank(); }
                else if (v == 1) _pen.Bold = true;
                else if (v == 4) _pen.Underline = true;
                else if (v == 7) _pen.Reverse = true;
                else if (v == 22) _pen.Bold = false;
                else if (v == 24) _pen.Underline = false;
                else if (v == 27) _pen.Reverse = false;
                else if (v >= 30 && v <= 37) _pen.Fg = (sbyte)(v - 30);
                else if (v == 39) _pen.Fg = -1;
                else if (v >= 40 && v <= 47) _pen.Bg = (sbyte)(v - 40);
                else if (v == 49) _pen.Bg = -1;
                else if (v >= 90 && v <= 97) _pen.Fg = (sbyte)(v - 90 + 8);
                else if (v >= 100 && v <= 107) _pen.Bg = (sbyte)(v - 100 + 8);
                else if ((v == 38 || v == 48) && i + 1 < p.Length)
                {
                    // 256 renk / 24 bit: en yakın 16 renge indirilir
                    sbyte color = -1;
                    if (p[i + 1] == 5 && i + 2 < p.Length) { color = (sbyte)(p[i + 2] < 16 ? p[i + 2] : 7); i += 2; }
                    else if (p[i + 1] == 2 && i + 4 < p.Length) { color = Nearest(p[i + 2], p[i + 3], p[i + 4]); i += 4; }
                    else i += 1;
                    if (v == 38) _pen.Fg = color; else _pen.Bg = color;
                }
            }
        }

        private static sbyte Nearest(int r, int g, int b)
        {
            int idx = (r > 127 ? 1 : 0) | (g > 127 ? 2 : 0) | (b > 127 ? 4 : 0);
            bool bright = r > 191 || g > 191 || b > 191;
            return (sbyte)(idx + (bright ? 8 : 0));
        }

        /// <summary>DEC özel çizgi karakterleri (ESC ( 0): çerçeveler.</summary>
        private static char DecGraphics(char c)
        {
            switch (c)
            {
                case 'j': return '┘';
                case 'k': return '┐';
                case 'l': return '┌';
                case 'm': return '└';
                case 'n': return '┼';
                case 'q': return '─';
                case 't': return '├';
                case 'u': return '┤';
                case 'v': return '┴';
                case 'w': return '┬';
                case 'x': return '│';
                case 'a': return '▒';
                case '`': return '◆';
                case 'f': return '°';
                case 'g': return '±';
                case '~': return '·';
                case 'o': case 'p': return '─';
                case 'r': case 's': return '─';
                case 'y': return '≤';
                case 'z': return '≥';
                case '{': return 'π';
                case '|': return '≠';
                case '}': return '£';
                default: return c;
            }
        }

        // =====================================================================
        // Çıktı
        // =====================================================================

        /// <summary>Ekranın düz metni (satır sonu boşlukları atılmış; kaydetme ve kopyalama için).</summary>
        public string Text()
        {
            var sb = new StringBuilder();
            int last = Rows - 1;
            while (last > 0 && RowText(last).Length == 0) last--;
            for (int r = 0; r <= last; r++)
            {
                sb.Append(RowText(r));
                if (r < last) sb.Append('\n');
            }
            return sb.ToString();
        }

        public string RowText(int row)
        {
            var chars = new char[Cols];
            for (int c = 0; c < Cols; c++) chars[c] = _cells[row][c].Ch == '\0' ? ' ' : _cells[row][c].Ch;
            return new string(chars).TrimEnd();
        }

        /// <summary>
        /// Ekran boyutunu değiştirir (Ekran görünümü pencereyi doldursun diye; en az 80×25 — BIOS bu alana çizer).
        /// İçerik sol üstten korunur, imleç alana sığdırılır, kaydırma bölgesi tüm ekran olur.
        /// </summary>
        public void Resize(int cols, int rows)
        {
            cols = Math.Max(DefaultCols, Math.Min(400, cols));
            rows = Math.Max(DefaultRows, Math.Min(200, rows));
            if (cols == Cols && rows == Rows) return;
            var cells = new Cell[rows][];
            int oldRows = Rows, oldCols = Cols;
            Cols = cols;
            Rows = rows;
            for (int r = 0; r < rows; r++)
            {
                cells[r] = NewRow();
                if (r < oldRows) for (int c = 0; c < Math.Min(cols, oldCols); c++) cells[r][c] = _cells[r][c];
            }
            _cells = cells;
            _top = 0;
            _bottom = rows - 1;
            CursorRow = Math.Min(CursorRow, rows - 1);
            CursorCol = Math.Min(CursorCol, cols - 1);
            _wrapPending = false;
            Touch();
        }

        /// <summary>Görünümü elle değiştirmek (başlıktaki düğme).</summary>
        public void SetMode(SerialViewMode mode)
        {
            Mode = mode;
            ResetModeCounters();
            if (mode == SerialViewMode.Log) _sinceDraw.Clear();
        }
    }
}
