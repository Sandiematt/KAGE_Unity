using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;

namespace Kage
{
    /// <summary>Bind-space figure drawing for the four lieutenants. Sleeves, greaves and gloves
    /// overlap at the gameplay pivots. A garment's folds and embroidery share its deformed mesh.</summary>
    internal sealed class SubBossDrawing
    {
        public readonly RigDef rig;
        public readonly Tone cloth, metal;
        static readonly Color Silhouette = Ink("050507");
        public readonly Color accent;
        readonly float bulk;

        public SubBossDrawing(string name, float bulk, Color rim, Color accent, Tone cloth, Tone metal)
        {
            rig = Skeleton(name);
            rig.scarf = rig.softCoat = rig.coatTail = false;
            rig.rimColor = rim;
            rig.bodyColor = cloth.mid;
            this.cloth = cloth;
            this.metal = metal;
            this.accent = accent;
            this.bulk = bulk;
        }

        public static Color Ink(string hex) => KMath.Hex("#" + hex);
        public static Tone Paint(string shadow, string mid, string light) => new(Ink(shadow), Ink(mid), Ink(light));
        public static Outline Path(float x, float y) => new Outline().MoveTo(x, y);
        public static Vector2[] Curve(params float[] xy) => RoundedAll(Frame.Ref, xy);
        public static (Vector2[], Color) Mark(Color c, Vector2[] shape) => (shape, c);
        public static (Vector2[], Color) Stroke(Color c, float width, float x, float y, float cx, float cy, float ex, float ey)
            => (Band(width, QuadXy(Frame.Ref, x, y, cx, cy, ex, ey)), c);

        public PartDef Piece(string name, B bone, int slot, Vector2[] contour, Tone tone, params (Vector2[], Color)[] marks)
        {
            bool farArm = bone == B.ArmFL || bone == B.HandF;
            return AddCel(rig, name, bone, slot, contour, tone, 8f, 3f, detail: marks, swordLayer: farArm);
        }

        public void Drape(string name, B bone, int slot, float x, float y, ChainFeel feel,
            Vector2[] contour, Tone tone, params (Vector2[], Color)[] marks)
        {
            var p = AddCel(rig, name, bone, slot, contour, tone, 10f, 4f, rim: false, detail: marks);
            p.soft = SoftKind.Flow;
            p.flowRoot = P(x, y);
            p.chainFeel = feel;
        }

        static Tone Recede(Tone t) => new(Color.Lerp(t.shade, Seam, 0.22f), Color.Lerp(t.mid, t.shade, 0.5f), Color.Lerp(t.lit, t.mid, 0.6f));

        public void Figure()
        {
            Leg(false);
            Leg(true);
            Arm(false);
            Arm(true);
            Piece("Undercoat", B.Hips, Slot.Pelvis,
                Curve(832,373, 907,372, 927+bulk*16,412, 923+bulk*12,499, 946,554, 905,580, 852,570, 819,585, 802,549, 822,470), cloth);
            Add(rig, "Neck", B.Neck, Slot.Neck, Silhouette, Curve(830,188,868,190,884,245,866,257,832,239), rim: false);
        }

        void Leg(bool near)
        {
            float hx = near ? 848 : 905, kx = near ? 835 : 955;
            var thigh = Frame.Along(hx,545,kx,680,bulk);
            var shin = Frame.Along(kx,680,kx,763,bulk);
            var foot = new Frame(new Vector2(kx,763), Vector2.right * bulk, Vector2.up);
            int slot = near ? Slot.NearLeg : Slot.FarLeg;
            string side = near ? "Near" : "Far";
            Tone fab = near ? cloth : Recede(cloth), plate = near ? metal : Recede(metal);
            Piece(side+"Hakama", near ? B.LegNU : B.LegFU, slot,
                new Outline(thigh).MoveTo(-0.17f,20).QuadTo(-0.22f,51,0.28f,48)
                    .CubicTo(0.55f,46,0.66f,36,0.92f,31).QuadTo(1.18f,28,1.13f,-10)
                    .QuadTo(1.14f,-31,0.79f,-32).CubicTo(0.45f,-38,0.18f,-52,-0.12f,-36).QuadTo(-0.27f,-20,-0.17f,20).Bind(), fab,
                Mark(fab.shade,new Outline(thigh).MoveTo(0.05f,-20).QuadTo(0.49f,-9,0.84f,16).QuadTo(0.52f,4,0.29f,5).Bind()),
                Mark(fab.lit,new Outline(thigh).MoveTo(0.21f,32).QuadTo(0.51f,29,0.85f,12)
                    .QuadTo(0.60f,33,0.21f,32).Bind()),
                Mark(fab.shade,new Outline(thigh).MoveTo(0.62f,-24).QuadTo(0.80f,-10,1.02f,-3)
                    .QuadTo(0.88f,-18,0.62f,-24).Bind()));
            Piece(side+"Greave", near ? B.LegNL : B.LegFL, slot+1,
                new Outline(shin).MoveTo(-0.19f,13).QuadTo(-0.28f,33,0.18f,28)
                    .QuadTo(0.59f,23,1.1f,17).QuadTo(1.2f,-6,1.02f,-19)
                    .QuadTo(0.81f,-17,0.47f,-27).QuadTo(-0.12f,-43,-0.19f,13).Bind(), plate,
                Mark(plate.lit,new Outline(shin).MoveTo(0.02f,17).QuadTo(0.33f,12,0.98f,13).QuadTo(0.43f,18,0.02f,17).Bind()),
                Mark(fab.lit,Band(5,shin.Xy(0.39f,-24,0.49f,20))));
            Piece(side+"Tabi", near ? B.FootN : B.FootF, near ? Slot.NearFoot : Slot.FarFoot,
                new Outline(foot).MoveTo(-19,-10).QuadTo(-7,-18,15,-6).QuadTo(24,12,20,38)
                    .QuadTo(19,51,12,51).QuadTo(-12,48,-48,52).QuadTo(-72,52,-63,40)
                    .QuadTo(-57,33,-43,29).QuadTo(-24,20,-19,-10).Bind(), plate,
                Mark(fab.lit,new Outline(foot).MoveTo(-54,36).QuadTo(-35,26,-20,11).LineTo(-15,17).QuadTo(-34,34,-54,36).Bind()));
        }

