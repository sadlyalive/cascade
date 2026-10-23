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
        private T _value;

        private BaseField<T> _element;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                _element?.SetValueWithoutNotify(value);
            }
        }

        public string Label { get; init; }

        /// <summary>Indicates to the user whether the field's value will write to more than one place.</summary>
        public bool Mixed { get; init; }

        public bool Edited { get; private set; }


        public VisualElement Create()
        {
            _element = CreateElement(Label);
            _element.SetValueWithoutNotify(Value);
            _element.RegisterValueChangedCallback(OnValueChanged);
            _element.showMixedValue = Mixed;

            return _element;
        }

        public event Action Changed;

        /// <summary>Builds the Unity element. Virtual for more setup (choices, a range, etc.).</summary>
        protected virtual BaseField<T> CreateElement(string label)
        {
            return new TElement
            {
                label = label
            };
        }

        /// <summary>Adjusts the final value after a user edit (e.g. a clamp). Identity by default.</summary>
        private protected virtual T Adjust(T value)
        {
            return value;
        }

        private void OnValueChanged(ChangeEvent<T> args)
        {
            var final = Adjust(args.newValue);

            // write the adjustment back into the element only if it changed anything (e.g. a clamp kicked in)
            if (!EqualityComparer<T>.Default.Equals(final, args.newValue))
                _element.SetValueWithoutNotify(final);

            _value = final; // update field with user edits
            Edited = true;

            _element.showMixedValue = false;
            Changed?.Invoke();
        }
    }
}