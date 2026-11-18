using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The perks of the helmet and armour Kage wears, summed (JS <c>playerCombatStats().perks</c>, ADR-0005). Raw JS values: a
    /// fraction (<c>0.12</c> = 12 %) except <see cref="ammo"/>, a count. <see cref="KageCharacter.Def(Rules.SaveData)"/> fills it from the
    /// save, <see cref="Fighter"/> applies it (<c>Fighter.Gear.cs</c>). Eight perks exist in the JS data:
    /// <list type="bullet">
    /// <item><c>dmg</c> and <c>speed</c>: folded into the character's <see cref="CharacterDef.damageMul"/> and <see cref="CharacterDef.speedMul"/>.</item>
    /// <item><c>rage</c>, <c>stunRes</c>, <c>dash</c>, <c>ammo</c>, <c>kunai</c>: read by the fighter (rage gain, hit-stun, dash, kunai count and damage).</item>
    /// <item><c>blockRage</c>: read as <see cref="BlockRageMul"/>, used by the block's rage gain when the guard ticket adds it (ADR-0005).</item>
    /// </list>
    /// </summary>
    [System.Serializable]
    public struct GearPerks
    {
        /// <summary>Kunai Kage starts a fight with before the <c>ammo</c> perk (JS 5).</summary>
        public const int KunaiBase = 5;

        public float speed, dash, ammo, stunRes, rage, kunai, dmg, blockRage;

        /// <summary>Every perk id in the JS data.</summary>
        public static readonly string[] Keys = { "speed", "dash", "ammo", "stunRes", "rage", "kunai", "dmg", "blockRage" };

        public bool IsNone => speed == 0f && dash == 0f && ammo == 0f && stunRes == 0f && rage == 0f && kunai == 0f && dmg == 0f && blockRage == 0f;

        /// <summary>1 + the speed perk: locomotion speed.</summary>
        public float SpeedMul => 1f + speed;
        /// <summary>1 + the damage perk.</summary>
        public float DamageMul => 1f + dmg;
        /// <summary>1 + the rage perk: every Rage gain is multiplied by it.</summary>
        public float RageMul => 1f + rage;
        /// <summary>1 + the kunai perk: kunai damage.</summary>
        public float KunaiMul => 1f + kunai;
        /// <summary>1 + the block-rage perk (JS <c>1 + blockRage</c>): the extra a block's Rage gain is multiplied by.</summary>
        public float BlockRageMul => 1f + blockRage;
        /// <summary>The kunai count a fight starts with: 5 + the ammo perk.</summary>
        public int KunaiStart => KunaiBase + Mathf.RoundToInt(ammo);

        /// <summary>The value of a perk by its JS id; 0 for an id this game doesn't know.</summary>
        public float Get(string key)
        {
            switch (key)
            {
                case "speed": return speed;
                case "dash": return dash;
                case "ammo": return ammo;
                case "stunRes": return stunRes;
                case "rage": return rage;
                case "kunai": return kunai;
                case "dmg": return dmg;
                case "blockRage": return blockRage;
                default: return 0f;
            }
        }

        /// <summary>The perks of a JS perk table (id to value), e.g. <c>PlayerGearStats.Perks</c>. Null = none.</summary>
        public static GearPerks FromJs(IDictionary<string, double> perks)
        {
            var p = new GearPerks();
            if (perks == null) return p;
            foreach (var kv in perks)
            {
                float v = (float)kv.Value;
                switch (kv.Key)
                {
                    case "speed": p.speed += v; break;
                    case "dash": p.dash += v; break;
                    case "ammo": p.ammo += v; break;
                    case "stunRes": p.stunRes += v; break;
                    case "rage": p.rage += v; break;
                    case "kunai": p.kunai += v; break;
                    case "dmg": p.dmg += v; break;
                    case "blockRage": p.blockRage += v; break;
                }
            }
            return p;
        }
    }
}
