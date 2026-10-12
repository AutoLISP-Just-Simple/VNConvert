namespace VNConvert.Core;

internal sealed class UnicodeCompEngine : ICharsetEngine
{
    private readonly struct CompCharInfo : IComparable<CompCharInfo>
    {
        public readonly uint CompChar;
        public readonly int StdIndex;

        public CompCharInfo(uint compChar, int stdIndex)
        {
            CompChar = compChar;
            StdIndex = stdIndex;
        }

        public int CompareTo(CompCharInfo other) => CompChar.CompareTo(other.CompChar);
    }

    private readonly uint[] _uniCompChars;
    private readonly CompCharInfo[] _info;

    public UnicodeCompEngine(ReadOnlySpan<ushort> uniChars, ReadOnlySpan<uint> uniCompChars)
    {
        _uniCompChars = uniCompChars.ToArray();
        var list = new List<CompCharInfo>(VnTables.TotalVnChars * 2);

        for (int i = 0; i < VnTables.TotalVnChars; i++)
        {
            list.Add(new CompCharInfo(_uniCompChars[i], i));
        }

        for (int k = 0; k < VnTables.TotalVnChars; k++)
        {
            if (uniChars[k] != _uniCompChars[k])
            {
                list.Add(new CompCharInfo(uniChars[k], k));
            }
        }

        _info = list.ToArray();
        Array.Sort(_info);
    }

    public void StartInput(in VnConvOptions options) { }
    public void StartOutput(in VnConvOptions options) { }

    private int BinarySearch(uint key)
    {
        int low = 0;
        int high = _info.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            uint midVal = _info[mid].CompChar;

            if (midVal < key)
                low = mid + 1;
            else if (midVal > key)
                high = mid - 1;
            else
                return mid;
        }

        return -1;
    }

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNextW(out ushort w))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 2;
        int idx = BinarySearch(w);
        if (idx < 0)
        {
            stdChar = w;
        }
        else
        {
            stdChar = StdVnChar.Offset + (uint)_info[idx].StdIndex;

            if (stream.PeekNextW(out ushort hi) && hi > 0)
            {
                uint key2 = (uint)(w | (hi << 16));
                int idx2 = BinarySearch(key2);
                if (idx2 >= 0)
                {
                    stdChar = StdVnChar.Offset + (uint)_info[idx2].StdIndex;
                    bytesRead += 2;
                    stream.GetNextW(out _);
                }
            }
        }

        return true;
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        if (stdChar >= StdVnChar.Offset)
        {
            int index = (int)(stdChar - StdVnChar.Offset);
            uint uniCompCh = (index < _uniCompChars.Length) ? _uniCompChars[index] : stdChar;
            ushort lo = (ushort)(uniCompCh & 0xFFFF);
            ushort hi = (ushort)(uniCompCh >> 16);

            bytesWritten = 2;
            bool ok = stream.PutW(lo);
            if (hi > 0)
            {
                bytesWritten += 2;
                ok = stream.PutW(hi);
            }
            return ok;
        }
        else
        {
            bytesWritten = 2;
            return stream.PutW((ushort)stdChar);
        }
    }
}
