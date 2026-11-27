using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using static Kage.SubBossDrawing;

namespace Kage
{
    public sealed class ShidenPartSet : IPartSet
    {
        public string Id => Shiden.Id;

        public RigDef Build(CharacterDef c)
        {
            var silk = Paint("1C1729","393048","675577");
            var iron = Paint("20212F","373A4C","7E8396");
            var lapel = Paint("241B30","493451","89689B");
            var violet = ShidenPalette.Accent;
            var a = new SubBossDrawing("SHIDEN",1.04f,ShidenPalette.Rim,violet,silk,iron);
            a.Figure();
            a.Face(Ink("AA55FF"),844,175,50,HollowAnimeBody.Profile.Gaunt);
            a.Drape("HaoriTrain",B.Chest,0,909,246,SoftPresets.Cape,
                Path(879,239).QuadTo(925,213,948,255).CubicTo(979,319,942,419,1012,508)
                .QuadTo(1041,549,1082,563).LineTo(1054,563).LineTo(1089,588).LineTo(1050,581)
                .LineTo(1066,601).QuadTo(1021,598,998,565).LineTo(1004,592)
                .CubicTo(944,543,934,464,903,417).QuadTo(886,337,879,239).Bind(),silk,
                Mark(lapel.mid,Path(932,258).CubicTo(961,317,926,467,1010,555).CubicTo(919,486,946,328,932,258).Bind()),
                Stroke(lapel.lit,4,973,510,1006,565,1046,580));
            var plates = new List<(Vector2[],Color)>();
            for(int i=0;i<4;i++)
            {
                float y=268+i*32;
                plates.Add(Mark(iron.shade,Path(806,y).QuadTo(868,y+21,944,y-5).LineTo(941,y+4)
                    .QuadTo(869,y+33,808,y+10).Bind()));
                plates.Add(Stroke(iron.lit,3,810,y+2,867,y+24,939,y-2));
                for(int j=0;j<3;j++) plates.Add(Mark(lapel.lit,Oval(837+j*30,y+10,3,6,-20)));
            }
            a.Piece("Do",B.Chest,Slot.Torso,
                Path(832,222).QuadTo(895,209,931,239).QuadTo(955,264,946,305)
                .QuadTo(935,354,935,405).QuadTo(881,429,815,406)
                .QuadTo(824,364,803,309).QuadTo(793,257,832,222).Bind(),iron,plates.ToArray());
            a.Piece("HaoriLapel",B.Chest,Slot.TorsoTrim,
                Path(847,217).CubicTo(836,260,824,323,841,383).QuadTo(827,417,810,438)
                .QuadTo(821,382,799,325).QuadTo(783,265,816,232).Bind(),lapel,
                Stroke(lapel.lit,4,840,225,810,293,835,377));
            a.Drape("Kusazuri_Front",B.Hips,Slot.FrontPlates,864,408,SoftPresets.Plates,
                Path(811,400).QuadTo(865,419,920,399).QuadTo(927,475,944,516)
                .QuadTo(895,553,820,550).QuadTo(830,494,811,400).Bind(),iron,
                Stroke(iron.lit,6,821,449,877,462,923,437),Stroke(lapel.lit,5,826,483,882,495,931,466),
                Stroke(iron.lit,6,826,519,888,524,938,493));
            a.Piece("OfficerObi",B.Hips,Slot.Belt,Curve(810,383,863,399,931,381,939,407,876,431,809,416),lapel,
                Stroke(violet,4,815,407,870,420,930,397),Mark(iron.lit,Oval(860,413,10,9)));
            a.Piece("Hoate",B.Head,Slot.Face,
                Path(787,189).QuadTo(804,185,818,196).QuadTo(850,199,869,175)
                .LineTo(878,193).QuadTo(862,222,832,232).QuadTo(801,223,787,204).Bind(),iron,
                Stroke(iron.lit,3,801,204,823,221,849,201));
            a.Piece("Kabuto",B.Head,Slot.HeadTop,
                Path(775,158).QuadTo(791,148,798,143).CubicTo(803,90,877,85,904,132)
                .QuadTo(913,148,910,157).QuadTo(924,174,932,199)
                .QuadTo(906,193,888,190).LineTo(898,209).QuadTo(872,205,856,178)
                .LineTo(862,164).QuadTo(834,143,797,158).QuadTo(785,162,775,158).Bind(),iron,
                Mark(iron.lit,Path(809,145).QuadTo(817,110,850,106).QuadTo(828,118,820,144).Bind()),
                Stroke(lapel.lit,5,865,159,896,157,911,171),Stroke(iron.lit,4,878,179,901,173,922,187));
            a.Piece("Maedate",B.Head,Slot.HeadTop+1,
                Path(819,152).QuadTo(794,131,778,79).QuadTo(800,105,812,110)
                .LineTo(804,73).QuadTo(833,106,837,137).QuadTo(832,150,819,152).Bind(),lapel,
                Mark(violet,Path(822,143).LineTo(801,113).LineTo(818,123).LineTo(814,99).QuadTo(831,131,822,143).Bind()));
            a.Piece("SodeN",B.ArmNU,Slot.NearArm+1,
                Path(874,252).QuadTo(902,222,931,243).QuadTo(948,257,958,302)
                .QuadTo(928,297,889,318).QuadTo(873,289,874,252).Bind(),iron,
                Stroke(lapel.lit,5,880,267,912,248,942,261),Stroke(iron.lit,4,885,287,921,267,949,281),
                Mark(violet,Path(905,265).LineTo(897,281).LineTo(905,278).LineTo(901,290).LineTo(918,271).LineTo(909,274).Bind()));
            return a.rig;
        }
    }
}
