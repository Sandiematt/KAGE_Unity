using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A fighter wearing each armour (SAN-85), through the real spawn path: the equipped armour on Kage's character data reaches
    /// his built rig, the pieces render in the right layers, the coat and the scarf keep moving, and walking, striking, being
    /// hit and the K.O. ragdoll with it on raise no error. Without an armour drawing Kage's rig is the same as always.
    /// </summary>
    public class ArmorFighterTests
    {
        static CharacterDef KageWearing(string armor, string helmet = null)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.armor = armor;
            def.helmet = helmet;
            return def;
        }

        static Renderer[] ArmourRenderers(Fighter f) => f.rig.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Armor_") && !r.name.EndsWith("_Rim")).ToArray();

        static Renderer Named(Fighter f, string name) => f.rig.GetComponentsInChildren<Renderer>(true).First(r => r.name == name);

        static void AssertAllFinite(Fighter f, string what)
        {
            foreach (var r in f.rig.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                var b = r.bounds;
                Assert.IsFalse(float.IsNaN(b.center.x) || float.IsNaN(b.center.y) || float.IsNaN(b.extents.x) || float.IsNaN(b.extents.y), $"{what}: {r.name} has NaN bounds");
            }
        }

        [Test]
        public void ASpawnedKageWearsTheArmourFromHisCharacterData()
        {
            foreach (var id in ArmorVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                Assert.IsNotEmpty(ArmourRenderers(fight.Kage), $"{id} is not on the rig");
            }
        }

        [Test]
        public void WithoutAnArmourDrawingKagesRigHasNoArmourParts()
        {
            foreach (var id in new[] { "tachibana_yoroi", "kozane_do", null })
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                Assert.IsEmpty(ArmourRenderers(fight.Kage), id);
                Assert.IsNotNull(Named(fight.Kage, "Coat_Side"));
                Assert.IsNotNull(fight.Kage.rig.scarf, id);
            }
        }

        [Test]
        public void TheArmourIsDrawnOverTheBodyAndUnderTheScarfAndAnythingOnTheHeadUnderTheEye()
        {
            foreach (var id in ArmorVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                int eye = Named(fight.Kage, "Eye").sortingOrder;
                int torso = Named(fight.Kage, "Torso").sortingOrder;
                int wrap = Named(fight.Kage, "Scarf_Wrap").sortingOrder;
                foreach (var r in ArmourRenderers(fight.Kage))
                {
                    var bone = r.transform.parent.name;
                    if (bone == "Head") Assert.Less(r.sortingOrder, eye, $"{id}/{r.name} over the eye");
                    if (bone == "Chest") { Assert.Greater(r.sortingOrder, torso, $"{id}/{r.name}"); Assert.Less(r.sortingOrder, wrap, $"{id}/{r.name}"); }
                }
            }
        }

        [Test]
        public void KageCanWalkStrikeAndBeHitInEveryArmourAndTheCoatAndScarfKeepMoving()
        {
            foreach (var id in ArmorVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageDef: KageWearing(id));
                fight.KageInput.Add(30, fwd: 1).Add(1, buttons: "P").Add(8).Add(1, buttons: "K").Add(20);
                fight.RoninInput.Add(600);
                fight.Run(90);
                fight.Kage.rig.Apply(PoseLib.HurtHigh());
                fight.Run(10);
                foreach (var r in ArmourRenderers(fight.Kage))
                {
                    Assert.IsTrue(r.enabled, $"{id}/{r.name}");
                    Assert.IsFalse(float.IsNaN(r.bounds.center.x), $"{id}/{r.name}");
                }
                AssertAllFinite(fight.Kage, id);
                Assert.IsNotNull(fight.Kage.rig.scarf, id);
                Assert.IsTrue(fight.Kage.rig.scarf.GetComponentsInChildren<MeshRenderer>().All(m => m.enabled), $"{id} scarf");
            }
        }

        [Test]
        public void TheCoatClothStillSwingsWithEveryArmour()
        {
            foreach (var id in ArmorVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.8f, kageDef: KageWearing(id));
                fight.Run(10);
                var mesh = Named(fight.Kage, "Coat_Side").GetComponent<MeshFilter>().sharedMesh;
                var before = mesh.vertices.ToArray();
                fight.KageInput.Add(40, fwd: 1);
                fight.Run(40);
                var after = mesh.vertices;
                Assert.AreEqual(before.Length, after.Length, id);
                float moved = 0f;
                for (int i = 0; i < before.Length; i++) moved = Mathf.Max(moved, (after[i] - before[i]).magnitude);
                Assert.Greater(moved, 0.005f, $"{id}: the coat did not move");
            }
        }

        [Test]
        public void TheScarfTailsIncludeTheArmoursExtraStrands()
        {
            using var plain = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(null));
            int tails = plain.Kage.rig.scarf.GetComponentsInChildren<MeshRenderer>().Length;
            Assert.AreEqual(2, tails);
            using var ronin = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing("ronin_yoroi"));
            Assert.AreEqual(2 + 4, ronin.Kage.rig.scarf.GetComponentsInChildren<MeshRenderer>().Length);
            using var kagehira = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing("kagehira_yoroi"));
            Assert.AreEqual(2 + 2, kagehira.Kage.rig.scarf.GetComponentsInChildren<MeshRenderer>().Length);
            using var trophy = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing("shiden_yoroi"));
            Assert.AreEqual(2, trophy.Kage.rig.scarf.GetComponentsInChildren<MeshRenderer>().Length);
        }

        [Test]
        public void TheKnockOutRagdollCarriesTheArmourWithoutError()
        {
            foreach (var id in ArmorVisuals.Ids)
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.4f, 0.4f, kageDef: KageWearing(id));
                fight.Kage.health = 1f;
                fight.KageInput.Add(300);
                fight.RoninInput.Add(1, dy: -1, buttons: "K").Add(300);
                fight.Run(60);
                Assert.AreEqual(FState.KO, fight.Kage.state, $"{id}: Kage was not knocked out");
                fight.Run(30);
                AssertAllFinite(fight.Kage, id + " ragdoll");
                // each armour piece is still on the bone it dresses, so it tumbles with the body
                foreach (var r in ArmourRenderers(fight.Kage)) Assert.IsNotNull(r.transform.parent, $"{id}/{r.name}");
            }
        }

        [Test]
        public void LegendaryArmoursAttachPrestigeAuraAndNonLegendaryDoNot()
        {
            foreach (var id in new[] { "yurei_yoroi", "mugen_yoroi" })
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                var aura = fight.Kage.GetComponent<ArmorAura>();
                Assert.IsNotNull(aura, $"{id} should carry ArmorAura");
                Assert.AreNotEqual(ArmorAuraKind.None, aura.Kind, id);
                fight.Run(15);
                AssertAllFinite(fight.Kage, id + " aura");
            }
            foreach (var id in new[] { "tachibana_yoroi", "ronin_yoroi", "yami_yoroi", "shinsen_yoroi", "raiko_yoroi", "kagehira_yoroi", "ikazuchi_do", "shiden_yoroi" })
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(id));
                Assert.IsNull(fight.Kage.GetComponent<ArmorAura>(), $"{id} should not carry Legendary ArmorAura");
            }
        }

        [Test]
        public void SplitHaoriTailsAndCapesReactToDashFlipAndHeavyAttacksWithoutJitter()
        {
            foreach (var id in new[] { "yami_yoroi", "raiko_yoroi", "shiden_yoroi", "yurei_yoroi", "mugen_yoroi" })
            {
                using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.8f, 0.8f, kageDef: KageWearing(id));
                fight.Run(8);
                var tail = fight.Kage.rig.GetComponentsInChildren<MeshFilter>(true).First(m => m.name.Contains("HaoriTailNear"));
                var before = tail.sharedMesh.vertices.ToArray();
                // Dash forward, stop, heavy slash, and turn around
                fight.KageInput.Add(1, fwd: 1, dash: true).Add(14, fwd: 1).Add(1, buttons: "S").Add(25);
                fight.RoninInput.Add(100);
                fight.Run(45);
                fight.Kage.SetFacing(-fight.Kage.facing);
                fight.Run(15);
                var after = tail.sharedMesh.vertices;
                float moved = 0f;
                for (int i = 0; i < before.Length; i++) moved = Mathf.Max(moved, (after[i] - before[i]).magnitude);
                Assert.Greater(moved, 0.004f, $"{id}: split haori tail did not react to dash/slash/flip");
                AssertAllFinite(fight.Kage, id + " dynamic cloth");
            }
        }

        [Test]
        public void EveryHelmetAndArmourCombinationSpawnsAndStandsStill()
        {
            var helmets = new[] { null, "shiro_maki", "kuro_zukin", "sugegasa", "hachigane", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto", "shika_kabuto", "utsuro_kabuto", "shikko_zukin", "sumi_eboshi", "hisame_zukin", "raijin_kabuto" };
            foreach (var armor in ArmorVisuals.Ids.Concat(new[] { "tachibana_yoroi" }))
                foreach (var helmet in helmets)
                {
                    using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: KageWearing(armor, helmet));
                    fight.Run(3);
                    Assert.IsNotNull(Named(fight.Kage, "Eye"), $"{helmet}+{armor}");
                    AssertAllFinite(fight.Kage, $"{helmet}+{armor}");
                }
        }
    }
}
