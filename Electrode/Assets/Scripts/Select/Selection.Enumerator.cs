using System.Collections;
using System.Collections.Generic;
using Brain.Data;

namespace Cascade.Select
{
    public sealed partial class Selection
    {
        // boxing doesn't matter here since selection isn't nearly as tight as NeuronPool
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
            private readonly Selection _selection;

            private int _i;

            // `: this()` zeroes every field first. before C# 11 a struct constructor can't call its own methods (Reset)
            // until every field is assigned, and Electrode compiles at Unity's C# 9 while Brain opts into 12 via
            // csc.rsp
            public Enumerator(Selection selection) : this()
            {
                _selection = selection;
                Reset();
            }

            public readonly Neuron Current => _selection[_i];

            readonly object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                return ++_i < _selection.Count;
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
}