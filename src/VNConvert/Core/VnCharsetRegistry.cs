namespace VNConvert.Core;

internal sealed class VnCharsetRegistry
{
    private readonly SingleByteEngine?[] _sgEngines = new SingleByteEngine?[VnCharsetHelper.TotalSingleByteCharsets];
    private readonly DoubleByteEngine?[] _dbEngines = new DoubleByteEngine?[VnCharsetHelper.TotalDoubleByteCharsets];

    private UnicodeEngine? _unicode;
    private Utf8Engine? _utf8;
    private UnicodeCompEngine? _uniComp;
    private WinCp1258Engine? _winCp1258;
    private UnicodeRefEngine? _uniRef;
    private UnicodeHexEngine? _uniHex;
    private UnicodeCStringEngine? _uniCString;
    private ViqrEngine? _viqr;
    private Utf8ViqrEngine? _utf8Viqr;

    public ICharsetEngine? GetEngine(VnCharsetId charsetId)
    {
        switch (charsetId)
        {
            case VnCharsetId.Unicode:
                return _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);

            case VnCharsetId.Utf8:
                _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);
                return _utf8 ??= new Utf8Engine(_unicode, VnTables.UnicodeTable);

            case VnCharsetId.UnicodeDecomposed:
                return _uniComp ??= new UnicodeCompEngine(VnTables.UnicodeTable, VnTables.UnicodeCompositeTable);

            case VnCharsetId.UnicodeRef:
                _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);
                return _uniRef ??= new UnicodeRefEngine(_unicode, VnTables.UnicodeTable);

            case VnCharsetId.UnicodeRefHex:
                _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);
                return _uniHex ??= new UnicodeHexEngine(_unicode, VnTables.UnicodeTable);

            case VnCharsetId.UnicodeCString:
                _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);
                return _uniCString ??= new UnicodeCStringEngine(_unicode, VnTables.UnicodeTable);

            case VnCharsetId.WinCp1258:
                return _winCp1258 ??= new WinCp1258Engine(VnTables.WinCp1258Table, VnTables.WinCp1258PreTable);

            case VnCharsetId.Viqr:
                return _viqr ??= new ViqrEngine(VnTables.ViqrTable);

            case VnCharsetId.Utf8Viqr:
                _unicode ??= new UnicodeEngine(VnTables.UnicodeTable);
                _utf8 ??= new Utf8Engine(_unicode, VnTables.UnicodeTable);
                _viqr ??= new ViqrEngine(VnTables.ViqrTable);
                return _utf8Viqr ??= new Utf8ViqrEngine(_utf8, _viqr);

            default:
                if (VnCharsetHelper.IsSingleByteCharset(charsetId))
                {
                    int idx = (int)charsetId - (int)VnCharsetId.Tcvn3;
                    if (idx >= 0 && idx < _sgEngines.Length)
                    {
                        return _sgEngines[idx] ??= CreateSingleByteEngine(idx);
                    }
                }
                else if (VnCharsetHelper.IsDoubleByteCharset(charsetId))
                {
                    int idx = (int)charsetId - (int)VnCharsetId.VniWin;
                    if (idx >= 0 && idx < _dbEngines.Length)
                    {
                        return _dbEngines[idx] ??= CreateDoubleByteEngine(idx);
                    }
                }
                return null;
        }
    }

    private static SingleByteEngine CreateSingleByteEngine(int index) => index switch
    {
        0 => new SingleByteEngine(VnTables.Tcvn3Table),
        1 => new SingleByteEngine(VnTables.VpsTable),
        2 => new SingleByteEngine(VnTables.VisciiTable),
        3 => new SingleByteEngine(VnTables.Bkhcm1Table),
        4 => new SingleByteEngine(VnTables.VietwareFTable),
        5 => new SingleByteEngine(VnTables.IscTable),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    private static DoubleByteEngine CreateDoubleByteEngine(int index) => index switch
    {
        0 => new DoubleByteEngine(VnTables.VniWinTable),
        1 => new DoubleByteEngine(VnTables.Bkhcm2Table),
        2 => new DoubleByteEngine(VnTables.VietwareXTable),
        3 => new DoubleByteEngine(VnTables.VniMacTable),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
