using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Dresses Kage's articulated rig with the equipped armour. The shop sets also dye his base garb.
    /// </summary>
    public static class ArmorVisuals
    {
        static readonly Dictionary<string, ArmorTheme> ShopDyes = new()
        {
            ["ronin_yoroi"] = ArmorLooks.Ronin,
            ["yami_yoroi"] = ArmorLooks.Yami,
            ["shinsen_yoroi"] = ArmorLooks.Shinsen,
            ["raiko_yoroi"] = ArmorLooks.Raiko,
            ["kagehira_yoroi"] = ArmorLooks.Kagehira,
            ["yurei_yoroi"] = ArmorLooks.Yurei,
            ["mugen_yoroi"] = ArmorLooks.Mugen,
        };

        static readonly HashSet<string> CoveredByStructuredArmor = new()
        {
            "Lapel", "Collar",
            "Belt", "Belt_Line", "Buckle", "Buckle_In", "Belt_Loop",
            "ArmN_Cuff", "ArmN_CuffLine", "ArmN_Wrist",
            "LegN_KneeStrap", "LegN_KneeBuckle", "LegN_AnkleStrap",
            "LegF_KneeStrap", "LegF_AnkleStrap",
        };

        public static bool Has(string armorId) => ArmorDesigns.Has(armorId);
        public static IEnumerable<string> Ids => ArmorDesigns.Ids;

        public static bool Apply(RigDef rig, string armorId, Color trim, Color trimDeep)
        {
            var design = ArmorDesigns.Get(armorId);
            if (design == null) return false;

            if (ShopDyes.TryGetValue(armorId, out var theme))
            {
                ArmorLooks.Retone(rig, theme, trim, trimDeep);
                if (design.tier == ArmorTier.Common)
                {
                    var coatArt = new ArmorArt(rig, design.look);
                    ArmorLooks.DressCoatAndStrands(coatArt, theme);
                    coatArt.Commit();
                }
                else ArmorLooks.DressStrandsOnly(rig, theme);
            }

            rig.parts.RemoveAll(p => CoveredByStructuredArmor.Contains(p.name));
            TrimBaseCoatForArmor(rig, design);
            AddStructuredPieces(rig, design);
            return true;
        }

        /// <summary>
        /// Trims Kage's base Coat / Coat_Side / Coat_Back polygons underneath structured armors so baggy under-coat cloth
        /// never pokes out behind split haori tails, kusazuri tassets, or Legendary warlord capes. Keeps all three parts
        /// intact with their exact bone, order, SoftKind.Coat, clothLayer and retoned palette colors.
        /// </summary>
        static void TrimBaseCoatForArmor(RigDef rig, ArmorDesign design)
        {
            Color rim = rig.rimColor;
            switch (design.tier)
            {
                case ArmorTier.Common:
                    // Ronin keeps his ragged side/back coat; only trim the front coat top seam neatly under Kusazuri
                    ReplaceCoatPanel(rig, "Coat", rim, new float[]
                    {
                        806, 418, 886, 418, 888, 554, 866, 668, 848, 736, 816, 742, 782, 734, 792, 646, 800, 530
                    });
                    break;
                case ArmorTier.Rare:
                    // Short tactical / shrine haori tails cover upper hips; lower under-coat emerges cleanly below hem
                    ReplaceCoatPanel(rig, "Coat", rim, new float[]
                    {
                        806, 424, 882, 424, 884, 548, 868, 648, 848, 732, 816, 738, 788, 730, 796, 640, 802, 530
                    });
                    ReplaceCoatPanel(rig, "Coat_Side", rim, new float[]
                    {
                        886, 420, 948, 420, 970, 512, 994, 620, 1016, 724, 976, 734, 932, 732, 908, 624, 892, 516
                    });
                    ReplaceCoatPanel(rig, "Coat_Back", rim.Mul(0.75f), new float[]
                    {
                        884, 420, 950, 420, 978, 518, 1008, 626, 1034, 726, 990, 738, 936, 736, 910, 628, 892, 518
                    });
                    break;
                case ArmorTier.Epic:
                case ArmorTier.Trophy:
                    // Mid-length structured battle haori & surcoats: slim base coat into a crisp inner under-hakama
                    ReplaceCoatPanel(rig, "Coat", rim, new float[]
                    {
                        804, 444, 878, 444, 880, 548, 866, 640, 846, 722, 816, 728, 792, 720, 800, 632, 804, 536
                    });
                    ReplaceCoatPanel(rig, "Coat_Side", rim, new float[]
                    {
                        890, 436, 946, 436, 960, 516, 976, 618, 988, 720, 956, 726, 924, 722, 906, 622, 896, 526
                    });
                    ReplaceCoatPanel(rig, "Coat_Back", rim.Mul(0.75f), new float[]
                    {
                        890, 436, 948, 436, 966, 520, 986, 622, 1000, 724, 966, 730, 926, 726, 908, 626, 896, 528
                    });
                    break;
                case ArmorTier.Legendary:
                    // Ornate long 3-panel warlord coat + 2-layer cape: slim base coat tightly around the inner legs
                    ReplaceCoatPanel(rig, "Coat", rim, new float[]
                    {
                        808, 462, 874, 462, 876, 556, 864, 640, 846, 714, 816, 718, 796, 712, 802, 630, 806, 544
                    });
                    ReplaceCoatPanel(rig, "Coat_Side", rim, new float[]
                    {
                        894, 454, 944, 454, 956, 522, 968, 616, 976, 712, 948, 718, 922, 714, 908, 622, 898, 530
                    });
                    ReplaceCoatPanel(rig, "Coat_Back", rim.Mul(0.75f), new float[]
                    {
                        894, 454, 946, 454, 962, 526, 976, 620, 988, 716, 958, 722, 924, 718, 908, 626, 898, 532
                    });
                    break;
            }
        }

        static void ReplaceCoatPanel(RigDef rig, string partName, Color rimCol, float[] outlinePx)
        {
            var part = rig.parts.Find(p => p.name == partName);
            if (part == null || part.polys == null || part.polys.Count < 2) return;
            // Preserve the retoned base coat fill color (index 1 in KageModel.BuildCoat) and hem accent (last poly)
            Color fillCol = part.polys[1].col;
            Color accentCol = part.polys[part.polys.Count - 1].col;
            var poly = ArmorArt.Dense(Pts(outlinePx));
            var rimPts = Shift(MeshKit.Grow(poly, 0.0042f), new Vector2(-0.0055f, 0.0055f));
            part.polys = new List<(Vector2[] pts, Color col)>
            {
                (rimPts, rimCol),
                (poly, fillCol),
            };
            // Subtle lower hem trim band matching the armor palette so the trimmed under-tunic looks intentional
            int n = outlinePx.Length / 2;
            if (n >= 7)
            {
                float x0 = outlinePx[8], y0 = outlinePx[9] - 5f;
                float x1 = outlinePx[10], y1 = outlinePx[11] - 5f;
                float x2 = outlinePx[12], y2 = outlinePx[13] - 5f;
                var hem = ArmorArt.Dense(ToBind(ArmorArt.Strip(ArmorArt.Pts(x0, y0, x1, y1, x2, y2), 6.5f, 6.5f)));
                if (hem.Length >= 3) part.polys.Add((hem, accentCol));
            }
        }

        static Vector2[] ToBind(IList<Vector2> px)
        {
            var r = new Vector2[px.Count];
            for (int i = 0; i < px.Count; i++) r[i] = KageModel.P(px[i].x, px[i].y);
            return r;
        }

        // ---------------------------------------------------------------- structured cel/cloth pieces (zero Z-fighting)

        /// <summary>
        /// Builds all structured pieces of an armor design onto <paramref name="rig"/>:
        /// - Rigid non-glow Cel pieces sharing the same (bone, order, swordLayer) are merged in authoring order into a
        ///   single multi-polygon mesh (eliminating Z-fighting on stacked pauldrons &amp; rayed halos while keeping rims crisp).
        /// - Any remaining pieces sharing the same (bone, order, swordLayer) (e.g. merged rigid plate + outer glow + hot core)
        ///   receive strictly ascending <see cref="PartDef.underSlot"/> values (-2, -1, 0) so no two renderers share a sortingOrder.
        /// </summary>
        static void AddStructuredPieces(RigDef rig, ArmorDesign design)
        {
            var built = new List<PartDef>();
            var mergedCel = new Dictionary<(B bone, int order, bool sword), PartDef>();
            var mergedCount = new Dictionary<(B bone, int order, bool sword), int>();

            foreach (var p in design.pieces)
            {
                if (p.kind == PieceKind.Cel && p.glow <= 1.01f)
                {
                    var key = (p.bone, p.order, p.swordLayer);
                    mergedCount[key] = mergedCount.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }

            foreach (var p in design.pieces)
            {
                if (p.kind == PieceKind.Cel && p.glow <= 1.01f)
                {
                    var key = (p.bone, p.order, p.swordLayer);
                    if (mergedCount[key] > 1)
                    {
                        if (!mergedCel.TryGetValue(key, out var group))
                        {
                            group = new PartDef
                            {
                                name = $"Armor_{design.look}_{p.bone}_{p.order}",
                                bone = p.bone,
                                order = p.order,
                                role = PartRole.Trim,
                                rim = false,
                                glow = 1f,
                                swordLayer = p.swordLayer,
                                polys = new List<(Vector2[] pts, Color col)>(),
                            };
                            mergedCel[key] = group;
                            built.Add(group);
                        }
                        AppendCelPolys(group.polys, p, design.palette, bakeRim: true);
                        continue;
                    }
                }
                built.Add(Build(p, design.look, design.palette));
            }

            // Assign strictly distinct underSlots (-2, -1, 0) to any parts sharing (bone, order, swordLayer)
            var bySlot = new Dictionary<(B bone, int order, bool sword), List<PartDef>>();
            foreach (var part in built)
            {
                var key = (part.bone, part.order, part.swordLayer);
                if (!bySlot.TryGetValue(key, out var list)) bySlot[key] = list = new List<PartDef>();
                list.Add(part);
            }
            foreach (var list in bySlot.Values)
            {
                if (list.Count <= 1) continue;
                int n = list.Count;
                for (int i = 0; i < n; i++)
                    list[i].underSlot = Mathf.Clamp(i - (n - 1), -2, 0);
            }

            rig.parts.AddRange(built);
        }

        static void AppendCelPolys(List<(Vector2[] pts, Color col)> dst, ArmorPiece p, ArmorPalette pal, bool bakeRim)
        {
            var shape = Pts(p.outline);
            if (bakeRim && p.rim)
                dst.Add((Shift(MeshKit.Grow(shape, 0.0045f), new Vector2(-0.0055f, 0.0055f)), pal.rim));
            dst.AddRange(OpponentKit.Cel(shape, pal.Tone(p.tone), p.sd, p.hd, p.seam));
            dst.AddRange(Details(p, pal));
        }

        static Vector2[] Pts(float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = KageModel.P(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, c.a * a);

        static List<(Vector2[] pts, Color col)> Details(ArmorPiece p, ArmorPalette pal)
        {
            var r = new List<(Vector2[] pts, Color col)>();
            if (p.detail == null) return r;
            foreach (var d in p.detail)
                r.Add((Pts(d.pts), WithAlpha(pal.Colour(d.tone, d.which), d.alpha)));
            return r;
        }

        /// <summary>Builds one structured armor piece as a rig part (cel-shaded, flat glow, or spring-chain cloth).</summary>
        public static PartDef Build(ArmorPiece p, string look, ArmorPalette pal)
        {
            var shape = Pts(p.outline);
            var part = new PartDef
            {
                name = $"Armor_{look}_{p.name}",
                bone = p.bone,
                order = p.order,
                role = PartRole.Trim,
                rim = false,
                glow = p.glow,
                swordLayer = p.swordLayer,
            };
            switch (p.kind)
            {
                case PieceKind.Flat:
                    part.polys = new List<(Vector2[] pts, Color col)> { (shape, WithAlpha(pal.Colour(p.tone, p.which), p.alpha)) };
                    break;
                case PieceKind.Cel:
                    part.polys = new List<(Vector2[] pts, Color col)>();
                    AppendCelPolys(part.polys, p, pal, bakeRim: false);
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
    }
}
