using System;
using System.Collections.Generic;
using System.Numerics;
using Brain.Data.Collection.Pool;

namespace Brain.Data.Collection.Space;

/// <summary>
///     A helper struct that internally hashes neurons into cubic cells for faster proximal fetches.
/// </summary>
public readonly ref struct SpatialQuery
{
    private readonly Dictionary<Cell3D, List<int>> _cells = new();

    private readonly NeuronPool _neurons;
    private readonly float      _cellSize;

    /// <summary>
    ///     Partition all neurons in <paramref name="neurons" /> to cells of size <paramref name="cellSize" />.
    /// </summary>
    public SpatialQuery(NeuronPool neurons, float cellSize) : this(neurons, cellSize, default, true)
    {
    }

    /// <summary>
    ///     Partition all neurons in <paramref name="whitelist" /> from <paramref name="neurons" /> to cells of size
    ///     <paramref name="cellSize" />
    /// </summary>
    public SpatialQuery(NeuronPool neurons, float cellSize, ReadOnlySpan<int> whitelist) : this(
        neurons,
        cellSize,
        whitelist,
        false
    )
    {
    }

    private SpatialQuery(NeuronPool neurons, float cellSize, ReadOnlySpan<int> whitelist, bool all)
    {
        _neurons  = neurons;
        _cellSize = cellSize;

        var x = neurons.X;
        var y = neurons.Y;
        var z = neurons.Z;

        var count = all ? neurons.Count : whitelist.Length;

        for (var i = 0; i < count; i++)
        {
            var n    = all ? i : whitelist[i];
            var cell = CellOf(x[n], y[n], z[n]);

            // TODO: System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault on CoreCLR
            if (!_cells.TryGetValue(cell, out var bucket))
                bucket = _cells[cell] = []; // second hash op

            bucket.Add(n);
        }
    }

    /// <summary>
    ///     Find all neurons within <paramref name="radius" /> of neuron <paramref name="neuron" />.
    /// </summary>
    /// <returns>
    ///     A <see cref="PollResult" /> per neuron that report's the neuron's ID and squared distance from
    ///     <paramref name="neuron" />
    /// </returns>
    // one contiguous span to let the consumer sort without maintaining two parallel spans
    public Span<PollResult> Poll(int neuron, float radius)
    {
        var rings = (int)MathF.Ceiling(radius / _cellSize);

        var x = _neurons.X;
        var y = _neurons.Y;
        var z = _neurons.Z;

        var pos  = new Vector3(x[neuron], y[neuron], z[neuron]);
        var home = CellOf(x[neuron], y[neuron], z[neuron]);

        var rSq = radius * radius;

        // TODO: consider List<PollResult> with CollectionsMarshal.AsSpan() on CoreCLR
        var result = new PollResult[16];
        var count  = 0;

        for (var dx = -rings; dx <= rings; dx++)
        for (var dy = -rings; dy <= rings; dy++)
        for (var dz = -rings; dz <= rings; dz++)
        {
            if (!_cells.TryGetValue(home + new Cell3D(dx, dy, dz), out var bucket))
                continue;

            foreach (var n in bucket)
            {
                var distSq = (new Vector3(x[n], y[n], z[n]) - pos).LengthSquared();

                if (distSq > rSq)
                    continue;

                result[count++] = new PollResult(n, distSq);

                if (count >= result.Length)
                    Array.Resize(ref result, count * 2);
            }
        }

        return result.AsSpan(0, count);
    }

    private Cell3D CellOf(float x, float y, float z)
    {
        return new Cell3D(Decimate(x / _cellSize), Decimate(y / _cellSize), Decimate(z / _cellSize));

        int Decimate(float n)
        {
            return (int)MathF.Floor(n); // cannot enclose _cellSize
        }
    }
}