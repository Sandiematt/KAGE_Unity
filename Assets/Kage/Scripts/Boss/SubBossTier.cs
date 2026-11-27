using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Act I sub-boss difficulty ladder (docs/act1-subbosses-vfx.md §5): Kuroe (sub-boss I) is the gentlest, Shiden (sub-boss IV) the
    /// hardest, and every rung is harder than the one before on every knob. The JS stats stay as they are (<see cref="CharacterDef.FromJs"/>);
    /// a tier works on top of them:
    /// <list type="bullet">
    /// <item><b>Footwork</b>: <see cref="groundSpeed"/> (walk and run only, not dash or jump) and <see cref="runInBeyond"/> (further than this the
    /// AI holds forward long enough to break into a run instead of shuffling in).</item>
    /// <item><b>Brain</b>: <see cref="skill"/> is added to the stage's AI level (reaction time, guard, parry, punish, aggression, think rate).</item>
    /// <item><b>Pressure</b>: per-phase attack speed, damage and ability cooldown (<see cref="PhaseDef"/>), and how often and how fast one
    /// ability strings into the next (<see cref="AbilityDef.chain"/>, the attack patterns in each boss's abilities file).</item>
    /// <item><b>Stamina</b>: the health multipliers in Tuning/health.json climb with the rank.</item>
    /// </list>
    /// </summary>
    public sealed class SubBossTier
    {
        public string id;
        /// <summary>1..4: the sub-boss's place in Act I (act1.sub1 .. act1.sub4).</summary>
        public int rank;
        /// <summary>Walk and run speed on top of the JS speed (<see cref="CharacterDef.groundSpeedMul"/>).</summary>
        public float groundSpeed = 1f;
        /// <summary>Metres: further from Kage than this, the AI runs in (<see cref="CharacterDef.runInBeyond"/>).</summary>
        public float runInBeyond;
        /// <summary>Added to the AI's stage level (<see cref="CharacterDef.aiSkill"/>).</summary>
        public float skill;
        /// <summary>Phase 1 / Phase 2 attack playback rate (<see cref="PhaseDef.moveRateMul"/>).</summary>
        public float moveRate1 = 1f, moveRate2 = 1f;
        /// <summary>Phase 1 / Phase 2 damage on top of the JS dmg (<see cref="PhaseDef.damageMul"/>).</summary>
        public float damage1 = 1f, damage2 = 1f;
        /// <summary>Phase 1 / Phase 2 ability cooldowns (<see cref="PhaseDef.cooldownMul"/>); lower is more often.</summary>
        public float cooldown1 = 1f, cooldown2 = 1f;
        /// <summary>Phase 2's walk/run/dash speed on top of everything else (<see cref="PhaseDef.speedMul"/>).</summary>
        public float phase2Speed = 1f;
        /// <summary>Phase 1 / Phase 2 chance that a finished ability strings into its follow-up.</summary>
        public float chain1, chain2;
        /// <summary>Phase 1 / Phase 2 frames held between one link of a string and the next (the tell the player gets).</summary>
        public int beat1 = 12, beat2 = 10;

        public static readonly SubBossTier Kuroe = new()
        {
            id = Kage.Kuroe.Id, rank = 1,
            groundSpeed = 1.25f, runInBeyond = 3.3f, skill = -0.08f,
            moveRate1 = 1.0f, moveRate2 = 1.03f, damage1 = 0.9f, damage2 = 0.95f, cooldown1 = 1.15f, cooldown2 = 1.0f, phase2Speed = 1.05f,
            chain1 = 0.3f, chain2 = 0.5f, beat1 = 16, beat2 = 13,
        };

        public static readonly SubBossTier Garan = new()
        {
            id = Kage.Garan.Id, rank = 2,
            groundSpeed = 1.36f, runInBeyond = 3.0f, skill = 0f,
            moveRate1 = 1.03f, moveRate2 = 1.07f, damage1 = 1.0f, damage2 = 1.06f, cooldown1 = 1.0f, cooldown2 = 0.9f, phase2Speed = 1.08f,
            chain1 = 0.45f, chain2 = 0.62f, beat1 = 13, beat2 = 11,
        };

        public static readonly SubBossTier Hisame = new()
        {
            id = Kage.Hisame.Id, rank = 3,
            groundSpeed = 1.3f, runInBeyond = 2.6f, skill = 0.1f,
            moveRate1 = 1.07f, moveRate2 = 1.11f, damage1 = 1.2f, damage2 = 1.26f, cooldown1 = 0.9f, cooldown2 = 0.8f, phase2Speed = 1.1f,
            chain1 = 0.6f, chain2 = 0.76f, beat1 = 10, beat2 = 8,
        };

        public static readonly SubBossTier Shiden = new()
        {
            id = Kage.Shiden.Id, rank = 4,
            groundSpeed = 1.55f, runInBeyond = 2.3f, skill = 0.2f,
            moveRate1 = 1.1f, moveRate2 = 1.15f, damage1 = 1.3f, damage2 = 1.38f, cooldown1 = 0.8f, cooldown2 = 0.7f, phase2Speed = 1.12f,
            chain1 = 0.72f, chain2 = 0.88f, beat1 = 8, beat2 = 6,
        };

        /// <summary>The four rungs in Act I order.</summary>
        public static readonly SubBossTier[] Ladder = { Kuroe, Garan, Hisame, Shiden };

        /// <summary>The tier of a sub-boss id, or null.</summary>
        public static SubBossTier For(string id)
        {
            foreach (var t in Ladder) if (t.id == id) return t;
            return null;
        }

        /// <summary>Footwork and brain onto the character (the JS stats are left as they are).</summary>
        public void Apply(CharacterDef d)
        {
            if (d == null) return;
            d.groundSpeedMul = groundSpeed;
            d.runInBeyond = runInBeyond;
            d.aiSkill = skill;
        }

        /// <summary>Attack speed, damage, cooldowns and the Phase 2 speed-up onto the boss's two phases.</summary>
        public void Apply(BossDef b)
        {
            if (b == null || b.phases.Count == 0) return;
            var p1 = b.phases[0];
            p1.moveRateMul = moveRate1; p1.damageMul = damage1; p1.cooldownMul = cooldown1;
            if (b.phases.Count < 2) return;
            var p2 = b.phases[1];
            p2.moveRateMul = moveRate2; p2.damageMul = damage2; p2.cooldownMul = cooldown2; p2.speedMul = phase2Speed;
        }

        static bool PhaseTwo(Fighter f) => f?.Phases != null && f.Phases.Index >= 1;

        /// <summary>The chance a finished ability strings into the next (<see cref="AbilityDef.chainChance"/>).</summary>
        public float ChainChance(Fighter f) => PhaseTwo(f) ? chain2 : chain1;

        /// <summary>The frames between two links of a string (<see cref="AbilityDef.chainBeat"/>).</summary>
        public int ChainBeat(Fighter f) => PhaseTwo(f) ? beat2 : beat1;

        /// <summary>Makes <paramref name="id"/>'s ability string into the first ready one of <paramref name="next"/>, at this tier's chance and beat.</summary>
        public void Chain(string id, params string[] next)
        {
            var a = AbilityCatalog.Get(id);
            if (a == null) return;
            a.chain = next;
            a.chainChance = ChainChance;
            a.chainBeat = ChainBeat;
        }
    }
}
