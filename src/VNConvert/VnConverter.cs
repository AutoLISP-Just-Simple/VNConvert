using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VNConvert.Core;

namespace VNConvert;

/// <summary>
/// High-performance Vietnamese character encoding converter for .NET 8.
/// Native AOT compatible, zero-allocation span-based operations.
/// </summary>
public static class VnConverter
{
    private static readonly VnCharsetRegistry DefaultRegistry = new();
    private static VnConvOptions _globalOptions = VnConvOptions.Default;

    /// <summary>
    /// Global conversion options (compatible with original vnconv library options).
    /// </summary>
    public static VnConvOptions GlobalOptions
    {
        get => _globalOptions;
        set => _globalOptions = value;
    }

    /// <summary>
    /// Resets global options to their default values.
    /// </summary>
    public static void ResetGlobalOptions()
    {
        _globalOptions = VnConvOptions.Default;
    }

    /// <summary>
    /// Returns the human-readable error message for a given vnconv error code.
    /// </summary>
    public static string GetErrorMessage(int errorCode) => errorCode switch
    {
        0 => "No error",
        1 => "Unknown error",
        2 => "Invalid charset",
        3 => "Error opening input file",
        4 => "Error opening output file",
        5 => "Error writing to output stream",
        6 => "Not enough memory",
        _ => "Unknown error"
    };

    /// <summary>
    /// Core conversion loop between abstract byte streams.
    /// </summary>
    internal static int ConvertCore(
        ICharsetEngine inEngine,
        ICharsetEngine outEngine,
        IByteInStream inStream,
        IByteOutStream outStream,
        in VnConvOptions options)
    {
        inEngine.StartInput(options);
        outEngine.StartOutput(options);

        bool toLower = options.ToLower != 0;
        bool toUpper = options.ToUpper != 0;
        bool removeTone = options.RemoveTone != 0;

        while (!inStream.IsEos)
        {
            if (inEngine.NextInput(inStream, out uint stdChar, out _))
            {
                if (stdChar != StdVnChar.Invalid)
                {
                    if (toLower)
                        stdChar = StdVnChar.ToLower(stdChar);
                    else if (toUpper)
                        stdChar = StdVnChar.ToUpper(stdChar);

                    if (removeTone)
                        stdChar = StdVnChar.RemoveTone(stdChar);

                    outEngine.PutChar(outStream, stdChar, out _);
                }
            }
            else
            {
                break;
            }
        }

        return outStream.IsOk ? 0 : 5;
    }

    /// <summary>
    /// Converts bytes from input buffer to output buffer using the specified charsets.
    /// Matches the behavior of native VnConvert0.
    /// </summary>
    public static unsafe int Convert(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        VnCharsetId inCharset,
        VnCharsetId outCharset,
        out int bytesLeft,
        out int bytesWritten,
        VnConvOptions? options = null)
    {
        VnConvOptions opts = options ?? _globalOptions;

        var inEngine = DefaultRegistry.GetEngine(inCharset);
        var outEngine = DefaultRegistry.GetEngine(outCharset);

        if (inEngine == null || outEngine == null)
        {
            bytesLeft = input.Length;
            bytesWritten = 0;
            return 2; // VNCONV_INVALID_CHARSET
        }

        fixed (byte* pIn = input)
        fixed (byte* pOut = output)
        {
            var inStream = new SpanByteInStream(pIn, input.Length);
            var outStream = new SpanByteOutStream(pOut, output.Length);

            int ret = ConvertCore(inEngine, outEngine, inStream, outStream, opts);
            bytesLeft = inStream.Left;
            bytesWritten = outStream.BytesWritten;
            return ret;
        }
    }

    /// <summary>
    /// Converts a byte array from one charset to another, dynamically allocating the result buffer.
    /// </summary>
    public static unsafe byte[] Convert(
        ReadOnlySpan<byte> input,
        VnCharsetId inCharset,
        VnCharsetId outCharset,
        VnConvOptions? options = null)
    {
        VnConvOptions opts = options ?? _globalOptions;

        var inEngine = DefaultRegistry.GetEngine(inCharset);
        var outEngine = DefaultRegistry.GetEngine(outCharset);

        if (inEngine == null || outEngine == null)
            throw new ArgumentException("Invalid input or output character set.");

        fixed (byte* pIn = input)
        {
            var inStream = new SpanByteInStream(pIn, input.Length);
            using var outStream = new GrowingByteOutStream(Math.Max(input.Length, 64));

            int ret = ConvertCore(inEngine, outEngine, inStream, outStream, opts);
            if (ret != 0 && ret != 5)
                throw new InvalidOperationException($"Conversion failed with error code: {ret}");

            return outStream.ToArray();
        }
    }

