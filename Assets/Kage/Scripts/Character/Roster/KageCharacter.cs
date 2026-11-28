using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kage (id "kage"). His health and defence come from the rules and his save: JS health is
    /// <see cref="GameRules.PlayerMaxHp"/> (232 on a fresh save, so 100 at the default health scale). His equipped weapon and
    /// level, helmet and armour, damage (player level and the damage perk), speed (the speed perk) and the other
    /// perks (<see cref="GearPerks"/>) come from the save too (SAN-84).
    /// The class isn't called <c>Kage</c> because that is the namespace.
    /// </summary>
    public static class KageCharacter
    {
        public const string Id = "kage";

        /// <summary>
        /// Kage as his save has him equipped. A fresh save (or null) is the starter loadout: Kagetsu, Crimson Scarf and Shinobi
        /// Garb at level 1, JS health 232, defence 0.03, no perks and every multiplier 1, exactly the Kage the baseline replay was recorded with.
        /// </summary>
        /// <param name="save">Whose level and gear; null = a fresh save.</param>
        public static CharacterDef Def(SaveData save = null)
        {
            save ??= GameRules.DefaultSave(false);
            var stats = GameRules.PlayerCombatStats(save);
            var perks = GearPerks.FromJs(stats.Perks);
            string weapon = save.equipped?.weapon ?? GameRules.StarterWeaponId;
            int weaponLevel = save.owned != null && save.owned.weapons.TryGetValue(weapon, out var lv) && lv > 0 ? lv : 1;
            return new CharacterDef
            {
                id = Id,
                displayName = "KAGE",
                title = "影",
                jsHealth = stats.Hp,
                defense = (float)stats.Defense,
                damageMul = (float)stats.DamageMul,
                speedMul = perks.SpeedMul,
                perks = perks,
                partSet = "kage",
                hairstyle = Hairstyles.Resolve(save.equipped?.hairstyle),
                armor = save.equipped?.armor,
                colors = new FighterColors(Palette.Trim, Color.white, Palette.TrimDeep),
                family = WeaponFamilies.FamilyIdOf(weapon) ?? WeaponFamilies.Katana,
                weaponId = weapon,
                weaponLevel = weaponLevel,
                aiProfile = null,
                flags = CharacterFlags.CanRageArt | CharacterFlags.CanThrowKunai,
            };
        }

        /// <summary>
        /// A text that is the same for two saves exactly when <see cref="Def"/> builds the same Kage from them (weapon, hairstyle and armour
        /// with their levels, and the player level). A screen that already built a fight compares it to know when to build it again.
        /// </summary>
        public static string LoadoutKey(SaveData save)
        {
            save ??= GameRules.DefaultSave(false);
            int Lv(System.Collections.Generic.Dictionary<string, int> owned, string id) => id != null && owned != null && owned.TryGetValue(id, out var l) && l > 0 ? l : 1;
            var e = save.equipped;
            return $"{e?.weapon}:{Lv(save.owned?.weapons, e?.weapon)}|{Hairstyles.Resolve(e?.hairstyle)}|{e?.armor}:{Lv(save.owned?.armors, e?.armor)}|{save.level}";
        }
    }
}
