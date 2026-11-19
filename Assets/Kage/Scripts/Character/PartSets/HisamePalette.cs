using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hisame's colours (SAN-105), from the JS sub-boss tones (<c>21-characters/sub-bosses/_shared/tones.js</c>, <c>hisame/*.js</c>): her oiled
    /// rain cloth (<c>RAIN</c>: #07090c, #10151b, #212a35) for the gi, the hood and the mino cape, black lacquer (<c>SUB_LACQ</c>) for the visor
    /// and the message tube, slate shin wraps and kote, and her rain blue (<c>#7fb8ff</c>, her accent; <c>#9fd4ff</c>, her eye) in the cords, the
    /// hems, the tube's bands and a cold rain back-light. Kuroe is ink, gold and paper; Garan iron and storm red; the Order slate and silver;
    /// the Hollows violet: Hisame is rain-dark cloth and rain blue.
    /// </summary>
    public static class HisamePalette
    {
        public static readonly Color Rain = KMath.Hex("#10151B");            // RAIN[1]: the gi, the hood
        public static readonly Color RainDeep = KMath.Hex("#07090C");        // RAIN[0]
        public static readonly Color RainLit = KMath.Hex("#212A35");         // RAIN[2]: a lit fold, the strand rows
        public static readonly Color RainFar = KMath.Hex("#0B0F14");         // the same on the far side of the body
        public static readonly Color Hood = KMath.Hex("#18202B");            // the rain hood (HelmetLooks.RainHood)
        public static readonly Color Cape = KMath.Hex("#202A36");            // the mino's strands
        public static readonly Color CapeDeep = KMath.Hex("#141B24");
        public static readonly Color Lacquer = KMath.Hex("#060607");         // SUB_LACQ: the visor, the tube
        public static readonly Color LacquerLit = KMath.Hex("#2A2A30");
        public static readonly Color Wrap = KMath.Hex("#2B3440");            // the shin wraps and kote
        public static readonly Color WrapLit = KMath.Hex("#3C4756");
        public static readonly Color Accent = KMath.Hex("#7FB8FF");          // her accent: cords, hems, the tube's bands, effect colour
        public static readonly Color AccentDeep = KMath.Hex("#3E6A9E");
        public static readonly Color Pale = KMath.Hex("#9FD4FF");            // her eye, the glint on the fans
        public static readonly Color Face = KMath.Hex("#1A1B20");            // the anime sub-bosses' black face, a cool cast
        public static readonly Color Rim = KMath.Hex("#8FB8DC");             // a cold rain back-light
        public static readonly Color Smoke = KMath.Hex("#7A8494");           // the smoke bomb's smoke
        public static readonly Color SmokeDark = KMath.Hex("#4A5260");
    }
}
