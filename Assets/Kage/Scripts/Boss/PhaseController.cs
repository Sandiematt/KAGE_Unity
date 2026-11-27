namespace Kage
{
    /// <summary>
    /// Runs a boss's phases (SAN-102, ADR-0008). One is attached to every fighter whose <see cref="CharacterDef.bossId"/> is set
    /// (<see cref="FightBuilder.Spawn(CharacterDef,int,IInputSource)"/>) and ticked by the fight loop after each frame's hits, projectiles and
    /// hazards have resolved. It compares the boss's health fraction with the next phase's <see cref="PhaseDef.at"/>: at or below it, the phase
    /// opens (a hit that crosses two thresholds opens both, in order). Phases only go forward; a new round (<see cref="Fighter.ResetForRound"/>)
    /// starts again from the first.
    /// <para>
    /// Entering a phase changes only what the fighter already exposes, so there is no second combat path: the enabled ability ids
    /// (<see cref="Fighter.SetEnabledAbilities"/>), the AI's profile and <c>aggressive</c> flag (<see cref="AIInputSource.SetProfile"/>), the
    /// speed, move rate and damage multipliers, the character flags (<see cref="Fighter.Flags"/>), the ability cooldown scale
    /// (<c>berserk</c>) and <see cref="Fighter.SecondWaveDelay"/>. It then publishes <see cref="CombatEventType.BossPhaseChanged"/>.
    /// A boss driven by a player input (no AI) still gets everything but the AI's two settings.
    /// </para>
    /// </summary>
    public sealed class PhaseController
    {
        /// <summary>JS: a berserk phase cools abilities down in 0.7 of the time.</summary>
        public const float BerserkCooldownMul = 0.7f;

        readonly Fighter _f;
        bool _announced;

        public BossDef Boss { get; }

        /// <summary>The current phase index (0 = the first).</summary>
        public int Index { get; private set; }

        public PhaseDef Current => Boss.phases[Index];
        public int Count => Boss.phases.Count;

        public PhaseController(Fighter fighter, BossDef boss)
        {
            _f = fighter;
            Boss = boss;
            Apply(0);
        }

        /// <summary>The AI behind the fighter's input (directly, or as a scripted input's fallback); null for a player.</summary>
        AIInputSource Ai => _f.input as AIInputSource ?? (_f.input as ScriptedInput)?.fallback as AIInputSource;

        /// <summary>
        /// Opens every phase the boss's health has fallen into. The first call after a reset publishes the first phase's
        /// event, so a listener sees "phase 0" as the fight's first boss event. A boss with no health left opens no new phase.
        /// </summary>
        public void Tick()
        {
            if (!_announced)
            {
                _announced = true;
                Publish();
            }
            TickWind();
            if (_f.health <= 0f || _f.maxHealth <= 0f) return;
            float fraction = _f.health / _f.maxHealth;
            while (Index + 1 < Count && fraction <= Boss.phases[Index + 1].at)
            {
                Apply(Index + 1);
                var enter = Current.enterMove;
                if (enter != null) _f.QueueForcedMove(enter);         // a later phase's entry replaces an earlier one still waiting
                Publish();
            }
        }

        // ---- the phase's wind (FighterRig.wind on both rigs), ramped in ----
        Wind _windFrom, _windTo;
        int _windAge, _windFrames;

        /// <summary>The wind the phase is blowing now (mid-ramp: between the last phase's and this one's), in world space.</summary>
        public Wind WindNow => _windFrames <= 0 || _windAge >= _windFrames ? _windTo : LerpWind(_windFrom, _windTo, _windAge / (float)_windFrames);

        void StartWind(PhaseDef p, bool instant)
        {
            if (p.wind == null) return;
            var w = p.wind.Value;
            float face = _f.facing >= 0f ? 1f : -1f;
            w.steady = new UnityEngine.Vector2(w.steady.x * face, w.steady.y);
            _windFrom = instant ? w : WindNow;
            _windTo = w;
            _windAge = 0;
            _windFrames = instant ? 0 : p.windRampFrames;
            SetWind(WindNow);
        }

        void TickWind()
        {
            if (_windFrames <= 0 || _windAge >= _windFrames) return;
            _windAge++;
            SetWind(WindNow);
        }

        void SetWind(Wind w)
        {
            if (_f.rig != null) _f.rig.wind = w;
            if (_f.opponent != null && _f.opponent.rig != null) _f.opponent.rig.wind = w;
        }

        /// <summary>Steady wind, gust and gust rate eased linearly; the seed is the target's (a calm end keeps the start's, so gusts don't jump).</summary>
        public static Wind LerpWind(Wind a, Wind b, float u)
        {
            u = UnityEngine.Mathf.Clamp01(u);
            float rateA = a.gustRate > 0f ? a.gustRate : 0.6f, rateB = b.gustRate > 0f ? b.gustRate : 0.6f;
            return new Wind
            {
                steady = UnityEngine.Vector2.Lerp(a.steady, b.steady, u),
                gust = UnityEngine.Mathf.Lerp(a.gust, b.gust, u),
                gustRate = UnityEngine.Mathf.Lerp(rateA, rateB, u),
                seed = b.IsCalm ? a.seed : b.seed,
            };
        }

        /// <summary>Back to the first phase (a new round: the boss has its health back).</summary>
        public void Reset()
        {
            Apply(0);
            _announced = false;
        }

        /// <summary>Applies phase <paramref name="index"/> at once with no entry move (tests, a dev jump to a phase).</summary>
        public void JumpTo(int index)
        {
            Apply(UnityEngine.Mathf.Clamp(index, 0, Count - 1));
            Publish();
        }

        void Publish()
        {
            var ctx = _f.ctx;
            ctx?.Events.Publish(CombatEvent.BossPhaseChanged(ctx.Clock.Frame, _f, Index, Current.name));
        }

        void Apply(int index)
        {
            Index = index;
            var p = Boss.phases[index];
            var c = _f.Character;
            _f.SetEnabledAbilities(p.abilities);
            _f.SpeedMul = c.speedMul * p.speedMul;
            _f.MoveRate = c.moveRate * p.moveRateMul;
            _f.DamageMul = c.damageMul * p.damageMul;
            _f.Flags = (c.flags | p.addFlags) & ~p.removeFlags;
            _f.AbilityCooldownMul = (p.berserk ? BerserkCooldownMul : 1f) * p.cooldownMul;
            _f.SecondWaveDelay = p.secondWave;
            var ai = Ai;
            if (ai != null)
            {
                if (p.aiProfile != null) ai.SetProfile(AiProfiles.Get(p.aiProfile));
                ai.aggressive = p.aggressive;
            }
            StartWind(p, index == 0);
            p.onEnter?.Invoke(_f, index);
        }
    }
}
