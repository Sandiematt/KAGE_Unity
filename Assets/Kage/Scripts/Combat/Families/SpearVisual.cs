using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the spear family's weapon on the Sword bone in place of the part set's katana (SAN-78): a long shaft
    /// with the lead hand a little ahead of its middle, a butt spike, a head, and the look of the weapon: Mizuchi's
    /// wave-banded lacquer shaft and kris head in a dragon collar, the Order's leaf-headed spear, Shiden's red-tasselled
    /// yari, or a plain staff (Kuroe's temple staff, the sage staff) with no head. Axis frame: x along the shaft from
    /// the lead hand's grip, so the head ends at the family's blade length.
    /// </summary>
    public sealed class SpearVisual : IWeaponVisual
    {
        enum Head { Kris, Leaf, Yari, None }

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
                case "vanguard_yari":
                    return new Look { head = Head.Yari, shaft = KMath.Hex("#241b1b"), band = KMath.Hex("#8c2f26"), glow = KMath.Hex("#ff6a52"), fitting = KMath.Hex("#6c6a70"), tassel = KMath.Hex("#d9402e") };
                case "temple_staff":    // Kuroe's (SAN-103, the JS kuroe/weapon.js): dark wood, gold bands, iron caps, a black ink-brush tassel
                    return new Look { head = Head.None, shaft = KMath.Hex("#3a2c20"), band = KMath.Hex("#c9a45c"), glow = KMath.Hex("#ecd9a8"), fitting = KMath.Hex("#4a4f57"), tassel = KMath.Hex("#0b0a0a") };
                case "sage_staff":
                    return new Look { head = Head.None, shaft = KMath.Hex("#5a4636"), band = KMath.Hex("#7fd0c8"), glow = KMath.Hex("#bff3ee"), fitting = KMath.Hex("#2e5f5b"), tassel = KMath.Hex("#7fd0c8") };
                default:    // Mizuchi: the water dragon
                    return new Look { head = Head.Kris, shaft = KMath.Hex("#121722"), band = KMath.Hex("#1d5d8f"), glow = KMath.Hex("#8fd4ff"), fitting = KMath.Hex("#9aa7b8"), tassel = KMath.Hex("#2a8be0") };
            }
        }

        const float Butt = -0.55f;          // the shaft's butt, behind the lead hand
        const float HeadStart = 1.12f;      // where the head's collar begins; the head ends at 0.08 + BladeLength

        static Vector2[] Rect(float x0, float x1, float h) => new[] { new Vector2(x0, -h), new Vector2(x1, -h), new Vector2(x1, h), new Vector2(x0, h) };

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetBaseSwordHidden(true);
            rig.SetSwordAlwaysFront(true);
            var look = LookOf(weaponId);
            float tip = 0.08f + SpearMoves.BladeLength;
            bool staff = look.head == Head.None;
            float shaftEnd = staff ? tip : HeadStart + 0.04f;

            // the shaft, with a butt spike (a staff has capped ends instead)
            rig.AddFamilyPart("Spear_Shaft", Rect(Butt, shaftEnd, staff ? 0.017f : 0.014f), look.shaft, PartRole.Body, 0, true);
            if (staff)
            {
                rig.AddFamilyPart("Spear_CapButt", Rect(Butt - 0.02f, Butt + 0.07f, 0.021f), look.fitting, PartRole.Metal, 1);
                rig.AddFamilyPart("Spear_CapTip", Rect(tip - 0.07f, tip + 0.02f, 0.021f), look.fitting, PartRole.Metal, 1);
            }
            else
            {
                rig.AddFamilyPart("Spear_Butt", new[] { new Vector2(Butt, -0.017f), new Vector2(Butt - 0.07f, 0f), new Vector2(Butt, 0.017f) }, look.fitting, PartRole.Metal, 1);
            }

            // bands (with a thin lit stripe) up the shaft
            foreach (float x in new[] { -0.4f, -0.14f, 0.12f, 0.38f, 0.64f, 0.9f })
            {
                float top = staff && x > tip - 0.2f ? tip - 0.2f : x;
                rig.AddFamilyPart("Spear_Band", Rect(top - 0.012f, top + 0.012f, staff ? 0.021f : 0.019f), look.band, PartRole.TrimDeep, 1);
                rig.AddFamilyPart("Spear_BandLit", Rect(top - 0.004f, top + 0.004f, staff ? 0.021f : 0.019f), look.glow, PartRole.Trim, 1, false, 1.5f);
            }

            if (look.head != Head.None) AddHead(rig, look, tip);

            // the tassel under the head, trailing back along the shaft
            float tx = look.head == Head.None ? tip - 0.1f : HeadStart - 0.02f;
            rig.AddFamilyPart("Spear_Tassel", new[]
            {
                new Vector2(tx, -0.012f), new Vector2(tx - 0.2f, -0.05f), new Vector2(tx - 0.26f, -0.06f), new Vector2(tx - 0.2f, -0.02f), new Vector2(tx, 0.0f),
            }, look.tassel, PartRole.Trim, 2);
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
