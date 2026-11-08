using System;
using System.Collections.Generic;
using Brain.Data.Collection.Csr;

namespace Brain.Data.Collection;

/// <summary>
///     <para>Records synaptic weights between neurons in both directions (inbound and outbound).</para>
///     <para>
///         Both directions are compressed sparse row matrices of the same synapses, one indexed by pre and one by post.
///         New and removed synapses remain in a single pending buffer until <see cref="CommitChanges" />, which merges
///         them
///         into <c>outbound</c> and uses its transpose as <c>inbound</c> so they never disagree.
///     </para>
///     <para>Does not implement an interface since interfaces prevent JIT inlining.</para>
/// </summary>
/// <typeparam name="T">Weight value type</typeparam>
public sealed class Connectome<T>
{
    private const int InitialCommitThreshold = 128;

    // dictionary because lookups are very frequent for `Set`
    private Dictionary<Synapse, T> _pending = new();

    private int _lastCommit;

    private CsrMatrix<T> _outbound; // row = pre, column = post
    private CsrMatrix<T> _inbound;  // row = post, column = pre

    /// <summary>Creates an empty connectome for neurons in <paramref name="pool" />.</summary>
    internal Connectome(NeuronPool pool)
    {
        Pool = pool;

        _outbound = new CsrMatrix<T>();
        _inbound  = new CsrMatrix<T>();
    }

    /// <summary>
    ///     <para>Create a connectome from prebuilt CSR arrays with one row per neuron in <paramref name="pool" />.</para>
    ///     <para>
    ///         This constructor expects <b>outbound</b> (presynaptic rows, postsynaptic columns) arrays and automatically
    ///         constructs a second CSR matrix for reverse inbound lookups.
    ///     </para>
    /// </summary>
    internal Connectome(NeuronPool pool, int[] rowPtr, int[] columns, T[] values)
    {
        if (rowPtr.Length != pool.Count + 1)
        {
            throw new ArgumentException(
                $"Synapses have {rowPtr.Length - 1} rows but the pool has {pool.Count} neurons",
                nameof(rowPtr)
            );
        }

        Pool = pool;

        _outbound = new CsrMatrix<T>(rowPtr, columns, values);
        _inbound  = _outbound.Transpose();
    }

    /// <summary>The neurons this connectome's indices refer to.</summary>
    internal NeuronPool Pool { get; }

    /// <summary>
    ///     <para>
    ///         Increments on every write (<see cref="this[int, int]" />) and successful commit (<see cref="CommitChanges" />).
    ///     </para>
    /// </summary>
    // more performant than an event since connectome writes can become extremely frequent with potentiation
    public int Version { get; private set; }

    /// <summary>
    ///     <para>
    ///         The weight from <paramref name="pre" /> to <paramref name="post" />, or <c>default</c> if no synapse exists.
    ///         Writing <c>default</c> (i.e., <c>0</c> for most <typeparamref name="T" />) removes the synapse.
    ///     </para>
    ///     <para>
    ///         Weight changes to existing synapses apply immediately. New and removed synapses are pending until
    ///         <see cref="CommitChanges" />. This indexer can perceive and modify pending writes, but bulk fetches via
    ///         <see cref="To" /> or <see cref="From" /> cannot.
    ///     </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">On write if either neuron doesn't exist.</exception>
    public T? this[int pre, int post]
    {
        get
        {
            VerifyExists(pre, post);

            // pending writes are newer and take precedence
            if (_pending.TryGetValue(new Synapse(pre, post), out var pending))
                return pending;

            var k = _outbound.IndexOf(pre, post);

            return k < 0 ? default : _outbound.ValueAt(k);
        }
        set => Set(pre, post, value);
    }

