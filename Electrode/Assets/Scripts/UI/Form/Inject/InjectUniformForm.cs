using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Inject
{
    /// <summary>
    ///     Sets the same constant input current on every selected neuron. 0 switches injection off.
    /// </summary>
    public sealed class InjectUniformForm : ActionForm
    {
        private readonly FloatFormField _current;
        private readonly Sandbox        _sandbox;

        public InjectUniformForm(Sandbox sandbox) : base($"Inject uniformly ({sandbox.Selection.Count} selected)")
        {
            _sandbox = sandbox;
            _current = Add(new FloatFormField("Current", 3.5f));
        }

        public override void Apply()
        {
            foreach (var n in _sandbox.Selection)
                n.InputCurrent = _current.Value;
        }
    }
}