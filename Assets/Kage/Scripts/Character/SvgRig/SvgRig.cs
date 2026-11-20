using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>The colours an SVG rig is painted in: one per tone of the cut, a darker version for far-side parts, and the rim.</summary>
    public sealed class SvgRigStyle
    {
        public readonly Dictionary<string, Color> tones = new();
        /// <summary>A far-side part's colour from its near-side one (the far arm and leg drawn from the near ones).</summary>
        public Func<Color, Color> far = c => c;
        /// <summary>The rig's back-light rim (<see cref="RigDef.rimColor"/>) and its body colour.</summary>
        public Color rim, body;

        public Color Of(string tone, bool farSide)
        {
            if (!tones.TryGetValue(tone, out var c)) throw new ArgumentException("the style has no tone " + tone);
            return farSide ? far(c) : c;
        }
    }

    /// <summary>
    /// Builds a character's parts from an SVG cut (<see cref="SvgRigData"/>, SAN-76 SVG redesign) on the shared gameplay skeleton.
    /// Nothing here is about one character: the data says which piece of the painting goes on which bone and how it is placed, the
    /// style says the colours. Art never moves a joint: only parts are added.
    /// <para>
    /// <b>Maps</b> (a part's <c>map</c>, SVG px to bind space). The painting is first fitted to Kage's reference frame by the data's
    /// <c>fit</c> (scale k, an SVG point on a reference point, 400 reference px to a unit). Then:
    /// <list type="bullet">
    /// <item><c>trunk</c>: that fit alone.</item>
    /// <item><c>pin sx sy TARGET k deg</c>: the SVG point s on TARGET (a bone's pivot, or <c>rx ry</c> in reference px), scaled by k and
    /// turned by deg (clockwise on screen); a rigid piece placed by one point (a head, a boot, a plate column).</item>
    /// <item><c>seg ax ay bx by FROM TO</c>: the drawn joints a and b on the pivots of FROM and TO (turned, stretched along the segment,
    /// the fit's scale across): a limb segment (SAN-71's method).</item>
    /// <item><c>line ax ay bx by rax ray rbx rby</c>: the same onto two reference points: a cape or coat tails hung from the body.</item>
    /// <item><c>attach ax ay d0x d0y d1x d1y BONE TO0 TO1</c>: a turned onto BONE's pivot, the drawn direction d0 to d1 along TO0 to TO1 (a hand
    /// on its forearm).</item>
    /// <item><c>blade gx gy tx ty len</c>: the grip g on the sword bone's pivot and the tip t along Kage's blade, scaled so the tip is
    /// <c>len</c> units out: what you see is what hits.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Parts</b>: every polygon of the region in paint order in one mesh (<see cref="PartDef.polys"/>); a <c>rim</c> part gets the
    /// rig's world-fixed back-light rim round the region's outline; a <c>far</c> part is painted in the style's far colours; a
    /// <c>soft</c> part hangs on its spring chains with its <see cref="SoftPresets"/> feel; a <c>swing</c> part hangs from its own
    /// pivot (spikes, shards).
    /// </para>
    /// </summary>
    public static class SvgRig
    {
        const float RefScale = KageModel.RefScale;

        public static void Build(RigDef d, SvgRigData data, SvgRigStyle style)
        {
            foreach (var spec in data.parts)
            {
                if (!data.regions.TryGetValue(spec.region, out var region)) throw new ArgumentException($"{spec.name}: no region {spec.region}");
                var map = Map(d, data, spec.map, spec.name);
                var polys = new List<(Vector2[] pts, Color col)>(region.polys.Count);
                foreach (var (tone, pts) in region.polys) polys.Add((Apply(map, pts), style.Of(tone, spec.far)));
                var p = new PartDef
                {
                    name = spec.name, bone = spec.bone, order = spec.slot * 4 + spec.sub, polys = polys, rim = false, glow = spec.glow,
                    swordLayer = spec.sword, color = style.body,
                    role = spec.role != null ? (PartRole)Enum.Parse(typeof(PartRole), spec.role) : PartRole.Body,
                };
                if (spec.rim && spec.soft == null && region.outline != null) p.rimPts = Apply(map, region.outline);
                if (spec.soft != null)
                {
                    p.soft = SoftKind.Chain;
                    p.chainFeel = SoftPresets.Get(spec.soft);
                    p.chains = new List<Vector2[]>();
                    foreach (var ch in spec.chains) p.chains.Add(Apply(map, ch));
                    if (p.chains.Count == 0) throw new ArgumentException(spec.name + ": a soft part needs chains");
                }
                if (spec.swingPivot.HasValue)
                {
                    p.jigglePivot = map(spec.swingPivot.Value);
                    p.jiggle = spec.swing;
                }
                d.parts.Add(p);
            }
        }

        static Vector2[] Apply(Func<Vector2, Vector2> map, Vector2[] pts)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = map(pts[i]);
            return r;
        }

        /// <summary>The fit of the painting onto Kage's reference frame, in bind units.</summary>
        static Vector2 Fit(SvgRigData data, Vector2 s) =>
            KageModel.P(data.fitRef.x + (s.x - data.fitSvg.x) * data.fitK, data.fitRef.y + (s.y - data.fitSvg.y) * data.fitK);

        static float N(string s) => float.Parse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

        static bool IsBone(string s) => Enum.TryParse(typeof(B), s, out _) && !float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

        public static Func<Vector2, Vector2> Map(RigDef d, SvgRigData data, string[] m, string who)
        {
            if (m == null || m.Length == 0) throw new ArgumentException(who + ": no map");
            Vector2 Pivot(string bone) => d.pivot[(int)(B)Enum.Parse(typeof(B), bone)];
            Func<Vector2, Vector2> G = s => Fit(data, s);
            Func<Vector2, Vector2> Segment(Vector2 a, Vector2 b, Vector2 pa, Vector2 pb)
            {
                Vector2 ga = G(a), gb = G(b);
                Vector2 u = (gb - ga).normalized, w = (pb - pa).normalized;
                float s = (pb - pa).magnitude / (gb - ga).magnitude;
                Vector2 un = new(-u.y, u.x), wn = new(-w.y, w.x);
                return x =>
                {
                    Vector2 r = G(x) - ga;
                    return pa + w * (Vector2.Dot(r, u) * s) + wn * Vector2.Dot(r, un);
                };
            }
            switch (m[0])
            {
                case "trunk":
                    return G;
                case "pin":
                {
                    var s = new Vector2(N(m[1]), N(m[2]));
                    Vector2 target; int i;
                    if (IsBone(m[3])) { target = Pivot(m[3]); i = 4; }
                    else { target = KageModel.P(N(m[3]), N(m[4])); i = 5; }
                    float k = N(m[i]), deg = m.Length > i + 1 ? N(m[i + 1]) : 0f;
                    // reference px have y down: a turn clockwise on screen, and P turns the frame half round (orientation kept)
                    return x => target + KMath.Rot(-(x - s) * k / RefScale, -deg);
                }
                case "seg":
                    return Segment(new Vector2(N(m[1]), N(m[2])), new Vector2(N(m[3]), N(m[4])), Pivot(m[5]), Pivot(m[6]));
                case "line":
                    return Segment(new Vector2(N(m[1]), N(m[2])), new Vector2(N(m[3]), N(m[4])),
                        KageModel.P(N(m[5]), N(m[6])), KageModel.P(N(m[7]), N(m[8])));
                case "attach":
                {
                    Vector2 at = new(N(m[1]), N(m[2])), d0 = new(N(m[3]), N(m[4])), d1 = new(N(m[5]), N(m[6]));
                    B bone = (B)Enum.Parse(typeof(B), m[7]), to0 = (B)Enum.Parse(typeof(B), m[8]), to1 = (B)Enum.Parse(typeof(B), m[9]);
                    float turn = KMath.Ang(d.pivot[(int)to1] - d.pivot[(int)to0]) - KMath.Ang(G(d1) - G(d0));
                    Vector2 ga = G(at), pa = d.pivot[(int)bone];
                    return x => pa + KMath.Rot(G(x) - ga, turn);
                }
                case "blade":
                {
                    Vector2 g = new(N(m[1]), N(m[2])), t = new(N(m[3]), N(m[4]));
                    float len = N(m[5]);
                    Vector2 gg = G(g), axis = G(t) - gg;
                    float scale = len / axis.magnitude, turn = d.bladeBindAngle - KMath.Ang(axis);
                    Vector2 pivot = d.pivot[(int)B.Sword];
                    return x => pivot + KMath.Rot((G(x) - gg) * scale, turn);
                }
                default:
                    throw new ArgumentException(who + ": unknown map " + m[0]);
            }
        }
    }
}
