using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    /// <summary>Wires the first selected neuron into every other selected one (divergence).</summary>
    public sealed class WireFanOutForm : ActionForm
    {
        private readonly Sandbox        _sandbox;
        private readonly FloatFormField _weight;

        public WireFanOutForm(Sandbox sandbox) : base($"Wire fan out ({sandbox.Selection.Count} selected)")
        {
            _sandbox = sandbox;
            _weight = Add(new FloatFormField
            {
                Label = "Weight",
                Value = 16f
            });
        }

        public override void Apply()
        {
            var order = _sandbox.Selection.Order;

            // fewer than 2 means no pairs, and order[0] would throw on an empty selection
            if (order.Length < 2)
                return;

            var connectome = _sandbox.Network.Connectome;
            var weight     = _weight.Value;

            foreach (var post in order[1..])
                connectome[order[0], post] = weight;
        }
    }
}