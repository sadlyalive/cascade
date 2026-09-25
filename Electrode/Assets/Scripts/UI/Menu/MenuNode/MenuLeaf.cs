using System;

namespace Cascade.UI.Menu.MenuNode
{
    /// <summary>
    ///     Represents a dropdown node that guaranteeably defines behavior and does not own a submenu.
    /// </summary>
    public sealed class MenuLeaf : IMenuNode
    {
        public MenuLeaf(string label, Action onClick)
        {
            Label   = label;
            OnClick = onClick;
        }

        public Action OnClick { get; }

        public string Label { get; }
    }
}