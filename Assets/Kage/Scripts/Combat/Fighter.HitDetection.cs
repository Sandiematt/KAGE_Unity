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

        void HitPoints(HitPoint hp, HitWindow w, List<Vector2> into)
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
                    if (w != null && w.chainWhip && TryChainWhipWorld(0f, out var chHand, out var chWeight, out var chCtrl, out float chExt) && chExt > 0.04f)
                    {
                        float t0 = w.bladeFrom, t1 = w.bladeTo;
                        float span = Mathf.Max(0.18f, chExt * Mathf.Max(0.1f, t1 - t0));
                        int n = Mathf.Max(5, Mathf.CeilToInt(span / 0.16f));
                        for (int i = 0; i < n; i++)
                        {
                            float u = Mathf.Lerp(t0, t1, i / (float)(n - 1));
                            into.Add(ChainBezier(chHand, chCtrl, chWeight, u));
                        }
                        break;
                    }
                    // five points along a katana (one every 14 cm); a longer blade gets one about every 18 cm so a shaft has no gaps
                    float f0 = w != null ? w.bladeFrom : 0f, f1 = w != null ? w.bladeTo : 1f;
                    Vector2 b0 = Vector2.Lerp(BladeBaseLocal, BladeTipLocal, f0);
                    Vector2 b1 = Vector2.Lerp(BladeBaseLocal, BladeTipLocal, f1);
                    int count = Mathf.Max(5, Mathf.CeilToInt((b1 - b0).magnitude / 0.18f));
                    if (f0 <= 1e-4f && f1 >= 1f - 1e-4f)
                    {
                        for (int i = 1; i <= count; i++) into.Add(rig.World(B.Sword, Vector2.Lerp(b0, b1, i / (float)count)));
                    }
                    else
                    {
                        for (int i = 0; i < count; i++) into.Add(rig.World(B.Sword, Vector2.Lerp(b0, b1, i / (float)(count - 1))));
                    }
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
                HitPoints(hp, w, _pts);
                while (_ptKinds.Count < _pts.Count) _ptKinds.Add(hp);
            }
            if (!_sweepPrev.TryGetValue(w, out var prev)) _sweepPrev[w] = prev = new SweepPrev { frame = int.MinValue };
            bool continues = prev.frame == _moveTicks - 1 && prev.pts.Count == _pts.Count;
            for (int k = 0; k < _pts.Count; k++)
                ctx.Sweeps.Add(new HitSweep
                {
                    owner = this, move = move, window = w, point = _ptKinds[k], sample = k,
                    from = continues ? prev.pts[k] : _pts[k], to = _pts[k], radius = w.radius * _contactScale,
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
                Hurtbox.Overlap(ctx.Physics, s.to, s.radius, BoxTmp);
                foreach (var b in BoxTmp) AddTarget(b.Owner);
                Hurtbox.Sweep(ctx.Physics, s.from, s.to, s.radius, SweepTmp);
                foreach (var h in SweepTmp) AddTarget(h.box.Owner);
            }
            _targets.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var (t, _) in _targets)
            {
                if (t == null || ReferenceEquals(t, this)) continue;
                if (t is Fighter tf && tf.team == team) continue;
                if (Logged(w, t)) continue;
                if (!FindContact(t, sweeps, i, j, out var zone, out var contact, out int k, out float frac)) continue;
                ResolveContact(t, w, zone, contact, StrikeOf(sweeps, i, j, k, contact, frac));
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
        bool FindContact(IHittable t, List<HitSweep> sweeps, int i, int j, out HitZone zone, out Vector2 contact, out int sample, out float fraction)
        {
            zone = HitZone.Body;
            contact = default;
            sample = -1;
            fraction = 1f;
            for (int k = i; k < j; k++)
            {
                var s = sweeps[k];
                Hurtbox.Overlap(ctx.Physics, s.to, s.radius, BoxTmp);
                Hurtbox best = null;
                foreach (var b in BoxTmp) if (ReferenceEquals(b.Owner, t) && (best == null || b.Index < best.Index)) best = b;
                if (best == null) continue;
                zone = best.Zone;
                contact = Vector2.Lerp(s.to, best.Centre, s.radius / (s.radius + best.Radius));
                sample = k;
                return true;
            }
            float bestFrac = float.MaxValue;
            int bestIndex = int.MaxValue;
            for (int k = i; k < j; k++)
            {
                var s = sweeps[k];
                Hurtbox.Sweep(ctx.Physics, s.from, s.to, s.radius, SweepTmp);
                foreach (var h in SweepTmp)
                {
                    if (!ReferenceEquals(h.box.Owner, t)) continue;
                    if (h.fraction > bestFrac || (h.fraction == bestFrac && h.box.Index >= bestIndex)) continue;
                    bestFrac = h.fraction;
                    bestIndex = h.box.Index;
                    zone = h.box.Zone;
                    contact = h.point;
                    sample = k;
                }
            }
            if (bestFrac != float.MaxValue) fraction = Mathf.Clamp01(bestFrac);
            return bestFrac != float.MaxValue;
        }

        // ---- the striking part (the contact-matched parry, ParryContactSolver) ----
        static readonly Pose StrikePoseTmp = new();
        PoseFK _strikeFk;

        /// <summary>
        /// The striking part of the window whose sample <paramref name="k"/> touched: its world segment at the moment it touched (the blade from
        /// its guard to its tip, a fist's forearm, a leg from the knee to the toe) and the way it was travelling: the samples' sweep since the
        /// last frame, or on the window's first frame (no sweep yet) the pose half a frame back, through <see cref="PoseFK"/>. A touch part way
        /// along the sweep (<paramref name="fraction"/> below 1) is posed at that moment of the move, so a fast cut is met where it met the
        /// body, not where it ended the frame.
        /// </summary>
        HitInfoStrike StrikeOf(List<HitSweep> sweeps, int i, int j, int k, Vector2 contact, float fraction)
        {
            if (k < i || k >= j) return default;
            var kind = sweeps[k].point;
            var w = sweeps[k].window;
            Vector2 sweep = Vector2.zero;
            int n = 0;
            for (int m = i; m < j; m++)
            {
                if (sweeps[m].point != kind) continue;
                sweep += sweeps[m].to - sweeps[m].from;
                n++;
            }
            if (n > 0) sweep /= n;
            return BuildStrike(kind, w, sweep, contact, fraction);
        }

        /// <summary>A hit's striking segment and travel (see <see cref="StrikeOf"/>); unset when the rig isn't posed.</summary>
        struct HitInfoStrike
        {
            public bool set;
            public HitPoint point;
            public Vector2 a, b, swing;
            /// <summary>The move time the segment was posed at; -1 = this frame's pose.</summary>
            public float time;
            public void Into(ref HitInfo info)
            {
                if (!set) return;
                info.hasStrikeGeometry = true;
                info.strikePoint = point;
                info.strikeBase = a;
                info.strikeTip = b;
                info.swingDir = swing;
                info.strikeTime = time;
            }
        }

        HitInfoStrike BuildStrike(HitPoint kind, HitWindow w, Vector2 sweepDelta, Vector2 contact, float fraction = 1f)
        {
            if (rig == null || def == null) return default;
            var s = new HitInfoStrike { set = true, point = kind, time = -1f };
            StrikeSegment(kind, w, out s.a, out s.b);
            if (fraction < 0.999f && state == FState.Attack && move != null)
            {
                // touched part way through this frame's sweep: the pose of that moment (the rig stands where it does this frame)
                float at = _moveTime - (1f - fraction) * Mathf.Max(0.01f, _moveRateNow);
                if (at >= 0f && PosedStrike(kind, w, at, out var pa, out var pb)) { s.a = pa; s.b = pb; s.time = at; }
            }
            Vector2 swing = sweepDelta;
            if (swing.sqrMagnitude < 1e-8f) swing = StrikeTravelFromPose(kind, w, s.b);
            if (swing.sqrMagnitude < 1e-8f && rb != null) swing = rb.linearVelocity * Time.fixedDeltaTime;
            s.swing = swing.sqrMagnitude > 1e-10f ? swing.normalized : Vector2.zero;
            return s;
        }

        /// <summary>The live world segment of a hit point's striking part on the currently posed rig (used by pre-contact parry anticipation).</summary>
        internal bool TryLiveStrikeSegment(HitPoint kind, HitWindow w, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            if (rig == null || def == null) return false;
            StrikeSegment(kind, w, out a, out b);
            return true;
        }

        /// <summary>The world segment of a hit point's striking part on the posed rig, hilt/elbow/knee end to tip.</summary>
        void StrikeSegment(HitPoint kind, HitWindow w, out Vector2 a, out Vector2 b)
        {
            switch (kind)
            {
                case HitPoint.Blade:
                    if (w != null && w.chainWhip && TryChainWhipWorld(0f, out var chHand, out var chWeight, out var chCtrl, out float chExt) && chExt > 0.04f)
                    {
                        a = ChainBezier(chHand, chCtrl, chWeight, w.bladeFrom);
                        b = chWeight;
                        break;
                    }
                    if (w != null && (w.bladeFrom > 1e-4f || w.bladeTo < 1f - 1e-4f))
                    {
                        a = rig.World(B.Sword, Vector2.Lerp(BladeBaseLocal, BladeTipLocal, w.bladeFrom));
                        b = rig.World(B.Sword, Vector2.Lerp(BladeBaseLocal, BladeTipLocal, w.bladeTo));
                        break;
                    }
                    a = rig.World(B.Sword, BladeBaseLocal); b = rig.World(B.Sword, BladeTipLocal); break;
                case HitPoint.BladeN: a = rig.World(B.HandN, NearBladeBaseLocal); b = rig.World(B.HandN, NearBladeTipLocal); break;
                case HitPoint.Fist: a = rig.World(B.ArmNL, Vector2.zero); b = rig.World(B.HandN, def.fistLocal); break;
                case HitPoint.FistF: a = rig.World(B.ArmFL, Vector2.zero); b = rig.World(B.HandF, def.BindLocal(B.Sword)); break;
                case HitPoint.Hilt: a = rig.World(B.ArmFL, Vector2.zero); b = rig.World(B.HandF, def.hiltLocal); break;
                case HitPoint.FootF:
                case HitPoint.ShinF: a = rig.World(B.LegFL, Vector2.zero); b = rig.World(B.FootF, def.footTipLocal); break;
                default: a = rig.World(B.LegNL, Vector2.zero); b = rig.World(B.FootN, def.footTipLocal); break;      // FootN, ShinN, KneeN
            }
        }

        /// <summary>
        /// The striking part's travel on a window's first frame (no sweep yet): its tip now less its tip half a frame back, posed through
        /// <see cref="PoseFK"/> on this fighter's rig frame. Zero outside a move.
        /// </summary>
        Vector2 StrikeTravelFromPose(HitPoint kind, HitWindow w, Vector2 tipNow)
        {
            if (move == null || state != FState.Attack) return Vector2.zero;
            float back = _moveTime - 0.5f * Mathf.Max(0.05f, PlaybackRate);
            if (back < 0f || !PosedStrike(kind, w, back, out _, out var tipThen)) return Vector2.zero;
            return tipNow - tipThen;
        }

        /// <summary>
        /// The striking segment (world, hilt end to tip) of the current move posed at move time <paramref name="at"/> on this frame's rig frame,
        /// through <see cref="PoseFK"/>: the same pose <see cref="ParryImpactPose"/> holds a parried blow in.
        /// </summary>
        bool PosedStrike(HitPoint kind, HitWindow w, float at, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            if (move == null || rig == null) return false;
            var frame = RootFrame.Of(rig.transform);
            if (!frame.Valid) return false;
            if (_strikeFk == null || _strikeFk.def != def) _strikeFk = new PoseFK(def);
            move.Evaluate(def, at, _start, StrikePoseTmp);
            ApplyGrip(StrikePoseTmp);
            var k = _strikeFk.Solve(StrikePoseTmp);
            if (kind == HitPoint.Blade && w != null && w.chainWhip && TryChainWhipPosed(k, frame, at, out var chHand, out var chWeight, out var chCtrl, out float chExt) && chExt > 0.04f)
            {
                a = ChainBezier(chHand, chCtrl, chWeight, w.bladeFrom);
                b = chWeight;
                return true;
            }
            Vector2 la, lb;
            switch (kind)
            {
                case HitPoint.Blade:
                    if (w != null && (w.bladeFrom > 1e-4f || w.bladeTo < 1f - 1e-4f))
                    {
                        la = k.Point(B.Sword, Vector2.Lerp(BladeBaseLocal, BladeTipLocal, w.bladeFrom));
                        lb = k.Point(B.Sword, Vector2.Lerp(BladeBaseLocal, BladeTipLocal, w.bladeTo));
                    }
                    else { la = k.Point(B.Sword, BladeBaseLocal); lb = k.Point(B.Sword, BladeTipLocal); }
                    break;
                case HitPoint.BladeN: la = k.Point(B.HandN, NearBladeBaseLocal); lb = k.Point(B.HandN, NearBladeTipLocal); break;
                case HitPoint.Fist: la = k.pos[(int)B.ArmNL]; lb = k.Point(B.HandN, def.fistLocal); break;
                case HitPoint.FistF: la = k.pos[(int)B.ArmFL]; lb = k.Point(B.HandF, def.BindLocal(B.Sword)); break;
                case HitPoint.Hilt: la = k.pos[(int)B.ArmFL]; lb = k.Point(B.HandF, def.hiltLocal); break;
                case HitPoint.FootF:
                case HitPoint.ShinF: la = k.pos[(int)B.LegFL]; lb = k.Point(B.FootF, def.footTipLocal); break;
                default: la = k.pos[(int)B.LegNL]; lb = k.Point(B.FootN, def.footTipLocal); break;
            }
            a = frame.ToWorld(la);
            b = frame.ToWorld(lb);
            return true;
        }

        /// <summary>
        /// The pose a parried blow is held in through the hit-stop: the move at the moment it touched (<see cref="HitInfo.strikeTime"/>), the
        /// pose the parrier's blade was solved against; this frame's pose when it touched at the frame's end.
        /// </summary>
        void ParryImpactPose(in HitInfo info, Pose into)
        {
            if (info.hasStrikeGeometry && info.strikeTime >= 0f && move != null && state == FState.Attack && move.id == info.moveId)
            {
                move.Evaluate(def, info.strikeTime, _start, into);
                ApplyGrip(into);
                return;
            }
            into.CopyFrom(_display);
        }

        /// <summary>
        /// A contact goes through the defence order (ADR-0004): the pre-hit checks (perfect dodge), then
        /// whether the target can be hit now, then the target's own checks in ReceiveHit.
        /// </summary>
        void ResolveContact(IHittable t, HitWindow w, HitZone zone, Vector2 contact, HitInfoStrike strike)
        {
            var info = new HitInfo
            {
                attacker = this, w = w, dmg = DamageCalc.For(this, t, w.dmg, false, !w.fixedDamage), kb = new Vector2(w.kb.x * facing, w.kb.y),
                zone = zone, contact = contact, moveId = move != null ? move.id : null, window = move != null ? move.hits.IndexOf(w) : -1,
                strikeVector = w.strikeVector,
            };
            strike.Into(ref info);
            float styleDmg = StyleShapeHit(t, w, ref info);          // the weapon's traits: sweet spot, hook, weight (Fighter.Style.cs)
            info.dmg = StyleScale(info.dmg, styleDmg);
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
            if (w.critical || RollCrit())
            {
                info.crit = true;
                info.dmg = StyleScale(DamageCalc.For(this, t, w.dmg, true, !w.fixedDamage), styleDmg);
            }
            var res = t.ReceiveHit(info);
            OnHitResolved(t, info, res);
        }

        // ---- legacy resolution (legacy switch): point tests inside the attacker's tick ----
        void CheckHits(HitWindow w)
        {
            _pts.Clear();
            _ptKinds.Clear();
            foreach (var hp in w.points)
            {
                HitPoints(hp, w, _pts);
                while (_ptKinds.Count < _pts.Count) _ptKinds.Add(hp);
            }
            if (Targets.Count > 1) foreach (var t in Targets) TryTarget(t, w);     // a group fight: any foe in reach (SAN-83)
            else TryTarget(opponent, w);
            TryTarget(extraTarget, w);
        }

        void TryTarget(IHittable t, HitWindow w)
        {
            if (t == null || ReferenceEquals(t, this)) return;
            if (t is Fighter tf && tf.team == team) return;
            foreach (var (hw, ht) in _hitLog) if (hw == w && ReferenceEquals(ht, t)) return;
            for (int pi = 0; pi < _pts.Count; pi++)
            {
                var p = _pts[pi];
                if (!t.Overlap(p, w.radius * _contactScale, w, out var zone, out var contact)) continue;
                _hitLog.Add((w, t));
                bool crit = w.critical || RollCrit();
                int dmg = DamageCalc.For(this, t, w.dmg, crit, !w.fixedDamage);
                var info = new HitInfo
                {
                    attacker = this, w = w, dmg = dmg, kb = new Vector2(w.kb.x * facing, w.kb.y), zone = zone, contact = contact, crit = crit,
                    moveId = move != null ? move.id : null, window = move != null ? move.hits.IndexOf(w) : -1,
                    strikeVector = w.strikeVector,
                };
                if (pi < _ptKinds.Count) BuildStrike(_ptKinds[pi], w, Vector2.zero, contact).Into(ref info);
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
            // a parry's posture is the one its attacker took (SAN-91); the blow it turned aside was no crit
            if (target != null && res == HitResult.Parried) { posture = target.LastParryPosture; info.crit = false; }
            var w = info.w;
            ctx.Events.Publish(CombatEvent.HitResolved(ctx.Clock.Frame, type, this, target, FightContext.IdOf(t), info.moveId, info.window,
                dmg, info.crit, w != null ? w.level : HitLevel.Mid, info.zone, info.contact, info.kb, posture));
        }

        static Vector2 ImpactDirection(in HitInfo info, float facing)
        {
            if (info.swingDir.sqrMagnitude > 1e-8f) return info.swingDir.normalized;
            var tag = info.strikeVector != StrikeVector.Auto ? info.strikeVector : info.w?.strikeVector ?? StrikeVector.Auto;
            if (tag == StrikeVector.Auto) return new Vector2(facing, 0.15f).normalized;
            // The contact solver's incoming directions, expressed in the attacker's forward space.
            float angle = tag switch
            {
                StrikeVector.HighOverhead => -65f,
                StrikeVector.Upward => 55f,
                StrikeVector.Low => 5f,
                _ => 0f
            };
            Vector2 dir = KMath.Dir(angle);
            return new Vector2(dir.x * facing, dir.y);
        }

        void PresentHit(IHittable target, in HitInfo info, bool counter)
        {
            var w = info.w;
            Vector2 dir = ImpactDirection(info, facing);
            bool strong = w.heavy || w.launch || info.crit || counter;
            GameFeel.Shake(w.shake * (info.crit ? 1.5f : 1f));
            if (strong)
            {
                GameFeel.Punch(w.heavy || counter ? 0.12f : 0.07f);
                GameFeel.Kick(dir, counter ? 0.14f : 0.095f);
                rig?.Impulse(new Vector2(-facing * (w.heavy ? 4.2f : 2.8f), 1.5f), 1.25f);
            }
            if (info.origin == HitOrigin.Melee)
            {
                if (target is Fighter victim)
                    victim.rig?.HitShake(dir, strong ? 0.043f : 0.019f, strong ? 0.14f : 0.075f);
                if (w.sword)
                {
                    if (info.strikePoint == HitPoint.BladeN) _trailBladeN?.Flare();
                    else _trailBlade?.Flare();
                }
            }
            if (Look == null || !Look.Hit(this, info.contact, dir, w, info.crit))
                Vfx.I?.HitBurst(info.contact, dir, strong ? 1.35f : 0.92f, w.sword, fxMain, fxCore, fxDark, strong);
            CombatAudio?.Play(w.sword ? SfxKind.Slash : strong ? SfxKind.HitHeavy : SfxKind.Hit, 0.8f);
        }

        void OnHitResolved(IHittable t, HitInfo info, HitResult res)
        {
            // a parried blow met the parrier's blade at the blades' crossing, not on its hurt circle: the event (and every call-out anchored
            // on it) carries the clash point (the contact-matched parry, ParryContactSolver)
            if (res == HitResult.Parried && t is Fighter parrier && parrier.HasParryContact) info.contact = parrier.LastParryContact;
            // a guard-broken target turned the hit into a guaranteed critical (ADR-0004 step 7)
            if (res == HitResult.Hit && t is Fighter hf && hf.LastHitCrit && !info.crit) { info.crit = true; info.dmg = hf.LastHitDamage; }
            // the parrier's answer landed: a counter hit (Fighter.Counter.cs)
            var counterOn = res == HitResult.Hit && t is Fighter cf && cf.LastHitCounter ? cf : null;
            if (counterOn != null) info.dmg = counterOn.LastHitDamage;
            // a blow on a fighter recovering from a whiff: a punish (Fighter.Flow.cs)
            var punishOn = res == HitResult.Hit && t is Fighter pf && pf.LastHitPunish ? pf : null;
            if (punishOn != null) info.dmg = punishOn.LastHitDamage;
            var w = info.w;
            if (res == HitResult.Hit)
            {
                moveHit = true;
                meter = Mathf.Min(100f, meter + (5f + info.dmg * 0.6f) * RageMul);
                comboCount++;
                GameFeel.Hitstop(w.hitstop + (info.crit ? 3 : 0));
                if (PresentsCombat) PresentHit(t, info, counterOn != null);
            }
            else if (res == HitResult.Blocked)
            {
                GameFeel.Hitstop(3);
                if (PresentsCombat)
                {
                    GameFeel.Shake(0.05f);
                    Vector2 dir = ImpactDirection(info, facing);
                    if (Look == null || !Look.Block(this, info.contact, dir, w)) Vfx.I?.BlockBurst(info.contact, -dir);
                    CombatAudio?.Play(SfxKind.Block, 0.7f);
                }
                if (opponent != null && opponent.AtWall)
                    Knock(new Vector2(KnockVelocity(new Vector2(-facing * 1.6f, 0f)).x, rb.linearVelocity.y));     // recoil off a cornered guard
            }
            NoteContact(res);
            NoteMoveContact(res);                                     // a connected move may cancel its recovery (Fighter.Flow.cs)
            StyleAfterHit(t, info, res);                              // momentum, hook marks, guard crush (Fighter.Style.cs)
            PublishHit(t, info, res);
            if (counterOn != null) CounterHitLanded(counterOn, info);
            if (punishOn != null) PunishLanded(punishOn, info);
            // a guard-break follows the event of the hit that caused it (on the target; on this fighter after a parry)
            (t as Fighter)?.FlushGuardBreak();
            FlushGuardBreak();
            if (t is Fighter f) onHit?.Invoke(this, f, info, res);
        }
    }
}
