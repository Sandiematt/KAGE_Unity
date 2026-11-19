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
            if (_f.health <= 0f || _f.maxHealth <= 0f) return;
            float fraction = _f.health / _f.maxHealth;
            while (Index + 1 < Count && fraction <= Boss.phases[Index + 1].at)
            {
                Apply(Index + 1);
                Publish();
            }
        }

        /// <summary>Back to the first phase (a new round: the boss has its health back).</summary>
        public void Reset()
        {
            Apply(0);
            _announced = false;
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
            _f.AbilityCooldownMul = p.berserk ? BerserkCooldownMul : 1f;
            _f.SecondWaveDelay = p.secondWave;
            var ai = Ai;
            if (ai != null)
            {
                if (p.aiProfile != null) ai.SetProfile(AiProfiles.Get(p.aiProfile));
                ai.aggressive = p.aggressive;
            }
        }
    }
}
