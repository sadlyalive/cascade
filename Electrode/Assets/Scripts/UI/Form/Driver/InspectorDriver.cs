using Cascade.Select;
using UnityEngine;

namespace Cascade.UI.Form.Driver
{
    public sealed class InspectorDriver : FormDriver
    {
        [SerializeField] private Sandbox sandbox;

        private Selection _selection;

        protected override string ShellName => "inspector";

        protected override void OnEnabled()
        {
            _selection = sandbox.Selection;

            _selection.Changed += Reveal;
            UI.BusyChanged     += HideIfBusy;

            HideIfBusy(UI.IsBusy);
        }

        protected override void OnDisabled()
        {
            _selection.Changed -= Reveal;
            UI.BusyChanged     -= HideIfBusy;
        }

        private void HideIfBusy(bool busy)
        {
            if (busy)
                Hide();
        }

        private void Reveal()
        {
            if (_selection.Count > 0)
                Show(new ConfigureForm(_selection));
            else
                Hide();
        }
    }
}