    /// <summary>
    /// Converts a text string between character sets.
    /// </summary>
    public static string Convert(
        string text,
        VnCharsetId inCharset,
        VnCharsetId outCharset,
        VnConvOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Encode input text to bytes according to inCharset
        byte[] inputBytes = EncodeStringToCharsetBytes(text, inCharset);

        // Perform encoding conversion
        byte[] outputBytes = Convert(inputBytes, inCharset, outCharset, options);

        // Decode output bytes back to string according to outCharset
        return DecodeCharsetBytesToString(outputBytes, outCharset);
    }

    /// <summary>
    /// Converts data between two streams.
    /// </summary>
    public static void Convert(
        Stream inputStream,
        Stream outputStream,
        VnCharsetId inCharset,
        VnCharsetId outCharset,
        VnConvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);

        VnConvOptions opts = options ?? _globalOptions;

        var inEngine = DefaultRegistry.GetEngine(inCharset);
        var outEngine = DefaultRegistry.GetEngine(outCharset);

        if (inEngine == null || outEngine == null)
            throw new ArgumentException("Invalid input or output character set.");

        // If target is Unicode UTF-16, write BOM as original vnconv does
        if (outCharset == VnCharsetId.Unicode)
        {
            outputStream.Write([0xFF, 0xFE]); // UTF-16 LE BOM
        }

        var inStream = new StreamByteInStream(inputStream);
        using var outStream = new StreamByteOutStream(outputStream);

        int ret = ConvertCore(inEngine, outEngine, inStream, outStream, opts);
        outStream.Flush();