        void Arm(bool near)
        {
            float sx = near ? 905 : 862, sy = near ? 268 : 272, ex = near ? 926 : 822, ey = near ? 375 : 385;
            float wx = near ? 914 : 797, wy = near ? 442 : 455;
            var fore = Frame.Along(ex,ey,wx,wy,bulk);
            var hand = new Frame(new Vector2(wx,wy),Vector2.right*bulk,Vector2.up*bulk);
            var fab = near ? cloth : Recede(cloth);
            var guard = near ? metal : Recede(metal);
            string side = near ? "ArmN" : "ArmF";
            int slot = near ? Slot.NearArm : Slot.FarArm;
            var sleeve = Frame.Along(sx,sy,ex,ey,bulk);
            Piece(side+"Sleeve",near ? B.ArmNU : B.ArmFU,slot,
                Capsule(sx,sy,ex,ey,32*bulk,23*bulk,4*bulk),fab,
                Mark(fab.shade,new Outline(sleeve).MoveTo(0.06f,-18).QuadTo(0.53f,-4,0.73f,21)
                    .QuadTo(0.60f,-7,0.41f,-15).LineTo(0.60f,-12).QuadTo(0.40f,-25,0.06f,-18).Bind()),
                Mark(fab.lit,new Outline(sleeve).MoveTo(0.68f,23).QuadTo(0.8f,2,0.82f,-15)
                    .QuadTo(0.85f,9,0.68f,23).Bind()));
            Piece(side+"Bracer",near ? B.ArmNL : B.ArmFL,slot+1,
                new Outline(fore).MoveTo(-0.18f,17).QuadTo(-0.32f,28,0.11f,28)
                    .QuadTo(0.54f,27,1.08f,17).QuadTo(1.17f,-4,1.02f,-18)
                    .QuadTo(0.7f,-18,0.31f,-27).QuadTo(-0.1f,-37,-0.18f,17).Bind(),guard,
                Mark(guard.lit,new Outline(fore).MoveTo(0,17).QuadTo(0.52f,11,0.94f,12).QuadTo(0.43f,21,0,17).Bind()),
                Mark(accent,Band(3.6f,fore.Xy(0.18f,22,0.32f,-19))));
            var palm = new Outline(hand).MoveTo(-13,-7).QuadTo(0,-14,14,-2)
                .QuadTo(21,4,16,21).QuadTo(16,31,9,34).QuadTo(3,39,-3,36)
                .QuadTo(-12,39,-16,33).QuadTo(-24,33,-26,24).QuadTo(-29,18,-24,10)
                .QuadTo(-22,4,-13,-7).Bind();
            var fingers = new List<(Vector2[],Color)>();
            fingers.Add(Mark(guard.lit,new Outline(hand).MoveTo(-21,9).QuadTo(-16,1,-9,3)
                .LineTo(0,14).QuadTo(-4,22,-11,18).Bind()));
            for(int i=0;i<3;i++) fingers.Add(Mark(guard.shade,Band(2.7f,hand.Xy(-17+i*8,22,-12+i*8,29))));
            Piece((near ? "HandN" : "HandF")+"_Tekko",near ? B.HandN : B.HandF,
                near ? Slot.NearHand : 5,palm,guard,fingers.ToArray());
        }

        public void Face(Color eye, float x, float y, float radius, HollowAnimeBody.Profile profile)
        {
            var f = Frame.Head(x, y, radius);
            Add(rig, "Face", B.Head, Slot.Head, Silhouette, HollowAnimeBody.FacePath(f, profile), rim: false);
            var slit = new Outline(f).MoveTo(0.97f, 0.06f).LineTo(0.20f, -0.16f)
                .LineTo(0.43f, 0.09f).LineTo(0.71f, 0.16f).Bind();
            var light = AddMulti(rig, "Eye", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                (MeshKit.Grow(slit, 0.009f), eye.WithA(0.15f)),
                (MeshKit.Grow(slit, 0.004f), eye.WithA(0.32f)),
                (slit, eye),
            }, 2, role: PartRole.Eye);
            light.glow = 1.8f;
        }
    }
}
