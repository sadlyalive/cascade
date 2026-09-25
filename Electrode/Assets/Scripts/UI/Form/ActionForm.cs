using System;
using System.Collections.Generic;
using Cascade.UI.Form.Field;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     Describes the generic contract for all forms which <see cref="FormDriver" /> uses to turn into UI elements.
    ///     Create it with a title, <see cref="Add{T}" /> its fields (keeping each returned field to read later), then set
    ///     <see cref="OnApply" />. The order is forced by that last step reading the fields, which is why it can't be a
    ///     constructor argument.
    /// </summary>
    public class ActionForm
    {
        private readonly List<IFormField> _fields = new();

        public ActionForm(string title)
        {
            Title = title;
        }

        public string Title { get; }

        /// <summary>What Apply does. Required: <see cref="Validate" /> rejects a form without it.</summary>
        public Action OnApply { get; set; }

        public IReadOnlyList<IFormField> Fields => _fields;

        /// <summary>Adds <paramref name="field" /> to the form and returns it, still its concrete type, to read from.</summary>
        public T Add<T>(T field) where T : IFormField
        {
            _fields.Add(field);
            return field;
        }

        /// <summary>Throws if a required late-set member was never set. <see cref="FormDriver" /> calls this before showing.</summary>
        public virtual void Validate()
        {
            if (OnApply == null)
                throw new InvalidOperationException($"Form \"{Title}\" has no {nameof(OnApply)}.");
        }

        /// <summary>Runs after form built and visible.</summary>
        public virtual void OnShow()
        {
        }

        /// <summary>Runs after any user edit to a field (post <see cref="FormField{T}.Adjust" />).</summary>
        public virtual void OnChange()
        {
        }

        /// <summary>Runs when the form hides (apply, click away, etc.).</summary>
        public virtual void OnHide()
        {
        }
    }
}