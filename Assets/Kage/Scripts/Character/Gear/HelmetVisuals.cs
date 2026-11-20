using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>What Kage's own head hair does under a helmet. The JS draws it under a band and the open oni mask, nothing under a hood; SAN-72 rework: also nothing under the closed kabuto, where the port's larger hair poked through the visor and the neck guard.</summary>
    public enum HairUnderHelmet
    {
        /// <summary>Covered: a hood, a cap or a closed kabuto hides it, a plain bare head shows below.</summary>
        Hidden,
        /// <summary>Kept and still moving under a band or the open oni mask.</summary>
        Kept,
        /// <summary>Only the hair that hangs below the brim shows (the straw hat).</summary>
        BelowBrim,
    }

    /// <summary>
    /// Helmet visuals, the gear layer on Kage's part set (SAN-72, ADR-0005). A helmet is drawn in code from its JS look, in
    /// parts on the head bone over his hair and under his eye, so his body, scarf, skeleton and hurt circles never change.
    /// A helmet with no drawing (the starter Shiro Maki, whose look is his bare hair and scarf, and every later-act helmet)
    /// leaves Kage exactly as he is. <see cref="KagePartSet"/> calls <see cref="Apply"/>.
    /// </summary>
    public static class HelmetVisuals
    {
        sealed class Look
        {
            public string name;                 // the JS look key
            public HairUnderHelmet hair;
            public Action<HelmetArt> draw;
        }

        // keyed by the JS helmet id. The 13 helmets of the shop and Act I that have a drawing.
        static readonly Dictionary<string, Look> Looks = new()
        {
            ["kuro_zukin"] = new Look { name = "ronin_hood", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.RoninHood },
            ["sugegasa"] = new Look { name = "straw_kasa", hair = HairUnderHelmet.BelowBrim, draw = HelmetLooks.StrawKasa },
            ["hachigane"] = new Look { name = "storm_band", hair = HairUnderHelmet.Kept, draw = HelmetLooks.StormBand },
            ["kasumi_men"] = new Look { name = "veil", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.Veil },
            ["tetsu_kabuto"] = new Look { name = "iron_kabuto", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.IronKabuto },
            ["oni_menpo"] = new Look { name = "oni_mask", hair = HairUnderHelmet.Kept, draw = HelmetLooks.OniMask },
            ["hoshi_kabuto"] = new Look { name = "sentinel_helm", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.SentinelHelm },
            ["shika_kabuto"] = new Look { name = "sage_antlers", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.SageAntlers },
            ["utsuro_kabuto"] = new Look { name = "hollow_crest", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.HollowCrest },
            ["shikko_zukin"] = new Look { name = "executioner", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.Executioner },
            ["raijin_kabuto"] = new Look { name = "raijin_kabuto", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.RaijinKabuto },
            ["sumi_eboshi"] = new Look { name = "sumi_eboshi", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.SumiEboshi },
            ["hisame_zukin"] = new Look { name = "rain_hood", hair = HairUnderHelmet.Hidden, draw = HelmetLooks.RainHood },
        };

        /// <summary>True when this JS helmet id has a drawing.</summary>
        public static bool Has(string helmetId) => helmetId != null && Looks.ContainsKey(helmetId);

        /// <summary>The ids of every helmet with a drawing.</summary>
        public static IEnumerable<string> Ids => Looks.Keys;

        /// <summary>What happens to Kage's hair under this helmet; <see cref="HairUnderHelmet.Kept"/> for a helmet with no drawing.</summary>
        public static HairUnderHelmet HairUnder(string helmetId) => Has(helmetId) ? Looks[helmetId].hair : HairUnderHelmet.Kept;

        /// <summary>
        /// Layers the helmet on Kage's rig: its parts on the head bone and, for a hood or cap, a bare head instead of the hair.
        /// Returns false and leaves the rig exactly as it was when the helmet has no drawing (null, the starter, a later-act
        /// helmet, an unknown id).
        /// </summary>
        public static bool Apply(RigDef rig, string helmetId)
        {
            if (!Has(helmetId)) return false;
            var look = Looks[helmetId];
            switch (look.hair)
            {
                case HairUnderHelmet.Hidden: HideHair(rig); break;
                case HairUnderHelmet.BelowBrim: KeepHairBelow(rig, HelmetArt.At(0f, -0.36f).y); break;
            }
            var art = new HelmetArt(rig);
            look.draw(art);
            art.Commit(look.name);
            return true;
        }

        // ---------------------------------------------------------------- hair

        /// <summary>The hair (and with it the skull's drawing) is replaced by a plain black head, as under any hood.</summary>
        static void HideHair(RigDef rig)
        {
            int i = rig.parts.FindIndex(p => p.soft == SoftKind.Hair);
            if (i < 0) return;
            var hair = rig.parts[i];
            Vector2 P(float x, float y) => KageModel.P(x, y);
            rig.parts[i] = new PartDef
            {
                name = "Head", bone = B.Head, order = hair.order, color = Palette.Body, role = PartRole.Body, rim = true,
                pts = new[]
                {
                    P(813, 146), P(805, 160), P(806, 178), P(799, 191), P(806, 197), P(809, 205), P(818, 214), P(832, 223), P(852, 225),
                    P(872, 216), P(890, 198), P(899, 172), P(896, 146), P(882, 126), P(858, 116), P(833, 121),
                },
            };
        }

        /// <summary>Keeps only the part of the hair at or below <paramref name="yMax"/> (bind space, y up): the strands that hang under a brim.</summary>
        static void KeepHairBelow(RigDef rig, float yMax)
        {
            var hair = rig.parts.Find(p => p.soft == SoftKind.Hair);
            if (hair == null) return;
            var kept = new List<(Vector2[] pts, Color col)>();
            foreach (var (pts, col) in hair.polys)
            {
                var c = ClipBelow(pts, yMax);
                if (c.Length >= 3) kept.Add((c, col));
            }
            rig.parts[rig.parts.IndexOf(hair)] = new PartDef
            {
                name = hair.name, bone = hair.bone, order = hair.order, role = hair.role, polys = kept, soft = hair.soft, rim = false,
            };
        }

        /// <summary>Sutherland-Hodgman against the half plane y &lt;= yMax.</summary>
        static Vector2[] ClipBelow(Vector2[] poly, float yMax)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                bool ia = a.y <= yMax, ib = b.y <= yMax;
                if (ia) o.Add(a);
                if (ia != ib)
                {
                    float t = (yMax - a.y) / (b.y - a.y);
                    o.Add(Vector2.Lerp(a, b, t));
                }
            }
            return o.ToArray();
        }
    }
}
