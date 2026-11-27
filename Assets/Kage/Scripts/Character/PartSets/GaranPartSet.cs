using UnityEngine;
using static Kage.OpponentKit;
using static Kage.SubBossDrawing;

namespace Kage
{
    public sealed class GaranPartSet : IPartSet
    {
        public string Id => Garan.Id;

        public RigDef Build(CharacterDef c)
        {
            var iron = Paint("24222B","494855","838897");
            var fabric = Paint("2D1B22","52323A","835452");
            var fur = Paint("302C39","625861","A59693");
            var horn = Paint("79594D","B39474","F2D5A3");
            var rope = Paint("5B342B","996B4B","D2A775");
            var ember = GaranPalette.Storm;
            var a = new SubBossDrawing("GARAN",1.48f,GaranPalette.Rim,ember,fabric,iron);
            a.Figure();
            var jaw = HollowAnimeBody.Profile.Gaunt;
            jaw.jaw=1.7f; jaw.chin=1.5f; jaw.brow=1.4f;
            a.Face(Ink("72CDFF"),844,170,56,jaw);
            a.Piece("StormCuirass",B.Chest,Slot.Torso,
                Path(829,216).QuadTo(887,191,951,225).QuadTo(991,240,993,296)
                .QuadTo(984,351,948,405).QuadTo(878,443,796,403)
                .QuadTo(799,360,771,311).QuadTo(755,259,795,232).Bind(),iron,
                Mark(iron.shade,Path(776,294).QuadTo(827,316,838,280).QuadTo(858,318,958,293)
                    .QuadTo(913,331,853,326).LineTo(834,360).QuadTo(821,317,788,324).Bind()),
                Stroke(iron.lit,6,793,348,872,381,961,337),Stroke(iron.lit,5,801,373,868,406,950,363),
                Mark(ember,Path(850,242).LineTo(821,286).LineTo(840,280).LineTo(814,322).LineTo(854,267).LineTo(837,275).Bind()));
            a.Drape("StormApron",B.Hips,Slot.FrontPlates,866,418,SoftPresets.Plates,
                Path(801,400).QuadTo(868,427,939,402).QuadTo(944,494,971,552)
                .QuadTo(933,561,901,580).LineTo(868,547).QuadTo(834,565,793,563)
                .QuadTo(815,480,801,400).Bind(),fabric,
                Stroke(iron.lit,8,807,477,860,490,936,465),Stroke(ember,4,804,544,839,548,863,534));
            a.Piece("Shimenawa",B.Hips,Slot.Belt,
                Path(793,390).QuadTo(870,418,950,386).QuadTo(964,399,953,415)
                .QuadTo(875,450,793,420).QuadTo(781,408,793,390).Bind(),rope);
            for(int i=0;i<10;i++)
            {
                float x=800+16*i, y=404+15*Mathf.Sin(i*Mathf.PI/9);
                a.Piece("Braid"+i,B.Hips,Slot.BeltTrim,Oval(x,y,6.5f,13,-36),rope);
            }
            a.Drape("FurMantle",B.Chest,Slot.TorsoTrim,878,244,SoftPresets.Fur,
                Path(774,254).QuadTo(779,220,821,215).LineTo(814,204).QuadTo(843,208,860,223)
                .QuadTo(888,201,917,206).LineTo(909,215).QuadTo(966,196,996,229)
                .LineTo(982,229).QuadTo(1015,247,1023,279).LineTo(1001,267)
                .QuadTo(1015,286,1005,304).QuadTo(990,276,965,275).LineTo(970,291)
                .QuadTo(932,260,914,277).LineTo(901,291).LineTo(896,270).QuadTo(873,271,862,297)
                .LineTo(858,278).LineTo(838,307).QuadTo(840,280,810,271).LineTo(790,285)
                .LineTo(794,268).QuadTo(773,282,762,289).Bind(),fur,
                Mark(fur.shade,Path(789,253).QuadTo(807,227,846,243).QuadTo(852,257,866,260)
                    .QuadTo(835,262,824,250).LineTo(827,264).QuadTo(810,250,789,253).Bind()),
                Mark(fur.lit,Path(911,232).QuadTo(953,207,984,241).QuadTo(957,226,946,238).LineTo(950,229).Bind()));
            a.Piece("ArmN_Sode",B.ArmNU,Slot.NearArm+1,
                Path(866,264).QuadTo(874,218,929,231).QuadTo(972,237,981,279)
                .LineTo(970,302).QuadTo(936,290,882,313).QuadTo(866,296,866,264).Bind(),iron,
                Stroke(fur.mid,5,877,277,928,251,973,273),
                Stroke(iron.lit,6,882,297,927,274,971,290),Mark(ember,Oval(906,257,7,7)));
            a.Piece("Mane",B.Head,Slot.HeadTop,
                Path(779,160).QuadTo(786,119,816,107).LineTo(804,97).QuadTo(834,94,859,108)
                .QuadTo(878,91,913,92).LineTo(899,107).QuadTo(928,105,950,127).LineTo(927,128)
                .QuadTo(950,151,953,173).LineTo(931,161).QuadTo(943,192,930,222)
                .QuadTo(917,193,908,192).LineTo(905,220).QuadTo(891,204,881,181)
                .QuadTo(864,170,850,165).QuadTo(813,143,779,160).Bind(),fabric,
                Mark(fur.mid,Path(799,141).QuadTo(842,105,892,140).QuadTo(842,122,799,141).Bind()));
            a.Piece("Menpo",B.Head,Slot.Face,
                Path(779,193).QuadTo(802,179,816,190).QuadTo(848,210,876,179)
                .QuadTo(881,212,857,234).QuadTo(823,252,800,226).QuadTo(776,219,779,193).Bind(),
                iron,
                Stroke(iron.shade,6,790,208,812,228,842,208),
                Mark(horn.lit,Path(805,216).QuadTo(799,214,798,201).QuadTo(791,223,805,227).Bind()));
            a.Piece("BrowHorn",B.Head,Slot.HeadTop+1,
                Path(801,153).CubicTo(774,147,758,121,757,95).QuadTo(785,126,807,125)
                .QuadTo(820,135,812,150).Bind(),horn);
            return a.rig;
        }
    }
}
