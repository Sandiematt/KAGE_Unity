namespace Kage
{
    /// <summary>
    /// Raikon's look (SAN-71), cut from the owner's SVG (Resources/Art/Raikon/Raikon.svg.bytes, a byte copy of
    /// Tools/raikon_art/original/Raikon.svg) by <see cref="SvgArt"/> along the regions in <see cref="RaikonRegions"/>
    /// (written by Tools/raikon_art/regions.py). Every part keeps the painting's own colours and shapes; it is moved from
    /// the painting onto the shared skeleton (Kage's bind pose, scaled by the character's body scale at spawn):
    /// the trunk by one fit (his ground and hips on Kage's), each limb segment so its drawn joints land on the bones'
    /// pivots. Art never moves a gameplay joint: pivots, parents, hurt circles and hit points are Kage's.
    /// <para>
    /// The painting holds the tachi in the near hand; on the shared skeleton the sword is in the far hand, so the far arm
    /// is the sword arm drawn from the painted near arm (sleeve, gauntlet and gripping hand, a shade darker), the near arm
    /// keeps the shoulder plate and gets the painted far fist. The far leg is the near leg a shade darker. What the
    /// painting hides behind moving parts (the torso behind the arms, the legs under the skirt, the hilt under the hand)
    /// is filled in the painting's own colours.
    /// </para>
    /// </summary>
    public sealed class RaikonPartSet : IPartSet
    {
        public string Id => "raikon";

        public RigDef Build(CharacterDef c) => RaikonModel.Build();
    }
}
