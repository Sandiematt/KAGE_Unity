using UnityEngine;
using static Kage.OpponentKit;
using static Kage.SubBossDrawing;

namespace Kage
{
    public sealed class HisamePartSet : IPartSet
    {
        public string Id => Hisame.Id;

        public RigDef Build(CharacterDef c)
        {
            var silk = Paint("152630","284751","54838C");
            var mantle = Paint("111F2C","274654","6599A5");
            var silver = Paint("273840","517380","A7C9CD");
            var frost = Ink("B0EBE0");
            var a = new SubBossDrawing("HISAME",0.78f,HisamePalette.Rim,frost,silk,silver);
            a.Figure();
            a.Face(Ink("69EFFF"),845,172,46,HollowAnimeBody.Profile.Gaunt);
            a.Piece("RainJacket",B.Chest,Slot.Torso,
                Path(837,224).QuadTo(878,215,909,239).QuadTo(933,262,923,304)
                .QuadTo(902,357,914,404).QuadTo(867,428,824,404)
                .QuadTo(843,371,820,325).QuadTo(797,295,813,263).Bind(),silk,
                Mark(silk.shade,Path(895,257).QuadTo(886,299,862,319).QuadTo(891,313,878,351)
                    .QuadTo(903,321,908,283).Bind()),
                Stroke(frost,4,834,238,862,279,859,300),
                Stroke(mantle.lit,6,858,303,854,336,825,389));
            a.Piece("CourierWrap",B.Hips,Slot.FrontPlates,
                Path(821,390).QuadTo(873,410,914,386).CubicTo(917,421,933,469,964,505)
                .QuadTo(928,502,894,473).QuadTo(850,495,807,538).QuadTo(824,473,821,390).Bind(),mantle,
                Stroke(frost,4,817,516,847,482,879,470));
            a.Piece("Tube",B.Hips,Slot.BeltTrim,Capsule(878,427,933,384,11,11),silver,
                Stroke(mantle.lit,4,882,421,901,402,924,388));
            a.Drape("RainScarf",B.Neck,0,873,234,SoftPresets.Strip,
                Path(861,226).CubicTo(920,210,946,249,998,236).CubicTo(1045,221,1070,176,1146,188)
                .QuadTo(1107,194,1079,224).QuadTo(1103,215,1122,214)
                .CubicTo(1070,236,1039,271,988,269).CubicTo(942,270,911,245,865,250).Bind(),mantle,
                Mark(frost,Path(952,244).CubicTo(1023,267,1054,210,1103,201)
                    .CubicTo(1056,223,1020,277,952,244).Bind()));
            a.Drape("CourierRibbon",B.Neck,0,878,240,SoftPresets.Tails,
                Path(873,233).CubicTo(947,255,976,302,1044,271).QuadTo(1083,254,1113,260)
                .QuadTo(1088,264,1065,280).CubicTo(978,336,955,277,878,250).Bind(),silver);
            a.Piece("Hood",B.Head,Slot.HeadTop,
                Path(784,165).QuadTo(797,126,826,113).CubicTo(868,94,888,120,927,115)
                .QuadTo(914,129,899,134).QuadTo(912,161,905,187).QuadTo(899,213,870,226)
                .QuadTo(869,202,854,192).QuadTo(872,174,849,155).QuadTo(822,146,784,165).Bind(),mantle,
                Mark(mantle.shade,Path(798,152).QuadTo(844,113,883,154).QuadTo(854,134,836,146).Bind()),
                Stroke(mantle.lit,4,874,205,898,171,885,145));
            a.Piece("FaceCloth",B.Head,Slot.Face,
                Path(790,188).QuadTo(820,196,850,175).QuadTo(862,182,877,198)
                .QuadTo(864,223,832,232).QuadTo(807,225,797,207).Bind(),silk,
                Stroke(mantle.lit,4,801,198,828,204,849,188));
            a.Piece("Capelet",B.Chest,Slot.Neck,
                Path(809,220).QuadTo(846,240,878,214).QuadTo(891,231,884,240)
                .QuadTo(918,245,950,272).QuadTo(924,271,911,264).LineTo(929,284)
                .QuadTo(880,276,850,258).QuadTo(827,268,811,254).Bind(),mantle,
                Stroke(frost,4,817,239,847,253,875,231));
            a.Piece("ShoulderFin",B.ArmNU,Slot.NearArm+1,
                Path(882,255).QuadTo(908,226,929,253).QuadTo(936,268,944,290)
                .QuadTo(924,277,899,294).QuadTo(882,285,882,255).Bind(),silver,
                Mark(frost,Path(897,257).QuadTo(911,240,927,261).QuadTo(915,252,897,257).Bind()));
            return a.rig;
        }
    }
}
