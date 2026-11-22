using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>What Kage's own head hair does under a helmet (SAN-72).</summary>
    public enum HairUnderHelmet
    {
        /// <summary>Covered: a hood, a cap, a kabuto or a hat with a head cloth hides it, a plain black head shows below.</summary>
        Hidden,
        /// <summary>Kept and still moving: only a brow band (Hachigane) leaves his spiky hair out.</summary>
        Kept,
    }

    /// <summary>
    /// Helmet visuals, the gear layer on Kage's part set (SAN-72, ADR-0005). Every Act I helmet but the starter has a design in one of
    /// the five rarity tiers (<see cref="HelmetDesigns"/>, authored in <c>Tools/helmet_art</c>): cel-shaded pieces on the head bone over
    /// his hair and under his eye, soft pieces (tails, ribbons, drapes, tassels) on spring chains that trail, sag and take the wind,
    /// and a neck wrap in the tier's colour on the chest over the front of his scarf wrap. His body, scarf, skeleton and hurt circles
    /// never change. A helmet with no design (the starter Shiro Maki, whose look is his bare hair and crimson scarf, and every
    /// later-act helmet) leaves Kage exactly as he is. <see cref="KagePartSet"/> calls <see cref="Apply"/>.
    /// </summary>
    public static class HelmetVisuals
    {
        /// <summary>Draw order of the eye; no helmet piece at or above it may cover the eye.</summary>
        public const int EyeOrder = 96;

        /// <summary>True when this JS helmet id has a design.</summary>
        public static bool Has(string helmetId) => HelmetDesigns.Has(helmetId);

        /// <summary>The ids of every helmet with a design.</summary>
        public static IEnumerable<string> Ids
        {
            get { foreach (var d in HelmetDesigns.All) yield return d.id; }
        }

        /// <summary>What happens to Kage's hair under this helmet; <see cref="HairUnderHelmet.Kept"/> for a helmet with no design.</summary>
        public static HairUnderHelmet HairUnder(string helmetId) => Has(helmetId) ? HelmetDesigns.Get(helmetId).hair : HairUnderHelmet.Kept;

        /// <summary>
        /// Layers the helmet on Kage's rig: a bare head instead of the hair when the design hides it, then the design's pieces as parts
        /// named <c>Helmet_&lt;look&gt;_&lt;piece&gt;</c>. Returns false and leaves the rig exactly as it was when the helmet has no design
        /// (null, the starter, a later-act helmet, an unknown id).
        /// </summary>
        public static bool Apply(RigDef rig, string helmetId)
        {
            var design = HelmetDesigns.Get(helmetId);
            if (design == null) return false;
            if (design.hair == HairUnderHelmet.Hidden) HideHair(rig);
            var pal = HelmetTiers.Palette(design.tier);
            foreach (var piece in design.pieces) rig.parts.Add(Build(piece, design.look, pal));
            return true;
        }

        // ---------------------------------------------------------------- pieces

        static Vector2[] Pts(float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = KageModel.P(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, c.a * a);

        static List<(Vector2[] pts, Color col)> Details(HelmetPiece p, HelmetPalette pal)
        {
            var r = new List<(Vector2[] pts, Color col)>();
            if (p.detail == null) return r;
            foreach (var d in p.detail) r.Add((Pts(d.pts), WithAlpha(pal.Colour(d.tone, d.which), d.alpha)));
            return r;
        }

        /// <summary>One piece as a rig part (see <see cref="OpponentKit.AddCel"/> and <see cref="OpponentKit.AddCloth"/>, here in the tier's rim colour).</summary>
        public static PartDef Build(HelmetPiece p, string look, HelmetPalette pal)
        {
            var shape = Pts(p.outline);
            var part = new PartDef { name = $"Helmet_{look}_{p.name}", bone = p.bone, order = p.order, role = PartRole.Trim, rim = false, glow = p.glow };
            switch (p.kind)
            {
                case PieceKind.Flat:
                    part.pts = shape;
                    part.color = WithAlpha(pal.Colour(p.tone, p.which), p.alpha);
                    break;
                case PieceKind.Cel:
                    part.polys = OpponentKit.Cel(shape, pal.Tone(p.tone), p.sd, p.hd, p.seam);
                    part.polys.AddRange(Details(p, pal));
                    if (p.rim) { part.rimPts = shape; part.rimColor = pal.rim; }
                    break;
                case PieceKind.Cloth:
                    part.polys = new List<(Vector2[] pts, Color col)>();
                    if (p.rim) part.polys.Add((Shift(MeshKit.Grow(shape, 0.0045f), new Vector2(-0.0055f, 0.0055f)), pal.rim));
                    part.polys.AddRange(OpponentKit.Cel(shape, pal.Tone(p.tone), p.sd, p.hd, p.seam));
                    part.polys.AddRange(Details(p, pal));
                    part.soft = SoftKind.Chain;
                    part.chains = new List<Vector2[]>();
                    foreach (var c in p.chains) part.chains.Add(Pts(c));
                    part.chainFeel = SoftPresets.Get(p.feel);
                    break;
            }
            return part;
        }

        static Vector2[] Shift(Vector2[] pts, Vector2 by)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = pts[i] + by;
            return r;
        }

        // ---------------------------------------------------------------- hair

        /// <summary>The outline of Kage's bare head (reference px), drawn instead of his hair under a hiding helmet.</summary>
        public static readonly float[] BareHead =
        {
            813, 146, 805, 160, 806, 178, 799, 191, 806, 197, 809, 205, 818, 214, 832, 223, 852, 225,
            872, 216, 890, 198, 899, 172, 896, 146, 882, 126, 858, 116, 833, 121,
        };

        /// <summary>The hair (and with it the skull's drawing) is replaced by a plain black head, as under any hood.</summary>
        static void HideHair(RigDef rig)
        {
            int i = rig.parts.FindIndex(p => p.soft == SoftKind.Hair);
            if (i < 0) return;
            var hair = rig.parts[i];
            rig.parts[i] = new PartDef
            {
                name = "Head", bone = B.Head, order = hair.order, color = Palette.Body, role = PartRole.Body, rim = true, pts = Pts(BareHead),
            };
        }
    }
}
