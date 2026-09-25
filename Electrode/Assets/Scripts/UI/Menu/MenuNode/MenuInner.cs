namespace Cascade.UI.Menu.MenuNode
{
    /// <summary>
    ///     Represents an intermediate dropdown node that guarantees a submenu.
    /// </summary>
    public class MenuInner : IMenuNode
    {
        public MenuInner(string label, params IMenuNode[] children)
        {
            Label    = label;
            Children = children;
        }

        public IMenuNode[] Children { get; }

        public string Label { get; }
    }
}