using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cascade.UI.Form
{
    /// <summary>
    ///     Fills the form shell declared in MainUI.uxml (a "title" label, a "fields" scroll view, and "apply"/"close"
    ///     buttons) from an <see cref="ActionForm" />, and dismisses it on Esc or a click elsewhere. The shell is static
    ///     UXML; what goes in it is data, so one shell serves every form. Appearance lives in Form.uss. Re-attaches to the
    ///     shell whenever <see cref="UIDriver" /> reloads the UI.
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

            if (kb != null &&
                kb.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            // a left click anywhere outside the form cancels it. left only: middle-drag orbits the camera, which should
            // stay usable while a form is open
            if (mouse == null ||
                !mouse.leftButton.wasPressedThisFrame)
                return;

            var hoverUI = _ui.CursorPick;

            if (hoverUI == null ||
                !Owns(hoverUI))
                Hide();
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

            // one token spans replacing a form with another so the UI doesn't unlock for a frame in between
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

        // (re)binds to the shell in a freshly built UI tree
        private void Rebuild(VisualElement root)
        {
            Detach(); // the old tree's elements are gone, but the form's side effects (e.g. a hint) might not be

            _root   = root.Q("form");
            _title  = _root.Q<Label>("title");
            _fields = _root.Q<ScrollView>("fields");

            // form action buttons simply act on whichever form is currently shown (shared frame)
            _root.Q<Button>("apply").clicked += () =>
            {
                _current?.OnApply();
                Hide();
            };

            Hide();
        }

        /// <summary>Whether <paramref name="element" /> is the form itself or something inside it.</summary>
        private bool Owns(VisualElement element)
        {
            return element == _root || _root.Contains(element);
        }

        private void Detach()
        {
            var ending = _current;

            // clear preemptively so a second hide op (e.g., applying then clicking outside) won't run OnHide twice
            _current = null;
            ending?.OnHide(); // good practice to run callback after time-sensitive cleanup
        }
    }
}