    private void Set(int pre, int post, T? value)
    {
        VerifyExists(pre, post);

        Version++;

        var synapse = new Synapse(pre, post);

        // check for an existing pending write and just overwrite it if present
        if (_pending.ContainsKey(synapse))
        {
            _pending[synapse] = value!;
            return;
        }

        var present = _outbound.IndexOf(pre, post);

        if (CsrMatrix<T>.IsDefault(value))
        {
            // removing something that doesn't exist (and isn't pending, see above) changes nothing
            if (present < 0)
                return;

            // arrays have to shrink for removals; fall into modification buffer
        }
        else if (present >= 0)
        {
            // quick update in both directions
            // safe because we already checked pending writes, so no accidental overwrite on commit
            _outbound.ValueAt(present)                    = value!;
            _inbound.ValueAt(_inbound.IndexOf(post, pre)) = value!;

            return;
        }

        // commit once the buffer holds as many writes as the matrix has entries
        // a commit costs O(rows + nnz + k log k), so letting k grow with nnz keeps the commit cost per write constant
        // small matrices will still auto-commit at 128 pending writes
        if (_pending.Count >= Math.Max(InitialCommitThreshold, _outbound.Count / 2))
            CommitChanges();

        _pending[synapse] = value!;
    }

    /// <summary>
    ///     <para>
    ///         Bidirectionally bake pending synaptic additions and removals which allows <see cref="To" />/
    ///         <see cref="From" /> to perceive them. Invokes automatically once the number of pending writes reaches
    ///         <c>max(synapses, 128)</c>.
    ///     </para>
    ///     <para>
    ///         Outbound merges sorted pending writes and is then transposed into inbound. Both lookups previously stored and
    ///         sorted their own pending writes which was less performant for larger commits.
    ///     </para>
    /// </summary>
    /// <returns>True if any change was committed and hence a <see cref="Version" /> bump</returns>
    public bool CommitChanges()
    {
        // skip sort and O(rows + nnz) rebuild so callers can commit defensively
        if (_pending.Count == 0)
            return false;

        var changes = _pending.Count;
        var batch   = new PendingWrite<T>[changes];
        var i       = 0;

        foreach (var pair in _pending)
            batch[i++] = new PendingWrite<T>(pair.Key.Pre, pair.Key.Post, pair.Value);

        Array.Sort(batch);

        // the merge sizes itself to the surviving synapses, and transposing swaps those dimensions for inbound
        _outbound = _outbound.Merge(batch);
        _inbound  = _outbound.Transpose();

        // deallocate the entire pending dictionary if this commit was over doubly smaller than the last
        // otherwise reuse it (Dictionary.Clear doesn't shrink the open-hashing buckets back)
        if (changes * 2 < _lastCommit)
            _pending = new Dictionary<Synapse, T>();
        else
            _pending.Clear();

        _lastCommit = changes;

        Version++;

        return true;
    }

    /// <summary>Remove all pending and committed synapses.</summary>
    public void Clear()
    {
        _outbound = new CsrMatrix<T>();
        _inbound  = new CsrMatrix<T>();

        _pending    = new Dictionary<Synapse, T>();
        _lastCommit = 0;

        Version++;
    }

    public RowSpan<T> To(int post)
    {
        VerifyExists(post, nameof(post));
        return _inbound.GetRow(post);
    }

    public RowSpan<T> From(int pre)
    {
        VerifyExists(pre, nameof(pre));
        return _outbound.GetRow(pre);
    }

    private void ThrowRangeException(string message, string name)
    {
        throw new ArgumentOutOfRangeException(name, $"{message} (count {Pool.Count})");
    }

    private void VerifyExists(int pre, int post)
    {
        if (Pool.Exists(pre) && Pool.Exists(post))
            return;

        var synapse = $"[{pre}, {post}]";

        ThrowRangeException($"Attempt to index nonexistent neuron(s) with synapse {synapse}", synapse);
    }

    private void VerifyExists(int i, string name)
    {
        if (!Pool.Exists(i))
            ThrowRangeException($"Attempt to index nonexistent neuron {i} in connectome", name);
    }

    // dictionary key for one directed synapse
    // two synapses may share a hash code, but that only marginally increases collisions
    private readonly record struct Synapse(int Pre, int Post);
}