namespace Kage
{
    /// <summary>
    /// Hisame, the Rain Courier (id "hisame", SAN-105), the third Act I sub-boss, built on Kuroe's and Garan's recipe (ADR-0008 notes).
    /// <list type="bullet">
    /// <item><b>Character</b> (<see cref="Def"/>): <see cref="CharacterDef.FromJs"/> gives the JS stats (health 375, defence 0.04, damage
    /// ×0.95, speed ×1.16, build scale 0.95), the Mist Fans, the agile archetype, the ability slots (<c>lightningCut</c>, <c>hisameKocho</c>,
    /// <c>smokeBomb</c>) and the boss id. This file adds the Unity side: the twin blades set, the part set (<see cref="HisamePartSet"/>), a
    /// light mass, a light posture and her colours.</item>
    /// <item><b>Abilities</b>: <see cref="HisameAbilities"/> (Lightning Cut, Kocho the butterfly step, Smoke Bomb).</item>
    /// <item><b>Phases</b>: the JS table as it is, so <see cref="BossCatalog"/> needs no line: "Rainfall" (Lightning Cut, Kocho), then at half
    /// health "Downpour" (adds Smoke Bomb, berserk: abilities cool down in 0.7 of the time). She stays on the agile profile.</item>
    /// <item><b>Flags</b>: none. A throw takes her, except out of Kocho's invulnerable step.</item>
    /// </list>
    /// </summary>
    public static class Hisame
    {
        public const string Id = "hisame";

        /// <summary>JS <c>FIGHT_STYLES.hisame.poise</c> (41-styles/hisame.js): "light: her guard does not hold a heavy blow", posture gained × 1.15.</summary>
        const float Poise = 1.15f;

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.family = WeaponFamilies.Twin;
            d.mass = 0.85f;                // slight (JS build w 8.8 at scale 0.95): knockback carries her further than Kage
            d.postureMax = 80f / Poise;    // a sub-boss's 80 (Kuroe, Garan), filled 1.15 times as fast: 70
            d.colors = new FighterColors(HisamePalette.Accent, HisamePalette.Pale, HisamePalette.RainDeep);
            HisameAbilities.Register(d.bodyScale);
            return d;
        }
    }
}
