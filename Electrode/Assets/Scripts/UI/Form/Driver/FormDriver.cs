using JetBrains.Annotations;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cascade.UI.Form.Driver
{
    /// <summary>
    ///     <para>
    ///         Fills the form shell in MainUI.uxml with an <see cref="ActionForm" />, and dismisses it on <c>esc</c> or an
    ///         external click.
    ///     </para>
    ///     <para>
    ///         One shell serves every form. Universal appearance lives in Form.uss. Reassigns fields and resubscribes to
    ///         events whenever <see cref="UIDriver" /> reloads the UI (which includes the form shell).
    ///     </para>
    /// </summary>
    [NoReorder]
    public abstract class FormDriver : UIListener
    {
        private protected ActionForm    CurrentForm;
        private protected VisualElement Shell;

        private ScrollView _fields;
        private Label      _title;
        private bool       _visible;

        protected abstract string ShellName { get; }

        private void Update()
        {
            if (!_visible)
                return;

            var kb    = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            // a left click anywhere outside the form cancels it
            // other inputs (e.g., middle mouse for camera orbit) still work
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (!IsInFormUI(UI.CursorPick))
                Hide();

            return;

            bool IsInFormUI(VisualElement element)
            {
                // unity places dropdowns in a sibling of the UI root
                return element == Shell || Shell.Contains(element) || UIDriver.IsInDropdown(element);
            }
        }

        protected sealed override void OnUIReload(VisualElement root)
        {
            var shell = root.Q(ShellName);

            if (shell != Shell)
            {
                Shell   = shell;
                _title  = Shell.Q<Label>("title");
                _fields = Shell.Q<ScrollView>("fields");

                OnShellFound();
            }

            Hide();
        }

        protected virtual void OnShellFound()
        {
        }

        protected override void OnDisabled()
        {
            Hide();
        }

        /// <summary>Replaces the shell's contents with <paramref name="form" />'s inputs and reveals it.</summary>
        protected void Show(ActionForm form)
        {
            _visible = true;

            Detach(); // a form being replaced never goes through Hide() which would flicker

            _title.text = form.Title;
            _fields.Clear();

            foreach (var field in form.Fields)
            {
                var ui = field.Create();

                // marks top-level rows so Form.uss can target them without also matching the fields nested inside
                // composite ones (a Vector3Field contains three FloatFields of its own)
                ui.AddToClassList("form-row");

                _fields.Add(ui);
            }

            CurrentForm = form;

            foreach (var field in form.Fields)
                field.Changed += form.OnChange;

            form.OnShow();
            Shell.RemoveFromClassList("hidden");
        }

        protected void Hide()
        {
            _visible = false;

            Detach();
            OnHidden();
            Shell?.AddToClassList("hidden");
        }

        protected virtual void OnHidden()
        {
        }

        // clear _current first to make repeat calls no-op (e.g., Apply then Hide)
        // also prevents Hide from triggering a redundant Hide()
        private void Detach()
        {
            if (CurrentForm is null)
                return;

            var ending = CurrentForm;
            CurrentForm = null;
            ending.OnHide();
        }
    }
}