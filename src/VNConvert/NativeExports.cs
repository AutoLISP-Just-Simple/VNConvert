using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VNConvert.Core;

namespace VNConvert;

/// <summary>
/// C-ABI exports providing 100% binary compatibility with the original native vnconv.dll.
/// Built with [UnmanagedCallersOnly] for .NET 8 Native AOT.
/// </summary>
public static unsafe class NativeExports
{
    private static readonly VnCharsetRegistry Registry = new();

    private static readonly byte[][] ErrorMessagesUtf8 =
    [
        "No error\0"u8.ToArray(),
        "Unknown error\0"u8.ToArray(),
        "Invalid charset\0"u8.ToArray(),
        "Error opening input file\0"u8.ToArray(),
        "Error opening output file\0"u8.ToArray(),
        "Error writing to output stream\0"u8.ToArray(),
        "Not enough memory\0"u8.ToArray()
    ];

    [UnmanagedCallersOnly(EntryPoint = "VnConvert01")]
    public static int VnConvert01() => 0;

    [UnmanagedCallersOnly(EntryPoint = "VnConvert")]
    public static int VnConvert(int inCharset, int outCharset, byte* input, byte* output, int inlen, int outlen)
    {
        int pInLen = inlen;
        int pMaxOutLen = outlen;
        return Convert0Internal(inCharset, outCharset, input, output, &pInLen, &pMaxOutLen);
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvert0")]
    public static int VnConvert0(int inCharset, int outCharset, byte* input, byte* output, int* pInLen, int* pMaxOutLen)
    {
        return Convert0Internal(inCharset, outCharset, input, output, pInLen, pMaxOutLen);
    }

    private static int Convert0Internal(int inCharset, int outCharset, byte* input, byte* output, int* pInLen, int* pMaxOutLen)
    {
        if (pInLen == null || pMaxOutLen == null)
            return 1; // VNCONV_UNKNOWN_ERROR

        int inLen = *pInLen;
        int maxOutLen = *pMaxOutLen;

        if (inLen != -1 && inLen < 0)
            return -1;

        var inEngine = Registry.GetEngine((VnCharsetId)inCharset);
        var outEngine = Registry.GetEngine((VnCharsetId)outCharset);

        if (inEngine == null || outEngine == null)
            return 2; // VNCONV_INVALID_CHARSET

        var inStream = new SpanByteInStream(input, inLen);
        var outStream = new SpanByteOutStream(output, maxOutLen);

        int ret = VnConverter.ConvertCore(inEngine, outEngine, inStream, outStream, VnConverter.GlobalOptions);

        *pMaxOutLen = outStream.BytesWritten;
        *pInLen = inStream.Left;
        return ret;
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvertSB")]
    public static int VnConvertSB(int inCharset, int outCharset, byte* input, byte* output, int strlen)
    {
        return ConvertSbInternal(inCharset, outCharset, input, output, strlen);
    }

    private static int ConvertSbInternal(int inCharset, int outCharset, byte* input, byte* output, int strlen)
    {
        if (input == null || output == null || strlen <= 0)
            return -1;

        int inLen = -1;
        int maxOutLen = strlen - 1; // Leave 1 byte for null terminator
        int ret = Convert0Internal(inCharset, outCharset, input, output, &inLen, &maxOutLen);

        int written = Math.Min(maxOutLen, strlen - 1);
        output[written] = 0; // Null terminator
        return ret;
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvertChar")]
    public static int VnConvertChar(int inCharset, int outCharset, byte* input, byte* output, int strlen)
    {
        return ConvertSbInternal(inCharset, outCharset, input, output, strlen);
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvertStr")]
    public static int VnConvertStr(int inCharset, int outCharset, byte* input)
    {
        if (input == null)
            return -1;

        byte* dummyOutput = stackalloc byte[800];
        int inLen = -1;
        int maxOutLen = 800;
        return Convert0Internal(inCharset, outCharset, input, dummyOutput, &inLen, &maxOutLen);
    }

    [UnmanagedCallersOnly(EntryPoint = "VnFileConvert")]
    public static int VnFileConvert(int inCharset, int outCharset, byte* inFile, byte* outFile)
    {
        try
        {
            string? inPath = inFile != null ? Marshal.PtrToStringAnsi((nint)inFile) : null;
            string? outPath = outFile != null ? Marshal.PtrToStringAnsi((nint)outFile) : null;

            Stream inStream;
            bool ownsIn = false;
            if (string.IsNullOrEmpty(inPath))
            {
                inStream = Console.OpenStandardInput();
            }
            else
            {
                if (!File.Exists(inPath))
                    return 3; // VNCONV_ERR_INPUT_FILE
                inStream = new FileStream(inPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                ownsIn = true;
            }

            Stream outStream;
            bool ownsOut = false;
            string? tempOutPath = null;

            if (string.IsNullOrEmpty(outPath))
            {
                outStream = Console.OpenStandardOutput();
            }
            else
            {
                string? outDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (string.IsNullOrEmpty(outDir))
                    outDir = Directory.GetCurrentDirectory();

                tempOutPath = Path.Combine(outDir, $"vnconv_{Guid.NewGuid():N}.tmp");
                outStream = new FileStream(tempOutPath, FileMode.Create, FileAccess.Write, FileShare.None);
                ownsOut = true;
            }

            try
            {
                VnConverter.Convert(inStream, outStream, (VnCharsetId)inCharset, (VnCharsetId)outCharset);
            }
            finally
            {
                if (ownsIn) inStream.Dispose();
                if (ownsOut) outStream.Dispose();
            }

            if (tempOutPath != null && outPath != null)
            {
                File.Move(tempOutPath, outPath, overwrite: true);
            }

            return 0;
        }
        catch
        {
            return 4; // VNCONV_ERR_OUTPUT_FILE
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvertString")]
    public static nint VnConvertString(int inCharset, int outCharset, byte* input, int inlen)
    {
        if (input == null)
            return 0;

        ReadOnlySpan<byte> inSpan = inlen >= 0
            ? new ReadOnlySpan<byte>(input, inlen)
            : MemoryMarshal.CreateReadOnlySpanFromNullTerminated(input);

        var targetCharset = (VnCharsetId)outCharset;
        byte[] converted = VnConverter.Convert(inSpan, (VnCharsetId)inCharset, targetCharset);
        string result = VnConverter.DecodeCharsetBytesToString(converted, targetCharset);

        return Marshal.StringToBSTR(result);
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvErrMsg")]
    public static byte* VnConvErrMsg(int errCode)
    {
        if (errCode < 0 || errCode >= ErrorMessagesUtf8.Length)
            errCode = 1; // VNCONV_UNKNOWN_ERROR

        fixed (byte* p = ErrorMessagesUtf8[errCode])
        {
            return p;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvSetOptions")]
    public static void VnConvSetOptions(VnConvOptions* pOptions)
    {
        if (pOptions != null)
            VnConverter.GlobalOptions = *pOptions;
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvGetOptions")]
    public static void VnConvGetOptions(VnConvOptions* pOptions)
    {
        if (pOptions != null)
            *pOptions = VnConverter.GlobalOptions;
    }

    [UnmanagedCallersOnly(EntryPoint = "VnConvResetOptions")]
    public static void VnConvResetOptions(VnConvOptions* pOptions)
    {
        if (pOptions != null)
            *pOptions = VnConvOptions.Default;
        VnConverter.ResetGlobalOptions();
    }
}