        if (ret != 0)
            throw new InvalidOperationException($"Stream conversion failed with error code {ret}: {GetErrorMessage(ret)}");
    }

    /// <summary>
    /// Converts an input file to an output file.
    /// Matches the atomic file replacement behavior of native VnFileConvert.
    /// </summary>
    public static void ConvertFile(
        string inputFile,
        string outputFile,
        VnCharsetId inCharset,
        VnCharsetId outCharset,
        VnConvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inputFile);
        ArgumentNullException.ThrowIfNull(outputFile);

        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file not found", inputFile);

        string? outDir = Path.GetDirectoryName(Path.GetFullPath(outputFile));
        if (string.IsNullOrEmpty(outDir))
            outDir = Directory.GetCurrentDirectory();

        string tempFile = Path.Combine(outDir, $"vnconv_{Guid.NewGuid():N}.tmp");

        try
        {
            using (var inStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Convert(inStream, outStream, inCharset, outCharset, options);
            }

            // Atomic move / replace
            File.Move(tempFile, outputFile, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            throw;
        }
    }

    /// <summary>
    /// Removes tone marks (bỏ dấu) from a Vietnamese string.
    /// </summary>
    public static string RemoveTone(string text)
    {
        return Convert(text, VnCharsetId.Unicode, VnCharsetId.Unicode, new VnConvOptions { RemoveTone = 1 });
    }

    /// <summary>
    /// Converts a Vietnamese string to uppercase respecting Vietnamese tone rules.
    /// </summary>
    public static string ToUpper(string text)
    {
        return Convert(text, VnCharsetId.Unicode, VnCharsetId.Unicode, new VnConvOptions { ToUpper = 1 });
    }

    /// <summary>
    /// Converts a Vietnamese string to lowercase respecting Vietnamese tone rules.
    /// </summary>
    public static string ToLower(string text)
    {
        return Convert(text, VnCharsetId.Unicode, VnCharsetId.Unicode, new VnConvOptions { ToLower = 1 });
    }

    internal static byte[] EncodeStringToCharsetBytes(string text, VnCharsetId charset)
    {
        switch (charset)
        {
            case VnCharsetId.Unicode:
                return MemoryMarshal.AsBytes(text.AsSpan()).ToArray();

            case VnCharsetId.Utf8:
                return Encoding.UTF8.GetBytes(text);

            case VnCharsetId.UnicodeDecomposed:
                return Convert(MemoryMarshal.AsBytes(text.AsSpan()), VnCharsetId.Unicode, VnCharsetId.UnicodeDecomposed);

            default:
                // For legacy single/double-byte charsets or VIQR represented as a string:
                // Map each char (including Windows-1252 0x80..0x9F glyphs) back to its 8-bit byte value
                byte[] raw = new byte[text.Length];
                bool allAnsiChars = true;
                for (int i = 0; i < text.Length; i++)
                {
                    if (!TryMapCharToAnsiByte(text[i], out byte b))
                    {
                        allAnsiChars = false;
                        break;
                    }
                    raw[i] = b;
                }

                if (allAnsiChars)
                    return raw;

                // If string contains real Unicode Vietnamese characters, convert from Unicode to the target charset
                return Convert(MemoryMarshal.AsBytes(text.AsSpan()), VnCharsetId.Unicode, charset);
        }
    }

    internal static string DecodeCharsetBytesToString(ReadOnlySpan<byte> bytes, VnCharsetId charset)
    {
        switch (charset)
        {
            case VnCharsetId.Unicode:
                int charCount = bytes.Length / 2;
                return new string(MemoryMarshal.Cast<byte, char>(bytes.Slice(0, charCount * 2)));

            case VnCharsetId.Utf8:
                return Encoding.UTF8.GetString(bytes);

            case VnCharsetId.UnicodeDecomposed:
                int compCharCount = bytes.Length / 2;
                return new string(MemoryMarshal.Cast<byte, char>(bytes.Slice(0, compCharCount * 2)));

            default:
                // Map each byte to its Windows-1252 / Latin-1 char representation
                // so Windows applications (Word, Excel, AutoCAD) render legacy fonts (.VnTime, VNI-Times) properly
                char[] chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                {
                    chars[i] = MapAnsiByteToChar(bytes[i]);
                }
                return new string(chars);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMapCharToAnsiByte(char c, out byte b)
    {
        if (c <= 0xFF)
        {
            b = (byte)c;
            return true;
        }

        b = c switch
        {
            '\u20AC' => 0x80,
            '\u201A' or '\u20A1' => 0x82,
            '\u0192' => 0x83,
            '\u201E' => 0x84,
            '\u2026' => 0x85,
            '\u2020' => 0x86,
            '\u2021' => 0x87,
            '\u02C6' => 0x88,
            '\u2030' => 0x89,
            '\u0160' => 0x8A,
            '\u2039' => 0x8B,
            '\u0152' => 0x8C,
            '\u017D' => 0x8E,
            '\u2018' => 0x91,
            '\u2019' => 0x92,
            '\u201C' => 0x93,
            '\u201D' => 0x94,
            '\u2022' => 0x95,
            '\u2013' => 0x96,
            '\u2014' => 0x97,
            '\u02DC' => 0x98,
            '\u2122' => 0x99,
            '\u0161' => 0x9A,
            '\u203A' => 0x9B,
            '\u0153' => 0x9C,
            '\u017E' => 0x9E,
            '\u0178' => 0x9F,
            _ => 0
        };
        return b != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char MapAnsiByteToChar(byte b) => b switch
    {
        0x80 => '\u20AC',
        0x82 => '\u201A',
        0x83 => '\u0192',
        0x84 => '\u201E',
        0x85 => '\u2026',
        0x86 => '\u2020',
        0x87 => '\u2021',
        0x88 => '\u02C6',
        0x89 => '\u2030',
        0x8A => '\u0160',
        0x8B => '\u2039',
        0x8C => '\u0152',
        0x8E => '\u017D',
        0x91 => '\u2018',
        0x92 => '\u2019',
        0x93 => '\u201C',
        0x94 => '\u201D',
        0x95 => '\u2022',
        0x96 => '\u2013',
        0x97 => '\u2014',
        0x98 => '\u02DC',
        0x99 => '\u2122',
        0x9A => '\u0161',
        0x9B => '\u203A',
        0x9C => '\u0153',
        0x9E => '\u017E',
        0x9F => '\u0178',
        _ => (char)b
    };
}
