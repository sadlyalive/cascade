using System;
using System.Collections.Generic;
using System.Linq;
using Brain;
using Cascade.UI.Form.Field;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Cascade.UI.Form.Synapse
{
    /// <summary>
    ///     Randomly wires the selection so each neuron receives a mean number of inputs, optionally preferring nearby
    ///     sources (see <see cref="Wire" />).
    /// </summary>
    public sealed class WireRandomForm : ActionForm
    {
        private readonly FloatFormField _degree;
        private readonly FloatFormField _max;
        private readonly FloatFormField _min;
        private readonly FloatFormField _reach;
        private readonly Sandbox        _sandbox;
        private readonly UIntFormField  _seed;

        public WireRandomForm(Sandbox sandbox) : base($"Wire randomly ({sandbox.Selection.Count} selected)")
        {
            _degree = Add(new FloatFormField
            {
                Label    = "Mean degree",
                Value    = 3f,
                MinValue = 0f
            });
            _reach = Add(new FloatFormField
            {
                Label    = "Reach (0 = any)",
                Value    = 0f,
                MinValue = 0f
            });
            _min = Add(new FloatFormField
            {
                Label   = "Min weight",
                Value   = 1f,
                Delayed = true
            });
            _max = Add(new FloatFormField
            {
                Label   = "Max weight",
                Value   = 10f,
                Delayed = true
            });
            _seed = Add(new UIntFormField
            {
                Label = "Seed"
            });

            _min.Changed += () =>
            {
                if (_min.Value > _max.Value)
                    _max.Value = _min.Value;
            };
            _max.Changed += () =>
            {
                if (_max.Value < _min.Value)
                    _min.Value = _max.Value;
            };

            _sandbox = sandbox;
        }

        public override void Apply()
        {
            Wire(_sandbox.Network,
                _sandbox.Selection.Order,
                _degree.Value,
                _reach.Value,
                _min.Value,
                _max.Value,
                _seed.Value);
        }

        /// <summary>
        ///     <para>
        ///         Gives every neuron in <paramref name="targets" /> a Poisson-distributed number of inputs (mean
        ///         <paramref name="meanDegree" />) from other neurons in <paramref name="targets" />, each with a uniformly
        ///         random weight in [<paramref name="minWeight" />, <paramref name="maxWeight" />). No self-synapses.
        ///     </para>
        ///     <para>
        ///         With <paramref name="reach" /> &gt; 0, inputs only come from neurons closer than <paramref name="reach" />,
        ///         and the chance falls linearly with distance (<c>1 - distance / reach</c>). A neuron with fewer candidates
        ///         in reach than it drew gets them all, so isolated neurons get none.
        ///     </para>
        ///     <para>
        ///         Costs about O(synapses) without reach and O(neurons × candidates in reach) with it, instead of testing
        ///         every pair: at 130k neurons that's the difference between ~500k draws and ~17 billion tests.
        ///     </para>
        ///     <para>Static so it can run (and be tested) without the UI.</para>
        /// </summary>
        public static void Wire(Network           network,
                                ReadOnlySpan<int> targets,
                                float             meanDegree,
                                float             reach,
                                float             minWeight,
                                float             maxWeight,
                                uint              seed = 0)
        {
            if (targets.Length < 2 || meanDegree <= 0f)
                return;

            var rng = NewRandom(seed);

            if (reach > 0f)
                WireNear(network, targets, meanDegree, reach, minWeight, maxWeight, ref rng);
            else
                WireAny(network, targets, meanDegree, minWeight, maxWeight, ref rng);
        }

        private static void WireAny(Network           network,
                                    ReadOnlySpan<int> targets,
                                    float             meanDegree,
                                    float             minWeight,
                                    float             maxWeight,
                                    ref Random        rng)
        {
            var connectome = network.Connectome;
            var others     = targets.Length - 1; // possible sources for any one neuron
            var picks      = new HashSet<int>();

            for (var i = 0; i < targets.Length; i++)
            {
                // testing each of the n - 1 others with chance meanDegree / (n - 1) gives a binomial count, which is
                // Poisson for large n. drawing the count directly skips every test that would have failed
                var k = Math.Min(Poisson(ref rng, meanDegree), others);

                // Floyd's algorithm: k DISTINCT positions out of `others` in O(k). each step either takes its random
                // pick or, if that's already taken, the step's own top slot, which no earlier step could have reached
                picks.Clear();

                for (var top = others - k; top < others; top++)
                {
                    var pick = rng.NextInt(0, top + 1);
                    picks.Add(picks.Contains(pick) ? top : pick);
                }

                var post = targets[i];

                // positions count the others only, so shift past i itself. that's what rules out self-synapses
                foreach (var pick in picks)
                    connectome[targets[pick < i ? pick : pick + 1], post] = rng.NextFloat(minWeight, maxWeight);
            }
        }

        private static void WireNear(Network           network,
                                     ReadOnlySpan<int> targets,
                                     float             meanDegree,
                                     float             reach,
                                     float             minWeight,
                                     float             maxWeight,
                                     ref Random        rng)
        {
            var connectome = network.Connectome;
            var neurons    = network.Neurons;
            var x          = neurons.X;
            var y          = neurons.Y;
            var z          = neurons.Z;

            // bucket every target into cubic cells as wide as `reach`. anything within reach of a point lies in the
            // point's own cell or one of the 26 around it, so each neuron only looks there instead of at everyone
            var cells = new Dictionary<int3, List<int>>();

            foreach (var id in targets)
            {
                var cell = (int3)math.floor(new float3(x[id], y[id], z[id]) / reach);

                if (!cells.TryGetValue(cell, out var bucket))
                    cells[cell] = bucket = new List<int>();

                bucket.Add(id);
            }

            var reachSq = reach * reach;

            // reused per neuron: everyone in reach, and their "finish times" (see below)
            var candidates = new int[64];
            var times      = new float[64];

            foreach (var post in targets)
            {
                var p    = new float3(x[post], y[post], z[post]);
                var home = (int3)math.floor(p / reach);
                var c    = 0;

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (!cells.TryGetValue(home + new int3(dx, dy, dz), out var bucket))
                        continue;

                    foreach (var pre in bucket.Where(pre => pre != post))
                    {
                        // compare squared first: most of the 27 cells' occupants are out of reach, and they never
                        // need the square root
                        var distanceSq = math.distancesq(p, new float3(x[pre], y[pre], z[pre]));

                        if (distanceSq >= reachSq)
                            continue;

                        var distance = math.sqrt(distanceSq);

                        if (c == candidates.Length)
                        {
                            Array.Resize(ref candidates, c * 2);
                            Array.Resize(ref times,      c * 2);
                        }

                        // weighted sampling without replacement as a race: every candidate "finishes" after a random
                        // exponential time divided by its weight, and the k fastest win. heavier (closer) candidates
                        // tend to finish sooner, and nobody can win twice
                        var weight = 1f - distance / reach;

                        candidates[c] = pre;
                        times[c]      = -math.log(1f - rng.NextFloat()) / weight;
                        c++;
                    }
                }

                var k = Math.Min(Poisson(ref rng, meanDegree), c);

                if (k == 0)
                    continue;

                // only the k fastest matter, and k (a few) is far smaller than c (dozens), so select them directly in
                // O(k * c) instead of sorting all c. skipped when everyone wins anyway
                for (var j = 0; j < k && k < c; j++)
                {
                    var fastest = j;

                    for (var l = j + 1; l < c; l++)
                    {
                        if (times[l] < times[fastest])
                            fastest = l;
                    }

                    (times[j], times[fastest])           = (times[fastest], times[j]);
                    (candidates[j], candidates[fastest]) = (candidates[fastest], candidates[j]);
                }

                for (var j = 0; j < k; j++)
                    connectome[candidates[j], post] = rng.NextFloat(minWeight, maxWeight);
            }
        }

        /// <summary>A Poisson-distributed count with the given <paramref name="mean" />.</summary>
        private static int Poisson(ref Random rng, float mean)
        {
            // Knuth's method needs about `mean` draws, and e^-mean underflows to 0 past ~87 in floats (it would never
            // stop), so large means use the normal approximation, which is accurate there
            if (mean > 30f)
            {
                // Box-Muller: two uniforms -> one standard normal
                var u      = 1f - rng.NextFloat(); // (0, 1], log(0) would be -infinity
                var normal = math.sqrt(-2f * math.log(u)) * math.cos(2f * math.PI * rng.NextFloat());

                return math.max(0, (int)math.round(mean + math.sqrt(mean) * normal));
            }

            // Knuth: count how many uniforms it takes for their running product to drop below e^-mean
            var limit   = math.exp(-mean);
            var product = rng.NextFloat();
            var k       = 0;

            while (product > limit)
            {
                k++;
                product *= rng.NextFloat();
            }

            return k;
        }
    }
}