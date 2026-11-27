using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Raikon (id "raikon"), the Act I boss: his JS stats (health 640, defence 0.16, damage ×1.1, speed ×0.9, build
    /// scale 1.24, heavy armour, no-grab) and his look, cut from the owner's painting (<see cref="RaikonPartSet"/>, SAN-71).
    /// His fight: the tachi set, mass, posture, a hit line that is his painted blade (<see cref="CharacterDef.bladeLength"/>), and the Lightning Ascendant's
    /// three phases and twenty-three abilities (<see cref="RaikonAscendant"/>, docs/raikon-ascendant-vfx.md). The four SAN-107 abilities
    /// (<see cref="RaikonAbilities"/>) keep their slots but no phase enables them.
    /// </summary>
    public static class Raikon
    {
        public const string Id = "raikon";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = "raikon";
            d.family = WeaponFamilies.Tachi;
            d.mass = 2.2f;              // the heaviest of Act I (JS build w 13.5 at scale 1.24, heavy armour): pushed back least (Garan 1.8)
            d.postureMax = 100f / (float)JsTables.FightStyle(Id).Poise;   // a boss's 100 (SAN-86), filled 0.75 times as fast (JS poise 0.75: a quarter harder to stagger): 133
            d.moveRate = 1f / WeaponStats.Combat(d.weaponId, 1).speed;    // raikon_tachi's JS speed (40 against Onikiri's 35) would shorten every tell: his moves play as authored
            d.bladeLength = RaikonPartSet.PaintedBladeLength;             // his hit line is his painted blade, not the family's 1.5 m (SAN-107)
            d.colors = new FighterColors(KMath.Hex("#FF4A3C"), KMath.Hex("#FFE2DC"), KMath.Hex("#7A0E14"));
            RaikonAbilities.Register(d.bodyScale);
            // the Lightning Ascendant's kit (RaikonAscendant): its slots follow the four SAN-107 ones, which no phase enables any more
            foreach (var id in RaikonAscendant.AllIds) if (!d.abilities.Contains(id)) d.abilities.Add(id);
            RaikonAscendant.Register(d.bodyScale);
            return d;
        }

        /// <summary>His boss data: the Lightning Ascendant's three phases and the Final Overload (<see cref="RaikonAscendant.Boss"/>).</summary>
        public static BossDef Boss() => RaikonAscendant.Boss();
    }
}
