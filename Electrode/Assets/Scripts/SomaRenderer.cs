using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cascade
{
    /// <summary>
    ///     <para>
    ///         Draws every neuron in a <see cref="Sandbox" />'s pool as a camera-facing disc, all in one instanced draw via
    ///         <see cref="Graphics.RenderPrimitives" />. The CPU fills a buffer of positions and spike ages; the GPU builds
    ///         each disc's geometry itself (see Soma.shader), so there is no mesh.
    ///     </para>
    /// </summary>
    [RequireComponent(typeof(Sandbox))]
    public sealed class SomaRenderer : MonoBehaviour
    {
        private const string ShaderPath = "Custom/Soma";

        private const int VerticesPerSoma = 6; // one quad, two triangles

        private static readonly int NeuronBufferId = Shader.PropertyToID("_NeuronBuffer");
        private static readonly int NeuronScaleId  = Shader.PropertyToID("_NeuronScale");
        private static readonly int GlowTimeId     = Shader.PropertyToID("_GlowTime");

        [SerializeField] private float neuronScale = 0.08f;

        [Tooltip("Real seconds a spike's glow takes to fade to ~37%, whatever the time scale")]
        [SerializeField] private float glowSeconds = 0.15f;

        private Soma[]                _data;
        private Material              _material;
        private MaterialPropertyBlock _properties;

        private Sandbox _sandbox;

        /// <summary>
        ///     Per-neuron positions, spike ages, and selection states per frame. Always expands to match the network's current
        ///     <see cref="Brain.Data.Collection.NeuronPool" /> capacity.
        /// </summary>
        public GraphicsBuffer InstanceBuffer { get; private set; }

        /// <summary>Soma radius in world units.</summary>
        public float Radius => neuronScale * 0.5f;

        /// <summary>
        ///     <para>
        ///         This frame's glow decay constant in SIMULATED ms: a neuron glows <c>exp(-age / GlowTime)</c>, where age is
        ///         the simulated time since its last spike. Shared with Synapse.shader so synapses light up in sync.
        ///     </para>
        ///     <para>
        ///         Converted from <see cref="glowSeconds" /> through the time scale so a spike stays visible for the same
        ///         real duration at any speed. Never below one step, so a paused network still shows its last step's spikes.
        ///     </para>
        /// </summary>
        public float GlowTime { get; private set; }

        private void Awake()
        {
            _sandbox    = GetComponent<Sandbox>();
            _properties = new MaterialPropertyBlock();

            var shader = Shader.Find(ShaderPath);

            if (shader == null)
            {
                Debug.LogError($"Shader \"{ShaderPath}\" not found");
                enabled = false;
                return;
            }

            _material = new Material(shader);
        }

        // LateUpdate guarantees sandbox already stepped this frame
        private void LateUpdate()
        {
            var neurons = _sandbox.Network.Neurons;
            var count   = neurons.Count;

            if (count == 0)
                return;

            // reallocate lazily
            // null check for first use or after OnDisable() clears the buffer
            // OR when the current buffer can't accomodate the current number of existing neurons
            if (InstanceBuffer is null || InstanceBuffer.count < count)
            {
                InstanceBuffer?.Dispose();

                _data = new Soma[neurons.Capacity];
                InstanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    neurons.Capacity,
                    Marshal.SizeOf<Soma>()
                );
            }

            var x         = neurons.X;
            var y         = neurons.Y;
            var z         = neurons.Z;
            var lastSpike = neurons.LastSpike;
            var selection = _sandbox.Selection;
            var now       = _sandbox.Network.Time;

            // many steps can run per frame, so the one-step Spike flag would miss most spikes. time since the last
            // spike
            // can't miss any. the GPU turns it into a glow, which saves an exp() per neuron here
            GlowTime = (float)Math.Max(glowSeconds * 1000.0 * _sandbox.TimeScale, Sandbox.StepSize.TotalMilliseconds);

            // populate CPU-side array from the SoA spans and ship only the live part to the shader
            // TODO: turn _data into SoA (?)
            for (var i = 0; i < count; i++)
            {
                _data[i].Position = new Vector3(x[i], y[i], z[i]);

                // never-spiked neurons have lastSpike = -infinity. a large finite age glows exactly as little (exp
                // underflows to 0) without trusting every GPU's infinity handling
                _data[i].Age      = (float)Math.Min(now - lastSpike[i], 1e9);
                _data[i].Selected = selection.Contains(i) ? 1f : 0f;
            }

            InstanceBuffer.SetData(_data, 0, 0, count);

            _properties.SetBuffer(NeuronBufferId, InstanceBuffer);
            _properties.SetFloat(NeuronScaleId, neuronScale);
            _properties.SetFloat(GlowTimeId,    GlowTime);

            var renderParams = new RenderParams(_material)
            {
                // cull the whole draw only if this is off-screen entirely
                worldBounds = new Bounds(transform.position, Vector3.one * 10000f), matProps = _properties
            };

            // no mesh; shader turns vertices 0..5 of instance i into neuron i's quad
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, VerticesPerSoma, count);
        }

        private void OnDisable()
        {
            InstanceBuffer?.Dispose();
            InstanceBuffer = null;
        }

        // struct must match Soma.shader and Synapse.shader in field order and types
        [StructLayout(LayoutKind.Sequential)]
        private struct Soma
        {
            public Vector3 Position;
            public float   Age;      // simulated ms since the neuron's last spike
            public float   Selected; // 1 or 0, shader buffers do not support bool
        }
    }
}