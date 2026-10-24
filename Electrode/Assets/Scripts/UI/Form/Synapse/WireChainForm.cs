using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    /// <summary>
    ///     Wires each selected neuron into the next, in selection order. <see cref="_closed" /> closes the chain.
    /// </summary>
    public sealed class WireChainForm : ActionForm
    {
        private readonly ToggleFormField _closed;
        private readonly FloatFormField  _weight;

        public WireChainForm(Sandbox sandbox) : base(sandbox, "Wire chain")
        {
            // threshold sits 15 mV above rest by default, so 16 fires a resting neuron from a single spike
            _weight = Add(new FloatFormField
            {
                Label = "Weight",
                Value = 16f
            });
            _closed = Add(new ToggleFormField
            {
                Label = "Closed"
            });
        }

        public override void Apply()
        {
            var order  = Selection.Order;
            var weight = _weight.Value;

            for (var i = 1; i < order.Length; i++)
                Connectome[order[i - 1], order[i]] = weight;

            // 2 neurons closed would just be a reciprocal pair, which is still a valid (tiny) ring
            if (_closed.Value && order.Length >= 2)
                Connectome[order[^1], order[0]] = weight;
        }
    }
}