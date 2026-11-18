using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the heavy tachi on the Sword bone in place of the part set's katana (SAN-81): a long wrapped hilt with a
    /// round guard and a red cord, and a broad odachi blade with a slight curve and a glowing red groove down it
    /// (Raikō no Tachi: "the red groove drinks lightning"). A rig that paints its own tachi (Raikon's part set) keeps it:
    /// nothing is added. Another rig carrying <c>raikon_tachi</c> gets the same blade with a brighter groove.
    /// Axis frame: x along the hilt from the lead hand; the blade runs from the guard (0.08, 0) along the same line
    /// the hit points follow (the katana's blade direction, <see cref="Fighter.BladeTipLocal"/>) and ends at the family's blade length.
    /// </summary>
    public sealed class TachiVisual : IWeaponVisual
    {
        struct Look
        {
            public Color wrap, band, groove, cord;
            public float grooveGlow, grooveWidth;
        }

        static Look LookOf(string weaponId) => weaponId == "raikon_tachi"
            ? new Look { wrap = KMath.Hex("#140f10"), band = KMath.Hex("#5e1618"), groove = KMath.Hex("#ff5a46"), cord = KMath.Hex("#a82424"), grooveGlow = 2.6f, grooveWidth = 0.013f }
            : new Look { wrap = KMath.Hex("#1b1416"), band = KMath.Hex("#7a1f24"), groove = KMath.Hex("#ff4a3c"), cord = KMath.Hex("#8c2323"), grooveGlow = 2.0f, grooveWidth = 0.009f };

        const float Pommel = -0.46f, GuardAt = 0.08f;
        // the katana's blade line: from the guard (0.08, 0) to (0.80, 0.03)
        static readonly float Slant = Mathf.Atan2(0.03f, 0.72f);

        static Vector2[] Rect(float x0, float x1, float h) => new[] { new Vector2(x0, -h), new Vector2(x1, -h), new Vector2(x1, h), new Vector2(x0, h) };

        /// <summary>A point of the blade's centreline at <paramref name="u"/> (0 guard, 1 tip), with the slight curve towards the edge (-y).</summary>
        static Vector2 Centre(float u, float tip, float side)
        {
            Vector2 d = new Vector2(Mathf.Cos(Slant), Mathf.Sin(Slant)), n = new Vector2(-d.y, d.x);
            float sori = -0.022f * 4f * u * (1f - u);
            return new Vector2(GuardAt, 0f) + d * (tip * u) + n * (sori + side);
        }

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            // a character whose part set already paints its own tachi (Raikon, SAN-71) keeps it: draw nothing
            if (rig.HasOwnPart("Tachi_Blade")) { rig.SetSwordAlwaysFront(false); return; }
            rig.SetBaseSwordHidden(true);
            rig.SetSwordAlwaysFront(true);
            var look = LookOf(weaponId);
            float len = TachiMoves.BladeLength;

            // hilt: wrapped grip, diamond bands, cap, round guard, collar
            rig.AddFamilyPart("Tachi_Hilt", Rect(Pommel, GuardAt - 0.01f, 0.024f), look.wrap, PartRole.Body, 0, true);
            for (float x = Pommel + 0.05f; x < GuardAt - 0.04f; x += 0.07f)
                rig.AddFamilyPart("Tachi_Wrap", new[]
                {
                    new Vector2(x, 0f), new Vector2(x + 0.035f, -0.024f), new Vector2(x + 0.07f, 0f), new Vector2(x + 0.035f, 0.024f),
                }, look.band, PartRole.TrimDeep, 1);
            rig.AddFamilyPart("Tachi_Cap", Rect(Pommel - 0.03f, Pommel + 0.02f, 0.03f), Palette.Steel, PartRole.Metal, 1, true);
            rig.AddFamilyPart("Tachi_Guard", new[]
            {
                new Vector2(GuardAt - 0.02f, -0.07f), new Vector2(GuardAt + 0.01f, -0.095f), new Vector2(GuardAt + 0.035f, -0.07f),
                new Vector2(GuardAt + 0.035f, 0.07f), new Vector2(GuardAt + 0.01f, 0.095f), new Vector2(GuardAt - 0.02f, 0.07f),
            }, KMath.Hex("#2a2326"), PartRole.Metal, 2, true);
            rig.AddFamilyPart("Tachi_GuardTrim", Rect(GuardAt - 0.004f, GuardAt + 0.016f, 0.082f), look.groove, PartRole.Eye, 2, false, 1.4f);
            rig.AddFamilyPart("Tachi_Collar", Rect(GuardAt + 0.035f, GuardAt + 0.1f, 0.036f), KMath.Hex("#c9a45c"), PartRole.Metal, 2, true);

            // the blade: broad, with a kissaki point at the family's blade length
            const int n = 24;
            var edge = new List<Vector2>();
            var spine = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float w = u < 0.92f ? Mathf.Lerp(0.04f, 0.032f, u / 0.92f) : 0.032f * (1f - (u - 0.92f) / 0.08f);
                spine.Add(Centre(u, len, w));
                edge.Add(Centre(u, len, -w));
            }
            var poly = new List<Vector2>(spine);
            for (int i = edge.Count - 2; i >= 0; i--) poly.Add(edge[i]);
            rig.AddFamilyPart("Tachi_Blade", poly.ToArray(), Palette.Steel, PartRole.Metal, 0, true);

            // a lit stripe along the back of the blade, and the glowing groove
            var stripe = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float u = 0.1f + i / 16f * 0.8f; stripe.Add(Centre(u, len, 0.022f)); }
            for (int i = 16; i >= 0; i--) { float u = 0.1f + i / 16f * 0.8f; stripe.Add(Centre(u, len, 0.014f)); }
            rig.AddFamilyPart("Tachi_Glint", stripe.ToArray(), Color.white, PartRole.Metal, 1, false, 1.6f);

            var groove = new List<Vector2>();
            for (int i = 0; i <= 18; i++) { float u = 0.06f + i / 18f * 0.8f; groove.Add(Centre(u, len, look.grooveWidth - 0.002f)); }
            for (int i = 18; i >= 0; i--) { float u = 0.06f + i / 18f * 0.8f; groove.Add(Centre(u, len, -look.grooveWidth - 0.002f)); }
            rig.AddFamilyPart("Tachi_Groove", groove.ToArray(), look.groove, PartRole.Eye, 3, false, look.grooveGlow);

            // the cord at the cap, trailing back
            rig.AddFamilyPart("Tachi_Cord", new[]
            {
                new Vector2(Pommel - 0.02f, -0.01f), new Vector2(Pommel - 0.2f, -0.06f), new Vector2(Pommel - 0.27f, -0.08f), new Vector2(Pommel - 0.2f, -0.03f), new Vector2(Pommel - 0.02f, 0.01f),
            }, look.cord, PartRole.Trim, 3);
        }

        public void Detach(FighterRig rig)
        {
            rig.ClearFamilyParts();
            rig.SetSwordAlwaysFront(false);
        }
    }
}
