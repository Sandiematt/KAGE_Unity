using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// AI profiles as data (SAN-95): one per JS archetype, read from the generated JS table and picked by character data. The
    /// expected numbers are the JS game's <c>AI_PROFILES</c> (10-data.js), written out as literals so a wrong conversion shows.
    /// How a profile makes a foe behave is in ProfileAiBehaviourTests (PlayMode).
    /// </summary>
    public class ProfileAiProfileTests
    {
        // id: pref px, aggr, block, parry, dodge, jump, punish, grab, kunai, dashIn, react s, combo
        static readonly object[][] Js =
        {
            new object[] { "regular",   125, 0.55f, 0.38f, 0.06f, 0.12f, 0.05f, 0.55f, 0.05f, 0.05f, 0.00f, 0.24f, 0.40f, 1, 3 },
            new object[] { "agile",     160, 0.62f, 0.18f, 0.05f, 0.38f, 0.20f, 0.70f, 0.03f, 0.06f, 0.35f, 0.15f, 0.27f, 2, 4 },
            new object[] { "defensive", 115, 0.34f, 0.62f, 0.16f, 0.08f, 0.02f, 0.85f, 0.20f, 0.02f, 0.00f, 0.17f, 0.30f, 1, 2 },
            new object[] { "ranged",    430, 0.42f, 0.32f, 0.04f, 0.28f, 0.10f, 0.45f, 0.02f, 0.55f, 0.00f, 0.20f, 0.34f, 1, 2 },
            new object[] { "heavy",     140, 0.50f, 0.12f, 0.00f, 0.03f, 0.00f, 0.45f, 0.00f, 0.00f, 0.00f, 0.32f, 0.52f, 1, 2 },
            new object[] { "boss",      150, 0.62f, 0.42f, 0.12f, 0.18f, 0.06f, 0.78f, 0.06f, 0.05f, 0.00f, 0.16f, 0.28f, 2, 4 },
        };

        [TestCaseSource(nameof(Js))]
        public void EachJsArchetypeIsAProfileWithTheJsNumbers(string id, int pref, float aggr, float block, float parry, float dodge, float jump,
            float punish, float grab, float kunai, float dashIn, float reactLo, float reactHi, int comboLo, int comboHi)
        {
            var p = AiProfiles.Get(id);
            Assert.IsNotNull(p, id);
            Assert.AreEqual(id, p.id);
            Assert.IsFalse(p.legacy);
            Assert.AreEqual(pref / 115f, p.preferredRange, 1e-4, "preferred range in metres (115 px a metre)");
            Assert.AreEqual(aggr, p.aggression, 1e-5);
            Assert.AreEqual(block, p.guardRate, 1e-5);
            Assert.AreEqual(parry, p.parryRate, 1e-5, "parry rate");
            Assert.AreEqual(dodge, p.dodgeRate, 1e-5);
            Assert.AreEqual(jump, p.jumpRate, 1e-5);
            Assert.AreEqual(punish, p.punishRate, 1e-5);
            Assert.AreEqual(grab, p.grabRate, 1e-5);
            Assert.AreEqual(kunai, p.kunaiRate, 1e-5);
            Assert.AreEqual(dashIn, p.dashInRate, 1e-5);
            Assert.AreEqual(Mathf.RoundToInt(reactLo * 60f), p.reactMinFrames, "reaction frames: seconds x 60");
            Assert.AreEqual(Mathf.RoundToInt(reactHi * 60f), p.reactMaxFrames);
            Assert.AreEqual(comboLo, p.comboMin);
            Assert.AreEqual(comboHi, p.comboMax);
            Assert.AreEqual(id == "ranged", p.ranged);
            Assert.AreEqual(id == "heavy", p.heavy);
        }

        [Test]
        public void TheParryRatesAreTheJsParryColumn()
        {
            Assert.AreEqual(0.06f, AiProfiles.Get("regular").parryRate, 1e-6);
            Assert.AreEqual(0.05f, AiProfiles.Get("agile").parryRate, 1e-6);
            Assert.AreEqual(0.16f, AiProfiles.Get("defensive").parryRate, 1e-6);
            Assert.AreEqual(0.04f, AiProfiles.Get("ranged").parryRate, 1e-6);
            Assert.AreEqual(0f, AiProfiles.Get("heavy").parryRate, 1e-6);
            Assert.AreEqual(0.12f, AiProfiles.Get("boss").parryRate, 1e-6);
        }

        [Test]
        public void ThereIsOneProfileForEveryArchetypeThatAFoeUses()
        {
            // every AI archetype named by a JS foe has a profile (so a foe ticket never finds its profile missing)
            foreach (var e in Kage.Rules.JsTables.Enemies)
                Assert.IsNotNull(AiProfiles.Get(e.Ai), $"{e.Id} uses the '{e.Ai}' archetype");
        }

        [Test]
        public void AProfileFromTheCatalogIsACopy()
        {
            var a = AiProfiles.Get("agile");
            a.aggression = 0.99f;
            Assert.AreEqual(0.62f, AiProfiles.Get("agile").aggression, 1e-6);
        }

        [Test]
        public void AnUnknownOrMissingProfileIsNull()
        {
            Assert.IsNull(AiProfiles.Get(null));
            Assert.IsNull(AiProfiles.Get("no_such_profile"));
        }

        [Test]
        public void TheRoninKeepsTheLegacyProfileSoItsBehaviourIsUnchanged()
        {
            Assert.AreEqual(AiProfiles.LegacyId, CharacterCatalog.Get("ronin").aiProfile);
            Assert.IsTrue(AiProfiles.Get(AiProfiles.LegacyId).legacy);
        }

        [TestCase("order_footman", "regular")]
        [TestCase("hollow_shade", "regular")]
        [TestCase("hollow_tutorial", "regular")]
        [TestCase("raikon", "boss")]
        public void AFoeSelectsItsProfileThroughCharacterData(string characterId, string profileId)
        {
            var def = CharacterCatalog.Get(characterId);
            Assert.AreEqual(profileId, def.aiProfile);
            var ai = new AIInputSource();
            ai.Configure(def);
            Assert.IsNotNull(ai.profile);
            Assert.AreEqual(profileId, ai.profile.id);
            Assert.AreEqual(ai.profile.parryRate, ai.parryRate, 1e-6, "the AI's parry rate comes from the profile");
        }

        [Test]
        public void SwitchingTheFootmansProfileSelectsAnotherProfile()
        {
            var def = CharacterCatalog.Get("order_footman");
            def.aiProfile = "defensive";
            var ai = new AIInputSource();
            ai.Configure(def);
            Assert.AreEqual("defensive", ai.profile.id);
            Assert.AreEqual(0.16f, ai.parryRate, 1e-6);
        }

        [Test]
        public void ACharactersOwnParryRateOverridesTheProfilesAndAttackRateStillApplies()
        {
            var def = CharacterCatalog.Get("order_footman");
            def.parryRate = 0.5f;
            def.attackRate = 0.3f;
            var ai = new AIInputSource();
            ai.Configure(def);
            Assert.AreEqual(0.5f, ai.parryRate, 1e-6);
            Assert.AreEqual(0.3f, ai.attackRate, 1e-6);
        }

        [Test]
        public void TheLegacyProfileTakesNoRatesFromTheProfile()
        {
            var ai = new AIInputSource();
            ai.Configure(CharacterCatalog.Get("ronin"));
            Assert.IsTrue(ai.profile.legacy);
            Assert.AreEqual(0f, ai.parryRate, 1e-6, "the Ronin never parries, as before");
        }

        [Test]
        public void TheShippedAiTuningMatchesItsDefaults()
        {
            var file = Tuning.Get<AiTuning>();
            var code = new AiTuning();
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(code), UnityEngine.JsonUtility.ToJson(file), "ai.json matches the AiTuning defaults");
            Assert.AreEqual(1f, file.levelBase + file.levelSlope * 0.6f, 1e-5, "the default stage level 0.6 leaves a profile's numbers as they are");
        }

        [Test]
        public void AFoeWithAbilitiesKeepsThemInSlotOrder()
        {
            // the JS ability list is the slot order (ADR-0008); Raikon's is from the generated table
            var raikon = CharacterCatalog.Get("raikon");
            CollectionAssert.AreEqual(Kage.Rules.JsTables.Enemy("raikon").Abilities.ToList(), raikon.abilities);
        }
    }
}
