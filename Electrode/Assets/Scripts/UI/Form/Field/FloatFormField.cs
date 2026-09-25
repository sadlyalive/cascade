using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     A float input clamped to at least <c>min</c>. Defaults to 0 because most parameters here are non-negative; pass
    ///     <see cref="float.NegativeInfinity" /> for one that can be (e.g. an inhibitory weight).
    /// </summary>
    public sealed class FloatFormField : FormField<float>
    {
        private readonly float _min;

        public FloatFormField(string label, float defaultValue, float min = 0f) : base(label, defaultValue)
        {
            _min = min;
        }

        protected override BaseField<float> CreateElement(string label)
        {
            return new FloatField(label);
        }

        private protected override float Adjust(float value)
        {
            return Mathf.Max(value, _min);
        }
    }
}