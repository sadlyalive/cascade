using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    public class SeverForm : ActionForm
    {
        private readonly ToggleFormField _within;
        private readonly ToggleFormField _inbound;
        private readonly ToggleFormField _outbound;

        public SeverForm(Sandbox sandbox) : base(sandbox, "Sever synapses", true)
        {
            _within = new ToggleFormField
            {
                Label = "Synapses within selection only"
            };
            _inbound = new ToggleFormField
            {
                Label = "Include inbound synapses",
                Value = true
            };
            _outbound = new ToggleFormField
            {
                Label = "Include outbound synapses",
                Value = true
            };
        }

        public override void Apply()
        {
            foreach (var i in Selection.Order)
            {
                if (_inbound.Value)
                {
                    foreach (var (pre, _) in Connectome.To(i))
                    {
                        if (IsRelevant(pre))
                            Connectome[pre, i] = 0f;
                    }
                }

                if (!_outbound.Value)
                    continue;

                foreach (var (post, _) in Connectome.From(i))
                {
                    if (IsRelevant(post))
                        Connectome[i, post] = 0f;
                }
            }

            return;

            bool IsRelevant(int i)
            {
                return !_within.Value || Selection.Contains(i);
            }
        }
    }
}