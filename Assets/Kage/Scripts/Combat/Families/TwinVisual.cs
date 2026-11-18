using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the twin blades (ADR-0006): the part set's katana is replaced by one short blade on the Sword bone (far
    /// hand) and a second on the near hand's own bone (<c>HandN</c>), each built along its own axis in bind space.
    /// Tsubame, Kagehira &amp; Yūrei and the daggers draw as short blades; Hisame's Mist Fans as open fans. Attaching
    /// is idempotent (it removes what it added before), as an equip and a restored broken sword both call it.
    /// </summary>
    public sealed class TwinVisual : IWeaponVisual
    {
        enum Look { Blades, Daggers, Fans }

        static Look LookOf(string weaponId) => weaponId switch
        {
            "shadow_daggers" or "courier_kunai" or "quiet_kodachi" => Look.Daggers,
            "mist_fans" => Look.Fans,
            _ => Look.Blades,
        };

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearWeaponParts();
            rig.HideBaseSword(true);
            var d = rig.def;
            var look = LookOf(weaponId);

            // far blade: along the Sword bone's own blade line (the part set's katana line), shortened to the family's length
            Vector2 farBase = d.pivot[(int)B.Sword] + d.bladeBaseLocal;
            Vector2 farDir = (d.bladeTipLocal - d.bladeBaseLocal).normalized;
            foreach (var p in Blade(rig, "Far", B.Sword, farBase, farDir, 16, true, look)) rig.AddWeaponPart(p);

            // near blade: straight out of the near fist along the hand's own axis (bind angle 0), a fist-width past the grip point
            Vector2 nearBase = d.pivot[(int)B.HandN] + d.fistLocal + Vector2.right * 0.05f;
            foreach (var p in Blade(rig, "Near", B.HandN, nearBase, Vector2.right, 28 * 4 + 1, false, look)) rig.AddWeaponPart(p);
        }

        public void Detach(FighterRig rig)
        {
            rig.ClearWeaponParts();
            rig.HideBaseSword(false);
        }

        static List<PartDef> Blade(FighterRig rig, string side, B bone, Vector2 origin, Vector2 dir, int order, bool swordLayer, Look look)
        {
            var parts = new List<PartDef>();
            Vector2 perp = new(-dir.y, dir.x);
            Vector2 Pt(float u, float v) => origin + dir * u + perp * v;
            Vector2[] Poly(params float[] uv)
            {
                var r = new Vector2[uv.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = Pt(uv[i * 2], uv[i * 2 + 1]);
                return r;
            }
            void Add(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
            {
                parts.Add(new PartDef
                {
                    name = $"Twin_{side}Blade_{name}", bone = bone, order = order + sub, color = c, role = role, pts = pts,
                    rim = rim, rimGrow = 0.003f, rimColor = KMath.Hex("#050304"), glow = glow, swordLayer = swordLayer,
                });
            }

            float L = TwinMoves.BladeLength;
            Color steel = look == Look.Daggers ? KMath.Hex("#8F8C99") : look == Look.Fans ? KMath.Hex("#B9C4C9") : Palette.Steel;
            Color dark = look == Look.Daggers ? KMath.Hex("#3A3B44") : Palette.SteelDark;
            Color metal = look == Look.Daggers ? KMath.Hex("#5A5B66") : KMath.Hex("#B89A5A");

            if (look == Look.Fans)
            {
                // an open fan: ribs spreading from the pivot, the paper wedge between them
                var arc = new List<float> { -0.03f, 0f };
                for (int a = -48; a <= 48; a += 12)
                {
                    float r = a == -48 || a == 48 ? L * 0.9f : L;
                    arc.Add(r * Mathf.Cos(a * Mathf.Deg2Rad)); arc.Add(r * Mathf.Sin(a * Mathf.Deg2Rad));
                }
                Add("Fan", 0, steel, PartRole.Metal, Poly(arc.ToArray()), true);
                for (int a = -36; a <= 36; a += 24)
                    Add("Rib", 1, dark, PartRole.Metal, Poly(-0.03f, 0f, L * Mathf.Cos(a * Mathf.Deg2Rad), L * Mathf.Sin(a * Mathf.Deg2Rad), L * Mathf.Cos(a * Mathf.Deg2Rad), L * Mathf.Sin(a * Mathf.Deg2Rad) + 0.006f));
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
            return parts;
        }
    }
}
