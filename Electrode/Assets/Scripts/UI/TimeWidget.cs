using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    public class TimeWidget : UIListener
    {
        [SerializeField] private float   minSpeed = 0.001f;
        [SerializeField] private float   maxSpeed = 10f;
        [SerializeField] private Sandbox sandbox;

        private float _sliderRange;

        public float MinSpeed
        {
            get => minSpeed;
            private set
            {
                minSpeed     = value;
                _sliderRange = Mathf.Log(maxSpeed / value);
            }
        }

        public float MaxSpeed
        {
            get => maxSpeed;
            private set
            {
                maxSpeed     = value;
                _sliderRange = Mathf.Log(value / minSpeed);
            }
        }

        private void Awake()
        {
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
        }

        protected override void OnEnabled()
        {
            _sliderRange = Mathf.Log(maxSpeed / minSpeed);
        }

        protected override void OnUIReload(VisualElement root)
        {
            var menu = root.Q(className: "time-menu");

            var field  = menu.Q<FloatField>();
            var slider = menu.Q<Slider>();

            field.RegisterValueChangedCallback(args => Apply(args.newValue));
            slider.RegisterValueChangedCallback(args =>
            {
                const int digits = 3;

                var v = args.newValue;

                if (v > 0f)
                {
                    v = MinSpeed * Mathf.Exp(v * _sliderRange);
                    var scale = Math.Pow(10, digits - 1 - Math.Floor(Math.Log10(v))); // sig fig scale
                    v = (float)(Math.Round(v * scale) / scale);                       // apply round
                }

                Apply(v);
            });

            Apply(sandbox.TimeScale);
            return;

            // push clamped result to sandbox and back into both field and slider
            // MUST use `SetValueWithoutNotify` to avoid recursive callback
            void Apply(float speed)
            {
                speed = Mathf.Clamp(speed, 0f, ushort.MaxValue);

                sandbox.TimeScale = speed;
                field.SetValueWithoutNotify(speed);

                var alpha = speed <= MinSpeed ? 0f : Mathf.Clamp01(Mathf.Log(speed / MinSpeed) / _sliderRange);
                slider.SetValueWithoutNotify(alpha);
            }
        }
    }
}