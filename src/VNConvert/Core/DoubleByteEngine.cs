namespace VNConvert.Core;

internal class DoubleByteEngine : ICharsetEngine
{
    protected readonly ushort[] _toDoubleChar;
    protected readonly ushort[] _stdMap;
    // Each uint is: (stdIndex << 16) | codeUnit
    protected uint[] _searchTable;
    protected readonly int _totalChars;

    public DoubleByteEngine(ReadOnlySpan<ushort> table)
    {
        _toDoubleChar = table.ToArray();
        _totalChars = VnTables.TotalVnChars;
        _stdMap = new ushort[256];
        _searchTable = new uint[_totalChars];

        for (int i = 0; i < _totalChars; i++)
        {
            ushort code = _toDoubleChar[i];
            if ((code >> 8) != 0)
            {
                _stdMap[code >> 8] = 0xFFFF;
            }
            else if (_stdMap[(byte)code] == 0)
            {
                _stdMap[(byte)code] = (ushort)(i + 1);
            }

            _searchTable[i] = ((uint)i << 16) | code;
        }

        Array.Sort(_searchTable, static (a, b) => ((ushort)(a & 0xFFFF)).CompareTo((ushort)(b & 0xFFFF)));
    }

    public virtual void StartInput(in VnConvOptions options) { }
    public virtual void StartOutput(in VnConvOptions options) { }

    public virtual bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNext(out byte ch))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 1;
        ushort mapVal = _stdMap[ch];

        if (mapVal == 0)
        {
            stdChar = ch;
        }
        else if (mapVal == 0xFFFF)
        {
            stdChar = StdVnChar.Invalid;
        }
        else
        {
            stdChar = StdVnChar.Offset + (uint)mapVal - 1;
            if (stream.PeekNext(out byte hi) && hi > 0)
            {
                ushort key = (ushort)(ch | (hi << 8));
                int idx = BinarySearch(key);
                if (idx >= 0)
                {
                    stdChar = StdVnChar.Offset + (_searchTable[idx] >> 16);
                    bytesRead = 2;
                    stream.GetNext(out _);
                }
            }
        }

        return true;
    }

    public virtual bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        if (stdChar >= StdVnChar.Offset)
        {
            int index = (int)(stdChar - StdVnChar.Offset);
            ushort wCh = (index < _toDoubleChar.Length) ? _toDoubleChar[index] : (ushort)0;

            if ((wCh & 0xFF00) != 0)
            {
                bytesWritten = 2;
                stream.PutB((byte)(wCh & 0xFF));
                return stream.PutB((byte)(wCh >> 8));
            }
            else
            {
                byte b = (byte)wCh;
                if (_stdMap[b] == 0xFFFF)
                    b = StdVnChar.PadChar;
                bytesWritten = 1;
                return stream.PutB(b);
            }
        }
        else
        {
            bytesWritten = 1;
            if (stdChar > 255 || _stdMap[(byte)stdChar] != 0)
            {
                return stream.PutB(StdVnChar.PadChar);
            }
            else
            {
                return stream.PutB((byte)stdChar);
            }
        }
    }

    protected int BinarySearch(ushort key)
    {
        int low = 0;
        int high = _searchTable.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            ushort midVal = (ushort)(_searchTable[mid] & 0xFFFF);

            if (midVal < key)
                low = mid + 1;
            else if (midVal > key)
                high = mid - 1;
            else
                return mid;
        }

        return -1;
    }
}
