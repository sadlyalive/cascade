using System;
using System.Collections.Generic;
using Brain;
using Brain.Data;

namespace Cascade.Select
{
    /// <summary>
    ///     <para>
    ///         Records neuron indices to represent user selection. Preserves order for order-dependent commands (e.g., wiring
    ///         synapses in a chain).
    ///     </para>
    ///     <para>
    ///         Automatically patches on <see cref="Network.NeuronRemoved" />.
    ///     </para>
    /// </summary>
    public sealed partial class Selection : IEnumerable<Neuron>
    {
        private readonly Network _network;

        // Count before each Grow that's still undoable. growing only ever appends, so undoing one is just truncating
        // back to the count it started from. any other change could reorder or remove entries, so it forgets these
        private readonly Stack<int> _growth = new();

        // membership per neuron for O(1) Contains, which the renderer asks for every neuron every frame. sized to the
        // pool's capacity, lazily, since neurons can be added without this knowing
        private bool[] _contains = Array.Empty<bool>();

        // grows with the selection rather than the network, so a small selection stays small
        private int[] _order = new int[16];

        public Selection(Network network)
        {
            _network               =  network;
            _network.NeuronRemoved += OnNeuronRemoved;
            _network.Cleared       += Clear;
        }

        public int Count { get; private set; }

        /// <summary>Selected neuron indices, in the order they were selected.</summary>
        public ReadOnlySpan<int> Order => _order.AsSpan(0, Count);

        /// <summary>
        ///     The <paramref name="i" />th selected neuron.
        /// </summary>
        public Neuron this[int i] =>
            (uint)i >= (uint)Count
                ? throw new ArgumentOutOfRangeException(nameof(i), i, $"No selected neuron at {i} (count {Count})")
                : _network.Neurons[_order[i]];

        /// <summary>Raises after any change to which neurons are selected or their order.</summary>
        public event Action Changed;

        public bool Contains(int id)
        {
            return (uint)id < (uint)_contains.Length && _contains[id];
        }

        /// <summary>Select the neuron at index <paramref name="id" />.</summary>
        public void Append(int id)
        {
            if (!AppendRaw(id))
                return;

            OnChanged();
        }

        public void Append(ReadOnlySpan<int> ids)
        {
            var change = false;

            foreach (var id in ids)
                change |= AppendRaw(id);

            if (change)
                OnChanged();
        }

        /// <summary>Deselect the neuron at index <paramref name="id" />.</summary>
        public void Remove(int id)
        {
            if (!RemoveRaw(id))
                return;

            OnChanged();
        }

        public void Toggle(int id)
        {
            if (Contains(id))
                Remove(id);
            else
                Append(id);
        }

        /// <summary>Replace the whole selection with just <paramref name="id" />.</summary>
        public void Set(int id)
        {
            ClearRaw();
            AppendRaw(id);
            OnChanged();
        }

        public void Set(ReadOnlySpan<int> ids)
        {
            ClearRaw();

            foreach (var id in ids)
                AppendRaw(id);

            OnChanged();
        }

        /// <summary>
        ///     Select every unselected neuron in the network in index order.
        /// </summary>
        // TODO: ctrl+a
        public void SelectAll()
        {
            var change = false;

            for (var id = 0; id < _network.Count; id++)
                change |= AppendRaw(id);

            if (change)
                OnChanged();
        }

        public void Clear()
        {
            if (Count == 0)
                return;

            ClearRaw();
            OnChanged();
        }

        /// <summary>
        ///     <para>
        ///         Adds every neuron one synapse away from the selection: postsynaptic targets if
        ///         <paramref name="downstream" />, otherwise presynaptic sources. Appended after the current selection, so
        ///         repeated grows trace outward one hop at a time.
        ///     </para>
        ///     <para>Undo with <see cref="Shrink" />.</para>
        /// </summary>
        public void Grow(bool downstream)
        {
            var connectome = _network.Connectome;

            // rows only show committed synapses. Step commits, but a paused network may still hold fresh wiring
            connectome.CommitChanges();

            var before = Count;

            // only the neurons selected before this grow, so one call is exactly one hop. index-based because
            // AppendRaw may replace _order while this reads it
            for (var i = 0; i < before; i++)
            {
                var row = downstream ? connectome.From(_order[i]) : connectome.To(_order[i]);

                foreach (var neighbor in row.Columns)
                    AppendRaw(neighbor);
            }

            // nothing new (e.g. no outgoing synapses): don't push an undo step that would do nothing
            if (Count == before)
                return;

            _growth.Push(before);
            Changed?.Invoke();
        }

        /// <summary>Undoes the most recent <see cref="Grow" />, if nothing else has changed the selection since.</summary>
        public void Shrink()
        {
            if (_growth.Count == 0)
                return;

            var before = _growth.Pop();

            for (var i = before; i < Count; i++)
                _contains[_order[i]] = false;

            Count = before;
            Changed?.Invoke();
        }

        // every change except Grow and Shrink goes through here
        private void OnChanged()
        {
            _growth.Clear();
            Changed?.Invoke();
        }

        private void ClearRaw()
        {
            // only touch the flags that are actually set, so clearing costs O(selected) rather than O(neurons)
            for (var i = 0; i < Count; i++)
                _contains[_order[i]] = false;

            Count = 0;
        }

        private bool AppendRaw(int id)
        {
            if ((uint)id >= (uint)_network.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(id),
                                                      id,
                                                      $"Attempt to select nonexistent neuron at index {id} (count {_network.Count})");
            }

            if (Contains(id))
                return false;

            if (_contains.Length < _network.Neurons.Capacity)
                Array.Resize(ref _contains, _network.Neurons.Capacity);

            if (Count == _order.Length)
                Array.Resize(ref _order, Count * 2);

            _order[Count++] = id;
            _contains[id]   = true;
            return true;
        }

        private bool RemoveRaw(int id)
        {
            if (!Contains(id))
                return false;

            var at = IndexOf(id);

            // shift everything after it down by one to preserve order (unlike Network._spike)
            Array.Copy(_order, at + 1, _order, at, Count - at - 1);
            Count--;

            _contains[id] = false;
            return true;
        }

        private int IndexOf(int id)
        {
            return Array.IndexOf(_order, id, 0, Count);
        }

        private void OnNeuronRemoved(int removed, int moved)
        {
            var change = RemoveRaw(removed);

            // adhere to the successor's new index if in selection
            if (moved >= 0 && Contains(moved))
            {
                _order[IndexOf(moved)] = removed;
                _contains[moved]       = false;
                _contains[removed]     = true;
                change                 = true;
            }

            if (change)
                OnChanged();
        }
    }
}