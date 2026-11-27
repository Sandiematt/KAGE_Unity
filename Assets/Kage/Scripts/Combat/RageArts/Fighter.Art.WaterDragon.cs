using UnityEngine;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Rising Water Dragon</b> (水龍, Mizuchi; the JS <c>waterDragon</c>): the spear's dragon.
        /// <list type="number">
        /// <item><b>The flood</b> (f0–36): rain falls, a sheet of water spreads over the floor, and water is drawn up off it into the
        /// spear's head; the head glints (f25).</item>
        /// <item><b>The dragon</b> (f36): it bursts from the spear, flies to the foe (f53) and coils round it twice, a blow each time it
        /// passes in front, lifting the foe off the floor (f53–87).</item>
        /// <item><b>The rise and the dive</b> (f87–108): it climbs high above the foe, the foe carried up with it, and dives.</item>
        /// <item><b>Mizuchi</b> (f108): the dragon crashes down on the foe in a tidal splash and slams it into the floor (MIZUCHI).</item>
        /// </list>
        /// The dragon is a head flying a scripted path; its body is the head's trail (<see cref="Vfx.WaterDragon"/>), driven from here on
        /// Kage's ticks so it always stays in step with the blows.
        /// </summary>
        sealed class WaterDragonArt : CinematicArt
        {
            public override string Id => WaterDragon;
            const int Burst = 36, Arrive = 53, CoilEnd = 87, Rise = 101, Crash = 108;
            readonly Vfx.DragonBody _dragon = new();
            Vector2 _from, _anchor;
            float _side;
            int _coilHits;
            SpearMoves _m;

            public override void Init()
            {
                total = 140; closeIn = Burst; cutIn = 42; closeSize = 1.9f; exchangeSize = 3.1f;
                _m = k.moves as SpearMoves;
                if (_m == null) { Hold(0, PoseLib.Stance(), 8); return; }
                int hit = _m.thrust.hits[0].from;
                Play(0, _m.thrust, 0, hit - 2, Burst);                         // the spear drawn back, water gathering at its head
                Play(Burst, _m.thrust, hit - 2, hit + 3, 6);                    // the thrust that frees the dragon
                Play(Burst + 6, _m.thrust, hit + 3, hit + 4, CoilEnd - Burst - 6);
                int vh = _m.vault.hits[0].from;
                Play(CoilEnd, _m.vault, vh - 2, vh + 6, Crash - CoilEnd);       // the spear raised after the rising dragon
                Play(Crash, _m.thrust, hit + 4, _m.thrust.total, 30);
            }

            public override void Start()
            {
                _side = SideOfTarget;
                Vfx.I?.Flood(new Vector2(k.Pos.x, 0f), 7.5f, Look.col, Look.hot, total / 60f + 0.4f);
            }

            public override float CamY(int f) => f >= CoilEnd && f < Crash + 10 ? CamSize(f) * 0.86f : base.CamY(f);

            Vector2 Tip => k.rig.World(B.Sword, k.BladeTipLocal);

            public override void Tick(int f)
            {
                var look = Look;
                k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.8f, k.rb.linearVelocity.y);
                trail = f >= Burst && f < Burst + 6;
                if (f % 2 == 0) Vfx.I?.Rain(new Vector2(CamX(f), CamY(f)), CamSize(f) * 2.2f, CamSize(f) * 1.1f, look.hot);
                if (f < Burst)
                {
                    if (f % 2 == 0)
                    {
                        Vector2 floor = new(k.Pos.x + Random.Range(-1.9f, 1.9f), 0.02f);
                        Vfx.I?.Spark(floor, (Tip - floor) * 1.7f, Random.value < 0.5f ? look.hot : look.col, 0.035f, 0.55f);
                    }
                    if (f == 25) { Vfx.I?.Glint(Tip, look.hot); KageGame.I?.Audio?.Play(SfxKind.Crescent, 0.6f); }
                    return;
                }
                if (f == Burst)
                {
                    _from = Tip;
                    Vfx.I?.WaterDragon(_dragon, look.col, look.hot, look.deep, (total - Burst) / 60f + 0.5f);
                    Vfx.I?.Splash(_from, new Vector2(k.facing, 0.2f), look.col, look.hot, 1.2f);
                    Bind(150);
                    KageGame.I?.Audio?.Play(SfxKind.Whoosh, 1f);
                    KageGame.I?.Audio?.Play(SfxKind.Gong, 0.4f);
                }
                _anchor = Live ? tgt.Pos + Vector2.up * 1.05f : new Vector2(AnchorX, 1.05f);
                Vector2 head = HeadAt(f);
                _dragon.Push(head, Front(f));
                if (f % 2 == 0) Vfx.I?.Spark(head + Random.insideUnitCircle * 0.12f, Random.insideUnitCircle * 0.8f, Random.value < 0.5f ? look.hot : look.col, 0.03f, 0.4f);

                if (f == Arrive) { Blow(6, new Vector2(0.3f, 0f), stun: 150, hitstop: 3, shake: 0.3f); Vfx.I?.Splash(Chest, new Vector2(_side, 0f), look.col, look.hot, 1f); }
                if (f > Arrive && f <= CoilEnd)
                {
                    float u = (f - Arrive) / (float)(CoilEnd - Arrive);
                    int n = Mathf.FloorToInt(u * 5f);
                    if (n > _coilHits && _coilHits < 5)
                    {
                        _coilHits = n;
                        Blow(3, Vector2.zero, stun: 150, hitstop: 2, shake: 0.18f);
                        Vfx.I?.Splash(Chest, new Vector2(-_side, 0.4f), look.col, look.hot, 0.7f);
                        KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 0.45f);
                    }
                    Lift(Mathf.Lerp(0.02f, 0.9f, KMath.Apply(Ease.InOutQuad, u)));
                }
                if (f > CoilEnd && f < Rise) Lift(Mathf.Lerp(0.9f, 1.7f, (f - CoilEnd) / (float)(Rise - CoilEnd)));
                if (f == Crash) TheCrash(look);
                if (f > Crash) _dragon.gone = Mathf.Clamp01((f - Crash) / 26f);
            }

            /// <summary>The bound foe carried up in the coils (the world is held, so its body stays where it is put).</summary>
            void Lift(float y)
            {
                if (!Live || tgt.rb == null) return;
                tgt.rb.position = new Vector2(tgt.rb.position.x, y);
                tgt.transform.position = new Vector3(tgt.rb.position.x, y, 0f);
                tgt.rb.linearVelocity = Vector2.zero;
                tgt._worldVelocity = Vector2.zero;
            }

            /// <summary>The head's scripted flight (the JS <c>dragonStep</c>): launch, two coils round the foe, the rise, the dive.</summary>
            Vector2 HeadAt(int f)
            {
                const float R = 1.35f;
                if (f < Arrive)
                {
                    float k0 = KMath.Apply(Ease.OutCubic, (f - Burst) / (float)(Arrive - Burst));
                    Vector2 to = _anchor + new Vector2(_side * 0.6f, 0.45f);
                    return Vector2.Lerp(_from, to, k0) + Vector2.up * Mathf.Sin(k0 * Mathf.PI) * 0.55f;
                }
                if (f < CoilEnd)
                {
                    float u = (f - Arrive) / (float)(CoilEnd - Arrive), a = u * Mathf.PI * 2f * 1.15f;
                    return _anchor + new Vector2(Mathf.Cos(a) * R * _side, 0.26f + Mathf.Sin(a) * R * 0.45f + u * 0.8f);
                }
                Vector2 coilEnd = _anchor + new Vector2(Mathf.Cos(Mathf.PI * 2.3f) * R * _side, 0.26f + Mathf.Sin(Mathf.PI * 2.3f) * R * 0.45f + 0.8f);
                if (f < Rise)
                {
                    float k1 = KMath.Apply(Ease.InOutQuad, (f - CoilEnd) / (float)(Rise - CoilEnd));
                    return Vector2.Lerp(coilEnd, new Vector2(_anchor.x, _anchor.y + 4.2f), k1);
                }
                if (f < Crash)
                {
                    float k2 = KMath.Apply(Ease.InCubic, (f - Rise) / (float)(Crash - Rise));
                    return Vector2.Lerp(new Vector2(_anchor.x, _anchor.y + 4.2f), new Vector2(_anchor.x, 0.05f), k2);
                }
                return new Vector2(_anchor.x, 0.05f);
            }

            /// <summary>The coil passes in front of the foe on its near half and behind it on the far half.</summary>
            bool Front(int f)
            {
                if (f < Arrive || f >= CoilEnd) return true;
                float u = (f - Arrive) / (float)(CoilEnd - Arrive), a = u * Mathf.PI * 2f * 1.15f;
                return Mathf.Sin(a) < 0.2f;
            }

            void TheCrash(RageArtLook look)
            {
                float x = AnchorX;
                // the foe was carried up bound (in hit-stun, as if standing, so its body stays where the coils put it); it doesn't
                // tick while the world is held, so tell the crash it is in the air: the blow spikes it into the floor
                if (Live && tgt.rb != null && tgt.rb.position.y > 0.05f) tgt.Grounded = false;
                Vfx.I?.TidalCrash(new Vector2(x, 0f), look.col, look.hot, look.deep, 1.3f);
                Blow(16, new Vector2(2.6f, -8f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.6f, keepMotion: true);   // slammed into the floor
                Finish(new Vector2(x, 0.8f), hitstop: 8, shake: 1f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 1f);
            }

            public override void Stop() { _dragon.gone = 1f; }
        }
    }
}
