using System;

namespace Kage
{
    /// <summary>
    /// How an Armory tile or card is picked (SAN-108: "the thing should be applied when only clicked, not when hovering"). The
    /// selection (the detail panel, the live Kage, the weapon illustration) changes only when the tile is pressed: a mouse click,
    /// or confirm (Enter, Space, J, gamepad A) on the focused tile. Moving the focus onto it (hover, arrows, the stick) only
    /// highlights it, through the item's own focus tween.
    /// </summary>
    public static class ArmoryTilePick
    {
        /// <summary>Makes pressing <paramref name="item"/> select <paramref name="id"/>, and focusing it do nothing but highlight.</summary>
        public static void Bind(NavItem item, string id, Action<string> select)
        {
            item.onSubmit = () => select(id);
            item.onUserFocus = null;
        }
    }
}
