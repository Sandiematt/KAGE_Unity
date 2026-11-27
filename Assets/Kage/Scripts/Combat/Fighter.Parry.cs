using UnityEngine;

namespace Kage
{
    /// <summary>A fighter's tier in the parry exchange (<see cref="Fighter.Tier"/>): what a parry does to it, and how well its AI parries.</summary>
    public enum CombatTier { Player, Regular, Elite, Sub, Boss }

    /// <summary>
    /// The parry (SAN-91, ADR-0004 defence step 5). A Guard press opens a parry window of
    /// <see cref="ParryTuning.windowFrames"/> of the defender's frames: a hit that reaches a guarding fighter while the window
    /// is open is parried instead of blocked. Measured as <see cref="FramesSinceGuardPress"/> (0 on the frame of the press,
    /// so a hit on the same frame is inside), and inclusive: a press 8 frames before the hit parries it, 9 frames before
    /// only blocks it.
    /// <list type="bullet">
    /// <item><b>Lockout.</b> A press opens a window only when parrying isn't locked out, and locks it for
    /// <see cref="ParryTuning.lockoutFrames"/> frames from that press. A press inside the lockout only guards (it neither
    /// opens a window nor ends the one that is open). A parry doesn't clear it. So a press that parries nothing (too early,
    /// too late) costs the next 0.33 s, and mashing Guard gives at most one window in twenty frames.</item>
    /// <item><b>What parries.</b> A melee window (or the Thousand Shadows cut) that isn't Unblockable, on a fighter that
    /// <see cref="IsGuarding"/>. The hit level doesn't matter (the JS game has none): a standing guard parries a low sweep.
    /// A projectile is only blocked unless <see cref="ParryTuning.parryProjectiles"/>; a hazard never is.</item>
    /// <item><b>Defender.</b> No damage, no posture, no blockstun: it keeps its guard and can answer at once. It gains
    /// <see cref="ParryTuning.meterGain"/> Rage and the <c>VFX_Kage_Parry</c> cue plays with a short hit-stop.</item>
    /// <item><b>Attacker.</b> <see cref="GetParried"/>: it reels for its tier's stagger frames (<see cref="Tier"/>: regular, elite, sub-boss,
    /// boss, Kage; hit-stun, cut by the stunRes perk through <see cref="ScaleStun"/>), is pushed back, and takes the tier's share of its
    /// posture bar, which can break its guard (the break's longer helplessness replaces the stagger). The event is
    /// <see cref="CombatEventType.Parried"/> with Source = the attacker, Target = the defender, Damage 0 and Posture = what the attacker took.</item>
    /// <item><b>Two ways.</b> Kage parries every tier's blows (only the aura-marked unblockables are dodge-only). Foes parry him back
    /// (docs/combat-flow.md): their own crimson cue and a "PARRIED" call-out over Kage; his stagger can be left from
    /// <see cref="ParryTuning.playerRecoverFrames"/> by a guard or a dodge (<see cref="TryStaggerEscape"/>); and the foe's answer shows a
    /// tell as it winds up (<see cref="AnswerTell"/>).</item>
    /// <item><b>AI foes</b> parry only when their AI chose to try one (<see cref="AIInputSource.ParryAttempt"/>): the character's or
    /// profile's parry rate, more against a repeated attack or into a held guard, scaled by its tier (AIInputSource.Profile.cs).</item>
    /// </list>
    /// Every number is in Resources/Tuning/parry.json (<see cref="ParryTuning"/>).
    /// </summary>
    public partial class Fighter
    {
        public const string ParryCue = "VFX_Kage_Parry";

        const int NoPress = 9999;
        int _parryLock;
        int _parryAge = NoPress;

        static ParryTuning ParryT => Tuning.Get<ParryTuning>();

        /// <summary>Frames since the Guard press that opened the open (or last) parry window; large when none. Equals <see cref="FramesSinceGuardPress"/> unless a later press was locked out.</summary>
        public int ParryWindowAge => _parryAge;

        /// <summary>Frames left of the lockout: a Guard press now only guards.</summary>
        public int ParryLockoutLeft => _parryLock;

