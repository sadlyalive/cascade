using System;
using System.Collections.Generic;

namespace Brain.Data.Collection.CSR;

public partial class CsrMatrix<T>
{
    private const int InitialCommitThreshold = 128;
    private const int QuicksortCutoff = 16;

    // writes not yet merged into the matrix itself since
    private PendingWrite[] _pending = new PendingWrite[InitialCommitThreshold];

    // where each pending cell sits in _pending, so finding one costs O(1) however many are pending instead of a scan.
    // also means a cell is never pending twice: a rewrite just overwrites its slot
    private Dictionary<Cell, int> _pendingSlots = new();

    private int[] _rowPtr;
    private int[] _columns;
    private T[]   _values;

    private int _changes;

    /// <summary>
    ///     Creates an empty compressed sparse row matrix with preallocated <paramref name="rows" />.
    /// </summary>
    public CsrMatrix(int rows)
    {
        _rowPtr  = new int[rows + 1]; // all zero by default, so every row lacks columns at initialization
        _columns = [];
        _values  = [];
    }

    private int Rows => _rowPtr.Length - 1;

    public T this[int row, int col]
    {
        get => Get(row, col);
        set => Set(row, col, value);
    }

    public T Get(int row, int col)
    {
        // pending writes are newer than the arrays, so they take precedence
        if (_pendingSlots.TryGetValue(new Cell(row, col), out var slot))
            return _pending[slot].Value;

        if (row > Rows - 1) // last _rowPtr is just an upper bound for the final member's column
            return default;

        var j = FindInRow(row, col);

        return j < 0 ? default : _values[j];
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

    public void Set(int row, int col, T value)
    {
        var cell = new Cell(row, col);

        // check for an existing pending write and just overwrite it if present
        if (_pendingSlots.TryGetValue(cell, out var slot))
        {
            _pending[slot].Value = value;
            return;
        }

        var existing = row < Rows ? FindInRow(row, col) : -1;

        if (IsRemoveOp(value))
        {
            // removing something that doesn't exist (and isn't pending, see above) changes nothing
            if (existing < 0)
                return;

            // an existing entry can't be removed in place, the arrays have to shrink: fall into modification buffer
        }
        else if (existing >= 0)
        {
            // quick update. safe because the cell isn't pending, so no older pending write can overwrite this on commit
            _values[existing] = value;
            return;
        }
        else
        {
            EnsureRows(row + 1);

            // a new entry has to be inserted: fall into modification buffer
        }

        // commit once the buffer holds as many writes as the matrix has entries. a commit costs O(nnz + k log k), so
        // letting k grow with nnz keeps the merge cost per write constant: a burst of a million writes (an import, a big
        // wiring) merges a handful of times instead of once every 128 writes. small edits still commit at InitialCommitThreshold
        if (_changes >= Math.Max(InitialCommitThreshold, _columns.Length))
            CommitChanges();

        if (_changes == _pending.Length)
            Array.Resize(ref _pending, _changes * 2);

        _pending[_changes]  = new PendingWrite(row, col, value);
        _pendingSlots[cell] = _changes;

        _changes++;
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
    ///     Quicksort then file-in all pending writes into the actual matrix. Invokes automatically once the number of
    ///     pending writes reaches the matrix's nonzero count (at least 128).
    /// </summary>
    /// <returns>True if any writes were committed</returns>
    public bool CommitChanges()
    {
        // skip quicksort call and O(rows + nnz) rebuild so callers can commit defensively
        if (_changes == 0)
            return false;

        Quicksort(0, _changes - 1);

        // TODO: rent temporary arrays to reduce gc overhead (?)
        var counts = new int[Rows];

        // simultaneous merging and discovering exact nnz after changes
        var id       = 0;
        var batchId  = 0;
        var finalNnz = 0;

        for (var i = 0; i < Rows; i++)
        {
            var rowEnd = _rowPtr[i + 1];

            // merge existing elements and pending batch modifications for this row
            while (id < rowEnd || (batchId < _changes && _pending[batchId].Row == i))
            {
                bool takeBatch;

                if (id >= rowEnd)
                    takeBatch = true; // no more elements in this row
                else if (batchId >= _changes || _pending[batchId].Row != i)
                    takeBatch = false; // no modifications left for this row
                else
                    takeBatch = _pending[batchId].Column <= _columns[id]; // compare column keys

                int targetCol;
                T   targetVal;

                if (takeBatch)
                {
                    targetCol = _pending[batchId].Column;
                    targetVal = _pending[batchId].Value;

                    // skip if batch overrides an existing CSR entry
                    if (id < rowEnd && _columns[id] == targetCol)
                        id++;

                    batchId++;
                }
                else
                {
                    targetVal = _values[id];
                    id++;
                }

                // omit removals from nnz
                if (IsRemoveOp(targetVal))
                    continue;

                counts[i]++;
                finalNnz++;
            }
        }

        // TODO: rent arrays (see above) (?)
        var finalCols = new int[finalNnz];
        var finalVals = new T[finalNnz];

        // populate final arrays in a second pass
        id      = 0;
        batchId = 0;
        var writeId = 0;

        for (var r = 0; r < Rows; r++)
        {
            var rowEnd = _rowPtr[r + 1];

            while (id < rowEnd || (batchId < _changes && _pending[batchId].Row == r))
            {
                bool takeBatch;

                if (id >= rowEnd)
                    takeBatch = true;
                else if (batchId >= _changes || _pending[batchId].Row != r)
                    takeBatch = false;
                else
                    takeBatch = _pending[batchId].Column <= _columns[id];

                int targetCol;
                T   targetVal;

                if (takeBatch)
                {
                    targetCol = _pending[batchId].Column;
                    targetVal = _pending[batchId].Value;
                    if (id < rowEnd && _columns[id] == targetCol) id++;
                    batchId++;
                }
                else
                {
                    targetCol = _columns[id];
                    targetVal = _values[id];
                    id++;
                }

                if (IsRemoveOp(targetVal))
                    continue;

                finalCols[writeId] = targetCol;
                finalVals[writeId] = targetVal;
                writeId++;
            }
        }

        _columns = finalCols;
        _values  = finalVals;

        // rebuild row intervals ground-up with counts
        var curSum = 0;

        for (var i = 0; i < Rows; i++)
        {
            _rowPtr[i] =  curSum;
            curSum     += counts[i];
        }

        _rowPtr[Rows] = curSum;

        // a burst (e.g. from an import) can leave the buffer and index enormous. Dictionary.Clear keeps its capacity and
        // costs O(capacity), so every later small commit would keep paying for that one burst: start small instead
        if (_pending.Length > InitialCommitThreshold * 8)
        {
            _pending      = new PendingWrite[InitialCommitThreshold];
            _pendingSlots = new Dictionary<Cell, int>();
        }
        else
        {
            _pendingSlots.Clear();
        }

        _changes = 0;

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

    private static bool IsRemoveOp(T value)
    {
        return EqualityComparer<T>.Default.Equals(value, default);
    }

    // better than repeating row, col, and val arrays along with an addition seq array
    // buffer can get very swap-heavy (sorting) so this helps shorten the code (not writing to 4 different arrays per
    // swap) and (i think?) performs better due to bulk memory copy
    // TODO: implement IComparisonOperators on c# 11 (requires CoreCLR)
    private struct PendingWrite(int row, int col, T val) : IComparable<PendingWrite>
    {
        public readonly int Row = row;
        public readonly int Column = col;
        public T   Value = val;

        public static bool operator <(PendingWrite left, PendingWrite right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(PendingWrite left, PendingWrite right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(PendingWrite left, PendingWrite right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(PendingWrite left, PendingWrite right)
        {
            return left.CompareTo(right) >= 0;
        }

        public int CompareTo(PendingWrite other)
        {
            var delta = Row.CompareTo(other.Row);

            // tiebreaker for unstable sort no longer needed thanks to hash table (always 1:1 cell and value)
            return delta != 0 ? delta : Column.CompareTo(other.Column);
        }
    }

    // dictionary key for one matrix cell
    // two cells may share a hash code but that only marginally increases collisions. dictionary still differenties them
    // with Equals()
    private readonly struct Cell(int row, int column) : IEquatable<Cell>
    {
        private readonly int _row    = row;
        private readonly int _column = column;

        public bool Equals(Cell other)
        {
            return _row == other._row && _column == other._column;
        }

        public override bool Equals(object obj)
        {
            return obj is Cell other && Equals(other);
        }

        // HashCode.Combine mixes the bits of both. packing (row, col) into a long WOULD be unique, but long's own hash
        // XORs its halves back into row ^ col, which collides along every diagonal: (0, 3), (1, 2), (2, 1), (3, 0)...
        public override int GetHashCode()
        {
            return HashCode.Combine(_row, _column);
        }
    }
}