using System.Text;
using VNConvert;
using Xunit;

namespace VNConvert.Tests;

public class VnConverterTests
{
    private const string SampleLower = "trăm năm trong cõi người ta, chữ tài chữ mệnh khéo là ghét nhau.";
    private const string SampleMixed = "Cộng hòa Xã hội Chủ nghĩa Việt Nam - Độc lập, Tự do, Hạnh phúc!";

    [Fact]
    public void Convert_Utf8_To_Tcvn3_And_Back()
    {
        // TCVN3 only has full tone support for lowercase vowels
        string tcvn3 = VnConverter.Convert(SampleLower, VnCharsetId.Utf8, VnCharsetId.Tcvn3);
        Assert.NotEqual(SampleLower, tcvn3);

        string backToUtf8 = VnConverter.Convert(tcvn3, VnCharsetId.Tcvn3, VnCharsetId.Utf8);
        Assert.Equal(SampleLower, backToUtf8);
    }

    [Fact]
    public void Convert_Utf8_To_VniWin_And_Back_PreservesCase()
    {
        string vni = VnConverter.Convert(SampleMixed, VnCharsetId.Utf8, VnCharsetId.VniWin);
        Assert.NotEqual(SampleMixed, vni);

        string backToUtf8 = VnConverter.Convert(vni, VnCharsetId.VniWin, VnCharsetId.Utf8);
        Assert.Equal(SampleMixed, backToUtf8);
    }

    [Fact]
    public void Convert_UserTcvnSample_To_VniWin_And_Utf8()
    {
        const string tcvnInput = "®o ®¹c theo ranh giíi hiÖn tr¹ng sö dông ®Êt vµ x¸c nhËn diÖn tÝch thöa ®Êt sè :        thuéc ";
        const string expectedUnicode = "đo đạc theo ranh giới hiện trạng sử dụng đất và xác nhận diện tích thửa đất số :        thuộc ";
        const string expectedVni = "ño ñaïc theo ranh giôùi hieän traïng söû duïng ñaát vaø xaùc nhaän dieän tích thöûa ñaát soá :        thuoäc ";

        string actualUnicode = VnConverter.Convert(tcvnInput, VnCharsetId.Tcvn3, VnCharsetId.Utf8);
        string actualVni = VnConverter.Convert(tcvnInput, VnCharsetId.Tcvn3, VnCharsetId.VniWin);
        string roundTripTcvn = VnConverter.Convert(actualVni, VnCharsetId.VniWin, VnCharsetId.Tcvn3);

        Assert.Equal(expectedUnicode, actualUnicode);
        Assert.Equal(expectedVni, actualVni);
        Assert.Equal(tcvnInput, roundTripTcvn);
    }

    [Theory]
    [InlineData(VnCharsetId.Unicode)]
    [InlineData(VnCharsetId.UnicodeDecomposed)]
    [InlineData(VnCharsetId.UnicodeRef)]
    [InlineData(VnCharsetId.UnicodeRefHex)]
    [InlineData(VnCharsetId.UnicodeCString)]
    [InlineData(VnCharsetId.WinCp1258)]
    [InlineData(VnCharsetId.Viqr)]
    [InlineData(VnCharsetId.Viscii)]
    [InlineData(VnCharsetId.Vps)]
    [InlineData(VnCharsetId.VniWin)]
    [InlineData(VnCharsetId.Bkhcm2)]
    [InlineData(VnCharsetId.VietwareX)]
    [InlineData(VnCharsetId.VniMac)]
    public void RoundTrip_FullMixedCaseCharsets_Succeeds(VnCharsetId targetCharset)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(SampleMixed);
        byte[] encoded = VnConverter.Convert(utf8Bytes, VnCharsetId.Utf8, targetCharset);
        byte[] decoded = VnConverter.Convert(encoded, targetCharset, VnCharsetId.Utf8);

