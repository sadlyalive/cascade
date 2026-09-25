using System;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     Allows collection of <see cref="FormField{T}" />s despite different generic types.
    /// </summary>
    public interface IFormField
    {
        event Action Changed;

        /// <summary>
        ///     Creates the UI field and connects the form field's behavior.
        /// </summary>
        /// <returns>The resultant UI</returns>
        VisualElement Create();
    }
}