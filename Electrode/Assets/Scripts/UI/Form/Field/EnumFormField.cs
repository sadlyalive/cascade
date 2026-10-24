using System;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    public sealed class EnumFormField<T> : FormField<Enum, EnumField> where T : struct, Enum
    {
        public T Choice => (T)Value;

        protected override BaseField<Enum> CreateElement(string label)
        {
            return new EnumField(label, default(T));
        }
    }
}