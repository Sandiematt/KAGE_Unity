using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Fighters built from character data (ADR-0005, SAN-66): Kage and the Ronin come from their catalog
    /// entries, and any other entry spawns a fighter that fights through the same input interface.
    /// Each fight is disposed before the next is built.
    /// </summary>
    public class CharacterSpawnTests
    {
        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        /// <summary>A recoloured Kage with half his health and 1.5 times his speed, on the foe's side.</summary>
        static CharacterDef Swift()
        {
            var d = CharacterCatalog.Get("kage");
            d.id = "test_swift";
            d.displayName = "SWIFT";
            d.jsHealth = 116f;
            d.speedMul = 1.5f;
            d.colors = new FighterColors(Color.cyan, Color.white, new Color(0f, 0.3f, 0.6f));
            d.aiProfile = "regular";
            d.flags = CharacterFlags.None;
            return d;
        }

        [Test]
        public void KageIsBuiltFromHisCharacterData()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            Assert.AreEqual("kage", f.Kage.CharacterId);
            Assert.AreEqual("kage", f.Kage.Character.id);
            Assert.AreEqual(100f, f.Kage.maxHealth);
            Assert.AreEqual(100f, f.Kage.health);
            Assert.AreEqual(1f, f.Kage.Mass);
            Assert.AreEqual(Vector3.one, f.Kage.transform.localScale);
            Assert.IsTrue(f.Kage.Has(CharacterFlags.CanRageArt));
            Assert.IsTrue(f.Kage.isPlayer);
        }

        [Test]
        public void TheRoninIsBuiltFromHisEntry()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Ai);
            Assert.AreEqual("ronin", f.Ronin.CharacterId);
            Assert.AreEqual(100f, f.Ronin.maxHealth);
            Assert.AreEqual(new Vector3(1.04f, 1.04f, 1f), f.Ronin.transform.localScale);
            Assert.AreEqual(Palette.Amber, f.Ronin.fxMain);
            Assert.IsFalse(f.Ronin.Has(CharacterFlags.CanRageArt));
            Assert.IsFalse(f.Ronin.isPlayer);
        }

        [Test]
        public void TheHealthScaleSetsEveryFightersHealth()
        {
            Tuning.Override(new HealthTuning { healthScale = 0.5 });
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Ai);
            Assert.AreEqual(116f, f.Kage.maxHealth);
            Assert.AreEqual(116f, f.Ronin.maxHealth);
        }

        [Test]
        public void AnotherEntrySpawnsAFighterWithItsOwnHealthLookAndSpeed()
        {
            float Walk(CharacterDef foe)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX: -3f, roninX: 3f, foeDef: foe);
                f.KageInput.Add(600);
                f.RoninInput.Add(40, fwd: 1).Add(600);
                f.Run(12);   // up to walking speed
                float x0 = f.Ronin.Pos.x;
                f.Run(12);   // still walking: the run starts after 0.42 s
                return x0 - f.Ronin.Pos.x;
            }

            float ronin = Walk(null), swift = Walk(Swift());
            Assert.Greater(ronin, 0.2f);
            Assert.AreEqual(1.5f, swift / ronin, 0.05f, $"walked {swift:0.000} m against the Ronin's {ronin:0.000} m");

            using var g = new HeadlessFight(KageMoveScenarios.Seed, Foe.Ai, foeDef: Swift());
            Assert.AreEqual("test_swift", g.Ronin.FighterId);
            Assert.AreEqual(50f, g.Ronin.maxHealth);
            Assert.AreEqual(Color.cyan, g.Ronin.fxMain);
            Assert.AreEqual(Color.cyan, g.Ronin.def.parts.Find(p => p.name == "Lapel").color);
            Assert.AreEqual(Vector3.one, g.Ronin.transform.localScale);
        }

        [Test]
        public void AnotherEntryFightsThroughTheSameInput()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX: -0.3f, roninX: 0.3f, foeDef: Swift());
            f.KageInput.Add(600);
            f.RoninInput.Add(1, buttons: "P").Add(600);
            f.Run(30);
            var hits = f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin).ToList();
            Assert.IsNotEmpty(hits, "the swift Kage's jab never landed");
            Assert.AreEqual(f.Kage, hits[0].Target);
            Assert.AreEqual("test_swift", hits[0].SourceId);
        }

        [Test]
        public void TheRageArtNeedsTheCanRageArtFlag()
        {
            bool RageArt(bool addFlag)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX: -0.6f, roninX: 0.6f);
                f.Ronin.meter = 100f;
                if (addFlag) f.Ronin.Flags |= CharacterFlags.CanRageArt;   // as a boss phase would
                f.KageInput.Add(600);
                f.RoninInput.Add(1, dy: -1, buttons: "X").Add(600);
                f.Run(20);
                return f.Events(CombatEventType.RageArt).Any(e => e.Source == f.Ronin);
            }

            Assert.IsFalse(RageArt(false), "the Ronin has no rage art");
            Assert.IsTrue(RageArt(true), "a flag added at run time gives him one");
        }
    }
}
