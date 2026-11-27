using UnityEngine;
using static Kage.OpponentKit;

namespace Kage
{
    // A cel painted on an existing rig part. All layers share its bone, draw order and cloth weights.
    internal sealed class EliteCel
    {
        readonly PartDef part;
        readonly SvgRigStyle palette;
        readonly bool far;
        readonly Frame frame;
        public Outline Path => new(frame);

        public EliteCel(RigDef rig, string name, SvgRigStyle palette, Frame frame, bool far = false)
        {
            part = rig.parts.Find(p => p.name == name);
            this.palette = palette;
            this.frame = frame;
            this.far = far;
            part.polys.Clear();
        }

        public void Ground(string tone, Outline path)
        {
            var shape = path.Bind();
            part.polys.Add((shape, palette.Of(tone, far)));
            if (part.soft != SoftKind.Chain) part.rimPts = shape;
        }

        public void Fill(string tone, Outline path) => part.polys.Add((path.Bind(), palette.Of(tone, far)));

        public void Hang(params float[][] chains)
        {
            part.chains.Clear();
            foreach (var chain in chains) part.chains.Add(V(chain));
        }

        static Vector2 Ref(Vector2 p) => new(KageModel.OriginXRef - p.x * KageModel.RefScale, KageModel.OriginYRef - p.y * KageModel.RefScale);

        // The painted mask, belt and greaves are separate cuts. A continuous lining
        // beneath them follows the same pivots when the chest, head or knee turns.
        public static void LineGarments(RigDef rig, SvgRigStyle palette, string headPart, string chestPart, float width)
        {
            void Back(string name, Vector2[] path, bool far = false) =>
                rig.parts.Find(p => p.name == name).polys.Insert(0, (path, palette.Of("Shadow", far)));

            Back(headPart, new Outline().MoveTo(797, 175).QuadTo(802, 139, 847, 140)
                .QuadTo(891, 142, 887, 188).QuadTo(884, 219, 850, 230)
                .QuadTo(826, 230, 811, 211).QuadTo(799, 197, 797, 175).Bind());
            Back(chestPart, Capsule(852, 210, 880, 275, 23, 42));
            var seat = new Frame(new Vector2(878, 520), Vector2.right * width, Vector2.up);
            Back("Pelvis", new Outline(seat).MoveTo(-.75f, -105).QuadTo(0, -124, .84f, -103)
                .CubicTo(.90f, -63, 1.22f, -8, 1.05f, 35).QuadTo(.95f, 60, .56f, 61)
                .QuadTo(.11f, 46, -.48f, 61).QuadTo(-.94f, 63, -1.05f, 36)
                .QuadTo(-1.13f, 3, -.75f, -105).Bind());
            foreach (bool far in new[] { false, true })
            {
                string side = far ? "F" : "N";
                var elbow = Ref(rig.pivot[(int)(far ? B.ArmFL : B.ArmNL)]);
                var hand = Ref(rig.pivot[(int)(far ? B.HandF : B.HandN)]);
                var knee = Ref(rig.pivot[(int)(far ? B.LegFL : B.LegNL)]);
                var ankle = Ref(rig.pivot[(int)(far ? B.FootF : B.FootN)]);
                Back("Arm" + side + "_Fore", Capsule(elbow.x, elbow.y, hand.x, hand.y, width * .33f, width * .24f), far);
                Back("Leg" + side + "_Shin", Capsule(knee.x, knee.y, ankle.x, ankle.y, width * .43f, width * .33f), far);
            }
        }

        public static Frame BladeFrame(RigDef rig, Vector2 tip)
        {
            var grip = Ref(rig.pivot[(int)B.Sword]);
            var end = Ref(rig.pivot[(int)B.Sword] + tip);
            return Frame.Along(grip.x, grip.y, end.x, end.y, 1);
        }

        public static void Grip(RigDef rig, string name, SvgRigStyle palette, Frame blade, float butt, float guard, string cord, string metal)
        {
            var p = new EliteCel(rig, name, palette, blade);
            p.Ground("Ink", p.Path.MoveTo(butt, -8).QuadTo(butt - .025f, 0, butt, 8)
                .LineTo(guard + .025f, 9).LineTo(guard + .025f, -9));
            p.Fill(cord, p.Path.MoveTo(butt + .005f, -5).LineTo(guard, -6).LineTo(guard, 6).LineTo(butt + .005f, 5));
            for (float x = butt + .035f; x < guard - .03f; x += .066f)
                p.Fill(metal, p.Path.MoveTo(x, -5).LineTo(x + .012f, -5).LineTo(x + .036f, 5).LineTo(x + .024f, 5));
            p.Fill(metal, p.Path.MoveTo(guard - .025f, -20).QuadTo(guard + .014f, -26, guard + .018f, 0)
                .QuadTo(guard + .02f, 21, guard - .006f, 22).QuadTo(guard - .032f, 12, guard - .025f, -20));
        }

