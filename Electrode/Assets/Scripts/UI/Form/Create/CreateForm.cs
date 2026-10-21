using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using UnityEngine;

namespace Cascade.UI.Form.Create
{
    /// <summary>Adds one or more neurons with the specified properties.</summary>
    public sealed class CreateForm : HintActionForm
    {
        private const float MarkerRadius = .16f; // somas are tiny (0.04 radius by default)

        // TODO: provide options for all neural properties
        private readonly IntFormField     _count;
        private readonly Vector3FormField _position;
        private readonly Sandbox          _sandbox;

        public CreateForm(Sandbox sandbox, Hint hint, CameraDriver view) : base("Create neurons", hint, WireMesh.Sphere)
        {
            _sandbox = sandbox;
            _count   = Add(new IntFormField("Count", 1, 1));

            // positions can be negative, unlike the sizes and radii elsewhere, so lift the default clamp
            _position = Add(new Vector3FormField("Position", view.Target, float.NegativeInfinity));
        }

        public override void Apply()
        {
            var network = _sandbox.Network;
            var p       = _position.Value;

            for (var i = 0; i < _count.Value; i++)
                network.AddNeuron().MoveTo(p.x, p.y, p.z);
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _position.Value;
            hint.Size   = Vector3.one * MarkerRadius;
        }
    }
}