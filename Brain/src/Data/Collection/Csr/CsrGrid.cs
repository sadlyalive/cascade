using System;

namespace Brain.Data.Collection.Csr;

internal class CsrGrid(int[] rowPtr, int[] columns)
{
    protected readonly int[] RowPtr  = rowPtr;
    protected readonly int[] Columns = columns;

    public int Rows => RowPtr.Length - 1;

    public ReadOnlySpan<int> GetColumns(int row)
    {
        var start = RowPtr[row];
        return Columns.AsSpan(start, RowPtr[row + 1] - start);
    }
}