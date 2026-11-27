using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Garan, the Stormbroken (id "garan", SAN-104), the second Act I sub-boss: former vanguard of Raikon's thunder guard, fighting
    /// with Kyokushin Karate &amp; Sumō strikes plus scarred vanguard lightning (<see cref="GaranAbilities"/>).
    /// </summary>
    public static class Garan
    {
        public const float BodyScale = 1.35f;
        public const string Id = "garan";

        public static Wind EmberGust => Wind.Blowing(new Vector2(-2.0f, 0.22f), 2.3f, 67, 0.75f);
        public const int WindRampFrames = 55;

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.bodyScale = BodyScale;
            d.partSet = Id;
            d.family = WeaponFamilies.Cleaver;
            d.weaponId = "onikiri";
            d.bladeLength = SubBossBlades.GreatswordLength;
            d.mass = 1.8f;              // the heavy brawler (JS build w 14 at scale 1.18): knockback moves him about half as far as Kage
            d.postureMax = 80f;         // a sub-boss's, as Kuroe's (a regular foe 60, Raikon 100)
            d.colors = new FighterColors(GaranPalette.Storm, GaranPalette.Horn, GaranPalette.LacquerDeep);
            if (!d.abilities.Contains(GaranAbilities.ClinchId)) d.abilities.Add(GaranAbilities.ClinchId);
            foreach (var id in GaranAbilities.ExtraIds)
                if (!d.abilities.Contains(id)) d.abilities.Add(id);
            GaranAbilities.Register(d.bodyScale);
            SubBossTier.Garan.Apply(d);         // sub-boss rank on the Act I ladder: walk speed, run-in, AI skill
            return d;
        }

        /// <summary>
        /// Garan's two phases: "Thunderstruck" (Storm Fist, Tsuppari, Clinch + Karate/Sumō combos and Shiko Raijin Stomp) and at 50 %
        /// "Stormbroken" (adds Charge + Raikon-trained Broken Static Palm, ember wind, and chest-scar to tekko lightning arcs).
        /// </summary>
        public static BossDef Boss()
        {
            GaranAbilities.Register(BodyScale);
            var b = BossDef.FromJs(Id);
            b.phases[0].abilities = new[]
            {
                GaranAbilities.StormFistId, GaranAbilities.TsuppariId, GaranAbilities.ClinchId,
                GaranAbilities.GedanComboId, GaranAbilities.ShikoStompId,
            };
            b.phases[0].wind = Wind.Calm;
            b.phases[0].onEnter = SubBossAura.EnterGaran;

            b.phases[1].abilities = new[]
            {
                GaranAbilities.StormFistId, GaranAbilities.ChargeId, GaranAbilities.TsuppariId, GaranAbilities.ClinchId,
                GaranAbilities.GedanComboId, GaranAbilities.ShikoStompId, GaranAbilities.BrokenPalmId,
            };
            b.phases[1].wind = EmberGust;
            b.phases[1].windRampFrames = WindRampFrames;
            b.phases[1].onEnter = SubBossAura.EnterGaran;
            SubBossTier.Garan.Apply(b);         // attack speed, damage, cooldowns per phase, Phase 2 speed-up
            b.Validate();
            return b;
        }
    }
}
