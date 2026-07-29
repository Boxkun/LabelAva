using System;
using System.Collections.Generic;

namespace LabelAva.Services;

/// <summary>
/// 自然排序：数字段按数值比较，非数字段按字符比较。避免文件系统 inode 序影响列表顺序。
/// </summary>
internal static class NaturalSort
{
    public static int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                while (ix < x.Length && x[ix] == '0') ix++;
                while (iy < y.Length && y[iy] == '0') iy++;

                int startX = ix, startY = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;

                int lenX = ix - startX;
                int lenY = iy - startY;
                if (lenX != lenY) return lenX.CompareTo(lenY);

                for (int k = 0; k < lenX; k++)
                {
                    int cmp = x[startX + k].CompareTo(y[startY + k]);
                    if (cmp != 0) return cmp;
                }
            }
            else
            {
                int cmp = x[ix].CompareTo(y[iy]);
                if (cmp != 0) return cmp;
                ix++;
                iy++;
            }
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }
}
