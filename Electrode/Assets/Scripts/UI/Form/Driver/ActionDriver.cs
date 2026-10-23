using System;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Driver
{
    public sealed class ActionDriver : FormDriver
    {
        private IDisposable _busy;

        protected override string ShellName => "form";

        public void Reveal(ActionForm form)
        {
            // let old token persist when replacing a form with another so the UI doesn't unlock for a frame in between
            _busy ??= UI.Busy();
            Show(form);
        }

        protected override void OnShellFound()
        {
            // acts on whichever form is currently shown since the ui structure is reused
            Shell.Q<Button>("apply").clicked += Apply;
        }

        private void Apply()
        {
            CurrentForm?.Apply();
            Hide();
        }

        protected override void OnHidden()
        {
            _busy?.Dispose();
            _busy = null;
        }
    }
}