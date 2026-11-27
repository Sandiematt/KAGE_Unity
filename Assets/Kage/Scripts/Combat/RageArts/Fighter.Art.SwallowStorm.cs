using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Thousand Swallows</b> (雷燕, Tsubame; the JS <c>swallowStorm</c>): lightning in eight directions.
        /// <list type="number">
        /// <item><b>Gathering</b> (f0–30): lightning crawls between the two fangs; both tips glint (f20).</item>
        /// <item><b>Eight strikes</b> (f30–68, one every 5 frames): Kage flashes from spot to spot round the foe (on the floor, in the air
        /// above it, behind it), and each strike runs a bolt through the foe to the far side; each spot keeps a lightning afterimage.</item>
        /// <item><b>Step out</b> (f74): Kage on the floor behind the foe, every afterimage turned to it.</item>
        /// <item><b>Raijin</b> (f87): every afterimage strikes at once (a bolt from each), a lightning X through the foe; it is thrown.</item>
        /// </list>
        /// </summary>
        sealed class SwallowStormArt : CinematicArt
        {
            public override string Id => SwallowStorm;
            const int Strikes = 30, Every = 5, StepOut = 74, Raijin = 87;
            // the eight spots round the foe (metres from its feet: the JS body units at 115 px a metre)
            static readonly Vector2[] Spots =
            {
                new(-1.09f, 0f), new(1.09f, 0f), new(-0.83f, 1.39f), new(0.91f, 1.22f), new(-1.22f, 0.61f), new(1.22f, 0.43f), new(0.09f, 1.78f), new(-1.04f, 0f),
            };
            readonly List<Vector2> _ghosts = new();
            Vector2 _hold;
            bool _holding;

            public override void Init()
            {
                total = 118; closeIn = Strikes; cutIn = 40; closeSize = 1.7f; exchangeSize = 2.8f;
                var m = k.moves as TwinMoves;
                if (m == null) { Hold(0, PoseLib.Stance(), 8); return; }
                Play(0, m.fang4, 0, m.fang4.hits[0].from - 1, Strikes);          // both fangs drawn back: the gathering
                var order = new[] { m.fang1, m.fang2, m.airSpin, m.fang3, m.fang1, m.fang4, m.airSpin, m.fang2 };
                for (int i = 0; i < Spots.Length; i++) PlayStrike(Strikes + i * Every, order[i], Every);
                Play(StepOut, m.crossSlash, 0, m.crossSlash.hits[0].from - 1, Raijin - StepOut);
                Play(Raijin, m.crossSlash, m.crossSlash.hits[0].from - 1, m.crossSlash.total, 26);
            }

            /// <summary>A strike's own frames: from just before its first blow to just after its last, over <paramref name="len"/> art frames.</summary>
            void PlayStrike(int at, MoveDef m, int len)
            {
                if (m == null || m.hits.Count == 0) return;
                Play(at, m, Mathf.Max(0, m.hits[0].from - 2), m.hits[m.hits.Count - 1].to + 2, len);
            }

            public override void Start() { }

            public override float CamY(int f) => f >= Strikes && f < StepOut ? CamSize(f) * 0.72f : base.CamY(f);

            public override void Tick(int f)
            {
                var look = Look;
                trail = (f >= Strikes && f < StepOut) || (f >= Raijin && f < Raijin + 6);
                if (_holding) { k.rb.position = _hold; k.rb.linearVelocity = Vector2.zero; }

                if (f < Strikes)
                {
                    k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.8f, k.rb.linearVelocity.y);
                    if (f % 3 == 0) Vfx.I?.Bolt(k.rig.World(B.Sword, k.BladeTipLocal), k.rig.World(B.HandN, k.NearBladeTipLocal), 0.02f, 0.08f, look.col, 6, -1f, 1);
                    if (f == 20)
                    {
                        Vfx.I?.Glint(k.rig.World(B.Sword, k.BladeTipLocal), look.hot);
                        Vfx.I?.Glint(k.rig.World(B.HandN, k.NearBladeTipLocal), look.hot);
                        KageGame.I?.Audio?.Play(SfxKind.Crescent, 0.8f);
                    }
                    return;
                }
                int i = (f - Strikes) / Every;
                if (f < StepOut && (f - Strikes) % Every == 0 && i < Spots.Length) Strike(i, look);
                if (f == StepOut)
                {
                    _holding = false;
                    float side = SideOfTarget;
                    k.ArtPlace(AnchorX - side * 1.3f, 0.02f, side);
                    Vfx.I?.SpeedLines(k.Pos + Vector2.up, k.facing, look.hot, 8);
                    KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.8f);
                }
                if (f == Raijin) TheRaijin(look);
            }

            void Strike(int i, RageArtLook look)
            {
                Vector2 c = Chest, feet = Live ? tgt.Pos : new Vector2(AnchorX, 0f);
                Vector2 at = feet + Spots[i];
                at.x = Mathf.Clamp(at.x, k.StageMin, k.StageMax);
                float face = Mathf.Sign(feet.x - at.x);
                k.ArtPlace(at.x, at.y, face == 0f ? k.facing : face);
                _hold = k.rb.position; _holding = true;
                Vector2 from = k.Pos + Vector2.up * 0.95f;
                _ghosts.Add(from);
                Vfx.I?.Ghost(k.rig, look.col.WithA(0.55f), 1.25f - i * 0.09f);
                Vector2 far = c + (c - from) * 0.9f;
                Vfx.I?.Bolt(from, far, 0.05f, 0.2f, look.col, 12, -1f, 2);
                Vfx.I?.SlashLine(from, far, look.col, look.hot, 0.2f, 0.04f);
                Blow(3, new Vector2(0.3f, 0.5f), stun: 120, hitstop: 2, shake: 0.16f);
                if (i == 0) Bind(120);
                Vfx.I?.HitBurst(c, (far - from).normalized, 0.8f, true, look.col, look.hot, look.deep);
                KageGame.I?.Audio?.Play(SfxKind.SwordWhoosh, 0.6f);
            }

            void TheRaijin(RageArtLook look)
            {
                Vector2 c = Chest;
                foreach (var g in _ghosts) Vfx.I?.Bolt(g, c, 0.06f, 0.24f, look.col, 12, -1f, 2);
                Vfx.I?.Bolt(c + new Vector2(-1.5f, 1.3f), c + new Vector2(1.5f, -1.05f), 0.11f, 0.32f, look.col, 16, -1f, 3);
                Vfx.I?.Bolt(c + new Vector2(-1.5f, -1.05f), c + new Vector2(1.5f, 1.3f), 0.11f, 0.32f, look.col, 16, -1f, 3);
                Vfx.I?.Ring(c, 0.1f, 2.1f, 0.1f, look.col, 0.42f, true, 1f, 2.4f);
                Blow(14, new Vector2(4.5f, 5.4f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.5f, keepMotion: true);
                Finish(c, hitstop: 7, shake: 0.9f);
                for (int i = 0; i < 30; i++)
                    Vfx.I?.Spark(c, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(3f, 9f) + Vector2.up, i % 3 == 0 ? look.hot : look.col, 0.05f, Random.Range(0.3f, 0.7f));
                KageGame.I?.Audio?.Play(SfxKind.Crescent, 1f);
                _ghosts.Clear();
            }

            public override void Stop() { _holding = false; }
        }
    }
}
