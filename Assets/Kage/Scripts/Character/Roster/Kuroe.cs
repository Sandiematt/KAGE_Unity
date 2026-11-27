using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kuroe, the Ink Censor (id "kuroe", SAN-103), the first Act I sub-boss: fights with a giant calligraphy ink brush-pen
    /// (<c>temple_staff</c> in <see cref="SpearVisual"/>), combining Bōjutsu &amp; Bleach/Ichimonji sumi-e ink slashes with an apprentice
    /// lightning technique taught by Raikon (<see cref="KuroeAbilities"/>).
    /// </summary>
    public static class Kuroe
    {
        public const float BodyScale = 1.21f;
        public const string Id = "kuroe";

        public static Wind InkBreeze => Wind.Blowing(new Vector2(-1.6f, 0.18f), 1.9f, 53, 0.65f);
        public const int WindRampFrames = 55;

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.bodyScale = BodyScale;
            d.partSet = Id;
            d.family = WeaponFamilies.Spear;
            d.mass = 1.1f;              // tall and slight (JS build w 9.6) but a sub-boss: a little harder to shove than Kage
            d.postureMax = 80f;         // between a regular foe (60) and Raikon (100)
            d.colors = new FighterColors(KuroePalette.Gold, KuroePalette.Paper, KuroePalette.InkDeep);
            foreach (var id in KuroeAbilities.ExtraIds)
                if (!d.abilities.Contains(id)) d.abilities.Add(id);
            KuroeAbilities.Register();
            SubBossTier.Kuroe.Apply(d);         // sub-boss rank on the Act I ladder: walk speed, run-in, AI skill
            return d;
        }

        /// <summary>
        /// Kuroe's two phases: "The Black Line" (Censure + Bōjutsu calligraphy ink moves) and at 50 % "Redacted" (aggressive, ink wind,
        /// dripping crimson-ink brush static, Sumi Nidan's delayed ink-spark floor line, and Raikon-trained Thunder Redaction).
        /// </summary>
        public static BossDef Boss()
        {
            KuroeAbilities.Register();
            var b = BossDef.FromJs(Id);
            b.phases[0].abilities = new[]
            {
                KuroeAbilities.CensureId, KuroeAbilities.SumiNidanId, KuroeAbilities.SumiBaraiId, KuroeAbilities.BokusekiWaveId,
            };
            b.phases[0].wind = Wind.Calm;
            b.phases[0].onEnter = SubBossAura.EnterKuroe;

            b.phases[1].abilities = new[]
            {
                KuroeAbilities.CensureId, KuroeAbilities.SumiNidanId, KuroeAbilities.SumiBaraiId, KuroeAbilities.BokusekiWaveId,
                KuroeAbilities.ThunderRedactionId,
            };
            b.phases[1].wind = InkBreeze;
            b.phases[1].windRampFrames = WindRampFrames;
            b.phases[1].onEnter = SubBossAura.EnterKuroe;
            SubBossTier.Kuroe.Apply(b);         // attack speed, damage, cooldowns per phase, Phase 2 speed-up
            b.Validate();
            return b;
        }
    }
}
