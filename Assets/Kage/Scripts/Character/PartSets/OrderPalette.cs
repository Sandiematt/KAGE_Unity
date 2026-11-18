using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order of Silence's colours (SAN-69): slate-tinted lacquer, cold silver, a muted crimson for the lacing. Shared by every
    /// Order soldier's part set so the family reads as one (Footman, Runner, Shieldbearer, Chainthrower, Ironclad). Taken from the
    /// JS Order skin (<c>21-characters/enemies/order/_skin.js</c>): near-black lacquer with a blue-grey lift, muted silver fittings.
    /// Kage is warm black and crimson; the Order is cool slate and silver, with crimson only in the lacing.
    /// </summary>
    public static class OrderPalette
    {
        public static readonly Color Lacquer = KMath.Hex("#12161B");        // dō, kusazuri, sode
        public static readonly Color LacquerFar = KMath.Hex("#0B0E12");     // the same on the far side of the body
        public static readonly Color LacquerLit = KMath.Hex("#2E3641");     // lit edge of a plate
        public static readonly Color Cloth = KMath.Hex("#232B35");          // gi, hakama, hood
        public static readonly Color ClothDeep = KMath.Hex("#171D25");
        public static readonly Color ClothFar = KMath.Hex("#10141A");
        public static readonly Color Glove = KMath.Hex("#1B2027");
        public static readonly Color Silver = KMath.Hex("#AEB2B8");
        public static readonly Color SilverMid = KMath.Hex("#6F737A");
        public static readonly Color SilverDark = KMath.Hex("#3C4048");
        public static readonly Color Lacing = KMath.Hex("#8A2F37");         // odoshi cords: the only crimson
        public static readonly Color Mask = KMath.Hex("#D5D9DD");           // the fox mask
        public static readonly Color MaskShade = KMath.Hex("#8E959D");
        public static readonly Color Eye = KMath.Hex("#C4C8CC");
        public static readonly Color Rim = KMath.Hex("#8EA0B6");            // cold back-light
        public static readonly Color Steel = KMath.Hex("#A5AFBA");          // the Order's plain blades
    }
}
