namespace Kage
{
    /// <summary>
    /// The Hollow Husk's look (SAN-112, redrawn from the owner's SVG "hooded warrior" with the SAN-76 pipeline): a violet-black hooded
    /// brute with two horns on the hood, a horned brow mask over a black face and a cold white eye, a heavy pauldron and a mantle humped
    /// over the shoulders, huge bandaged fists with torn bandage ends, a twisted shimenawa rope belt hung with white paper shide, a skirt
    /// of pointed tassets, wide hakama over armoured boots, and a long torn cloak behind. No blade and no saya: the fists are the weapon.
    /// <para>
    /// Nothing is drawn in code. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/hollow_art/husk/original/</c>) by the
    /// regions of <c>Tools/hollow_art/husk/rig.json</c> into <see cref="Resource"/>; <see cref="SvgRig"/> puts each piece on its bone in
    /// the shared Hollow tones (<see cref="HollowArt"/>). Rigid pieces (hood, horns, mask, face, eye, torso, pauldron, arms, fists, belt,
    /// legs, boots) ride their bones; the cloak, mantle, tassets, shide and bandage ends hang on spring chains (<see cref="SoftPresets"/>)
    /// that swing with the body and bend in the wind (<see cref="FighterRig.wind"/>). The far arm and leg are the near ones in the far
    /// tones. The skeleton, hurt circles, hit points, scale and mass are unchanged (<see cref="HollowHusk"/>, ADR-0005).
    /// </para>
    /// </summary>
    public sealed class HollowHuskPartSet : IPartSet
    {
        public const string Resource = "Art/SvgRig/HollowHusk";

        public string Id => "hollow_husk";

        public RigDef Build(CharacterDef c)
        {
            var d = OpponentKit.Skeleton("HOLLOW HUSK");
            HollowArt.Dress(d);
            SvgRig.Build(d, SvgRigData.Load(Resource), HollowArt.Style);
            return d;
        }
    }
}
