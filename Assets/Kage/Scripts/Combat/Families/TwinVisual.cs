using UnityEngine;
using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// Draws the twin blades (ADR-0006): the part set's katana is replaced by one short blade on the Sword bone (far
    /// hand) and a second on the near hand's own bone (<c>HandN</c>), each built along its own axis with the rig's family
    /// part API (<see cref="FighterRig.AddFamilyPart"/>). Tsubame, Kagehira &amp; Yūrei and the daggers draw as short
    /// blades; Hisame's Mist Fans as open fans. Attaching is idempotent (it removes what it added before), as an equip
    /// and a restored broken sword both call it.
    /// </summary>
    public sealed class TwinVisual : IWeaponVisual
    {
        enum Look { Blades, Daggers, Fans, Tsubame }

        static Look LookOf(string weaponId) => weaponId switch
        {
            "tsubame" => Look.Tsubame,
            "shadow_daggers" or "courier_kunai" or "quiet_kodachi" => Look.Daggers,
            "mist_fans" => Look.Fans,
            _ => Look.Blades,
        };

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetBaseSwordHidden(true);
            var d = rig.def;
            var look = LookOf(weaponId);

            // far blade: along the Sword bone's own blade line (the part set's katana line), shortened to the family's length.
            // AddFamilyPart's points are in the weapon frame: x along the katana from the grip pivot, turned onto the bone by the bind angle.
            Vector2 farBase = KMath.Rot(d.bladeBaseLocal, -d.bladeBindAngle);
            Vector2 farDir = KMath.Rot((d.bladeTipLocal - d.bladeBaseLocal).normalized, -d.bladeBindAngle);
            Blade(rig, "Far", B.Sword, farBase, farDir, look);

            // near blade: straight out of the near fist along the hand's own axis, a fist-width past the grip point
            // (HandN-local points: the near hand's rotation is the blade's, see Fighter.NearBladeBaseLocal)
            Blade(rig, "Near", B.HandN, d.fistLocal + Vector2.right * 0.05f, Vector2.right, look);
        }

        public void Detach(FighterRig rig) => rig.ClearFamilyParts();

        /// <summary>
        /// Tsubame as the Armory draws it (JS <c>kb_tsubame</c>, src/js/21-characters/kage/weapons.js): a short fang bowing toward
        /// the spine like a swallow's wing with a hooked bump at its base, the lightning engraving down the middle of the
        /// flat, the electric-cyan edge light, a swallow-tail pommel and a swept-back wing guard. JS units, 64 to the blade.
        /// </summary>
        static void Tsubame(BladePen p)
        {
            const float L = 64f, hl = 0.32f;
            Color fit = KMath.Hex("#C9A45C"), fitHi = KMath.Hex("#F2DCA4"), glow = KMath.Hex("#38c8ff"), hot = KMath.Hex("#e6f9ff");
            static Vector2 V(float x, float y) => new(x, y);

            p.Grip("Grip", 0, -L * hl, 0f, 2.7f, KMath.Hex("#2a2620"), glow, 4);
            // swallow-tail pommel: two prongs
            p.Fill("Pommel", 1, fit, PartRole.Metal, new[] { V(-L * hl + 1f, -2.4f), V(-L * hl - 5f, -5.6f), V(-L * hl - 2.4f, 0f), V(-L * hl - 5f, 5.6f), V(-L * hl + 1f, 2.4f) }, true);
            // wing guard, swept back toward the hand
            var guard = new[] { V(1.2f, -1.6f), V(-1.2f, -7.6f), V(-4.2f, -9.4f), V(-1.6f, -4.4f), V(-1.6f, 4.4f), V(-4.2f, 9.4f), V(-1.2f, 7.6f), V(1.2f, 1.6f) };
            p.Fill("Guard", 1, fit, PartRole.Metal, guard, true);
            p.Stroke("GuardHi", 2, fitHi, PartRole.Metal, new List<Vector2>(guard) { guard[0] }, 0.7f);

            float Bow(float u) => -0.24f * L * Mathf.Pow(u, 2.2f);      // the fang bows toward the spine
            float Belly(float u) => Bow(u) + Mathf.Lerp(3.6f, 0.1f, Mathf.Pow(u, 1.15f)) + 1.6f * Mathf.Sin(u * Mathf.PI);
            var spine = new List<Vector2> { V(0.02f, -2.6f), V(0.09f, -3.3f), V(0.13f, -5.0f), V(0.16f, -3.6f) };   // the hooked bump
            for (int i = 1; i <= 8; i++) { float u = 0.16f + 0.84f * i / 8f; spine.Add(V(u, Bow(u) - Mathf.Lerp(3.3f, 0.2f, Mathf.Pow((u - 0.16f) / 0.84f, 1.3f)))); }
            var edge = new List<Vector2> { V(0.02f, 3.0f) };
            for (int i = 1; i <= 10; i++) { float u = 0.02f + 0.98f * i / 10f; edge.Add(V(u, Belly(u))); }
            p.Fill("Steel", 0, Palette.Steel, PartRole.Metal, BladePen.Outline(L, spine, edge), true);
            // the lightning engraving along the middle of the flat, then the cyan edge on the belly
            var zz = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float u = 0.12f + 0.6f * i / 6f; zz.Add(V(u * L, Bow(u) + (i % 2 == 1 ? 1.1f : -0.6f))); }
            p.Stroke("Engraving", 1, KMath.Hex("#7a5f34"), PartRole.TrimDeep, zz, 0.75f);
            var eg = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float u = 0.06f + 0.92f * i / 12f; eg.Add(V(u * L, Belly(u) - 0.5f)); }
            p.Glow("Edge", eg, 0.6f, glow, hot, 1, 2);
        }

        static void Blade(FighterRig rig, string side, B bone, Vector2 origin, Vector2 dir, Look look)
        {
            if (look == Look.Tsubame) { Tsubame(new BladePen(rig, $"Twin_{side}Blade", bone, origin, dir, TwinMoves.BladeLength / 64f)); return; }
            Vector2 perp = new(-dir.y, dir.x);
            Vector2[] Poly(params float[] uv)
            {
                var r = new Vector2[uv.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = origin + dir * uv[i * 2] + perp * uv[i * 2 + 1];
                return r;
            }
            void Add(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
                => rig.AddFamilyPart($"Twin_{side}Blade_{name}", pts, c, role, sub, rim, glow, bone);

            float L = TwinMoves.BladeLength;
            Color steel = look == Look.Daggers ? KMath.Hex("#8F8C99") : look == Look.Fans ? KMath.Hex("#B9C4C9") : Palette.Steel;
            Color dark = look == Look.Daggers ? KMath.Hex("#3A3B44") : Palette.SteelDark;
            Color metal = look == Look.Daggers ? KMath.Hex("#5A5B66") : KMath.Hex("#B89A5A");

            if (look == Look.Fans)
            {
                // Hisame's war fan (SAN-105, the JS SUB_WEAPON_ART.mist_fans): rain-dark paper in alternating pleats on a wedge from a
                // third of the way out, a rain-blue edge, pale rain streaks, and black lacquered guard sticks along both sides
                const float A0 = -26f, A1 = 14f, Inner = 0.3f;
                const int Pleats = 7;
                Vector2 At(float deg, float r) => new(r * Mathf.Cos(deg * Mathf.Deg2Rad), r * Mathf.Sin(deg * Mathf.Deg2Rad));
                float[] Wedge(float a, float b, float r0, float r1)
                {
                    var xy = new List<float>();
                    void P2(Vector2 v) { xy.Add(v.x); xy.Add(v.y); }
                    P2(At(a, r0));
                    for (int k = 0; k <= 3; k++) P2(At(Mathf.Lerp(a, b, k / 3f), r1));
                    P2(At(b, r0));
                    return xy.ToArray();
                }
                Add("Fan", 0, KMath.Hex("#253447"), PartRole.Metal, Poly(Wedge(A0, A1, L * Inner, L)), true);
                for (int i = 1; i < Pleats; i += 2)
                    Add("Pleat" + i, 1, KMath.Hex("#1A2532"), PartRole.Metal, Poly(Wedge(Mathf.Lerp(A0, A1, (float)i / Pleats), Mathf.Lerp(A0, A1, (i + 1f) / Pleats), L * Inner, L)));
                Add("Edge", 1, KMath.Hex("#7FB8FF"), PartRole.Trim, Poly(Wedge(A0, A1, L * 0.93f, L)), false, 1.4f);
                foreach (var (r0, r1, a) in new[] { (0.5f, 0.7f, -17f), (0.62f, 0.86f, -7f), (0.45f, 0.62f, 3f), (0.72f, 0.9f, -21f) })
                {
                    Vector2 p = At(a, L * r0), q = At(a + 3f, L * r1);
                    Add("Rain", 2, KMath.Hex("#6E8FAE"), PartRole.Trim, Poly(p.x, p.y, q.x, q.y, q.x, q.y + 0.004f, p.x, p.y + 0.004f), false);
                }
                foreach (float a in new[] { A0, A1 })
                {
                    Vector2 e = At(a, L), n = new Vector2(-Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 0.008f;
                    Add("Stick", 2, KMath.Hex("#060607"), PartRole.Metal, Poly(-n.x, -n.y, e.x - n.x, e.y - n.y, e.x + n.x, e.y + n.y, n.x, n.y), true);
                }
            }
            else
            {
                // a short, straight-backed blade with a swept point
                Add("Steel", 0, steel, PartRole.Metal, Poly(
                    0f, 0.018f, 0.55f * L, 0.0185f, 0.9f * L, 0.013f, L, 0.001f, 0.88f * L, -0.011f, 0.5f * L, -0.016f, 0f, -0.017f), true);
                Add("Glint", 1, Color.white, PartRole.Metal, Poly(
                    0.05f * L, 0.011f, 0.8f * L, 0.011f, 0.85f * L, 0.0075f, 0.05f * L, 0.0075f), false, 1.8f);
                Add("Fuller", 1, dark, PartRole.Metal, Poly(0.05f * L, 0.0f, 0.75f * L, 0.0f, 0.75f * L, -0.0045f, 0.05f * L, -0.0045f));
            }

            // guard, hilt, wrap and pommel
            Add("Guard", 2, metal, PartRole.Metal, Poly(-0.004f, -0.045f, 0.012f, -0.045f, 0.012f, 0.045f, -0.004f, 0.045f), true);
            Add("Hilt", 0, KMath.Hex("#1C1214"), PartRole.Body, Poly(-0.15f, -0.014f, -0.004f, -0.014f, -0.004f, 0.014f, -0.15f, 0.014f), true);
            for (int i = 0; i < 3; i++)
            {
                float u = -0.12f + i * 0.04f;
                Add("Wrap", 1, rig.def.scarfBack, PartRole.TrimDeep, Poly(u - 0.014f, 0f, u, -0.011f, u + 0.014f, 0f, u, 0.011f));
            }
            Add("Pommel", 1, metal, PartRole.Metal, Poly(-0.17f, -0.017f, -0.148f, -0.017f, -0.148f, 0.017f, -0.17f, 0.017f));
        }
    }
}
