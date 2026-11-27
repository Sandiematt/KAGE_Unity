using UnityEngine;
using static Kage.OpponentKit;
using static Kage.SubBossDrawing;

namespace Kage
{
    public sealed class KuroePartSet : IPartSet
    {
        public string Id => Kuroe.Id;

        public RigDef Build(CharacterDef c)
        {
            var robe = Paint("1C1624","37313E","696071");
            var plum = Paint("291826","613446","AF6973");
            var hair = Paint("12131E","272836","676076");
            var paper = Paint("928373","D4BE9E","F4E3BA");
            var gold = KuroePalette.Gold;
            var a = new SubBossDrawing("KUROE",0.86f,KuroePalette.Rim,gold,robe,
                Paint("211E29","453A49","746271"));
            a.Figure();
            a.Face(Ink("E2D6FF"),845,172,47,HollowAnimeBody.Profile.Gaunt);
            a.Drape("Ponytail",B.Head,0,886,113,SoftPresets.Hair,
                Path(872,126).CubicTo(884,63,934,65,963,108).CubicTo(996,158,1007,193,1074,158)
                .QuadTo(1054,197,1008,189).QuadTo(1041,215,1072,200).CubicTo(1017,256,965,192,950,157)
                .QuadTo(937,186,953,199).CubicTo(920,190,932,108,898,124).LineTo(883,142).Bind(),hair,
                Mark(hair.lit,Path(890,105).CubicTo(947,63,944,163,1002,181).CubicTo(945,173,943,83,890,105).Bind()));
            a.Piece("Hair",B.Head,Slot.HeadTop,
                Path(792,171).CubicTo(790,119,824,99,865,112).QuadTo(906,115,905,153)
                .QuadTo(907,189,880,211).QuadTo(882,186,875,178).QuadTo(866,207,851,217)
                .QuadTo(862,184,850,156).QuadTo(827,170,812,177).LineTo(823,154).QuadTo(806,170,792,171).Bind(),hair,
                Mark(hair.lit,Path(801,151).QuadTo(827,109,868,121).QuadTo(833,120,801,151).Bind()),
                Mark(plum.lit,Oval(885,116,10,16,-25)));
            a.Piece("Kosode",B.Chest,Slot.Torso,
                Path(847,218).QuadTo(878,210,919,242).QuadTo(946,258,941,295)
                .CubicTo(939,332,913,362,917,411).QuadTo(870,434,818,408)
                .QuadTo(833,365,811,321).QuadTo(798,294,814,260).Bind(),robe,
                Mark(robe.shade,Path(897,245).QuadTo(881,299,856,331).QuadTo(891,302,884,344).LineTo(911,317).Bind()),
                Mark(paper.mid,Path(840,224).QuadTo(847,262,871,289).LineTo(861,304).QuadTo(831,268,828,237).Bind()),
                Mark(plum.mid,Path(898,225).LineTo(921,242).QuadTo(878,326,826,387).LineTo(819,365).QuadTo(866,303,898,225).Bind()),
                Stroke(gold,3,905,237,884,289,827,369));
            a.Drape("SplitRobe",B.Hips,Slot.FrontPlates,865,408,SoftPresets.Tails,
                Path(823,394).QuadTo(870,405,912,392).CubicTo(914,475,905,531,958,595)
                .LineTo(922,577).LineTo(931,598).LineTo(899,570).LineTo(901,585).LineTo(878,552).QuadTo(864,585,827,630).CubicTo(850,528,816,488,823,394).Bind(),robe,
                Mark(robe.lit,Path(845,422).QuadTo(864,507,846,565).QuadTo(870,514,859,461).Bind()),
                Stroke(plum.lit,5,831,611,869,561,878,540));
            a.Drape("InkSash",B.Hips,0,900,402,SoftPresets.Strip,
                Path(895,398).CubicTo(962,373,1006,434,1063,427).QuadTo(1113,423,1147,386)
                .CubicTo(1119,453,1072,477,1014,453).QuadTo(958,420,900,425).Bind(),paper,
                Mark(robe.shade,Path(1022,431).QuadTo(1061,452,1101,425).QuadTo(1080,450,1070,449)
                .LineTo(1074,439).QuadTo(1053,458,1022,431).Bind()),
                Stroke(gold,3,921,400,969,391,1001,421));
            a.Drape("BackSash",B.Hips,0,903,413,SoftPresets.Tails,
                Path(897,407).QuadTo(969,406,983,459).CubicTo(1006,524,1057,511,1103,501)
                .QuadTo(1038,561,989,508).QuadTo(944,435,897,431).Bind(),plum);
            a.Piece("Obi",B.Hips,Slot.Belt,Curve(821,386,864,398,919,385,925,406,905,426,849,429,815,412),plum,
                Stroke(gold,5,824,406,874,421,916,399),Mark(gold,Oval(898,407,9,10)));
            a.Piece("BrushSleeve",B.ArmNU,Slot.NearArm+1,
                Path(889,247).QuadTo(923,231,940,265).QuadTo(944,299,959,328)
                .QuadTo(941,345,908,347).QuadTo(882,325,878,287).QuadTo(878,257,889,247).Bind(),robe,
                Stroke(paper.mid,4,907,337,933,337,951,325),
                Mark(gold,Oval(918,279,8,9)),Mark(robe.mid,Oval(918,279,4,5)));
            return a.rig;
        }
    }
}
