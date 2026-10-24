using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    /// <summary>Wires every other selected neuron into the first selected one (convergence).</summary>
    public sealed class WireFanInForm : ActionForm
    {
        private readonly FloatFormField _weight;

        public WireFanInForm(Sandbox sandbox) : base(sandbox, "Wire fan in")
        {
            _weight = Add(new FloatFormField
            {
                Label = "Weight",
                Value = 16f
            });
        }

        public override void Apply()
        {
            // fewer than 2 means no pairs, and order[0] would throw on an empty selection
            if (Selection.Count < 2)
                return;

            var order  = Selection.Order;
            var weight = _weight.Value;

            foreach (var pre in order[1..])
                Connectome[pre, order[0]] = weight;
        }
    }
}