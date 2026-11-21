using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The shared look of the Order foes drawn from the owner's paintings (SAN-100 SVG redesign, the Ironclad first): the tones an
    /// Order trace is cut into (<see cref="Style"/>), the crimson back-light rim and the white eye's glow. It is the Order's
    /// counterpart of <see cref="HollowArt"/>: an Order foe's config (<c>Tools/order_art/&lt;name&gt;/rig.json</c>) snaps its trace's
    /// colours to these tone names and the game paints them in these colours, so every Order foe drawn this way shares one palette.
    /// <list type="bullet">
    /// <item>Black lacquer, darkest to lit: <c>Ink</c>, <c>Shadow</c>, <c>Lacquer</c>, <c>LacquerMid</c>, <c>LacquerLit</c>; the cold
    /// grey light along the plate edges: <c>Edge</c>, <c>EdgeLit</c>. A little blue in every one: the Order's slate, not Kage's warm black.</item>
    /// <item>The odoshi lacing, crest and rope, deepest to hottest: <c>CrimsonDeep</c>, <c>Crimson</c>, <c>CrimsonLit</c>.</item>
    /// <item>Steel and light: <c>Steel</c> (a blade's flat), <c>White</c> (the eye, a blade's edge).</item>
    /// </list>
    /// Far-side parts (the far arm and leg, drawn from the near ones) are half way to the deepest ink. The rim is the painting's
    /// crimson back-light (<see cref="Rim"/>). It is new beside <see cref="OrderPalette"/> and changes nothing of the Order foes
    /// drawn in code.
    /// </summary>
    public static class OrderArt
    {
        public const float EyeGlow = 2.6f;

        /// <summary>The crimson back-light the painting outlines its figure with.</summary>
        public static readonly Color Rim = KMath.Hex("#C0303C");

        static readonly Color FarInk = KMath.Hex("#040407");

        static SvgRigStyle _style;

        public static SvgRigStyle Style => _style ??= Make();

        static SvgRigStyle Make()
        {
            var s = new SvgRigStyle { rim = Rim, body = KMath.Hex("#1C1B27"), far = c => Color.Lerp(c, FarInk, 0.5f) };
            void T(string name, string hex) => s.tones[name] = KMath.Hex(hex);
            T("Ink", "#060609");
            T("Shadow", "#100F16");
            T("Lacquer", "#17161F");
            T("LacquerMid", "#1E1D29");
            T("LacquerLit", "#363345");
            T("Edge", "#5A5770");
            T("EdgeLit", "#83809A");
            T("CrimsonDeep", "#3A0D16");
            T("Crimson", "#A02A36");
            T("CrimsonLit", "#F04A52");
            T("Steel", "#A6A4B6");
            T("White", "#F0EEF4");
            return s;
        }

        /// <summary>An Order foe's rig settings for an SVG-drawn body: the crimson rim, slate-black lacquer, no scarf or coat of Kage's.</summary>
        public static void Dress(RigDef d) => Style.Dress(d);
    }
}
