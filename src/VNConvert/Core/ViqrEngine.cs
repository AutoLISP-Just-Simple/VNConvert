namespace VNConvert.Core;

internal sealed class ViqrEngine : ICharsetEngine
{
    private static readonly string[] EscapePrefixes = ["/", "@", "mailto:", "email:", "news:", "www", "ftp"];

    private readonly uint[] _vnChars;
    private readonly ushort[] _stdMap;
    private readonly PatternMatcher _escPatterns;
    private readonly PatternMatcher _outEscPatterns;

    private VnConvOptions _options;
    internal bool Suspicious;
    private bool _atWordBeginning;
    private bool _gotTone;
    private bool _escAll;

    private bool _escapeBowl;
    private bool _escapeRoof;
    private bool _escapeHook;
    private bool _escapeTone;
    private bool _noOutEsc;

    public ViqrEngine(ReadOnlySpan<uint> table)
    {
        _vnChars = table.ToArray();
        _stdMap = new ushort[256];
        _escPatterns = new PatternMatcher(EscapePrefixes);
        _outEscPatterns = new PatternMatcher(EscapePrefixes);

        for (int i = 0; i < VnTables.TotalVnChars; i++)
        {
            uint dw = _vnChars[i];
            if ((dw & 0xFFFFFF00) == 0) // single byte base
            {
                _stdMap[(byte)dw] = (ushort)(i + 256);
            }
        }

        // Tones & marks offsets
        _stdMap[(byte)'\''] = 2;
        _stdMap[(byte)'`'] = 4;
        _stdMap[(byte)'?'] = 6;
        _stdMap[(byte)'~'] = 8;
        _stdMap[(byte)'.'] = 10;
        _stdMap[(byte)'^'] = 12;
        _stdMap[(byte)'('] = 24;
        _stdMap[(byte)'+'] = 26;
        _stdMap[(byte)'*'] = 26;
    }

    public void StartInput(in VnConvOptions options)
    {
        _options = options;
        Suspicious = false;
        _atWordBeginning = true;
        _gotTone = false;
        _escAll = false;
        if (_options.ViqrEsc != 0)
            _escPatterns.Reset();
    }

    public void StartOutput(in VnConvOptions options)
    {
        _options = options;
        _escapeBowl = false;
        _escapeRoof = false;
        _escapeHook = false;
        _escapeTone = false;
        _noOutEsc = false;
        _outEscPatterns.Reset();
    }

    private static bool IsVowel(byte ch) =>
        ch is (byte)'a' or (byte)'e' or (byte)'i' or (byte)'o' or (byte)'u' or (byte)'y' or
              (byte)'A' or (byte)'E' or (byte)'I' or (byte)'O' or (byte)'U' or (byte)'Y';

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        bytesRead = 0;
        if (!stream.GetNext(out byte ch1))
        {
            stdChar = 0;
            return false;
        }

        bytesRead = 1;
        stdChar = _stdMap[ch1];

        if (_options.ViqrEsc != 0)
        {
            if (_escPatterns.FoundAtNextChar(ch1) != -1)
                _escAll = true;
        }

        if (_escAll && (ch1 == (byte)' ' || ch1 == (byte)'\t' || ch1 == (byte)'\r' || ch1 == (byte)'\n'))
            _escAll = false;

        if (ch1 == (byte)'\\')
        {
            if (stream.GetNext(out ch1))
            {
                bytesRead++;
                stdChar = _stdMap[ch1];
            }
        }

