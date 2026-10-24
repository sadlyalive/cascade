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

        public CreateForm(Sandbox sandbox, Hint hint, CameraDriver view) : base(sandbox,
            "Create neurons",
            hint,
            WireMesh.Sphere)
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
        }

        public override void Apply()
        {
            var pos   = (float3)_position.Value;
            var count = _count.Value;

            var @new = new int[count];

            for (var i = 0; i < count; i++)
            {
                var n = Network.AddNeuron();

                @new[i] = n;
                n.MoveTo(pos);
            }

            Selection.Set(@new);
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _position.Value;
            hint.Size   = Vector3.one * MarkerRadius;
        }
    }
}