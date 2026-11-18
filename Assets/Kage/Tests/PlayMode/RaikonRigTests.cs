using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon's rig in a fight (SAN-71). Seam: Raikon spawned from his catalog entry by <see cref="FightBuilder"/> (the
    /// headless harness, driven by scripted input) → the drawn meshes and transforms of his <see cref="FighterRig"/>.
    /// </summary>
    public class RaikonRigTests
    {
        static readonly string[] Soft = { "Cape", "Ponytail", "Tassel1", "Tassel2", "Tassel3", "Fur" };

        static HeadlessFight Fight() => new(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: CharacterCatalog.Get(Raikon.Id));

        static MeshFilter Part(Fighter f, string name) =>
            f.rig.Renderers.First(r => r.gameObject.name == name).GetComponent<MeshFilter>();

        /// <summary>Largest distance (bone-local units) of any vertex of a part from where it was at rest.</summary>
        static float Moved(Vector3[] rest, MeshFilter mf)
        {
            var now = mf.sharedMesh.vertices;
            float m = 0f;
            for (int i = 0; i < now.Length; i++) m = Mathf.Max(m, (now[i] - rest[i]).magnitude);
            return m;
        }

        [Test]
        public void RaikonSpawnsWithHisBodyScale()
        {
            using var f = Fight();
            f.Run(2);
            Assert.AreEqual(1.24f, f.Kage.transform.localScale.y, 1e-4f);
            Assert.AreEqual("raikon", f.Kage.CharacterId);
        }

        [Test]
        public void CapePonytailTasselsAndFurMoveSoftlyWithMotionWhileTheArmourStaysRigid()
        {
            using var f = Fight();
            f.Run(2);
            var rest = Soft.Concat(new[] { "Torso", "Skirt", "Hat" }).ToDictionary(n => n, n => Part(f.Kage, n).sharedMesh.vertices);
            var moved = Soft.ToDictionary(n => n, n => 0f);
            f.KageInput.Add(30, fwd: 1).Add(1, buttons: "S").Add(30);   // walk, swing, stop
            for (int i = 0; i < 61; i++)
            {
                f.Run(1);
                foreach (var n in Soft) moved[n] = Mathf.Max(moved[n], Moved(rest[n], Part(f.Kage, n)));
            }
            foreach (var n in Soft) Assert.Greater(moved[n], 0.01f, n + " should sway (bone-local units)");
            foreach (var n in new[] { "Torso", "Skirt", "Hat" }) Assert.AreEqual(0f, Moved(rest[n], Part(f.Kage, n)), 1e-6f, n + " is rigid");
            // soft, not loose: nothing flies off its root
            foreach (var n in Soft) Assert.Less(moved[n], 0.35f, n + " stays attached");
        }

        [Test]
        public void TheTachiIsHeldByTheHandBoneAndFollowsIt()
        {
            using var f = Fight();
            f.Run(2);
            var blade = f.Kage.rig.Renderers.First(r => r.gameObject.name == "Tachi_Blade").transform;
            Assert.AreSame(f.Kage.rig.Bone(B.Sword), blade.parent);
            Assert.AreSame(f.Kage.rig.Bone(B.HandF), blade.parent.parent);

            // in a swing, the drawn tachi stays where the hand holds it
            f.KageInput.Add(1, buttons: "S");
            Vector2 HandToBlade() => f.Kage.rig.Bone(B.HandF).InverseTransformPoint(blade.position);
            var before = HandToBlade();
            float moved = 0f;
            Vector2 hand0 = f.Kage.rig.Bone(B.HandF).position;
            for (int i = 0; i < 12; i++)
            {
                f.Run(1);
                moved = Mathf.Max(moved, ((Vector2)f.Kage.rig.Bone(B.HandF).position - hand0).magnitude);
                Assert.Less((HandToBlade() - before).magnitude, 0.25f, "the tachi stays in the hand");
            }
            Assert.Greater(moved, 0.05f, "the hand moved in the swing");
        }
    }
}
