using Cascade.UI.Wireframe;
using UnityEngine;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     A form that previews its inputs as a wireframe: it shows <c>shape</c> while open, lets <see cref="Preview" />
    ///     place and size it from the current field values, and takes it away again when the form goes.
    /// </summary>
    public abstract class HintActionForm : ActionForm
    {
        private readonly Hint _hint;
        private readonly Mesh _shape;

        protected HintActionForm(Sandbox sandbox, string title, bool appendSelectionCount, Hint hint, Mesh shape) :
            base(sandbox, title, appendSelectionCount)
        {
            _hint  = hint;
            _shape = shape;
        }

        public override void OnShow()
        {
            Preview(_hint); // place the hint from the form's defaults before it appears
            _hint.Shape = _shape;
        }

        public override void OnChange()
        {
            Preview(_hint);
        }

        public override void OnHide()
        {
            _hint.Shape = null;
        }

        /// <summary>Positions and sizes <paramref name="hint" /> from the current field values.</summary>
        protected abstract void Preview(Hint hint);
    }
}