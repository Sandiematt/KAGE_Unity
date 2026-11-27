using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Silent Moon</b> (静月, Shizuka; the JS <c>silentMoon</c>, refined to the owner's brief, docs/weapon-styles.md): the iai in one breath.
        /// <list type="number">
        /// <item><b>Sheathe</b> (f0–14): the world goes still and violet; Kage slides the blade home. It clicks shut (f14) and a ring of
        /// silence spreads from his feet.</item>
        /// <item><b>Stillness</b> (f14–44): the iai crouch, the right hand on the hilt; petals hang in the air; a thin glint runs along the saya.</item>
        /// <item><b>The draw</b> (f44): one cut nobody sees. Kage is past the foe, on its far side, his back to it, the blade out in the
        /// follow-through; a single long violet line is left across where he went, and his afterimages along it.</item>
        /// <item><b>The lattice</b> (f56–81): eight thin violet cuts bloom on the foe, one at a time; it doesn't move.</item>
        /// <item><b>Noto</b> (f84–108): chiburi, then the blade slides home slowly. It clicks shut (f108).</item>
        /// <item><b>The fall</b> (f112): every cut opens at once: a violet cross through the foe, SILENCE on the screen, and the foe falls
        /// where it stands. The world runs again; the petals fall.</item>
        /// </list>
        /// Kage draws back to guard at the end (f140). Unblockable throughout; binds only the foe it started on.
        /// </summary>
        sealed class SilentMoonArt : CinematicArt
        {
            public override string Id => SilentMoon;
            const int Click1 = 14, Draw = 44, LatticeFrom = 56, Chiburi = 84, Click2 = 108, Fall = 112, Unsheathe = 140;
            static readonly int[] LatticeAt = { 56, 60, 63, 67, 70, 74, 77, 81 };
            static readonly float[] LatticeRot = { -0.9f, 0.7f, -0.2f, 1.25f, -1.3f, 0.35f, -0.55f, 0.95f };
            static readonly float[] LatticeSize = { 0.62f, 0.78f, 0.9f, 0.6f, 0.66f, 0.82f, 0.74f, 0.7f };
            float _x0;

            public override void Init()
            {
                total = 150; closeIn = Draw; cutIn = 42; closeSize = 1.6f; exchangeSize = 2.25f;
                Pose Body(float hx, float hy, float hr, float chest, float head, float fn, float ff) => new Pose().Hip(hx, hy, hr).Chest(chest).Head(head).Feet(fn, ff);
                // sheathe: the blade slides back along the saya, then the iai ready, then sinking into the crouch
                Hold(0, Body(0.02f, -0.16f, -6f, -16f, 10f, 0.34f, -0.25f).HandF(0.3f, 0.92f).Blade(-162f).HandN(0.12f, 0.85f), 14, Ease.InOutQuad);
                Hold(Click1, ShizukaMoves.Sheathed(Body(-0.02f, -0.17f, -6f, -18f, 12f, 0.28f, -0.28f)), 8);
                Hold(24, ShizukaMoves.Sheathed(Body(-0.04f, -0.25f, -8f, -26f, 16f, 0.32f, -0.3f)), 18, Ease.InOutQuad);
                // the draw: the follow-through, almost at once (the cut is never seen), then zanshin with the back to the foe
                Hold(Draw, Body(0.14f, -0.18f, -16f, -30f, 16f, 0.46f, -0.3f).HandF(0.66f, 1.16f).Blade(18f).HandN(-0.08f, 0.86f).EdgeUp(), 2, Ease.OutExpo);
                Hold(Draw + 6, Body(0.12f, -0.17f, -14f, -28f, 15f, 0.46f, -0.28f).HandF(0.62f, 1.22f).Blade(28f).HandN(-0.08f, 0.86f).EdgeUp(), 12);
                // chiburi, then noto: the blade home, slowly
                Hold(Chiburi, Body(0.08f, -0.17f, -10f, -20f, 12f, 0.42f, -0.26f).HandF(0.5f, 0.86f).Blade(-42f).HandN(0f, 0.86f), 4, Ease.OutCubic);
                Hold(92, Body(0.05f, -0.16f, -8f, -18f, 11f, 0.38f, -0.26f).HandF(0.42f, 0.92f).Blade(-150f).HandN(0.1f, 0.85f), 8, Ease.InOutQuad);
                Hold(100, Body(0.02f, -0.15f, -6f, -16f, 10f, 0.34f, -0.26f).HandF(0.3f, 0.92f).Blade(-162f).HandN(0.12f, 0.85f), 8, Ease.InOutQuad);
                Hold(Click2, ShizukaMoves.Sheathed(Body(-0.02f, -0.12f, -4f, -10f, 6f, 0.26f, -0.28f)), 6);
                Hold(Unsheathe - 4, Stance(), 14, Ease.InOutQuad);
            }

            public override void Start() { }

            Vector2 SayaMouth => k.rig.World(B.Hips, new Vector2(0.13f, 0.1f));

            public override void Tick(int f)
            {
                var look = Look;
                Vector2 v = k.rb.linearVelocity;
                k.rb.linearVelocity = new Vector2(v.x * Mathf.Exp(-14f * Time.fixedDeltaTime), v.y);
                k._artSheathed = (f >= Click1 && f < Draw) || (f >= Click2 && f < Unsheathe);
                trail = (f >= Draw && f <= Draw + 6) || (f >= Chiburi && f <= Chiburi + 3);

                if (f == 1) Vfx.I?.HangingPetals(k.Pos + new Vector2(k.facing * 0.6f, 1.6f), 4.2f, 1.8f, 26, look.col, look.hot, total / 60f + 0.6f);
                if (f == Click1 || f == Click2) Click(look);
                if (f == 30) Vfx.I?.LensStreak(SayaMouth, 0.7f, look.hot, 0.3f, KMath.Ang(new Vector2(-0.73f * k.facing, -0.34f)), 0.012f);
                if (f == Draw) TheDraw(look);
                for (int i = 0; i < LatticeAt.Length; i++) if (f == LatticeAt[i]) LatticeCut(i, look, 1f);
                if (f == Chiburi)
                {
                    Vector2 tip = k.rig.World(B.Sword, k.BladeTipLocal);
                    for (int i = 0; i < 7; i++) Vfx.I?.Embers(tip, new Vector2(k.facing * Random.Range(0.5f, 2.2f), Random.Range(-2.6f, -0.6f)), i % 2 == 0 ? look.col : look.hot, 0.035f, 0.45f);
                    KageGame.I?.Audio?.Play(SfxKind.SwordWhoosh, 0.5f);
                }
                if (f == Fall) TheFall(look);
                if (f == Unsheathe) Vfx.I?.Glint(k.rig.World(B.Sword, k.def.bladeTipLocal * 0.5f), look.hot);
            }

            void Click(RageArtLook look)
            {
                Vfx.I?.Glint(SayaMouth, look.hot);
                Vfx.I?.Ring(new Vector2(k.Pos.x, 0.03f), 0.1f, 1.1f, 0.04f, look.hot.WithA(0.8f), 0.6f, true, 0.22f, 1.6f);
                KageGame.I?.Audio?.Play(SfxKind.Block, 0.55f);
                GameFeel.Hitstop(2);
            }

            void TheDraw(RageArtLook look)
            {
                _x0 = k.Pos.x;
                float side = SideOfTarget;
                float dest = Live ? tgt.Pos.x + side * 1.35f : k.Pos.x + side * 2.6f;
                dest = Mathf.Clamp(dest, k.StageMin + 0.3f, k.StageMax - 0.3f);
                k.ArtPlace(dest, 0.02f, side);                       // past the foe, the back turned to it
                float y = 1.08f;
                Vfx.I?.ArtLine(new Vector2(_x0, y), new Vector2(dest, y), look.col, look.hot, 0.95f, 0.03f);
                for (int i = 1; i <= 4; i++) Vfx.I?.Ghost(k.rig, look.col.WithA(0.42f), 0.35f + i * 0.08f, new Vector2(Mathf.Lerp(_x0, dest, i / 5f) - dest, 0f));
                Vfx.I?.SpeedLines(new Vector2(dest, 1f), -side, look.hot, 9);
                if (Live) Vfx.I?.SlashLine(Chest + new Vector2(-0.5f, -0.18f), Chest + new Vector2(0.5f, 0.18f), look.col, look.hot, 0.18f, 0.03f);
                Blow(4, Vector2.zero, stun: 120, hitstop: 4, shake: 0.12f);
                Bind(120);
                GameFeel.Flash(look.hot.WithA(0.1f), 0.06f);
                KageGame.I?.Audio?.Play(SfxKind.SwordWhoosh, 1f);
                KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.7f);
            }

            Vector2 LatticePoint(int i) => Chest + new Vector2(Mathf.Cos(i * 2.4f) * 0.14f, Mathf.Sin(i * 1.7f) * 0.26f);

            void LatticeCut(int i, RageArtLook look, float scale)
            {
                Vfx.I?.LensCut(LatticePoint(i), LatticeRot[i] * Mathf.Rad2Deg, LatticeSize[i] * scale, look.col, look.hot, 0.55f);
                if (scale <= 1f)
                {
                    Blow(2, Vector2.zero, stun: 120, hitstop: 0, shake: 0.05f);
                    KageGame.I?.Audio?.Play(SfxKind.Slash, 0.3f);
                }
            }

            void TheFall(RageArtLook look)
            {
                Vector2 c = Chest;
                Vfx.I?.LensCut(c, -26f, 1.9f, look.col, look.hot, 0.8f);
                Vfx.I?.LensCut(c, 32f, 1.7f, look.col, look.hot, 0.8f);
                for (int i = 0; i < LatticeAt.Length; i++) LatticeCut(i, look, 1.35f);
                Vfx.I?.SilentCut(c, -k.facing, 1.6f);
                Blow(16, new Vector2(0.6f, 1.0f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.4f, keepMotion: true);   // it falls where it stands
                Finish(c, hitstop: 7, slow: 0.25f, slowSeconds: 0.5f, shake: 0.7f);
                for (int i = 0; i < 24; i++)
                    Vfx.I?.Embers(c, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(1f, 4.5f), i % 2 == 0 ? look.col : look.hot, Random.Range(0.03f, 0.06f), Random.Range(0.5f, 0.9f));
            }
        }
    }
}
