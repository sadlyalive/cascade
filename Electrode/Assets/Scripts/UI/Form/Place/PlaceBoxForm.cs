using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form.Place
{
    /// <summary>
    ///     Scatters the selection to random positions uniformly distributed inside an axis-aligned box centered on its
    ///     origin.
    /// </summary>
    public sealed class PlaceBoxForm : HintActionForm
    {
        private readonly Vector3FormField _origin;
        private readonly UIntFormField    _seed;
        private readonly Vector3FormField _size;

        public PlaceBoxForm(Sandbox sandbox, Hint hint) : base(sandbox,
                                                               "Place randomly in box",
                                                               true,
                                                               hint,
                                                               WireMesh.Cube)
        {
            _origin = Add(new Vector3FormField
            {
                Label = "Origin",
                Value = Vector3.zero
            });
            _size = Add(new Vector3FormField
            {
                Label    = "Size",
                Value    = Vector3.one * 8f,
                MinValue = 0f
            });
            _seed = Add(new UIntFormField
            {
                Label = "Seed"
            });
        }

        public override void Apply()
        {
            var rng    = NewRandom(_seed.Value);
            var origin = (float3)_origin.Value;
            var half   = (float3)_size.Value / 2f;

            foreach (var n in Selection)
                n.MoveTo(origin + rng.NextFloat3(-half, half));
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _origin.Value;
            hint.Size   = _size.Value;
        }
    }
}