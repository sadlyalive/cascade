using Cascade.Select;
using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Inject
{
    /// <summary>
    ///     Sets the same constant input current on every selected neuron. 0 switches injection off.
    /// </summary>
    public sealed class InjectUniformForm : ActionForm
    {
        private readonly FloatFormField _current;
        private readonly Selection      _selection;

        public InjectUniformForm(Selection selection) : base($"Inject uniformly ({selection.Count} selected)")
        {
            _selection = selection;
            _current = Add(new FloatFormField
            {
                Label = "Current",
                Value = 3.5f
            });
        }

        public override void Apply()
        {
            foreach (var n in _selection)
                n.InputCurrent = _current.Value;
        }
    }
}