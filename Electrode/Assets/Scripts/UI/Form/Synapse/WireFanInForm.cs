using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    /// <summary>Wires every other selected neuron into the first selected one (convergence).</summary>
    public sealed class WireFanInForm : ActionForm
    {
        private readonly Sandbox        _sandbox;
        private readonly FloatFormField _weight;

        public WireFanInForm(Sandbox sandbox) : base($"Wire fan in ({sandbox.Selection.Count} selected)")
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

            foreach (var pre in order[1..])
                connectome[pre, order[0]] = weight;
        }
    }
}