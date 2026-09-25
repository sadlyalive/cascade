using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>An integer input clamped to at least <c>min</c> (default 0).</summary>
    public sealed class IntFormField : FormField<int>
    {
        private readonly int _min;

        public IntFormField(string label, int defaultValue, int min = 0) : base(label, defaultValue)
        {
            _min = min;
        }

        protected override BaseField<int> CreateElement(string label)
        {
            return new IntegerField(label);
        }

        private protected override int Adjust(int value)
        {
            return Mathf.Max(value, _min);
        }
    }
}