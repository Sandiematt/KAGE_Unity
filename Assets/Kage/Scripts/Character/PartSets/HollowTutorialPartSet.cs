using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The tutorial Hollow's look (SAN-76, redrawn smooth in the SAN-76 rework): a port of the JS <c>hollow_tutorial</c> shape for
    /// shape, on Kage's skeleton with the smooth half of <see cref="OpponentKit"/> and the gi body of <see cref="HollowAnimeBody"/>.
    /// The training husk: a straw dummy that learned to stand. Straw bursting from the crown of a stitched sack hood tied at the
    /// throat with a rope whose two ends hang at the nape, a plank practice board strapped over the eyes (wood grain, two nails, a
    /// glowing crack), a softer gaunt profile with a snub nose under it, two ropes crossed over the chest and a stitched patch, a rope
    /// sash knotted at the front with two short ends, straw bursting from the sleeve openings, a short rag skirt to above the knee,
    /// hakama over wrapped shins and straw sandals. No saya and no shards. The echo of a katana is <see cref="HollowBody.Sword"/>.
    /// <para>
    /// Sizes as the Shade's (the same JS build), the sleeves the JS width (1, the Shade's 1.04).
    /// </para>
    /// </summary>
    public sealed class HollowTutorialPartSet : IPartSet
    {
        public string Id => "hollow_tutorial";

        static readonly HollowAnimeBody K = new(2.84f, 1f, 4.1f, 846f, 168f, 56f);

        // the JS face of a hollow_tutorial: gaunt but softer, a short lifted nose, a smaller chin and jaw, a low brow
        static readonly HollowAnimeBody.Profile Face = new() { nose = 0.92f, up = 0.03f, lip = 0.85f, chin = 0.9f, jaw = 0.94f, brow = 0.8f, open = 0.07f };

        static readonly Tone SackT = new(H.SackDark, H.Sack, KMath.Hex("#4E443A"));
        static readonly Tone WoodT = new(KMath.Hex("#14100C"), H.Wood, KMath.Hex("#5A4A36"));

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW (TUTORIAL)");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            // JS robe: back [4 links, 10 px, 22 -> 32 wide], flap [4, 9.5, 16 -> 24], teeth 4 (the flap one fewer), rag 6: a short rag skirt
            K.Robe(d, true, 4, 22f, 32f, 4, 6f, new Vector2(912f, 412f), new Vector2(944f, 590f), 0.85f);
            K.Arm(d, far: true, HollowAnimeBody.Cuff.Straw);
            K.Leg(d, far: true);
            K.Pelvis(d);
            Torso(d);
            K.Leg(d, far: false);
            K.Robe(d, false, 4, 16f, 24f, 3, 6f, new Vector2(852f, 404f), new Vector2(846f, 578f), 0.7f);
            Sash(d);
            K.Neck(d);
            Head(d);
            K.Arm(d, far: false, HollowAnimeBody.Cuff.Straw);
            HollowBody.Sword(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- crossed ropes and a patch (drawHollowArmor 'rope')
        static void Torso(RigDef d)
        {
            var torso = K.Torso(d);
            var sp = K.Sp;
            float U = K.U;
            var polys = new List<(Vector2[] pts, Color col)>();
            // a stitched patch of sacking
            var patch = V(sp.Xy(0.72f, -6f, 0.72f, 2f, 0.48f, 3f, 0.46f, -6f));
            polys.Add((patch, H.SackDark));
            polys.AddRange(Dashes(H.Sack, 2.2f, 1.6f * U, 1.4f * U, sp.Xy(0.72f, -6f, 0.72f, 2f, 0.48f, 3f, 0.46f, -6f, 0.72f, -6f)));
            // two ropes crossed over the chest, the twist of their strands across each
            Vector2 up = (sp.Px(1f, 0f) - sp.Px(0f, 0f)).normalized, nf = sp.ay.normalized;
            foreach (var (a, b) in new[] { (sp.Px(1.0f, -10f), sp.Px(0.1f, 12f)), (sp.Px(1.0f, 9f), sp.Px(0.12f, -12f)) })
            {
                polys.Add((Band(3.2f * U, a.x, a.y, b.x, b.y), KMath.Hex("#17130D")));
                polys.Add((Band(2.2f * U, a.x, a.y, b.x, b.y), H.RopeDark));
                for (int i = 1; i < 12; i++)
                {
                    Vector2 x = Vector2.Lerp(a, b, i / 12f), p = x - up * 1.2f * U, q = x + nf * 1.1f * U;
                    polys.Add((Band(2.2f, p.x, p.y, q.x, q.y), H.Rope));
                }
            }
            d.parts.Add(new PartDef
            {
                name = "Rope_Cross", bone = B.Chest, order = Slot.TorsoTrim * 4, role = PartRole.Detail, rim = false,
                polys = new List<(Vector2[] pts, Color col)>(Inside(HollowAnimeBody.ConvexHull(torso), polys)),
            });
        }

        // ---------------------------------------------------------------------------------------------- the rope sash (JS hollowSash 'rope')
        static void Sash(RigDef d)
        {
            var sp = K.Sp;
            float U = K.U;
            const float w0 = 12.6f, w1 = 11.8f, u1 = 0.1f;
            Vector2 k = sp.Px(u1 * 0.5f, w1 - 2.6f);

            // two short ends hanging from the knot, swinging
            Vector2 down = (sp.Px(0f, 0f) - sp.Px(1f, 0f)).normalized, nf = sp.ay.normalized;
            int i = 0;
            foreach (var (lean, len) in new[] { (0.35f, 15f), (-0.1f, 12f) })
            {
                Vector2 e = k + (down + nf * lean).normalized * len * U;
                var shape = Capsule(k.x, k.y, e.x, e.y, 1.3f * U, 0.95f * U);
                AddCloth(d, i == 0 ? "Sash_Tail" : "Sash_Tail2", B.Chest, Slot.BeltTrim, shape, HollowAnimeBody.Rope, 0.8f * U, 0.4f * U,
                    new ChainFeel { stiffRoot = 0.45f, stiffTip = 0.1f, drag = 2.2f, gravity = 6f, breeze = 0.5f },
                    new[] { HollowAnimeBody.Xy(k, Vector2.Lerp(k, e, 0.5f), e) }, i, false);
                i++;
            }

            // the band, its corners eased as a rope's, the twist of the strands and the knot at the front
            var band = RoundedAll(sp, 0f, -w0 + 0.8f, 0f, -w0, 0.03f, -w0 - 0.2f, 0.07f, -w0 - 0.2f, u1, -w0, u1, -w0 + 0.8f,
                u1, w1 - 0.8f, u1, w1, 0.07f, w1 + 0.2f, 0.03f, w1 + 0.2f, 0f, w1, 0f, w1 - 0.8f);
            var strands = new List<(Vector2[] pts, Color col)>();
            for (int j = 0; j <= 14; j++)
            {
                float w = Mathf.Lerp(-w0, w1, j / 14f);
                strands.Add((Band(3f, sp.Xy(0f, w - 1.6f, u1, w + 1.6f)), KMath.Hex("#17130D")));
                strands.Add((Band(1.8f, sp.Xy(u1 * 0.2f, w + 0.8f - 1.2f, u1 * 0.8f, w + 0.8f + 1.2f)), H.Rope));
            }
            var detail = new List<(Vector2[] pts, Color col)>(Inside(band, strands));
            detail.AddRange(Cel(Oval(k.x, k.y, 2.4f * U * 1.3f, 2.4f * U * 1.3f), HollowAnimeBody.Rope, 1f * U, 0.5f * U));
            AddCel(d, "Sash", B.Chest, Slot.Belt, band, HollowAnimeBody.Rope, 2.2f * U, 0.6f * U, 1, true, detail, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- the head (JS drawHollowHead 'sack')
        static void Head(RigDef d)
        {
            float R = K.R;
            var hd = K.Hd;
            K.Face(d, Face);

            // straw bursting from the crown: five tufts that sway, behind the sack
            var tufts = new[] { (-0.5f, -0.2f, -0.86f, -1.9f), (-0.25f, 0.05f, -0.18f, -2.05f), (0f, 0.3f, 0.42f, -1.86f), (-0.7f, -0.45f, -1.3f, -1.56f), (0.2f, 0.46f, 0.86f, -1.42f) };
            var strands = new[] { (-0.35f, -0.8f, -1.7f), (-0.1f, -0.15f, -1.85f), (0.15f, 0.4f, -1.66f) };
            for (int i = 0; i < tufts.Length; i++)
            {
                var (x0, x1, tx, ty) = tufts[i];
                var tuft = Rounded(hd, x0, -1.0f, 1, tx, ty, 1, x1, -1.02f, 1, (x0 + x1) * 0.5f, -0.86f, 0);
                var detail = new List<(Vector2[] pts, Color col)>();
                if (i < strands.Length)
                {
                    var (x, sx, sy) = strands[i];
                    detail.AddRange(Inside(HollowAnimeBody.ConvexHull(tuft), new[] { (Band(Mathf.Max(1.6f, R * 0.035f), hd.Xy(x, -1f, sx, sy)), KMath.Hex("#2A2216")) }));
                }
                var p = AddCel(d, "Straw_" + i, B.Head, Slot.Head, tuft, HollowAnimeBody.Straw, R * 0.08f, R * 0.03f, 1, true, detail, PartRole.Detail, seamPx: 0f);
                Vector2 pv = hd.Px((x0 + x1) * 0.5f, -1.0f);
                Swing(p, pv.x, pv.y, 0.5f + 0.1f * (i % 3));
            }

            // the stitched sack hood: a seam over the crown with its stitches, a glowing crack at the back
            var sack = RoundedAll(hd, 0.74f, -0.58f, 0.44f, -1.1f, -0.3f, -1.22f, -0.96f, -0.82f, -1.14f, 0.0f, -0.98f, 0.7f, -0.5f, 0.98f, 0.0f, 0.92f,
                0.2f, 0.66f, 0.02f, 0.2f, 0.1f, -0.3f, 0.5f, -0.5f);
            var sd = new List<(Vector2[] pts, Color col)>();
            float lw = Mathf.Max(2f, R * 0.045f);
            sd.Add((Band(lw, QuadXy(hd, -0.1f, -1.2f, -0.7f, -0.6f, -0.62f, 0.6f, 14)), H.Rope));
            for (int i = 0; i < 7; i++)
            {
                float k = i / 6f, x = Mathf.Lerp(-0.2f, -0.66f, k) - Mathf.Sin(k * 3f) * 0.2f, y = Mathf.Lerp(-1.1f, 0.5f, k);
                sd.Add((Band(lw, hd.Xy(x - 0.08f, y - 0.06f, x + 0.08f, y + 0.06f)), H.Rope));
            }
            sd.AddRange(Crack(HollowAnimeBody.Bloom, H.Violet, Mathf.Max(1.6f, R * 0.03f), hd.Xy(-0.9f, -0.4f, -0.62f, -0.2f, -0.74f, 0.1f)));
            AddCel(d, "Sack", B.Head, Slot.Head, sack, SackT, R * 0.3f, R * 0.06f, 2, true, Inside(HollowAnimeBody.ConvexHull(sack), sd));

            // the rope at the throat, its two ends hanging at the nape (cloth)
            var rope = RoundedAll(hd, -0.98f, 0.6f, -0.4f, 0.64f, 0.24f, 0.7f, 0.22f, 0.86f, -0.4f, 0.84f, -0.96f, 0.8f);
            AddCel(d, "Throat_Rope", B.Head, Slot.Head, rope, HollowAnimeBody.Rope, R * 0.06f, R * 0.03f, 3, true, null, PartRole.Detail);
            foreach (var (name, a, b, sub) in new[] { ("Sack_Tail", new Vector2(-0.85f, 0.74f), new Vector2(-1.23f, 1.33f), 0), ("Sack_Tail2", new Vector2(-0.8f, 0.75f), new Vector2(-0.89f, 1.46f), 1) })
            {
                Vector2 pa = hd.Px(a.x, a.y), pb = hd.Px(b.x, b.y);
                var end = Capsule(pa.x, pa.y, pb.x, pb.y, R * 0.075f, R * 0.06f);
                AddCloth(d, name, B.Head, Slot.Neck, end, HollowAnimeBody.Rope, R * 0.05f, R * 0.02f,
                    new ChainFeel { stiffRoot = 0.45f, stiffTip = 0.1f, drag = 2.2f, gravity = 6f, breeze = 0.6f },
                    new[] { HollowAnimeBody.Xy(pa, Vector2.Lerp(pa, pb, 0.5f), pb) }, 2 + sub, false);
            }

            // the plank: a narrow board across the eyes only, so the snub nose, the parted mouth and the chin show under it;
            // wood grain, two nails, the eye holes and a glowing crack
            var plank = Rounded(hd, 0.08f, -0.42f, 1, 1.02f, -0.4f, 1, 1.0f, 0.08f, 1, 0.84f, 0.03f, 1, 0.7f, 0.12f, 1, 0.1f, 0.12f, 1);
            var pp = K.MaskPart(d, "Plank", Slot.HeadTop, 1, plank, WoodT, true, new[] { 0.24f, -0.42f, 0.32f, -0.14f, 0.22f, 0.12f }, false);
            var grain = new List<(Vector2[] pts, Color col)> { (Band(Mathf.Max(1.6f, R * 0.03f), hd.Xy(0.12f, -0.24f, 1.0f, -0.22f)), KMath.Hex("#14100C")) };
            foreach (float x in new[] { 0.16f, 0.96f })
            {
                Vector2 c = hd.Px(x, -0.3f);
                grain.Add((Oval(c.x, c.y, R * 0.06f, R * 0.06f), KMath.Hex("#625B76")));
            }
            pp.polys.AddRange(Inside(HollowAnimeBody.ConvexHull(plank), grain));

            K.Eye(d);
        }
    }
}
