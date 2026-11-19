namespace Kage
{
    /// <summary>
    /// Garan, the Stormbroken (id "garan", SAN-104), the second Act I sub-boss, built on Kuroe's recipe (ADR-0008 notes).
    /// <list type="bullet">
    /// <item><b>Character</b> (<see cref="Def"/>): <see cref="CharacterDef.FromJs"/> gives the JS stats (health 395, defence 0.1, damage
    /// ×1.1, speed ×0.92, build scale 1.18), the iron fists, the heavy archetype, heavy armour (the Armour flag: light hits never flinch
    /// him), the ability slots (<c>stormFist</c>, <c>garanTsuppari</c>, <c>charge</c>) and the boss id. This file adds the Unity side: the
    /// heavy fists set, the part set (<see cref="GaranPartSet"/>, with his iron gauntlets), heavy mass, posture, colours, and the clinch
    /// as a fourth slot.</item>
    /// <item><b>Abilities</b>: <see cref="GaranAbilities"/> (Storm Fist with its shock-area hazard, Tsuppari, the charge, the clinch).</item>
    /// <item><b>Phases</b> (<see cref="Boss"/>): the JS table, "Thunderstruck" (Storm Fist, Tsuppari) then at half health "Stormbroken"
    /// (adds the charge), with the clinch added to both (it is part of his style in every phase). He stays on the heavy profile.</item>
    /// </list>
    /// </summary>
    public static class Garan
    {
        public const string Id = "garan";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.family = WeaponFamilies.Fists;
            d.mass = 1.8f;              // the heavy brawler (JS build w 14 at scale 1.18): knockback moves him about half as far as Kage
            d.postureMax = 80f;         // a sub-boss's, as Kuroe's (a regular foe 60, Raikon 100)
            d.colors = new FighterColors(GaranPalette.Storm, GaranPalette.Horn, GaranPalette.LacquerDeep);
            d.abilities.Add(GaranAbilities.ClinchId);
            GaranAbilities.Register(d.bodyScale);
            return d;
        }

        /// <summary>His boss data: the JS phases with the clinch enabled in both.</summary>
        public static BossDef Boss()
        {
            var b = BossDef.FromJs(Id);
            foreach (var p in b.phases)
            {
                var list = new System.Collections.Generic.List<string>(p.abilities ?? new string[0]) { GaranAbilities.ClinchId };
                p.abilities = list.ToArray();
            }
            return b;
        }
    }
}
