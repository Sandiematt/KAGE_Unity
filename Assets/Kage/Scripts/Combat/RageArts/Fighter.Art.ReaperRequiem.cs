using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public partial class Fighter
    {
        /// <summary>
        /// <b>Reaper's Requiem</b> (闇鎌, Yamigama; the JS <c>reaperRequiem</c>): the reaper's shadows.
        /// <list type="number">
        /// <item><b>The dark</b> (f0–36): the dark closes in and eyes open in it; three shadow clones of Kage rise out of the floor round
        /// the foe (f6, f12, f18).</item>
        /// <item><b>The chains</b> (f36): chains from Kage and every clone bind the foe.</item>
        /// <item><b>The cuts</b> (f51, f60, f69): each clone passes through the foe in turn and leaves a dark scythe crescent in it.</item>
        /// <item><b>Requiem</b> (f78 the leap, f96 the fall): the clones fall to ash; Kage leaps and two enormous scythe crescents carve
        /// down through the foe, slamming it into the floor (REQUIEM).</item>
        /// </list>
        /// </summary>
        sealed class ReaperRequiemArt : CinematicArt
        {
            public override string Id => ReaperRequiem;
            const int Chains = 36, Leap = 78, Requiem = 96;
            static readonly float[] CloneDx = { 1.3f, -0.91f, 2.35f };
            static readonly int[] CloneAt = { 6, 12, 18 }, CutAt = { 51, 60, 69 };
            readonly List<Vfx.Clone> _clones = new();
            readonly float[] _cloneX = new float[3];
            ChainMoves _m;
            bool _landed;

            public override void Init()
            {
                total = 134; closeIn = Chains; cutIn = 42; closeSize = 1.8f; exchangeSize = 2.9f;
                _m = k.moves as ChainMoves;
                if (_m == null) { Hold(0, PoseLib.Stance(), 8); return; }
                int lash = _m.lash.hits[0].from, reap = _m.airReap.hits[0].from;
                Play(0, _m.lash, 0, lash - 3, Chains);                          // the sickle drawn back, the chain gathered
                Play(Chains, _m.lash, lash - 3, lash + 4, 8);                   // the chain thrown
                Play(Chains + 8, _m.pull, _m.pull.hits[0].from, _m.pull.hits[0].from + 2, Leap - Chains - 8);   // held taut
                Play(Leap, _m.airReap, 0, reap - 1, Requiem - Leap);
                Play(Requiem, _m.airReap, reap - 1, _m.airReap.total, 26);
            }

            public override void Start()
            {
                float face = k.facing;
                for (int i = 0; i < CloneDx.Length; i++) _cloneX[i] = Mathf.Clamp(AnchorX + CloneDx[i] * face, k.StageMin + 0.3f, k.StageMax - 0.3f);
            }

            Vector2 Hand => k.rig.World(B.HandN, k.def.fistLocal);

            public override void Tick(int f)
            {
                var look = Look;
                trail = (f >= Chains && f < Chains + 8) || (f >= Requiem && f < Requiem + 6);
                if (f < Leap) k.rb.linearVelocity = new Vector2(k.rb.linearVelocity.x * 0.8f, k.rb.linearVelocity.y);

                if (f % 5 == 0 && f < Requiem) Vfx.I?.Eyes(new Vector2(CamX(f), CamY(f)) + new Vector2(Random.Range(-1f, 1f) * CamSize(f) * 1.6f, Random.Range(0.1f, 0.85f) * CamSize(f)), look.col, look.hot);
                for (int i = 0; i < CloneAt.Length; i++) if (f == CloneAt[i]) RiseClone(i, look);
                if (f < Chains)
                {
                    if (f % 3 == 0) foreach (float x in _cloneX) Vfx.I?.Puff(new Vector2(x + Random.Range(-0.25f, 0.25f), 0.05f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.4f)), look.deep.WithA(0.7f), Random.Range(0.35f, 0.6f), 0.7f);
                    return;
                }
                if (f == Chains)
                {
                    Vfx.I?.ChainLine(Hand, Chest, look.col, look.hot, (Leap - Chains) / 60f);
                    for (int i = 0; i < _clones.Count; i++) Vfx.I?.ChainLine(new Vector2(_cloneX[i], 1.05f), Chest, look.col, look.hot, (CutAt[i] - Chains) / 60f + 0.05f);
                    Bind(120);
                    Blow(4, Vector2.zero, stun: 120, hitstop: 4, shake: 0.2f);
                    KageGame.I?.Audio?.Play(SfxKind.Grab, 0.9f);
                }
                for (int i = 0; i < CutAt.Length; i++) if (f == CutAt[i]) CloneCut(i, look);
                if (f == Leap)
                {
                    foreach (var c in _clones) c?.Ash();
                    float dest = Live ? tgt.Pos.x - SideOfTarget * 0.55f : k.Pos.x;
                    k.rb.linearVelocity = new Vector2((dest - k.Pos.x) / 0.4f, 8.5f);
                    KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.8f);
                }
                if (f == Requiem) TheRequiem(look);
                if (f > Requiem && !_landed && k.Grounded) { _landed = true; Vfx.I?.Dust(k.Pos, 1.2f, 0f); k.rb.linearVelocity = Vector2.zero; }
            }

            void RiseClone(int i, RageArtLook look)
            {
                float x = _cloneX[i];
                float face = Mathf.Sign(AnchorX - x);
                var c = Vfx.I?.ShadowClone(k.rig, k.Pos, new Vector2(x, 0.02f), face != k.facing, look.col, look.deep, 0.4f);
                _clones.Add(c);
                Vfx.I?.Ring(new Vector2(x, 0.03f), 0.1f, 0.8f, 0.05f, look.col, 0.5f, false, 0.22f, 1.6f);
            }

            void CloneCut(int i, RageArtLook look)
            {
                Vector2 c = Chest;
                float x = _cloneX[i], side = Mathf.Sign(x - AnchorX);
                if (side == 0f) side = 1f;
                if (i < _clones.Count) _clones[i]?.Ash();
                // the clone has passed through: it stands on the far side for a moment, then goes
                float far = Mathf.Clamp(AnchorX - side * 1.0f, k.StageMin + 0.3f, k.StageMax - 0.3f);
                Vfx.I?.ShadowClone(k.rig, k.Pos, new Vector2(far, 0.02f), -side != k.facing, look.col, look.deep, 0.05f)?.Ash(0.35f);
                Vfx.I?.ScytheCrescent(c, side > 0f ? 28f : -28f, 0.8f, -side, look.col, look.hot, look.deep);
                Blow(5, new Vector2(0.2f, 0.3f), stun: 120, hitstop: 3, shake: 0.22f);
                KageGame.I?.Audio?.Play(SfxKind.Slash, 0.7f);
            }

            void TheRequiem(RageArtLook look)
            {
                k.rb.linearVelocity = new Vector2(0f, -13f);
                Vector2 c = Chest + Vector2.up * 0.1f;
                Vfx.I?.ScytheCrescent(c + Vector2.up * 0.25f, -20f, 2.2f, k.facing, look.col, look.hot, look.deep);
                Vfx.I?.ScytheCrescent(c - Vector2.up * 0.18f, 17f, 1.9f, -k.facing, look.col, look.hot, look.deep);
                Blow(16, new Vector2(3.6f, -11f), stun: 40, knockdown: true, heavy: true, hitstop: 0, shake: 0.6f, keepMotion: true);
                Finish(c, hitstop: 7, shake: 0.9f);
                for (int i = 0; i < 20; i++)
                    Vfx.I?.Spark(c, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(2f, 7f), i % 3 == 0 ? look.hot : look.col, 0.045f, Random.Range(0.3f, 0.7f));
            }

            public override void Stop() { foreach (var c in _clones) c?.Ash(); _clones.Clear(); }
        }
    }
}
