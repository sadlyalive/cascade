using System;

namespace Cascade.UI.Form.Soma
{
    public sealed class DeleteForm : ActionForm
    {
        public DeleteForm(Sandbox sandbox) : base(sandbox, "Delete somas")
        {
        }

        public override void Apply()
        {
            var order = Selection.Order.ToArray();

            Array.Sort(order);

            for (var i = order.Length - 1; i >= 0; i--)
                Network.RemoveNeuron(order[i]);
        }
    }
}