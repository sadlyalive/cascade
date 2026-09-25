using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     An unsigned integer input; can't go negative by construction. Used for seeds, where 0 means "pick one for me".
    /// </summary>
    public sealed class UIntFormField : FormField<uint>
    {
        public UIntFormField(string label, uint defaultValue = 0) : base(label, defaultValue)
        {
        }

        protected override BaseField<uint> CreateElement(string label)
        {
            return new UnsignedIntegerField(label);
        }
    }
}