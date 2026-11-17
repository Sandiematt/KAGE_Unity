using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Character data (ADR-0005) and the health scale (ADR-0010, SAN-66): every fighter's health is its JS
    /// health × one shared scale, Kage and the Ronin are catalog entries, and foes take their stats from the
    /// generated JS tables.
    /// </summary>
    public class CharacterDataTests
    {
        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        [Test]
        public void TheShippedHealthScaleKeepsKagesFreshSaveHealthAt100()
        {
            Assert.AreEqual(100.0 / 232.0, Tuning.Get<HealthTuning>().healthScale, 1e-9);
            var kage = CharacterCatalog.Get("kage");
            Assert.AreEqual(232f, kage.jsHealth);
            Assert.AreEqual(100f, kage.MaxHealth());
        }

        [Test]
        public void AFoeStartsAtItsRawJsHealthTimesTheHealthScale()
        {
            // raw JS hp (ADR-0005 table), no JS role multipliers (owner decision on SAN-59)
            Assert.AreEqual(170f * 100f / 232f, CharacterDef.FromJs("order_footman").MaxHealth(), 1e-3);
            Assert.AreEqual(73.2759f, CharacterDef.FromJs("order_footman").MaxHealth(), 1e-3);
            Assert.AreEqual(275.8621f, CharacterDef.FromJs("raikon").MaxHealth(), 1e-3);
        }

        [Test]
        public void ChangingTheHealthScaleChangesEveryCharactersHealth()
        {
            Tuning.Override(new HealthTuning { healthScale = 0.5 });
            Assert.AreEqual(116f, CharacterCatalog.Get("kage").MaxHealth());
            Assert.AreEqual(116f, CharacterCatalog.Get("ronin").MaxHealth());
            Assert.AreEqual(85f, CharacterDef.FromJs("order_footman").MaxHealth());
            Assert.AreEqual(320f, CharacterDef.FromJs("raikon").MaxHealth());
        }

        [Test]
        public void HealthMulScalesOneCharacterOnly()
        {
            var footman = CharacterDef.FromJs("order_footman");
            footman.healthMul = 2f;
            Assert.AreEqual(146.5517f, footman.MaxHealth(), 1e-3);
            Assert.AreEqual(100f, CharacterCatalog.Get("kage").MaxHealth());
        }

        [Test]
        public void KagesHealthFollowsHisSave()
        {
            var save = Kage.Rules.GameRules.DefaultSave(false);
            save.level = 6;     // +4 JS health per player level
            Assert.AreEqual(252f, KageCharacter.Def(save).jsHealth);
        }

        [Test]
        public void JsStatsAndFlagsComeFromTheJsTables()
        {
            var raikon = CharacterDef.FromJs("raikon");
            Assert.AreEqual(640f, raikon.jsHealth);
            Assert.AreEqual(0.16f, raikon.defense, 1e-6);
            Assert.AreEqual(1.1f, raikon.damageMul, 1e-6);
            Assert.AreEqual(0.9f, raikon.speedMul, 1e-6);
            Assert.AreEqual("boss", raikon.aiProfile);
            Assert.AreEqual(CharacterFlags.Armour | CharacterFlags.NoGrab, raikon.flags);

            Assert.AreEqual(CharacterFlags.NoGrab, CharacterDef.FromJs("shiden").flags);
            Assert.AreEqual(CharacterFlags.Armour, CharacterDef.FromJs("garan").flags);
            Assert.AreEqual(395f, CharacterDef.FromJs("garan").jsHealth);
            Assert.AreEqual(CharacterFlags.None, CharacterDef.FromJs("order_footman").flags);
            Assert.AreEqual("Order Footman", CharacterDef.FromJs("order_footman").displayName);
            Assert.IsNull(CharacterDef.FromJs("nobody"));
        }

        [Test]
        public void TheCatalogHoldsKageTheRoninAndTheHeavyDummy()
        {
            CollectionAssert.IsSupersetOf(CharacterCatalog.Ids, new[] { "kage", "ronin", "test_heavy" });
            foreach (var id in CharacterCatalog.Ids)
            {
                var d = CharacterCatalog.Get(id);
                Assert.AreEqual(id, d.id);
                Assert.IsNotNull(CharacterCatalog.PartSet(d.partSet), $"{id}: part set {d.partSet}");
            }
            Assert.IsNull(CharacterCatalog.Get("nobody"));
        }

        [Test]
        public void GetReturnsACopy()
        {
            var a = CharacterCatalog.Get("ronin");
            a.jsHealth = 1f;
            a.abilities.Add("x");
            var b = CharacterCatalog.Get("ronin");
            Assert.AreEqual(232f, b.jsHealth);
            CollectionAssert.IsEmpty(b.abilities);
        }

        [Test]
        public void KageCanUseTheRageArtAndKunaiAndTheRoninCant()
        {
            var kage = CharacterCatalog.Get("kage");
            Assert.AreEqual(CharacterFlags.CanRageArt | CharacterFlags.CanThrowKunai, kage.flags);
            Assert.IsTrue(kage.IsPlayer);
            var ronin = CharacterCatalog.Get("ronin");
            Assert.AreEqual(CharacterFlags.None, ronin.flags);
            Assert.IsFalse(ronin.IsPlayer);
        }

        [Test]
        public void TheRoninRecolourIsData()
        {
            var ronin = CharacterCatalog.Get("ronin");
            Assert.AreEqual("ronin", ronin.partSet);
            Assert.AreEqual(1.04f, ronin.bodyScale);
            Assert.AreEqual(Palette.Amber, ronin.colors.main);
            Assert.AreEqual(Palette.AmberDeep, ronin.colors.dark);
            Assert.AreEqual(1f, ronin.mass);
            Assert.AreEqual(3f, CharacterCatalog.Get("test_heavy").mass);
        }

        [Test]
        public void APartSetDrawsItsTrimInTheCharactersColours()
        {
            var kage = CharacterCatalog.Get("kage");
            var lapel = CharacterCatalog.PartSet("kage").Build(kage).parts.Find(p => p.name == "Lapel");
            Assert.AreEqual(Palette.Trim, lapel.color);

            kage.colors = new FighterColors(Color.cyan, Color.white, Color.blue);
            var recoloured = CharacterCatalog.PartSet("kage").Build(kage);
            Assert.AreEqual(Color.cyan, recoloured.parts.Find(p => p.name == "Lapel").color);
            Assert.AreEqual(Color.blue, recoloured.parts.Find(p => p.name == "Collar").color);
        }
    }
}
