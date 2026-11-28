using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    public class LongHairWindTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void GustLiftsTheTipsThenSettlesWithoutMovingTheSkull(int fps)
        {
            var rig = KageModel.Build(KageModel.Look.Kage, "long_flowing");
            var part = rig.parts.Single(p => p.soft == SoftKind.Hair);
            var mesh = MeshKit.PolyMulti(part.polys, rig.hairCentre, "WindTest");
            try
            {
                var rest = mesh.vertices;
                int tip = System.Array.IndexOf(rest, rest.OrderBy(p => p.x).First());
                var hair = new HairField(mesh, Vector2.zero, trailLength: part.hairTrailLength);
                hair.worldWind = Wind.Blowing(new Vector2(-3.4f, .95f), gust: 3.5f, seed: 47);
                for (int frame = 0; frame < fps; frame++)
                    hair.Tick(1f / fps, Vector2.zero, 0, 1);

                var gust = mesh.vertices;
                Assert.Greater(gust[tip].y - rest[tip].y, .12f);
                for (int i = 0; i < rest.Length; i++)
                    if (rest[i].x >= -.10f) Assert.AreEqual(rest[i], gust[i], "skull vertex " + i);

                hair.Tick(0, Vector2.zero, 0, 1);
                CollectionAssert.AreEqual(gust, mesh.vertices, "hit-stop holds the pose");

                hair.worldWind = Wind.Calm;
                for (int frame = 0; frame < 2 * fps; frame++)
                    hair.Tick(1f / fps, Vector2.zero, 0, 1);
                Assert.Less(Vector3.Distance(rest[tip], mesh.vertices[tip]), .025f);

                hair.worldWind = Wind.Blowing(Vector2.right * 4f);
                for (int frame = 0; frame < fps; frame++)
                    hair.Tick(1f / fps, Vector2.zero, 0, 1);
                float facingRight = mesh.vertices[tip].y;
                hair.Reset();
                CollectionAssert.AreEqual(rest, mesh.vertices);
                for (int frame = 0; frame < fps; frame++)
                    hair.Tick(1f / fps, Vector2.zero, 0, -1);
                Assert.Greater(mesh.vertices[tip].y - facingRight, .1f, "turning across the wind reverses the bend");
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
