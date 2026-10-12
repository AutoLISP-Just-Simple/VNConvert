namespace VNConvert.Core;

internal class UnicodeRefEngine : ICharsetEngine
{
    protected readonly UnicodeEngine _unicodeEngine;
    protected readonly ushort[] _toUnicode;

    public UnicodeRefEngine(UnicodeEngine unicodeEngine, ReadOnlySpan<ushort> unicodeTable)
    {
        _unicodeEngine = unicodeEngine;
        _toUnicode = unicodeTable.ToArray();
    }

    public virtual void StartInput(in VnConvOptions options) { }
    public virtual void StartOutput(in VnConvOptions options) { }

    protected static int HexDigitValue(byte digit)
    {
        if (digit >= (byte)'a' && digit <= (byte)'f')
            return digit - (byte)'a' + 10;
        if (digit >= (byte)'A' && digit <= (byte)'F')
            return digit - (byte)'A' + 10;
        if (digit >= (byte)'0' && digit <= (byte)'9')
            return digit - (byte)'0';
        return 0;
    }

    protected static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';
    protected static bool IsHexDigit(byte b) =>
        (b >= (byte)'0' && b <= (byte)'9') ||
        (b >= (byte)'a' && b <= (byte)'f') ||
        (b >= (byte)'A' && b <= (byte)'F');

    public virtual bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNext(out byte ch))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 1;
        ushort uniCh = ch;

        if (ch == (byte)'&')
        {
            if (stream.PeekNext(out byte nextCh) && nextCh == (byte)'#')
            {
                stream.GetNext(out _);
                bytesRead++;

                if (!stream.IsEos)
                {
                    stream.PeekNext(out nextCh);
                    if (nextCh != (byte)'x' && nextCh != (byte)'X')
                    {
                        ushort code = 0;
                        int digits = 0;
                        while (stream.PeekNext(out nextCh) && IsDigit(nextCh) && digits < 5)
                        {
                            stream.GetNext(out _);
                            bytesRead++;
                            code = (ushort)(code * 10 + (nextCh - (byte)'0'));
                            digits++;
                        }

                        if (stream.PeekNext(out nextCh) && nextCh == (byte)';')
                        {
                            stream.GetNext(out _);
                            bytesRead++;
                            uniCh = code;
                        }
                    }
                    else
                    {
                        stream.GetNext(out _);
                        bytesRead++;
                        ushort code = 0;
                        int digits = 0;
                        while (stream.PeekNext(out nextCh) && IsHexDigit(nextCh) && digits < 4)
                        {
                            stream.GetNext(out _);
                            bytesRead++;
                            code = (ushort)((code << 4) + HexDigitValue(nextCh));
                            digits++;
                        }

                        if (stream.PeekNext(out nextCh) && nextCh == (byte)';')
                        {
                            stream.GetNext(out _);
                            bytesRead++;
                            uniCh = code;
                        }
                    }
                }
            }
        }

        stdChar = _unicodeEngine.MapUnicodeToStd(uniCh);
        return true;
    }

    public virtual bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        ushort uChar = (stdChar < StdVnChar.Offset)
            ? (ushort)stdChar
            : (int)(stdChar - StdVnChar.Offset) < _toUnicode.Length
                ? _toUnicode[stdChar - StdVnChar.Offset]
                : (ushort)stdChar;

        if (uChar < 128)
        {
            bytesWritten = 1;
            return stream.PutB((byte)uChar);
        }

        // Output &#D;
        stream.PutB((byte)'&');
        stream.PutB((byte)'#');
        bytesWritten = 2;

        int prev = 0;
        int baseDiv = 10000;
        int val = uChar;

        for (int i = 0; i < 5; i++)
        {
            int digit = val / baseDiv;
            if (digit != 0 || prev != 0)
            {
                prev = 1;
                bytesWritten++;
                stream.PutB((byte)('0' + digit));
            }
            val %= baseDiv;
            baseDiv /= 10;
        }

        bytesWritten++;
        return stream.PutB((byte)';');
    }
}

internal sealed class UnicodeHexEngine : UnicodeRefEngine
{
    private static readonly byte[] HexChars = "0123456789ABCDEF"u8.ToArray();

    public UnicodeHexEngine(UnicodeEngine unicodeEngine, ReadOnlySpan<ushort> unicodeTable)
        : base(unicodeEngine, unicodeTable)
    {
    }

    public override bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        ushort uChar = (stdChar < StdVnChar.Offset)
            ? (ushort)stdChar
            : (int)(stdChar - StdVnChar.Offset) < _toUnicode.Length
                ? _toUnicode[stdChar - StdVnChar.Offset]
                : (ushort)stdChar;

        if (uChar < 256)
        {
            bytesWritten = 1;
            return stream.PutB((byte)uChar);
        }

        // Output &#xH;
        stream.PutB((byte)'&');
        stream.PutB((byte)'#');
        stream.PutB((byte)'x');
        bytesWritten = 3;

        int prev = 0;
        int shifts = 12;

        for (int i = 0; i < 4; i++)
        {
            int digit = (uChar >> shifts) & 0x0F;
            if (digit > 0 || prev != 0)
            {
                prev = 1;
                bytesWritten++;
                stream.PutB(HexChars[digit]);
            }
            shifts -= 4;
        }

        bytesWritten++;
        return stream.PutB((byte)';');
    }
}
