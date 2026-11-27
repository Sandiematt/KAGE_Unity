namespace Kage
{
    /// <summary>
    /// The Hollow Shade's look (SAN-76, redrawn from the owner's painting and its SVG trace): a violet-black hooded Hollow with a
    /// crown of three spikes, a round ear disc, a cracked mask with a cold white eye, a long violet-rimmed katana, a sash of hanging
    /// plates at the hip, a saya, torn shoulder strips, coat tails and a torn cape behind.
    /// <para>
    /// A narrow wrap coat, split cape and curved katana are painted over the articulated base. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/hollow_art/shade/original/</c>) by the
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
            var palette = HollowArt.Style;
            // The three silk-bound mask splinters fan away from the hood. Their roots
            // use the painted pivots so the crown still flexes over a sudden turn.
            var crown = new OpponentKit.Frame(new UnityEngine.Vector2(852, 168),
                UnityEngine.Vector2.right * .86f, UnityEngine.Vector2.up * .86f);
            void Splinter(string name, float x, float y, float dx, float dy)
            {
                var p = new EliteCel(d, name, palette, crown);
                p.Ground("Ink", p.Path.MoveTo(x - 8, y + 3)
                    .QuadTo(x + dx * .75f, y + dy * .48f, x + dx, y + dy)
                    .QuadTo(x + dx * .28f, y + dy * .79f, x + 8, y - 2)
                    .QuadTo(x + 6, y + 10, x - 8, y + 3));
                p.Fill("RimBright", p.Path.MoveTo(x - 4, y - 3)
                    .QuadTo(x + dx * .62f, y + dy * .45f, x + dx * .92f, y + dy * .88f)
                    .QuadTo(x + dx * .43f, y + dy * .56f, x, y - 6));
            }
            Splinter("Spike_1", -37, -31, -61, -48);
            Splinter("Spike_2", -21, -53, -22, -78);
            Splinter("Spike_3", 19, -55, 45, -77);
            EliteCel.Sleeves(d, palette, 24, "ClothMid", "ClothLit");
            var cape = new EliteCel(d, "Cape", palette, OpponentKit.Frame.Ref);
            cape.Hang(new float[] { 862, 225, 908, 316, 937, 443, 969, 565, 1024, 682 },
                      new float[] { 881, 232, 960, 318, 1003, 463, 1066, 580, 1160, 667 },
                      new float[] { 871, 229, 934, 326, 971, 464, 1026, 586, 1129, 716 });
            cape.Ground("Ink", cape.Path.MoveTo(852, 218).CubicTo(948, 213, 984, 295, 1001, 383)
                .CubicTo(1022, 493, 1081, 598, 1172, 673).QuadTo(1110, 659, 1076, 635)
                .QuadTo(1096, 689, 1136, 723).QuadTo(1062, 704, 1023, 671)
                .LineTo(1028, 696).CubicTo(951, 641, 926, 553, 918, 457).QuadTo(909, 318, 852, 218));
            cape.Fill("ClothMid", cape.Path.MoveTo(873, 229).CubicTo(956, 229, 982, 326, 999, 405)
                .QuadTo(1040, 560, 1149, 658).CubicTo(1040, 606, 994, 510, 965, 427).QuadTo(944, 317, 873, 229));
            cape.Fill("VioletDeep", cape.Path.MoveTo(891, 237).QuadTo(964, 248, 986, 369)
                .QuadTo(959, 292, 930, 272).QuadTo(914, 253, 891, 237));
            cape.Fill("Cloth", cape.Path.MoveTo(936, 386).CubicTo(969, 545, 1041, 637, 1115, 708)
                .CubicTo(1003, 658, 948, 548, 936, 386));
            var chest = new EliteCel(d, "Torso", palette, OpponentKit.Frame.Ref);
            chest.Ground("Ink", chest.Path.MoveTo(846, 216).QuadTo(913, 205, 938, 254)
                .CubicTo(952, 298, 924, 365, 929, 403).QuadTo(884, 424, 835, 407)
                .QuadTo(844, 358, 819, 303).QuadTo(799, 254, 846, 216));
            chest.Fill("ClothMid", chest.Path.MoveTo(844, 226).QuadTo(901, 224, 926, 256)
                .QuadTo(875, 264, 837, 330).QuadTo(799, 257, 844, 226));
            chest.Fill("ClothLit", chest.Path.MoveTo(822, 283).QuadTo(853, 248, 901, 239)
                .QuadTo(855, 261, 826, 306));
            chest.Fill("Cloth", chest.Path.MoveTo(842, 353).QuadTo(878, 299, 934, 274)
                .QuadTo(891, 339, 918, 400).QuadTo(879, 417, 843, 399));
            EliteCel.Hakama(d, palette, 36, "Cloth", "ClothLit");
            var katana = new EliteCel(d, "Blade", palette, EliteCel.BladeFrame(d, d.bladeTipLocal));
            katana.Ground("Ink", katana.Path.MoveTo(.095f, -7).QuadTo(.63f, -10, 1, 0)
                .QuadTo(.86f, 13, .1f, 11));
            katana.Fill("Steel", katana.Path.MoveTo(.105f, -4).QuadTo(.69f, -7, .992f, 0)
                .QuadTo(.81f, 10, .105f, 8));
            katana.Fill("RimBright", katana.Path.MoveTo(.105f, -3).QuadTo(.62f, -7, .91f, -1)
                .QuadTo(.56f, -3, .105f, 1));
            katana.Fill("White", katana.Path.MoveTo(.105f, 6).QuadTo(.83f, 8, .992f, 0)
                .QuadTo(.84f, 12, .105f, 9));
            EliteCel.Waist(d, palette, 45, "Cloth");
            EliteCel.Grip(d, "Hilt", palette, EliteCel.BladeFrame(d, d.bladeTipLocal), -.26f, .095f, "VioletDeep", "SteelDark");
            EliteCel.LineGarments(d, palette, "Head", "Torso", 54);
            return d;
        }
    }
}