        /// <summary>True when a hit reaching this fighter now would be parried (not counting the hit's own level or kind).</summary>
        public bool InParryWindow => IsGuarding && _parryAge <= ParryT.windowFrames && ParryAllowed;

        /// <summary>The posture the last parry this fighter made took off its attacker (the Parried event's Posture); 0 when it parried nothing.</summary>
        public float LastParryPosture { get; private set; }

        /// <summary>True when this fighter's last parry landed inside the tightest peak frames (&lt;= 3 frames after pressing Guard).</summary>
        public bool LastParryPerfect { get; private set; }

        /// <summary>The parries this fighter has made since the round began.</summary>
        public int ParriesMade { get; private set; }

        /// <summary>The player always may; an AI foe only when its AI is trying a parry (its guard was not just a guard).</summary>
        bool ParryAllowed => isPlayer || input is not AIInputSource ai || ai.ParryAttempt;

        // ---- the contact-matched parry (ParryContactSolver, DynamicParryController) ----
        readonly DynamicParryController _parryCtrl = new();

        /// <summary>This fighter's parry motion: its blade brought onto the clash point when it parries, its weapon locked and recoiling when it is parried.</summary>
        public DynamicParryController ParryController => _parryCtrl;

        /// <summary>The last parry this fighter made, solved: where the blades met and how it stood to meet them.</summary>
        public ParryContactSolution LastParrySolution { get; private set; }

        /// <summary>World point where this fighter's blade met the blow it last parried (the clash, not the hurt circle): the cue and the call-out anchor there.</summary>
        public Vector2 LastParryContact { get; private set; }

        /// <summary>True once this fighter has parried a blow this round (<see cref="LastParryContact"/> is set).</summary>
        public bool HasParryContact { get; private set; }

        internal void NoteParryContact(in ParryContactSolution sol)
        {
            LastParrySolution = sol;
            LastParryContact = sol.clashWorld;
            HasParryContact = true;
        }

        /// <summary>The guard this fighter parries from: the low guard when Down is held, bare fists when it has no blade.</summary>
        internal Pose ParryGuardPose() => Unarmed(_crouchHeld ? _crouchBlock : _block);

        /// <summary>This fighter's blade, grip and frame for <see cref="ParryContactSolver"/>.</summary>
        internal DefenderBladeSpec ParryBladeSpec()
        {
            bool polearm = Family != null && Family.grip == GripPose.Polearm;
            bool heavy = Family != null && Family.grip == GripPose.HeavyTwoHand;
            return new DefenderBladeSpec
            {
                rigDef = def,
                frame = rig != null ? RootFrame.Of(rig.transform) : RootFrame.Of(Pos, facing, transform.localScale.y),
                bladeBaseLocal = BladeBaseLocal, bladeTipLocal = StaticBladeTip,
                bareHanded = BareHanded, gripHeld = GripHeld, dual = Dual, polearm = polearm,
                keepPointOffFloor = polearm || (Family != null && Family.keepPointOffFloor), crouchHeld = _crouchHeld,
                gripSpacing = polearm ? Pose.PolearmSpacing : heavy ? Pose.HeavySpacing : Pose.GripSpacing,
                gripPommel = polearm ? Pose.PolearmPommel : heavy ? Pose.HeavyPommel : Pose.GripPommel,
                reach = BladeReach, guardPlaneX = ParryT.guardPlaneX,
            };
        }

        /// <summary>The fighter's grip finish (<see cref="ApplyGrip"/>) on a pose the parry built.</summary>
        internal void FinishGrip(Pose p) => ApplyGrip(p);

        /// <summary>Once per tick, from <see cref="ReadGuard"/>: counts the window and the lockout, and takes a Guard press.</summary>
        void ParryTick(bool press)
        {
            _parryCtrl.LogicTick(this);
            if (_parryLock > 0) _parryLock--;
            if (_parryAge < NoPress) _parryAge++;
            if (!press || _parryLock > 0) return;
            _parryAge = 0;
            _parryLock = ParryT.lockoutFrames;
        }

