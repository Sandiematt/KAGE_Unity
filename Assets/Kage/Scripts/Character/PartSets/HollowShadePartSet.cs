using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The Hollow Shade's look (SAN-76, redrawn smooth in the SAN-76 rework): a port of the JS <c>hollow_shade</c> shape for shape,
    /// on Kage's skeleton with the smooth half of <see cref="OpponentKit"/> and the gi body of <see cref="HollowAnimeBody"/>. A memory
    /// of a samurai coming apart: a ragged cowl with a torn tail and a crown of three floating mask shards, the gaunt black profile
    /// with a long nose and a parted mouth under the upper half of a cracked mask, a cold white eye, a gi with a violet-edged collar
    /// and a rip across the chest showing the dark under it, sleeves torn into points at mid-forearm over bandaged kote, a torn obi
    /// with a hanging end, a saya at the hip, a torn haori skirt to the calf (a long back panel and a shorter front flap, cloth on
    /// chains), wide hakama over wrapped shins and straw sandals. The echo of a katana is <see cref="HollowBody.Sword"/>.
    /// <para>
    /// Sizes: the JS Shade is the lean Hollow (build 9.5, U 1.14 JS px per design unit against the Husk's 2.14), so its limbs are
    /// 2.84 reference px per unit (the Husk's 4.45 scaled by the two skeletons), its trunk 4.1 across (the Husk's 6.45 the same way)
    /// and its head 56 px (the Husk's 58 at 1.16 times the body).
    /// </para>
    /// </summary>
    public sealed class HollowShadePartSet : IPartSet
    {
        public string Id => "hollow_shade";

        static readonly HollowAnimeBody K = new(2.84f, 1.04f, 4.1f, 846f, 168f, 56f);

        // the JS face of a hollow_shade: gaunt, the nose a little longer, the mouth parted
        static readonly HollowAnimeBody.Profile Face = new() { nose = 1.15f, lip = 0.7f, chin = 1f, jaw = 0.88f, brow = 1.1f, open = 0.1f };

        static readonly Tone Obi = new(KMath.Hex("#0C0A12"), KMath.Hex("#17131F"), KMath.Hex("#2C2640"));
        static readonly Tone SayaT = new(KMath.Hex("#0A0810"), KMath.Hex("#15111C"), KMath.Hex("#2C2638"));

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW SHADE");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            // JS robe: back [7 links, 11 px, 22 -> 50 wide], flap [6, 10.5, 15 -> 30], teeth 6 (the flap one fewer), rag 9;
            // hung from the waist, to the calf behind and over the knee in front
            K.Robe(d, true, 7, 22f, 50f, 6, 9f, new Vector2(914f, 412f), new Vector2(982f, 742f), 1f);
            K.Arm(d, far: true, HollowAnimeBody.Cuff.Torn);
            K.Leg(d, far: true);
            K.Pelvis(d);
            Torso(d);
            K.Leg(d, far: false);
            K.Robe(d, false, 6, 15f, 30f, 5, 9f, new Vector2(852f, 404f), new Vector2(848f, 686f), 0.65f);
            Sash(d);
            Saya(d);
            K.Neck(d);
            Head(d);
            K.Arm(d, far: false, HollowAnimeBody.Cuff.Torn);
            HollowBody.Sword(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- the rip across the chest (drawHollowArmor 'rip')
        static void Torso(RigDef d)
        {
            var torso = K.Torso(d);
            var sp = K.Sp;
            var rip = Rounded(sp, 0.82f, -9f, 1, 0.74f, -4f, 1, 0.7f, 0f, 1, 0.6f, 4f, 1, 0.52f, 9f, 1, 0.44f, 13f, 1, 0.5f, 10.4f, 1,
                0.56f, 6.6f, 1, 0.64f, 2.4f, 1, 0.7f, -2.4f, 1, 0.76f, -6f, 1);
            var polys = new List<(Vector2[] pts, Color col)> { (rip, HollowAnimeBody.Black) };
            // its lips frayed violet: the lower lip back to the start
            polys.Add((Band(3f, sp.Xy(0.44f, 13f, 0.5f, 10.4f, 0.56f, 6.6f, 0.64f, 2.4f, 0.7f, -2.4f, 0.76f, -6f, 0.82f, -9f)), H.Violet));
            polys.Add((Band(1.6f, sp.Xy(0.82f, -9f, 0.74f, -4f, 0.7f, 0f, 0.6f, 4f, 0.52f, 9f)), H.ClothLit));
            d.parts.Add(new PartDef
            {
                name = "Chest_Rip", bone = B.Chest, order = Slot.TorsoTrim * 4, role = PartRole.Detail, rim = false,
                polys = new List<(Vector2[] pts, Color col)>(Inside(HollowAnimeBody.ConvexHull(torso), polys)),
            });
        }

        // ---------------------------------------------------------------------------------------------- the torn obi (JS hollowSash, the Hollow's own)
        static void Sash(RigDef d)
        {
            var sp = K.Sp;
            float U = K.U;
            // the torn end hanging at the front, swinging
            Vector2 t = sp.Px(0.04f, 9.5f);
            Vector2 down = (sp.Px(0f, 0f) - sp.Px(1f, 0f)).normalized;
            Vector2 du = (down * 0.7f + new Vector2(0f, 0.5f)).normalized, n = new(-du.y, du.x);
            float L = 22f * U, w = 2.6f * U;
            Vector2 P(float nw, float l) => t + n * w * nw + du * L * l;
            var end = new Outline().MoveTo(P(-1f, 0f).x, P(-1f, 0f).y);
            foreach (var (a, b) in new[] { (1f, 0f), (1.2f, 0.86f), (0.3f, 0.72f), (-0.2f, 1f), (-1.1f, 0.78f) }) end.LineTo(P(a, b).x, P(a, b).y);
            AddCloth(d, "Sash_Tail", B.Chest, Slot.Belt, end.Bind(), HollowAnimeBody.Back, 1.6f * U, 0f,
                new ChainFeel { stiffRoot = 0.4f, stiffTip = 0.08f, drag = 2.2f, gravity = 5f, breeze = 0.7f },
                new[] { HollowAnimeBody.Xy(t, P(0f, 0.5f), P(0f, 1f)) }, 0, true);

            // the band: a dark obi across the waist, its corners eased, two broken violet lines along it
            var band = RoundedAll(sp, 0f, -12f, 0f, -12.8f, 0.04f, -13f, 0.12f, -13f, 0.16f, -12.8f, 0.16f, -12f,
                0.16f, 11f, 0.16f, 11.8f, 0.12f, 12f, 0.04f, 12f, 0f, 11.8f, 0f, 11f);
            var detail = new List<(Vector2[] pts, Color col)>();
            foreach (float u in new[] { 0.03f, 0.135f })
                detail.AddRange(Dashes(H.Violet, 2.6f, 4f * U, 2f * U, sp.Xy(u, -12.8f, u, 11.8f)));
            AddCel(d, "Sash", B.Chest, Slot.Belt, band, Obi, 3f * U, 0.6f * U, 1, true, Inside(band, detail), PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- the saya (JS animeSaya)
        static void Saya(RigDef d)
        {
            var sp = K.Sp;
            float U = K.U;
            Vector2 a = sp.Px(0.16f, 7f);
            Vector2 front = sp.ay.normalized, up = (sp.Px(1f, 0f) - sp.Px(0f, 0f)).normalized;
            Vector2 dir = (-front * 0.78f - up * 0.62f).normalized;
            Vector2 b = a + dir * 175f, n = new(-dir.y, dir.x);
            var saya = Capsule(a.x, a.y, b.x, b.y, 2.4f * U * 1.2f, 1.9f * U * 1.2f);
            Vector2 L(float k) => Vector2.Lerp(a, b, k);
            var detail = new List<(Vector2[] pts, Color col)>
            {
                (Oval(b.x, b.y, 2.1f * U * 1.2f, 2.1f * U * 1.2f), H.Steel),                                       // the chape
                (Band(1.4f * U, L(0.04f).x + n.x * 3f * U, L(0.04f).y + n.y * 3f * U, L(0.04f).x - n.x * 3f * U, L(0.04f).y - n.y * 3f * U), H.Steel),   // the mouth
                (Band(1.2f * U, HollowAnimeBody.QuadPx(L(0.16f), L(0.25f) + new Vector2(0f, 6f * U), L(0.34f))), H.VioletDeep),   // the cord
            };
            AddCel(d, "Saya", B.Hips, Slot.BeltTrim, saya, SayaT, 1.6f * U, 0.6f * U, 0, true, detail, PartRole.Detail);
        }

        // ---------------------------------------------------------------------------------------------- the head (JS drawHollowHead 'shade')
        static readonly float[][] Shards =
        {
            new[] { -0.62f, -1.2f, 1f, -1.2f, -1.86f, 1f, -0.92f, -2.0f, 1f, -0.3f, -1.3f, 1f },
            new[] { -0.2f, -1.26f, 1f, -0.12f, -2.24f, 1f, 0.14f, -2.08f, 1f, 0.22f, -1.24f, 1f },
            new[] { 0.36f, -1.16f, 1f, 0.86f, -1.72f, 1f, 0.98f, -1.5f, 1f, 0.64f, -1.04f, 1f },
        };

        static void Head(RigDef d)
        {
            float R = K.R;
            K.Face(d, Face);

            // the crown: three shards of its broken mask hanging in the air over the head, drifting (two behind the cowl, one before it)
            for (int i = 0; i < 3; i++)
            {
                var s = Shards[i];
                var shape = Rounded(K.Hd, s);
                bool far = i == 0;
                var p = AddCel(d, "Shard_" + (i + 1), B.Head, i < 2 ? Slot.Head : Slot.HeadTop, shape, far ? HollowAnimeBody.MaskFar : HollowAnimeBody.Mask,
                    R * 0.2f, far ? 0f : R * 0.07f, i < 2 ? 1 : 0, true, null, PartRole.Metal);
                Vector2 pv = K.Hd.Px((s[0] + s[9]) * 0.5f, (s[1] + s[10]) * 0.5f);
                Swing(p, pv.x, pv.y, 0.7f + 0.2f * i);
            }

            // the ragged cowl, its tail torn into points at the nape, a dashed violet seam over it
            var cowl = Rounded(K.Hd, 0.66f, -0.78f, 0, 0.2f, -1.16f, 0, -0.55f, -1.12f, 0, -1.08f, -0.6f, 0, -1.2f, 0.2f, 0, -1.06f, 0.86f, 0,
                -1.42f, 1.42f, 1, -0.86f, 1.1f, 0, -0.78f, 1.62f, 1, -0.4f, 1.12f, 0, -0.12f, 1.46f, 1, 0.04f, 1.0f, 0, 0.42f, 1.02f, 1,
                0.2f, 0.62f, 0, 0.0f, 0.2f, 0, 0.06f, -0.32f, 0, 0.4f, -0.62f, 0);
            AddCel(d, "Cowl", B.Head, Slot.Head, cowl, HollowAnimeBody.Fab, R * 0.3f, R * 0.05f, 2, true,
                Inside(HollowAnimeBody.ConvexHull(cowl), Dashes(H.Violet, R * 0.06f, R * 0.3f, R * 0.14f, QuadXy(K.Hd, 0.5f, -0.72f, -0.2f, -0.6f, -0.5f, 0.6f, 16))));

            // the upper half of its mask, sculpted on its own face (grown 1.06) and broken off over the parted mouth (JS HOLLOW_HALF_CUT)
            var grown = HollowAnimeBody.FacePath(Frame.Head(K.HeadX, K.HeadY, K.Rf * 1.06f), Face);
            var cut = Rounded(K.Hd, 0.06f, -1.6f, 1, 1.8f, -1.6f, 1, 1.8f, 0.36f, 1, 1.02f, 0.42f, 1, 0.86f, 0.28f, 1, 0.66f, 0.42f, 1,
                0.48f, 0.22f, 1, 0.3f, 0.34f, 1, 0.12f, 0.14f, 1, 0.0f, -0.4f, 1);
            var mask = ClipStar(grown, cut, P(K.Hd.Px(0.5f, 0f).x, K.Hd.Px(0.5f, 0f).y));
            K.MaskPart(d, "Mask", Slot.HeadTop, 1, mask, HollowAnimeBody.Mask, true,
                new[] { 0.56f, -0.97f, 0.62f, -0.58f, 0.5f, -0.36f, 0.64f, -0.12f, 0.6f, 0.12f }, true);

            K.Eye(d);
        }
    }
}
