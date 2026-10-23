using Cascade.Render;
using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form.Soma
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
            _count = Add(new IntFormField
            {
                Label    = "Count",
                Value    = 1,
                MinValue = 1
            });
            _position = Add(new Vector3FormField
            {
                Label = "Position",
                Value = view.Target
            });
            _sandbox = sandbox;
        }

        public override void Apply()
        {
            var pos   = (float3)_position.Value;
            var count = _count.Value;

            for (var i = 0; i < count; i++)
                _sandbox.Network.AddNeuron().MoveTo(pos);
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _position.Value;
            hint.Size   = Vector3.one * MarkerRadius;
        }
    }
}