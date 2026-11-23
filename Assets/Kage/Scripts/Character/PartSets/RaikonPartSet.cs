namespace Kage
{
    /// <summary>
    /// Raikon's look (SAN-71, redesigned on the SAN-76 SVG rig pipeline): the Act I boss, a samurai under a wide red-veined jingasa
    /// hung with three red tassels and a white ponytail tied with red ribbons, a black face with one glowing red eye, a white fur
    /// mantle, a sode with the tomoe, black lacquered armour laced in crimson with a white chest tassel, a white rope and red braided
    /// belt, a front apron of red-edged panels with a red cord tassel, a haidate with the cloud pattern, back coat panels, black
    /// boots, a torn black-and-crimson cape, and his glowing red tachi.
    /// <para>
    /// Nothing is drawn in code. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/raikon_art/original/Raikon.svg</c>,
    /// its silhouette and the hair, fur and face tones from <c>Raikon.png</c>) by the regions of <c>Tools/raikon_art/rig.json</c> into
    /// <see cref="Resource"/>; <see cref="SvgRig"/> puts each piece on its bone in the trace's own colours (<see cref="RaikonArt"/>).
    /// Rigid pieces (hat, head, eye, torso, sode, arms, hands, rope belt, skirt, legs, boots, the tachi) ride their bones; the cape,
    /// ponytail, ribbons, hat tassels, fur, chest tassel, apron, cord tassel and back coat panels hang on spring chains
    /// (<see cref="SoftPresets"/>) that swing with the body and bend in the wind (<see cref="FighterRig.wind"/>).
    /// </para>
    /// <para>
    /// The painting holds the tachi in the near hand; on the shared skeleton the sword hangs from the far hand. So the far arm is the
    /// sword arm: the painted near sleeve, sode and gauntlet one step darker, closing the painted far fist (the free hand in the
    /// painting) on the hilt; the near arm keeps the painted sode, sleeve, gauntlet and the painted hand that grips the tachi, which
    /// closes on the handle behind the far hand in the two-handed stances. The far leg is the near leg one step darker (the painting
    /// hides most of the far boot behind the near one). The tachi is cut from the painting onto the Sword bone at the trace's scale
    /// with its grip on the bone's pivot, as SAN-71 placed it, so the hit line ends at the painted tip (<see cref="PaintedBladeLength"/>).
    /// The skeleton, hurt circles, hit points, body scale and stats are unchanged (<see cref="Raikon"/>, ADR-0005).
    /// </para>
    /// </summary>
    public sealed class RaikonPartSet : IPartSet
    {
        public const string Resource = "Art/SvgRig/Raikon";

        /// <summary>
        /// The painted tachi's blade, guard to tip, in Sword-bone units (SAN-107): Raikon's <see cref="CharacterDef.bladeLength"/>, so his
        /// hit line ends where his painted blade does. Measured on SAN-71's region cut (the blade's furthest point along its axis from the
        /// grip, less the 0.08 guard) and kept as that number so his reach does not move by a hair with the redrawn outline; the SVG rig
        /// places the blade at the same grip, angle and scale, and an EditMode test checks the drawn tip still lies on the hit line's end.
        /// </summary>
        public const float PaintedBladeLength = 0.87721705f;

        public string Id => "raikon";

        public RigDef Build(CharacterDef c)
        {
            var d = KageModel.Skeleton("RAIKON");
            RaikonArt.Dress(d);
            SvgRig.Build(d, SvgRigData.Load(Resource), RaikonArt.Style);
            return d;
        }
    }
}
