using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cascade.UI.Form
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
    [RequireComponent(typeof(UIDriver))]
    public sealed class FormDriver : MonoBehaviour
    {
        private IDisposable   _busy;
        private ActionForm    _current;
        private ScrollView    _fields;
        private VisualElement _root;
        private Label         _title;
        private UIDriver      _ui;

        public bool Visible => _root != null && !_root.ClassListContains("hidden");

        private void Update()
        {
            if (!Visible)
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

            var hoverUI = _ui.CursorPick;

            if (hoverUI == null || !IsInFormUI(hoverUI))
                Hide();

            return;

            bool IsInFormUI(VisualElement element)
            {
                return element == _root || _root.Contains(element);
            }
        }

        private void OnEnable()
        {
            _ui          =  GetComponent<UIDriver>();
            _ui.Reloaded += Rebuild;

            // the UI may have loaded before this enabled, in which case that event is already gone
            if (_ui.Root != null)
                Rebuild(_ui.Root);
        }

        private void OnDisable()
        {
            _ui.Reloaded -= Rebuild;
            Hide(); // don't leave a hint showing (or the UI held busy) for a form nobody can close
        }

        /// <summary>Replaces the shell's contents with <paramref name="form" />'s inputs and reveals it.</summary>
        public void Reveal(ActionForm form)
        {
            form.Validate();

            Detach(); // a form being replaced never went through Hide

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

            _current = form;

            foreach (var field in form.Fields)
                field.Changed += form.OnChange;

            form.OnShow();

            // old token persists when replacing a form with another so the UI doesn't unlock for a frame in between
            _busy ??= _ui.Busy();

            _root.RemoveFromClassList("hidden");
        }

        // visibly hide and detach current form
        private void Hide()
        {
            Detach();

            _busy?.Dispose();
            _busy = null;

            _root?.AddToClassList("hidden");
        }

        // find and reattach relevant form components per UI rebuild
        private void Rebuild(VisualElement root)
        {
            var form = root.Q("form");

            // prevent duplicate binding on re-enable (the only time when Rebuild() calls without an actual UI rebuild)
            if (form != _root)
            {
                _root   = form;
                _title  = form.Q<Label>("title");
                _fields = form.Q<ScrollView>("fields");

                // acts on whichever form is currently shown since the ui structure is reused
                form.Q<Button>("apply").clicked += Apply;
            }

            Hide();
        }

        private void Apply()
        {
            _current?.OnApply();
            Hide();
        }

        // clear _current first to make repeat calls no-op (e.g., Apply then Hide)
        // also covers prevents OnHide from triggering a redundant Hide
        private void Detach()
        {
            if (_current is null)
                return;

            var ending = _current;
            _current = null;
            ending.OnHide();
        }
    }
}