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
                case HitPoint.FistF: into.Add(rig.World(B.HandF, def.BindLocal(B.Sword))); break;
                case HitPoint.FootN: into.Add(rig.World(B.FootN, def.footTipLocal)); into.Add(rig.World(B.FootN, def.heelLocal)); break;
                case HitPoint.FootF: into.Add(rig.World(B.FootF, def.footTipLocal)); into.Add(rig.World(B.FootF, def.heelLocal)); break;
                case HitPoint.ShinN: into.Add(rig.World(B.LegNL, new Vector2(0f, -0.1f))); break;
                case HitPoint.ShinF: into.Add(rig.World(B.LegFL, new Vector2(0f, -0.1f))); break;
                case HitPoint.KneeN: into.Add(rig.World(B.LegNL, Vector2.zero)); break;
                case HitPoint.Blade:
                {
                    // five points along a katana (one every 14 cm); a longer blade gets one about every 18 cm so a shaft has no gaps
                    Vector2 b0 = BladeBaseLocal, b1 = BladeTipLocal;
                    int n = Mathf.Max(5, Mathf.CeilToInt((b1 - b0).magnitude / 0.18f));
                    for (int i = 1; i <= n; i++) into.Add(rig.World(B.Sword, Vector2.Lerp(b0, b1, i / (float)n)));
                }
                    break;
                case HitPoint.BladeN:
                    for (int i = 1; i <= 5; i++) into.Add(rig.World(B.HandN, Vector2.Lerp(NearBladeBaseLocal, NearBladeTipLocal, i / 5f)));
                    break;
            }
        }

        // =====================================================================================
        //  SWEPT HITS (SAN-65): sweeps recorded in the tick, cast against hurtboxes in the hit phase
        // =====================================================================================
        sealed class SweepPrev { public int frame; public readonly List<Vector2> pts = new(); }
        readonly Dictionary<HitWindow, SweepPrev> _sweepPrev = new();
        readonly List<HitPoint> _ptKinds = new();
        static readonly List<Hurtbox> BoxTmp = new();
        static readonly List<HurtboxHit> SweepTmp = new();
        readonly List<(IHittable t, int order)> _targets = new();

        /// <summary>An active window on this frame (the rig is posed at this exact frame).</summary>
        void WindowActive(HitWindow w)
        {
            if (ctx == null || ctx.LegacyHits) CheckHits(w);
            else RecordSweeps(w);
        }

        /// <summary>Records one sweep per sample point: from last frame's position (if the window was active then) to this frame's.</summary>
        void RecordSweeps(HitWindow w)
        {
            _pts.Clear();
            _ptKinds.Clear();
            foreach (var hp in w.points)
            {
                HitPoints(hp, _pts);
                while (_ptKinds.Count < _pts.Count) _ptKinds.Add(hp);
            }
            if (!_sweepPrev.TryGetValue(w, out var prev)) _sweepPrev[w] = prev = new SweepPrev { frame = int.MinValue };
            bool continues = prev.frame == _moveTicks - 1 && prev.pts.Count == _pts.Count;
            for (int k = 0; k < _pts.Count; k++)
                ctx.Sweeps.Add(new HitSweep
                {
                    owner = this, move = move, window = w, point = _ptKinds[k], sample = k,
                    from = continues ? prev.pts[k] : _pts[k], to = _pts[k], radius = w.radius,
                });
            prev.frame = _moveTicks;
            prev.pts.Clear();
            prev.pts.AddRange(_pts);
        }

        /// <summary>
        /// Fight loop hit phase: resolves this fighter's sweeps <c>[start, end)</c> of this frame, window by
        /// window. A window whose move was interrupted earlier in this phase (this fighter was hit first) is dropped.
        /// </summary>
        public void ResolveSweeps(List<HitSweep> sweeps, int start, int end)
        {
            int i = start;
            while (i < end)
            {
                var w = sweeps[i].window;
                var m = sweeps[i].move;
                int j = i + 1;
                while (j < end && sweeps[j].window == w && sweeps[j].move == m) j++;
                if (state == FState.Attack && move == m) ResolveWindow(sweeps, i, j);
                i = j;
            }
        }

        void ResolveWindow(List<HitSweep> sweeps, int i, int j)
        {
            var w = sweeps[i].window;
            // every owner any sample touches, in a fixed order: fighters in tick order, then props
            _targets.Clear();
            for (int k = i; k < j; k++)
            {
                var s = sweeps[k];
                Hurtbox.Overlap(s.to, s.radius, BoxTmp);
                foreach (var b in BoxTmp) AddTarget(b.Owner);
                Hurtbox.Sweep(s.from, s.to, s.radius, SweepTmp);
                foreach (var h in SweepTmp) AddTarget(h.box.Owner);
            }
            _targets.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var (t, _) in _targets)
            {
                if (t == null || ReferenceEquals(t, this)) continue;
                if (t is Fighter tf && tf.team == team) continue;
                if (Logged(w, t)) continue;
                if (!FindContact(t, sweeps, i, j, out var zone, out var contact)) continue;
                ResolveContact(t, w, zone, contact);
                if (state != FState.Attack || move != sweeps[i].move) return;   // countered: the window is over
            }
        }

        void AddTarget(IHittable t)
        {
            if (t == null) return;
            foreach (var (x, _) in _targets) if (ReferenceEquals(x, t)) return;
            int fi = t is Fighter f ? ctx.Fighters.IndexOf(f) : -1;
            int pi = fi < 0 ? ctx.Props.IndexOf(t) : -1;
            int order = fi >= 0 ? fi : pi >= 0 ? 10000 + pi : 20000 + _targets.Count;
            _targets.Add((t, order));
        }

        bool Logged(HitWindow w, IHittable t)
        {
            foreach (var (hw, ht) in _hitLog) if (hw == w && ReferenceEquals(ht, t)) return true;
            return false;
        }

        /// <summary>
        /// Where the window touches <paramref name="t"/>. The first sample (in hit-point order) that rests in
        /// one of its circles this frame wins, with the lowest-index circle's zone, exactly as the point
        /// tests did; otherwise the earliest touch along any sample's path since last frame.
        /// </summary>
        bool FindContact(IHittable t, List<HitSweep> sweeps, int i, int j, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = default;
            for (int k = i; k < j; k++)
            {
                var s = sweeps[k];
                Hurtbox.Overlap(s.to, s.radius, BoxTmp);
                Hurtbox best = null;
                foreach (var b in BoxTmp) if (ReferenceEquals(b.Owner, t) && (best == null || b.Index < best.Index)) best = b;
                if (best == null) continue;
                zone = best.Zone;
                contact = Vector2.Lerp(s.to, best.Centre, s.radius / (s.radius + best.Radius));
                return true;
            }
            float bestFrac = float.MaxValue;
            int bestIndex = int.MaxValue;
            for (int k = i; k < j; k++)
            {
                var s = sweeps[k];
                Hurtbox.Sweep(s.from, s.to, s.radius, SweepTmp);
                foreach (var h in SweepTmp)
                {
                    if (!ReferenceEquals(h.box.Owner, t)) continue;
                    if (h.fraction > bestFrac || (h.fraction == bestFrac && h.box.Index >= bestIndex)) continue;
                    bestFrac = h.fraction;
                    bestIndex = h.box.Index;
                    zone = h.box.Zone;
                    contact = h.point;
                }
            }
            return bestFrac != float.MaxValue;
        }

        /// <summary>
        /// A contact goes through the defence order (ADR-0004): the pre-hit checks (perfect dodge), then
        /// whether the target can be hit now, then the target's own checks in ReceiveHit.
        /// </summary>
        void ResolveContact(IHittable t, HitWindow w, HitZone zone, Vector2 contact)
        {
            var info = new HitInfo
            {
                attacker = this, w = w, dmg = DamageCalc.For(this, t, w.dmg, false, true), kb = new Vector2(w.kb.x * facing, w.kb.y),
                zone = zone, contact = contact, moveId = move != null ? move.id : null, window = move != null ? move.hits.IndexOf(w) : -1,
            };
            if (t is Fighter tf)
            {
                var c = new DefenceCheck { info = info };
                if (tf.PreHit(ref c))
                {
                    _hitLog.Add((w, t));
                    OnHitResolved(t, c.info, c.result);
                    return;
                }
            }
            if (!t.HittableBy(w)) return;
            _hitLog.Add((w, t));
            if (RollCrit())
            {
                info.crit = true;
                info.dmg = DamageCalc.For(this, t, w.dmg, true, true);
            }
            var res = t.ReceiveHit(info);
            OnHitResolved(t, info, res);
        }

        // ---- legacy resolution (legacy switch): point tests inside the attacker's tick ----
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
                bool crit = RollCrit();
                int dmg = DamageCalc.For(this, t, w.dmg, crit, true);
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
            var type = res switch
            {
                HitResult.Blocked => CombatEventType.Blocked, HitResult.Countered => CombatEventType.Countered,
                HitResult.Parried => CombatEventType.Parried, HitResult.Dodged => CombatEventType.PerfectDodged, _ => CombatEventType.Hit,
            };
            var target = t as Fighter;
            float dmg = target != null ? target.LastDamageTaken : (res == HitResult.Hit ? info.dmg : 0f);
            // posture the target took from this hit (SAN-86); a parry's or a clash's posture is the checks' own business
            float posture = target != null && res is HitResult.Hit or HitResult.Blocked ? target.LastPostureTaken : 0f;
            var w = info.w;
            ctx.Events.Publish(CombatEvent.HitResolved(ctx.Clock.Frame, type, this, target, FightContext.IdOf(t), info.moveId, info.window,
                dmg, info.crit, w != null ? w.level : HitLevel.Mid, info.zone, info.contact, info.kb, posture));
        }

        void OnHitResolved(IHittable t, HitInfo info, HitResult res)
        {
            // a guard-broken target turned the hit into a guaranteed critical (ADR-0004 step 7)
            if (res == HitResult.Hit && t is Fighter hf && hf.LastHitCrit && !info.crit) { info.crit = true; info.dmg = hf.LastHitDamage; }
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
                    Knock(new Vector2(KnockVelocity(new Vector2(-facing * 1.6f, 0f)).x, rb.linearVelocity.y));     // recoil off a cornered guard
            }
            PublishHit(t, info, res);
            // a guard-break follows the event of the hit that caused it (on the target; on this fighter after a parry)
            (t as Fighter)?.FlushGuardBreak();
            FlushGuardBreak();
            if (t is Fighter f) onHit?.Invoke(this, f, info, res);
        }
    }
}
