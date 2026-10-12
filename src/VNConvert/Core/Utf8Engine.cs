namespace VNConvert.Core;

internal sealed class Utf8Engine : ICharsetEngine
{
    private readonly UnicodeEngine _unicodeEngine;
    private readonly ushort[] _toUnicode;

    public Utf8Engine(UnicodeEngine unicodeEngine, ReadOnlySpan<ushort> unicodeTable)
    {
        _unicodeEngine = unicodeEngine;
        _toUnicode = unicodeTable.ToArray();
    }

    public void StartInput(in VnConvOptions options) { }
    public void StartOutput(in VnConvOptions options) { }

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNext(out byte first))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 1;
        ushort uniCh;

        if (first < 0x80)
        {
            uniCh = first;
        }
        else if ((first & 0xE0) == 0xC0)
        {
            if (!stream.PeekNext(out byte second))
            {
                stdChar = 0;
                return false;
            }

            if ((second & 0xC0) != 0x80)
            {
                stdChar = StdVnChar.Invalid;
                return true;
            }

            stream.GetNext(out _);
            bytesRead = 2;
            uniCh = (ushort)(((first & 0x1F) << 6) | (second & 0x3F));
        }
        else if ((first & 0xF0) == 0xE0)
        {
            if (!stream.PeekNext(out byte second))
            {
                stdChar = 0;
                return false;
            }

            if ((second & 0xC0) != 0x80)
            {
                stdChar = StdVnChar.Invalid;
                return true;
            }

            stream.GetNext(out _);
            bytesRead = 2;

            if (!stream.PeekNext(out byte third))
            {
                stdChar = 0;
                return false;
            }

            if ((third & 0xC0) != 0x80)
            {
                stdChar = StdVnChar.Invalid;
                return true;
            }

            stream.GetNext(out _);
            bytesRead = 3;
            uniCh = (ushort)(((first & 0x0F) << 12) | ((second & 0x3F) << 6) | (third & 0x3F));
        }
        else
        {
            stdChar = StdVnChar.Invalid;
            return true;
        }

        stdChar = _unicodeEngine.MapUnicodeToStd(uniCh);
        return true;
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        ushort uChar = (stdChar < StdVnChar.Offset)
            ? (ushort)stdChar
            : (int)(stdChar - StdVnChar.Offset) < _toUnicode.Length
                ? _toUnicode[stdChar - StdVnChar.Offset]
                : (ushort)stdChar;

        if (uChar < 0x0080)
        {
            bytesWritten = 1;
            return stream.PutB((byte)uChar);
        }
        else if (uChar < 0x0800)
        {
            bytesWritten = 2;
            stream.PutB((byte)(0xC0 | (uChar >> 6)));
            return stream.PutB((byte)(0x80 | (uChar & 0x3F)));
        }
        else
        {
            bytesWritten = 3;
            stream.PutB((byte)(0xE0 | (uChar >> 12)));
            stream.PutB((byte)(0x80 | ((uChar >> 6) & 0x3F)));
            return stream.PutB((byte)(0x80 | (uChar & 0x3F)));
        }
    }
}
