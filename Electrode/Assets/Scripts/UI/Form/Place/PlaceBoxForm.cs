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
        private readonly Sandbox          _sandbox;
        private readonly UIntFormField    _seed;
        private readonly Vector3FormField _size;

        public PlaceBoxForm(Sandbox sandbox, Hint hint) : base(
            $"Place randomly in box ({sandbox.Selection.Count} selected)",
            hint,
            WireMesh.Cube
        )
        {
            _sandbox = sandbox;
            _origin  = Add(new Vector3FormField("Origin", Vector3.zero, float.NegativeInfinity));
            _size    = Add(new Vector3FormField("Size",   Vector3.one * 8f));
            _seed    = Add(new UIntFormField("Seed"));
        }

        public override void Apply()
        {
            var rng    = NewRandom(_seed.Value);
            var origin = (float3)_origin.Value;
            var half   = (float3)_size.Value / 2f;

            foreach (var n in _sandbox.Selection)
            {
                var p = origin + rng.NextFloat3(-half, half);
                n.MoveTo(p.x, p.y, p.z);
            }
        }

        protected override void Preview(Hint hint)
        {
            hint.Origin = _origin.Value;
            hint.Size   = _size.Value;
        }
    }
}