        // A continuous waist under the laced plates. Its top encloses the chest pivot,
        // so leaning into a dash cannot open a hole between the breastplate and belt.
        public static void Waist(RigDef rig, SvgRigStyle palette, float width, string cloth)
        {
            var pelvis = rig.parts.Find(p => p.name == "Pelvis");
            var f = new Frame(new Vector2(878, 395), Vector2.right * width, Vector2.up);
            var waist = new Outline(f).MoveTo(-.87f, -9).QuadTo(0, -47, .91f, -12)
                .QuadTo(.77f, 57, 1, 113).QuadTo(.35f, 161, -.82f, 137)
                .QuadTo(-.92f, 110, -.77f, 75).QuadTo(-.68f, 31, -.87f, -9).Bind();
            pelvis.polys.Insert(0, (waist, palette.tones[cloth]));
        }

        public static void Hakama(RigDef d, SvgRigStyle palette, float width, string cloth, string fold)
        {
            foreach (bool far in new[] { false, true })
            {
                var hip = Ref(d.pivot[(int)(far ? B.LegFU : B.LegNU)]);
                var knee = Ref(d.pivot[(int)(far ? B.LegFL : B.LegNL)]);
                var frame = Frame.Along(hip.x, hip.y, knee.x, knee.y, width);
                var p = new EliteCel(d, far ? "LegF_Thigh" : "LegN_Thigh", palette, frame, far);
                p.Ground("Ink", p.Path.MoveTo(-.18f, 0).CubicTo(-.2f, .88f, .2f, .89f, .48f, 1.05f)
                    .QuadTo(.83f, 1.22f, 1.08f, .78f).QuadTo(1.27f, .33f, 1.19f, -.54f)
                    .QuadTo(1.09f, -1.03f, .88f, -.87f).QuadTo(.45f, -1.14f, .06f, -.87f).QuadTo(-.17f, -.7f, -.18f, 0));
                p.Fill(cloth, p.Path.MoveTo(-.1f, .1f).QuadTo(-.09f, .73f, .32f, .84f)
                    .QuadTo(.82f, 1.2f, 1.08f, .65f).QuadTo(1.24f, .25f, 1.13f, -.54f)
                    .QuadTo(.91f, -.28f, .70f, -.49f).QuadTo(.38f, -.78f, -.1f, .1f));
                p.Fill(fold, p.Path.MoveTo(.22f, .65f).CubicTo(.49f, .62f, .67f, .96f, 1.04f, .62f)
                    .QuadTo(.79f, 1.04f, .53f, .81f).QuadTo(.34f, .68f, .22f, .65f));
            }
        }

        // The cap overlaps both pivots, including when the elbow folds back against the shoulder.
        public static void Sleeves(RigDef d, SvgRigStyle palette, float radius, string mid, string light)
        {
            foreach (bool far in new[] { false, true })
            {
                var a = d.pivot[(int)(far ? B.ArmFU : B.ArmNU)];
                var b = d.pivot[(int)(far ? B.ArmFL : B.ArmNL)];
                var sh = Ref(a);
                var el = Ref(b);
                var f = Frame.Along(sh.x, sh.y, el.x, el.y, radius);
                var cel = new EliteCel(d, far ? "ArmF_Upper" : "ArmN_Upper", palette, f, far);
                cel.Ground("Ink", cel.Path.MoveTo(-.16f, 0).CubicTo(-.19f, .85f, .09f, 1.17f, .42f, .91f)
                    .QuadTo(.73f, .57f, .98f, .63f).CubicTo(1.24f, .59f, 1.23f, -.55f, 1.02f, -.66f)
                    .QuadTo(.66f, -.68f, .38f, -.98f).QuadTo(-.13f, -1.17f, -.16f, 0));
                cel.Fill(mid, cel.Path.MoveTo(-.11f, .04f).QuadTo(-.08f, 1.1f, .39f, .82f)
                    .QuadTo(.7f, .49f, .99f, .52f).QuadTo(1.11f, .1f, 1.04f, -.34f)
                    .QuadTo(.73f, -.17f, .49f, -.63f).QuadTo(.07f, -1.12f, -.11f, .04f));
                cel.Fill(light, cel.Path.MoveTo(.06f, .55f).QuadTo(.16f, .94f, .41f, .74f)
                    .QuadTo(.61f, .54f, .73f, .49f).QuadTo(.42f, .53f, .33f, .56f).QuadTo(.15f, .68f, .06f, .55f));
            }
        }
    }
}
