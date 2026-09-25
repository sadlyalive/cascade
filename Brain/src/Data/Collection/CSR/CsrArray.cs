using System;
using System.Collections.Generic;

namespace Brain.Data.Collection.CSR;

public partial class CsrArray<T>
{
    private const int MaxChanges      = 128;
    private const int QuicksortCutoff = 16;

    private readonly PendingWrite[] _pending = new PendingWrite[MaxChanges];

    private int[] _rowPtr;
    private int[] _columns;
    private T[]   _values;

    private int _changes;

    /// <summary>
    ///     Creates an empty compressed sparse row matrix with preallocated <paramref name="rows" />.
    /// </summary>
    public CsrArray(int rows)
    {
        _rowPtr  = new int[rows + 1]; // all zero by default, so every row lacks columns at initialization
        _columns = [];
        _values  = [];
    }

    public int Rows => _rowPtr.Length - 1;

    public T this[int row, int col]
    {
        get => Get(row, col);
        set => Set(row, col, value);
    }

    public T Get(int row, int col)
    {
        // check pending writes first
        // must traverse in reverse so that newer writes take precedence
        for (var i = _changes - 1; i >= 0; i--)
        {
            if (row == _pending[i].Row && col == _pending[i].Column)
                return _pending[i].Value;
        }

        if (row > _rowPtr.Length - 2) // last _rowPtr is just an upper bound for the final member's column
            return default;

        var j = FindInRow(row, col);

        return j < 0 ? default : _values[j];
    }

    public RowRange GetRow(int row)
    {
        var start = _rowPtr[row];
        var len   = _rowPtr[row + 1] - start;

        return new RowRange(_columns.AsSpan(start, len), _values.AsSpan(start, len));
    }

    public void Set(int row, int col, T value)
    {
        var maxRow    = _rowPtr.Length - 1;
        var rowExists = row < maxRow;

        if (IsRemoveOp(value))
        {
            if (!rowExists)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(row),
                    row,
                    $"Attempt to remove from nonexistent row {row} (max {maxRow})"
                );
            }
        }
        else
        {
            if (rowExists)
            {
                // edit column's value directly if entry is present AND this isn't a remove op
                var i = FindInRow(row, col);

                if (i >= 0)
                {
                    _values[i] = value;
                    return;
                }

                // fall into modification buffer
            }
            else
            {
                // expand _rowPtr
                var oldLength = _rowPtr.Length;
                var newLength = maxRow + (maxRow >> 1);

                // multiplicative expansion must land strictly past `row` because _rowPtr[row + 1] has to exist
                if (newLength <= row)
                    newLength = row + 1;

                Array.Resize(ref _rowPtr, newLength + 1);

                // initialize new tail pointers to point to the previous column end. empty rows carry over the offset
                // index value of the last valid row
                var lastColIndex = _rowPtr[oldLength - 1];

                for (var i = oldLength; i < _rowPtr.Length; i++)
                    _rowPtr[i] = lastColIndex;
            }
        }

        if (_changes >= MaxChanges) // commit changes if buffer is full before the new modification
            CommitChanges();

        // Order = _changes = current buffer count = position of this change = order to combat unstable sort
        // _changes resets on commit which is perfect since sorts only care about the current batch
        _pending[_changes] = new PendingWrite { Row = row, Column = col, Order = _changes, Value = value };

        _changes++;
    }

    public void CommitChanges()
    {
        Quicksort(0, _changes - 1);

        var maxRowIndex = _rowPtr.Length - 1;

        // TODO: rent temporary arrays to reduce gc overhead (?)
        var counts = new int[maxRowIndex];

        // simultaneous merging and discovering exact nnz after changes
        var id       = 0;
        var batchId  = 0;
        var finalNnz = 0;

        for (var i = 0; i < maxRowIndex; i++)
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
                    targetCol = _columns[id];
                    targetVal = _values[id];
                    id++;
                }

                // de-duplicate identical sequential (sorted) entries inside the batch itself (last write wins)
                while (batchId < _changes && _pending[batchId].Row == i && _pending[batchId].Column == targetCol)
                {
                    targetVal = _pending[batchId].Value;
                    batchId++;
                }

                // omit removals from nnz
                if (IsRemoveOp(targetVal))
                    continue;

                counts[i]++;
                finalNnz++;
            }
        }

        // TODO: rent arrays (?)
        var finalCols = new int[finalNnz];
        var finalVals = new T[finalNnz];

        // populate final arrays in a second pass
        id      = 0;
        batchId = 0;
        var writeId = 0;

        for (var r = 0; r < maxRowIndex; r++)
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

                while (batchId < _changes && _pending[batchId].Row == r && _pending[batchId].Column == targetCol)
                {
                    targetVal = _pending[batchId].Value;
                    batchId++;
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

        for (var i = 0; i < maxRowIndex; i++)
        {
            _rowPtr[i] =  curSum;
            curSum     += counts[i];
        }

        _rowPtr[maxRowIndex] = curSum;

        // clean up
        _changes = 0;
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
    private struct PendingWrite : IComparable<PendingWrite>
    {
        public int Row;
        public int Column;
        public int Order; // equal to _changes (buffer count) to map original insertion order
        public T   Value;

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

            if (delta != 0)
                return delta;

            delta = Column.CompareTo(other.Column);

            return delta != 0 ? delta : Order.CompareTo(other.Order); // tiebreaker for unstable sort
        }
    }
}