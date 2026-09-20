using System;
using Cascade.UI.MenuNode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UIDriver : MonoBehaviour
    {
        [SerializeField] private Sandbox sandbox;

        private bool _initialized;
        private PanelRenderer _renderer;
        private int _version = -1;

        private void Start()
        {
            _initialized = true;
            OnEnable();
        }

        private void OnEnable()
        {
            if (!_initialized)
                return;

            _renderer = GetComponent<PanelRenderer>();
            _renderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            _renderer.UnregisterUIReloadCallback(OnUIReload);
        }

        // fires every ui rebuild
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (_version == version)
                return;

            _version = version;

            LatchTimeMenu(root);
            AssembleDropdowns(root);
        }

        // the whole menu structure, as data. rebuilt on every reload, so Clear() first
        private static void AssembleDropdowns(VisualElement root)
        {
            var container = root.Q("menus");
            container.Clear();

            container.Add(MenuBuilder.Build(new MenuLeaf("Create", () => Pick("Create"))));
            container.Add(
                MenuBuilder.Build(
                    new MenuInner(
                        "Place",
                        new MenuInner(
                            "Random",
                            new MenuLeaf("Sphere", () => Pick("Place > Random > Sphere")),
                            new MenuLeaf("Box", () => Pick("Place > Random > Box"))
                        )
                    )
                )
            );
            container.Add(
                MenuBuilder.Build(new MenuInner("Wire", new MenuLeaf("Random", () => Pick("Wire > Random"))))
            );
            container.Add(
                MenuBuilder.Build(new MenuInner("Inject", new MenuLeaf("Random", () => Pick("Inject > Random"))))
            );
        }

        // TODO: placeholder until each leaf opens its form
        private static void Pick(string path)
        {
            Debug.Log(path);
        }

        private void LatchTimeMenu(VisualElement root)
        {
            var field = root.Q<FloatField>("time-scale-field");
            var slider = root.Q<Slider>("slider");

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