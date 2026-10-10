using System.Runtime.InteropServices;
using UnityEngine;

namespace Cascade
{
    /// <summary>
    ///     <para>
    ///         Draws every neuron in a <see cref="Sandbox" />'s pool as a GPU-instanced sphere via
    ///         <see cref="Graphics.RenderMeshPrimitives" />. The CPU fills the buffer, then the GPU positions and colors every
    ///         instance.
    ///     </para>
    /// </summary>
    [RequireComponent(typeof(Sandbox))]
    public sealed class NeuronRenderer : MonoBehaviour
    {
        private const string ShaderPath = "Custom/Soma";

        private static readonly int NeuronBufferId = Shader.PropertyToID("_NeuronBuffer");
        private static readonly int NeuronScaleId  = Shader.PropertyToID("_NeuronScale");

        [SerializeField] private float neuronScale = 0.08f;

        private GraphicsBuffer   _instanceBuffer;
        private NeuronInstance[] _instanceData;
        private Material         _material;
        private Mesh             _mesh;

        private Sandbox _sandbox;

        private void Awake()
        {
            _sandbox = GetComponent<Sandbox>();

            // grab default sphere mesh
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            _mesh = temp.GetComponent<MeshFilter>().sharedMesh;

            Destroy(temp);

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
            if (_instanceBuffer is null || _instanceBuffer.count < count)
            {
                _instanceBuffer?.Dispose();

                _instanceData = new NeuronInstance[neurons.Capacity];
                _instanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    neurons.Capacity,
                    Marshal.SizeOf<NeuronInstance>()
                );
            }

            var x     = neurons.X;
            var y     = neurons.Y;
            var z     = neurons.Z;
            var spike = neurons.Spike;

            // populate CPU-side array from the SoA spans and ship only the live part to the shader
            // TODO: turn _instanceData into SoA (?)
            for (var i = 0; i < count; i++)
            {
                _instanceData[i].Position = new Vector3(x[i], y[i], z[i]);
                _instanceData[i].Spike    = spike[i] ? 1f : 0f;
            }

            _instanceBuffer.SetData(_instanceData, 0, 0, count);

            var materialProperties = new MaterialPropertyBlock();

            materialProperties.SetBuffer(NeuronBufferId, _instanceBuffer);
            materialProperties.SetFloat(NeuronScaleId, neuronScale);

            var renderParams = new RenderParams(_material)
            {
                // cull the whole draw only if this is off-screen entirely
                worldBounds = new Bounds(transform.position, Vector3.one * 10000f), matProps = materialProperties
            };

            Graphics.RenderMeshPrimitives(renderParams, _mesh, 0, count);
        }

        private void OnDisable()
        {
            _instanceBuffer?.Dispose();
            _instanceBuffer = null;
        }

        // struct must match NeuronInstanced.shader in field order and types
        [StructLayout(LayoutKind.Sequential)]
        private struct NeuronInstance
        {
            public Vector3 Position;
            public float   Spike; // shader buffers do not support bool
        }
    }
}