        void ResetParry()
        {
            _parryLock = 0;
            _parryAge = NoPress;
            LastParryPosture = 0f;
            LastParryPerfect = false;
            ParriesMade = 0;
            LastParrySolution = default;
            LastParryContact = default;
            HasParryContact = false;
            _parryLockExtension = 0f;
            _parryLockChainArc = false;
            _parryCtrl.Reset();
        }

        partial void ParryCheck(ref DefenceCheck c)
        {
            var info = c.info;
            var w = info.w;
            LastParryPosture = 0f;
            if (w == null || w.level == HitLevel.Unblockable || info.attacker == null || ReferenceEquals(info.attacker, this)) return;
            if (info.origin == HitOrigin.Hazard) return;
            var t = ParryT;
            if (info.origin == HitOrigin.Projectile && !t.parryProjectiles) return;
            if (!IsGuarding || _parryAge > t.windowFrames || !ParryAllowed) return;
            c.resolved = true;
            c.result = HitResult.Parried;
            Parry(info);
        }

        void Parry(in HitInfo info)
        {
            var t = ParryT;
            var attacker = info.attacker;
            LastParryPerfect = _parryAge <= 3;
            _parryAge = NoPress;       // the press is spent; the lockout it started carries on, so parries can't be chained by mashing
            ParriesMade++;
            TurnAfterParry(attacker);                                      // a blow from behind: turn to it, and keep it as the target (Fighter.Counter.cs)
            if (state == FState.Blockstun) SetState(FState.Idle);      // a parry leaves no blockstun: answer at once
            // the contact-matched parry: the blade brought onto the incoming weapon at the point the two meet (ParryContactSolver)
            var sol = _parryCtrl.SolveDefender(this, attacker, info.Strike);
            NoteParryContact(sol);
            meter = Mathf.Min(100f, meter + t.meterGain * RageMul);
            if (info.origin == HitOrigin.Melee) LastParryPosture = attacker.GetParried(this, info, sol);
            rig?.Impulse(new Vector2(-facing * 7.6f, 2.5f), 1.1f);
            attacker.rig?.Impulse(new Vector2(-attacker.facing * 5.2f, 1.9f), 1.1f);
            GameFeel.Hitstop(isPlayer ? t.playerHitstopFrames : t.foeHitstopFrames);
            NoteDefended();                                                // a parry opens the weapon's riposte (Fighter.Style.cs)
            if (PresentsCombat) GameFeel.Shake(t.shake);
            if (state is not FState.Attack and not FState.Hitstun) _parryCtrl.ActivateDefender(this);     // locked steel on steel through the hit-stop, then back to the guard
            Vector2 at = LastParryContact;
            Vector2 dir = sol.valid && sol.clashNormalWorld.sqrMagnitude > 0.5f ? sol.clashNormalWorld : new Vector2(facing, 0f);
            if (PresentsCombat)
            {
                rig.HitShake(-dir, 0.017f, 0.11f);
                attacker.rig?.HitShake(dir, 0.021f, 0.11f);
            }
            if (isPlayer)
            {
                if (PresentsCombat) rig.Flash(new Color(1f, 0.9f, 0.65f), 0.3f);
                EffectCues.Play(ParryCue, at, new CueArgs { owner = this, dir = dir, scale = LastParryPerfect ? 1.5f : 1f });
                return;
            }
            // a foe turned Kage's blow aside: its own look and sound, a call-out over Kage, and its answer will show a tell as it winds up
            if (PresentsCombat)
            {
                rig.Flash(t.foeParryTint, 0.45f, 10f);
                attacker.rig?.Flash(t.foeParryTint, 0.55f, 10f);
            }
            EffectCues.Play(FoeParryCue, at, new CueArgs { owner = this, dir = dir, scale = 1f });
            _answerTellLeft = t.answerTellWindow;
        }

