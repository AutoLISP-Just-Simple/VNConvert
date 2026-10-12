using System.Text;
using VNConvert;

namespace VNConvert.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        string? inFile = null;
        string? outFile = null;
        string? inputText = null;
        string fromCharsetStr = "UTF-8";
        string toCharsetStr = "UTF-8";
        var options = VnConvOptions.Default;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    PrintUsage();
                    return 0;

                case "--list":
                    PrintCharsetList();
                    return 0;

                case "-i" or "--in":
                    if (i + 1 >= args.Length) return Error("Missing value for " + arg);
                    inFile = args[++i];
                    break;

                case "-o" or "--out":
                    if (i + 1 >= args.Length) return Error("Missing value for " + arg);
                    outFile = args[++i];
                    break;

                case "-f" or "--from":
                    if (i + 1 >= args.Length) return Error("Missing value for " + arg);
                    fromCharsetStr = args[++i];
                    break;

                case "-t" or "--to":
                    if (i + 1 >= args.Length) return Error("Missing value for " + arg);
                    toCharsetStr = args[++i];
                    break;

                case "-u" or "--upper":
                    options.ToUpper = 1;
                    options.ToLower = 0;
                    break;

                case "-l" or "--lower":
                    options.ToLower = 1;
                    options.ToUpper = 0;
                    break;

                case "-r" or "--remove-tone":
                    options.RemoveTone = 1;
                    break;

                case "--viqr-mixed":
                    options.ViqrMixed = 1;
                    break;

                case "--no-viqr-esc":
                    options.ViqrEsc = 0;
                    break;

                default:
                    if (arg.StartsWith('-'))
                        return Error($"Unknown option: {arg}");
                    inputText = arg;
                    break;
            }
        }

        if (!VnCharsetHelper.TryParse(fromCharsetStr, out var inCharset))
            return Error($"Invalid source charset: '{fromCharsetStr}'. Use --list to see supported charsets.");

        if (!VnCharsetHelper.TryParse(toCharsetStr, out var outCharset))
            return Error($"Invalid target charset: '{toCharsetStr}'. Use --list to see supported charsets.");

        try
        {
            if (inputText != null)
            {
                string converted = VnConverter.Convert(inputText, inCharset, outCharset, options);
                if (outFile != null)
                {
                    File.WriteAllText(outFile, converted, Encoding.UTF8);
                }
                else
                {
                    Console.WriteLine(converted);
                }
            }
            else if (inFile != null && outFile != null)
            {
                VnConverter.ConvertFile(inFile, outFile, inCharset, outCharset, options);
            }
            else
            {
                using Stream inStream = inFile != null
                    ? File.OpenRead(inFile)
                    : Console.OpenStandardInput();

                using Stream outStream = outFile != null
                    ? File.Create(outFile)
                    : Console.OpenStandardOutput();

                VnConverter.Convert(inStream, outStream, inCharset, outCharset, options);
            }

            return 0;
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            VNConvert 2.0 (.NET 8.0 Native AOT) - Vietnamese Encoding Converter
            Usage:
              vnconv [options] [text]
              vnconv -f <from-charset> -t <to-charset> -i <input-file> -o <output-file> [options]

            Options:
              -f, --from <charset>   Source charset (default: UTF-8)
              -t, --to <charset>     Target charset (default: UTF-8)
              -i, --in <file>        Input file path (stdin if omitted and no text provided)
              -o, --out <file>       Output file path (stdout if omitted)
              -u, --upper            Convert to UPPERCASE
              -l, --lower            Convert to lowercase
              -r, --remove-tone      Remove Vietnamese tone marks / accents (bỏ dấu)
              --viqr-mixed           Enable VIQR mixed mode
              --no-viqr-esc          Disable VIQR URL/email escape detection
              --list                 List all supported character sets
              -h, --help             Show this help message
            """);
    }

    private static void PrintCharsetList()
    {
        Console.WriteLine("""
            Supported Vietnamese Character Sets:
              ID   Name          Description
              ------------------------------------------------------------
               0   UNICODE       Unicode UTF-16 LE
               1   UTF-8         Unicode UTF-8
               2   NCR-DEC       Numeric Character Reference Decimal (&#D;)
               3   NCR-HEX       Numeric Character Reference Hex (&#xH;)
               4   UNI-COMP      Unicode Decomposed / Tổ hợp (UTF-16 LE)
               5   WINCP-1258    Windows Code Page 1258
               6   UNI-CSTRING   C-String Hex Escape (\xHHHH)
              10   VIQR          Vietnamese Quoted-Readable
              11   UVIQR         UTF-8 / VIQR Mixed
              20   TCVN3         TCVN3 (ABC) 1-byte
              21   VPS           VPS 1-byte
              22   VISCII        VISCII 1-byte
              23   BKHCM1        Bách Khoa HCM 1 (1-byte)
              24   VIETWARE-F    Vietware-F (1-byte)
              25   ISC           ISC (1-byte)
              40   VNI-WIN       VNI-Windows (2-byte)
              41   BKHCM2        Bách Khoa HCM 2 (2-byte)
              42   VIETWARE-X    Vietware-X (2-byte)
              43   VNI-MAC       VNI-Macintosh (2-byte)
            """);
    }
}
