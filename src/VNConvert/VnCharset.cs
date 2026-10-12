namespace VNConvert;

/// <summary>
/// Supported Vietnamese character encoding formats.
/// Matches the original vnconv character set identifiers.
/// </summary>
public enum VnCharsetId
{
    /// <summary>Unicode (UTF-16 LE)</summary>
    Unicode = 0,

    /// <summary>Unicode (UTF-8)</summary>
    Utf8 = 1,

    /// <summary>Numeric Character Reference Decimal: &amp;#D;</summary>
    UnicodeRef = 2,

    /// <summary>Numeric Character Reference Hexadecimal: &amp;#xH;</summary>
    UnicodeRefHex = 3,

    /// <summary>Unicode Tổ hợp / Decomposed (UTF-16 LE)</summary>
    UnicodeDecomposed = 4,

    /// <summary>Windows-1258 (Vietnamese Code Page)</summary>
    WinCp1258 = 5,

    /// <summary>Unicode C-String escape sequence: \xHHHH</summary>
    UnicodeCString = 6,

    /// <summary>Vietnamese Quoted-Readable (VIQR)</summary>
    Viqr = 10,

    /// <summary>UTF-8 / VIQR auto-detected mixed encoding</summary>
    Utf8Viqr = 11,

    /// <summary>TCVN3 (ABC) single-byte encoding</summary>
    Tcvn3 = 20,

    /// <summary>VPS single-byte encoding</summary>
    Vps = 21,

    /// <summary>VISCII single-byte encoding</summary>
    Viscii = 22,

    /// <summary>Bach Khoa HCM 1 single-byte encoding</summary>
    Bkhcm1 = 23,

    /// <summary>Vietware-F single-byte encoding</summary>
    VietwareF = 24,

    /// <summary>ISC single-byte encoding</summary>
    Isc = 25,

    /// <summary>VNI-Windows double-byte encoding</summary>
    VniWin = 40,

    /// <summary>Bach Khoa HCM 2 double-byte encoding</summary>
    Bkhcm2 = 41,

    /// <summary>Vietware-X double-byte encoding</summary>
    VietwareX = 42,

    /// <summary>VNI-Macintosh double-byte encoding</summary>
    VniMac = 43
}

/// <summary>
/// Helpers and metadata for Vietnamese character sets.
/// </summary>
public static class VnCharsetHelper
{
    public const int TotalSingleByteCharsets = 6;
    public const int TotalDoubleByteCharsets = 4;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsSingleByteCharset(VnCharsetId id)
    {
        int val = (int)id;
        return val >= (int)VnCharsetId.Tcvn3 && val < (int)VnCharsetId.Tcvn3 + TotalSingleByteCharsets;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsDoubleByteCharset(VnCharsetId id)
    {
        int val = (int)id;
        return val >= (int)VnCharsetId.VniWin && val < (int)VnCharsetId.VniWin + TotalDoubleByteCharsets;
    }

    public static bool TryParse(string? name, out VnCharsetId charsetId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            charsetId = default;
            return false;
        }

        string normalized = name.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "UNICODE" or "UTF-16" or "UTF16" or "UTF-16LE" or "UTF16LE":
                charsetId = VnCharsetId.Unicode;
                return true;
            case "UTF-8" or "UTF8":
                charsetId = VnCharsetId.Utf8;
                return true;
            case "NCR-DEC" or "NCRDEC" or "UNIREF" or "DEC":
                charsetId = VnCharsetId.UnicodeRef;
                return true;
            case "NCR-HEX" or "NCRHEX" or "UNIREF-HEX" or "HEX":
                charsetId = VnCharsetId.UnicodeRefHex;
                return true;
            case "UNI-COMP" or "UNICOMP" or "COMPOSITE" or "DECOMPOSED" or "TOHOP":
                charsetId = VnCharsetId.UnicodeDecomposed;
                return true;
            case "WINCP-1258" or "WINCP1258" or "CP1258" or "WINDOWS-1258":
                charsetId = VnCharsetId.WinCp1258;
                return true;
            case "UNI-CSTRING" or "CSTRING":
                charsetId = VnCharsetId.UnicodeCString;
                return true;
            case "VIQR":
                charsetId = VnCharsetId.Viqr;
                return true;
            case "UVIQR" or "UTF8VIQR" or "UTF8-VIQR":
                charsetId = VnCharsetId.Utf8Viqr;
                return true;
            case "TCVN3" or "TCVN" or "ABC":
                charsetId = VnCharsetId.Tcvn3;
                return true;
            case "VPS":
                charsetId = VnCharsetId.Vps;
                return true;
            case "VISCII":
                charsetId = VnCharsetId.Viscii;
                return true;
            case "BKHCM1" or "BK1":
                charsetId = VnCharsetId.Bkhcm1;
                return true;
            case "VIETWARE-F" or "VIETWAREF":
                charsetId = VnCharsetId.VietwareF;
                return true;
            case "ISC":
                charsetId = VnCharsetId.Isc;
                return true;
            case "VNI-WIN" or "VNIWIN" or "VNI":
                charsetId = VnCharsetId.VniWin;
                return true;
            case "BKHCM2" or "BK2":
                charsetId = VnCharsetId.Bkhcm2;
                return true;
            case "VIETWARE-X" or "VIETWAREX":
                charsetId = VnCharsetId.VietwareX;
                return true;
            case "VNI-MAC" or "VNIMAC":
                charsetId = VnCharsetId.VniMac;
                return true;
            default:
                charsetId = default;
                return false;
        }
    }

    public static VnCharsetId Parse(string name)
    {
        if (TryParse(name, out var id))
            return id;
        throw new ArgumentException($"Unknown charset name: '{name}'", nameof(name));
    }
}
