using System.Collections;
using System.Collections.Generic;

namespace Brain.Data.Collection.Pool;

public sealed partial class NeuronPool
{
    IEnumerator<Neuron> IEnumerable<Neuron>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator : IEnumerator<Neuron>
    {
        private readonly NeuronPool _pool;

        private int _i;

        public Enumerator(NeuronPool pool)
        {
            _pool = pool;
            Reset();
        }

        public readonly Neuron Current => _pool[_i];

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return ++_i < _pool.Count;
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