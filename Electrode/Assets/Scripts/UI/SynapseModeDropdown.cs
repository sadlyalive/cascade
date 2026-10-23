using System;
using System.Linq;
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