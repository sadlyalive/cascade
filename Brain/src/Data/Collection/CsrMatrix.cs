using System;
using System.Collections.Generic;
using System.Linq;

namespace Brain.Data.Collection;

public class CsrMatrix<T>
{
    private const int InitialCommitThreshold = 128;

    // writes not yet merged into the arrays below, keyed by cell: finding one is O(1) however many are pending, and a
    // cell can never be pending twice since a rewrite just replaces its value
    private Dictionary<Cell, T> _pending = new();

    // how many writes the previous commit merged (see CommitChanges)
    private int _lastCommit;

    private int[] _rowPtr;
    private int[] _columns;
    private T[]   _values;

    /// <summary>
    ///     Create an empty compressed sparse row matrix with preallocated <paramref name="rows" />.
    /// </summary>
    public CsrMatrix(int rows)
    {
        _rowPtr  = new int[rows + 1]; // all zero by default, so every row lacks columns at initialization
        _columns = [];
        _values  = [];
    }

    private int Rows => _rowPtr.Length - 1;

    public T? this[int row, int col]
    {
        get => Get(row, col);
        set => Set(row, col, value);
    }

    public T? Get(int row, int col)
    {
        // pending writes are newer and take precedence
        if (_pending.TryGetValue(new Cell(row, col), out var pending))
            return pending;

        if (row > Rows - 1) // last _rowPtr is just an upper bound for the final member's column
            return default;

        var j = FindInRow(row, col);

        return j < 0 ? default! : _values[j];
    }

    /// <summary>Remove all entries and pending writes. Row pointers remain allocated.</summary>
    public void Clear()
    {
        Array.Clear(_rowPtr, 0, _rowPtr.Length);

        _columns    = [];
        _values     = [];
        _pending    = new Dictionary<Cell, T>();
        _lastCommit = 0;
    }

    public RowSpan<T> GetRow(int row)
    {
        // treat nonexistent rows as empty rather than tossing an out of range exception (same as Get())
        if (row >= Rows)
            return default;

        var start = _rowPtr[row];
        var len   = _rowPtr[row + 1] - start;

        return new RowSpan<T>(_columns.AsSpan(start, len), _values.AsSpan(start, len));
    }

    public void Set(int row, int col, T? value)
    {
        var cell = new Cell(row, col);

        // check for an existing pending write and just overwrite it if present
        if (_pending.ContainsKey(cell))
        {
            _pending[cell] = value!;
            return;
        }

        var existing = row < Rows ? FindInRow(row, col) : -1;

        if (IsDefault(value!))
        {
            // removing something that doesn't exist (and isn't pending, see above) changes nothing
            if (existing < 0)
                return;

            // arrays have to shrink for removals; fall into modification buffer
        }
        else if (existing >= 0)
        {
            // quick update. safe because we already checked pending writes, so no older pending write can overwrite
            // this on commit
            _values[existing] = value!;
            return;
        }
        else
            EnsureRows(row + 1); // a new entry has to be inserted; fall into modification buffer

        // commit once the buffer holds as many writes as the matrix has entries
        // a commit costs O(nnz + k log k), so letting k grow with nnz keeps the commit cost per write constant
        // small matrices will still auto-commit at 128 pending writes
        if (_pending.Count >= Math.Max(InitialCommitThreshold, _columns.Length))
            CommitChanges();

        _pending[cell] = value!;
    }

    // grow matrix to max(rows, Rows * 1.5)
    private void EnsureRows(int rows)
    {
        if (rows <= Rows)
            return;

        var oldLength = _rowPtr.Length;

        Array.Resize(ref _rowPtr, Math.Max(rows, Rows + (Rows >> 1)) + 1);

        var end = _rowPtr[oldLength - 1];

        // new rows are always empty, so they all start and end where the last existing row ended
        for (var i = oldLength; i < _rowPtr.Length; i++)
            _rowPtr[i] = end;
    }

    /// <summary>
    ///     Sort then incorporate all pending writes into the actual matrix. Invokes automatically once the number of
    ///     pending writes reaches <c>max(number non-zero, 128)</c>.
    /// </summary>
    /// <returns>`true` if any writes were committed</returns>
    public bool CommitChanges()
    {
        // skip sort and O(rows + nnz) rebuild so callers can commit defensively
        if (_pending.Count == 0)
            return false;

        var changes = _pending.Count;
        var batch   = _pending.Select(k => new PendingWrite(k.Key.Row, k.Key.Column, k.Value)).ToArray();

        Array.Sort(batch);

        // columns per row to rebuild the row pointers at the end
        var columns = new int[_columns.Length + changes];
        var values  = new T[columns.Length];

        var colI   = 0; // next existing entry
        var batchI = 0; // next pending write
        var nnz    = 0; // entries thus far

        for (var r = 0; r < Rows; r++)
        {
            // row's original end pointer
            // colI is already at the start pointer
            var rowEnd = _rowPtr[r + 1];

            _rowPtr[r] = nnz;

            while (true)
            {
                var hasOld   = colI < rowEnd;
                var hasWrite = batchI < changes && batch[batchI].Row == r;

                if (!hasOld && !hasWrite) // nothing else to place in this row
                    break;

                // IMPORTANT: batch[batchI] doesn't guaranteeably exist since we don't escape on !hasWrite
                // pending write goes first only if it exists && (no more old columns remain || it comes before this
                // column)
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
            }
        }

        _rowPtr[Rows] = nnz;

        // trim worst-case (previous column count + change count) slack with actual nnz
        if (nnz < columns.Length)
        {
            Array.Resize(ref columns, nnz);
            Array.Resize(ref values,  nnz);
        }

        _columns = columns;
        _values  = values;

        if (changes * 2 < _lastCommit)
            _pending = new Dictionary<Cell, T>();
        else
            _pending.Clear();

        _lastCommit = changes;

        return true;
    }

    // returns the absolute index at which a column exists, otherwise -1
    private int FindInRow(int row, int col)
    {
        for (var i = _rowPtr[row]; i < _rowPtr[row + 1]; i++)
        {
            if (_columns[i] == col)
                return i;
        }

        return -1;
    }

    private static bool IsDefault(T? value)
    {
        return EqualityComparer<T>.Default.Equals(value!, default!);
    }

    // better than repeating row, col, and val arrays along with an addition seq array
    // buffer can get very swap-heavy (sorting) so this helps shorten the code (not writing to 4 different arrays per
    // swap) and (I think?) performs better due to bulk memory copy
    private readonly record struct PendingWrite(int Row, int Column, T Value) : IComparable<PendingWrite>
    {
        public int CompareTo(PendingWrite other)
        {
            var rowDiff = Row.CompareTo(other.Row);

            // tiebreaker for unstable sort no longer needed thanks to hash table (prevents a second [row, col] pending
            // write)
            return rowDiff != 0 ? rowDiff : Column.CompareTo(other.Column);
        }
    }

    // dictionary key for one matrix cell
    // two cells may share a hash code but that only marginally increases collisions
    private readonly record struct Cell(int Row, int Column);
}