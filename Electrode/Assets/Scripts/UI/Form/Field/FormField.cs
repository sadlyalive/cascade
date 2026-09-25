using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    public abstract class FormField<T> : IFormField
    {
        private readonly T      _default;
        private readonly string _label;

        private BaseField<T> _element;

        protected FormField(string label, T defaultValue)
        {
            _label   = label;
            _default = defaultValue;
        }

        /// <summary>
        ///     <para>
        ///         The current value. Listeners (via <c>update()</c>) execute after Unity handles callback, so
        ///         <see cref="Adjust" /> always applies correctly.
        ///     </para>
        ///     <para>Returns the preset default if the form field isn't in use.</para>
        /// </summary>
        public T Value => _element != null ? _element.value : _default;

        public VisualElement Create()
        {
            _element = CreateElement(_label);
            _element.SetValueWithoutNotify(_default);
            _element.RegisterValueChangedCallback(OnValueChanged);

            return _element;
        }

        public event Action Changed;

        protected abstract BaseField<T> CreateElement(string label);

        /// <summary>Adjusts the final value after a user edit (e.g. a clamp). Identity by default.</summary>
        private protected virtual T Adjust(T value)
        {
            return value;
        }

        private void OnValueChanged(ChangeEvent<T> args)
        {
            var final = Adjust(args.newValue);

            // apply adjustment only if it actually elicited a change
            if (!EqualityComparer<T>.Default.Equals(final, args.newValue))
                _element.SetValueWithoutNotify(final);

            Changed?.Invoke();
        }
    }
}