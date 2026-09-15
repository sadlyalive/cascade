using Brain;
using System;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Owns a <see cref="Brain.Network"/> and steps it every frame. Purely a driver —
/// no rendering here. Attach to one empty GameObject in the scene; other scripts
/// (like <see cref="NeuronRenderer"/>) read <see cref="Network"/> off this.
/// </summary>
public sealed class Sandbox : MonoBehaviour
{
    private const float THIRD = 1f / 3f;

    [Header("Simulation")]
    [SerializeField] private float timeScale = 1f;

    /// <summary>The live neural network.</summary>
    public Network Network { get; private set; }

    /// <summary>Adjust playback speed at runtime.</summary>
    public void SetTimeScale(float value) => timeScale = value;

    private void Update()
    {
        Network.Step(TimeSpan.FromSeconds(Time.deltaTime * timeScale));
    }

    /// <summary>
    /// <para>Randomly wires <see cref="Network"/> such that each neuron receives <paramref name="meanDegree"/> inbound connections on average.</para>
    /// <para>Each connection receives a random weight within <c>[<paramref name="minWeight"/>, <paramref name="maxWeight"/>]</c> (inclusive).</para>
    /// </summary>
    public void WireRandom(float meanDegree, float minWeight, float maxWeight, uint seed = 0)
    {
        Random rng = MakeRandom(seed);

        int count = Network.Count;
        float connectChance = meanDegree / (count - 1);

        for (int post = 0; post < count; post++)
        {
            for (int pre = 0; pre < count; pre++)
            {
                if (pre == post)
                    continue;

                if (rng.NextFloat() < connectChance)
                    Network.Connect(pre, post, rng.NextFloat(minWeight, maxWeight));
            }
        }
    }

    // TODO: Hammersley sequence for less clumping (?)
    /// <summary>
    /// Scatters every soma to random positions uniformly distributed within (not just on the surface of) a sphere of radius <paramref name="r"/> and origin <c>(0, 0, 0)</c>.
    /// </summary>
    public void PlaceRandomSphere(float r, uint seed = 0)
    {
        Random rng = MakeRandom(seed);

        foreach (Neuron n in Network.Neurons)
        {
            // cube-rooting the random scalar compensates for r^3 volume growth
            float3 p = math.pow(rng.NextFloat(), THIRD) * r * rng.NextFloat3Direction();
            n.MoveTo(p.x, p.y, p.z);
        }
    }

    /// <summary>
    /// Scatters every soma to random positions within an axis-aligned box of size <c>(<paramref name="sizeX"/>, <paramref name="sizeY"/>, <paramref name="sizeZ"/>)</c> and origin <c>(0, 0, 0)</c>.
    /// </summary>
    public void PlaceRandomBox(float sizeX, float sizeY, float sizeZ, uint seed = 0)
    {
        Random rng = MakeRandom(seed);

        float halfX = sizeX / 2;
        float halfY = sizeY / 2;
        float halfZ = sizeZ / 2;

        foreach (Neuron n in Network.Neurons)
            n.MoveTo(rng.NextFloat(-halfX, halfX), rng.NextFloat(-halfY, halfY), rng.NextFloat(-halfZ, halfZ));
    }

    /// <summary>
    /// <para>Returns an instance-bound random number generator unlike <see cref="UnityEngine.Random"/>.</para>
    /// <para>Uses a xor-shift algorithm which is incompatible with seed <c>0</c> (default value). Uses a time-dependent seed if <paramref name="seed"/> is <c>0</c>.</para>
    /// </summary>
    // instance-bound unlike UnityEngine.Random
    // uses xor-shift algorithm which is not compatible with seed 0
    private static Random MakeRandom(uint seed = 0)
    {
        return seed < 1 ? new Random((uint)Environment.TickCount) : new Random(seed);
    }
}
