using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Wire
{
    /// <summary>
    ///     Wires each selected neuron into the next, in selection order. <see cref="_closed" /> closes the chain.
    /// </summary>
    public sealed class WireChainForm : ActionForm
    {
        private readonly ToggleFormField _closed;
        private readonly Sandbox         _sandbox;
        private readonly FloatFormField  _weight;

        public WireChainForm(Sandbox sandbox) : base($"Wire chain ({sandbox.Selection.Count} selected)")
        {
            _sandbox = sandbox;

            // threshold sits 15 mV above rest by default, so 16 fires a resting neuron from a single spike
            _weight = Add(new FloatFormField("Weight", 16f));
            _closed = Add(new ToggleFormField("Closed"));
        }

        public override void Apply()
        {
            var connectome = _sandbox.Network.Connectome;
            var order      = _sandbox.Selection.Order;
            var weight     = _weight.Value;

            for (var i = 1; i < order.Length; i++)
                connectome[order[i - 1], order[i]] = weight;

            // 2 neurons closed would just be a reciprocal pair, which is still a valid (tiny) ring
            if (_closed.Value && order.Length >= 2)
                connectome[order[^1], order[0]] = weight;
        }
    }
}