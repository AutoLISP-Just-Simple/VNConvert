namespace VNConvert.Core;

internal sealed class UnicodeCStringEngine : ICharsetEngine
{
    private static readonly byte[] HexChars = "0123456789ABCDEF"u8.ToArray();
    private readonly UnicodeEngine _unicodeEngine;
    private readonly ushort[] _toUnicode;
    private bool _prevIsHex;

    public UnicodeCStringEngine(UnicodeEngine unicodeEngine, ReadOnlySpan<ushort> unicodeTable)
    {
        _unicodeEngine = unicodeEngine;
        _toUnicode = unicodeTable.ToArray();
        _prevIsHex = false;
    }

    public void StartInput(in VnConvOptions options)
    {
        _prevIsHex = false;
    }

    public void StartOutput(in VnConvOptions options)
    {
        _prevIsHex = false;
    }

    private static int HexDigitValue(byte digit)
    {
        if (digit >= (byte)'a' && digit <= (byte)'f')
            return digit - (byte)'a' + 10;
        if (digit >= (byte)'A' && digit <= (byte)'F')
            return digit - (byte)'A' + 10;
        if (digit >= (byte)'0' && digit <= (byte)'9')
            return digit - (byte)'0';
        return 0;
    }

    private static bool IsHexDigit(byte b) =>
        (b >= (byte)'0' && b <= (byte)'9') ||
        (b >= (byte)'a' && b <= (byte)'f') ||
        (b >= (byte)'A' && b <= (byte)'F');

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNext(out byte ch))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 1;
        ushort uniCh = ch;

        if (ch == (byte)'\\')
        {
            if (stream.PeekNext(out byte nextCh) && (nextCh == (byte)'x' || nextCh == (byte)'X'))
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
                uniCh = code;
            }
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

        if (uChar < 256 && (!IsHexDigit((byte)uChar) || !_prevIsHex))
        {
            bytesWritten = 1;
            _prevIsHex = false;
            return stream.PutB((byte)uChar);
        }
        else
        {
            bytesWritten = 2;
            stream.PutB((byte)'\\');
            stream.PutB((byte)'x');

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

            _prevIsHex = true;
            return stream.IsOk;
        }
    }
}
