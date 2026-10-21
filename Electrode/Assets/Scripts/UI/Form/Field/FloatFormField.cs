using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>A float input clamped to at least <c>min</c>.</summary>
    public sealed class FloatFormField : FormField<float, FloatField>
    {
        private readonly float _min;

        public FloatFormField(string label, float defaultValue, float min = 0f) : base(label, defaultValue)
        {
            _min = min;
        }

        private protected override float Adjust(float value)
        {
            return Mathf.Max(value, _min);
        }
    }
}