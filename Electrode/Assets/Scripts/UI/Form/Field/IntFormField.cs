using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>An integer input clamped to at least <c>min</c> (default 0).</summary>
    public sealed class IntFormField : FormField<int, IntegerField>
    {
        public int MinValue { get; init; } = 0;

        private protected override int Adjust(int value)
        {
            return Mathf.Max(value, MinValue);
        }
    }
}