        /// <summary>
        /// This fighter's place in the parry exchange (and the AI's skill at it, <see cref="AIInputSource"/>): Kage; a sub-boss or a boss by the JS enemy
        /// type; an elite foe (a regular foe the character data puts in armour or behind a shield: the Ironclad, the Husk, the Shieldbearer);
        /// every other foe, and one with no JS entry, regular.
        /// </summary>
        public CombatTier Tier => isPlayer ? CombatTier.Player : TierOf(Character);

        /// <summary>The tier of a character (<see cref="Tier"/>): Kage's is Player; null or a character with no JS entry and no armour is Regular.</summary>
        public static CombatTier TierOf(CharacterDef c)
        {
            if (c == null) return CombatTier.Regular;
            if (c.IsPlayer) return CombatTier.Player;
            string type = Rules.JsTables.Enemy(c.id)?.Type;
            if (type == "boss") return CombatTier.Boss;
            if (type == "sub") return CombatTier.Sub;
            return (c.flags & (CharacterFlags.Armour | CharacterFlags.FrontalArmour)) != 0 ? CombatTier.Elite : CombatTier.Regular;
        }

        /// <summary>
        /// This fighter's blow was parried by <paramref name="by"/>: it reels, is pushed back and takes posture. Returns the posture
        /// taken. A guard-break from it publishes with the Parried event's hit (<see cref="FlushGuardBreak"/>). Kage's own stagger can be
        /// left early from <see cref="ParryTuning.playerRecoverFrames"/> by a guard or a dodge (<see cref="TryStaggerEscape"/>), so the
        /// parrier's answer can be met.
        /// </summary>
        float GetParried(Fighter by, in HitInfo info) => GetParried(by, info, default);

        /// <summary>
        /// As <see cref="GetParried(Fighter, in HitInfo)"/>, with the parry's solved clash (<paramref name="sol"/>): this fighter's weapon is held
        /// in its impact pose, locked on the parrier's blade, through the hit-stop, then recoils along the clash normal into the reel
        /// (<see cref="DynamicParryController"/>). Frames, pushback and posture are the same either way.
        /// </summary>
        float GetParried(Fighter by, in HitInfo info, in ParryContactSolution sol)
        {
            if (state == FState.KO) return 0f;
            bool lockOn = sol.valid && info.origin == HitOrigin.Melee;
            if (lockOn) ParryImpactPose(info, _parryImpact);          // the pose of the blow at the moment of the clash
            _parryLockChainArc = false;
            if (!lockOn || move == null || state != FState.Attack)
            {
                _parryLockExtension = 0f;
            }
            else
            {
                float at = info.hasStrikeGeometry && info.strikeTime >= 0f && move.id == info.moveId ? info.strikeTime : _moveTime;
                if (move.HasChainArc && move.ChainAt(at, out float cm, out _parryLockChainAngle, out _parryLockChainBow, out _parryLockChainSpin) && cm > 0.02f)
                {
                    _parryLockChainArc = true;
                    _parryLockExtension = cm * Weapon.reach;
                }
                else
                {
                    _parryLockExtension = info.hasStrikeGeometry && info.strikeTime >= 0f && move.id == info.moveId ? move.ExtensionAt(info.strikeTime) * Weapon.reach : BladeExtension;
                }
            }
            float taken = GetParriedCore(by, info, sol.valid ? sol.clashWorld : info.contact);
            if (lockOn && state == FState.Hitstun) _parryCtrl.BeginAttacker(this, _parryImpact, sol, info.hasStrikeGeometry ? info.strikePoint : HitPoint.Blade);
            return taken;
        }

        readonly Pose _parryImpact = new();

