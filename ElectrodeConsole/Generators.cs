using Brain;

namespace ElectrodeConsole;

/// <summary>
/// Random network-setup strategies, deliberately not part of Brain: Brain owns
/// simulating whatever state exists, not deciding how that state was generated.
/// This is ElectrodeConsole's own copy — Electrode (the Unity project) has its
/// own, since neither references the other.
/// </summary>
internal static class Generators
{
    /// <summary>
    /// Randomly wires <paramref name="network"/> so each neuron has, on average,
    /// <paramref name="meanDegree"/> inbound connections, each a uniformly random
    /// weight along <c>[minWeight, maxWeight)</c>.
    /// </summary>
    public static void WireRandom(Network network, float meanDegree, float minWeight, float maxWeight, int? seed = null)
    {
        int count = network.Count;
        float connectChance = meanDegree / (count - 1);
        Random rng = seed is null ? new() : new(seed.Value);

        for (int post = 0; post < count; post++)
        {
            for (int pre = 0; pre < count; pre++)
            {
                if (pre == post)
                    continue;

                if ((float)rng.NextDouble() < connectChance)
                    network.Connect(pre, post, minWeight + (float)rng.NextDouble() * (maxWeight - minWeight));
            }
        }
    }

    /// <summary>Scatters every soma to a uniformly random point inside a sphere of radius <paramref name="r"/> centered on the origin.</summary>
    public static void PlaceRandomSphere(Network network, float r, int? seed = null)
    {
        Random rng = seed is null ? new() : new(seed.Value);
        float rSquared = r * r;

        foreach (Neuron n in network.Neurons)
        {
            float x, y, z;

            // rejection sampling: random cube coordinate, retry until inside the sphere
            do
            {
                x = (float)(rng.NextDouble() * 2 - 1) * r;
                y = (float)(rng.NextDouble() * 2 - 1) * r;
                z = (float)(rng.NextDouble() * 2 - 1) * r;
            }
            while (x * x + y * y + z * z > rSquared);

            n.MoveTo(x, y, z);
        }
    }
}
