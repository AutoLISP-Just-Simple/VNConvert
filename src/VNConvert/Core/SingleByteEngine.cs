namespace VNConvert.Core;

internal sealed class SingleByteEngine : ICharsetEngine
{
    private readonly byte[] _vnChars;
    private readonly ushort[] _stdMap;

    public SingleByteEngine(ReadOnlySpan<byte> table)
    {
        _vnChars = table.ToArray();
        _stdMap = new ushort[256];

        for (int i = 0; i < VnTables.TotalVnChars; i++)
        {
            byte b = _vnChars[i];
            if (b != 0 && (i == VnTables.TotalVnChars - 1 || b != _vnChars[i + 1]))
            {
                _stdMap[b] = (ushort)(i + 1);
            }
        }
    }

    public void StartInput(in VnConvOptions options) { }
    public void StartOutput(in VnConvOptions options) { }

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        if (!stream.GetNext(out byte ch))
        {
            bytesRead = 0;
            stdChar = 0;
            return false;
        }

        ushort mapVal = _stdMap[ch];
        stdChar = (mapVal != 0) ? (StdVnChar.Offset + (uint)mapVal - 1) : ch;
        bytesRead = 1;
        return true;
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        bytesWritten = 1;
        byte ch;
        if (stdChar >= StdVnChar.Offset)
        {
            int index = (int)(stdChar - StdVnChar.Offset);
            ch = (index < VnTables.TotalVnChars) ? _vnChars[index] : (byte)0;
            if (ch == 0)
            {
                ch = stdChar == StdVnChar.StdStartQuote ? StdVnChar.PadStartQuote :
                     stdChar == StdVnChar.StdEndQuote ? StdVnChar.PadEndQuote :
                     stdChar == StdVnChar.StdEllipsis ? StdVnChar.PadEllipsis : StdVnChar.PadChar;
            }
            return stream.PutB(ch);
        }
        else
        {
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
}
