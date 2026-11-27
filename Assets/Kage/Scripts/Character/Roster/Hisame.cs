using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hisame, the Rain Courier (id "hisame", SAN-105), the third Act I sub-boss: fights with Tessenjutsu iron-ribbed Mist Fans,
    /// cold rain-mist screens, and Raikon-trained lightning steps (<see cref="HisameAbilities"/>).
    /// </summary>
    public static class Hisame
    {
        public const float BodyScale = 1.24f;
        public const string Id = "hisame";

        /// <summary>JS <c>FIGHT_STYLES.hisame.poise</c> (41-styles/hisame.js): "light: her guard does not hold a heavy blow", posture gained × 1.15.</summary>
        const float Poise = 1.15f;

        public static Wind RainWind => Wind.Blowing(new Vector2(-1.2f, -0.15f), 1.4f, 83, 0.6f);
        public static Wind DownpourSquall => Wind.Blowing(new Vector2(-3.1f, -0.35f), 3.6f, 97, 1.0f);
        public const int WindRampFrames = 50;

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.bodyScale = BodyScale;
            d.partSet = Id;
            d.family = WeaponFamilies.Twin;
            d.mass = 0.85f;                // slight (JS build w 8.8 at scale 0.95): knockback carries her further than Kage
            d.postureMax = 80f / Poise;    // a sub-boss's 80 (Kuroe, Garan), filled 1.15 times as fast: 70
            d.colors = new FighterColors(HisamePalette.Accent, HisamePalette.Pale, HisamePalette.RainDeep);
            foreach (var id in HisameAbilities.ExtraIds)
                if (!d.abilities.Contains(id)) d.abilities.Add(id);
            HisameAbilities.Register(d.bodyScale);
            SubBossTier.Hisame.Apply(d);         // sub-boss rank on the Act I ladder: walk speed, run-in, AI skill
            return d;
        }

        /// <summary>
        /// Hisame's two phases: "Rainfall" (Lightning Cut, Kocho + Tessenjutsu Kasumi Fan Dance and Shigure Fan Gust) and at 50 %
        /// "Downpour" (berserk, squall wind, fan static, Smoke Bomb + Raikon-trained Inazuma Mist Step).
        /// </summary>
        public static BossDef Boss()
        {
            HisameAbilities.Register(BodyScale);
            var b = BossDef.FromJs(Id);
            b.phases[0].abilities = new[]
            {
                HisameAbilities.LightningCutId, HisameAbilities.KochoId,
                HisameAbilities.KasumiMaiId, HisameAbilities.RainGustId,
            };
            b.phases[0].wind = RainWind;
            b.phases[0].onEnter = SubBossAura.EnterHisame;

            b.phases[1].abilities = new[]
            {
                HisameAbilities.LightningCutId, HisameAbilities.SmokeBombId, HisameAbilities.KochoId,
                HisameAbilities.KasumiMaiId, HisameAbilities.RainGustId, HisameAbilities.InazumaStepId,
            };
            b.phases[1].wind = DownpourSquall;
            b.phases[1].windRampFrames = WindRampFrames;
            b.phases[1].onEnter = SubBossAura.EnterHisame;
            SubBossTier.Hisame.Apply(b);         // attack speed, damage, cooldowns per phase, Phase 2 speed-up
            b.Validate();
            return b;
        }
    }
}
