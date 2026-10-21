using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     An unsigned integer input. Mostly for custom pseudorandom generator seeds.
    /// </summary>
    public sealed class UIntFormField : FormField<uint, UnsignedIntegerField>
    {
        public UIntFormField(string label, uint defaultValue = 0) : base(label, defaultValue)
        {
        }
    }
}