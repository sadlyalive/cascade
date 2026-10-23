using Cascade.UI.Wireframe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    public class HintToggleWidget : UIListener
    {
        [SerializeField] private Hint hint;

        protected override void OnUIReload(VisualElement root)
        {
            var toggle = root.Q<Toggle>("hint-toggle");

            toggle.RegisterValueChangedCallback(args => hint.Visible = args.newValue);

            hint.Visible = toggle.value;
        }
    }
}
