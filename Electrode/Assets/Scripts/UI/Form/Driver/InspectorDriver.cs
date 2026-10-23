using Cascade.Select;
using UnityEngine;

namespace Cascade.UI.Form.Driver
{
    public sealed class InspectorDriver : FormDriver
    {
        [SerializeField] private Sandbox sandbox;

        private Selection Selection => sandbox.Selection;

        protected override string ShellName => "inspector";

        protected override void OnEnabled()
        {
            Selection.Changed += Reveal;
            UI.BusyChanged    += HideIfBusy;

            HideIfBusy(UI.IsBusy);
        }

        protected override void OnDisabled()
        {
            Selection.Changed -= Reveal;
            UI.BusyChanged    -= HideIfBusy;
        }

        private void HideIfBusy(bool busy)
        {
            if (busy)
                Hide();
        }

        private void Reveal()
        {
            if (Selection.Count > 0)
                Show(new ConfigureForm(sandbox));
            else
                Hide();
        }
    }
}