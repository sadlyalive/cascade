using Cascade.Render;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    public class SynapseModeDropdown : UIListener
    {
        [SerializeField] private SynapseRenderer renderer;
        [SerializeField] private Sandbox         sandbox;

        protected override void OnUIReload(VisualElement root)
        {
            var dropdown = root.Q<EnumField>("synapse-mode");

            dropdown.RegisterValueChangedCallback(_ => { renderer.Filter = (SynapseFilter)dropdown.value; });
            dropdown.SetValueWithoutNotify(renderer.Filter);
        }
    }
}