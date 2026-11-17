using System;

namespace Brain.Data.Collection.Pool;

/// <summary>
///     <para>
///         A contiguous range of neurons in a <see cref="NeuronPool" />.
///     </para>
///     <para>
///         Captures its start and count on creation, so neurons added afterward never leak into it. It can become stale
///         after removals since swap-remove moves neurons between indices.
///     </para>
/// </summary>
// a struct with a struct enumerator rather than a `yield return` iterator for two reasons:
// 1. iterators run lazily, so `[i ..]` would resolve against whatever the NeuronPool `Count` is when the loop starts, not
//    when the range was created
// 2. iterators allocate an object on the heap per call, whereas foreach finds `GetEnumerator` by name (duck typing) and
//    never boxes this struct
// Span<Neuron> isn't an option since NeuronPool's indexer makes neuron structs on the fly
public readonly struct NeuronRange
{
    private readonly NeuronPool _pool;

    internal NeuronRange(NeuronPool pool, Range range, int max)
    {
        _pool          = pool;
        (Start, Count) = range.GetOffsetAndLength(max);
    }

    /// <summary>Pool index of the first neuron in this range.</summary>
    public int Start { get; }

    public int Count { get; }

    /// <summary>The <paramref name="i" />th neuron in this range.</summary>
    public Neuron this[int i] =>
        (uint)i >= (uint)Count
            ? throw new ArgumentOutOfRangeException(nameof(i), i, $"No neuron at index {i} in range (count {Count})")
            : _pool[Start + i];

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_pool, Start, Start + Count);
    }

    // only MoveNext and Current are required for foreach; IEnumerator<Neuron> is skipped so nothing tempts a boxing cast
    public struct Enumerator
    {
        private readonly NeuronPool _pool;
        private readonly int        _end;

        private int _i;

        internal Enumerator(NeuronPool pool, int start, int end)
        {
            _pool = pool;
            _end  = end;
            _i    = start - 1;
        }

        public readonly Neuron Current => _pool[_i];

        public bool MoveNext()
        {
            return ++_i < _end;
        }
    }
}