        if (stdChar < 256)
        {
            stdChar = ch1;
        }
        else if (!_escAll && !stream.IsEos)
        {
            if (stream.PeekNext(out byte ch2))
            {
                byte upper = (byte)char.ToUpperInvariant((char)ch1);
                if (_atWordBeginning && upper == (byte)'D' && (ch2 == (byte)'d' || ch2 == (byte)'D'))
                {
                    stream.GetNext(out _);
                    bytesRead++;
                    stdChar += 2; // 'dd' is 2 positions after 'd'
                }
                else
                {
                    uint index = _stdMap[ch2];
                    bool cond;
                    if (Suspicious)
                    {
                        cond = IsVowel(ch1) &&
                            (index == 2 || index == 4 || index == 8 ||
                             (index == 12 && (upper == (byte)'A' || upper == (byte)'E' || upper == (byte)'O')) ||
                             (_stdMap[ch2] == 24 && upper == (byte)'A') ||
                             (_stdMap[ch2] == 26 && (upper == (byte)'O' || upper == (byte)'U')));
                        if (cond)
                            Suspicious = false;
                    }
                    else
                    {
                        cond = IsVowel(ch1) &&
                            ((index <= 10 && index > 0 && (!_gotTone || (index != 6 && index != 10))) ||
                             (index == 12 && (upper == (byte)'A' || upper == (byte)'E' || upper == (byte)'O')) ||
                             (_stdMap[ch2] == 24 && upper == (byte)'A') ||
                             (_stdMap[ch2] == 26 && (upper == (byte)'O' || upper == (byte)'U')));
                    }

                    if (cond)
                    {
                        if (index > 0)
                            _gotTone = true;

                        stream.GetNext(out _);
                        bytesRead++;
                        int offset = _stdMap[ch2];
                        if (offset == 26) offset = 24;
                        if (offset == 24 && (ch1 == (byte)'u' || ch1 == (byte)'U')) offset = 12;
                        stdChar += (uint)offset;

                        if (stream.PeekNext(out ch2))
                        {
                            if (index > 10 && _stdMap[ch2] > 0 && _stdMap[ch2] <= 10)
                            {
                                stream.GetNext(out _);
                                bytesRead++;
                                stdChar += _stdMap[ch2];
                            }
                        }
                    }
                }
            }
        }

        _atWordBeginning = (stdChar < 256);
        if (stdChar < 256)
            _gotTone = false;

        if (stdChar >= 256)
            stdChar += StdVnChar.Offset - 256;

        return true;
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        bytesWritten = 1;
        if (stdChar >= StdVnChar.Offset)
        {
            int index = (int)(stdChar - StdVnChar.Offset);
            uint dw = (index < _vnChars.Length) ? _vnChars[index] : 0;

            byte first = (byte)(dw & 0xFF);
            byte firstUpper = (byte)char.ToUpperInvariant((char)first);

            bool ok = stream.PutB(first);
            if (_outEscPatterns.FoundAtNextChar(first) != -1)
                _noOutEsc = true;

            if (_noOutEsc && (first == (byte)' ' || first == (byte)'\t' || first == (byte)'\r' || first == (byte)'\n'))
                _noOutEsc = false;

            if ((dw & 0x0000FF00) != 0)
            {
                byte second = (byte)((dw >> 8) & 0xFF);
                bytesWritten++;
                ok = stream.PutB(second);

                if ((dw & 0x00FF0000) != 0)
                {
                    bytesWritten++;
                    ok = stream.PutB((byte)((dw >> 16) & 0xFF));
                    _escapeTone = false;
                }
                else
                {
                    ushort idx = _stdMap[second];
                    _escapeTone = (idx == 12 || idx == 24 || idx == 26);
                }

                _outEscPatterns.Reset();
                _escapeBowl = false;
                _escapeHook = false;
                _escapeRoof = false;
            }
            else
            {
                _escapeTone = IsVowel(first);
                _escapeBowl = (firstUpper == (byte)'A');
                _escapeHook = (firstUpper == (byte)'U' || firstUpper == (byte)'O');
                _escapeRoof = (firstUpper == (byte)'A' || firstUpper == (byte)'E' || firstUpper == (byte)'O');
            }

            return ok;
        }
        else
        {
            if (stdChar > 255)
            {
                return stream.PutB(StdVnChar.PadChar);
            }

            byte b = (byte)stdChar;
            ushort idx = _stdMap[b];
            if (_options.ViqrMixed == 0 && !_noOutEsc &&
                (b == (byte)'\\' ||
                 (idx > 0 && idx <= 10 && _escapeTone) ||
                 (idx == 12 && _escapeRoof) ||
                 (idx == 24 && _escapeBowl) ||
                 (idx == 26 && _escapeHook)))
            {
                bytesWritten++;
                stream.PutB((byte)'\\');
                if (_outEscPatterns.FoundAtNextChar((byte)'\\') != -1)
                    _noOutEsc = true;
            }

            bool ok = stream.PutB(b);
            if (_outEscPatterns.FoundAtNextChar(b) != -1)
                _noOutEsc = true;

            if (_noOutEsc && (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n'))
                _noOutEsc = false;

            _escapeBowl = false;
            _escapeRoof = false;
            _escapeHook = false;
            _escapeTone = false;

            return ok;
        }
    }
}
