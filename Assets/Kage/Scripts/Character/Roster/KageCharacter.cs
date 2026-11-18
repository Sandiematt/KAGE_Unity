using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kage (id "kage"). His health and defence come from the rules and his save: JS health is
    /// <see cref="GameRules.PlayerMaxHp"/> (232 on a fresh save, so 100 at the default health scale).
    /// The class isn't called <c>Kage</c> because that is the namespace.
    /// </summary>
    public static class KageCharacter
    {
        public const string Id = "kage";

        /// <param name="save">Whose level and gear; null = a fresh save.</param>
        public static CharacterDef Def(SaveData save = null)
        {
            save ??= GameRules.DefaultSave(false);
            var stats = GameRules.PlayerCombatStats(save);
            return new CharacterDef
            {
                id = Id,
                displayName = "KAGE",
                title = "影",
                jsHealth = stats.Hp,
                defense = (float)stats.Defense,
                partSet = "kage",
                helmet = save.equipped?.helmet,
                colors = new FighterColors(Palette.Trim, Color.white, Palette.TrimDeep),
                family = "katana",
                aiProfile = null,
                flags = CharacterFlags.CanRageArt | CharacterFlags.CanThrowKunai,
            };
        }
    }
}
