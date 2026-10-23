using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [RequireComponent(typeof(UIDriver))]
    public abstract class UIListener : MonoBehaviour
    {
        protected UIDriver UI;

        private bool _started;

        private void Start()
        {
            _started = true;
            RefreshState();
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            RefreshState();
        }

        private void OnDisable()
        {
            if (!_started)
                return;

            FlushState();
        }

        private void RefreshState()
        {
            if (!_started || !isActiveAndEnabled)
                return;

            // UI needs to be set before template method call
            UI = GetComponent<UIDriver>();

            OnEnabled();

            UI.Reloaded += OnUIReload;

            // rebuild on enable if UI loaded beforehand
            if (UI.Root != null)
                OnUIReload(UI.Root);
        }

        private void FlushState()
        {
            UI.Reloaded -= OnUIReload;
            OnDisabled();
        }

        protected abstract void OnUIReload(VisualElement root);

        protected virtual void OnEnabled()
        {
        }

        protected virtual void OnDisabled()
        {
        }
    }
}