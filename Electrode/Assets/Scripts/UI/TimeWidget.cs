using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [RequireComponent(typeof(UIDriver))]
    public class TimeWidget : MonoBehaviour
    {
        [SerializeField]
        private Sandbox sandbox;

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
            var menu = root.Q(className: "time-menu");

            var field  = menu.Q<FloatField>();
            var slider = menu.Q<Slider>();

            field.RegisterValueChangedCallback(args => Apply(args.newValue));
            slider.RegisterValueChangedCallback(args => ApplyRound(args.newValue));

            ApplyRound(slider.value);
            return;

            // push clamped result to sandbox and back into both field and slider
            // MUST use `SetValueWithoutNotify` to avoid recursive callback
            void Apply(float v)
            {
                v = Mathf.Clamp(v, 0f, ushort.MaxValue);

                field.SetValueWithoutNotify(v);
                slider.SetValueWithoutNotify(v);

                sandbox.TimeScale = v;
            }

            void ApplyRound(float raw)
            {
                const decimal step = .1m;
                Apply((float)(Math.Round((decimal)raw / step) * step));
            }
        }
    }
}