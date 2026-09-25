namespace Brain.Data.Collection.CSR;

public sealed class Connectome<T>
{
    private readonly CsrArray<T> _inbound;
    private readonly CsrArray<T> _outbound;

    /// <summary>Creates an empty connectome for <paramref name="neurons" /> neurons, with no synapses yet.</summary>
    public Connectome(int neurons)
    {
        _inbound  = new CsrArray<T>(neurons);
        _outbound = new CsrArray<T>(neurons);
    }

    public int Neurons => _outbound.Rows;

    public T this[int pre, int post]
    {
        get => _outbound[pre, post];
        set
        {
            _inbound[post, pre]  = value;
            _outbound[pre, post] = value;
        }
    }

    public CsrArray<T>.RowRange To(int post)
    {
        return _inbound.GetRow(post);
    }

    public CsrArray<T>.RowRange From(int pre)
    {
        return _outbound.GetRow(pre);
    }
}