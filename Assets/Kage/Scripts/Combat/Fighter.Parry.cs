using UnityEngine;

namespace Kage
{
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
    /// <item><b>Attacker.</b> <see cref="GetParried"/>: it reels for the role's stagger frames (hit-stun, cut by the stunRes
    /// perk through <see cref="ScaleStun"/>), is pushed back, and takes the role's share of its posture bar, which can break its guard
    /// (the break's longer helplessness replaces the stagger). The event is <see cref="CombatEventType.Parried"/> with
    /// Source = the attacker, Target = the defender, Damage 0 and Posture = what the attacker took.</item>
    /// <item><b>AI foes</b> parry only when their AI chose to try one (<see cref="AIInputSource.ParryAttempt"/>), at a rate set
    /// by character data (<see cref="CharacterDef.parryRate"/>, 0 by default).</item>
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

        /// <summary>The parries this fighter has made since the round began.</summary>
        public int ParriesMade { get; private set; }

        /// <summary>The player always may; an AI foe only when its AI is trying a parry (its guard was not just a guard).</summary>
        bool ParryAllowed => isPlayer || input is not AIInputSource ai || ai.ParryAttempt;

        /// <summary>Once per tick, from <see cref="ReadGuard"/>: counts the window and the lockout, and takes a Guard press.</summary>
        void ParryTick(bool press)
        {
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
            ParriesMade = 0;
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
            _parryAge = NoPress;       // the press is spent; the lockout it started carries on, so parries can't be chained by mashing
            ParriesMade++;
            if (state == FState.Blockstun) SetState(FState.Idle);      // a parry leaves no blockstun: answer at once
            meter = Mathf.Min(100f, meter + t.meterGain * RageMul);
            if (info.origin == HitOrigin.Melee) LastParryPosture = attacker.GetParried(this, info);
            GameFeel.Hitstop(isPlayer ? t.playerHitstopFrames : t.foeHitstopFrames);
            GameFeel.Shake(t.shake);
            rig.Flash(new Color(1f, 0.9f, 0.65f), 0.3f);
            EffectCues.Play(ParryCue, info.contact, new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = 1f });
        }

        /// <summary>The role that picks the stagger and posture numbers: "player" for Kage, else the JS enemy type ("regular", "sub", "boss"; a foe with no JS entry is regular).</summary>
        string ParryRole => isPlayer ? "player" : (Character != null ? Rules.JsTables.Enemy(Character.id)?.Type : null) ?? "regular";

        /// <summary>
        /// This fighter's blow was parried by <paramref name="by"/>: it reels, is pushed back and takes posture. Returns the posture
        /// taken. A guard-break from it publishes with the Parried event's hit (<see cref="FlushGuardBreak"/>).
        /// </summary>
        float GetParried(Fighter by, in HitInfo info)
        {
            if (state == FState.KO) return 0f;
            var t = ParryT;
            string role = ParryRole;
            int frames = role switch { "player" => t.playerStaggerFrames, "sub" => t.subStaggerFrames, "boss" => t.bossStaggerFrames, _ => t.regularStaggerFrames };
            float share = role switch { "player" => t.playerPostureShare, "sub" => t.subPostureShare, "boss" => t.bossPostureShare, _ => t.regularPostureShare };
            if (_victim != null) ReleaseVictim(false);
            SetState(FState.Hitstun);
            _hurt = _hurtBody;
            _stun = ScaleStun(frames, t.stunResistFactor);
            float away = Mathf.Sign(Pos.x - by.Pos.x);
            if (away == 0f) away = -facing;
            Knock(new Vector2(KnockVelocity(new Vector2(away * t.pushback, 0f)).x, rb.linearVelocity.y));
            rig.Squash(0.06f);
            rig.Flash(Color.white, 0.5f, 10f);
            return AddPosture(PostureMax * share, true, new HitInfo { attacker = by, moveId = info.moveId, window = info.window, contact = info.contact });
        }

        // ---- the cue ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterParryCue() => EffectCues.Register(ParryCue, PlayParry);

        /// <summary>Placeholder: a gold screen flash, a star, two rings and a burst of sparks flying back at the attacker, and a "PARRY" call-out.</summary>
        static void PlayParry(Vector2 pos, in CueArgs a)
        {
            var t = ParryT;
            Vfx.I.Star(pos, 0.7f, Color.white, t.sparkTint);
            Vfx.I.Shockwave(pos, 0.7f, t.sparkTint);
            Vector2 toward = a.dir.x >= 0f ? Vector2.right : Vector2.left;
            for (int i = 0; i < t.sparks; i++)
                Vfx.I.Spark(pos, KMath.Dir(KMath.Ang(toward) + Random.Range(-70f, 70f)) * Random.Range(3.5f, 8f), t.sparkTint, Random.Range(0.025f, 0.05f), Random.Range(0.25f, 0.5f));
            GameFeel.Flash(t.flashTint, t.flashSeconds);
            KageGame.I?.Hud?.Float(pos + Vector2.up * 0.5f, "PARRY", t.sparkTint.WithA(1f), 1f);
            KageGame.I?.Audio?.Play(SfxKind.Counter, 0.7f);
        }
    }
}
