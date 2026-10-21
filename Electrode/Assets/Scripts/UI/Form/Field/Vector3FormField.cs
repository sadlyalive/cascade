using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>A <see cref="Vector3" /> whose components each clamp to at least <c>min</c> (default 0).</summary>
    public sealed class Vector3FormField : FormField<Vector3, Vector3Field>
    {
        private readonly float _min;

        public Vector3FormField(string label, Vector3 defaultValue, float min = 0f) : base(label, defaultValue)
        {
            _min = min;
        }

        private protected override Vector3 Adjust(Vector3 value)
        {
            return new Vector3(Mathf.Max(value.x, _min), Mathf.Max(value.y, _min), Mathf.Max(value.z, _min));
        }
    }
}