using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kuroe, the Ink Censor (id "kuroe", SAN-103), the first Act I sub-boss and the worked example of a sub-boss entry (ADR-0008 notes).
    /// <list type="bullet">
    /// <item><b>Character</b> (<see cref="Def"/>): <see cref="CharacterDef.FromJs"/> gives the JS stats (health 360, defence 0.08, damage
    /// ×1.15, build scale 1.06), the temple staff, the defensive archetype, the ability slots (<c>censure</c>) and the boss id. This file adds
    /// the Unity side: the spear/staff family, the part set (<see cref="KuroePartSet"/>), mass, posture and colours.</item>
    /// <item><b>Abilities</b>: <see cref="KuroeAbilities"/> (Censure, the counter stance).</item>
    /// <item><b>Phases</b>: the JS table as it is, so <see cref="BossCatalog"/> needs no line: "The Black Line" (Censure), then at half
    /// health "Redacted" (Censure, aggressive). She stays on the defensive profile.</item>
    /// <item><b>Flags</b>: none. No heavy armour and no no-grab: a throw takes her, even out of Censure.</item>
    /// </list>
    /// </summary>
    public static class Kuroe
    {
        public const string Id = "kuroe";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.family = WeaponFamilies.Spear;
            d.mass = 1.1f;              // tall and slight (JS build w 9.6) but a sub-boss: a little harder to shove than Kage
            d.postureMax = 80f;         // between a regular foe (60) and Raikon (100)
            d.colors = new FighterColors(KuroePalette.Gold, KuroePalette.Paper, KuroePalette.InkDeep);
            KuroeAbilities.Register();
            return d;
        }
    }
}
