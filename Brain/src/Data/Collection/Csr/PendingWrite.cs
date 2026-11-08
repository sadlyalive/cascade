using System;

namespace Brain.Data.Collection.Csr;

// better than repeating row, col, and val arrays
// buffer can get very swap-heavy (sorting) so this helps shorten the code (not writing to 4 different arrays per
// swap) and (I think?) performs better due to bulk memory copy
public readonly record struct PendingWrite<T>(int Row, int Column, T Value) : IComparable<PendingWrite<T>>
{
    public int CompareTo(PendingWrite<T> other)
    {
        var rowDiff = Row.CompareTo(other.Row);

        // no unstable sort tiebreaker needed since the connectome's hash table never holds a second write to the same cell
        return rowDiff != 0 ? rowDiff : Column.CompareTo(other.Column);
    }
}