namespace Kage
{
    /// <summary>
    /// The Hollow Husk's look (SAN-112, redrawn from the owner's SVG "hooded warrior" with the SAN-76 pipeline): a violet-black hooded
    /// brute with two horns on the hood, a horned brow mask over a black face and a cold white eye, a heavy pauldron and a mantle humped
    /// over the shoulders, huge bandaged fists with torn bandage ends, a twisted shimenawa rope belt hung with white paper shide, a skirt
    /// of pointed tassets, wide hakama over armoured boots, and a long torn cloak behind. No blade and no saya: the fists are the weapon.
    /// <para>
    /// A broad mantle, wrapped torso and gathered hakama give the brawler his weight. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/hollow_art/husk/original/</c>) by the
    /// regions of <c>Tools/hollow_art/husk/rig.json</c> into <see cref="Resource"/>; <see cref="SvgRig"/> puts each piece on its bone in
    /// the Hollow tone names painted in the SVG's own colours (<see cref="HollowArt.Husk"/>, SAN-112 definition pass). Rigid pieces (hood,
    /// horns, mask, face, eye, torso, pauldron, arms, fists, belt, legs, boots) ride their bones; the cloak, mantle, tassets, shide and
    /// bandage ends hang on spring chains (<see cref="SoftPresets"/>) that swing with the body and bend in the wind
    /// (<see cref="FighterRig.wind"/>). The far arm and thigh are the near ones in the far tones; the far boot is the painted far boot.
    /// The skeleton, hurt circles, hit points, scale and mass are unchanged (<see cref="HollowHusk"/>, ADR-0005).
    /// </para>
    /// </summary>
    public sealed class HollowHuskPartSet : IPartSet
    {
        public const string Resource = "Art/SvgRig/HollowHusk";

        public string Id => "hollow_husk";

        public RigDef Build(CharacterDef c)
        {
            var d = OpponentKit.Skeleton("HOLLOW HUSK");
            HollowArt.Husk.Dress(d);
            SvgRig.Build(d, SvgRigData.Load(Resource), HollowArt.Husk);
            var palette = HollowArt.Husk;
            EliteCel.Sleeves(d, palette, 63, "ClothMid", "VioletDeep");
            var chest = new EliteCel(d, "Torso", palette, OpponentKit.Frame.Ref);
            chest.Ground("Ink", chest.Path.MoveTo(825, 210).CubicTo(884, 185, 972, 210, 987, 275)
                .QuadTo(997, 325, 957, 388).QuadTo(939, 421, 948, 455)
                .QuadTo(886, 487, 820, 447).QuadTo(827, 404, 795, 352).CubicTo(757, 287, 768, 238, 825, 210));
            chest.Fill("ClothMid", chest.Path.MoveTo(810, 241).QuadTo(883, 194, 956, 245)
                .QuadTo(901, 244, 879, 293).QuadTo(849, 337, 801, 329).QuadTo(779, 282, 810, 241));
            chest.Fill("Cloth", chest.Path.MoveTo(802, 340).QuadTo(854, 350, 902, 291)
                .QuadTo(888, 365, 938, 393).LineTo(933, 441).QuadTo(879, 461, 827, 434).QuadTo(830, 386, 802, 340));
            var mantle = new EliteCel(d, "Mantle", palette, OpponentKit.Frame.Ref);
            mantle.Hang(new float[] { 809, 222, 826, 243, 842, 273 },
                        new float[] { 856, 207, 894, 233, 938, 284 },
                        new float[] { 896, 204, 956, 230, 992, 274, 1022, 306 });
            mantle.Ground("Ink", mantle.Path.MoveTo(796, 231).CubicTo(819, 180, 928, 186, 966, 217)
                .CubicTo(995, 240, 1009, 283, 1033, 314).QuadTo(1007, 309, 986, 293)
                .QuadTo(993, 316, 1001, 329).QuadTo(958, 316, 939, 293)
                .QuadTo(881, 315, 805, 273).QuadTo(782, 252, 796, 231));
            mantle.Fill("ClothLit", mantle.Path.MoveTo(799, 232).QuadTo(853, 173, 945, 210)
                .QuadTo(988, 228, 1018, 299).QuadTo(982, 258, 941, 249)
                .QuadTo(861, 216, 799, 250));
            mantle.Fill("VioletDeep", mantle.Path.MoveTo(808, 253).QuadTo(862, 217, 932, 251)
                .QuadTo(882, 239, 849, 260).QuadTo(831, 269, 808, 253));
            EliteCel.Hakama(d, palette, 72, "ClothMid", "ClothLit");
            foreach (bool far in new[] { false, true })
            {
                var wrist = d.pivot[(int)(far ? B.HandF : B.HandN)];
                var frame = new OpponentKit.Frame(new UnityEngine.Vector2(KageModel.OriginXRef - wrist.x * KageModel.RefScale,
                    KageModel.OriginYRef - wrist.y * KageModel.RefScale), UnityEngine.Vector2.right * 1.2f, UnityEngine.Vector2.up);
                var fist = new EliteCel(d, far ? "HandF" : "HandN", palette, frame, far);
                fist.Ground("Ink", fist.Path.MoveTo(-24, -13).QuadTo(-7, -27, 21, -17)
                    .QuadTo(42, -11, 43, 10).QuadTo(49, 22, 42, 33)
                    .QuadTo(44, 46, 31, 50).QuadTo(21, 63, 8, 56)
                    .QuadTo(-6, 62, -18, 50).QuadTo(-33, 53, -43, 36)
                    .QuadTo(-57, 15, -43, 2).QuadTo(-38, -9, -24, -13));
                fist.Fill("ClothLit", fist.Path.MoveTo(-24, -9).QuadTo(7, -24, 28, -9)
                    .QuadTo(39, -1, 36, 11).QuadTo(1, 2, -35, 23).QuadTo(-49, 10, -24, -9));
                fist.Fill("SteelDark", fist.Path.MoveTo(-36, 21).QuadTo(-4, -1, 35, 11)
                    .QuadTo(38, 16, 37, 21).QuadTo(3, 11, -31, 33));
                fist.Fill("ClothMid", fist.Path.MoveTo(-28, 36).QuadTo(0, 16, 36, 24)
                    .QuadTo(38, 39, 28, 44).QuadTo(15, 54, 7, 48)
                    .QuadTo(-6, 55, -17, 43).QuadTo(-22, 46, -28, 36));
            }
            EliteCel.Waist(d, palette, 68, "ClothMid");
            return d;
        }
    }
}
