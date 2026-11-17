using UnityEngine;

namespace Kage
{
    /// <summary>
    /// One screen or overlay of the campaign flow. It owns a 1600 x 900 rectangle under the stage and a <see cref="NavGroup"/>;
    /// <see cref="Rebuild"/> draws it again from the controller's state (like the JS render functions), keeping the focus on the
    /// item with the same key.
    /// </summary>
    public abstract class FlowPanel
    {
        protected readonly GameFlow flow;
        public readonly RectTransform Root;
        public readonly NavGroup Nav;

        protected FlowController C { get { return flow.Controller; } }

        protected FlowPanel(GameFlow flow, RectTransform stage, string name)
        {
            this.flow = flow;
            Root = Ui.Fill(stage, name);
            Nav = new NavGroup(stage);
            Root.gameObject.SetActive(false);
        }

        public bool Visible { get { return Root.gameObject.activeSelf; } }

        /// <summary>The key of the item that gets the focus when the panel opens.</summary>
        protected virtual string DefaultFocus { get { return null; } }

        protected abstract void Build();

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Rebuild(DefaultFocus);
        }

        public void Hide()
        {
            Root.gameObject.SetActive(false);
        }

        public void Rebuild(string focusKey = null)
        {
            if (focusKey == null && Nav.Focus != null) focusKey = Nav.Focus.key;
            for (int i = Root.childCount - 1; i >= 0; i--)
            {
                var child = Root.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
            Nav.Clear();
            Build();
            Nav.SetFocus(focusKey);
        }

        /// <summary>Applies a command; the panel can use it before the focus does. Returns true if used.</summary>
        public virtual bool Handle(MenuCommand cmd)
        {
            if (Nav.Handle(cmd)) return true;
            if (cmd == MenuCommand.Back) { C.Back(); return true; }
            return false;
        }

        public virtual void Tick() { }
    }
}
