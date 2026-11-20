using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Raikon (id "raikon"), the Act I boss: his JS stats (health 640, defence 0.16, damage ×1.1, speed ×0.9, build
    /// scale 1.24, heavy armour, no-grab) and his look, cut from the owner's painting (<see cref="RaikonPartSet"/>, SAN-71).
    /// His fight (moves, phases, abilities, mass, posture) is SAN-81 / SAN-107.
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
            d.bladeLength = RaikonModel.PaintedBladeLength;               // his hit line is his painted blade, not the family's 1.5 m (SAN-107)
            d.colors = new FighterColors(KMath.Hex("#FF4A3C"), KMath.Hex("#FFE2DC"), KMath.Hex("#7A0E14"));
            RaikonAbilities.Register(d.bodyScale);
            return d;
        }
    }
}
