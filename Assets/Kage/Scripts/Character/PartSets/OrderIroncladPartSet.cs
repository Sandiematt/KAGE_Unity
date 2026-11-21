namespace Kage
{
    /// <summary>
    /// The Order Ironclad's look (SAN-100, redrawn from the owner's painting and its VTracer trace with the SAN-76 pipeline, the first
    /// Order foe drawn this way): a black-lacquered samurai with two big black ox horns on a kabuto with a crimson crest disc and brim
    /// band, a black face with a white eye, a laced shikoro and throat guard, big laced sode, a dō laced in crimson, a crimson rope belt
    /// with a hanging rope and tassel, laced kusazuri over a dark hakama, black boots with crimson tops, a wakizashi at the belt, and the
    /// painting's heavy cleaver with a crimson line along the blade.
    /// <para>
    /// Nothing is drawn in code. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/order_art/ironclad/original/</c>) by the
    /// regions of <c>Tools/order_art/ironclad/rig.json</c> into <see cref="Resource"/>; <see cref="SvgRig"/> puts each piece on its bone in
    /// the shared Order tones (<see cref="OrderArt"/>). Rigid pieces (kabuto, horns, face, eye, shikoro, throat guard, dō, sode, arms,
    /// hands, belt, wakizashi, legs, boots) ride their bones; the kusazuri (lacquered plates, stiff), the rope's tassel and the hakama hang
    /// on spring chains (<see cref="SoftPresets"/>) that swing with the body and bend in the wind (<see cref="FighterRig.wind"/>). The far
    /// arm and leg are the near ones in the far tones. The cleaver is cut from the painting onto the Sword bone with its point on the
    /// cleaver family's hit line, and <see cref="CleaverVisual"/> leaves it alone. The skeleton, hurt circles, hit line, scale, mass,
    /// armour and stats are unchanged (<see cref="OrderIronclad"/>, ADR-0005).
    /// </para>
    /// </summary>
    public sealed class OrderIroncladPartSet : IPartSet
    {
        public const string Resource = "Art/SvgRig/OrderIronclad";

        public string Id => "order_ironclad";

        public RigDef Build(CharacterDef c)
        {
            var d = OpponentKit.Skeleton("ORDER IRONCLAD");
            OrderArt.Dress(d);
            SvgRig.Build(d, SvgRigData.Load(Resource), OrderArt.Style);
            return d;
        }
    }
}
