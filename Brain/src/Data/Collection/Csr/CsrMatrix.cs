using System;
using System.Collections.Generic;
using System.Linq;

namespace Brain.Data.Collection.Csr;

/// <summary>
///     <para>
///         A compressed sparse row matrix with mutable existing values (via <see cref="ValueAt" />) but immutable
///         dimensions.
///     </para>
///     <para>
///         Dimensions are as tight as the entries allow: <see cref="Rows" /> ends at the last non-empty row, and
///         <see cref="Width" /> records the highest column. Any reads to nonexistent columns or entire rows just returns
///         <c>default(T)</c> or an empty <see cref="RowSpan{T}" /> respectively.
///     </para>
///     <para>
///         New or dead entries require merging pending writes into a new matrix via <see cref="Merge" />. The
///         consumer now owns the pending buffer logic for no risk of desync across multiple matrices that record the same
///         data (i.e., a matrix and its transpose).
///     </para>
/// </summary>
internal sealed class CsrMatrix<T>
{
    private readonly int[] _rowPtr;
    private readonly int[] _columns;
    private readonly T[]   _values;

    /// <summary>Create an empty matrix (no rows, no columns).</summary>
    public CsrMatrix() : this([0], [], [], 0)
    {
    }

    /// <summary>
    ///     Directly incorporate prebuilt CSR arrays without copy. The caller must guarantee:
    ///     <list type="bullet">
    ///         <item><paramref name="rowPtr" /> starts at 0 and never decreases</item>
    ///         <item><paramref name="columns" /> array size abides by row pointer ranges</item>
    ///         <item><paramref name="columns" /> ascend in order per row</item>
    ///         <item><paramref name="values" /> must be the same size as <paramref name="columns" /></item>
    ///         <item><paramref name="values" /> excludes default values</item>
    ///     </list>
    /// </summary>
    public CsrMatrix(int[] rowPtr, int[] columns, T[] values) : this(
        rowPtr,
        columns,
        values,
        columns.DefaultIfEmpty(0).Max() + 1 // determine width as highest existing column (for transposing)
    )
    {
    }

    // for callers that already know the width (merges and transposes), so the columns aren't scanned again
    private CsrMatrix(int[] rowPtr, int[] columns, T[] values, int width)
    {
        _rowPtr  = rowPtr;
        _columns = columns;
        _values  = values;
        Width    = width;
    }

    public int Rows => _rowPtr.Length - 1;

    /// <summary>
    ///     One past the highest column, i.e., the row count of <see cref="Transpose" />. Exact after a
    ///     <see cref="Merge" />. Prebuilt arrays may carry trailing empty rows, which become trailing empty columns once
    ///     transposed until the next merge trims them.
    /// </summary>
    public int Width { get; }

    /// <summary>Number of stored (non-default) entries</summary>
    public int Count => _rowPtr[Rows]; // last row pointer is just the end of the final row

    public RowSpan<T> GetRow(int row)
    {
        // treat nonexistent rows as empty since trailing empty rows aren't stored
        // impossible to fail loud unless fake dimensions are cached
        if (!RowExists(row))
            return default;

        var start = _rowPtr[row];
        var len   = _rowPtr[row + 1] - start;

        return new RowSpan<T>(_columns.AsSpan(start, len), _values.AsSpan(start, len));
    }

    public bool RowExists(int row)
    {
        return (uint)row < (uint)Rows;
    }

    /// <returns>The absolute index of (<paramref name="row" />, <paramref name="col" />), or -1 if nonexistent</returns>
    public int IndexOf(int row, int col)
    {
        if (!RowExists(row))
            return -1;

        for (var i = _rowPtr[row]; i < _rowPtr[row + 1]; i++)
        {
            if (_columns[i] == col)
                return i;
        }

        return -1;
    }

    /// <summary>The value at absolute index <paramref name="i" />. Returns its memory address for in-place writes.</summary>
    public ref T ValueAt(int i)
    {
        return ref _values[i];
    }

