using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The bosses read the rage art: a sub-boss (Kuroe, Garan, Hisame, Shiden) or the boss (Raikon) turns Kage's rage art aside unless it is
    /// <b>open</b> the moment he starts it, so the art has to be timed like a punish.
    /// <list type="bullet">
    /// <item><b>Open</b> (<see cref="OpenToRageArt"/>): reeling (a blow's hit-stun, a parry's stagger, a broken guard), thrown up by a blow, on the
    /// floor, or committed to an attack, from its wind-up to the end of its recovery. The art plays as it always has.</item>
    /// <item><b>Not open</b>: in neutral (standing, walking, running, crouched, jumping, landing, dashing), guarding or in blockstun, in a move
    /// that isn't an attack (a roll, a backflip, getting up, Shiden's brace), on a move's invulnerable frames, hidden, or in a counter stance's
    /// window that catches Kage from where he stands (Censure, Hold the Line, Redline Counter). The art is <b>turned aside</b>.</item>
    /// </list>
    /// Turned aside, the Rage is spent and the art never starts: no freeze, no cut-in. Kage lunges in to where the art would have struck from and
    /// the boss turns it away. A counter stance answers with its own move at once; any other boss parries it, and its AI answers that the way it
    /// answers a parry (its tier's beat, then an attack with the tell). Kage reels for <see cref="RageArtCounterTuning.staggerFrames"/> with no
    /// early way out and loses a share of his posture. The event is <c>Countered</c> (Source = Kage, Target = the boss, MoveId = the art), which
    /// puts the RAGE COUNTERED call-out over Kage (<see cref="CombatCalloutKind.RageCountered"/>: PARRIED's red brush, held longer). Every other foe takes the art wherever it stands. Numbers: Resources/Tuning/rageArtCounter.json.
    /// </summary>
    public partial class Fighter
    {
        static RageArtCounterTuning RageCounterT => Tuning.Get<RageArtCounterTuning>();

        /// <summary>True when <paramref name="id"/> names a rage art (Thousand Shadows or a cinematic one): the HUD's RAGE COUNTERED reads it off the Countered event.</summary>
        public static bool IsRageArtId(string id) => !string.IsNullOrEmpty(id) && (id == ThousandShadows || id == Mikazuki || IsCinematicArt(id));

        /// <summary>This fighter reads rage arts: a sub-boss or the boss (<see cref="Tier"/>), while the rule is on.</summary>
        public bool CountersRageArts => RageCounterT.enabled && Tier is CombatTier.Sub or CombatTier.Boss;

        /// <summary>
        /// True when a rage art <paramref name="by"/> started now would land on this fighter, read or not: it is reeling, in the air from a blow,
        /// on the floor, or committed to an attack outside its invulnerable frames and outside a counter stance's catch.
        /// </summary>
        public bool OpenToRageArt(Fighter by)
        {
            if (_hidden) return false;
            return state switch
            {
                FState.Hitstun or FState.Launched or FState.Down or FState.Thrown or FState.KO => true,
                FState.Attack => InAttackMove && !MoveInvulnerable() && !RageArtCaught(by),
                _ => false,
            };
        }

        /// <summary>True when this fighter would turn aside the rage art <paramref name="by"/> started now (it reads rage arts and isn't open).</summary>
        public bool WouldCounterRageArt(Fighter by) =>
            by != null && !ReferenceEquals(by, this) && CountersRageArts && health > 0f && !OpenToRageArt(by);

        /// <summary>
        /// A counter stance's window is up and catches <paramref name="by"/>: from the front when it only catches the front. A stance that catches
        /// only light blows (Iron Guard) lets the art through: it is no light blow.
        /// </summary>
        bool RageArtCaught(Fighter by) =>
            state == FState.Attack && move != null && move.counterFrom >= 0 && moveFrame >= move.counterFrom && moveFrame <= move.counterTo
            && !move.counterLightOnly && (!move.counterFrontOnly || (by != null && Mathf.Sign(by.Pos.x - Pos.x) == facing));

        /// <summary>
        /// Kage's rage art read by <paramref name="boss"/>: the Rage is spent, Kage lunges in and the boss turns the art aside (its stance's answer,
        /// or a parry its AI answers), Kage reels, and <c>Countered</c> is published.
        /// </summary>
        void RageArtTurnedAside(Fighter boss, string art)
        {
            var t = RageCounterT;
            meter = 0f;
            var answer = boss.RageArtCaught(this) ? boss.move.counterAnswer : null;

            // the lunge: to where the art would have struck from, on his own side of the boss (never past it)
            float side = Mathf.Sign(Pos.x - boss.Pos.x);
            if (side == 0f) side = -boss.facing;
            float stop = t.lungeStop * Mathf.Max(1f, boss.Character != null ? boss.Character.bodyScale : 1f);    // a bigger body, a longer blade's length off
            if (Mathf.Abs(Pos.x - boss.Pos.x) > stop)
            {
                if (PresentsCombat) Vfx.I?.Ghost(rig, fxMain, 0.3f);
                float x = Mathf.Clamp(boss.Pos.x + side * stop, StageMin, StageMax);
                rb.position = new Vector2(x, rb.position.y);
                transform.position = new Vector3(x, transform.position.y, transform.position.z);
            }
            rb.linearVelocity = Vector2.zero;
            SetFacing(-side, true);
            Vector2 contact = new((Pos.x + boss.Pos.x) * 0.5f, Pos.y + 1.1f);

            if (answer != null) boss.AnswerRageArt(this, answer, contact);
            else boss.ParryRageArt(this);
            ReelFromTurnedAsideArt(boss, art, contact);

            ctx?.Events.Publish(CombatEvent.HitResolved(ctx.Clock.Frame, CombatEventType.Countered, this, boss, null, art, -1, 0f, false,
                HitLevel.Unblockable, HitZone.Body, contact, Vector2.zero));
            FlushGuardBreak();                                   // a guard the turned-aside art broke publishes after the Countered event

            GameFeel.Hitstop(t.hitstopFrames);
            EffectCues.Play(FoeParryCue, contact, new CueArgs { owner = boss, dir = new Vector2(side, 0.25f).normalized, scale = 1.3f });
            if (PresentsCombat)
            {
                GameFeel.Shake(t.shake);
                GameFeel.Flash(t.flashTint, t.flashSeconds);
                GameFeel.Punch(0.1f);
            }
        }

        /// <summary>A boss's counter stance caught the art: it faces Kage and its answer plays at once, where it stands.</summary>
        void AnswerRageArt(Fighter kage, MoveDef answer, Vector2 contact)
        {
            float dx = kage.Pos.x - Pos.x;
            if (dx != 0f) SetFacing(Mathf.Sign(dx), true);
            if (PresentsCombat)
            {
                Vfx.I?.Glint(contact, fxCore);
                rig.Flash(fxMain, 0.4f, 10f);
            }
            StartMove(answer);
            CombatAudio?.Play(SfxKind.Counter, 0.9f);
        }

        /// <summary>
        /// A boss turned the art aside like a parry: out of neutral or a guard it faces Kage and keeps him as its target; in a move of its own
        /// (a roll, a brace, getting up) it carries on. Its AI answers it as it answers a parry, with the tell on the answer.
        /// </summary>
        void ParryRageArt(Fighter kage)
        {
            if (state != FState.Attack)
            {
                if (state == FState.Blockstun) SetState(FState.Idle);
                TurnAfterParry(kage);
            }
            ParriesMade++;
            _answerTellLeft = ParryT.answerTellWindow;
            if (PresentsCombat) rig.Flash(ParryT.foeParryTint, 0.45f, 10f);
        }

        /// <summary>Kage's art was turned aside by <paramref name="by"/>: he reels with no early way out, is pushed back and loses posture.</summary>
        void ReelFromTurnedAsideArt(Fighter by, string art, Vector2 contact)
        {
            var t = RageCounterT;
            if (_victim != null) ReleaseVictim(false);
            SetState(FState.Hitstun);                            // clears a stagger's early way out (_recoverAt): this reel runs its course
            _hurt = _hurtBody;
            int stun = ScaleStun(Mathf.Max(1, t.staggerFrames), ParryT.stunResistFactor);
            _stun = stun;
            float away = Mathf.Sign(Pos.x - by.Pos.x);
            if (away == 0f) away = -facing;
            Knock(new Vector2(KnockVelocity(new Vector2(away * t.pushback, 0f)).x, rb.linearVelocity.y));
            rig.Squash(0.06f);
            rig.Flash(Color.white, 0.5f, 10f);
            AddPosture(PostureMax * t.postureShare, true, new HitInfo { attacker = by, moveId = art, window = -1, contact = contact });
            if (_stun < stun) _stun = stun;                      // a guard it broke leaves him helpless at least as long as the reel
        }
    }
}
