using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Field
{
    /// <summary>
    ///     A form input field that forms can query with <see cref="Value" />.
    /// </summary>
    /// <typeparam name="T">The input field's value type</typeparam>
    /// <typeparam name="TElement">The type of UXML field to instantiate when built</typeparam>
    public abstract class FormField<T, TElement> : IFormField where TElement : BaseField<T>, new()
    {
        private readonly T      _default;
        private readonly string _label;

        private BaseField<T> _element;

        protected FormField(string label, T defaultValue)
        {
            _label   = label;
            _default = defaultValue;
        }

        public T Value => _element != null ? _element.value : _default;

        public VisualElement Create()
        {
            _element = CreateElement(_label);
            _element.SetValueWithoutNotify(_default);
            _element.RegisterValueChangedCallback(OnValueChanged);

            return _element;
        }

        public event Action Changed;

        /// <summary>Builds the Unity element. Virtual for more setup (choices, a range, etc.).</summary>
        protected virtual BaseField<T> CreateElement(string label)
        {
            return new TElement { label = label };
        }

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
                return;

            _element.SetValueWithoutNotify(final);
            Changed?.Invoke();
        }
    }
}