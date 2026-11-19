using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The perfect dodge (SAN-92, ADR-0004 defence step 2). A hit that reaches the player's hurtboxes while he is in a dash
    /// (ground or air, any direction) or a dodge move (roll, backflip) that started at most <see cref="PerfectDodgeWindow"/>
    /// of his frames before is perfect-dodged instead of going through the rest of the defence order:
    /// <list type="bullet">
    /// <item>"Would have landed" means the hit found a contact on his hurtboxes (the melee sweep, the projectile's cast or the
    /// hazard's box): the check runs before the hittable check, so the dash's and the roll's own invulnerability don't hide it.</item>
    /// <item>The hit does nothing; its result is <see cref="HitResult.Dodged"/>, which the attacker publishes as
    /// <see cref="CombatEventType.PerfectDodged"/> (damage 0) and logs, so that window or projectile can't touch him again.</item>
    /// <item>The world slows (<see cref="FightClock.SlowWorld"/>: <see cref="DodgeTuning.worldScale"/> for
    /// <see cref="DodgeTuning.slowFrames"/>, the player exempt), he gains <see cref="DodgeTuning.meterGain"/> × the Rage perk,
    /// and the <c>VFX_Kage_PerfectDodge</c> cue leaves his afterimages.</item>
    /// <item>One per dash or roll: once it has fired, later contacts in the same dodge go through the normal order.</item>
    /// <item>Player only: a foe's dash or roll is never a perfect dodge.</item>
    /// </list>
    /// Every number is in Resources/Tuning/dodge.json (<see cref="DodgeTuning"/>).
    /// </summary>
    public partial class Fighter
    {
        public const string PerfectDodgeCue = "VFX_Kage_PerfectDodge";

        /// <summary>Counts state entries (<see cref="SetState"/>): a dash or a dodge move is one entry, so it can perfect-dodge once.</summary>
        int _stateEntries;
        int _perfectDodgeSpentAt = -1;

        static DodgeTuning DodgeT => Tuning.Get<DodgeTuning>();

        /// <summary>The perfect-dodge window in frames: the tuning's, plus the dash perk's bonus (SAN-84's <see cref="DashBonus"/> hook).</summary>
        public int PerfectDodgeWindow
        {
            get
            {
                var t = DodgeT;
                return t.windowFrames + (DashBonus == 0f ? 0 : Mathf.RoundToInt(DashBonus * t.dashBonusWindowFrames));
            }
        }

        /// <summary>Frames since the current dash or dodge move (roll, backflip) started (0 on its first frame); -1 when in neither.</summary>
        public int DodgeAge
        {
            get
            {
                if (state == FState.Dash) return stateFrame;
                if (state == FState.Attack && move != null && moves != null && (move == moves.roll || move == moves.backflip)) return stateFrame;
                return -1;
            }
        }

        /// <summary>True when a hit reaching this fighter now would be perfect-dodged.</summary>
        public bool InPerfectDodgeWindow
        {
            get
            {
                if (!isPlayer || ctx == null || _perfectDodgeSpentAt == _stateEntries) return false;
                if (state == FState.Ultimate) return false;       // none during a rage art: it holds the world's scale itself (SAN-82)
                int age = DodgeAge;
                return age >= 0 && age <= PerfectDodgeWindow;
            }
        }

        partial void PreHitCheck(ref DefenceCheck c)
        {
            if (!InPerfectDodgeWindow) return;
            c.resolved = true;
            c.result = HitResult.Dodged;
            PerfectDodge(c.info);
        }

        void PerfectDodge(in HitInfo info)
        {
            var t = DodgeT;
            _perfectDodgeSpentAt = _stateEntries;
            LastDamageTaken = 0f;       // the event reports no damage (and no posture or crit) for this hit
            meter = Mathf.Min(100f, meter + t.meterGain * RageMul);
            ctx.Clock.SlowWorld(t.worldScale, t.slowFrames, this);
            EffectCues.Play(PerfectDodgeCue, Pos, new CueArgs { owner = this, dir = new Vector2(-facing, 0f), scale = 1f });
        }

        // ---- the cue ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterPerfectDodgeCue() => EffectCues.Register(PerfectDodgeCue, PlayPerfectDodge);

        /// <summary>Blue afterimages of the dodging fighter, trailing back along the dodge, a blue screen tint and an "EVADE" call-out.</summary>
        static void PlayPerfectDodge(Vector2 pos, in CueArgs a)
        {
            var f = a.owner;
            if (f == null || f.rig == null) return;
            var t = DodgeT;
            // each ghost is a snapshot of the rig now, spread back along the dodge so they read as a trail
            for (int i = 0; i < t.afterimages; i++)
                Vfx.I.Ghost(f.rig, t.afterimageTint, t.afterimageLife * (1f - 0.2f * i), a.dir * (t.afterimageSpacing * i));
            GameFeel.Flash(t.screenTint, t.screenTintSeconds);
            KageGame.I?.Hud?.Float(pos + Vector2.up * 2.0f, "EVADE", t.afterimageTint.WithA(1f), 1.0f);
            KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.7f);
        }
    }
}
