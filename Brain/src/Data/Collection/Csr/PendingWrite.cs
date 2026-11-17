using System;

namespace Brain.Data.Collection.Csr;

// better than repeating row, col, and val arrays
// buffer can get very swap-heavy (sorting) so this helps shorten the code (not writing to 4 different arrays per
// swap) and (I think?) performs better due to bulk memory copy
internal readonly record struct PendingWrite<T>(Cell Cell, T Value) : IComparable<PendingWrite<T>>
{
    public int Row    => Cell.Row;
    public int Column => Cell.Column;

    public int CompareTo(PendingWrite<T> other)
    {
        var rowDiff = Cell.Row.CompareTo(other.Cell.Row);

        // no unstable sort tiebreaker needed since the connectome's hash table never holds a second write to the same cell
        return rowDiff != 0 ? rowDiff : Cell.Column.CompareTo(other.Cell.Column);
    }
}