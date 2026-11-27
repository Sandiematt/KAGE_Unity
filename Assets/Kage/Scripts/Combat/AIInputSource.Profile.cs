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

        /// <summary>
        /// True for an AI with a profile (SAN-95). Only these throw the reference kata (<see cref="MoveDef.profiledOnly"/>: Crown Wheel, Poised
        /// Fang); the legacy AI keeps to the moves and chains it always had, so its fights replay as before.
        /// </summary>
        public bool IsProfiled => Profiled;

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
        float LevelScale() { var t = T; return t.levelBase + t.levelSlope * Level; }
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
            if (_decidedFor == opp.move || self.IsAttacking || !self.Grounded) return;
            if (_guardFrames > 0) { StanceParry(self, opp, dist); return; }     // a guard already up: the stance may turn the blow aside

            var m = opp.move;
            if (!NextBlow(opp, dist, out int firstHit, out var lvl)) return;     // no blow coming yet: look again next frame
            _decidedFor = m;

            var t = T;
            float parry = lvl == HitLevel.Unblockable ? 0f : ParryChance(self, m, false), guard = Rate(profile.guardRate), dodge = profile.dodgeRate;
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
                _stance = false; _stanceRepress = 0;
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
                Backflip(cornered);                                     // the back step (a flat invulnerable slide back); cornered, the slide forward out of the corner
            }
            else if (dist < t.closeReach + 0.1f && R() < Aggression(self) * t.tradeChance * attackRate)
            {
                _plan.Clear(); _curLeft = 0;
                Enqueue(2, 0, 0, Btn.P);                                // trade with a quick punch
                _comboLeft = 0;
            }
        }

        /// <summary>The opponent's next blow in <paramref name="opp"/>'s current move: its first frame and level. False when none is coming or it is out of reach.</summary>
        static bool NextBlow(Fighter opp, float dist, out int firstHit, out HitLevel lvl)
        {
            var m = opp.move;
            firstHit = 999; lvl = HitLevel.Mid;
            foreach (var w in m.hits) if (w.from >= opp.moveFrame && w.from < firstHit) { firstHit = w.from; lvl = w.level; }
            float reach = m.swordFront ? 1.6f : 1.15f;
            if (m.projectileFrame > 0) reach = 9f;
            return firstHit < 999 && dist < reach;
        }

        // ---------------------------------------------------------------- reading the opponent, and the two-way parry (docs/combat-flow.md)

        bool _stance;                          // the guard up now is held for its own sake (a defensive stance), not an answer to a blow
        int _stanceRepress;                    // frames until the stance drops its guard for one frame and raises it again: the parry press
        readonly List<(string id, int frame)> _seen = new();      // the opponent's recent attack starts, oldest first
        MoveDef _seenMove;
        int _seenMoveFrame = -1, _polls, _parriesSeen;

        /// <summary>Every poll: notes a new attack start by the opponent (a move with blows) for the read.</summary>
        void WatchOpponent(Fighter opp)
        {
            _polls++;
            if (!opp.IsAttacking || opp.move == null) { _seenMove = null; _seenMoveFrame = -1; return; }
            bool fresh = opp.move != _seenMove || opp.moveFrame < _seenMoveFrame;
            _seenMove = opp.move;
            _seenMoveFrame = opp.moveFrame;
            if (!fresh || opp.move.hits.Count == 0) return;
            _seen.Add((opp.move.id, _polls));
            while (_seen.Count > Mathf.Max(1, T.readHistory)) _seen.RemoveAt(0);
        }

        /// <summary>How many earlier starts of <paramref name="m"/> this foe remembers (inside the read window), not counting the one running now.</summary>
        public int Repeats(MoveDef m)
        {
            if (m == null) return 0;
            int n = 0, window = T.readWindowFrames;
            foreach (var s in _seen) if (s.id == m.id && _polls - s.frame <= window) n++;
            return Mathf.Max(0, n - 1);
        }

        /// <summary>
        /// The chance this foe tries to parry <paramref name="m"/>: its parry rate on the stage's curve, plus its read of a repeated attack
        /// (<see cref="AiTuning.readPerRepeat"/> per earlier start in the read window, up to <see cref="AiTuning.readMax"/>) and, into a held guard, the
        /// stance's <see cref="AiTuning.stanceParry"/>, both times its tier's skill (<see cref="AiTuning.ParrySkillFor"/>); the read and the stance
        /// never lift it above <see cref="AiTuning.parryCap"/>. With no profile it is the plain parry rate.
        /// </summary>
        public float ParryChance(Fighter self, MoveDef m, bool stance)
        {
            if (!Profiled) return parryRate;
            var t = T;
            float baseRate = Rate(parryRate);
            float read = Mathf.Min(t.readMax, t.readPerRepeat * Repeats(m));
            float bonus = (read + (stance ? t.stanceParry : 0f)) * t.ParrySkillFor(self.Tier) * LevelScale();
            return Mathf.Max(baseRate, Mathf.Min(t.parryCap, baseRate + bonus));
        }

        /// <summary>
        /// The guard is already up as a stance and a blow is coming into it: once per attack, roll the stance parry. On a hit the guard stays up
        /// and drops for one frame half a window before the blow, then goes up again: the press that opens the parry window.
        /// </summary>
        void StanceParry(Fighter self, Fighter opp, float dist)
        {
            if (!_stance || _parryTry || !NextBlow(opp, dist, out int firstHit, out var lvl)) return;
            var m = opp.move;
            _decidedFor = m;
            if (lvl == HitLevel.Unblockable || m.isThrow || R() >= ParryChance(self, m, true)) return;
            int ahead = firstHit - opp.moveFrame;
            _parryTry = true;
            _stanceRepress = Mathf.Max(1, ahead - Tuning.Get<ParryTuning>().windowFrames / 2);
            _guardFrames = Mathf.Max(_guardFrames, ahead + 10);
            _guardLow = lvl == HitLevel.Low;
        }

        /// <summary>
        /// This foe just parried the opponent: answer it. The guard comes down, it holds still for its tier's delay (<see cref="ParryTuning.AnswerDelayFor"/>,
        /// time for Kage to recover and defend), then attacks; the attack's wind-up shows the tell (Fighter.Parry.cs). Profiled foes only. True when it queued one.
        /// </summary>
        bool AnswerParry(Fighter self, float dist)
        {
            if (!Profiled) return false;
            if (self.ParriesMade < _parriesSeen) _parriesSeen = 0;          // a new round
            if (self.ParriesMade == _parriesSeen) return false;
            _parriesSeen = self.ParriesMade;
            if (R() >= T.answerChance) return false;
            _plan.Clear(); _curLeft = 0; _think = 0;
            _guardFrames = 0; _parryTry = false; ParryAttempt = false; _parryWait = 0; _stance = false; _stanceRepress = 0;
            Enqueue(Mathf.Max(1, Tuning.Get<ParryTuning>().AnswerDelayFor(self.Tier)), 0, 0, Btn.None);
            if (!QuickAttack(dist)) Attack(self, dist);
            return true;
        }

        /// <summary>The distance at which a run-in (<see cref="runInBeyond"/>) stops: the profile's fighting range, a little beyond melee reach.</summary>
        float PursueStop() => Mathf.Max(T.meleeReach + 0.3f, Profiled ? profile.preferredRange + 0.15f : 0f);

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
                if (runInBeyond > 0f && dist > runInBeyond)
                {
                    // a sub-boss runs the gap down: forward held past the run threshold, cut short by PursueStop in Poll
                    Enqueue(Mathf.Clamp(Mathf.RoundToInt(dist * 12f) + 30, 36, 96), 1, 0, Btn.None, pursue: true);
                    return;
                }
                if (dist > pref + 0.05f) Enqueue(8 + N(8), 1, 0, Btn.None);
                else Enqueue(10 + N(10), 0, 0, Btn.None);
                return;
            }

            // in range
            if (R() < aggr) { Attack(self, dist); return; }
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
                _guardFrames = t.holdGuardMin + N(t.holdGuardMax - t.holdGuardMin + 1);       // a guard held for its own sake: the defensive stance
                _guardLow = false; _parryTry = false; _parryWait = 0;
                _stance = true; _stanceRepress = 0;
            }
            else if (dist > pref + 0.12f) Enqueue(6 + N(6), 1, 0, Btn.None);
            else Enqueue(8 + N(10), 0, 0, Btn.None);
        }

        /// <summary>The roll for chaining the next attack of a string after a hit: the legacy odds, or (profiled) the string's remaining attacks and <see cref="AiTuning.comboFollow"/>.</summary>
        bool ChainRoll()
        {
            if (!Profiled) return R() < Mathf.Lerp(0.25f, 0.85f, Level) * attackRate;
            if (_comboLeft <= 0 || R() >= T.comboFollow * attackRate) return false;
            _comboLeft--;
            return true;
        }

        /// <summary>The chance per attack that a punish after a guarded blow is thrown: the legacy <c>level x attackRate</c>, or the profile's punish rate.</summary>
        float PunishChance() => Profiled ? Rate(profile.punishRate) * attackRate : Level * attackRate;

        /// <summary>The fighter's weapon has a reference kata on Back + P (the katana's Poised Fang, the tachi's Thunder Poise).</summary>
        static bool OffersKata(Fighter self) => self != null && self.moves != null && self.moves.Select(Btn.P, -1, 0, false, self) is { profiledOnly: true };

        void Combo() => _comboLeft = Mathf.Max(0, N(profile.comboMax - profile.comboMin + 1) + profile.comboMin - 1);

        /// <summary>One string's first attack, by distance (the legacy bands with the throw and the retreats left to the other branches); the rest of the string chains on hit.</summary>
        void Attack(Fighter self, float dist)
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
                else if (r < 0.95f || !OffersKata(self)) Enqueue(2, 0, -1, Btn.S);
                else Enqueue(2, -1, 0, Btn.P);                          // Back + P: the Poised Fang's held overhead (a katana or a tachi)
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
        /// <summary>
        /// The reaction abilities (<see cref="AbilityDef.reactive"/>), asked every frame the fighter is free: the first one in slot order that is ready, in
        /// range, whose situation holds and whose chance rolls. Returns its slot, 0 for none. A failed roll costs the ability a short wait
        /// (<see cref="ReactiveRetryFrames"/>), so a reaction is not rolled again sixty times a second.
        /// </summary>
        int ReactiveAbility(Fighter self, float dist)
        {
            var ids = self.AbilityIds;
            for (int i = 0; i < ids.Count; i++)
            {
                var a = AbilityCatalog.Get(ids[i]);
                if (a == null || !a.reactive || !self.AbilityReady(i + 1) || dist < a.range.x || dist > a.range.y) continue;
                if (a.when != null && !a.when(self, self.opponent)) continue;
                if (_reactiveWait.TryGetValue(a.id, out var until) && self.ctx != null && self.ctx.Clock.Frame < until) continue;
                if (R() < a.chance * (aggressive ? T.aggressiveAbilityMul : 1f)) return i + 1;
                if (self.ctx != null) _reactiveWait[a.id] = self.ctx.Clock.Frame + ReactiveRetryFrames;
            }
            return 0;
        }

        /// <summary>Frames a reaction ability waits after its chance roll failed before it is rolled again.</summary>
        public const int ReactiveRetryFrames = 20;
        readonly System.Collections.Generic.Dictionary<string, int> _reactiveWait = new();

        bool TryAbility(Fighter self, float dist)
        {
            var ids = self.AbilityIds;
            if (ids.Count == 0 || self.Busy) return false;
            _abilityPick.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                var a = AbilityCatalog.Get(ids[i]);
                if (a == null || a.reactive || a.escapeAfterHits > 0 || !self.AbilityReady(i + 1) || dist < a.range.x || dist > a.range.y) continue;
                if (a.when != null && !a.when(self, self.opponent)) continue;
                _abilityPick.Add(i + 1);
            }
            if (_abilityPick.Count == 0) return false;
            int slot = PickWeighted(ids);
            var d = AbilityCatalog.Get(ids[slot - 1]);
            if (R() >= d.chance * (aggressive ? T.aggressiveAbilityMul : 1f)) return false;
            _plan.Clear(); _curLeft = 0;
            Enqueue(1, 0, 0, Btn.None, ability: slot);
            _comboLeft = 0;
            return true;
        }

        /// <summary>
        /// One of <see cref="_abilityPick"/>'s slots: uniformly (one draw, as before) when every candidate weighs 1, else by <see cref="AbilityDef.weight"/>.
        /// </summary>
        int PickWeighted(IReadOnlyList<string> ids)
        {
            float total = 0f;
            bool weighted = false;
            foreach (var s in _abilityPick)
            {
                float w = Mathf.Max(0f, AbilityCatalog.Get(ids[s - 1]).weight);
                total += w;
                if (!Mathf.Approximately(w, 1f)) weighted = true;
            }
            if (!weighted || total <= 0f) return _abilityPick[N(_abilityPick.Count)];
            float r = R() * total;
            foreach (var s in _abilityPick)
            {
                r -= Mathf.Max(0f, AbilityCatalog.Get(ids[s - 1]).weight);
                if (r < 0f) return s;
            }
            return _abilityPick[_abilityPick.Count - 1];
        }

        // ---------------------------------------------------------------- boss strings

        MoveDef _chainSeen;          // the move last seen running (to notice a new one)
        MoveDef _chainLast;          // the last move that ran (what ended, for AbilityDef.chainIf)
        AbilityDef _chainFrom;       // the ability whose move (or its links) is running or just ended
        int _chainSlot, _chainWait;  // the chained slot waiting out its beat

        /// <summary>
        /// A boss string (<see cref="AbilityDef.chain"/>): while an ability's move runs it is remembered; on the first free frame after it, the AI rolls the
        /// ability's <see cref="AbilityDef.chainChance"/> and, on a hit, holds still for the string's beat and presses the first of its follow-ups that is
        /// enabled, ready, in its band and whose situation holds. True when it used the frame (holding the beat or pressing).
        /// </summary>
        bool ChainStep(Fighter self, Fighter opp, float dist, ref FrameInput o)
        {
            if (self.IsAttacking)
            {
                if (self.move != _chainSeen)
                {
                    _chainSeen = self.move;
                    _chainLast = self.move;
                    var a = AbilityOf(self, self.move);
                    if (a != null) _chainFrom = a;          // a link of the same ability (a then follow-up) keeps the one it came from
                    _chainSlot = 0;
                }
                return false;
            }
            _chainSeen = null;
            if (self.Busy) { _chainFrom = null; _chainSlot = 0; return false; }
            if (_chainSlot > 0)
            {
                if (--_chainWait > 0) return true;
                int slot = _chainSlot;
                _chainSlot = 0;
                if (!self.AbilityReady(slot) || opp.state is FState.Down or FState.KO) return false;
                _plan.Clear(); _curLeft = 0; _comboLeft = 0; _guardFrames = 0;
                o.ability = slot;
                return true;
            }
            if (_chainFrom == null) return false;
            var from = _chainFrom;
            _chainFrom = null;
            if (from.chain == null || from.chain.Length == 0 || opp.state is FState.Down or FState.KO or FState.Thrown) return false;
            if (from.chainIf != null && !from.chainIf(_chainLast)) return false;
            if (R() >= (from.chainChance != null ? from.chainChance(self) : 0.6f)) return false;
            var ids = self.AbilityIds;
            foreach (var id in from.chain)
            {
                int s = IndexOf(ids, id) + 1;
                var a = AbilityCatalog.Get(id);
                if (s <= 0 || a == null || !self.AbilityReady(s)) continue;
                if (dist < a.range.x - ChainRangeSlack || dist > a.range.y + ChainRangeSlack) continue;
                if (a.when != null && !a.when(self, opp)) continue;
                _chainSlot = s;
                _chainWait = Mathf.Max(1, from.chainBeat != null ? from.chainBeat(self) : 8);
                _plan.Clear(); _curLeft = 0;
                return true;
            }
            return false;
        }

        /// <summary>How far outside a follow-up's distance band a string still takes it, metres.</summary>
        public const float ChainRangeSlack = 0.6f;

        /// <summary>The ability whose move <paramref name="m"/> is (its own, or the phase variant it starts), or null.</summary>
        static AbilityDef AbilityOf(Fighter self, MoveDef m)
        {
            if (m == null) return null;
            foreach (var id in self.AbilityIds)
            {
                var am = self.AbilityMove(id);
                if (am == null) continue;
                if (am == m || (am.variantFor != null && am.variantFor(self) == m)) return AbilityCatalog.Get(id);
            }
            return null;
        }

        static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return i;
            return -1;
        }

        /// <summary>Drops a string in progress (the fighter was hit, or a round began).</summary>
        void DropChain() { _chainFrom = null; _chainSeen = null; _chainSlot = 0; }
    }
}
