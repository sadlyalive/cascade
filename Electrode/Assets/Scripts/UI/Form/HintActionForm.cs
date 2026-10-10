using System;
using Cascade.UI.Wireframe;
using UnityEngine;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     A form that previews its inputs as a wireframe: it shows <c>shape</c> while open, lets <see cref="OnUpdate" />
    ///     place and size it from the current field values, and takes it away again when the form goes.
    /// </summary>
    public sealed class HintActionForm : ActionForm
    {
        private readonly Hint _hint;
        private readonly Mesh _shape;

        public HintActionForm(string title, Hint hint, Mesh shape) : base(title)
        {
            _hint  = hint;
            _shape = shape;
        }

        /// <summary>Positions the hint from the current field values. Required: <see cref="Validate" /> rejects null.</summary>
        public Action<Hint> OnUpdate { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (OnUpdate == null)
                throw new InvalidOperationException($"Form \"{Title}\" has no {nameof(OnUpdate)}.");
        }

        public override void OnShow()
        {
            OnUpdate(_hint); // update hint with form defaults
            _hint.Shape = _shape;
        }

        public override void OnChange()
        {
            OnUpdate(_hint);
        }

        public override void OnHide()
        {
            _hint.Shape = null;
        }
    }
}