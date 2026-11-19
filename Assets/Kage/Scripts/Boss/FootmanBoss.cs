using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The test boss (SAN-102): the Order Footman's data with two phases, there to prove the boss layer before any real boss exists and to
    /// give the dev arena a boss to fight (F5). It is also the worked example of a boss entry (ADR-0008 notes):
    /// <list type="bullet">
    /// <item><b>Character</b> (<see cref="Def"/>): a <see cref="CharacterDef"/> with <c>bossId</c> set and the boss's abilities in slot order.</item>
    /// <item><b>Abilities</b> (<see cref="RegisterAbilities"/>): <see cref="AbilityDef"/>s in the <see cref="AbilityCatalog"/>; here they reuse two
    /// katana moves under new ids, a real boss writes its own <see cref="MoveDef"/>s.</item>
    /// <item><b>Phases</b> (<see cref="Boss"/>): a <see cref="BossDef"/> of <see cref="PhaseDef"/>s, registered in <see cref="BossCatalog"/>.</item>
    /// </list>
    /// Phase 1 "Drill Order": only the cleave. Phase 2 "Charge" (health 50%): adds the rush, the boss profile, aggressive, 30% faster on its
    /// feet, attacks 15% quicker, hits 20% harder, with armour and no-grab.
    /// </summary>
    public static class FootmanBoss
    {
        public const string Id = "footman_boss";
        public const string CleaveId = "captainCleave", RushId = "captainRush";

        public static CharacterDef Def()
        {
            var d = OrderFootman.Def();
            d.id = Id;
            d.displayName = "Footman Captain";
            d.title = "Test boss";
            d.bossId = Id;
            d.abilities = new System.Collections.Generic.List<string> { CleaveId, RushId };
            RegisterAbilities();
            return d;
        }

        public static void RegisterAbilities()
        {
            var katana = KatanaMoves.Build();
            var cleave = katana.cleave; cleave.id = "captain.cleave"; cleave.display = "Captain's Cleave";
            var rush = katana.shadowStep; rush.id = "captain.rush"; rush.display = "Captain's Rush";
            AbilityCatalog.Register(new AbilityDef { id = CleaveId, move = cleave, cooldownFrames = 150, range = new Vector2(0f, 1.6f), chance = 0.5f });
            AbilityCatalog.Register(new AbilityDef { id = RushId, move = rush, cooldownFrames = 180, range = new Vector2(1.5f, 4f), chance = 0.6f });
        }

        public static BossDef Boss() => new()
        {
            id = Id, name = "Footman Captain", title = "Test boss",
            phases =
            {
                new PhaseDef { name = "Drill Order", at = 1f, abilities = new[] { CleaveId } },
                new PhaseDef
                {
                    name = "Charge", at = 0.5f, abilities = new[] { CleaveId, RushId },
                    aiProfile = "boss", aggressive = true,
                    speedMul = 1.3f, moveRateMul = 1.15f, damageMul = 1.2f,
                    addFlags = CharacterFlags.Armour | CharacterFlags.NoGrab,
                },
            },
        };
    }
}
