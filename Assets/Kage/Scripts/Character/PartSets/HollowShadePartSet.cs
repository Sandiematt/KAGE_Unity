namespace Kage
{
    /// <summary>
    /// The Hollow Shade's look (SAN-76, redrawn from the owner's painting and its SVG trace): a violet-black hooded Hollow with a
    /// crown of three spikes, a round ear disc, a cracked mask with a cold white eye, a long violet-rimmed katana, a sash of hanging
    /// plates at the hip, a saya, torn shoulder strips, coat tails and a torn cape behind.
    /// <para>
    /// Nothing is drawn in code. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/hollow_art/shade/original/</c>) by the
    /// regions of <c>Tools/hollow_art/shade/rig.json</c> into <see cref="Resource"/>; <see cref="SvgRig"/> puts each piece on its bone in
    /// the shared Hollow tones (<see cref="HollowArt"/>). Rigid pieces (head, mask, spikes, ear disc, torso, arms, hands, legs, boots,
    /// saya, sword) ride their bones; the cape, coat tails, shoulder strips and hip plates hang on spring chains (<see cref="SoftPresets"/>)
    /// that swing with the body and bend in the wind (<see cref="FighterRig.wind"/>). The far arm and leg are the near ones in the far
    /// tones. The skeleton, hurt circles and hit points are Kage's (ADR-0005).
    /// </para>
    /// </summary>
    public sealed class HollowShadePartSet : IPartSet
    {
        public const string Resource = "Art/SvgRig/HollowShade";

        public string Id => "hollow_shade";

        public RigDef Build(CharacterDef c)
        {
            var d = OpponentKit.Skeleton("HOLLOW SHADE");
            HollowArt.Dress(d);
            SvgRig.Build(d, SvgRigData.Load(Resource), HollowArt.Style);
            return d;
        }
    }
}
