using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Mugen Armor's purple ring (SAN-85 rework: "the ring behind the character is not aligned properly"). Seam: the rig Kage's
    /// part set builds for the armour, posed on a real <see cref="FighterRig"/> in either facing. The ring must sit on the back of
    /// his head (the JS puts its centre a head-radius behind the skull's centre and half a radius above it, three head-radii
    /// across, opening to the face) and ride the head bone: wherever a pose takes the head, the ring's centre is on the back of it.
    /// The independent source of truth is Kage's bare head shape (<c>KageModel.Build(Look.Ronin)</c>, part "Head": the skull outline, same head, no hair), not the ring's own numbers.
    /// </summary>
    public class MugenRingTests
    {
        GameObject _go;
        FighterRig _rig;
        RigDef _def;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        static RigDef Mugen()
        {
            var def = KageCharacter.Def();
            def.armor = "mugen_yoroi";
            return new KagePartSet().Build(def);
        }

        void Build(float facing)
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _def = Mugen();
            _go = new GameObject("MugenRingRig");
            _rig = _go.AddComponent<FighterRig>();
            _rig.Build(_def, 0);
            _rig.SetFacing(facing, true);
        }

        /// <summary>
        /// The ring's polygon corners, in the Head bone's bind space. Since the 5-tier armour redesign (Tools/armor_art/armors.py) the ring is the halo's
        /// band, its frame, glow and core (<c>Armor_mugen_Halo*</c>, on the head bone); the gold rays that radiate out of it merge into
        /// <c>Armor_mugen_Head_&lt;order&gt;</c> and are not part of the ring.
        /// </summary>
        static List<Vector2> RingBind(RigDef d)
        {
            var o = new List<Vector2>();
            foreach (var p in d.parts.Where(p => p.bone == B.Head && p.name.StartsWith("Armor_mugen_Halo")))
                if (p.polys != null)
                    foreach (var (pts, _) in p.polys) o.AddRange(pts);
            return o;
        }

        Vector2 World(Vector2 bind) => _rig.World(B.Head, bind - _def.pivot[(int)B.Head]);

        /// <summary>Kage's skull in bind space: the centre of his head shape and its half width (the head radius).</summary>
        static (Vector2 centre, float r) Skull()
        {
            var head = KageModel.Build(KageModel.Look.Ronin).parts.Single(p => p.name == "Head");   // the plain head shape (Kage's own head is the SVG hair mesh)
            float x0 = head.pts.Min(v => v.x), x1 = head.pts.Max(v => v.x), y0 = head.pts.Min(v => v.y), y1 = head.pts.Max(v => v.y);
            return (new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f), (x1 - x0) / 2f);
        }

        /// <summary>Least-squares circle through the points (Kasa fit): centre and mean radius.</summary>
        static (Vector2 c, float r) Fit(IList<Vector2> pts)
        {
            // minimise sum (x^2 + y^2 + D x + E y + F)^2
            double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, n = pts.Count, bx = 0, by = 0, b1 = 0;
            foreach (var p in pts)
            {
                double x = p.x, y = p.y, z = x * x + y * y;
                sxx += x * x; sxy += x * y; syy += y * y; sx += x; sy += y;
                bx += x * z; by += y * z; b1 += z;
            }
            double[,] m = { { sxx, sxy, sx }, { sxy, syy, sy }, { sx, sy, n } };
            double[] rhs = { -bx, -by, -b1 };
            double Det(double[,] a) => a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1]) - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0]) + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);
            double d = Det(m);
            double Col(int k)
            {
                var a = (double[,])m.Clone();
                for (int i = 0; i < 3; i++) a[i, k] = rhs[i];
                return Det(a) / d;
            }
            double D = Col(0), E = Col(1), F = Col(2);
            double cx = -D / 2, cy = -E / 2;
            return (new Vector2((float)cx, (float)cy), (float)System.Math.Sqrt(cx * cx + cy * cy - F));
        }

        static readonly (string name, Pose pose)[] Poses =
        {
            ("reference", PoseLib.Ref()),
            ("stance", PoseLib.Stance()),
            ("crouch", PoseLib.Crouch()),
            ("hit high", PoseLib.HurtHigh()),
            ("hit body", PoseLib.HurtBody()),
            ("dash", PoseLib.DashFwd()),
            ("tuck", PoseLib.JumpTuck()),
            ("tumble", PoseLib.Tumble()),
            ("ragdoll lying", PoseLib.Lie()),
            ("thrown, spinning", PoseLib.Thrown(140f)),
        };

        [Test]
        public void TheRingIsCentredOnTheBackOfTheHeadInEveryPoseAndFacing()
        {
            var (centre, hr) = Skull();
            Vector2 anchor = new(centre.x - 1.0f * hr, centre.y + 0.52f * hr);   // behind the skull's centre by a radius, above it by half
            foreach (float facing in new[] { 1f, -1f })
            {
                Build(facing);
                var ring = RingBind(_def);
                Assert.IsNotEmpty(ring, "the Mugen armour has no ring on the head");
                foreach (var (name, pose) in Poses)
                {
                    _rig.Apply(pose.Clone());
                    var fit = Fit(ring.Select(World).ToList());
                    float off = Vector2.Distance(fit.c, World(anchor));
                    Assert.LessOrEqual(off, 0.12f * hr, $"ring centre is {off / hr:0.00} head radii off the back of the head ({name}, facing {facing})");
                }
            }
        }

        [Test]
        public void TheRingIsAboutThreeHeadRadiiAcrossAndSwingsWithTheHead()
        {
            var (_, hr) = Skull();
            Build(1f);
            var ring = RingBind(_def);
            _rig.Apply(PoseLib.Ref());
            var rest = Fit(ring.Select(World).ToList());
            Assert.That(rest.r, Is.InRange(2.3f * hr, 3.1f * hr), "the JS ring is about three head radii across");
            _rig.Apply(PoseLib.HurtHigh());
            var hit = Fit(ring.Select(World).ToList());
            Assert.Greater(Vector2.Distance(rest.c, hit.c), 0.05f * hr, "the ring stayed put while the head moved: it is not on the head");
            Assert.AreEqual(rest.r, hit.r, 0.02f * hr, "the ring changed size");
        }

        [Test]
        public void TheRingArchesOverTheHeadAndOpensTowardsTheFace()
        {
            var (centre, hr) = Skull();
            foreach (float facing in new[] { 1f, -1f })
            {
                Build(facing);
                _rig.Apply(PoseLib.Ref());
                var ring = RingBind(_def).Select(World).ToList();
                var fit = Fit(ring);
                Vector2 mean = ring.Aggregate(Vector2.zero, (a, v) => a + v) / ring.Count - fit.c;
                Vector2 up = (_rig.World(B.Head, Vector2.up) - _rig.World(B.Head, Vector2.zero)).normalized;
                Assert.Greater(Vector2.Dot(mean.normalized, up), 0.7f, $"the ring's mass is not over the head (facing {facing})");
                // the face is the front: the ring's open side (away from its mass) is below and in front of the eye
                var eye = _def.parts.Single(p => p.name == "Eye");
                Vector2 eyeWorld = World(eye.pts.Aggregate(Vector2.zero, (a, v) => a + v) / eye.pts.Length);
                foreach (var v in ring) Assert.Greater(Vector2.Distance(v, eyeWorld), 0.4f * hr, "the ring touches the face");
            }
        }
    }
}
