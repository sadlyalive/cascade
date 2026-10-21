using System;
using Cascade.UI.Form.Field;
using Cascade.UI.Wireframe;
using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Form.Place
{
    /// <summary>
    ///     Lays the selection out on a cubic grid centered on its origin, in selection order (x first, then y, then z),
    ///     with a spacing between neighboring centers. Previews the box the grid will fill.
    /// </summary>
    public sealed class PlaceGridForm : HintActionForm
    {
        private readonly Vector3FormField _origin;
        private readonly Sandbox          _sandbox;
        private readonly Vector3FormField _spacing;

        public PlaceGridForm(Sandbox sandbox, Hint hint) : base(
            $"Place in grid ({sandbox.Selection.Count} selected)",
            hint,
            WireMesh.Cube
        )
        {
            _sandbox = sandbox;
            _origin  = Add(new Vector3FormField("Origin",  Vector3.zero, float.NegativeInfinity));
            _spacing = Add(new Vector3FormField("Spacing", Vector3.one));
        }

        public override void Apply()
        {
            var selection = _sandbox.Selection;
            var count     = selection.Count;
            var filled    = Filled(count, out var side);
            var spacing   = (float3)_spacing.Value;

            // center what's actually filled, not the whole cube: 28 neurons on a 4-cube fill only 2 of its 4 layers, so
            // a centered cube would leave them visibly off to one side. (n - 1) because n cells span n - 1 gaps
            var corner = (float3)_origin.Value - (filled - 1) * 0.5f * spacing;

            for (var i = 0; i < count; i++)
            {
                // integer division and modulo turn the index into digits in base `side`
                var cell = new float3(i % side, i / side % side, i / (side * side));
                var pos  = corner + cell * spacing;

                selection[i].MoveTo(pos.x, pos.y, pos.z);
            }
        }

        protected override void Preview(Hint hint)
        {
            var filled = Filled(_sandbox.Selection.Count, out _);

            hint.Origin = _origin.Value;
            hint.Size   = math.max(filled - 1, 0) * _spacing.Value;
        }

        /// <summary>
        ///     Cells the grid fills along each axis for <paramref name="count" /> neurons, and the cube's
        ///     <paramref name="side" />. Each axis uses the full side once the axes before it are full, otherwise only as
        ///     many cells as needed.
        /// </summary>
        private static float3 Filled(int count, out int side)
        {
            // smallest side whose cube fits everything. counted up in integers because Pow(27, 1/3) comes out as
            // 3.0000002 in floats, which Ceil turns into 4
            side = 1;

            while (side * side * side < count)
                side++;

            return new float3(
                Math.Min(count,                     side),
                Math.Min((count + side - 1) / side, side),
                (count + side * side - 1) / (side * side)
            );
        }
    }
}