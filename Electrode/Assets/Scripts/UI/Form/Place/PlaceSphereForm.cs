using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form.Place
{
    // TODO: Hammersley sequence for less clumping (?)
    /// <summary>Scatters the selection to uniformly random positions inside a sphere.</summary>
    public sealed class PlaceSphereForm : HintActionForm
    {
        private readonly Vector3FormField _origin;
        private readonly FloatFormField   _radius;
        private readonly UIntFormField    _seed;

        public PlaceSphereForm(Sandbox sandbox, Hint hint) : base(sandbox,
                                                                  "Place randomly in sphere",
                                                                  true,
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
                Value    = 8f,
                MinValue = 0f
            });
            _seed = Add(new UIntFormField
            {
                Label = "Seed"
            });
        }

        public override void Apply()
        {
            const float third = 1f / 3f;

            var rng    = NewRandom(_seed.Value);
            var origin = (float3)_origin.Value;
            var radius = _radius.Value;

            foreach (var n in Selection)
            {
                // cube-rooting the random scalar compensates for r^3 volume growth
                var p = origin + math.pow(rng.NextFloat(), third) * radius * rng.NextFloat3Direction();
                n.MoveTo(p.x, p.y, p.z);
            }
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _origin.Value;
            hint.Size   = Vector3.one * _radius.Value;
        }
    }
}