using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>A checkbox input.</summary>
    public sealed class ToggleFormField : FormField<bool, Toggle>
    {
        public ToggleFormField(string label, bool defaultValue = false) : base(label, defaultValue)
        {
        }
    }
}