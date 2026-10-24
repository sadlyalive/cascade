using JetBrains.Annotations;
using UnityEngine;

namespace Cascade.UI.Wireframe
{
    /// <summary>
    ///     <para>Draws the axis gizmo at the world origin and a wireframe sphere or box for regional placement previews.</para>
    ///     <para>
    ///         Uses 1px <see cref="MeshTopology.Lines" /> and <see cref="Graphics.RenderMesh" /> to show in production
    ///         unlike gizmos.
    ///     </para>
    /// </summary>
    [NoReorder]
    public sealed class Hint : MonoBehaviour
    {
        [SerializeField] private Shader shader;
        [SerializeField] private float  axisLength = 4f;

        private Matrix4x4    _axisMatrix;
        private Material     _material;
        private RenderParams _renderParams;

        // TODO: avoid field reference since we plan to eventually upgrade to property field expression (c# 13)
        private Mesh      _shape;
        private Vector3   _origin;
        private Vector3   _size;
        private Matrix4x4 _shapeMatrix;

        public bool Visible { get; set; }

        public Mesh Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                UpdateMatrix();
            }
        }

        public Vector3 Origin
        {
            get => _origin;
            set
            {
                _origin = value;
                UpdateMatrix();
            }
        }

        public Vector3 Size
        {
            get => _size;
            set
            {
                _size = value;
                UpdateMatrix();
            }
        }

        private void Awake()
        {
            _material   = new Material(shader);
            _axisMatrix = Matrix4x4.Scale(Vector3.one * axisLength);
            _renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 8092f)
            };
        }

        private void LateUpdate()
        {
            if (!Visible)
                return;

            // always draw axes
            Graphics.RenderMesh(_renderParams, WireMesh.Axes, 0, _axisMatrix);

            if (Shape)
                Graphics.RenderMesh(_renderParams, Shape, 0, _shapeMatrix);
        }

        private void OnDestroy()
        {
            Destroy(_material);
        }

        private void UpdateMatrix()
        {
            if (Shape)
                _shapeMatrix = Matrix4x4.TRS(Origin, Quaternion.identity, Size);
        }
    }
}