using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Raikon's colours for his SVG rig (SAN-71 redesign, the SAN-76 pipeline): the seven colours of the owner's trace, each a tone
    /// name his config (<c>Tools/raikon_art/rig.json</c>) snaps the trace to, painted exactly as the trace paints it.
    /// <list type="bullet">
    /// <item>Lacquer and cloth, darkest to lit: <c>Ink</c> (the trace's black, its canvas and its outlines), <c>Shadow</c>, <c>Cloth</c>.</item>
    /// <item>Fur, hair and the hilt's light, darkest to lit: <c>Mauve</c>, <c>Pale</c>, <c>White</c>.</item>
    /// <item>The red of the lacing, tassels, cape and glowing tachi: <c>Crimson</c>.</item>
    /// </list>
    /// Far-side parts (the far arm and leg, drawn from the near ones) are one step darker along the trace's own palette, as SAN-71's
    /// first cut drew them: white to pale, pale to mauve, mauve to cloth, cloth to shadow, shadow to a deeper shadow, crimson a little
    /// deeper. The rim and body colours are SAN-71's.
    /// </summary>
    public static class RaikonArt
    {
        public const float EyeGlow = 2.6f, BladeGlow = 1.7f;

        /// <summary>The crimson back-light (SAN-71's rim).</summary>
        public static readonly Color Rim = new Color32(0xC7, 0x18, 0x1F, 0xFF);

        static SvgRigStyle _style;

        public static SvgRigStyle Style => _style ??= Make();

        static readonly (string tone, string hex, string far)[] Tones =
        {
            ("Ink", "#010000", "#010000"),
            ("Shadow", "#1E1218", "#140B10"),
            ("Cloth", "#39222A", "#1E1218"),
            ("Mauve", "#8A6C73", "#39222A"),
            ("Pale", "#D2B5B7", "#8A6C73"),
            ("White", "#F9EEEF", "#D2B5B7"),
            ("Crimson", "#C7181F", "#9A1218"),
        };

        static SvgRigStyle Make()
        {
            var farOf = new Dictionary<Color, Color>();
            var s = new SvgRigStyle { rim = Rim, body = KMath.Hex("#1E1218") };
            foreach (var (tone, hex, far) in Tones)
            {
                s.tones[tone] = KMath.Hex(hex);
                farOf[KMath.Hex(hex)] = KMath.Hex(far);
            }
            s.far = c => farOf.TryGetValue(c, out var f) ? f : c;
            return s;
        }

        /// <summary>Raikon's rig settings for his SVG-drawn body: the crimson rim, no scarf or coat of Kage's.</summary>
        public static void Dress(RigDef d) => Style.Dress(d);
    }
}
