using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Inject
{
    /// <summary>
    ///     Sets the same constant input current on every selected neuron. 0 switches injection off.
    /// </summary>
    public sealed class InjectUniformForm : ActionForm
    {
        private readonly FloatFormField _current;

        public InjectUniformForm(Sandbox sandbox) : base(sandbox, "Inject uniformly")
        {
            _current = Add(new FloatFormField
            {
                Label = "Current",
                Value = 3.5f
            });
        }

        public override void Apply()
        {
            foreach (var n in Selection)
                n.InputCurrent = _current.Value;
        }
    }
}