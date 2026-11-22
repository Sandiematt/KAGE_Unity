using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The shared look of the Hollow mini-bosses drawn from the owner's paintings (SAN-76 SVG redesign; the Husk next, SAN-112):
    /// the tones a Hollow trace is cut into (<see cref="Style"/>), the violet back-light rim and the cold glows. A Hollow's config
    /// (<c>Tools/hollow_art/&lt;name&gt;/rig.json</c>) snaps its trace's colours to these tone names; the game paints them in these
    /// colours, so every Hollow drawn this way shares one palette, whatever its painting's exact colours.
    /// <list type="bullet">
    /// <item>Cloth, darkest to lit: <c>Ink</c>, <c>Shadow</c>, <c>Cloth</c>, <c>ClothMid</c>, <c>ClothLit</c>.</item>
    /// <item>The violet light painted along edges and cracks, deepest to hottest: <c>VioletDeep</c>, <c>Rim</c>, <c>RimBright</c>, <c>RimHot</c>.</item>
    /// <item>Mask and steel: <c>SteelDark</c>, <c>Steel</c>, and <c>White</c> (the eye, the blade's edge, paper shide).</item>
    /// <item>Rope (the Husk's shimenawa, SAN-112): <c>RopeDark</c>, <c>Rope</c>, <c>RopeLit</c>.</item>
    /// </list>
    /// Far-side parts (the far arm and leg, drawn from the near ones) are half way to the deepest ink, as the painting's far limbs are
    /// near-black silhouettes. The rim is <see cref="HollowPalette.Rim"/>, the same back-light as the other Hollows. Glows are part
    /// flags in the config: the eye <see cref="EyeGlow"/>, a cracked mask's light <see cref="CrackGlow"/>. This class is new beside
    /// <see cref="HollowPalette"/> and changes nothing of the Hollows drawn in code (tutorial Hollow, Flicker, Husk).
    /// </summary>
    public static class HollowArt
    {
        public const float EyeGlow = 2.6f, CrackGlow = 1.6f;

        static readonly Color FarInk = KMath.Hex("#06040A");

        static SvgRigStyle _style;

        public static SvgRigStyle Style => _style ??= Make();

        static SvgRigStyle Make()
        {
            var s = new SvgRigStyle { rim = HollowPalette.Rim, body = KMath.Hex("#211A36"), far = c => Color.Lerp(c, FarInk, 0.5f) };
            void T(string name, string hex) => s.tones[name] = KMath.Hex(hex);
            T("Ink", "#0A0810");
            T("Shadow", "#151021");
            T("Cloth", "#211A36");
            T("ClothMid", "#2C2346");
            T("ClothLit", "#3A2D5C");
            T("VioletDeep", "#4E3285");
            T("Rim", "#6A42B4");
            T("RimBright", "#7E52D4");
            T("RimHot", "#A07EEA");
            T("SteelDark", "#6A5E84");
            T("Steel", "#B0A6C4");
            T("White", "#F0ECF6");
            // the Husk's shimenawa (SAN-112): straw rope gone dark, the only warm tone of a Hollow
            T("RopeDark", "#2E2222");
            T("Rope", "#4A3734");
            T("RopeLit", "#7A6158");
            return s;
        }

        /// <summary>A Hollow's rig settings for an SVG-drawn body: the violet rim, violet-black cloth, no scarf or coat of Kage's.</summary>
        public static void Dress(RigDef d) => Style.Dress(d);

        static SvgRigStyle _husk;

        /// <summary>
        /// The Hollow Husk's own colours (SAN-112 definition pass): the same tone names as <see cref="Style"/>, each painted in the
        /// colour the owner's SVG uses for it, so the Husk reads in its art's plum and lavender rather than the shared palette's cooler
        /// blue-violet. Each is the area-weighted mean of the trace colours the cut snaps to that tone (measured over the figure, see
        /// ADR-0005, SAN-112 definition notes). The far side, the rim and the glows are the shared ones; the Shade keeps
        /// <see cref="Style"/>.
        /// </summary>
        public static SvgRigStyle Husk => _husk ??= MakeHusk();

        static SvgRigStyle MakeHusk()
        {
            var s = new SvgRigStyle { rim = HollowPalette.Rim, body = KMath.Hex("#1F132A"), far = c => Color.Lerp(c, FarInk, 0.5f) };
            void T(string name, string hex) => s.tones[name] = KMath.Hex(hex);
            T("Ink", "#0F0914");
            T("Shadow", "#150D1E");
            T("Cloth", "#1F132A");
            T("ClothMid", "#2A193D");
            T("ClothLit", "#3A284C");
            T("VioletDeep", "#5B3D7A");
            T("Rim", "#6D4999");
            T("RimBright", "#8B65B4");
            T("RimHot", "#A87BDC");
            T("SteelDark", "#7E6A8A");
            T("Steel", "#CBB3DA");
            T("White", "#EBE8EC");
            T("RopeDark", "#3B2B31");
            T("Rope", "#513E3D");
            T("RopeLit", "#806761");
            return s;
        }
    }
}
