using Brain.Data.Collection.CSR;

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

    /// <summary>Creates an empty connectome with room for <paramref name="size" /> neurons.</summary>
    public Connectome(int size)
    {
        _inbound  = new CsrMatrix<T>(size);
        _outbound = new CsrMatrix<T>(size);
    }

    public T this[int pre, int post]
    {
        get => _outbound[pre, post];
        set
        {
            _inbound[post, pre]  = value;
            _outbound[pre, post] = value;
        }
    }

    /// <summary>
    ///     <para>Applies pending synapse creations or removals in both directions.</para>
    ///     <para>
    ///         <see cref="To" /> and <see cref="From" /> only see committed rows, so call this must execute before fetching
    ///         up-to-date rows.
    ///     </para>
    /// </summary>
    public void CommitChanges()
    {
        _inbound.CommitChanges();
        _outbound.CommitChanges();
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