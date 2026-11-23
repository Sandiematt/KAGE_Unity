using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The profile-driven half of the AI (SAN-95, ADR-0005). A <see cref="profile"/> (one per JS archetype, from the generated table)
    /// changes how the same utility AI behaves: where it stands (<see cref="AiProfile.preferredRange"/>), how often it attacks and
    /// how long its strings run (aggression, combo length), how it answers an attack it sees (parry, guard, dodge, after its reaction
    /// delay), what it tries from range (dash-in, jump-in, the ranged Special), when it counters an opening, and which ability slot it
    /// presses. Everything it does is a button press through <see cref="FrameInput"/>, the same interface as the player; every roll
    /// is on the fight's "ai/&lt;FighterId&gt;" stream (ADR-0002). With no profile, or the legacy one, none of this runs and the AI is
    /// the pre-SAN-95 one, draw for draw.
    /// </summary>
    public partial class AIInputSource
    {
        /// <summary>The AI's profile (<see cref="CharacterDef.aiProfile"/>); null or <see cref="AiProfile.legacy"/> = the pre-SAN-95 behaviour.</summary>
        public AiProfile profile;
        /// <summary>A boss phase's <c>aggressive</c> flag (ADR-0008): aggression up by <see cref="AiTuning.aggressiveBonus"/>, ability chances times <see cref="AiTuning.aggressiveAbilityMul"/>.</summary>
        public bool aggressive;

        bool Profiled => profile != null && !profile.legacy;

        /// <summary>Swaps the profile (a boss phase, ADR-0008). The parry rate follows the new profile.</summary>
        public void SetProfile(AiProfile p)
        {
            profile = p;
            parryRate = p != null ? p.parryRate : 0f;
        }

        int _comboLeft;                       // attacks still to chain on hit in this string
        MoveDef _decidedFor, _punished;       // the opponent's attack already answered / already punished
        int _lastOppFrame = -1;
        readonly List<int> _abilityPick = new();

        AiTuning T => Tuning.Get<AiTuning>();

        /// <summary>The stage level's scale on aggression, guard, parry and punish (1 at the default level).</summary>
        float LevelScale() { var t = T; return t.levelBase + t.levelSlope * level; }
        float Rate(float x) => Mathf.Min(T.rateCap, x * LevelScale());

        float ThinkMul => Profiled && profile.heavy ? T.heavyThinkMul : 1f;

        /// <summary>The attack chance per decision: the profile's aggression on the stage's curve, bolder or warier when hurt, bolder in an aggressive phase.</summary>
        float Aggression(Fighter self)
        {
            var t = T;
            float a = Rate(profile.aggression);
            if (self.maxHealth > 0f && self.health / self.maxHealth < t.lowHealthFraction)
                a += profile.cautious ? -t.lowHealthBoldness : t.lowHealthRashness;
            if (aggressive) a += t.aggressiveBonus;
            return Mathf.Clamp01(a);
        }

        /// <summary>The frames it takes this AI to notice an attack and answer it: the profile's reaction range, scaled to Unity's moves and the stage's level.</summary>
        int ReactFrames()
        {
            float k = T.reactionScale / LevelScale();
            float lo = profile.reactMinFrames * k, hi = profile.reactMaxFrames * k;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(lo, hi, R())));
        }

        // ---------------------------------------------------------------- answering an attack

        /// <summary>
        /// Once per opponent attack, when it is in reach of a blow: roll parry, guard, dodge in that order on the profile's rates (the
        /// JS AIController.respond). The answer starts after the reaction delay counted from the start of the attack, so a quick attack
        /// can land before the guard is up. A parry attempt also holds its press back until the blow is half a window away (SAN-91).
        /// </summary>
        void ReactProfiled(Fighter self, Fighter opp, float dist)
        {
            if (!opp.IsAttacking || opp.move == null) { _decidedFor = null; _lastOppFrame = -1; return; }
            if (opp.moveFrame < _lastOppFrame) _decidedFor = null;      // a new attack began
            _lastOppFrame = opp.moveFrame;
            if (_decidedFor == opp.move || _guardFrames > 0 || self.IsAttacking || !self.Grounded) return;

            var m = opp.move;
            int firstHit = 999; HitLevel lvl = HitLevel.Mid;
            foreach (var w in m.hits) if (w.from >= opp.moveFrame && w.from < firstHit) { firstHit = w.from; lvl = w.level; }
            float reach = m.swordFront ? 1.6f : 1.15f;
            if (m.projectileFrame > 0) reach = 9f;
            if (firstHit >= 999 || dist >= reach) return;               // no blow coming yet: look again next frame
            _decidedFor = m;

            var t = T;
            float parry = Rate(parryRate), guard = Rate(profile.guardRate), dodge = profile.dodgeRate;
            if (lvl == HitLevel.Unblockable || m.isThrow) { parry = guard = 0f; dodge += t.unblockableDodgeBonus; }
            int ahead = firstHit - opp.moveFrame;
            int wait = Mathf.Max(0, ReactFrames() - opp.moveFrame);
            float r = R();
            if (r < parry + guard)
            {
                bool tryParry = r < parry;
                _parryTry = tryParry;
                _parryWait = tryParry ? Mathf.Max(wait, ahead - Tuning.Get<ParryTuning>().windowFrames / 2) : wait;
                _guardFrames = Mathf.Max(ahead, _parryWait) + 10;
                _guardLow = lvl == HitLevel.Low;
                _plan.Clear(); _curLeft = 0;
            }
            else if (r < parry + guard + dodge)
            {
                _plan.Clear(); _curLeft = 0;
                bool cornered = Cornered(self, opp);
                if (cornered && lvl != HitLevel.Unblockable && !m.isThrow)
                {
                    // the wall is behind: no backflip into it (SAN-130); guard the blow instead (no parry), after the same reaction delay
                    _parryTry = false;
                    _parryWait = wait;
                    _guardFrames = Mathf.Max(ahead, wait) + 10;
                    _guardLow = lvl == HitLevel.Low;
                    return;
                }
                if (wait > 0) Enqueue(wait, 0, 0, Btn.None);
                Backflip(cornered);                                     // the backflip, out of the way of anything on the ground; cornered, a roll forward out of the corner
            }
            else if (dist < t.closeReach + 0.1f && R() < Aggression(self) * t.tradeChance * attackRate)
            {
                _plan.Clear(); _curLeft = 0;
                Enqueue(2, 0, 0, Btn.P);                                // trade with a quick punch
                _comboLeft = 0;
            }
        }

        // ---------------------------------------------------------------- openings

        /// <summary>
        /// The opponent's attack has finished its last hit window and it is in recovery (JS punish openings): counter it with a quick
        /// attack at the profile's punish rate, once per opening. True when it queued one.
        /// </summary>
        bool PunishOpening(Fighter self, Fighter opp, float dist)
        {
            if (!opp.IsAttacking || opp.move == null) { _punished = null; return false; }
            var m = opp.move;
            if (_punished == m || m.hits.Count == 0) return false;
            foreach (var w in m.hits) if (w.to >= opp.moveFrame) return false;       // still a blow to come
            if (opp.moveFrame >= m.total - 3 || dist > T.meleeReach) return false;
            _punished = m;
            if (R() >= Rate(profile.punishRate) * attackRate) return false;
            return QuickAttack(dist);
        }

        /// <summary>A punch up close, the weapon from the middle; false when out of reach.</summary>
        bool QuickAttack(float dist)
        {
            if (dist < 0.85f) Enqueue(2, 0, 0, Btn.P);
            else if (dist < 1.3f) Enqueue(2, 0, 0, Btn.S);
            else return false;
            _comboLeft = 1;
            return true;
        }

        // ---------------------------------------------------------------- deciding

        void DecideProfiled(Fighter self, Fighter opp, float dist)
        {
            var t = T; var p = profile;
            bool oppDown = opp.state == FState.Down;
            bool wallBehind = WallBehind(self, opp);        // = Cornered: a backflip or step back goes into the wall
            float aggr = Aggression(self);
            float pref = p.preferredRange;

            if (oppDown)
            {
                if (dist < 1.0f && R() < 0.5f) { Enqueue(2, 0, -1, Btn.P); _comboLeft = 0; return; }
                if (dist > 1.3f) Enqueue(18 + N(12), 1, 0, Btn.None); else StepBack(18 + N(12), wallBehind);
                return;
            }
            if (TryAbility(self, dist)) return;

            if (p.ranged)
            {
                if (dist < pref - t.rangedBackOff && !wallBehind && R() < 0.6f)
                {
                    if (R() < p.dodgeRate) Backflip(false);         // only ever with no wall behind (the branch's own test)
                    else Enqueue(14 + N(10), -1, 0, Btn.None);
                    return;
                }
                if (dist > t.shotRange && R() < p.kunaiRate && ShotReady(self)) { Enqueue(2, 0, 0, Btn.X); return; }
            }

            if (dist > t.meleeReach + t.attackRangeMargin)
            {
                // out of range: the profile's ways of closing, else walk in to the preferred range and hold there
                if (p.dashInRate > 0f && dist < t.dashInRange && R() < p.dashInRate)
                {
                    Enqueue(1, 1, 0, Btn.None, dash: true);
                    Enqueue(6, 1, 0, R() < 0.5f ? Btn.P : Btn.K);
                    Combo(); return;
                }
                if (p.jumpRate > 0f && dist < t.jumpInRange && R() < p.jumpRate)
                {
                    Enqueue(1, 1, 1, Btn.None); Enqueue(12, 1, 0, Btn.None); Enqueue(2, 0, 0, R() < 0.5f ? Btn.K : Btn.S);
                    Combo(); return;
                }
                if (dist > t.specialFar && p.kunaiRate > 0f && R() < p.kunaiRate * 0.6f && ShotReady(self)) { Enqueue(2, 0, 0, Btn.X); return; }
                if (dist < t.lungeRange && R() < aggr * t.lungeShare) { Enqueue(2, 1, 0, Btn.S); Combo(); return; }      // the shadow step
                if (dist > pref + 0.05f) Enqueue(8 + N(8), 1, 0, Btn.None);
                else Enqueue(10 + N(10), 0, 0, Btn.None);
                return;
            }

            // in range
            if (R() < aggr) { Attack(dist); return; }
            if (p.grabRate > 0f && dist < t.grabRange && (opp.IsGuarding || R() < 0.3f) && R() < p.grabRate)
            {
                Enqueue(2, 1, 0, Btn.P);                                // the throw: Punch toward the foe
                _comboLeft = 0;
                return;
            }
            if (dist < pref - t.spacingBand && !wallBehind && R() < t.spacingChance)
            {
                Enqueue(8 + N(8), -1, 0, Btn.None);                     // too close for its liking: back off to its range
                return;
            }
            float r = R();
            if (r < t.strafeChance)
            {
                int dir = R() < 0.5f ? 1 : -1;
                if (dir < 0 && wallBehind) dir = 1;
                Enqueue(8 + N(8), dir, 0, Btn.None);
            }
            else if (r < t.strafeChance + t.holdGuardChance && p.guardRate > 0.3f)
            {
                _guardFrames = t.holdGuardMin + N(t.holdGuardMax - t.holdGuardMin + 1);       // a guard held for its own sake
                _guardLow = false; _parryTry = false; _parryWait = 0;
            }
            else if (dist > pref + 0.12f) Enqueue(6 + N(6), 1, 0, Btn.None);
            else Enqueue(8 + N(10), 0, 0, Btn.None);
        }

        /// <summary>The roll for chaining the next attack of a string after a hit: the legacy odds, or (profiled) the string's remaining attacks and <see cref="AiTuning.comboFollow"/>.</summary>
        bool ChainRoll()
        {
            if (!Profiled) return R() < Mathf.Lerp(0.25f, 0.85f, level) * attackRate;
            if (_comboLeft <= 0 || R() >= T.comboFollow * attackRate) return false;
            _comboLeft--;
            return true;
        }

        /// <summary>The chance per attack that a punish after a guarded blow is thrown: the legacy <c>level x attackRate</c>, or the profile's punish rate.</summary>
        float PunishChance() => Profiled ? Rate(profile.punishRate) * attackRate : level * attackRate;

        void Combo() => _comboLeft = Mathf.Max(0, N(profile.comboMax - profile.comboMin + 1) + profile.comboMin - 1);

        /// <summary>One string's first attack, by distance (the legacy bands with the throw and the retreats left to the other branches); the rest of the string chains on hit.</summary>
        void Attack(float dist)
        {
            var t = T;
            float r = R();
            if (dist < t.closeReach)
            {
                if (r < 0.38f) Enqueue(2, 0, 0, Btn.P);
                else if (r < 0.60f) Enqueue(2, 0, -1, Btn.K);
                else if (r < 0.78f) Enqueue(2, 0, 0, Btn.K);
                else Enqueue(2, 0, -1, Btn.P);
            }
            else if (dist < t.meleeReach)
            {
                if (r < 0.30f) Enqueue(2, 0, 0, Btn.S);
                else if (r < 0.56f) Enqueue(2, 0, 0, Btn.K);
                else if (r < 0.76f) Enqueue(2, 0, -1, Btn.K);
                else if (r < 0.90f) Enqueue(2, 1, 0, Btn.K);
                else Enqueue(2, 0, -1, Btn.S);
            }
            else
            {
                if (r < 0.6f) Enqueue(2, 1, 0, Btn.S);                  // the shadow step closes the last of the gap
                else Enqueue(8, 1, 0, Btn.None);
            }
            Combo();
        }

        /// <summary>The ranged Special (a projectile move on the X button) is there to throw and the Shadow it costs is in the bar.</summary>
        bool ShotReady(Fighter self) => self.meter >= T.specialMeter && self.moves.Select(Btn.X, 0, 0, false, self)?.projectileFrame >= 0;

        // ---------------------------------------------------------------- ability slots

        /// <summary>
        /// Boss and sub-boss abilities (the JS tryAbility): of the character's abilities that are enabled, off cooldown and in their distance
        /// band, pick one, then roll its chance (times <see cref="AiTuning.aggressiveAbilityMul"/> in an aggressive phase). A hit presses
        /// that slot in <see cref="FrameInput.ability"/>, like a button. An ability the catalog doesn't know is never picked.
        /// </summary>
        bool TryAbility(Fighter self, float dist)
        {
            var ids = self.AbilityIds;
            if (ids.Count == 0 || self.Busy) return false;
            _abilityPick.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                var a = AbilityCatalog.Get(ids[i]);
                if (a == null || !self.AbilityReady(i + 1) || dist < a.range.x || dist > a.range.y) continue;
                if (a.when != null && !a.when(self, self.opponent)) continue;
                _abilityPick.Add(i + 1);
            }
            if (_abilityPick.Count == 0) return false;
            int slot = _abilityPick[N(_abilityPick.Count)];
            var d = AbilityCatalog.Get(ids[slot - 1]);
            if (R() >= d.chance * (aggressive ? T.aggressiveAbilityMul : 1f)) return false;
            _plan.Clear(); _curLeft = 0;
            Enqueue(1, 0, 0, Btn.None, ability: slot);
            _comboLeft = 0;
            return true;
        }
    }
}
