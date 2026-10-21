namespace Brain.Data.Collection;

/// <summary>
///     <para>Records synaptic weights between neurons in both directions (inbound and outbound).</para>
///     <para>Does not implement an interface since interfaces prevent JIT inlining.</para>
/// </summary>
/// <typeparam name="T">Weight value type</typeparam>
public sealed class Connectome<T>
{
    private readonly CsrMatrix<T> _inbound;
    private readonly CsrMatrix<T> _outbound;

    /// <summary>Creates an empty connectome and preallocates for <paramref name="size" /> neurons.</summary>
    public Connectome(int size)
    {
        _inbound  = new CsrMatrix<T>(size);
        _outbound = new CsrMatrix<T>(size);
    }

    /// <summary>
    ///     <para>
    ///         Increments on every write (<see cref="this[int,int]" />) and successful commit (<see cref="CommitChanges" />
    ///         ).
    ///     </para>
    ///     <para>
    ///         Exists primarily for external listeners to cheaply discern synaptic changes (e.g., rendering new synapses or
    ///         weight changes). This is more performant than an event since connectome writes are extremely frequent with
    ///         potentiation.
    ///     </para>
    /// </summary>
    public int Version { get; private set; }

    public T this[int pre, int post]
    {
        get => _outbound[pre, post];
        set
        {
            _inbound[post, pre]  = value;
            _outbound[pre, post] = value;

            Version++;
        }
    }

    /// <summary>
    ///     <para>
    ///         Bakes pending synapse creations or removals into both internal directional matrices, which allows
    ///         <see cref="To" />/<see cref="From" /> to perceive them.
    ///     </para>
    /// </summary>
    /// <returns>True if any change was committed and hence a <see cref="Version" /> bump</returns>
    public bool CommitChanges()
    {
        var success = _inbound.CommitChanges() | _outbound.CommitChanges();

        if (success)
            Version++;

        return success;
    }

    public RowSpan<T> To(int post)
    {
        return _inbound.GetRow(post);
    }

    public RowSpan<T> From(int pre)
    {
        return _outbound.GetRow(pre);
    }
}