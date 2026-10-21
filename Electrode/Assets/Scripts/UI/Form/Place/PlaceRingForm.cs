using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form.Place
{
    /// <summary>Spaces the selection evenly in a horizontal circle.</summary>
    public sealed class PlaceRingForm : HintActionForm
    {
        private readonly Vector3FormField _origin;
        private readonly FloatFormField   _radius;
        private readonly Sandbox          _sandbox;

        public PlaceRingForm(Sandbox sandbox, Hint hint) : base(
            $"Place in ring ({sandbox.Selection.Count} selected)",
            hint,
            WireMesh.Sphere
        )
        {
            _sandbox = sandbox;
            _origin  = Add(new Vector3FormField("Origin", Vector3.zero, float.NegativeInfinity));
            _radius  = Add(new FloatFormField("Radius", 4f));
        }

        public override void Apply()
        {
            var selection = _sandbox.Selection;
            var origin    = (float3)_origin.Value;
            var radius    = _radius.Value;
            var slice     = 2f * math.PI / selection.Count;

            for (var i = 0; i < selection.Count; i++)
            {
                var pos = origin + radius * new float3(math.cos(i * slice), 0f, math.sin(i * slice));
                selection[i].MoveTo(pos.x, pos.y, pos.z);
            }
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _origin.Value;
            hint.Size   = new Vector3(_radius.Value, 0f, _radius.Value); // squash the wire sphere
        }
    }
}