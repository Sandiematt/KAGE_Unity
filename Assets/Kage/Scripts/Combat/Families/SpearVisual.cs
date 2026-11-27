using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the spear family's weapon on the Sword bone in place of the part set's katana (SAN-78): a long shaft
    /// with the lead hand a little ahead of its middle, a butt spike, a head, and the look of the weapon: Mizuchi's
    /// wave-banded lacquer shaft and kris head in a dragon collar, the Order's leaf-headed spear, Shiden's violet-tasselled
    /// yari, Kuroe's giant calligraphy ink brush-pen (Ichimonji-style fude / ink pen), or a plain staff (the sage staff)
    /// with no head. Axis frame: x along the shaft from the lead hand's grip, so the head ends at the family's blade length.
    /// </summary>
    public sealed class SpearVisual : IWeaponVisual
    {
        enum Head { Kris, Leaf, Yari, InkBrush, None }

        struct Look
        {
            public Head head;
            public Color shaft, band, glow, fitting, tassel;
        }

        static Look LookOf(string weaponId)
        {
            switch (weaponId)
            {
                case "sentinel_spear":
                    return new Look { head = Head.Leaf, shaft = KMath.Hex("#2b2a2e"), band = KMath.Hex("#c9a45c"), glow = KMath.Hex("#e8d29a"), fitting = KMath.Hex("#8f8a82"), tassel = KMath.Hex("#8c3b3b") };
                case "vanguard_yari":   // Shiden's (SAN-106, the JS shiden/weapon.js): a black lacquered shaft banded violet, iron fittings, a violet tassel
                    return new Look { head = Head.Yari, shaft = KMath.Hex("#111114"), band = KMath.Hex("#714fa3"), glow = KMath.Hex("#e4d6ff"), fitting = KMath.Hex("#6c7076"), tassel = KMath.Hex("#b07cff") };
                case "temple_staff":    // Kuroe's Ink Censor brush-pen: lacquered ebony barrel, censor-gold bands, brass pen ferrule, sumi-soaked calligraphy brush nib
                    return new Look { head = Head.InkBrush, shaft = KMath.Hex("#1E1714"), band = KMath.Hex("#C9A45C"), glow = KMath.Hex("#ECE5D3"), fitting = KMath.Hex("#3A3842"), tassel = KMath.Hex("#0B0A0D") };
                case "sage_staff":
                    return new Look { head = Head.None, shaft = KMath.Hex("#5a4636"), band = KMath.Hex("#7fd0c8"), glow = KMath.Hex("#bff3ee"), fitting = KMath.Hex("#2e5f5b"), tassel = KMath.Hex("#7fd0c8") };
                default:    // Mizuchi: the water dragon
                    return new Look { head = Head.Kris, shaft = KMath.Hex("#121722"), band = KMath.Hex("#1d5d8f"), glow = KMath.Hex("#8fd4ff"), fitting = KMath.Hex("#9aa7b8"), tassel = KMath.Hex("#2a8be0") };
            }
        }

        const float Butt = -0.70f;          // extended shaft butt behind lead hand so sliding-grip thrusts keep both hands on the shaft
        const float HeadStart = 1.12f;      // where the head's collar begins; the head ends at 0.08 + BladeLength

        static Vector2[] Rect(float x0, float x1, float h) => new[] { new Vector2(x0, -h), new Vector2(x1, -h), new Vector2(x1, h), new Vector2(x0, h) };

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetBaseSwordHidden(true);
            rig.SetSwordAlwaysFront(true);
            if (rig.def.name == "SHIDEN" && weaponId == "vanguard_yari")
            {
                SubBossBlades.LieutenantKatana(rig);
                return;
            }
            var look = LookOf(weaponId);
            float tip = 0.08f + SpearMoves.BladeLength;
            bool staff = look.head == Head.None;
            bool brush = look.head == Head.InkBrush;
            float shaftHalf = brush ? 0.018f : staff ? 0.017f : 0.014f;
            float shaftEnd = staff ? tip : brush ? HeadStart - 0.03f : HeadStart + 0.04f;

            // the shaft / pen barrel, with a butt spike, capped staff ends, or a weighted calligraphy pen finial
            rig.AddFamilyPart("Spear_Shaft", Rect(Butt, shaftEnd, shaftHalf), look.shaft, PartRole.Body, 0, true);
            if (staff)
            {
                rig.AddFamilyPart("Spear_CapButt", Rect(Butt - 0.02f, Butt + 0.07f, 0.021f), look.fitting, PartRole.Metal, 1);
                rig.AddFamilyPart("Spear_CapTip", Rect(tip - 0.07f, tip + 0.02f, 0.021f), look.fitting, PartRole.Metal, 1);
            }
            else if (brush)
            {
                // ornate calligrapher's end-cap and hanging cord loop at the back of the ink pen
                rig.AddFamilyPart("Spear_PenButt", Rect(Butt - 0.035f, Butt + 0.055f, 0.023f), look.fitting, PartRole.Metal, 1, true);
                rig.AddFamilyPart("Spear_PenButtGold", Rect(Butt + 0.04f, Butt + 0.058f, 0.025f), look.band, PartRole.Trim, 2);
                rig.AddFamilyPart("Spear_PenLoop", new[]
                {
                    new Vector2(Butt - 0.035f, -0.011f), new Vector2(Butt - 0.085f, -0.024f),
                    new Vector2(Butt - 0.098f, 0f), new Vector2(Butt - 0.085f, 0.024f), new Vector2(Butt - 0.035f, 0.011f),
                }, look.band, PartRole.Trim, 1);
                // a pale censor's paper tally wrapped below the ferrule with two black redaction bars
                rig.AddFamilyPart("Spear_PenSlip", Rect(0.76f, 0.92f, 0.0205f), look.glow, PartRole.Trim, 1);
                rig.AddFamilyPart("Spear_PenSlipBar0", Rect(0.79f, 0.825f, 0.021f), look.tassel, PartRole.Detail, 2);
                rig.AddFamilyPart("Spear_PenSlipBar1", Rect(0.855f, 0.89f, 0.021f), look.tassel, PartRole.Detail, 2);
            }
            else
            {
                rig.AddFamilyPart("Spear_Butt", new[] { new Vector2(Butt, -0.017f), new Vector2(Butt - 0.07f, 0f), new Vector2(Butt, 0.017f) }, look.fitting, PartRole.Metal, 1);
            }

            // bands (with a thin lit stripe) up the shaft
            foreach (float x in new[] { -0.58f, -0.38f, -0.14f, 0.12f, 0.38f, 0.64f, 0.9f })
            {
                if (brush && x > 0.7f) continue;     // the censor slip sits at 0.76..0.92
                float top = staff && x > tip - 0.2f ? tip - 0.2f : x;
                float bh = brush ? 0.022f : staff ? 0.021f : 0.019f;
                rig.AddFamilyPart("Spear_Band", Rect(top - 0.012f, top + 0.012f, bh), look.band, PartRole.TrimDeep, 1);
                rig.AddFamilyPart("Spear_BandLit", Rect(top - 0.004f, top + 0.004f, bh), look.glow, PartRole.Trim, 1, false, 1.5f);
            }

            if (brush) AddInkBrush(rig, look, tip);
            else if (look.head != Head.None) AddHead(rig, look, tip);

            // the tassel under the head, trailing back along the shaft
            float tx = look.head == Head.None ? tip - 0.1f : brush ? HeadStart - 0.07f : HeadStart - 0.02f;
            rig.AddFamilyPart("Spear_Tassel", new[]
            {
                new Vector2(tx, -0.012f), new Vector2(tx - 0.2f, -0.05f), new Vector2(tx - 0.26f, -0.06f), new Vector2(tx - 0.2f, -0.02f), new Vector2(tx, 0.0f),
            }, look.tassel, PartRole.Trim, 2);
        }

        /// <summary>
        /// Kuroe's giant calligraphy ink brush-pen (Ichimonji-style fude + fountain nib collar): a stepped iron-and-gold ferrule
        /// with a nib breather slit, holding a lush tapered brush head soaked in deep sumi ink that comes to a razor point at <paramref name="tip"/>.
        /// </summary>
        static void AddInkBrush(FighterRig rig, Look look, float tip)
        {
            float c0 = HeadStart - 0.06f, c1 = HeadStart + 0.06f;
            // stepped ferrule (kubi) + gold collar rings + fountain pen nib slit
            rig.AddFamilyPart("Spear_PenFerrule", new[]
            {
                new Vector2(c0, -0.023f), new Vector2(c1, -0.034f), new Vector2(c1, 0.034f), new Vector2(c0, 0.023f),
            }, look.fitting, PartRole.Metal, 2, true);
            rig.AddFamilyPart("Spear_PenCollarRear", Rect(c0 - 0.01f, c0 + 0.016f, 0.026f), look.band, PartRole.Metal, 3);
            rig.AddFamilyPart("Spear_PenCollarFront", Rect(c1 - 0.02f, c1 + 0.008f, 0.036f), look.band, PartRole.Metal, 3);
            // golden fountain-pen nib shoulders embracing the base of the brush bristles, split by an ink channel
            float n1 = c1 + 0.095f;
            rig.AddFamilyPart("Spear_PenNib", new[]
            {
                new Vector2(c1, -0.031f), new Vector2(n1, -0.018f), new Vector2(n1 + 0.03f, 0f), new Vector2(n1, 0.018f), new Vector2(c1, 0.031f),
            }, look.band, PartRole.Metal, 3);
            rig.AddFamilyPart("Spear_PenSlit", Rect(c1 + 0.005f, n1 + 0.02f, 0.0035f), look.tassel, PartRole.Detail, 4);

            // lush calligraphy brush head (hosaki): swells into a full belly, then tapers to a sharp inked writing point at tip
            static float BrushHalfWidth(float u)
            {
                float belly = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI));   // clamp: Sin(PI) in float is -8.7e-8, so Pow would NaN
                return Mathf.Lerp(0.034f, 0f, Mathf.Pow(u, 1.35f)) + 0.079f * Mathf.Pow(belly, 0.85f) * (1f - u * 0.45f);
            }

            float b0 = c1 - 0.005f;
            var spine = new List<Vector2>();
            var edge = new List<Vector2>();
            const int n = 16;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(b0, tip, u);
                float w = BrushHalfWidth(u);
                float cy = 0.024f * u * u;          // slight calligrapher's upward flick at the very point
                spine.Add(new Vector2(x, cy - w));
                edge.Add(new Vector2(x, cy + w));
            }
            var poly = new List<Vector2>(spine);
            for (int i = edge.Count - 2; i >= 0; i--) poly.Add(edge[i]);
            rig.AddFamilyPart("Spear_Blade", poly.ToArray(), KMath.Hex("#1A1720"), PartRole.Body, 0, true);

            // deep void-black sumi ink saturating the front four-fifths of the brush
            var inkPoly = new List<Vector2>();
            for (int i = 3; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(b0, tip, u);
                float w = BrushHalfWidth(u) * 0.88f;
                float cy = 0.024f * u * u;
                inkPoly.Add(new Vector2(x, cy - w));
            }
            for (int i = n - 1; i >= 3; i--)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(b0, tip, u);
                float w = BrushHalfWidth(u) * 0.88f;
                float cy = 0.024f * u * u;
                inkPoly.Add(new Vector2(x, cy + w));
            }
            rig.AddFamilyPart("Spear_BrushInk", inkPoly.ToArray(), KMath.Hex("#050507"), PartRole.Detail, 1);

            // Split locks of pale goat hair disappear into the saturated tip at different lengths.
            for (int strand = 0; strand < 5; strand++)
            {
                float offset = (strand - 2) * 0.36f;
                float end = 0.46f + 0.09f * ((strand * 3) % 5);
                var lockShape = new List<Vector2>();
                Vector2 Bristle(float u, float width) => new(Mathf.Lerp(b0, tip, u),
                    0.024f * u * u + BrushHalfWidth(u) * (offset + 0.09f * Mathf.Sin(u * 5f) + width));
                for (int i = 0; i <= 10; i++) lockShape.Add(Bristle(end * i / 10f, 0.052f * (1f - i / 10f)));
                for (int i = 9; i >= 0; i--) lockShape.Add(Bristle(end * i / 10f, -0.052f * (1f - i / 10f)));
                rig.AddFamilyPart("Spear_Bristle", lockShape.ToArray(), KMath.Hex("#B3A493"), PartRole.Detail, 2);
            }

            // wet ink sheen curving along the upper edge to a bright paper-ivory tip glint
            var sheen = new List<Vector2>();
            for (int i = 2; i <= 14; i++)
            {
                float u = i / 16f;
                sheen.Add(new Vector2(Mathf.Lerp(b0, tip, u), edge[i].y * 0.62f + 0.003f));
            }
            for (int i = 14; i >= 2; i--)
            {
                float u = i / 16f;
                sheen.Add(new Vector2(Mathf.Lerp(b0, tip, u), edge[i].y * 0.44f - 0.002f));
            }
            rig.AddFamilyPart("Spear_BrushSheen", sheen.ToArray(), KMath.Hex("#3E3A4A"), PartRole.Trim, 2);
            rig.AddFamilyPart("Spear_TipGlint", new[]
            {
                new Vector2(Mathf.Lerp(b0, tip, 0.72f), 0.006f),
                new Vector2(tip, 0.024f),
                new Vector2(Mathf.Lerp(b0, tip, 0.78f), 0.015f),
            }, look.glow, PartRole.Trim, 3, false, 1.6f);
        }

        static void AddHead(FighterRig rig, Look look, float tip)
        {
            // the collar: a jaw (Mizuchi's dragon, with a swept-back horn) or a plain socket
            float c0 = HeadStart - 0.04f, c1 = HeadStart + 0.1f;
            if (look.head == Head.Kris)
                rig.AddFamilyPart("Spear_Collar", new[]
                {
                    new Vector2(c0, -0.03f), new Vector2(c0 + 0.03f, -0.05f), new Vector2(c0 - 0.02f, -0.1f), new Vector2(c0 + 0.08f, -0.058f), new Vector2(c1 + 0.01f, -0.05f),
                    new Vector2(c1 + 0.04f, -0.02f), new Vector2(c1 + 0.01f, 0f), new Vector2(c1 + 0.04f, 0.02f), new Vector2(c1 + 0.01f, 0.05f), new Vector2(c0 + 0.08f, 0.056f), new Vector2(c0, 0.03f),
                }, look.fitting, PartRole.Metal, 2, true);
            else
                rig.AddFamilyPart("Spear_Collar", new[] { new Vector2(c0, -0.03f), new Vector2(c1, -0.026f), new Vector2(c1, 0.026f), new Vector2(c0, 0.03f) }, look.fitting, PartRole.Metal, 2, true);
            if (look.head == Head.Kris)
                rig.AddFamilyPart("Spear_Eye", Rect(c1 - 0.04f, c1 - 0.015f, 0.01f), look.glow, PartRole.Eye, 3, false, 1.8f);

            // the head: a wavy kris, a leaf, or a straight yari blade, ending exactly at the hit line's tip
            float b0 = c1 - 0.01f;
            var spine = new List<Vector2>();
            var edge = new List<Vector2>();
            const int n = 16;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(b0, tip, u);
                float w, cy = 0f;
                switch (look.head)
                {
                    case Head.Kris:
                        w = Mathf.Lerp(0.044f, 0f, Mathf.Pow(u, 1.35f));
                        cy = Mathf.Sin(u * Mathf.PI * 3f) * Mathf.Lerp(0.014f, 0.005f, u);
                        break;
                    case Head.Leaf:
                        w = 0.05f * Mathf.Sin(Mathf.Clamp01(u * 0.9f + 0.1f) * Mathf.PI * 0.88f) * (1f - u * 0.15f);
                        break;
                    default:     // yari: a long straight blade
                        w = Mathf.Lerp(0.03f, 0f, Mathf.Pow(u, 2.2f));
                        break;
                }
                spine.Add(new Vector2(x, cy - w));
                edge.Add(new Vector2(x, cy + w));
            }
            var poly = new List<Vector2>(spine);
            for (int i = edge.Count - 2; i >= 0; i--) poly.Add(edge[i]);
            rig.AddFamilyPart("Spear_Blade", poly.ToArray(), Palette.Steel, PartRole.Metal, 0, true);

            var mid = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float u = 0.05f + i / 12f * 0.85f; mid.Add(new Vector2(Mathf.Lerp(b0, tip, u), spine[Mathf.RoundToInt(u * n)].y * 0.15f + 0.004f)); }
            for (int i = 12; i >= 0; i--) { float u = 0.05f + i / 12f * 0.85f; mid.Add(new Vector2(Mathf.Lerp(b0, tip, u), spine[Mathf.RoundToInt(u * n)].y * 0.15f - 0.004f)); }
            rig.AddFamilyPart("Spear_Glint", mid.ToArray(), Color.white, PartRole.Metal, 1, false, 1.8f);
        }

        public void Detach(FighterRig rig)
        {
            rig.ClearFamilyParts();
            rig.SetSwordAlwaysFront(false);
        }
    }
}