        string result = Encoding.UTF8.GetString(decoded);
        Assert.Equal(SampleMixed, result);
    }

    [Theory]
    [InlineData(VnCharsetId.Tcvn3)]
    [InlineData(VnCharsetId.Bkhcm1)]
    [InlineData(VnCharsetId.VietwareF)]
    [InlineData(VnCharsetId.Isc)]
    public void RoundTrip_SingleByteLowerCharsets_Succeeds(VnCharsetId targetCharset)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(SampleLower);
        byte[] encoded = VnConverter.Convert(utf8Bytes, VnCharsetId.Utf8, targetCharset);
        byte[] decoded = VnConverter.Convert(encoded, targetCharset, VnCharsetId.Utf8);

        string result = Encoding.UTF8.GetString(decoded);
        Assert.Equal(SampleLower, result);
    }

    [Fact]
    public void RemoveTone_StripsAllVietnameseDiacritics()
    {
        string input = "Cộng hòa Xã hội Chủ nghĩa Việt Nam - Độc lập, Tự do, Hạnh phúc!";
        string expected = "Cong hoa Xa hoi Chu nghia Viet Nam - Doc lap, Tu do, Hanh phuc!";

        string actual = VnConverter.RemoveTone(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToUpper_And_ToLower_WorkAccuratelyForVietnamese()
    {
        string input = "Tiếng Việt Đậm Đà Bản Sắc Dân Tộc";
        string upper = VnConverter.ToUpper(input);
        string lower = VnConverter.ToLower(input);

        Assert.Equal("TIẾNG VIỆT ĐẬM ĐÀ BẢN SẮC DÂN TỘC", upper);
        Assert.Equal("tiếng việt đậm đà bản sắc dân tộc", lower);
    }

    [Fact]
    public void Viqr_Conversion_And_UrlEscape_Works()
    {
        string viqrInput = "To^i ye^u Tie^'ng Vie^.t! Xem ta.i http://example.com/a?b=c&d=e.f hoa(.c mailto:test@vn.org.vn";
        byte[] viqrBytes = Encoding.ASCII.GetBytes(viqrInput);
        byte[] utf8Bytes = VnConverter.Convert(viqrBytes, VnCharsetId.Viqr, VnCharsetId.Utf8);
        string utf8 = Encoding.UTF8.GetString(utf8Bytes);

        Assert.Equal("Tôi yêu Tiếng Việt! Xem tại http://example.com/a?b=c&d=e.f hoặc mailto:test@vn.org.vn", utf8);
    }

    [Fact]
    public void NcrDecimal_And_Hex_FormatProperly()
    {
        string text = "Việt";
        byte[] ncrDec = VnConverter.Convert(Encoding.UTF8.GetBytes(text), VnCharsetId.Utf8, VnCharsetId.UnicodeRef);
        string decStr = Encoding.ASCII.GetString(ncrDec);
        Assert.Equal("Vi&#7879;t", decStr);

        byte[] ncrHex = VnConverter.Convert(Encoding.UTF8.GetBytes(text), VnCharsetId.Utf8, VnCharsetId.UnicodeRefHex);
        string hexStr = Encoding.ASCII.GetString(ncrHex);
        Assert.Equal("Vi&#x1EC7;t", hexStr);
    }

    [Fact]
    public void ConvertFile_CreatesConvertedOutputAtomically()
    {
        string tempIn = Path.GetTempFileName();
        string tempOut = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempIn, SampleMixed, Encoding.UTF8);
            VnConverter.ConvertFile(tempIn, tempOut, VnCharsetId.Utf8, VnCharsetId.VniWin);

            byte[] vniBytes = File.ReadAllBytes(tempOut);
            byte[] backBytes = VnConverter.Convert(vniBytes, VnCharsetId.VniWin, VnCharsetId.Utf8);
            string restored = Encoding.UTF8.GetString(backBytes);

            Assert.Equal(SampleMixed, restored);
        }
        finally
        {
            if (File.Exists(tempIn)) File.Delete(tempIn);
            if (File.Exists(tempOut)) File.Delete(tempOut);
        }
    }

    [Fact]
    public void Convert_SmallBuffer_ReturnsErrorAndReportsRequiredBytes()
    {
        byte[] input = Encoding.UTF8.GetBytes(SampleMixed);
        byte[] tinyOutput = new byte[5];

        int err = VnConverter.Convert(
            input,
            tinyOutput,
            VnCharsetId.Utf8,
            VnCharsetId.Utf8,
            out _,
            out int bytesNeeded);

        Assert.Equal(5, err); // Error writing / buffer too small
        Assert.Equal(input.Length, bytesNeeded);
    }
}
