using Cascade.Render;
using Cascade.UI.Form.Driver;
using Cascade.UI.Form.Inject;
using Cascade.UI.Form.Place;
using Cascade.UI.Form.Soma;
using Cascade.UI.Form.Synapse;
using Cascade.UI.Form.Workspace;
using Cascade.UI.Menu.MenuNode;
using Cascade.UI.Wireframe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cascade.UI.Menu
{
    /// <summary>
    ///     Owns the topbar's menu structure as code and rebuilds it into the "menus" container whenever
    ///     <see cref="UIDriver" /> reloads the UI. <see cref="MenuBuilder" /> turns a node tree into elements, while this
    ///     class dictates the menu's structure and leaf nodes' functions.
    /// </summary>
    [RequireComponent(typeof(ActionDriver))]
    public sealed class MenuDriver : UIListener
    {
        [SerializeField] private CameraDriver view;
        [SerializeField] private Sandbox      sandbox;
        [SerializeField] private Hint         hint;

        private VisualElement _container;
        private ActionDriver  _forms;

        protected override void OnEnabled()
        {
            _forms = GetComponent<ActionDriver>();

            UI.BusyChanged += SetLock;
        }

        protected override void OnDisabled()
        {
            UI.BusyChanged -= SetLock;
        }

        // the whole menu structure as code for max reusability
        protected override void OnUIReload(VisualElement root)
        {
            var container = _container = root.Q("menus");

            container.Clear(); // rebuilt every reload, so clear menu container first

            // we could shorten this a bit by calling `Reveal` internally, but we lose some scalability unless we keep
            // the lambda constructor. also forms are not MonoBehaviors, so they can't fetch the driver alone
            // IMPORTANT: all forms need sandbox directly since Selection could be stale on reset
            container.Add(MenuBuilder.Build(new MenuInner("Soma",
                                                          new MenuLeaf("Create",
                                                                       () => _forms.Reveal(
                                                                           new CreateForm(sandbox, hint, view))),
                                                          new MenuLeaf("Delete",
                                                                       () => _forms.Reveal(new DeleteForm(sandbox))))));
            container.Add(MenuBuilder.Build(new MenuInner("Place",
                                                          new MenuInner("Random",
                                                                        new MenuLeaf(
                                                                            "Sphere",
                                                                            () => _forms.Reveal(
                                                                                new PlaceSphereForm(sandbox, hint))),
                                                                        new MenuLeaf(
                                                                            "Box",
                                                                            () => _forms.Reveal(
                                                                                new PlaceBoxForm(sandbox, hint)))),
                                                          new MenuLeaf("Grid",
                                                                       () => _forms.Reveal(
                                                                           new PlaceGridForm(sandbox, hint))),
                                                          new MenuLeaf("Ring",
                                                                       () => _forms.Reveal(
                                                                           new PlaceRingForm(sandbox, hint))))));
            container.Add(MenuBuilder.Build(new MenuInner("Synapse",
                                                          new MenuLeaf("Random",
                                                                       () => _forms.Reveal(
                                                                           new WireRandomForm(sandbox))),
                                                          new MenuLeaf("Chain",
                                                                       () => _forms.Reveal(new WireChainForm(sandbox))),
                                                          new MenuLeaf("Fan in",
                                                                       () => _forms.Reveal(new WireFanInForm(sandbox))),
                                                          new MenuLeaf("Fan out",
                                                                       () => _forms.Reveal(
                                                                           new WireFanOutForm(sandbox))),
                                                          new MenuLeaf("Sever",
                                                                       () => _forms.Reveal(new SeverForm(sandbox))))));
            container.Add(MenuBuilder.Build(new MenuInner("Inject",
                                                          new MenuLeaf("Random",
                                                                       () => _forms.Reveal(
                                                                           new InjectRandomForm(sandbox))),
                                                          new MenuLeaf("Uniform",
                                                                       () => _forms.Reveal(
                                                                           new InjectUniformForm(sandbox))))));
            container.Add(MenuBuilder.Build(new MenuInner("Network",
                                                          new MenuLeaf("Reset",
                                                                       () => _forms.Reveal(new ResetForm(sandbox))))));

            SetLock(UI.IsBusy);
        }

        private void SetLock(bool busy)
        {
            _container.EnableInClassList("locked", busy); // suppresses `:hover` USS behavior
            _container.SetEnabled(!busy);                 // disabling element container blocks clicks
        }
    }
}