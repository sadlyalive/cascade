using System;

namespace Cascade.UI.Form.Soma
{
    public sealed class DeleteForm : ActionForm
    {
        private readonly Sandbox _sandbox;

        public DeleteForm(Sandbox sandbox) : base($"Delete somas ({sandbox.Selection.Count} selected)")
        {
            _sandbox = sandbox;
        }

        public override void Apply()
        {
            var order = _sandbox.Selection.Order.ToArray();

            Array.Sort(order);

            for (var i = order.Length - 1; i >= 0; i--)
                _sandbox.Network.RemoveNeuron(order[i]);
        }
    }
}