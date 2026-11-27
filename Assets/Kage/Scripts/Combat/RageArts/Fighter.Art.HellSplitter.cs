using UnityEngine;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Hell Splitter</b> (鬼斬, Onikiri; the JS <c>hellSplitter</c>, with more fire on the owner's ask): the cleaver splits the floor.
        /// <list type="number">
        /// <item><b>The demon aura</b> (f0–30): a ring of hell runes under Kage, fire wound round him in a helix, two horns of flame over
        /// his head, heat rings off the floor, the floor cracking red under his feet, the ground shuddering.</item>
        /// <item><b>The leap</b> (f30): up and over to the foe in a trail of fire, the cleaver raised; <b>the fall</b> (f47) straight down
        /// behind a giant flaming crescent.</item>
        /// <item><b>Hell split</b> (the landing): the floor splits open under the cleaver; a dome of fire bursts off it, molten droplets and
        /// rock are thrown up, fire runs out along the fissures both ways, the foe is thrown (HELL SPLIT). The world runs again; embers rise
        /// off the burning floor.</item>
        /// <item><b>Lava pillars</b> (7, 16 and 24 frames after the landing): columns erupt along the fissures at 1.3, 2.6 and 3.9 m each
        /// way, and every foe standing on one is thrown up.</item>
        /// </list>
        /// </summary>
        sealed class HellSplitterArt : CinematicArt
        {
            public override string Id => HellSplitter;
            const int Leap = 30, Fall = 47;
            int _land = -1;
            float _x;
            static readonly float[] PillarDx = { 1.3f, -1.3f, 2.6f, -2.6f, 3.9f, -3.9f };
            static readonly int[] PillarAt = { 7, 7, 16, 16, 24, 24 };
            CleaverMoves _m;

            public override void Init()
            {
                total = 124; closeIn = Leap; cutIn = 40; closeSize = 1.8f; exchangeSize = 2.9f;
                _m = k.moves as CleaverMoves;
                if (_m == null) { Hold(0, PoseLib.Stance(), 8); return; }
                int strike = _m.overhead.hits[0].from;
                Play(0, _m.overhead, 0, strike - 8, Leap);                     // the cleaver raised behind the head
                Play(Leap, _m.overhead, strike - 8, strike - 5, Fall - Leap);
                Play(Fall, _m.overhead, strike - 5, strike - 1, 8);
            }

            public override void Start()
            {
                var look = Look;
                Vfx.I?.RuneCircle(new Vector2(k.Pos.x, 0.02f), 1.25f, look.col, look.hot, 2.2f);       // a ring of hell-fire runes under him
            }

            Vector2 Head => k.rig.World(B.Head, Vector2.zero);

            public override float CamY(int f) => f >= Leap && _land < 0 ? CamSize(f) * 0.74f : base.CamY(f);

            public override void Tick(int f)
            {
                var look = Look;
                trail = f >= Fall && (_land < 0 || f < _land + 5);
                if (f < Leap)
                {
                    k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.8f, k.rb.linearVelocity.y);
                    Vfx.I?.FlameTongue(k.Pos + new Vector2(Random.Range(-0.5f, 0.5f), 0.05f), look.col, look.hot);
                    // the demon aura: fire wound up round him in a helix, and two horns of flame over his head
                    float a = f * 0.42f;
                    Vfx.I?.FlameTongue(k.Pos + new Vector2(Mathf.Cos(a) * 0.55f, 0.1f + (f % 20) / 20f * 1.6f), look.col, look.hot);
                    if (f > 8)
                        for (int s = -1; s <= 1; s += 2)
                            Vfx.I?.Embers(Head + new Vector2(s * 0.14f, 0.2f), new Vector2(s * 0.5f, 3.2f), f % 2 == 0 ? look.col : look.hot, 0.07f, 0.28f);
                    if (f % 10 == 0) Vfx.I?.Ring(new Vector2(k.Pos.x, 0.04f), 0.2f, 1.6f, 0.05f, look.col.WithA(0.7f), 0.45f, false, 0.22f, 1.8f);   // heat rings off the floor
                    if (f == 9) Vfx.I?.GroundCracks(k.Pos.x, 6, 0.5f, 1.05f, look.col, look.hot, 1.4f);
                    if (f % 6 == 0) { GameFeel.Shake(0.1f); KageGame.I?.Audio?.Play(SfxKind.Thud, 0.35f); }
                    return;
                }
                if (_land < 0 && f % 3 == 0)
                {
                    Vfx.I?.Ghost(k.rig, look.col.WithA(0.35f), 0.25f);                 // a fire trail through the leap
                    Vfx.I?.Embers(k.Pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1.6f)), new Vector2(-k.rb.linearVelocity.x * 0.15f, -0.5f), look.col, 0.06f, 0.4f);
                }
                if (f == Fall + 2)
                    Vfx.I?.ScytheCrescent(k.Pos + new Vector2(k.facing * 0.5f, 1.1f), -32f * k.facing, 1.5f, k.facing, look.col, look.hot, look.deep);   // the flaming arc of the fall
                if (f == Leap)
                {
                    float dest = Live ? tgt.Pos.x - SideOfTarget * 0.62f : k.Pos.x + k.facing * 1.9f;
                    dest = Mathf.Clamp(dest, k.StageMin, k.StageMax);
                    k.rb.linearVelocity = new Vector2((dest - k.Pos.x) / 0.3f, 9.5f);        // most of the way there by the fall
                    Vfx.I?.Dust(k.Pos, 1.6f, 0f);
                    KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.8f);
                }
                if (f == Fall) k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.5f, -16f);
                if (_land < 0 && f > Fall && (k.Grounded || f > Fall + 30)) TheSplit(f, look);
                if (_land >= 0)
                {
                    k.rb.linearVelocity = new Vector2(0f, k.rb.linearVelocity.y);
                    for (int i = 0; i < PillarDx.Length; i++) if (f == _land + PillarAt[i]) Pillar(_x + PillarDx[i], look);
                    int since = f - _land;
                    // fire runs out along the fissures, both ways, faster than the pillars follow it
                    if (since < 30 && since % 2 == 0)
                        for (int s = -1; s <= 1; s += 2)
                        {
                            float x = _x + s * (0.3f + since * 0.16f);
                            if (x > k.StageMin - 0.5f && x < k.StageMax + 0.5f) Vfx.I?.FlameTongue(new Vector2(x, 0.03f), look.col, look.hot);
                        }
                    // embers rising off the burning floor across the frame
                    if (since < 60 && since % 2 == 0)
                        Vfx.I?.Embers(new Vector2(CamX(f) + Random.Range(-1f, 1f) * CamSize(f) * 1.7f, Random.Range(0f, 0.4f)), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2.6f)), Random.value < 0.5f ? look.col : look.hot, Random.Range(0.03f, 0.06f), Random.Range(0.6f, 1.1f));
                }
            }

            void TheSplit(int f, RageArtLook look)
            {
                _land = f;
                _x = k.Pos.x + k.facing * 0.62f;
                if (_m != null) Play(f, _m.overhead, _m.overhead.hits[0].from - 1, _m.overhead.total, 36);
                Vfx.I?.HellSplit(new Vector2(_x, 0f), k.facing, look.col, look.hot, look.deep);
                Vfx.I?.GroundCracks(_x, 14, 1.6f, 3.8f, look.col, look.hot, 1.6f);
                Vfx.I?.Column(new Vector2(_x, 0f), 1.3f, 1.1f, look.col, look.hot, look.deep, 0.55f);       // a dome of fire bursting off the split
                Vfx.I?.Ring(new Vector2(_x, 0.04f), 0.2f, 4.2f, 0.12f, look.hot.WithA(0.8f), 0.6f, true, 0.12f, 2.2f);
                for (int i = 0; i < 24; i++)                                                                     // molten droplets
                    Vfx.I?.Spark(new Vector2(_x + Random.Range(-0.4f, 0.4f), 0.1f), new Vector2(Random.Range(-5f, 5f), Random.Range(3f, 9f)), i % 3 == 0 ? look.hot : look.col, Random.Range(0.04f, 0.08f), Random.Range(0.6f, 1.1f));
                GameFeel.Flash(look.col.WithA(0.16f), 0.12f);
                if (Live && Mathf.Abs(tgt.Pos.x - _x) < 2.4f) Blow(22, new Vector2(3.1f, 6.2f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.6f, keepMotion: true);
                BlowAround(_x, 2.4f, 12, new Vector2(2.4f, 5.2f), true, false);
                Finish(new Vector2(_x, 0.7f), hitstop: 8, shake: 1f, punch: 0.14f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 1f);
            }

            void Pillar(float x, RageArtLook look)
            {
                if (x < k.StageMin - 0.5f || x > k.StageMax + 0.5f) return;
                Vfx.I?.LavaPillar(new Vector2(x, 0f), 2.4f, look.col, look.hot, look.deep);
                for (int i = 0; i < 6; i++)                                                                      // rock thrown up with it
                    Vfx.I?.Spark(new Vector2(x + Random.Range(-0.2f, 0.2f), 0.1f), new Vector2(Random.Range(-2f, 2f), Random.Range(4f, 8f)), new Color(0.12f, 0.1f, 0.09f, 1f), Random.Range(0.06f, 0.1f), Random.Range(0.6f, 1f));
                Vfx.I?.Ring(new Vector2(x, 0.04f), 0.1f, 0.9f, 0.05f, look.hot, 0.3f, false, 0.2f, 1.8f);
                BlowAround(x, 0.5f, 5, new Vector2(1.2f, 4.9f), true, true);
                GameFeel.Shake(0.3f);
                KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 0.6f);
            }
        }
    }
}
