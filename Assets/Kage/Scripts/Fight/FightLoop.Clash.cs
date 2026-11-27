using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Weapon clashes (SAN-93, ADR-0004 defence step 1, ADR-0002 phase 4). After every fighter has ticked and before any hit is
    /// resolved, the blades recorded this frame are compared in pairs. Two blades clash when both owners are mid-attack, on
    /// different teams, and the blades' swept paths cross (<see cref="ClashGeometry"/>):
    /// <list type="bullet">
    /// <item>A blade is a run of one window's <see cref="HitPoint.Blade"/> or <see cref="HitPoint.BladeN"/> samples (a twin
    /// blade window has one run for each blade; the chain's lash is part of the Blade line already). Fist, hilt, foot, shin and
    /// knee points never make a run, so a blade passes through a fist or a foot and the hit phase handles it as a plain hit.
    /// Projectiles and hazards aren't sweeps, so they never clash (ADR-0007).</item>
    /// <item>Pairs are tried in sweep order (fighter order, then window order), so the same frame always resolves the same way.
    /// A fighter clashes once a frame: after a clash both moves are over, so neither fighter has a blade left for a third.</item>
    /// <item>Neither takes damage and neither hits: both are out of their moves, and the hit phase drops the sweeps of a
    /// move that is over.</item>
    /// <item>Both recoil (<see cref="Fighter.ClashRecoil"/>) and gain <see cref="ClashTuning.posture"/>
    /// (<see cref="Fighter.AddPosture(float, Fighter, string)"/>, which can break a guard). <see cref="CombatEventType.Clashed"/>
    /// is published once for the pair, then any guard-break the posture caused (<see cref="Fighter.FlushGuardBreak"/>).</item>
    /// <item>The fight freezes for <see cref="ClashTuning.hitstopFrames"/>, the screen shakes, and the <c>VFX_Kage_Clash</c> cue
    /// plays where the blades crossed.</item>
    /// </list>
    /// All numbers are in Resources/Tuning/clash.json.
    /// </summary>
    public partial class FightLoop
    {
        readonly List<ClashGeometry.BladeRun> _blades = new();
        readonly List<ClashGeometry.BladeRun> _runs = new();

        partial void ResolveClashes(List<HitSweep> sweeps)
        {
            CollectBlades(sweeps);
            for (int i = 0; i < _blades.Count; i++)
            {
                for (int j = i + 1; j < _blades.Count; j++)
                {
                    var a = _blades[i];
                    var b = _blades[j];
                    if (a.owner == b.owner || a.owner.team == b.owner.team) continue;
                    if (!StillAttacking(a) || !StillAttacking(b)) continue;     // clashed already this frame
                    if (!ClashGeometry.BladesCross(sweeps, a.start, a.end, b.start, b.end, out var contact)) continue;
                    Clash(a, b, contact);
                }
            }
        }

        /// <summary>The frame's blades (<see cref="ClashGeometry.CollectBlades"/>) whose owner is a live fighter.</summary>
        void CollectBlades(List<HitSweep> sweeps)
        {
            _runs.Clear();
            _blades.Clear();
            ClashGeometry.CollectBlades(sweeps, _runs);
            foreach (var r in _runs) if (r.owner != null && Live(r.owner)) _blades.Add(r);
        }

        static bool StillAttacking(in ClashGeometry.BladeRun r) => r.owner.state == FState.Attack && r.owner.move == r.move;

        void Clash(in ClashGeometry.BladeRun a, in ClashGeometry.BladeRun b, Vector2 contact)
        {
            var ctx = Context;
            var t = Tuning.Get<ClashTuning>();
            Fighter fa = a.owner, fb = b.owner;
            string moveA = a.move.id, moveB = b.move.id;
            int windowA = a.move.hits.IndexOf(a.window);
            fa.ClashRecoil(fb, t);
            fb.ClashRecoil(fa, t);
            float posture = fa.AddPosture(t.posture, fb, moveB);
            fb.AddPosture(t.posture, fa, moveA);
            ctx.Events.Publish(CombatEvent.Clashed(ctx.Clock.Frame, fa, fb, moveA, windowA, moveB, posture, contact));
            fa.FlushGuardBreak();
            fb.FlushGuardBreak();
            GameFeel.Hitstop(t.hitstopFrames);
            Vector2 dir = new Vector2(Mathf.Sign(fb.Pos.x - fa.Pos.x), 0f);
            if (fa.PresentsCombat)
            {
                GameFeel.Shake(t.shake);
                fa.rig.HitShake(-dir, 0.024f, 0.12f);
                fb.rig.HitShake(dir, 0.024f, 0.12f);
            }
            EffectCues.Play(ClashCues.Clash, contact, new CueArgs { owner = fa, dir = dir, scale = 1f });
        }
    }
}
