using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     A three-component input (one <see cref="Vector3Field" />) whose components are each clamped to at least
    ///     <c>min</c> (default 0). Reads back as a single <see cref="Vector3" />.
    /// </summary>
    public sealed class Vector3FormField : FormField<Vector3>
    {
        private readonly float _min;

        public Vector3FormField(string label, Vector3 defaultValue, float min = 0f) : base(label, defaultValue)
        {
            _min = min;
        }

        protected override BaseField<Vector3> CreateElement(string label)
        {
            return new Vector3Field(label);
        }

        private protected override Vector3 Adjust(Vector3 value)
        {
            return new Vector3(Mathf.Max(value.x, _min), Mathf.Max(value.y, _min), Mathf.Max(value.z, _min));
        }
    }
}