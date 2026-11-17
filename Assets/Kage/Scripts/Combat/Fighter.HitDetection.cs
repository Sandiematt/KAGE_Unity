using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Hit points, hit sweeps, target tests and hit resolution on the attacker's side.</summary>
    public partial class Fighter
    {
        // =====================================================================================
        //  HIT DETECTION
        // =====================================================================================
        readonly List<Vector2> _pts = new();

        void HitPoints(HitPoint hp, List<Vector2> into)
        {
            switch (hp)
            {
                case HitPoint.Fist: into.Add(rig.World(B.HandN, def.fistLocal)); break;
                case HitPoint.Hilt: into.Add(rig.World(B.HandF, def.hiltLocal)); break;
                case HitPoint.FootN: into.Add(rig.World(B.FootN, def.footTipLocal)); into.Add(rig.World(B.FootN, def.heelLocal)); break;
                case HitPoint.FootF: into.Add(rig.World(B.FootF, def.footTipLocal)); into.Add(rig.World(B.FootF, def.heelLocal)); break;
                case HitPoint.ShinN: into.Add(rig.World(B.LegNL, new Vector2(0f, -0.1f))); break;
                case HitPoint.ShinF: into.Add(rig.World(B.LegFL, new Vector2(0f, -0.1f))); break;
                case HitPoint.KneeN: into.Add(rig.World(B.LegNL, Vector2.zero)); break;
                case HitPoint.Blade:
                    for (int i = 1; i <= 5; i++) into.Add(rig.World(B.Sword, Vector2.Lerp(def.bladeBaseLocal, def.bladeTipLocal, i / 5f)));
                    break;
            }
        }

        void CheckHits(HitWindow w)
        {
            _pts.Clear();
            foreach (var hp in w.points) HitPoints(hp, _pts);
            TryTarget(opponent, w);
            TryTarget(extraTarget, w);
        }

        void TryTarget(IHittable t, HitWindow w)
        {
            if (t == null || ReferenceEquals(t, this)) return;
            if (t is Fighter tf && tf.team == team) return;
            foreach (var (hw, ht) in _hitLog) if (hw == w && ReferenceEquals(ht, t)) return;
            foreach (var p in _pts)
            {
                if (!t.Overlap(p, w.radius, w, out var zone, out var contact)) continue;
                _hitLog.Add((w, t));
                int dmg = w.dmg;
                bool crit = RollCrit();
                if (crit) dmg = Mathf.RoundToInt(dmg * 1.5f);
                var info = new HitInfo
                {
                    attacker = this, w = w, dmg = dmg, kb = new Vector2(w.kb.x * facing, w.kb.y), zone = zone, contact = contact, crit = crit,
                    moveId = move != null ? move.id : null, window = move != null ? move.hits.IndexOf(w) : -1,
                };
                var res = t.ReceiveHit(info);
                OnHitResolved(t, info, res);
                return;
            }
        }

        /// <summary>Crits come from the fight's "crit" stream (ADR-0002).</summary>
        bool RollCrit() => ctx != null ? ctx.Random.Stream("crit").Chance(0.1f) : Random.value < 0.1f;

        public void ResolveExternal(IHittable t, HitInfo info, HitResult res) => OnHitResolved(t, info, res);

        /// <summary>Publishes the one event every hit result gets (ADR-0003). A whiff publishes nothing.</summary>
        void PublishHit(IHittable t, HitInfo info, HitResult res)
        {
            if (ctx == null || res == HitResult.Whiff) return;
            var type = res switch { HitResult.Blocked => CombatEventType.Blocked, HitResult.Countered => CombatEventType.Countered, _ => CombatEventType.Hit };
            var target = t as Fighter;
            float dmg = target != null ? target.LastDamageTaken : (res == HitResult.Hit ? info.dmg : 0f);
            var w = info.w;
            ctx.Events.Publish(CombatEvent.HitResolved(ctx.Clock.Frame, type, this, target, FightContext.IdOf(t), info.moveId, info.window,
                dmg, info.crit, w != null ? w.level : HitLevel.Mid, info.zone, info.contact, info.kb));
        }

        void OnHitResolved(IHittable t, HitInfo info, HitResult res)
        {
            var w = info.w;
            Vector2 dir = new Vector2(facing, 0.15f).normalized;
            if (res == HitResult.Hit)
            {
                moveHit = true;
                meter = Mathf.Min(100f, meter + 5f + info.dmg * 0.6f);
                comboCount++;
                GameFeel.Hitstop(w.hitstop + (info.crit ? 3 : 0));
                GameFeel.Shake(w.shake * (info.crit ? 1.5f : 1f));
                if (w.heavy || info.crit) GameFeel.Punch(w.heavy ? 0.12f : 0.07f);
                Vfx.I?.HitBurst(info.contact, dir, w.heavy ? 1.35f : 0.9f + info.dmg * 0.03f, w.sword, fxMain, fxCore, fxDark);
                KageGame.I?.Audio?.Play(w.sword ? SfxKind.Slash : (w.heavy ? SfxKind.HitHeavy : SfxKind.Hit), 0.8f);
            }
            else if (res == HitResult.Blocked)
            {
                GameFeel.Hitstop(3);
                GameFeel.Shake(0.05f);
                Vfx.I?.BlockBurst(info.contact, -dir);
                KageGame.I?.Audio?.Play(SfxKind.Block, 0.7f);
                if (opponent != null && opponent.AtWall)
                    rb.linearVelocity = new Vector2(-facing * 1.6f, rb.linearVelocity.y);
            }
            PublishHit(t, info, res);
            if (t is Fighter f) onHit?.Invoke(this, f, info, res);
        }
    }
}
