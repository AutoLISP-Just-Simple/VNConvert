using System.Runtime.CompilerServices;

namespace VNConvert.Core;

internal sealed class UnicodeEngine : ICharsetEngine
{
    private readonly ushort[] _toUnicode;
    // Each uint is: (stdIndex << 16) | unicodeChar
    private readonly uint[] _searchTable;

    public UnicodeEngine(ReadOnlySpan<ushort> table)
    {
        _toUnicode = table.ToArray();
        _searchTable = new uint[VnTables.TotalVnChars];

        for (int i = 0; i < VnTables.TotalVnChars; i++)
        {
            _searchTable[i] = ((uint)i << 16) | _toUnicode[i];
        }

        Array.Sort(_searchTable, static (a, b) => ((ushort)(a & 0xFFFF)).CompareTo((ushort)(b & 0xFFFF)));
    }

    public void StartInput(in VnConvOptions options) { }
    public void StartOutput(in VnConvOptions options) { }

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        if (!stream.GetNextW(out ushort uniCh))
        {
            bytesRead = 0;
            stdChar = 0;
            return false;
        }

        bytesRead = 2;
        stdChar = MapUnicodeToStd(uniCh);
        return true;
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        bytesWritten = 2;
        ushort outCh;
        if (stdChar >= StdVnChar.Offset)
        {
            int index = (int)(stdChar - StdVnChar.Offset);
            outCh = (index < VnTables.TotalVnChars) ? _toUnicode[index] : (ushort)stdChar;
        }
        else
        {
            outCh = (ushort)stdChar;
        }
        return stream.PutW(outCh);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint MapUnicodeToStd(ushort uniCh)
    {
        if (uniCh == 0xFEFF)
            return StdVnChar.Invalid;

        int idx = BinarySearch(uniCh);
        if (idx >= 0)
        {
            return StdVnChar.Offset + (_searchTable[idx] >> 16);
        }
        return uniCh;
    }

    internal int BinarySearch(ushort uniCh)
    {
        int low = 0;
        int high = _searchTable.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            ushort midVal = (ushort)(_searchTable[mid] & 0xFFFF);

            if (midVal < uniCh)
                low = mid + 1;
            else if (midVal > uniCh)
                high = mid - 1;
            else
                return mid;
        }

        return -1;
    }
}
