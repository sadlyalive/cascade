using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cascade
{
    /// <summary>Which synapses <see cref="SynapseRenderer" /> draws.</summary>
    public enum SynapseFilter
    {
        /// <summary>Every synapse</summary>
        All,

        /// <summary>Only synapses with recent action potentials</summary>
        Active,

        /// <summary>Every synapse, but fade and eventually ignore further ones</summary>
        Near,

        /// <summary>Only synapses with at least one neuron (pre or post) in <see cref="Sandbox.Selection" /></summary>
        Selection,

        /// <summary>No synapses at all</summary>
        None
    }

    /// <summary>
    ///     <para>
    ///         Draws synapses in <see cref="Sandbox" />'s connectome as a flat arrow from the presynaptic to postsynaptic
    ///         soma with thickness proportional to weight in a single draw via <see cref="Graphics.RenderPrimitives" />.
    ///     </para>
    ///     <para>
    ///         Uploads only <c>int pre, int post, float weight</c> to the GPU per synapse. Soma positions come from
    ///         <see cref="SomaRenderer" />'s buffer.
    ///     </para>
    /// </summary>
    [RequireComponent(typeof(SomaRenderer))]
    [DefaultExecutionOrder(1)] // LateUpdate after SomaRenderer's, so its buffer is this frame's and won't be replaced
    public sealed class SynapseRenderer : MonoBehaviour
    {
        private const string ShaderPath = "Custom/Synapse";

        private const float Far                = 10000f;
        private const int   VerticesPerSynapse = 9; // shaft quad (6) + head triangle (3)

        private static readonly int NeuronBufferId   = Shader.PropertyToID("neuron_buffer");
        private static readonly int SynapseBufferId  = Shader.PropertyToID("synapse_buffer");
        private static readonly int SomaRadiusId     = Shader.PropertyToID("soma_radius");
        private static readonly int WidthPerWeightId = Shader.PropertyToID("width_per_weight");
        private static readonly int MinWidthId       = Shader.PropertyToID("min_width");
        private static readonly int MaxWidthId       = Shader.PropertyToID("max_width");
        private static readonly int HeadLengthId     = Shader.PropertyToID("head_length");
        private static readonly int HeadWidthScaleId = Shader.PropertyToID("head_width_scale");
        private static readonly int SideOffsetId     = Shader.PropertyToID("side_offset");
        private static readonly int RestColorId      = Shader.PropertyToID("rest_color");
        private static readonly int ActiveColorId    = Shader.PropertyToID("active_color");
        private static readonly int FadeStartId      = Shader.PropertyToID("fade_start");
        private static readonly int FadeEndId        = Shader.PropertyToID("fade_end");
        private static readonly int GlowTimeId       = Shader.PropertyToID("glow_time");
        private static readonly int SelectionOnlyId  = Shader.PropertyToID("selection_only");

        [Header("Shape")]
        [SerializeField] private float widthPerWeight = 0.003f;
        [SerializeField] private float minWidth = 0.003f;
        [SerializeField] private float maxWidth = 0.03f;

        [SerializeField] private float headLength     = 0.06f;
        [SerializeField] private float headWidthScale = 2.5f;

        [SerializeField] private float reciprocalOffset = 0.02f;

        [Header("Color")]
        [SerializeField] private Color restColor = new(0.6f, 0.65f, 0.8f, 0.35f);
        [SerializeField] private Color activeColor = new(1f, 0.9f, 0.1f, 0.9f);

        [Header("Distance fade (relative to camera)")]
        [SerializeField] private float fadeStart = 8f;
        [SerializeField] private float fadeEnd = 16f;

        /// <summary>
        ///     <para>Defines which synapses to draw.</para>
        ///     <para>
        ///         <see cref="SynapseFilter.Selection" /> costs nothing on the CPU; the shader just hides synapses where both
        ///         neurons lack <c>selected</c> flags.
        ///     </para>
        /// </summary>
        [field: SerializeField] public SynapseFilter Filter { get; set; } = SynapseFilter.All;

        private GraphicsBuffer        _buffer;
        private int                   _count;
        private Synapse[]             _data = new Synapse[256];
        private Material              _material;
        private MaterialPropertyBlock _properties;
        private Sandbox               _sandbox;
        private SomaRenderer          _somas;
        private int                   _uploadCount   = -1;
        private int                   _uploadVersion = -1;

        private void Awake()
        {
            _sandbox    = GetComponent<Sandbox>();
            _somas      = GetComponent<SomaRenderer>();
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

        private void LateUpdate()
        {
            var somas = _somas.InstanceBuffer;

            if (somas is null)
                return;

            var network = _sandbox.Network;

            // re-gather and re-upload only when the synapses (or which neurons exist) changed since the last upload
            if (_buffer is null || _uploadVersion != network.Connectome.Version || _uploadCount != network.Count)
            {
                _count = Gather();

                if (_buffer is null || _buffer.count < _count)
                {
                    _buffer?.Dispose();
                    _buffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        _data.Length,
                        Marshal.SizeOf<Synapse>()
                    );
                }

                _buffer.SetData(_data, 0, 0, _count);

                _uploadVersion = network.Connectome.Version;
                _uploadCount   = network.Count;
            }

            if (_count == 0 || Filter == SynapseFilter.None)
                return;

            var near = Filter == SynapseFilter.Near;

            _properties.SetBuffer(NeuronBufferId,  somas);
            _properties.SetBuffer(SynapseBufferId, _buffer);
            _properties.SetFloat(SomaRadiusId,     _somas.Radius);
            _properties.SetFloat(WidthPerWeightId, widthPerWeight);
            _properties.SetFloat(MinWidthId,       minWidth);
            _properties.SetFloat(MaxWidthId,       maxWidth);
            _properties.SetFloat(HeadLengthId,     headLength);
            _properties.SetFloat(HeadWidthScaleId, headWidthScale);
            _properties.SetFloat(SideOffsetId,     reciprocalOffset);
            _properties.SetFloat(FadeStartId,      near ? fadeStart : Far);
            _properties.SetFloat(FadeEndId,        near ? fadeEnd : Far);
            _properties.SetFloat(GlowTimeId,       _somas.GlowTime);
            _properties.SetFloat(SelectionOnlyId,  Filter == SynapseFilter.Selection ? 1f : 0f);
            _properties.SetColor(RestColorId,   Filter == SynapseFilter.Active ? Color.clear : restColor);
            _properties.SetColor(ActiveColorId, activeColor);

            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * 10000f), matProps = _properties
            };

            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, VerticesPerSynapse, _count);
        }

        private void OnDisable()
        {
            _buffer?.Dispose();
            _buffer = null; // also forces a fresh upload on re-enable
        }

        // flattens the connectome's outbound rows into (pre, post, weight) triples. only runs when Connectome.Version
        // changes. at O(synapses) this hurts once learning and synaptogenesis exist since they constantly update the
        // connectome. at fly scale we would need to filter out rendering some synapses regardless
        private int Gather()
        {
            var network    = _sandbox.Network;
            var connectome = network.Connectome;
            var neurons    = network.Count;
            var n          = 0;

            for (var pre = 0; pre < neurons; pre++)
            {
                var row     = connectome.From(pre);
                var columns = row.Columns;
                var values  = row.Values;

                for (var i = 0; i < row.Count; i++)
                {
                    // a synapse to an index past neuron count would read a stale soma slot on the GPU
                    if (columns[i] >= neurons)
                        continue;

                    if (n == _data.Length)
                        Array.Resize(ref _data, n * 2);

                    _data[n++] = new Synapse { Pre = pre, Post = columns[i], Weight = values[i] };
                }
            }

            return n;
        }

        // struct must match Synapse.shader in field order and types (just like soma renderer)
        [StructLayout(LayoutKind.Sequential)]
        private struct Synapse
        {
            public int   Pre;
            public int   Post;
            public float Weight;
        }
    }
}