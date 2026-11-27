using UnityEngine;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Earthsplitter</b> (地裂, Iwakudaki, legendary; the JS <c>goldenQuake</c> Golden Earthbreaker, reworked to the owner's brief:
        /// "it should hit the opponent and crack the ground, and the entire screen should break like glass after the opponent has been
        /// killed; a big animation when triggered"). docs/weapon-styles.md.
        /// <list type="number">
        /// <item><b>The waking</b> (f0–40): a prism ring bursts off Kage (the legendary opening), a gold rune circle lights under him,
        /// rocks float up off the floor across the arena, shafts of light rise out of the ground, the ground shudders, and gold motes
        /// gather into the hammer's head (it glints at f28).</item>
        /// <item><b>The leap</b> (f40) and <b>the plunge</b> (f58), the hammer raised over his head, a gold trail behind.</item>
        /// <item><b>The smash</b> (the landing): the hammer comes down on the foe. The ground cracks open in a gold web, the floating
        /// rocks are flung away, the first wave races out, and <b>the screen cracks</b> like glass from the impact.</item>
        /// <item><b>Two waves</b> (16 and 32 frames after): golden shockwaves race out along the floor, wider each time, striking every
        /// grounded foe.</item>
        /// <item><b>Earthsplitter</b> (46 frames after): a second smash; the last wave throws the foe and stuns every grounded foe on the
        /// stage (the JS Earthshatter rule). EARTHSPLITTER on the screen, in the legendary rainbow.</item>
        /// <item><b>If a blow of the art kills the foe</b>, the whole screen shatters like glass a beat later (<see cref="Hud.ShatterScreen"/>).</item>
        /// </list>
        /// </summary>
        sealed class EarthsplitterArt : CinematicArt
        {
            public override string Id => Earthsplitter;
            const int Leap = 40, Plunge = 58, Wave1 = 16, Wave2 = 32, Final = 46;
            int _land = -1, _shatterAt = -1, _breakAt = -1;
            bool _shattered;
            Vector2 _at;
            Vfx.Rocks _rocks;
            HammerMoves _m;

            public override void Init()
            {
                total = 180; closeIn = Leap; cutIn = 46; closeSize = 1.95f; exchangeSize = 3.2f;
                _m = k.moves as HammerMoves;
                if (_m == null) { Hold(0, PoseLib.Stance(), 8); return; }
                Play(0, _m.smash, 0, 22, Leap);                                   // the hammer raised behind the head, the body arched
                Play(Leap, _m.smash, 22, 24, Plunge - Leap);
                Play(Plunge, _m.smash, 24, HammerMoves.SmashImpact - 2, 5);
            }

            public override void Start()
            {
                var look = Look;
                float mid = Live ? (k.Pos.x + tgt.Pos.x) * 0.5f : k.Pos.x + k.facing * 1.2f;
                Vfx.I?.RuneCircle(new Vector2(k.Pos.x, 0.02f), 1.4f, look.col, look.hot, 2.9f);
                _rocks = Vfx.I?.FloatingRocks(new Vector2(mid, 0f), 3.4f, 13, 4f);
                Vfx.I?.GroundRays(new Vector2(mid, 0f), 3.6f, look.col, 2.2f);
                Vfx.I?.QuakeStomp(new Vector2(k.Pos.x, 0f), 1.6f);
                GameFeel.Shake(0.35f);
            }

            Vector2 Head => k.rig.World(B.Sword, new Vector2(0.84f, 0f));

            public override float CamY(int f) => f >= Leap && _land < 0 ? CamSize(f) * 0.78f : base.CamY(f);

            public override void Tick(int f)
            {
                var look = Look;
                trail = f >= Plunge && (_land < 0 || f < _land + 5);
                if (_shatterAt >= 0 && f >= _shatterAt && !_shattered) TheShatter(look);
                if (f == _breakAt)
                {
                    // the glass gives: the shards fall
                    GameFeel.Shake(0.7f);
                    GameFeel.Punch(0.1f);
                    KageGame.I?.Audio?.Play(SfxKind.Block, 1f);
                    KageGame.I?.Audio?.Play(SfxKind.Thud, 0.7f);
                }
                if (f < Leap)
                {
                    k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.8f, k.rb.linearVelocity.y);
                    if (f % 5 == 0) { GameFeel.Shake(0.1f); KageGame.I?.Audio?.Play(SfxKind.Thud, 0.3f); }
                    if (f % 2 == 0)
                    {
                        Vector2 h = Head, off = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.2f);
                        Vfx.I?.Spark(h + off, -off * 3.2f, Random.value < 0.5f ? look.hot : look.col, 0.04f, 0.3f);
                    }
                    if (f == 28) { Vfx.I?.Glint(Head, look.hot); KageGame.I?.Audio?.Play(SfxKind.Crescent, 0.5f); }
                    return;
                }
                if (f == Leap)
                {
                    float dest = Live ? tgt.Pos.x - SideOfTarget * 0.72f : k.Pos.x + k.facing * 2f;
                    dest = Mathf.Clamp(dest, k.StageMin, k.StageMax);
                    k.rb.linearVelocity = new Vector2((dest - k.Pos.x) / 0.32f, 10.5f);
                    Vfx.I?.Dust(k.Pos, 1.8f, 0f);
                    KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.9f);
                }
                if (_land < 0 && f % 3 == 0) Vfx.I?.Ghost(k.rig, look.col.WithA(0.3f), 0.25f);
                if (f == Plunge) k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.5f, -18f);
                if (_land < 0 && f > Plunge && (k.Grounded || f > Plunge + 30)) TheSmash(f, look);
                if (_land < 0) return;
                k.rb.linearVelocity = new Vector2(0f, k.rb.linearVelocity.y);
                if (f == _land + Wave1) Wave(4.6f, 5, look);
                if (f == _land + Wave2) Wave(6.6f, 5, look);
                if (f == _land + Final) TheFinal(look);
            }

            void TheSmash(int f, RageArtLook look)
            {
                _land = f;
                _at = new Vector2(k.Pos.x + k.facing * 0.85f, 0f);
                if (_m != null)
                {
                    Play(f, _m.smash, HammerMoves.SmashImpact, 42, 18);
                    Play(f + 24, _m.smash, 16, 24, Final - 24 - 6);              // raised again for the second smash
                    Play(f + Final - 6, _m.smash, 24, HammerMoves.SmashImpact, 6);
                    Play(f + Final, _m.smash, HammerMoves.SmashImpact, _m.smash.total, 40);
                }
                Vfx.I?.QuakeStomp(_at, 3.2f);
                Vfx.I?.GroundCracks(_at.x, 18, 1.6f, 4.8f, look.col, look.hot, 2.6f);
                Vfx.I?.QuakeWave(_at, 3.4f);
                Vfx.I?.Star(_at + Vector2.up * 0.5f, 1.5f, Color.white, look.col);
                _rocks?.Fling(_at);
                Vfx.I?.Dust(_at, 2.6f, 0f);
                HitAndWatch(() => Blow(18, new Vector2(0.4f, 0f), stun: 140, heavy: true, hitstop: 6, shake: 0.8f));
                Bind(140);
                BlowAround(_at.x, 2.2f, 10, new Vector2(1.6f, 3.4f), true, false, 60);
                k.ArtHud?.CrackScreen(_at + Vector2.up * 0.6f, look.col);
                GameFeel.Flash(look.hot.WithA(0.2f), 0.1f);
                GameFeel.Hitstop(6);
                GameFeel.Shake(1f);
                GameFeel.Punch(0.12f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 1f);
                KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 0.9f);
            }

            void Wave(float radius, int dmg, RageArtLook look)
            {
                Vfx.I?.QuakeWave(_at, radius, 0.7f);
                Vfx.I?.GroundCracks(_at.x, 8, radius * 0.4f, radius * 0.9f, look.col, look.hot, 1.4f);
                if (Live && tgt.Grounded) HitAndWatch(() => Blow(dmg, new Vector2(0.3f, 0f), stun: 140, heavy: true, hitstop: 3, shake: 0.4f));
                BlowAround(_at.x, radius, dmg, new Vector2(0.6f, 3.0f), false, false, 40);
                GameFeel.Shake(0.55f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 0.8f);
            }

            void TheFinal(RageArtLook look)
            {
                Vector2 c = Live ? tgt.Pos + Vector2.up * 0.9f : _at + Vector2.up * 0.9f;
                Vfx.I?.QuakeStomp(_at, 3.6f);
                Vfx.I?.QuakeWave(_at, 9f, 0.9f);
                Vfx.I?.GroundCracks(_at.x, 22, 2.5f, 7f, look.col, look.hot, 2.2f);
                Vfx.I?.GroundRays(_at, 4.5f, look.hot, 0.9f, 9);
                Vfx.I?.PrismSparks(c, 24, 1.6f);
                HitAndWatch(() => Blow(14, new Vector2(2.6f, 6.4f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.7f, keepMotion: true));
                // the Earthshatter rule: every grounded foe on the stage is stunned
                BlowAround(_at.x, 99f, 12, new Vector2(1.2f, 0.5f), false, false, 84);
                Finish(c, hitstop: 8, shake: 1f, punch: 0.15f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 1f);
            }

            /// <summary>Runs a blow, and when it was the one that killed the foe, the screen shatters a beat later.</summary>
            void HitAndWatch(System.Action blow)
            {
                bool was = Live;
                blow();
                if (was && !Live && _shatterAt < 0) _shatterAt = k._ultFrame + 4;
            }

            void TheShatter(RageArtLook look)
            {
                _shattered = true;
                _breakAt = k._ultFrame + Mathf.RoundToInt(Hud.GlassBreakAt * 60f);
                Vector2 at = tgt != null ? tgt.Pos + Vector2.up * 0.9f : _at + Vector2.up * 0.9f;
                k.ArtHud?.ShatterScreen(at, look.hot);
                GameFeel.Shake(0.6f);
                KageGame.I?.Audio?.Play(SfxKind.Block, 1f);         // the glass gives
                KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 0.8f);
            }

            public override void Stop()
            {
                if (_shatterAt >= 0 && !_shattered) TheShatter(Look);        // a kill near the end still breaks the screen
            }
        }
    }
}
