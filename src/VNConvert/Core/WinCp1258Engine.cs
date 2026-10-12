namespace VNConvert.Core;

internal sealed class WinCp1258Engine : DoubleByteEngine
{
    public WinCp1258Engine(ReadOnlySpan<ushort> compositeChars, ReadOnlySpan<ushort> precomposedChars)
        : base(BuildInitialTable(compositeChars, precomposedChars, out var stdMap, out var searchTable))
    {
        // Re-assign fields initialized in base
        Array.Copy(stdMap, _stdMap, 256);
        // Copy the extended search table
        _searchTable = searchTable;
    }

    private static ReadOnlySpan<ushort> BuildInitialTable(
        ReadOnlySpan<ushort> compositeChars,
        ReadOnlySpan<ushort> precomposedChars,
        out ushort[] stdMap,
        out uint[] searchTable)
    {
        stdMap = new ushort[256];
        var searchList = new List<uint>(VnTables.TotalVnChars * 2);

        // 1. Composite chars
        for (int i = 0; i < VnTables.TotalVnChars; i++)
        {
            ushort code = compositeChars[i];
            if ((code >> 8) != 0)
            {
                stdMap[code >> 8] = 0xFFFF;
            }
            else if (stdMap[(byte)code] == 0)
            {
                stdMap[(byte)code] = (ushort)(i + 1);
            }

            searchList.Add(((uint)i << 16) | code);
        }

        // 2. Precomposed chars where different
        for (int k = 0; k < VnTables.TotalVnChars; k++)
        {
            if (precomposedChars[k] != compositeChars[k])
            {
                ushort code = precomposedChars[k];
                if ((code >> 8) != 0)
                {
                    stdMap[code >> 8] = 0xFFFF;
                }
                else if (stdMap[(byte)code] == 0)
                {
                    stdMap[(byte)code] = (ushort)(k + 1);
                }

                searchList.Add(((uint)k << 16) | code);
            }
        }

        searchTable = searchList.ToArray();
        Array.Sort(searchTable, static (a, b) => ((ushort)(a & 0xFFFF)).CompareTo((ushort)(b & 0xFFFF)));

        return compositeChars;
    }
}
