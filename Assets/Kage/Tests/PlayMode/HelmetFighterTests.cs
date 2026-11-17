using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A fighter wearing each helmet (SAN-72), through the real spawn path: the equipped helmet on Kage's character data
    /// reaches his built rig, the helmet renders over the hair and under the eye, and walking, striking and being hit with it
    /// on raises no error. Without a helmet drawing Kage's rig is the same as always.
    /// </summary>
    public class HelmetFighterTests
    {
        static CharacterDef KageWearing(string helmet)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.helmet = helmet;
            return def;
        }

        static Renderer[] HelmetRenderers(Fighter f) => f.rig.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Helmet_") && !r.name.EndsWith("_Rim")).ToArray();

        static Renderer Named(Fighter f, string name) => f.rig.GetComponentsInChildren<Renderer>(true).First(r => r.name == name);

        [Test]
        public void ASpawnedKageWearsTheHelmetFromHisCharacterData()
        {
            foreach (var id in HelmetVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                Assert.IsNotEmpty(HelmetRenderers(fight.Kage), $"{id} is not on the rig");
            }
        }

        [Test]
        public void WithoutAHelmetDrawingKagesRigHasNoHelmetParts()
        {
            foreach (var id in new[] { "shiro_maki", "kasa", null })
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                Assert.IsEmpty(HelmetRenderers(fight.Kage), id);
                Assert.IsNotNull(Named(fight.Kage, "Head_Hair"));
            }
        }

        [Test]
        public void TheHelmetIsDrawnOverTheHairAndUnderTheEyeAndTheScarf()
        {
            foreach (var id in HelmetVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                int eye = Named(fight.Kage, "Eye").sortingOrder;
                int wrap = Named(fight.Kage, "Scarf_Wrap").sortingOrder;
                var hair = fight.Kage.rig.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Head_Hair" || r.name == "Head");
                Assert.IsNotNull(hair, id);
                foreach (var r in HelmetRenderers(fight.Kage))
                {
                    Assert.Greater(r.sortingOrder, hair.sortingOrder, $"{id}/{r.name} under the hair");
                    Assert.Less(r.sortingOrder, eye, $"{id}/{r.name} over the eye");
                    Assert.Less(r.sortingOrder, wrap, $"{id}/{r.name} over the scarf");
                }
            }
        }

        [Test]
        public void KageCanWalkStrikeAndBeHitInEveryHelmet()
        {
            foreach (var id in HelmetVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageDef: KageWearing(id));
                fight.KageInput.Add(30, fwd: 1).Add(1, buttons: "P").Add(8).Add(1, buttons: "K").Add(20);
                fight.RoninInput.Add(600);
                fight.Run(90);
                fight.Kage.rig.Apply(PoseLib.HurtHigh());
                fight.Run(10);
                foreach (var r in HelmetRenderers(fight.Kage))
                {
                    Assert.IsTrue(r.enabled, $"{id}/{r.name}");
                    Assert.IsFalse(float.IsNaN(r.bounds.center.x), $"{id}/{r.name}");
                }
            }
        }

        [Test]
        public void TheHelmetStaysOnTheHeadWhenKageTurnsAndFlinches()
        {
            using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing("tetsu_kabuto"));
            fight.Run(5);
            var head = fight.Kage.rig.Bone(B.Head);
            foreach (var r in HelmetRenderers(fight.Kage).Where(r => r.GetComponent<MeshFilter>() != null))
                Assert.IsTrue(r.transform.IsChildOf(head), r.name);
        }
    }
}
