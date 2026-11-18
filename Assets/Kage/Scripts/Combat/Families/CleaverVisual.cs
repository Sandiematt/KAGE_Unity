using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the cleaver family's weapon on the Sword bone in place of the part set's katana (SAN-79): a long
    /// cord-wrapped handle held by both hands, a heavy square guard, and a broad, squared-off blade with a notched
    /// spine and a bright edge, ending exactly at the family's blade length. Looks by weapon id: Onikiri's soot-dark
    /// fuller and ember edge, the Order's dull slate Raider Cleaver, the Tyrant's red-hot edge and Iwakudaki's stone
    /// grey. Axis frame: x along the weapon from the lead hand's grip, the edge on the lower side.
    /// </summary>
    public sealed class CleaverVisual : IWeaponVisual
    {
        struct Look
        {
            public Color steel, fuller, edge, wrap, fitting, glow;
            public bool notched;
        }

        static Look LookOf(string weaponId)
        {
            switch (weaponId)
            {
                case "raider_cleaver":
                    return new Look { steel = KMath.Hex("#7d8186"), fuller = KMath.Hex("#34373c"), edge = KMath.Hex("#d9dde0"), wrap = KMath.Hex("#2b2d31"), fitting = KMath.Hex("#4a4d52"), glow = KMath.Hex("#d24a3c"), notched = true };
                case "tyrant_cleaver":
                    return new Look { steel = KMath.Hex("#8a8680"), fuller = KMath.Hex("#3a2622"), edge = KMath.Hex("#ff8a52"), wrap = KMath.Hex("#2a1a18"), fitting = KMath.Hex("#5a3a30"), glow = KMath.Hex("#ff6a3c"), notched = true };
                case "iwakudaki":
                case "gate_kanabo":
                    return new Look { steel = KMath.Hex("#6c6a66"), fuller = KMath.Hex("#3e3c39"), edge = KMath.Hex("#b5b0a6"), wrap = KMath.Hex("#3a2f26"), fitting = KMath.Hex("#53514d"), glow = KMath.Hex("#c8b48a"), notched = true };
                default:    // Onikiri: the field cleaver of the Silent War
                    return new Look { steel = KMath.Hex("#a3a8b0"), fuller = KMath.Hex("#2a2220"), edge = KMath.Hex("#f2f0ec"), wrap = KMath.Hex("#5a2a26"), fitting = KMath.Hex("#6b6258"), glow = KMath.Hex("#ff6a3c"), notched = false };
            }
        }

        const float Pommel = -0.5f;      // the handle's butt, behind the lead hand
        const float Guard = 0.07f;       // the guard sits just ahead of the lead hand; the blade starts at 0.10

        static Vector2[] Rect(float x0, float x1, float y0, float y1) => new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetBaseSwordHidden(true);
            rig.SetSwordAlwaysFront(false);
            var look = LookOf(weaponId);
            float x0 = 0.1f, tip = 0.08f + CleaverMoves.BladeLength, len = tip - x0;

            // the handle, wrapped with cord, and a heavy pommel cap
            rig.AddFamilyPart("Cleaver_Handle", Rect(Pommel, Guard + 0.02f, -0.024f, 0.024f), look.wrap, PartRole.Body, 0, true);
            for (float x = Pommel + 0.08f; x < Guard - 0.04f; x += 0.085f)
                rig.AddFamilyPart("Cleaver_Cord", Rect(x, x + 0.03f, -0.027f, 0.027f), look.fitting, PartRole.TrimDeep, 1);
            rig.AddFamilyPart("Cleaver_Pommel", new[]
            {
                new Vector2(Pommel - 0.05f, 0f), new Vector2(Pommel - 0.02f, -0.045f), new Vector2(Pommel + 0.04f, -0.04f),
                new Vector2(Pommel + 0.04f, 0.04f), new Vector2(Pommel - 0.02f, 0.045f),
            }, look.fitting, PartRole.Metal, 2, true);

            // the guard: a heavy square plate
            rig.AddFamilyPart("Cleaver_Guard", Rect(Guard - 0.02f, Guard + 0.05f, -0.075f, 0.075f), look.fitting, PartRole.Metal, 2, true);

            // the blade: a straight spine with its notches, a clipped point, a squared front and a bellied edge
            var poly = new System.Collections.Generic.List<Vector2>
            {
                new Vector2(x0, 0.05f),
                new Vector2(x0 + 0.3f * len, 0.064f),
            };
            if (look.notched)
            {
                poly.Add(new Vector2(x0 + 0.42f * len, 0.074f)); poly.Add(new Vector2(x0 + 0.46f * len, 0.062f));
                poly.Add(new Vector2(x0 + 0.58f * len, 0.082f)); poly.Add(new Vector2(x0 + 0.62f * len, 0.07f));
            }
            poly.Add(new Vector2(x0 + 0.82f * len, 0.092f));
            poly.Add(new Vector2(tip, 0.022f));                         // the clipped point, on the hit line
            poly.Add(new Vector2(tip - 0.012f, -0.16f));                // the squared front dropping to the belly
            poly.Add(new Vector2(x0 + 0.5f * len, -0.15f));
            poly.Add(new Vector2(x0 + 0.18f * len, -0.125f));
            poly.Add(new Vector2(x0, -0.06f));
            rig.AddFamilyPart("Cleaver_Blade", poly.ToArray(), look.steel, PartRole.Metal, 0, true);

            // the fuller groove down the blade, soot-dark (Onikiri), and the eye hole near the point
            rig.AddFamilyPart("Cleaver_Fuller", Rect(x0 + 0.04f, x0 + 0.78f * len, 0.012f, 0.034f), look.fuller, PartRole.TrimDeep, 1);
            rig.AddFamilyPart("Cleaver_Eye", new[]
            {
                new Vector2(x0 + 0.76f * len, 0.0f), new Vector2(x0 + 0.8f * len, -0.03f), new Vector2(x0 + 0.84f * len, 0.0f), new Vector2(x0 + 0.8f * len, 0.03f),
            }, look.fuller, PartRole.TrimDeep, 1);

            // the lit edge along the belly, and an ember glow in the fuller
            rig.AddFamilyPart("Cleaver_Edge", new[]
            {
                new Vector2(x0 + 0.18f * len, -0.125f), new Vector2(x0 + 0.5f * len, -0.15f), new Vector2(tip - 0.012f, -0.16f),
                new Vector2(tip - 0.012f, -0.145f), new Vector2(x0 + 0.5f * len, -0.135f), new Vector2(x0 + 0.18f * len, -0.11f),
            }, look.edge, PartRole.Metal, 1, false, 1.7f);
            rig.AddFamilyPart("Cleaver_Ember", Rect(x0 + 0.1f * len, x0 + 0.6f * len, 0.018f, 0.026f), look.glow, PartRole.Trim, 2, false, 1.6f);
        }

        public void Detach(FighterRig rig)
        {
            rig.ClearFamilyParts();
            rig.SetSwordAlwaysFront(false);
        }
    }
}
