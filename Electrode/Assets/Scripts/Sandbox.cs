using System;
using Brain;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Cascade
{
    /// <summary>
    ///     Owns a <see cref="Brain.Network" /> and steps it every frame. Provides an interface to read and modify the current
    ///     neural network.
    /// </summary>
    public sealed class Sandbox : MonoBehaviour
    {
        [Header("Simulation")]
        [field: SerializeField] public float TimeScale { get; set; } = 1f;

        /// <summary>The live neural network.</summary>
        public Network Network { get; private set; }

        private void Awake()
        {
            Network = new Network(1024);
            PlaceRandomSphere(8);
            WireRandom(3, 1, 10);
        }

        private void Update()
        {
            Network?.Step(TimeSpan.FromSeconds(Time.deltaTime * TimeScale));
        }

        /// <summary>
        ///     <para>
        ///         Randomly wires <see cref="Network" /> such that each neuron receives <paramref name="meanDegree" /> inbound
        ///         connections on average.
        ///     </para>
        ///     <para>
        ///         Each connection receives a random weight within
        ///         <c>[<paramref name="minWeight" />, <paramref name="maxWeight" />]</c> (inclusive).
        ///     </para>
        /// </summary>
        public void WireRandom(float meanDegree, float minWeight, float maxWeight, uint seed = 0)
        {
            var rng = MakeRandom(seed);

            var count         = Network.Count;
            var connectChance = meanDegree / (count - 1);

            for (var post = 0; post < count; post++)
            {
                for (var pre = 0; pre < count; pre++)
                {
                    if (pre == post)
                        continue;

                    if (rng.NextFloat() < connectChance)
                        Network.Connectome[pre, post] = rng.NextFloat(minWeight, maxWeight);
                }
            }
        }

        // TODO: Hammersley sequence for less clumping (?)
        /// <summary>
        ///     Scatters every soma to random positions uniformly distributed inside a sphere of radius <paramref name="r" /> at
        ///     <paramref name="origin" />.
        /// </summary>
        public void PlaceRandomSphere(float r, float3 origin = default, uint seed = 0)
        {
            const float third = 1f / 3f;

            var rng = MakeRandom(seed);

            foreach (var n in Network.Neurons)
            {
                // cube-rooting the random scalar compensates for r^3 volume growth
                var p = origin + math.pow(rng.NextFloat(), third) * r * rng.NextFloat3Direction();
                n.MoveTo(p.x, p.y, p.z);
            }
        }

        /// <summary>
        ///     Scatters every soma to random positions within an axis-aligned box of size <paramref name="size" /> and origin
        ///     <c>size / 2</c>.
        /// </summary>
        public void PlaceRandomBox(float3 size, float3 origin = default, uint seed = 0)
        {
            var rng = MakeRandom(seed);

            var halfX = size.x / 2f;
            var halfY = size.y / 2f;
            var halfZ = size.z / 2f;

            foreach (var n in Network.Neurons)
            {
                n.MoveTo(
                    origin.x + rng.NextFloat(-halfX, halfX),
                    origin.y + rng.NextFloat(-halfY, halfY),
                    origin.z + rng.NextFloat(-halfZ, halfZ)
                );
            }
        }

        /// <summary>
        ///     Injects <paramref name="current" /> into <paramref name="count" /> random neurons.
        /// </summary>
        public void InjectRandom(float current, int count, uint seed = 0)
        {
            var rng   = MakeRandom(seed);
            var total = Network.Count;

            count = Mathf.Clamp(count, 0, total);

            // partial Fisher-Yates: only shuffle as many slots as there are distinct picks
            var indices = new int[total];
            for (var i = 0; i < total; i++)
                indices[i] = i;

            for (var i = 0; i < count; i++)
            {
                var j = rng.NextInt(i, total);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (var i = 0; i < count; i++)
                Network.Neurons[indices[i]].InputCurrent = current;
        }

        /// <summary>
        ///     <para>Returns an instance-bound random number generator unlike <see cref="UnityEngine.Random" />.</para>
        ///     <para>
        ///         Uses a xor-shift algorithm which is incompatible with seed <c>0</c> (default value). Uses a time-dependent
        ///         seed if <paramref name="seed" /> is <c>0</c>.
        ///     </para>
        /// </summary>
        // instance-bound unlike UnityEngine.Random
        // uses xor-shift algorithm which is not compatible with seed 0
        private static Random MakeRandom(uint seed = 0)
        {
            return seed < 1 ? new Random((uint)Environment.TickCount) : new Random(seed);
        }
    }
}