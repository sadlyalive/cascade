using Cascade.UI.Form;
using Cascade.UI.Menu.MenuNode;
using Cascade.UI.Wireframe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Menu
{
    /// <summary>
    ///     Owns the topbar's menu structure as code and rebuilds it into the "menus" container whenever
    ///     <see cref="UIDriver" /> reloads the UI. <see cref="MenuBuilder" /> turns a node tree into elements; this decides
    ///     what the tree is and what its leaves do.
    /// </summary>
    [RequireComponent(typeof(UIDriver))]
    [RequireComponent(typeof(FormDriver))]
    public sealed class MenuDriver : MonoBehaviour
    {
        [SerializeField] private Sandbox sandbox;
        [SerializeField] private Hint    hint;

        private FormBuilder   _builder;
        private VisualElement _container;
        private FormDriver    _forms;
        private UIDriver      _ui;

        private void OnEnable()
        {
            _builder        =  new FormBuilder(sandbox, hint);
            _forms          =  GetComponent<FormDriver>();
            _ui             =  GetComponent<UIDriver>();
            _ui.Reloaded    += Assemble;
            _ui.BusyChanged += Lock;

            // account for reload event miss especially if re-enabled
            if (_ui.Root != null)
                Assemble(_ui.Root);
        }

        private void OnDisable()
        {
            _ui.Reloaded    -= Assemble;
            _ui.BusyChanged -= Lock;
        }

        // the whole menu structure as code for max reusability
        private void Assemble(VisualElement root)
        {
            var container = _container = root.Q("menus");

            container.Clear(); // rebuilt every reload, so clear menu container first

            container.Add(MenuBuilder.Build(new MenuLeaf("Create", () => Pick("Create"))));
            container.Add(
                MenuBuilder.Build(
                    new MenuInner(
                        "Place",
                        new MenuInner(
                            "Random",
                            new MenuLeaf("Sphere", () => _forms.Reveal(_builder.PlaceSphere())),
                            new MenuLeaf("Box",    () => _forms.Reveal(_builder.PlaceBox()))
                        )
                    )
                )
            );
            container.Add(
                MenuBuilder.Build(
                    new MenuInner("Wire", new MenuLeaf("Random", () => _forms.Reveal(_builder.WireRandom())))
                )
            );
            container.Add(
                MenuBuilder.Build(
                    new MenuInner("Inject", new MenuLeaf("Random", () => _forms.Reveal(_builder.InjectRandom())))
                )
            );

            Lock(_ui.IsBusy);
        }

        // while the UI is busy (e.g. a form is open) nothing here should react. popups are pure `:hover` USS, so
        // `locked`
        // switches them off in Dropdown.uss; disabling the container blocks the clicks
        private void Lock(bool busy)
        {
            _container.EnableInClassList("locked", busy);
            _container.SetEnabled(!busy);
        }

        // TODO: placeholder
        private static void Pick(string path)
        {
            Debug.Log(path);
        }
    }
}