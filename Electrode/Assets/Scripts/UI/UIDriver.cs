using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [NoReorder]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UIDriver : MonoBehaviour
    {
        private bool _initialized;

        private PanelRenderer _renderer;
        private int           _version = -1;

        private int           _pickFrame = -1;
        private VisualElement _cursorPick;

        private int _busyCount;

        /// <summary>
        ///     The current UI tree's root, or null before the first load. Subscribers that enable after the first
        ///     <see cref="Reloaded" /> should build from this immediately, since they missed that event.
        /// </summary>
        public VisualElement Root { get; private set; }

        /// <summary>
        ///     Raises after every UI (re)build, once <see cref="Root" /> is fresh, with the new root.
        /// </summary>
        public event Action<VisualElement> Reloaded;

        /// <summary>Whether anything currently holds a <see cref="Busy" /> token.</summary>
        public bool IsBusy => _busyCount > 0;

        /// <summary>Raises when <see cref="IsBusy" /> flips, with its new value.</summary>
        public event Action<bool> BusyChanged;

        /// <summary>
        ///     <para>The UI element under the mouse if any, otherwise null.</para>
        ///     <para>
        ///         Deterministically recomputes then transiently saves the result per-frame since tracking enter/leave events
        ///         might inevitably drift when UI rebuilds and some elements hide.
        ///     </para>
        /// </summary>
        public VisualElement CursorPick => _pickFrame == Time.frameCount ? _cursorPick : UpdateCursorPick();

        /// <summary>
        ///     Whether the mouse is over an eligible UI element.
        /// </summary>
        public bool CursorOverUI => CursorPick is not null;

        private void OnEnable()
        {
            if (!_initialized)
                return;

            _renderer = GetComponent<PanelRenderer>();
            _renderer.RegisterUIReloadCallback(OnUIReload);
        }

        // activates only after OnEnable
        private void Start()
        {
            _initialized = true;
            OnEnable();
        }

        private void OnDisable()
        {
            _renderer.UnregisterUIReloadCallback(OnUIReload);
        }

        /// <summary>
        ///     <para>
        ///         Marks the UI busy until the returned token is disposed, e.g. <c>using var busy = ui.Busy();</c> or held in a
        ///         field for a form's lifetime. Menus and the like lock while <see cref="IsBusy" />.
        ///     </para>
        ///     <para>
        ///         Tokens count, so overlapping holders are fine, and disposing one twice is harmless. The point over a
        ///         shared list is that a holder can only release what it was granted.
        ///     </para>
        /// </summary>
        public IDisposable Busy()
        {
            if (_busyCount++ == 0)
                BusyChanged?.Invoke(true);

            return new BusyToken(this);
        }

        private void Release()
        {
            if (--_busyCount == 0)
                BusyChanged?.Invoke(false);
        }

        private VisualElement UpdateCursorPick()
        {
            _pickFrame = Time.frameCount;

            var mouse = Mouse.current;
            var panel = Root?.panel;

            if (mouse is null ||
                panel is null)
                return null;

            // ScreenToPanel wants a top-left origin but InputSystem reports bottom-left
            var screen = mouse.position.ReadValue();
            screen.y = Screen.height - screen.y;

            var hover = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen));

            // exclude the top-level UI root which could be pickable since it's fullscreen despite being invisible
            return _cursorPick = hover != null && hover != Root && Root.Contains(hover) ? hover : null;
        }

        // fires every UI rebuild
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_version == version)
                return;

            _version = version;
            Root     = root;

            Reloaded?.Invoke(root);
        }

        private sealed class BusyToken : IDisposable
        {
            private UIDriver _owner;

            public BusyToken(UIDriver owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                var owner = _owner;

                // cleared first so disposing twice can't release twice (which would steal someone else's hold)
                _owner = null;
                owner?.Release();
            }
        }
    }
}