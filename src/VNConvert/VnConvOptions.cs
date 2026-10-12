using System.Runtime.InteropServices;

namespace VNConvert;

/// <summary>
/// Options for Vietnamese encoding conversion.
/// Binary layout is compatible with the native C VnConvOptions struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VnConvOptions : IEquatable<VnConvOptions>
{
    /// <summary>
    /// If 1 (true), VIQR mixed mode is enabled.
    /// </summary>
    public int ViqrMixed;

    /// <summary>
    /// If 1 (true), VIQR escape sequences (\, @, http, email, etc.) are processed. Default is 1.
    /// </summary>
    public int ViqrEsc;

    /// <summary>
    /// If 1 (true), converts text to uppercase.
    /// </summary>
    public int ToUpper;

    /// <summary>
    /// If 1 (true), converts text to lowercase.
    /// </summary>
    public int ToLower;

    /// <summary>
    /// If 1 (true), removes Vietnamese tone marks / diacritics (bỏ dấu tiếng Việt).
    /// </summary>
    public int RemoveTone;

    public static VnConvOptions Default => new()
    {
        ViqrMixed = 0,
        ViqrEsc = 1,
        ToUpper = 0,
        ToLower = 0,
        RemoveTone = 0
    };

    public bool Equals(VnConvOptions other) =>
        ViqrMixed == other.ViqrMixed &&
        ViqrEsc == other.ViqrEsc &&
        ToUpper == other.ToUpper &&
        ToLower == other.ToLower &&
        RemoveTone == other.RemoveTone;

    public override bool Equals(object? obj) => obj is VnConvOptions other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ViqrMixed, ViqrEsc, ToUpper, ToLower, RemoveTone);

    public static bool operator ==(VnConvOptions left, VnConvOptions right) => left.Equals(right);
    public static bool operator !=(VnConvOptions left, VnConvOptions right) => !left.Equals(right);
}
