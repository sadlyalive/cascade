namespace Cascade.UI.Form.Workspace
{
    public class ResetForm : ActionForm
    {
        public ResetForm(Sandbox sandbox) : base(sandbox, "Reset whole network")
        {
        }

        public override void Apply()
        {
            Network.Clear();
        }
    }
}