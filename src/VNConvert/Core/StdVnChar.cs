using System.Runtime.CompilerServices;

namespace VNConvert.Core;

internal static class StdVnChar
{
    public const uint Offset = VnTables.VnStdCharOffset; // 0x10000
    public const uint Invalid = VnTables.InvalidStdChar; // 0xFFFFFFFF
    public const byte PadChar = (byte)'#';
    public const byte PadStartQuote = (byte)'"';
    public const byte PadEndQuote = (byte)'"';
    public const byte PadEllipsis = (byte)'.';

    public const uint StdStartQuote = Offset + 201;
    public const uint StdEndQuote = Offset + 202;
    public const uint StdEllipsis = Offset + 190;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUpper(uint ch)
    {
        if (ch >= Offset && ch < (Offset + VnTables.TotalAlphaVnChars) && (ch & 1) != 0)
        {
            return ch - 1;
        }
        return ch;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToLower(uint ch)
    {
        if (ch >= Offset && ch < (Offset + VnTables.TotalAlphaVnChars) && (ch & 1) == 0)
        {
            return ch + 1;
        }
        return ch;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint RemoveTone(uint ch)
    {
        if (ch >= Offset && ch < (Offset + VnTables.TotalVnChars))
        {
            int index = (int)(ch - Offset);
            return Offset + (uint)VnTables.StdVnNoToneTable[index];
        }
        return ch;
    }
}
