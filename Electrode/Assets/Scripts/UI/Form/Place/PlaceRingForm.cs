using Cascade.Select;
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
        private readonly Selection        _selection;

        public PlaceRingForm(Selection selection, Hint hint) : base($"Place in ring ({selection.Count} selected)",
            hint,
            WireMesh.Sphere)
        {
            _origin = Add(new Vector3FormField
            {
                Label = "Origin",
                Value = Vector3.zero
            });
            _radius = Add(new FloatFormField
            {
                Label    = "Radius",
                Value    = 4f,
                MinValue = 0f
            });

            _selection = selection;
        }

        public override void Apply()
        {
            var origin = (float3)_origin.Value;
            var radius = _radius.Value;
            var slice  = 2f * math.PI / _selection.Count;

            for (var i = 0; i < _selection.Count; i++)
                _selection[i].MoveTo(origin + radius * new float3(math.cos(i * slice), 0f, math.sin(i * slice)));
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _origin.Value;
            hint.Size   = new Vector3(_radius.Value, 0f, _radius.Value); // squash the wire sphere
        }
    }
}