        float GetParriedCore(Fighter by, in HitInfo info, Vector2 breakContact)
        {
            var t = ParryT;
            var tier = Tier;
            int frames = t.StaggerFor(tier);
            float share = t.PostureShareFor(tier);
            if (_victim != null) ReleaseVictim(false);
            SetState(FState.Hitstun);
            _hurt = _hurtBody;
            _stun = ScaleStun(frames, t.stunResistFactor);
            if (tier == CombatTier.Player && t.playerRecoverFrames > 0) _recoverAt = Mathf.Min(_stun, t.playerRecoverFrames);
            float away = Mathf.Sign(Pos.x - by.Pos.x);
            if (away == 0f) away = -facing;
            Knock(new Vector2(KnockVelocity(new Vector2(away * t.pushback, 0f)).x, rb.linearVelocity.y));
            rig.Squash(0.06f);
            rig.Flash(Color.white, 0.5f, 10f);
            float taken = AddPosture(PostureMax * share, true, new HitInfo { attacker = by, moveId = info.moveId, window = info.window, contact = breakContact });
            OpenToCounter(by, _stun);                                      // the parrier's next clean blow is a counter hit (Fighter.Counter.cs)
            return taken;
        }

        // ---- being parried: the stagger's early way out (Kage) ----
        /// <summary>The stagger frame from which <see cref="TryStaggerEscape"/> may end the current hitstun; -1 when it can't (every other reel). SetState clears it.</summary>
        int _recoverAt = -1;

        /// <summary>The frame of the current reel from which a guard or a dodge ends it (Kage's stagger from a parried blow); -1 for a reel that must run out.</summary>
        public int StaggerRecoverAt => _recoverAt;

        /// <summary>
        /// Kage's stagger from a parried blow, from <see cref="ParryTuning.playerRecoverFrames"/> on: Guard held ends it into the guard (a Guard pressed
        /// during the stagger has already opened its parry window, so the parrier's answer can be parried), a dash press dashes the stick's way, a
        /// double tap rolls or hops back. Nothing else ends it early: no attack comes out of a stagger. True when it ended.
        /// </summary>
        bool TryStaggerEscape(in FrameInput inp, int fwd)
        {
            if (_recoverAt < 0 || stateFrame < _recoverAt || !Grounded || health <= 0f || GuardBroken) return false;
            if (_guardHeld) { _juggle = 0; SetState(FState.Idle); return true; }
            if (_dashBuf <= 4 && _dashCool <= 0) { _dashBuf = 99; _juggle = 0; StartDash(fwd < 0 ? -1 : 1, false); return true; }
            bool tapFwd = facing > 0 ? inp.tapRight2 : inp.tapLeft2;
            bool tapBack = facing > 0 ? inp.tapLeft2 : inp.tapRight2;
            if (tapBack && moves?.backflip != null) { _juggle = 0; StartMove(moves.backflip); return true; }
            if (tapFwd && moves?.roll != null) { _juggle = 0; StartMove(moves.roll); return true; }
            return false;
        }

        // ---- a foe's answer to its own parry: the tell ----
        int _answerTellLeft;

        /// <summary>Frames left in which this foe's next attack is the answer to its own parry, and shows the tell as it winds up (0 when none).</summary>
        public int AnswerTellLeft => _answerTellLeft;

        void AnswerTellTick() { if (_answerTellLeft > 0) _answerTellLeft--; }
        void ResetAnswerTell() => _answerTellLeft = 0;

        /// <summary>A foe's first attack after its parry: a glint at the weapon hand, a "!" over it and a steel ring, as the wind-up starts.</summary>
        void AnswerTell(MoveDef m)
        {
            if (_answerTellLeft <= 0 || isPlayer || m == null || m.hits.Count == 0) return;
            _answerTellLeft = 0;
            var t = ParryT;
            Vector2 hand = rig != null ? (Vector2)rig.World(B.HandF, Vector2.zero) : Pos + Vector2.up;
            Vfx.I?.Glint(hand, t.foeParryTint);
            KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.3f, "!", t.foeParryTint, 1.2f);
            KageGame.I?.Audio?.Play(SfxKind.Block, 0.45f);
        }

        // ---- the cues ----
        /// <summary>A foe's parry of Kage's blow: crimson steel, so it never reads as his own.</summary>
        public const string FoeParryCue = "VFX_Foe_Parry";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterParryCue()
        {
            EffectCues.Register(ParryCue, PlayParry);
            EffectCues.Register(FoeParryCue, PlayFoeParry);
        }

