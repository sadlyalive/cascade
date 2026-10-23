using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>A <see cref="Vector3" /> whose components each clamp to at least <c>min</c> (default 0).</summary>
    public sealed class Vector3FormField : FormField<Vector3, Vector3Field>
    {
        public float MinValue { get; init; } = float.NegativeInfinity;

        private protected override Vector3 Adjust(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(value.x, MinValue),
                Mathf.Max(value.y, MinValue),
                Mathf.Max(value.z, MinValue)
            );
        }
    }
}