using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Pose output: LateUpdate, state poses, blends, gait and air poses, and motion trails.</summary>
    public partial class Fighter
    {
        static Pose _stance, _crouch, _block, _crouchBlock, _rise, _fall, _land, _hurtHigh, _hurtBody, _hurtLow, _tumble, _tumbleFront, _lie, _lieFront, _victory, _ref, _tuck;
        static Pose _apex, _airTuck, _fallLong, _preLand, _landDeep, _dashF, _dashB, _airDash, _skid;
        static void InitPoses()
        {
            if (_stance != null) return;
            _stance = PoseLib.Stance(); _crouch = PoseLib.Crouch(); _block = PoseLib.Block(); _crouchBlock = PoseLib.CrouchBlock();
            _rise = PoseLib.JumpRise(); _fall = PoseLib.JumpFall(); _land = PoseLib.Land();
            _hurtHigh = PoseLib.HurtHigh(); _hurtBody = PoseLib.HurtBody(); _hurtLow = PoseLib.HurtLow();
            _tumble = PoseLib.Tumble(); _tumbleFront = PoseLib.TumbleFront(); _lie = PoseLib.Lie(); _lieFront = PoseLib.LieFront(); _tuck = PoseLib.JumpTuck(); _victory = PoseLib.Victory(); _ref = PoseLib.Ref();
            _apex = PoseLib.JumpApex(); _airTuck = PoseLib.AirTuck(); _fall = PoseLib.FallOpen(); _fallLong = PoseLib.FallLong();
            _preLand = PoseLib.PreLand(); _landDeep = PoseLib.LandDeep();
            _dashF = PoseLib.DashFwd(); _dashB = PoseLib.DashBack(); _airDash = PoseLib.AirDash(); _skid = PoseLib.Skid();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _stance = null; }
        // =====================================================================================
        //  POSE OUTPUT
        // =====================================================================================
        void LateUpdate()
        {
            if (Headless) return;    // the fight loop poses headless fighters at each exact frame
            // the parry's snap runs on real time, so it plays through the hit-stop (a capture at a fixed frame rate uses that step)
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            rig?.RestorePainting();
            PoseOutput(Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime), Time.deltaTime, Time.time, real);
            rig?.DrawImpactPose();
            UpdateDodgeAfterimages();
        }

        float _lastPoseTime;

        /// <summary>
        /// A frame a headless fight's clock held (a hit-stop, FightLoop.StepHeadless): nothing of the fight moves, but a parry's clash pose
        /// still snaps in and holds, as it does on real time in play. Does nothing unless a parry's motion is on.
        /// </summary>
        public void PoseHitstopTick(float dt)
        {
            if (!_parryCtrl.HasActiveVisual) return;
            PoseOutput(0f, 0f, _lastPoseTime, dt);
        }

        float _koVyPrev;

        /// <summary>
        /// The KO ragdoll has no landing of its own: when its hips stop falling fast (they hit the floor), the soft parts get the
        /// landing's jolt and drape on the floor (<see cref="FighterRig.groundY"/>).
        /// </summary>
        void KoLandingJolt()
        {
            var hips = FallBody(B.Hips);
            if (hips == null) { _koVyPrev = 0f; return; }
            float vy = hips.linearVelocity.y;
            if (_koVyPrev < -2.5f && vy > _koVyPrev * 0.4f) LandSoftParts(-_koVyPrev);
            _koVyPrev = vy;
        }

        /// <summary>
        /// Poses the rig: <paramref name="alpha"/> is how far the rendered frame is past the last logic
        /// frame, <paramref name="dt"/> the time since the last pose and <paramref name="time"/> the clock
        /// for idle breathing and air flail. Play passes rendered time; the headless loop passes 0, 1/60 and frame time.
        /// <paramref name="realDt"/> is the real (unscaled) time since the last pose, which the parry's snap runs on through a hit-stop;
        /// below 0 it is <paramref name="dt"/>.
        /// </summary>
        public void PoseOutput(float alpha, float dt, float time, float realDt = -1f)
        {
            if (rig == null || def == null || rig.def == null || _stance == null) return;
            if (realDt < 0f) realDt = dt;
            if (dt > 0f) _lastPoseTime = time;
            if (_worldApplied < 1f)
            {
                // slowed by the world time scale (SAN-92): its logic frames come 1/s apart, so in play the pose interpolates
                // over the slowed world's frame, and secondary motion (cloth, hair) runs slow too
                if (!Headless && ctx != null) alpha = Mathf.Clamp01(ctx.Clock.WorldPhase + alpha * _worldApplied);
                dt *= _worldApplied;
            }
            if (state == FState.KO || FallingPhysically)
            {
                if (state == FState.KO) KoLandingJolt();
                rig.Secondary(dt, Vector2.zero);
                return;
            }
            float sf = stateFrame + alpha;
            switch (state)
            {
                case FState.Attack:
                    move?.Evaluate(def, _moveTime + alpha * PlaybackRate, _start, _display);
                    break;
                case FState.Idle:
                    if (_guardPose) { BlendTo(Unarmed(_block), sf, 5f); break; }     // the guard: the blade held straight up
                    _target.CopyFrom(Unarmed(_stance));
                    WeaponStance(_target);
                    SubBossStance(_target);
                    float br = Mathf.Sin(time * 2.4f + (isPlayer ? 0f : 1.3f));
                    _target.hip.y += br * 0.007f;
                    _target.a[(int)B.Chest] += br * 1.2f;
                    _target.a[(int)B.Head] -= br * 0.8f;
                    _target.MarkDirty();
                    BlendTo(_target, sf, 8f);
                    break;
                case FState.Walk:
                case FState.Run:
                    GaitPose(alpha);
                    BlendTo(Unarmed(_target), sf, state == FState.Run ? 6f : 4f);
                    break;
                case FState.Dash:
                    BlendTo(_dashAir ? _airDash : (_dashDir > 0 ? _dashF : _dashB), sf, 3f);
                    break;
                case FState.Skid: BlendTo(_skid, sf, 4f); break;
                case FState.Crouch: BlendTo(Unarmed(_guardPose ? _crouchBlock : _crouch), sf, 5f); break;
                case FState.Land:
                    Pose.Blend(def, _land, _landDeep, _landHard, _target);
                    BlendTo(_target, sf, 2.5f);
                    break;
                case FState.Blockstun: BlendTo(Unarmed(_crouchHeld ? _crouchBlock : _block), sf, 2f); break;
                case FState.Hitstun:
                    if (_stun > 6) BlendTo(_hurt ?? _hurtBody, sf, 3f);
                    else { Pose.Blend(def, _hurt ?? _hurtBody, _stance, 1f - _stun / 6f, _display); }
                    break;
                case FState.Air: AirPose(sf, time); break;
                case FState.Launched:
                {
                    _target.CopyFrom(_tumbleTo < 0f ? _tumbleFront : _tumble);
                    _target.a[(int)B.Hips] = Mathf.MoveTowards(_tumbleAng, _tumbleTo, Mathf.Abs(_tumbleVel) * alpha * Time.fixedDeltaTime);
                    _target.MarkDirty();
                    if (sf < 4f)
                    {
                        // blend limbs in, but never blend the spin itself (no unwinding)
                        _spinTmp.CopyFrom(_start);
                        _spinTmp.a[(int)B.Hips] = _target.a[(int)B.Hips];
                        _spinTmp.MarkDirty();
                        Pose.Blend(def, _spinTmp, _target, KMath.Apply(Ease.OutQuad, sf / 4f), _display);
                    }
                    else _display.CopyFrom(_target);
                    break;
                }
                case FState.Down: BlendTo(_liePose ?? _lie, sf, 7f); break;
                case FState.Thrown:
                    _target.CopyFrom(PoseLib.Thrown(_thrownSpin));
                    BlendTo(_target, sf, 3f);
                    break;
                case FState.Victory: BlendTo(_victory, sf, 24f); break;
                case FState.Intro: _display.CopyFrom(_ref); break;
                case FState.Ultimate:
                    if (_cmActive) CrimsonMovePoseOutput(alpha);        // Crimson Moon: Kage in plain sight through the whole art (SAN-82)
                    else if (_art != null) _art.PoseOutput(_ultFrame + alpha);   // the other weapons' arts: their own pose tracks (Fighter.RageArts.cs)
                    else if (_ultFrame < 66) BlendTo(_crouch, sf, 6f);
                    else _display.CopyFrom(_ref);
                    break;
            }
            if (_parryCtrl.HasActiveVisual || InParryWindow)
            {
                // the contact-matched parry (DynamicParryController): the blade on the clash point, the parried weapon locked then
                // recoiling, or the guard leaning towards a coming blow; over the pose with its grip already finished, so the two blend alike
                ApplyGrip(_display);
                _parryCtrl.ApplyPose(this, _display, alpha, realDt, GameFeel.InHitstop);
            }
            ApplyGrip(_display);     // no handle to hold, or the family holds it one-handed; a polearm's wide grip
            if (CharacterId == Garan.Id && WeaponId == "onikiri")
                rig.SetSlung(IsAttacking && move.id.StartsWith("garan.", System.StringComparison.Ordinal));
            rig.Apply(_display);
            rig.Secondary(dt, LogicVelocity);
            if (PresentsCombat) UpdateTrails();
        }

        /// <summary>
        /// Weapon-authentic idle guards for the four overhauled weapons (Mizuchi, Yamigama, Hammer, Onikiri).
        /// Katana, Fists, Twin and Tachi keep their baseline stance unchanged; SubBossStance overrides after this.
        /// </summary>
        void WeaponStance(Pose p)
        {
            if (BareHanded || Family == null) return;
            if (WeaponId == "iwakudaki")
            {
                // Hammer: counterbalanced two-handed maul guard resting angled back over trailing shoulder
                p.Hip(-0.04f, -0.11f, 6f).Chest(10f).Head(-6f).Feet(0.34f, -0.34f)
                    .HandF(0.14f, 1.18f).Blade(124f).Grip();
                return;
            }
            switch (Family.id)
            {
                case WeaponFamilies.Spear:
                    // Mizuchi: two-handed wide guard, lead hand mid-shaft, rear hand braced near butt
                    p.Hip(0.02f, -0.12f, -4f).Chest(-10f).Head(8f).Feet(0.34f, -0.32f)
                        .HandF(0.32f, 1.00f).Blade(18f).Grip();
                    break;
                case WeaponFamilies.Chain:
                    // Yamigama: nimble asymmetric assassin stance, kama forward in lead hand, fundo coiled in off-hand
                    p.Hip(0.03f, -0.11f, -5f).Chest(-14f).Head(10f).Feet(0.33f, -0.30f)
                        .HandF(0.34f, 0.98f).Blade(32f).HandN(0.02f, 0.94f, -10f);
                    break;
                case WeaponFamilies.Cleaver:
                    // Onikiri: heavy forward-leaning aggressive executioner guard
                    p.Hip(0.03f, -0.14f, -5f).Chest(-14f).Head(10f).Feet(0.36f, -0.34f)
                        .HandF(0.24f, 0.94f).Blade(52f).Grip();
                    break;
            }
        }

        // Keep the ankles pinned by the same IK as the walk cycle. The blade and both wrists
        // resolve from the new guard before we blend out into an attack.
        void SubBossStance(Pose p)
        {
            switch (CharacterId)
            {
                case Kuroe.Id:
                    p.Hip(0.04f,-0.11f,-5f).Chest(-17f).Head(13f).Feet(0.34f,-0.32f)
                        .HandF(0.30f,0.86f).Blade(24f);
                    break;
                case Garan.Id:
                    p.Hip(-0.025f,-0.15f,1f).Chest(2f).Head(-3f).Feet(0.40f,-0.36f)
                        .HandF(0.21f,1.02f).Blade(62f);
                    break;
                case Hisame.Id:
                    p.Hip(0.045f,-0.08f,-4f).Chest(-14f).Head(12f).Feet(0.30f,-0.30f)
                        .HandF(0.34f,0.99f).Blade(8f).BladeN(48f);
                    break;
                case Shiden.Id:
                    p.Hip(0,-0.11f,1f).Chest(-3f).Head(2f).Feet(0.27f,-0.28f)
                        .HandF(0.23f,1.06f).Blade(40f);
                    break;
            }
        }

        void BlendTo(Pose target, float sf, float frames)
        {
            float t = KMath.Apply(Ease.OutQuad, sf / frames);
            Pose.Blend(def, _start, target, t, _display);
        }

        /// <summary>Walk and run cycles share one phase, blended by how far into a run the fighter is.</summary>
        void GaitPose(float alpha)
        {
            float ph = _gaitPhase + _gaitRate * alpha * Time.fixedDeltaTime;
            PoseLib.WalkCycle(ph, _gaitDir, _walk);
            if (_runBlend > 0.005f)
            {
                PoseLib.RunCycle(ph, _run);
                Pose.Blend(def, _walk, _run, _runBlend, _target);
            }
            else _target.CopyFrom(_walk);
        }

        /// <summary>
        /// Velocity-driven air pose (no somersault): rise → knees up at the apex → legs open and arms
        /// lift as the fall builds → a long-fall reach → feet reaching for the floor just before touchdown.
        /// The body leans into its horizontal travel and a second jump kicks in a quick tuck.
        /// </summary>
        void AirPose(float sf, float t)
        {
            Vector2 vel = LogicVelocity;
            float vy = vel.y, vxl = vel.x * facing;

            float kApex = KMath.Smooth01(Mathf.InverseLerp(6f, 0.6f, vy));
            Pose.Blend(def, _rise, _apex, kApex, _airA);
            float kFall = KMath.Smooth01(Mathf.InverseLerp(0.4f, -5f, vy));
            Pose.Blend(def, _airA, _fall, kFall, _airB);
            float kLong = KMath.Smooth01(Mathf.InverseLerp(-7f, -12.5f, vy));
            Pose.Blend(def, _airB, _fallLong, kLong, _airA);

            // second jump: a quick tuck that opens back out
            if (_airJumpFrame < 18)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01((_airJumpFrame + sf - stateFrame) / 18f));
                Pose.Blend(def, _airA, _airTuck, k * 0.9f, _airB);
                _airA.CopyFrom(_airB);
            }

            // anticipate the floor: time to impact from height and fall speed
            float tGround = vy < -0.5f ? Mathf.Max(0f, rb.position.y) / -vy : 9f;
            float kLand = KMath.Smooth01(Mathf.InverseLerp(0.16f, 0.04f, tGround));
            Pose.Blend(def, _airA, _preLand, kLand, _target);

            // balance flail while falling, fading as the feet reach for the floor
            float flail = kFall * (1f - kLand);
            if (flail > 0.01f)
            {
                _target.target[(int)Limb.ArmN] += new Vector2(Mathf.Sin(t * 8.3f) * 0.035f, Mathf.Cos(t * 6.1f) * 0.03f) * flail;
                _target.target[(int)Limb.ArmF] += new Vector2(Mathf.Sin(t * 7.1f + 1.3f) * 0.03f, Mathf.Cos(t * 9.2f) * 0.025f) * flail;
                _target.a[(int)B.Head] += Mathf.Sin(t * 5.3f) * 2.5f * flail;
            }
            // lean into the travel; rising pitches slightly back, falling slightly forward
            _target.MarkDirty();
            _target.SpinAbout(def, Mathf.Clamp(-vxl * 2.6f, -10f, 10f) + Mathf.Clamp(vy * 0.35f, -3f, 3f) * (1f - kLand));
            BlendTo(_target, sf, 4f);
        }

        void UpdateTrails()
        {
            bool blade = false, bladeN = false, fist = false, footN = false, footF = false;
            if (state == FState.Attack && move != null)
            {
                foreach (var e in move.fx)
                {
                    if (moveFrame < e.from || moveFrame > e.to) continue;
                    switch (e.kind)
                    {
                        case FxKind.TrailBlade: blade = true; break;
                        case FxKind.TrailBladeN: bladeN = true; break;
                        case FxKind.TrailFist: fist = true; break;
                        case FxKind.TrailFootN: footN = true; break;
                        case FxKind.TrailFootF: footF = true; break;
                    }
                }
            }
            else if (state == FState.Ultimate && _art != null) { blade = _art.trail; bladeN = _art.trail && Dual; }     // a rage art's own cuts (Fighter.RageArts.cs)
            bool powered = state == FState.Ultimate;
            if ((blade || bladeN) && move != null && state == FState.Attack)
                foreach (var window in move.hits)
                    if ((window.heavy || window.critical || window.launch) && moveFrame >= window.from - 2 && moveFrame <= window.to + 2)
                    { powered = true; break; }
            if (_trailBlade is MotionTrail far) far.shedEmbers = powered;
            if (_trailBladeN is MotionTrail near) near.shedEmbers = powered;
            if (blade && state == FState.Attack && move != null && move.HasChainArc && TryChainWhipWorld(0f, out var chHand, out var chWeight, out var chCtrl, out float chExt) && chExt > 0.12f)
            {
                Vector2 inner = ChainBezier(chHand, chCtrl, chWeight, 0.62f);
                _trailBlade.Tick(true, inner, chWeight);
            }
            else
                _trailBlade.Tick(blade, rig.World(B.Sword, Vector2.Lerp(def.bladeBaseLocal, BladeTipLocal, 0.38f)), rig.World(B.Sword, BladeTipLocal * 1.04f));
            _trailBladeN.Tick(bladeN, rig.World(B.HandN, Vector2.Lerp(NearBladeBaseLocal, NearBladeTipLocal, 0.38f)), rig.World(B.HandN, NearBladeTipLocal + Vector2.right * 0.02f));
            Vector2 fistC = rig.World(B.HandN, def.fistLocal);
            _trailFist.Tick(fist, fistC + Vector2.up * 0.03f, fistC - Vector2.up * 0.03f);
            _trailFootN.Tick(footN, rig.World(B.FootN, def.footTipLocal), rig.World(B.LegNL, new Vector2(0f, -0.12f)));
            _trailFootF.Tick(footF, rig.World(B.FootF, def.footTipLocal), rig.World(B.LegFL, new Vector2(0f, -0.12f)));
        }
    }
}
