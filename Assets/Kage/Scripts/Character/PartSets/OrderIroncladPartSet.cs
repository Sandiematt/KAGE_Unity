namespace Kage
{
    /// <summary>
    /// The Order Ironclad's look (SAN-100, redrawn from the owner's painting and its VTracer trace with the SAN-76 pipeline, the first
    /// Order foe drawn this way): a black-lacquered samurai with two big black ox horns on a kabuto with a crimson crest disc and brim
    /// band, a black face with a white eye, a laced shikoro and throat guard, big laced sode, a dō laced in crimson, a crimson rope belt
    /// with a hanging rope and tassel, laced kusazuri over a dark hakama, black boots with crimson tops, a wakizashi at the belt, and the
    /// painting's heavy cleaver with a crimson line along the blade.
    /// <para>
    /// A bowed cuirass and a deep-bellied cleaver distinguish the heavy swordsman. <c>Tools/svg_art/svg_rig.py</c> cuts the owner's trace (<c>Tools/order_art/ironclad/original/</c>) by the
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
            var palette = OrderArt.Style;
            EliteCel.Sleeves(d, palette, 49, "LacquerMid", "Edge");
            var plate = new EliteCel(d, "Do", palette, OpponentKit.Frame.Ref);
            plate.Ground("Ink", plate.Path.MoveTo(817, 217).QuadTo(876, 236, 939, 214)
                .CubicTo(982, 234, 1001, 293, 985, 338).QuadTo(963, 391, 959, 422)
                .QuadTo(884, 460, 793, 423).QuadTo(796, 380, 777, 337).QuadTo(755, 272, 817, 217));
            plate.Fill("LacquerLit", plate.Path.MoveTo(815, 229).QuadTo(868, 247, 935, 226)
                .QuadTo(977, 247, 981, 295).QuadTo(931, 278, 877, 304)
                .QuadTo(820, 330, 781, 313).QuadTo(774, 262, 815, 229));
            plate.Fill("Edge", plate.Path.MoveTo(787, 291).QuadTo(791, 257, 817, 237)
                .QuadTo(866, 251, 899, 243).QuadTo(856, 260, 820, 246).QuadTo(797, 266, 787, 291));
            // Three overlapping breast lames; paired silk stitches follow the bow of each row.
            for (int i = 0; i < 3; i++)
            {
                float y = 306 + i * 40;
                float x = 782 + i * 6;
                plate.Fill("LacquerMid", plate.Path.MoveTo(x, y).QuadTo(851, y + 23, 972 - 5 * i, y - 13)
                    .QuadTo(964 - 5 * i, y + 4, 962 - 5 * i, y + 25)
                    .QuadTo(865, y + 60, x + 8, y + 33).QuadTo(x + 5, y + 18, x, y));
                for (int j = 0; j < 4; j++)
                {
                    float u = x + 25 + j * 35, v = y + 10 - j * (j - 1);
                    plate.Fill("Crimson", plate.Path.MoveTo(u, v).QuadTo(u + 5, v - 3, u + 7, v)
                        .LineTo(u + 3, v + 13).QuadTo(u, v + 16, u - 2, v + 12));
                }
            }
            plate.Fill("EdgeLit", plate.Path.MoveTo(803, 276).QuadTo(820, 271, 845, 251)
                .LineTo(827, 270).LineTo(832, 268).QuadTo(818, 280, 803, 283));
            EliteCel.Hakama(d, palette, 78, "LacquerMid", "LacquerLit");
            float reach = CleaverMoves.BladeLength * WeaponStats.Combat(c.weaponId, 1).reach;
            var tip = d.bladeBaseLocal + (d.bladeTipLocal - d.bladeBaseLocal).normalized * reach;
            var steel = new EliteCel(d, "Cleaver_Blade", palette, EliteCel.BladeFrame(d, tip));
            steel.Ground("Ink", steel.Path.MoveTo(.15f, -14).QuadTo(.48f, -18, .77f, -48)
                .QuadTo(.88f, -51, .94f, -42).QuadTo(.966f, -15, 1, 0)
                .CubicTo(.88f, 51, .61f, 57, .32f, 32).QuadTo(.22f, 19, .15f, 17));
            steel.Fill("Steel", steel.Path.MoveTo(.16f, -9).QuadTo(.47f, -11, .79f, -43)
                .QuadTo(.88f, -46, .93f, -38).QuadTo(.95f, -15, .991f, 0)
                .CubicTo(.83f, 39, .6f, 49, .33f, 26).LineTo(.16f, 13));
            steel.Fill("Edge", steel.Path.MoveTo(.16f, -9).QuadTo(.49f, -11, .79f, -43)
                .QuadTo(.85f, -46, .92f, -39).QuadTo(.72f, -2, .48f, 3).QuadTo(.29f, 9, .16f, 4));
            steel.Fill("Crimson", steel.Path.MoveTo(.19f, -5).QuadTo(.54f, -10, .82f, -36)
                .QuadTo(.55f, -5, .19f, -1));
            steel.Fill("White", steel.Path.MoveTo(.34f, 24).QuadTo(.75f, 52, .99f, 0)
                .QuadTo(.79f, 62, .34f, 30));
            EliteCel.Waist(d, palette, 72, "LacquerMid");
            EliteCel.Grip(d, "Cleaver_Handle", palette, EliteCel.BladeFrame(d, tip), -.46f, .15f, "CrimsonDeep", "Edge");
            EliteCel.LineGarments(d, palette, "Throat", "Do", 80);
            return d;
        }
    }
}
