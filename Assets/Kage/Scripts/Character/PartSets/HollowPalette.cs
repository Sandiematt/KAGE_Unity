using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Hollows' colours (SAN-76): near-black cloth leaning violet, ash-violet cracked mask lacquer, bandage wraps, a violet
    /// glow along every break and a cold white-violet eye. Shared by every Hollow's part set so the family reads as one (the Shade,
    /// the tutorial Hollow; Flicker, Ward, Mimic and Husk reuse it). Taken from the JS Hollow skin
    /// (<c>21-characters/enemies/hollow/_skin.js</c>, accent <c>#b9a8ff</c>, eye <c>#ece6ff</c>). Kage is warm black and crimson, the
    /// Order cool slate and silver; a Hollow is violet-black with a light that leaks out of it.
    /// </summary>
    public static class HollowPalette
    {
        public static readonly Color Cloth = KMath.Hex("#251F36");          // robe, hood, sleeves
        public static readonly Color ClothDeep = KMath.Hex("#1A152B");
        public static readonly Color ClothFar = KMath.Hex("#110E1C");       // the same on the far side of the body
        public static readonly Color ClothLit = KMath.Hex("#3B3260");       // a lit edge of torn cloth
        public static readonly Color Wrap = KMath.Hex("#3A3448");           // bandage wraps (kote, shins, hands)
        public static readonly Color WrapDark = KMath.Hex("#1B1822");
        public static readonly Color Glove = KMath.Hex("#17131F");
        public static readonly Color Face = KMath.Hex("#3B3352");           // the gaunt face under the mask
        public static readonly Color FaceDark = KMath.Hex("#231E31");
        public static readonly Color Mask = KMath.Hex("#6B6382");           // cracked mask lacquer
        public static readonly Color MaskDark = KMath.Hex("#363143");
        public static readonly Color MaskLit = KMath.Hex("#9B92B8");
        public static readonly Color Violet = KMath.Hex("#9A80F6");         // the glow along a break
        public static readonly Color VioletDeep = KMath.Hex("#4F3AA6");
        public static readonly Color Glow = KMath.Hex("#B9A8FF");           // the Hollow effect colour (trails, hits, shadow glow)
        public static readonly Color Eye = KMath.Hex("#ECE6FF");
        public static readonly Color Rim = KMath.Hex("#8F7CE0");            // violet back-light
        public static readonly Color Steel = KMath.Hex("#8E88AA");          // the echo of a blade
        public static readonly Color SteelDark = KMath.Hex("#3F3A56");

        // the tutorial Hollow's training-dummy parts
        public static readonly Color Straw = KMath.Hex("#6C5A3A");
        public static readonly Color StrawDark = KMath.Hex("#4A3D27");
        public static readonly Color Rope = KMath.Hex("#4A3F2C");
        public static readonly Color RopeDark = KMath.Hex("#2B241A");
        public static readonly Color Wood = KMath.Hex("#3D3226");
        public static readonly Color WoodDark = KMath.Hex("#241D16");
        public static readonly Color Sack = KMath.Hex("#342D26");
        public static readonly Color SackDark = KMath.Hex("#1F1A16");
    }
}
