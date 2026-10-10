using System;

namespace Brain.Data.Collection;

// TODO: implement IEnumerable<(int, T)> when CoreCLR releases (ref struct interfaces not supported currently)
public readonly ref struct RowSpan<T>(ReadOnlySpan<int> columns, ReadOnlySpan<T> values)
{
    public ReadOnlySpan<int> Columns { get; } = columns;
    public ReadOnlySpan<T>   Values  { get; } = values;
    public int               Count   => Values.Length;

    public Enumerator GetEnumerator()
    {
        return new Enumerator(Columns, Values);
    }

    // note: exactly how the BCL itself does it (List<T>.Enumerator lives in List.cs, not a separate file)
    // so remember to not bother giving it a new file unless it gets huge
    // TODO: implement IEnumerator<(int, T)> with CoreCLR (see above)
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<int> _columns;
        private readonly ReadOnlySpan<T>   _values;

        private int _i;

        public Enumerator(ReadOnlySpan<int> columns, ReadOnlySpan<T> values)
        {
            _columns = columns;
            _values  = values;
            Reset();
        }

        public readonly (int, T) Current => (_columns[_i], _values[_i]);

        public bool MoveNext()
        {
            return ++_i < _values.Length;
        }

        public void Reset()
        {
            _i = -1;
        }

        public readonly void Dispose()
        {
        }
    }
}