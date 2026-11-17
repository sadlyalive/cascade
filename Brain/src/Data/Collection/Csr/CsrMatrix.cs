using System;
using System.Collections.Generic;
using System.Linq;

namespace Brain.Data.Collection.Csr;

/// <summary>
///     <para>
///         A compressed sparse row matrix with mutable existing values (via <see cref="this[int, int]" />) but immutable
///         dimensions.
///     </para>
///     <para>
///         Dimensions are only as tight as entries allow. <see cref="Rows" /> ends at the last non-empty row, while
///         <see cref="Width" /> records the highest column for faster transposition. Any reads to nonexistent columns or
///         entire rows just returns <c>default(T)</c> or an empty <see cref="RowSpan{T}" /> respectively since this data
///         structure is agnostic to dimensional constraints.
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
    public CsrMatrix() : this([], [], [], 0)
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

    public T? this[int row, int col]
    {
        get
        {
            var i = IndexOf(row, col);
            return i < 0 ? default : _values[i];
        }

        set
        {
            var i = IndexOf(row, col);

            if (i < 0)
                throw new ArgumentException("In-place CsrMatrix write requires cell to already exist");

            if (IsDefault(value))
                throw new ArgumentException("In-place CsrMatrix write requires non-default value");

            _values[i] = value!;
        }
    }

    public bool TrySet(int row, int col, T value)
    {
        if (IsDefault(value))
        {
            throw new ArgumentException(
                $"Attempt to free a cell by writing a value of default({
                    typeof(T).Name
                }). Merge into a CsrMatrix with new dimensions instead"
            );
        }

        var i = IndexOf(row, col);

        if (i < 0)
            return false;

        _values[i] = value;
        return true;
    }

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

        var start  = _rowPtr[row];
        var end    = _rowPtr[row + 1];
        var length = end - start;

        // fast path for small rows (linear scan) since it's faster than binary search
        if (length <= 16)
        {
            for (var i = start; i < end; i++)
            {
                var c = _columns[i];

                if (c == col)
                    return i;
                if (c > col) // exit early if we reach a higher column (sorted advantage)
                    return -1;
            }

            return -1; // empty row or every column is smaller than non-present `col`
        }

        // binary search default
        while (length > 1)
        {
            var half = length >> 1; // bit shift faster than signed division (safe since indices can't be negative)

            if (_columns[start + half] <= col)
                start += half;

            length -= half;
        }

        return _columns[start] == col ? start : -1;
    }

    public bool Exists(int row, int col)
    {
        return IndexOf(row, col) >= 0;
    }

    /// <summary>
    ///     <para>
    ///         Build a new size-frozen <see cref="CsrMatrix{T}" /> by merging every pending write in
    ///         <paramref name="pending" />
    ///         to this matrix.
    ///     </para>
    ///     <para>
    ///         Sorts the batch internally to easily determine the highest row with at least one column and perform a two-way
    ///         merge with this matrix. Expects a dictionary to prohibit same-cell pending writes by design.
    ///     </para>
    /// </summary>
    /// <param name="pending">
    ///     Pending writes with <see cref="Cell" /> keys <c>(row, column)</c> and <typeparamref name="T" />
    ///     values
    /// </param>
    public CsrMatrix<T> Merge(IReadOnlyDictionary<Cell, T> pending)
    {
        var changes = pending.Count;

        if (changes == 0)
            return this;

        var batch = new PendingWrite<T>[changes];
        var i     = 0;

        foreach (var pair in pending)
            batch[i++] = new PendingWrite<T>(pair.Key, pair.Value);

        Array.Sort(batch);

        var rows   = Math.Max(Rows, batch[^1].Row + 1); // highest present row
        var rowPtr = new int[rows + 1];

        // worst case is every write being a new entry
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