using Cascade.UI.Form.Field;

namespace Cascade.UI.Form.Synapse
{
    public enum SeverMode
    {
        All,
        Selection,
        Inbound,
        Outbound
    }

    public class SeverForm : ActionForm
    {
        private readonly EnumFormField<SeverMode> _mode;

        public SeverForm(Sandbox sandbox) : base(sandbox, "Sever synapses")
        {
            _mode = Add(new EnumFormField<SeverMode>
            {
                Label = "Mode",
                Value = SeverMode.All
            });
        }

        public override void Apply()
        {
            var scope = _mode.Choice;

            foreach (var i in Selection.Order)
            {
                // skip the first pass for selection-only severs since a second pass would be redundant
                // (all internal synapses are both inbound and outbound)

                // inbound / all pass
                if (scope is SeverMode.Inbound or SeverMode.All)
                {
                    foreach (var (pre, _) in Connectome.To(i))
                        Connectome[pre, i] = 0f;
                }

                if (scope is SeverMode.Inbound)
                    continue;

                // outbound / selection-only pass
                foreach (var (post, _) in Connectome.From(i))
                {
                    if (scope is not SeverMode.Selection || Selection.Contains(post))
                        Connectome[i, post] = 0f;
                }
            }
        }
    }
}