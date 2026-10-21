using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [RequireComponent(typeof(UIDriver))]
    public class SynapseModeDropdown : MonoBehaviour
    {
        [SerializeField] private SynapseRenderer renderer;
        [SerializeField] private Sandbox         sandbox;

        private float    _sliderRange;
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
            var dropdown = root.Q<DropdownField>("synapse-mode");

            // TODO: use explicit method type GetValues<SynapseFilter>() on CoreCLR
            if (Enum.GetValues(typeof(SynapseFilter)) is not SynapseFilter[] modes)
                return;

            var choices = (from mode in modes
                           select mode.ToString()).ToList();

            dropdown.choices = choices;
            dropdown.RegisterValueChangedCallback(_ => { renderer.Filter = modes[dropdown.index]; });
            dropdown.SetValueWithoutNotify(renderer.Filter.ToString());
        }
    }
}