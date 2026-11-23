using System;

namespace Kage
{
    /// <summary>
    /// How each kind of hanging piece moves on its spring chains (SAN-76): a part cut from an SVG names one in its config
    /// (<c>"soft": "Cape"</c>), so every Hollow mini-boss's cloth and plates move alike. Light cloth (the cape, coat tails, torn strips)
    /// is loose at the tip and follows the air (high drag: it trails the body and bends in the wind); hanging plates are heavy and
    /// stiff (low drag, strong gravity: they swing with the hips and only stir in the wind); tassels are thin and fall straight.
    /// </summary>
    public static class SoftPresets
    {
        /// <summary>A long cape from the shoulders (looser at the hem than Raikon's, SAN-71, so a torn cape flies).</summary>
        public static ChainFeel Cape => new() { stiffRoot = 0.18f, stiffTip = 0.02f, drag = 2.4f, gravity = 6f, breeze = 1.1f };
        /// <summary>Coat tails from the waist: a little firmer than a cape.</summary>
        public static ChainFeel Tails => new() { stiffRoot = 0.22f, stiffTip = 0.03f, drag = 2.2f, gravity = 6.5f, breeze = 0.9f };
        /// <summary>Short torn strips and flaps of cloth.</summary>
        public static ChainFeel Strip => new() { stiffRoot = 0.26f, stiffTip = 0.045f, drag = 2f, gravity = 5f, breeze = 0.8f };
        /// <summary>Plates or bones hanging on cords: heavy, swing with the body, barely stirred by the wind.</summary>
        public static ChainFeel Plates => new() { stiffRoot = 0.4f, stiffTip = 0.14f, drag = 1.1f, gravity = 9f, breeze = 0.25f };
        /// <summary>A thin tassel or cord (Raikon's hat tassels).</summary>
        public static ChainFeel Tassel => new() { stiffRoot = 0.18f, stiffTip = 0.03f, drag = 1.6f, gravity = 9f, breeze = 0.5f };

        /// <summary>A long tail of hair (Raikon's ponytail): a little looser than SAN-71's first ponytail, light, follows the air.</summary>
        public static ChainFeel Hair => new() { stiffRoot = 0.24f, stiffTip = 0.035f, drag = 2.2f, gravity = 4.5f, breeze = 0.9f };
        /// <summary>A fur mantle's tufts (Raikon's; SAN-71's first fur, heavier so its tufts hang): stiff, they ruffle rather than swing.</summary>
        public static ChainFeel Fur => new() { stiffRoot = 0.45f, stiffTip = 0.1f, drag = 2.5f, gravity = 6f, breeze = 0.5f };

        public static readonly string[] Names = { "Cape", "Tails", "Strip", "Plates", "Tassel", "Hair", "Fur" };

        public static ChainFeel Get(string name) => name switch
        {
            "Cape" => Cape,
            "Tails" => Tails,
            "Strip" => Strip,
            "Plates" => Plates,
            "Tassel" => Tassel,
            "Hair" => Hair,
            "Fur" => Fur,
            _ => throw new ArgumentException("no soft-part preset " + name + " (" + string.Join(", ", Names) + ")"),
        };
    }
}