        /// <summary>The way a parry cue throws its sparks: the clash normal (any direction), right when none.</summary>
        static Vector2 SparkDir(in CueArgs a) => a.dir.sqrMagnitude > 0.01f ? a.dir.normalized : Vector2.right;

        /// <summary>
        /// Kage's parry, the metallic clash: crossed steel streaks, a white star and a ring, a burst of gold sparks flying back at the attacker with a
        /// white spray off his own blade, a gold flash with an impact punch (the frame the camera jolts and the colours split), a "PARRY"
        /// call-out, a ringing clang over the chime.
        /// </summary>
        static void PlayParry(Vector2 pos, in CueArgs a)
        {
            var t = ParryT;
            Vector2 toward = SparkDir(a);
            bool perfect = a.scale > 1f;
            Vfx.I.CrossedSteel(pos, toward, t.sparkTint, Color.white, perfect ? 0.69f : 0.42f);
            Vfx.I.Star(pos, 0.7f, Color.white, t.sparkTint);
            if (perfect)
            {
                Vfx.I.Ring(pos, 0.09f, 1.24f, 0.075f, t.sparkTint, 0.28f, true);
                Vfx.I.Ring(pos, 0.08f, 0.82f, 0.045f, Color.white, 0.17f, true);
                GameFeel.Kick(toward, 0.12f);
                GameFeel.ImpactFrame(new Color(0.12f, 0.065f, 0.025f, 0.82f), 2f / 60f);
            }
            else Vfx.I.Shockwave(pos, 0.7f, t.sparkTint);
            for (int i = 0; i < t.sparks; i++)
                Vfx.I.Spark(pos, KMath.Dir(KMath.Ang(toward) + Random.Range(-70f, 70f)) * Random.Range(3.5f, 8f), t.sparkTint, Random.Range(0.025f, 0.05f), Random.Range(0.25f, 0.5f));
            for (int i = 0; i < t.sparks / 3; i++)
                Vfx.I.Spark(pos, KMath.Dir(KMath.Ang(-toward) + Random.Range(-45f, 45f)) * Random.Range(2f, 4.5f) + Vector2.up * 1.2f, Color.white, Random.Range(0.02f, 0.04f), Random.Range(0.2f, 0.35f));
            GameFeel.Flash(t.flashTint, t.flashSeconds + (perfect ? 3f / 60f : 0f));
            GameFeel.Punch(t.impactPunch + (perfect ? 0.075f : 0f));
            a.owner?.CombatAudio?.Play(SfxKind.Clang, t.clangVolume);
            a.owner?.CombatAudio?.Play(perfect ? SfxKind.PerfectChime : SfxKind.Counter, perfect ? 0.78f : 0.45f);
        }

        /// <summary>
        /// A foe's parry of Kage's blow: crimson steel streaks, a white-and-crimson star and ring, sparks thrown at Kage, a dark red flash and a lower,
        /// harsher clang (a warning, not a reward). The "PARRIED" call-out over Kage and the tell on the foe's answer come from the fighter.
        /// </summary>
        static void PlayFoeParry(Vector2 pos, in CueArgs a)
        {
            var t = ParryT;
            Vector2 toward = SparkDir(a);
            Vfx.I.CrossedSteel(pos, toward, t.foeParryTint, new Color(1f, 0.85f, 0.8f), 0.38f);
            Vfx.I.Star(pos, 0.6f, Color.white, t.foeParryTint);
            Vfx.I.Ring(pos, 0.1f, 0.75f, 0.07f, t.foeParryTint, 0.32f, true);
            for (int i = 0; i < t.sparks; i++)
            {
                var c = i % 3 == 0 ? Color.white : t.foeParryTint;
                Vfx.I.Spark(pos, KMath.Dir(KMath.Ang(toward) + Random.Range(-60f, 60f)) * Random.Range(3f, 7f), c, Random.Range(0.025f, 0.05f), Random.Range(0.22f, 0.45f));
            }
            GameFeel.Flash(t.foeParryFlashTint, t.flashSeconds);
            a.owner?.CombatAudio?.Play(SfxKind.FoeParry, t.clangVolume);
        }
    }
}
