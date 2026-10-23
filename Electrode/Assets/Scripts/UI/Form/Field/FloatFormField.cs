using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>A float input clamped to at least <see cref="MinValue" />.</summary>
    public sealed class FloatFormField : FormField<float, FloatField>
    {
        public float MinValue { get; init; } = float.NegativeInfinity;
        public bool  Delayed  { get; init; }

        private protected override float Adjust(float value)
        {
            return Mathf.Max(value, MinValue);
        }

        protected override BaseField<float> CreateElement(string label)
        {
            var e = (FloatField)base.CreateElement(label);
            e.isDelayed = Delayed;
            return e;
        }
    }
}