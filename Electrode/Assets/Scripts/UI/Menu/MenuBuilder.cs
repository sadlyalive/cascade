using System;
using Cascade.UI.MenuNode;
using UnityEngine.UIElements;

namespace Cascade.UI
{
    /// <summary>
    ///     Turns a <see cref="MenuNode" /> tree into a hover menu. Structure and behavior live here; appearance lives in
    ///     Dropdown.uss (opening/closing is pure :hover).
    /// </summary>
    public static class MenuBuilder
    {
        public static VisualElement Build(IMenuNode root)
        {
            var ui = new VisualElement();

            Populate(root, ui, ui);

            ui.AddToClassList("menu-root");
            ui.RegisterCallback<PointerLeaveEvent>(_ => ForceHidden(ui, false));

            return ui;
        }

        private static void Populate(IMenuNode node, VisualElement ui, VisualElement rootUI, int depth = 0)
        {
            var label = new Label(node.Label);

            ui.AddToClassList("menu-node");
            ui.Add(label);

            switch (node)
            {
                case MenuLeaf leaf:
                    ui.RegisterCallback<ClickEvent>(_ =>
                        {
                            // uses inline uxml styling to hide the entire tree since uss `:hover` doesn't account for
                            // picking an option
                            ForceHidden(rootUI, true);
                            leaf.OnClick();
                        }
                    );
                    break;
                case MenuInner inner:
                    ProcessInner(inner, ui, rootUI, depth);
                    break;
                default:
                    throw new ArgumentException("Unhandled menu node type");
            }
        }

        private static void ForceHidden(VisualElement rootUI, bool hidden)
        {
            var popup = rootUI.Q(className: "menu-popup");

            // TODO: null-conditional assignment (`popup?.style ...`) when coreclr releases (c# 14)
            if (popup != null)
                // null StyleKeyword delegates display to `:hover` rule
                popup.style.display = hidden ? DisplayStyle.None : StyleKeyword.Null;
        }

        private static void ProcessInner(MenuInner inner, VisualElement ui, VisualElement rootUI, int depth)
        {
            // exclude top-level menu nodes from arrow sugar
            if (depth > 0)
            {
                var arrow = new Label("›");

                arrow.AddToClassList("menu-arrow");
                arrow.pickingMode = PickingMode.Ignore;

                ui.Add(arrow);
            }

            var popup = new VisualElement();

            popup.AddToClassList("menu-popup");

            foreach (var child in inner.Children)
            {
                var childUI = new VisualElement();

                childUI.AddToClassList("menu-node");
                popup.Add(childUI);

                Populate(child, childUI, rootUI, depth + 1);
            }

            ui.Add(popup);
        }
    }
}