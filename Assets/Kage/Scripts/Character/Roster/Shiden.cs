using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Shiden, the Last Lieutenant (id "shiden", SAN-106), the fourth Act I sub-boss: Raikon's right-hand general, fighting with
    /// Hōzōin-ryū sōjutsu spear forms and disciplined lieutenant lightning (<see cref="ShidenAbilities"/>).
    /// </summary>
    public static class Shiden
    {
        public const float BodyScale = 1.28f;
        public const string Id = "shiden";

        public static Wind LieutenantGust => Wind.Blowing(new Vector2(-2.1f, 0.24f), 2.5f, 89, 0.75f);
        public const int WindRampFrames = 55;

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.bodyScale = BodyScale;
            d.partSet = Id;
            d.family = WeaponFamilies.Spear;
            d.mass = 1.3f;                                               // armoured and square (JS build w 11.4 at scale 1.12): heavier than Kage
            d.postureMax = 80f / (float)JsTables.FightStyle(Id).Poise;   // a sub-boss's 80, filled 0.9 times as fast (JS poise 0.9: disciplined): 89
            d.colors = new FighterColors(ShidenPalette.Accent, ShidenPalette.Pale, ShidenPalette.LacquerDeep);
            if (!d.abilities.Contains(ShidenAbilities.ChargeId)) d.abilities.Add(ShidenAbilities.ChargeId);
            foreach (var id in ShidenAbilities.ExtraIds)
                if (!d.abilities.Contains(id)) d.abilities.Add(id);
            ShidenAbilities.Register(d.bodyScale);
            SubBossTier.Shiden.Apply(d);         // sub-boss rank on the Act I ladder: walk speed, run-in, AI skill
            return d;
        }

        /// <summary>
        /// Shiden's two phases: "Vanguard" (Vanguard Break, Brace, Charging Thrust + Hōzōin-ryū Maki-Otoshi and Suriage Sweep) and at 50 %
        /// "Hold the Line" (aggressive, lieutenant storm wind, spear-shaft lightning arcs, Vanguard Guard + Raikon-trained Vanguard Raijin Arc).
        /// </summary>
        public static BossDef Boss()
        {
            ShidenAbilities.Register(BodyScale);
            var b = BossDef.FromJs(Id);
            b.phases[0].abilities = new[]
            {
                ShidenAbilities.VanguardBreakId, ShidenAbilities.BraceId, ShidenAbilities.ChargeId,
                ShidenAbilities.MakiOtoshiId, ShidenAbilities.SojutsuSweepId,
            };
            b.phases[0].wind = Wind.Calm;
            b.phases[0].onEnter = SubBossAura.EnterShiden;

            b.phases[1].abilities = new[]
            {
                ShidenAbilities.VanguardBreakId, ShidenAbilities.VanguardGuardId, ShidenAbilities.BraceId, ShidenAbilities.ChargeId,
                ShidenAbilities.MakiOtoshiId, ShidenAbilities.SojutsuSweepId, ShidenAbilities.RaijinLanceId,
            };
            b.phases[1].wind = LieutenantGust;
            b.phases[1].windRampFrames = WindRampFrames;
            b.phases[1].onEnter = SubBossAura.EnterShiden;
            SubBossTier.Shiden.Apply(b);         // attack speed, damage, cooldowns per phase, Phase 2 speed-up
            b.Validate();
            return b;
        }
    }
}