    /// <summary>
    ///     <para>Build a new matrix by merging every pending write in <paramref name="batch" /> into this matrix.</para>
    ///     <para>
    ///         The batch must be sorted into rows then columns per row so that the merge only requires a single
    ///         <c>O(rows + entries + writes)</c> pass. That pass already sees every surviving entry, so it also measures
    ///         the result's exact dimensions for free: it grows to fit new writes and shrinks when the outermost entries
    ///         are removed. No growth headroom is kept since the pending buffer absorbs growth between merges.
    ///     </para>
    /// </summary>
    /// <param name="batch">Pending writes in ascending <c>(row, column)</c> order without repeats</param>
    public CsrMatrix<T> Merge(ReadOnlySpan<PendingWrite<T>> batch)
    {
        if (batch.IsEmpty)
            return this;

        // highest present row
        var rows = Math.Max(Rows, batch[^1].Row + 1);

        var rowPtr = new int[rows + 1];

        // worst case is every write being a new entry; trimmed at the end
        var columns = new int[Count + batch.Length];
        var values  = new T[columns.Length];

        var colI   = 0; // next existing entry
        var batchI = 0; // next pending write
        var nnz    = 0; // entries thus far

        var lastRow = -1; // last row that kept an entry
        var width   = 0;  // one past the highest kept column

        for (var r = 0; r < rows; r++)
        {
            // end of this row's existing entries. colI is already at its start, and rows past the old matrix have none
            var rowEnd = r < Rows ? _rowPtr[r + 1] : colI;

            rowPtr[r] = nnz;

            while (true)
            {
                var hasOld   = colI < rowEnd;
                var hasWrite = batchI < batch.Length && batch[batchI].Row == r;

                if (!hasOld && !hasWrite) // nothing else to place in this row
                    break;

                // IMPORTANT: batch[batchI] doesn't guaranteeably exist since we don't escape on !hasWrite
                // pending goes first only if it exists && (no more old columns remain || it comes before this column)
                var placeWrite = hasWrite && (!hasOld || batch[batchI].Column <= _columns[colI]);

                int col;
                T   val;

                if (placeWrite)
                {
                    var write = batch[batchI];

                    col = write.Column;
                    val = write.Value;

                    // replace/drop the old entry if it exists and is the same column
                    if (hasOld && _columns[colI] == col)
                        colI++;

                    batchI++; // scan pending write
                }
                else
                {
                    col = _columns[colI];
                    val = _values[colI];
                    colI++; // scan next column in row
                }

                if (IsDefault(val)) // default value considered removal
                    continue;

                columns[nnz] = col;
                values[nnz]  = val;
                nnz++;

                lastRow = r;
                width   = Math.Max(width, col + 1);
            }
        }

        // leftovers can only mean the batch wasn't sorted since the merge loop escaped too early
        if (batchI < batch.Length)
            throw new ArgumentException("Pending writes must be sorted by (row, column)", nameof(batch));

        // drop trailing empty rows. final end pointer is always nnz
        Array.Resize(ref rowPtr, lastRow + 2);
        rowPtr[lastRow + 1] = nnz;

        // trim worst-case (previous entries + writes) slack to actual nnz
        if (nnz < columns.Length)
        {
            Array.Resize(ref columns, nnz);
            Array.Resize(ref values,  nnz);
        }

        return new CsrMatrix<T>(rowPtr, columns, values, width);
    }

    /// <summary>
    ///     <para>Build the transpose such that entry <c>(r, c)</c> becomes <c>(c, r)</c>.</para>
    ///     <para>
    ///         Employs a counting sort in <c>O(rows + entries)</c> instead of a comparison sort: counts how many entries will
    ///         be in each new row, prefix-sums those counts into row start pointers, then deals every former row into its
    ///         column slot.
    ///         Dealing old rows in ascending order leaves every new row already sorted.
    ///     </para>
    /// </summary>
    public CsrMatrix<T> Transpose()
    {
        // rename new dimensions for readability
        // ReSharper disable once InlineTemporaryVariable
        var rows = Width;
        var nnz  = Count;

        // count entries per new row, offset by one so the prefix sum below turns counts into starts
        var rowPtr = new int[rows + 1];

        for (var k = 0; k < nnz; k++)
            rowPtr[_columns[k] + 1]++;

        for (var r = 0; r < rows; r++)
            rowPtr[r + 1] += rowPtr[r];


        int[] cursor  = [.. rowPtr]; // next free slot in each new row
        var   columns = new int[nnz];
        var   values  = new T[nnz];

        for (var row = 0; row < Rows; row++)
        {
            for (var k = _rowPtr[row]; k < _rowPtr[row + 1]; k++)
            {
                var slot = cursor[_columns[k]]++;

                columns[slot] = row;
                values[slot]  = _values[k];
            }
        }

        // every row becomes a column
        return new CsrMatrix<T>(rowPtr, columns, values, Rows);
    }

    public static bool IsDefault(T? value)
    {
        return EqualityComparer<T>.Default.Equals(value!, default!);
    }
}