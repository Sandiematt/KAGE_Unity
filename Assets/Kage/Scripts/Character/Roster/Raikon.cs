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
            d.postureMax = 100f;        // a boss's posture bar is larger (SAN-86); SAN-107 tunes it
            d.colors = new FighterColors(KMath.Hex("#FF4A3C"), KMath.Hex("#FFE2DC"), KMath.Hex("#7A0E14"));
            return d;
        }
    }
}
