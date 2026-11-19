using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The colours of one JS stage environment (SAN-103), the data <see cref="EnvBackdrop"/> paints a boss arena placeholder from. The ids are
    /// the JS <c>env</c> ids of the campaign stages (<c>JsStage.Env</c>); the colours are the JS <c>ENVS</c> in <c>50-world.js</c>. Art, not
    /// gameplay: nothing here is a JS number the game rules read. An env with no entry of its own paints as the rooftops.
    /// </summary>
    public sealed class EnvLook
    {
        public string id, name;
        /// <summary>The sky's gradient, top, middle and horizon (JS sky layer stops 0, 0.62, 1).</summary>
        public Color skyTop, skyMid, skyLow;
        public Color star, moon, moonHalo, cloud;
        /// <summary>The far city: building blocks and pagoda roofs, and the gold of their few lit windows.</summary>
        public Color far, farWindow;
        /// <summary>The haze band between the far city and the near roofs.</summary>
        public Color fog;
        /// <summary>The string of paper lanterns over the arena: the rope, the lantern and its glow.</summary>
        public Color rope, lantern, lanternGlow;
        /// <summary>The near roof ridges behind the fighters and the faint lines of their tiles.</summary>
        public Color roofs, roofLines;
        /// <summary>The roof the fight is on: top and bottom of its gradient, the tile seams and the lit edge.</summary>
        public Color floorTop, floorBottom, floorLines, floorEdge;
        /// <summary>A wash over the whole backdrop (alpha 0 = none): the districts' cold slate.</summary>
        public Color wash;

        /// <summary>
        /// The Forgotten Rooftops (JS <c>ENVS.rooftops</c>): a black-violet night, a pale bone moon, a far city of blocks and pagoda roofs with a
        /// few gold windows, a string of red paper lanterns, near tiled roof ridges, and the fight on a dark tiled roof. Kuroe's arena (act1.sub1).
        /// </summary>
        public static readonly EnvLook Rooftops = new()
        {
            id = "rooftops", name = "The Forgotten Rooftops",
            skyTop = KMath.Hex("#070709"), skyMid = KMath.Hex("#17161D"), skyLow = KMath.Hex("#0B0A0C"),
            star = KMath.Hex("#ECE5D3").WithA(0.45f), moon = KMath.Hex("#D9D2BF"), moonHalo = KMath.Hex("#D9D2BF").WithA(0.28f), cloud = KMath.Hex("#14131A").WithA(0.75f),
            far = KMath.Hex("#16151B"), farWindow = KMath.Hex("#C9A45C").WithA(0.6f),
            fog = KMath.Hex("#282630").WithA(0.35f),
            rope = KMath.Hex("#3A342B").WithA(0.8f), lantern = KMath.Hex("#B8392D"), lanternGlow = KMath.Hex("#D24A3C").WithA(0.35f),
            roofs = KMath.Hex("#0D0C10"), roofLines = KMath.Hex("#A09785").WithA(0.08f),
            floorTop = KMath.Hex("#141217"), floorBottom = KMath.Hex("#060507"), floorLines = KMath.Hex("#3A342B").WithA(0.5f), floorEdge = KMath.Hex("#3A342B"),
            wash = Color.clear,
        };

        /// <summary>
        /// The Outer Districts (JS <c>ENVS.districts</c>): the rooftops' city under a cold slate wash with a dimmer moon (Garan, Hisame and
        /// Shiden's arenas). The JS rain and wet road are not painted by the placeholder.
        /// </summary>
        public static readonly EnvLook Districts = new()
        {
            id = "districts", name = "The Outer Districts",
            skyTop = Rooftops.skyTop, skyMid = Rooftops.skyMid, skyLow = Rooftops.skyLow,
            star = Rooftops.star, moon = KMath.Hex("#9AA6B0"), moonHalo = KMath.Hex("#9AA6B0").WithA(0.24f), cloud = Rooftops.cloud,
            far = Rooftops.far, farWindow = Rooftops.farWindow, fog = KMath.Hex("#465462").WithA(0.32f),
            rope = Rooftops.rope, lantern = Rooftops.lantern, lanternGlow = Rooftops.lanternGlow,
            roofs = Rooftops.roofs, roofLines = Rooftops.roofLines,
            floorTop = Rooftops.floorTop, floorBottom = Rooftops.floorBottom, floorLines = Rooftops.floorLines, floorEdge = Rooftops.floorEdge,
            wash = KMath.Hex("#344252").WithA(0.28f),
        };

        /// <summary>The look of a JS env id; the rooftops for an env with no look of its own.</summary>
        public static EnvLook For(string env) => env switch
        {
            "districts" => Districts,
            _ => Rooftops,
        };
    }
}
