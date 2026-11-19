using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// Shiden, the Last Lieutenant (id "shiden", SAN-106), the fourth Act I sub-boss, built on Kuroe's, Garan's and Hisame's recipe (ADR-0008 notes).
    /// <list type="bullet">
    /// <item><b>Character</b> (<see cref="Def"/>): <see cref="CharacterDef.FromJs"/> gives the JS stats (health 400, defence 0.12, damage
    /// ×0.93, build scale 1.12), the Vanguard Yari, the boss archetype, no-grab (the NoGrab flag: every throw fails on him), the ability
    /// slots (<c>vanguardBreak</c>, <c>shidenBrace</c>, <c>vanguardGuard</c>) and the boss id. This file adds the Unity side: the spear set,
    /// the part set (<see cref="ShidenPartSet"/>), mass, posture, colours, and the charging thrust as a fourth slot.</item>
    /// <item><b>Abilities</b>: <see cref="ShidenAbilities"/> (Vanguard Break with its shockwave hazard, the Braced Spear, Hold the Line, the
    /// charging thrust).</item>
    /// <item><b>Phases</b> (<see cref="Boss"/>): the JS table, "Vanguard" (Vanguard Break, Brace) then at half health "Hold the Line" (adds
    /// Vanguard Guard, aggressive), with the charging thrust added to both. He stays on the boss profile.</item>
    /// </list>
    /// </summary>
    public static class Shiden
    {
        public const string Id = "shiden";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.family = WeaponFamilies.Spear;
            d.mass = 1.3f;                                               // armoured and square (JS build w 11.4 at scale 1.12): heavier than Kage
            d.postureMax = 80f / (float)JsTables.FightStyle(Id).Poise;   // a sub-boss's 80, filled 0.9 times as fast (JS poise 0.9: disciplined): 89
            d.colors = new FighterColors(ShidenPalette.Accent, ShidenPalette.Pale, ShidenPalette.LacquerDeep);
            d.abilities.Add(ShidenAbilities.ChargeId);
            ShidenAbilities.Register(d.bodyScale);
            return d;
        }

        /// <summary>His boss data: the JS phases with the charging thrust enabled in both.</summary>
        public static BossDef Boss()
        {
            var b = BossDef.FromJs(Id);
            foreach (var p in b.phases)
            {
                var list = new System.Collections.Generic.List<string>(p.abilities ?? new string[0]) { ShidenAbilities.ChargeId };
                p.abilities = list.ToArray();
            }
            return b;
        }
    }
}
