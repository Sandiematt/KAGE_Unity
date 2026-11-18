using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Posture and guard-break (ADR-0004 steps 6 and 7, SAN-86). Every fighter has a posture bar of
    /// <see cref="PostureMax"/> (character data). A guarded hit fills it by damage × <see cref="GuardTuning.blockPostureFactor"/>
    /// × the window's postureMul; a clean hit by a little (<see cref="GuardTuning.hitPostureFactor"/>), never past full and
    /// never breaking the guard. After <see cref="GuardTuning.drainDelayFrames"/> without a gain it drains (not while Guard
    /// is held, by default). A full bar breaks the guard: <see cref="GuardTuning.breakFrames"/> helpless in hitstun, and
    /// the next clean hit in them is a guaranteed critical. All numbers are in Resources/Tuning/guard.json.
    /// <para>
    /// Hooks for the next defence tickets: a parry (SAN-91) or a clash (SAN-93) takes posture with
    /// <see cref="AddPosture(float, Fighter, string)"/>, which may break the guard; the <see cref="CombatEventType.GuardBroken"/>
    /// event then waits for <see cref="FlushGuardBreak"/>, so it follows the event of the hit that caused it.
    /// </para>
    /// </summary>
    public partial class Fighter
    {
        /// <summary>Current posture, 0 to <see cref="PostureMax"/>.</summary>
        public float Posture { get; private set; }

        /// <summary>Posture as a share of the bar (what the HUD shows).</summary>
        public float PostureFill => PostureMax > 0f ? Mathf.Clamp01(Posture / PostureMax) : 0f;

        /// <summary>True while the fighter is helpless after a guard-break and the next hit is a guaranteed critical.</summary>
        public bool GuardBroken => _guardBreakLeft > 0;

        /// <summary>Posture the last ReceiveHit added (after the cap), for the event stream.</summary>
        public float LastPostureTaken { get; private set; }

        /// <summary>The last ReceiveHit landed as a critical (rolled by the attacker, or guaranteed by a guard-break).</summary>
        public bool LastHitCrit { get; private set; }

        /// <summary>The damage of the last ReceiveHit after the guaranteed critical (before health clamps).</summary>
        public int LastHitDamage { get; private set; }

        int _postureIdle, _guardBreakLeft;
        float _hitPosture;

        // the GuardBroken event waits until the hit that caused it has published its own (FlushGuardBreak)
        bool _breakPending;
        Fighter _breakBy;
        string _breakMove;
        int _breakWindow;
        Vector2 _breakContact;

        static Pose _guardBrokenPose;

        /// <summary>
        /// Adds posture from a parry, a clash or any other source that is not a received hit; a full bar breaks the guard.
        /// Returns the posture added (after the cap). The <see cref="CombatEventType.GuardBroken"/> event is published by the
        /// next <see cref="FlushGuardBreak"/> (the fight loop's hit resolution calls it; a caller outside it calls it after
        /// publishing its own event).
        /// </summary>
        public float AddPosture(float amount, Fighter by, string moveId = null) =>
            AddPosture(amount, true, new HitInfo { attacker = by, moveId = moveId, window = -1, contact = Pos + Vector2.up * 1.1f });

        /// <summary>Adds posture from <paramref name="source"/>; with <paramref name="canBreak"/> a full bar breaks the guard. Returns the posture added.</summary>
        float AddPosture(float amount, bool canBreak, in HitInfo source)
        {
            if (amount <= 0f || state == FState.KO || PostureMax <= 0f) return 0f;
            _postureIdle = 0;
            float before = Posture;
            Posture = Mathf.Min(PostureMax, Posture + amount);
            float added = Posture - before;
            _hitPosture += added;
            if (canBreak && Posture >= PostureMax) BreakGuard(source);
            return added;
        }

        /// <summary>The bar is full: the guard breaks. The fighter is helpless for breakFrames, and the next hit is a critical.</summary>
        void BreakGuard(in HitInfo source)
        {
            var t = Tuning.Get<GuardTuning>();
            Posture = PostureMax * Mathf.Clamp01(t.postureAfterBreak);
            if (_victim != null) ReleaseVictim(false);
            SetState(FState.Hitstun);
            _guardBrokenPose ??= PoseLib.GuardBroken();
            _hurt = _guardBrokenPose;
            _stun = t.breakFrames;
            _guardBreakLeft = t.breakFrames;
            rig.Squash(0.08f);
            rig.Flash(new Color(1f, 0.35f, 0.3f), 0.8f, 10f);
            Vfx.I?.Shockwave(source.contact, 0.6f, new Color(1f, 0.85f, 0.6f));
            GameFeel.Shake(0.25f);
            KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 0.9f);
            _breakPending = true;
            _breakBy = source.attacker;
            _breakMove = source.moveId;
            _breakWindow = source.window;
            _breakContact = source.contact;
        }

        /// <summary>Publishes a pending <see cref="CombatEventType.GuardBroken"/> (Posture = the bar that filled).</summary>
        public void FlushGuardBreak()
        {
            if (!_breakPending) return;
            _breakPending = false;
            ctx?.Events.Publish(CombatEvent.GuardBroken(ctx.Clock.Frame, _breakBy, this, _breakMove, _breakWindow, PostureMax, _breakContact));
            _breakBy = null;
        }

        /// <summary>
        /// Defence step 7: a hit on a guard-broken fighter becomes a critical (×<see cref="DamageCalc.CritMul"/>, rounded
        /// once, at least 1) if the attacker's roll didn't already make it one. The break is spent.
        /// </summary>
        void ApplyGuardBreakCrit(ref HitInfo info)
        {
            if (!GuardBroken) return;
            _guardBreakLeft = 0;
            if (info.crit) return;
            info.crit = true;
            info.dmg = Mathf.Max(1, Mathf.RoundToInt(info.dmg * DamageCalc.CritMul));
        }

        /// <summary>Step 7's posture: a clean hit adds a little, never past full, never a break.</summary>
        void CleanHitPosture(in HitInfo info)
        {
            float mul = info.w != null ? info.w.postureMul : 1f;
            AddPosture(info.dmg * Tuning.Get<GuardTuning>().hitPostureFactor * mul, false, info);
        }

        /// <summary>One frame of posture: the break counts down, and after the delay the bar drains.</summary>
        void PostureTick()
        {
            if (_breakPending) FlushGuardBreak();
            if (_guardBreakLeft > 0) _guardBreakLeft--;
            if (_postureIdle < 100000) _postureIdle++;
            if (Posture <= 0f) return;
            var t = Tuning.Get<GuardTuning>();
            if (_postureIdle <= t.drainDelayFrames) return;
            if (_guardHeld && !t.drainWhileGuarding) return;
            Posture = Mathf.Max(0f, Posture - t.drainPerFrame);
        }

        void ResetPosture()
        {
            Posture = 0f;
            _postureIdle = 0;
            _guardPose = _guardLast = _guardHeld = false;
            _sinceGuardPress = 9999;
            _guardBreakLeft = 0;
            _breakPending = false;
            _breakBy = null;
        }
    }
}
