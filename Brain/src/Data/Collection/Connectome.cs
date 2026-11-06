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

    public T? this[int pre, int post]
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
    ///         Bake pending synapse creations or removals into both internal directional matrices, which allows
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

    /// <summary>Remove all pending and committed synapses.</summary>
    public void Clear()
    {
        _inbound.Clear();
        _outbound.Clear();

        Version++;
    }

    /// <summary>
    ///     <para>
    ///         Replace every synapse with prebuilt outbound CSR arrays (see <see cref="CsrMatrix{T}.Load" /> for what they
    ///         must satisfy) and derive the inbound matrix from them.
    ///     </para>
    ///     <para>
    ///         Inbound is just outbound transposed, so it's built with a counting sort in O(rows + synapses): count how
    ///         many synapses land on each post, prefix-sum those counts into row starts, then deal every synapse into
    ///         its slot. Dealing pre in ascending order leaves every inbound row already sorted, which the CSR relies on.
    ///     </para>
    /// </summary>
    internal void Load(int rows, int[] rowPtr, int[] columns, T[] values)
    {
        _outbound.Load(rowPtr, columns, values);

        // construct rows for the inbound matrix from `post` ids in `columns`
        var inPtr = new int[rows + 1];

        foreach (var post in columns)
            inPtr[post + 1]++;

        for (var r = 0; r < rows; r++)
            inPtr[r + 1] += inPtr[r];

        // next free slot in each inbound row
        var cursor    = (int[])inPtr.Clone();
        var inColumns = new int[columns.Length];
        var inValues  = new T[values.Length];

        for (var pre = 0; pre < rows; pre++)
        {
            for (var k = rowPtr[pre]; k < rowPtr[pre + 1]; k++)
            {
                var slot = cursor[columns[k]]++;

                inColumns[slot] = pre;
                inValues[slot]  = values[k];
            }
        }

        _inbound.Load(inPtr, inColumns, inValues);

        Version++;
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