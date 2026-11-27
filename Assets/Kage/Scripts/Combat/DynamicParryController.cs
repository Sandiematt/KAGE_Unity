using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The contact-matched parry's motion, one per fighter (<see cref="Fighter.ParryController"/>), the same for Kage and every foe.
    /// It lays an overlay over the fighter's own pose output (<see cref="Fighter.PoseOutput"/>) and never touches the logic: states,
    /// frames, hurt circles before the parry, pushback and stagger are the parry's own (Fighter.Parry.cs).
    /// <list type="bullet">
    /// <item><b>Defender</b> (<see cref="SolveDefender"/>, <see cref="ActivateDefender"/>): from the impact frame the arms, wrists, torso and
    /// blade snap onto the clash point (<see cref="ParryTuning.contactSnapInitial"/> of the way at once, the rest over
    /// <see cref="ParryTuning.contactSnapFrames"/> on unscaled time, so it runs through the hit-stop), hold there through the hit-stop with
    /// the blade drawn in front (re-solved against the frozen attacker if the parry's pushback moved it, so no gap opens), hold
    /// <see cref="ParryTuning.contactHoldFrames"/> live frames more, then settle back over <see cref="ParryTuning.contactRecoverFrames"/>.</item>
    /// <item><b>Attacker</b> (<see cref="BeginAttacker"/>): frozen in its exact impact pose through the hit-stop, so the two blades stay
    /// locked steel on steel; then its weapon recoils back along the clash normal and the reel takes over over
    /// <see cref="ParryTuning.attackerDeflectFrames"/>.</item>
    /// <item><b>Anticipation</b>: with a parry window open and a foe swinging close by, the arms and blade only (never the hips or chest, so
    /// the hurt circles and every hit frame stay as they were) lean <see cref="ParryTuning.anticipationWeight"/> of the way towards the coming
    /// blow's height.</item>
    /// </list>
    /// Any state change ends it at once: the new state's blend starts from the pose on screen (Fighter.SetState copies it), so an Iai
    /// Counter, a dash or another blow flows straight out of the parry with no input lag.
    /// </summary>
    public sealed class DynamicParryController
    {
        public enum Role { None, Defender, Attacker }

        /// <summary>The frames the attacker's weapon takes to kick back along the clash normal after the hit-stop.</summary>
        public const float RecoilFrames = 2.5f;
        /// <summary>A foe swinging closer than this (metres) is one the guard leans towards.</summary>
        public const float AnticipationRange = 2.4f;

        public Role Mode { get; private set; }
        /// <summary>The overlay's weight on the last pose (1 = fully the clash pose, 0 = the fighter's own pose).</summary>
        public float Weight { get; private set; }
        /// <summary>The anticipation lean's current weight (0 when none).</summary>
        public float AnticipationWeight => _antW;
        /// <summary>Live (unfrozen) logic frames since the parry.</summary>
        public int LiveFrames => _live;
        /// <summary>The last solved parry (this fighter's own, or the one that parried it).</summary>
        public ParryContactSolution Solution => _sol;
        /// <summary>True when the overlay changes the pose this frame (a parry in progress, or the anticipation lean).</summary>
        public bool HasActiveVisual => Mode != Role.None || _antW > 0.0005f;
        /// <summary>The defender's clash pose (root space), or the attacker's recoil pose.</summary>
        public Pose TargetPose => Mode == Role.Attacker ? _deflected : _target;
        /// <summary>The attacker's frozen impact pose.</summary>
        public Pose ImpactPose => _impact;
        /// <summary>How many times the defender re-solved against the attacker during the hit-stop.</summary>
        public int Resyncs { get; private set; }

        /// <summary>The parried attacker's hold on its locked weapon: 1 through the hit-stop, falling to 0 as it recoils (a lash snaps back); 0 in any other role.</summary>
        public float AttackerHold => Mode != Role.Attacker ? 0f : _live == 0 ? 1f : Mathf.Clamp01(1f - (_live - 1) / RecoilFrames);

        readonly Pose _guard = new(), _target = new(), _impact = new(), _deflected = new(), _mix = new(), _mix2 = new(), _ant = new();
        PoseFK _fk;
        ParryContactSolution _sol;
        Fighter _other;
        StrikeGeometry _strikeLocal;        // the blow in the attacker's root space (frozen at impact), for the re-sync
        bool _resync;
        Vector2 _otherOrigin;
        float _snapT, _antW, _antY = 1.1f, _antAngle;
        bool _antHasAngle;
        int _live;

        static ParryTuning T => Tuning.Get<ParryTuning>();

        public void Reset()
        {
            Mode = Role.None;
            Weight = 0f;
            _antW = 0f;
            _antHasAngle = false;
            _live = 0;
            _snapT = 0f;
            _other = null;
            _resync = false;
            Resyncs = 0;
        }

        PoseFK Fk(RigDef d)
        {
            if (_fk == null || _fk.def != d) _fk = new PoseFK(d);
            return _fk;
        }

        // ================================================================================== defender

        /// <summary>
        /// Solves this fighter's parry of <paramref name="strike"/> from its guard and keeps the clash pose ready (finished through the
        /// fighter's grip). <see cref="ActivateDefender"/> starts showing it.
        /// </summary>
        public ParryContactSolution SolveDefender(Fighter self, Fighter attacker, in StrikeGeometry strike)
        {
            _other = attacker;
            _resync = false;
            Resyncs = 0;
            if (attacker != null && attacker.rig != null && strike.hasSegment)
            {
                var af = RootFrame.Of(attacker.rig.transform);
                if (af.Valid)
                {
                    _strikeLocal = strike;
                    _strikeLocal.baseWorld = af.ToRoot(strike.baseWorld);
                    _strikeLocal.tipWorld = af.ToRoot(strike.tipWorld);
                    _strikeLocal.rawContactWorld = af.ToRoot(strike.rawContactWorld);
                    _strikeLocal.swingDirWorld = af.DirToRoot(strike.swingDirWorld);
                    _otherOrigin = af.origin;
                    _resync = true;
                }
            }
            Solve(self, strike);
            return _sol;
        }

        void Solve(Fighter self, in StrikeGeometry strike)
        {
            var spec = self.ParryBladeSpec();
            _guard.CopyFrom(self.ParryGuardPose());
            _sol = ParryContactSolver.Solve(strike, spec, _guard, _target, Fk(self.def));
            self.FinishGrip(_target);
        }

        /// <summary>Starts showing the solved clash pose: the snap from the pose on screen, the hold, the settle.</summary>
        public void ActivateDefender(Fighter self)
        {
            Mode = Role.Defender;
            _snapT = 0f;
            _live = 0;
            _antW = 0f;
            self.rig?.SetSwordFront(true);
        }

        /// <summary>The attacker moved during the hit-stop (the parry's one frame of pushback): solve again so the blades stay touching.</summary>
        void Resync(Fighter self)
        {
            if (!_resync || _other == null || _other.rig == null) return;
            var af = RootFrame.Of(_other.rig.transform);
            if (!af.Valid || (af.origin - _otherOrigin).sqrMagnitude < 0.002f * 0.002f) return;
            _otherOrigin = af.origin;
            var s = _strikeLocal;
            s.baseWorld = af.ToWorld(_strikeLocal.baseWorld);
            s.tipWorld = af.ToWorld(_strikeLocal.tipWorld);
            s.rawContactWorld = af.ToWorld(_strikeLocal.rawContactWorld);
            s.swingDirWorld = af.DirToWorld(_strikeLocal.swingDirWorld);
            Solve(self, s);     // the pose only: the parry's contact (the event, the cue) stays where the blades met on impact
            Resyncs++;
        }

        // ================================================================================== attacker

        /// <summary>
        /// This fighter's blow was parried: <paramref name="impact"/> is its pose on the frame of the clash. It holds that pose through the
        /// hit-stop, then its weapon recoils along the clash normal by <see cref="ParryContactSolution.attackerRecoilDeg"/>.
        /// </summary>
        public void BeginAttacker(Fighter self, Pose impact, in ParryContactSolution sol, HitPoint point)
        {
            _sol = sol;
            _other = null;
            _resync = false;
            _impact.CopyFrom(impact);
            _impact.MarkDirty();
            var fk = Fk(self.def);
            fk.Solve(_impact);              // resolves it: the IK baked, the near blade's direction set
            _deflected.CopyFrom(_impact);
            var frame = self.rig != null ? RootFrame.Of(self.rig.transform) : RootFrame.Of(self.Pos, self.facing, 1f);
            if (frame.Valid && sol.valid)
            {
                Vector2 n = frame.DirToRoot(sol.clashNormalWorld);
                Vector2 contact = frame.ToRoot(sol.clashWorld);
                bool near = point == HitPoint.BladeN || point == HitPoint.Fist;
                bool leg = point is HitPoint.FootN or HitPoint.ShinN or HitPoint.KneeN or HitPoint.FootF or HitPoint.ShinF;
                Limb limb = leg ? (point is HitPoint.FootF or HitPoint.ShinF ? Limb.LegF : Limb.LegN) : near ? Limb.ArmN : Limb.ArmF;
                B wristBone = limb switch { Limb.ArmN => B.HandN, Limb.LegN => B.FootN, Limb.LegF => B.FootF, _ => B.HandF };
                Vector2 wrist = fk.pos[(int)wristBone];
                float turn = Mathf.Sign(Cross(contact - wrist, n));
                if (turn == 0f) turn = 1f;
                if (_deflected.ik[(int)limb])
                {
                    // the hands (or the foot) knocked back along the normal, never past the limb's reach
                    B root = limb switch { Limb.ArmN => B.ArmNU, Limb.LegN => B.LegNU, Limb.LegF => B.LegFU, _ => B.ArmFU };
                    B mid = limb switch { Limb.ArmN => B.ArmNL, Limb.LegN => B.LegNL, Limb.LegF => B.LegFL, _ => B.ArmFL };
                    float reach = (self.def.BindLocal(mid).magnitude + self.def.BindLocal(wristBone).magnitude) * 0.95f;
                    Vector2 sh = fk.pos[(int)root];
                    Vector2 w = _deflected.target[(int)limb] + n * (leg ? 0.05f : 0.08f);
                    Vector2 to = w - sh;
                    if (to.magnitude > reach) w = sh + to.normalized * reach;
                    _deflected.target[(int)limb] = w;
                }
                float deg = turn * sol.attackerRecoilDeg;
                if (leg) _deflected.a[(int)B.Hips] += turn * 4f;                                     // a kick turned aside: the body gives a little
                else if (point == HitPoint.BladeN && _deflected.dual) { _deflected.hasSwNAbs = true; _deflected.swNAbs += deg; }
                else if (point == HitPoint.Fist) _deflected.a[(int)B.HandN] += deg * 0.5f;           // the near fist: the wrist knocked round
                else if (_deflected.hasSwAbs) _deflected.swAbs += deg;                               // the blade (or the hilt it is held by)
                else _deflected.a[(int)B.Sword] += deg;
                _deflected.MarkDirty();
            }
            Mode = Role.Attacker;
            _live = 0;
            _antW = 0f;
            Weight = 1f;
        }

        // ================================================================================== the clock

        /// <summary>Any state change ends the parry's motion: the new state blends from the pose on screen.</summary>
        public void OnStateChanged(Fighter self, FState s)
        {
            if (Mode == Role.None) return;
            Mode = Role.None;
            Weight = 0f;
            _resync = false;
        }

        /// <summary>Once per live logic frame (Fighter.ParryTick): counts the frames after the hit-stop and ends the motion when it is done.</summary>
        public void LogicTick(Fighter self)
        {
            if (Mode == Role.None || GameFeel.InHitstop) return;
            _live++;
            var t = T;
            int end = Mode == Role.Defender ? t.contactHoldFrames + Mathf.Max(1, t.contactRecoverFrames) + 1 : Mathf.CeilToInt(RecoilFrames) + Mathf.Max(1, t.attackerDeflectFrames) + 1;
            if (_live > end) End(self);
        }

        void End(Fighter self)
        {
            bool wasDefender = Mode == Role.Defender;
            Mode = Role.None;
            Weight = 0f;
            _resync = false;
            if (wasDefender && self.rig != null && !self.IsAttacking) self.rig.SetSwordFront(false);
        }

        // ================================================================================== the pose

        /// <summary>
        /// Lays the overlay over <paramref name="display"/> (the fighter's own pose this frame, already through its grip).
        /// <paramref name="alpha"/> is how far past the last logic frame the pose is; <paramref name="unscaledDt"/> the real time since the
        /// last pose (it runs through the hit-stop); <paramref name="frozen"/> the clock is held by a hit-stop.
        /// </summary>
        public void ApplyPose(Fighter self, Pose display, float alpha, float unscaledDt, bool frozen)
        {
            var d = self.def;
            if (d == null) return;
            var t = T;
            bool held = frozen || _live == 0;
            switch (Mode)
            {
                case Role.Defender:
                {
                    if (held) Resync(self);
                    float snapFrames = Mathf.Max(0.01f, t.contactSnapFrames);
                    float k = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(_snapT * 60f / snapFrames));
                    float w = Mathf.Lerp(Mathf.Clamp01(t.contactSnapInitial), 1f, k);
                    _snapT += Mathf.Max(0f, unscaledDt);
                    if (!held)
                    {
                        float tau = _live - 1 + alpha - t.contactHoldFrames;
                        if (tau > 0f) w = Mathf.Min(w, 1f - KMath.Apply(Ease.InOutQuad, Mathf.Clamp01(tau / Mathf.Max(1, t.contactRecoverFrames))));
                    }
                    Overlay(d, display, _target, Mathf.Clamp01(w));
                    break;
                }
                case Role.Attacker:
                {
                    if (held)
                    {
                        display.CopyFrom(_impact);
                        Weight = 1f;
                        break;
                    }
                    float tau = _live - 1 + alpha;
                    float r = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(tau / RecoilFrames));
                    Pose.Blend(d, _impact, _deflected, r, _mix2);
                    float w = 1f - KMath.Apply(Ease.InOutQuad, Mathf.Clamp01((tau - RecoilFrames) / Mathf.Max(1, t.attackerDeflectFrames)));
                    Overlay(d, display, _mix2, Mathf.Clamp01(w));
                    break;
                }
                default:
                    Anticipate(self, display, unscaledDt, t);
                    break;
            }
        }

        void Overlay(RigDef d, Pose display, Pose over, float w)
        {
            Weight = w;
            if (w <= 0f) return;
            Pose.Blend(d, display, over, w, _mix);
            display.CopyFrom(_mix);
        }

        /// <summary>The arms and blade lean towards the coming blow while a parry window is open (no hip or chest change).</summary>
        void Anticipate(Fighter self, Pose display, float unscaledDt, ParryTuning t)
        {
            float want = 0f;
            float guardBlade = display.hasSwAbs ? display.swAbs : 24f;
            if (self.InParryWindow && Incoming(self, guardBlade, out float y, out float bladeAngle, out bool hasAngle))
            {
                want = Mathf.Clamp01(t.anticipationWeight);
                _antY = y;
                _antAngle = bladeAngle;
                _antHasAngle = hasAngle;
            }
            float rate = Mathf.Max(0.05f, t.anticipationWeight) * Mathf.Max(0f, unscaledDt) * 60f / 4f;     // about four frames in or out
            _antW = Mathf.MoveTowards(_antW, want, rate);
            if (_antW <= 0.0005f) { _antW = 0f; _antHasAngle = false; Weight = 0f; return; }
            _ant.CopyFrom(display);
            _ant.Resolve(self.def);
            if (_ant.ik[(int)Limb.ArmF])
            {
                var fk = Fk(self.def);
                fk.Solve(_ant);
                Vector2 shF = fk.pos[(int)B.ArmFU];
                Vector2 g = _ant.target[(int)Limb.ArmF];
                g.y += Mathf.Clamp((Mathf.Clamp(_antY, 0.5f, 1.7f) - g.y) * 0.5f, -0.3f, 0.3f);
                _ant.target[(int)Limb.ArmF] = ParryContactSolver.ClampFarArmReach(self.def, shF, g);
            }
            if (_ant.hasSwAbs)
            {
                float goal = _antHasAngle ? _antAngle : ParryContactSolver.PreferredAngle(_antY, _ant.swAbs);
                _ant.swAbs = Mathf.LerpAngle(_ant.swAbs, goal, 0.5f);
            }
            _ant.MarkDirty();
            Overlay(self.def, display, _ant, _antW);
        }

        /// <summary>
        /// The nearest foe in front swinging (or winding up) a blow within <see cref="AnticipationRange"/>: samples its live weapon/limb
        /// segment in the defender's root space for height and cross-deflection angle, blended with the next window's level during wind-up.
        /// </summary>
        static bool Incoming(Fighter self, float guardBlade, out float y, out float bladeAngle, out bool hasAngle)
        {
            y = 1.1f;
            bladeAngle = guardBlade;
            hasAngle = false;
            Fighter best = null;
            float bestDist = AnticipationRange;
            void Look(Fighter f, ref Fighter b, ref float bd)
            {
                if (f == null || ReferenceEquals(f, self) || f.team == self.team || !f.IsAttacking || f.move.hits.Count == 0) return;
                float ahead = (f.Pos.x - self.Pos.x) * self.facing;
                if (ahead < -0.15f) return;
                float dist = Mathf.Abs(ahead);
                if (dist > bd) return;
                bd = dist;
                b = f;
            }
            Look(self.opponent, ref best, ref bestDist);
            foreach (var f in self.Targets) Look(f, ref best, ref bestDist);
            if (best == null) return false;
            HitWindow next = null;
            foreach (var w in best.move.hits) if (w.to >= best.moveFrame) { next = w; break; }
            if (next == null) return false;
            float levelY = next.level switch { HitLevel.Overhead => 1.6f, HitLevel.High => 1.45f, HitLevel.Low => 0.45f, _ => 1.1f };
            y = levelY;
            if (self.rig != null && next.points != null && next.points.Length > 0 && best.TryLiveStrikeSegment(next.points[0], next, out var sa, out var sb))
            {
                var frame = RootFrame.Of(self.rig.transform);
                if (frame.Valid)
                {
                    Vector2 a = frame.ToRoot(sa), b = frame.ToRoot(sb);
                    Vector2 sample = Vector2.Lerp(a, b, 0.65f);
                    float liveBlend = best.moveFrame >= next.from ? 1f : best.moveFrame >= next.from - 4 ? 0.75f : 0.4f;
                    y = Mathf.Lerp(levelY, Mathf.Clamp(sample.y, 0.3f, 1.85f), liveBlend);
                    Vector2 seg = b - a;
                    if (seg.sqrMagnitude > 1e-4f)
                    {
                        float phi = KMath.Ang(seg);
                        float pref = ParryContactSolver.PreferredAngle(y, guardBlade);
                        float t1 = KMath.Wrap(phi + 90f), t2 = KMath.Wrap(phi - 90f);
                        bladeAngle = Mathf.Abs(Mathf.DeltaAngle(t1, pref)) <= Mathf.Abs(Mathf.DeltaAngle(t2, pref)) ? t1 : t2;
                        hasAngle = true;
                    }
                }
            }
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
