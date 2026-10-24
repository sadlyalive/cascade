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
        ///     The current UI tree's root, or null before the first load. Must expose for plugins that dynamically reattach to the
        ///     UI on reenable since they will miss `Reloaded` events while inactive.
        /// </summary>
        public VisualElement Root { get; private set; }

        /// <summary>
        ///     Raises after every UI rebuild after <see cref="Root" /> refreshes.
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
        ///         Locks menus if at least one busy disposable is alive.
        ///     </para>
        ///     <para>
        ///         Better than a shared list since the token guarantees consumers can only dispose their own busy state. Redundant
        ///         disposes are also harmless.
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

            if (mouse is null || panel is null)
                return null;

            // ScreenToPanel wants a top-left origin but InputSystem reports bottom-left
            var screen = mouse.position.ReadValue();
            screen.y = Screen.height - screen.y;

            var hover = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, screen));

            // exclude the top-level UI root which could be pickable since it's fullscreen despite being invisible
            if (hover == null || hover == Root)
                return _cursorPick = null;

            return _cursorPick = Root.Contains(hover) || IsInDropdown(hover) ? hover : null;
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

        /// <returns>Whether <paramref name="element" /> belongs to a dropdown's popup</returns>
        public static bool IsInDropdown(VisualElement element)
        {
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (e.ClassListContains("unity-base-dropdown"))
                    return true;
            }

            return false;
        }

        private sealed class BusyToken : IDisposable
        {
            private UIDriver _owner;

            public BusyToken(UIDriver owner)
            {
                _owner = owner;
            }

            // second Dispose finds _owner already null and no-ops
            public void Dispose()
            {
                if (_owner is null)
                    return;

                // _owner must clear BEFORE Release() because Release() raises BusyChanged, which could run code
                // that redundantly calls Dispose(). if that happens, _owner must be null by then to no-op
                var owner = _owner;
                _owner = null;
                owner.Release();
            }
        }
    }
}