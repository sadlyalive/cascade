using System;
using System.Collections.Generic;
using Cascade.UI.Form.Field;
using Random = Unity.Mathematics.Random;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     <para>
    ///         The base of every form <see cref="FormDriver" /> can display. A concrete form keeps its inputs as fields
    ///         (created with <see cref="Add{T}" /> in its constructor) and reads them back in <see cref="Apply" />.
    ///     </para>
    ///     <para>
    ///         Each concrete form owns the operation it performs, so a form is the whole story of one menu action: its
    ///         inputs, its preview (see <see cref="HintActionForm" />), and what it does to the network.
    ///     </para>
    /// </summary>
    public abstract class ActionForm
    {
        private readonly List<IFormField> _fields = new();

        protected ActionForm(string title)
        {
            Title = title;
        }

        public string Title { get; }

        public IReadOnlyList<IFormField> Fields => _fields;

        /// <summary>Runs when the user clicks <c>Apply</c> on the form.</summary>
        public virtual void Apply()
        {
        }

        /// <summary>Runs after form builds and becomes visible to the user.</summary>
        public virtual void OnShow()
        {
        }

        /// <summary>Runs after any user edit to a field (post <see cref="FormField{T, TElement}.Adjust" />).</summary>
        public virtual void OnChange()
        {
        }

        /// <summary>Runs when the form hides (apply, click away, etc.).</summary>
        public virtual void OnHide()
        {
        }

        /// <summary>Adds <paramref name="field" /> to the form and returns it, so constructors can keep it as a field.</summary>
        protected T Add<T>(T field) where T : IFormField
        {
            _fields.Add(field);
            return field;
        }

        /// <summary>
        ///     <para>Returns an instance-bound <see cref="Unity.Mathematics.Random" /> unlike <see cref="UnityEngine.Random" />.</para>
        ///     <para>
        ///         The former uses a xor-shift algorithm which is incompatible with seed <c>0</c> (default value), in which
        ///         case the method will hash a new <see cref="Guid" /> instead.
        ///     </para>
        /// </summary>
        protected static Random NewRandom(uint seed = 0)
        {
            return new Random(seed > 0 ? seed : Math.Max(1, (uint)Guid.NewGuid().GetHashCode()));
        }
    }
}