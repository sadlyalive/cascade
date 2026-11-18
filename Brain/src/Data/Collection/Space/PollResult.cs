using System;

namespace Brain.Data.Collection.Space;

public readonly record struct PollResult(int Id, float DistanceSq) : IComparable<PollResult>
{
    /// <summary>
    ///     Neuron <see cref="Id" />'s distance from the neuron passed to <see cref="SpatialQuery.Poll" />
    ///     <remarks>Performs a square-root on get</remarks>
    /// </summary>
    public float Distance => MathF.Sqrt(DistanceSq);

    public int CompareTo(PollResult other)
    {
        return DistanceSq.CompareTo(other.DistanceSq);
    }
}