using Cascade.UI.Wireframe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [RequireComponent(typeof(UIDriver))]
    public class HintToggleWidget : MonoBehaviour
    {
        [SerializeField]
        private Hint hint;

        private UIDriver _ui;

        private void OnEnable()
        {
            _ui          =  GetComponent<UIDriver>();
            _ui.Reloaded += Latch;

            if (_ui.Root != null)
                Latch(_ui.Root);
        }

        private void OnDisable()
        {
            _ui.Reloaded -= Latch;
        }

        private void Latch(VisualElement root)
        {
            var toggle = root.Q<Toggle>("hint-toggle");

            toggle.RegisterValueChangedCallback(args => hint.Visible = args.newValue);

            hint.Visible = toggle.value;
        }
    }
}