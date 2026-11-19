using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// Ability slots (SAN-95, ADR-0004, ADR-0008): <c>FrameInput.ability</c> = n starts the n-th ability of the character, through the
    /// same input interface as a button; a profile-driven AI chooses them like button presses. Tested with a test ability, a plain
    /// <see cref="MoveDef"/> registered in the <see cref="AbilityCatalog"/>.
    /// </summary>
    public class ProfileAiAbilityTests
    {
        readonly List<HeadlessFight> _fights = new();

        [SetUp]
        public void SetUp() => AbilityCatalog.Register(new AbilityDef { id = SlamId, move = SlamMove(), cooldownFrames = 120, range = new Vector2(0f, 3f), chance = 1f });

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Unregister();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(string profile = "regular", float foeX = 0.9f, bool withAbility = true)
        {
            var foe = Footman(profile);
            if (withAbility) foe.abilities = new List<string> { "test.other", SlamId };      // the test ability is slot 2
            var f = new HeadlessFight(Seed, Foe.Scripted, -0.9f, foeX, foeDef: foe);
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            f.Ronin.maxHealth = f.Ronin.health = 1e7f;
            f.KageInput.Add(4000);
            _fights.Add(f);
            return f;
        }

        const int Seed = 95;

        static List<int> Starts(HeadlessFight f) => f.Events(CombatEventType.MoveStarted).Where(e => e.MoveId == SlamId).Select(e => e.Frame).ToList();

        [Test]
        public void AbilitySlotTwoStartsTheSecondAbilitysMove()
        {
            var f = Duel();
            f.RoninInput.Add(1, ability: 2).Add(200);
            f.Run(5);
            Assert.AreEqual(1, Starts(f).Count, "the slot maps to the test move");
            Assert.AreEqual(SlamId, f.Ronin.move?.id);
            Assert.IsTrue(f.Ronin.IsAttacking);
        }

        [Test]
        public void SlotOneIsAnAbilityWithNoMoveSoNothingStarts()
        {
            var f = Duel();
            f.RoninInput.Add(1, ability: 1).Add(200);
            f.Run(60);
            Assert.AreEqual(0, Starts(f).Count);
            Assert.IsFalse(f.Ronin.IsAttacking);
        }

        [Test]
        public void ZeroAndOutOfRangeSlotsDoNothing()
        {
            var f = Duel();
            f.RoninInput.Add(1, ability: 0).Add(5).Add(1, ability: 3).Add(5).Add(1, ability: 99).Add(100);
            f.Run(60);
            Assert.AreEqual(0, Starts(f).Count);
        }

        [Test]
        public void ARequestWhileTheAbilityCoolsDownIsIgnoredUntilTheCooldownEnds()
        {
            var f = Duel();
            f.RoninInput.Add(1, ability: 2).Add(60).Add(1, ability: 2).Add(80)      // the second press, 61 frames in: cooling down
                        .Add(1, ability: 2).Add(200);                                  // the third, 142 frames in: ready again
            f.Run(300);
            var s = Starts(f);
            Assert.AreEqual(2, s.Count, "pressed three times, two started");
            Assert.GreaterOrEqual(s[1] - s[0], 120, "not before the cooldown (120 frames) is over");
        }

        [Test]
        public void ADisabledAbilityIsIgnored()
        {
            var f = Duel();
            f.Ronin.SetEnabledAbilities(new[] { "test.other" });
            f.RoninInput.Add(1, ability: 2).Add(100);
            f.Run(60);
            Assert.AreEqual(0, Starts(f).Count, "the phase doesn't allow it");
            f.Ronin.SetEnabledAbilities(null);
            f.RoninInput.Add(1, ability: 2).Add(100);
            f.Run(60);
            Assert.AreEqual(1, Starts(f).Count, "allowed again");
        }

        [Test]
        public void ABusyFighterIgnoresTheRequest()
        {
            var f = Duel();
            f.RoninInput.Add(1, buttons: "S").Add(3).Add(1, ability: 2).Add(100);       // in the middle of a sword move
            f.Run(50);
            Assert.AreEqual(0, Starts(f).Count, "abilities start from neutral, like any move");
        }

        [Test]
        public void ThePlayerSideCanUseAbilitiesToo()
        {
            // the slot is on the input interface, not on foes: a scripted Kage with an ability starts it
            AbilityCatalog.Register(new AbilityDef { id = "test.kage", move = SlamMove(), cooldownFrames = 0 });
            try
            {
                var kage = CharacterCatalog.Get("kage");
                kage.abilities = new List<string> { "test.kage" };
                var f = new HeadlessFight(Seed, Foe.Bag, kageDef: kage);
                _fights.Add(f);
                f.KageInput.Add(1, ability: 1).Add(60);
                f.Run(30);
                Assert.AreEqual(1, f.Events(CombatEventType.MoveStarted).Count(e => e.SourceId == "kage" && e.MoveId == SlamId));
            }
            finally { AbilityCatalog.Unregister("test.kage"); }
        }

        // ---------------------------------------------------------------- chosen by the AI

        [Test]
        public void AProfiledFoeChoosesItsAbilityInRangeAndOffCooldown()
        {
            var f = new HeadlessFight(Seed, Foe.Ai, -0.9f, 0.9f, foeDef: WithAbility("regular"));
            _fights.Add(f);
            f.Kage.maxHealth = f.Kage.health = 1e7f; f.Ronin.maxHealth = f.Ronin.health = 1e7f;
            f.KageInput.Add(2000);
            f.Run(1800);
            var s = Starts(f);
            Assert.GreaterOrEqual(s.Count, 5, "chosen like a button, whenever it is ready");
            for (int i = 1; i < s.Count; i++) Assert.GreaterOrEqual(s[i] - s[i - 1], 120, "never inside the cooldown");
        }

        static CharacterDef WithAbility(string profile)
        {
            var d = Footman(profile);
            d.abilities = new List<string> { "test.other", SlamId };
            return d;
        }

        [Test]
        public void AnAbilityOutsideItsDistanceBandIsNotChosen()
        {
            AbilityCatalog.Register(new AbilityDef { id = SlamId, move = SlamMove(), cooldownFrames = 60, range = new Vector2(6f, 9f), chance = 1f });
            var f = new HeadlessFight(Seed, Foe.Ai, -0.9f, 0.9f, foeDef: WithAbility("regular"));
            _fights.Add(f);
            f.Kage.maxHealth = f.Kage.health = 1e7f; f.Ronin.maxHealth = f.Ronin.health = 1e7f;
            f.KageInput.Add(1000);
            f.Run(900);
            Assert.AreEqual(0, Starts(f).Count, "the fighters stay under 6 m apart");
        }

        [Test]
        public void AnAbilityWithAChanceOfZeroIsNeverChosen()
        {
            AbilityCatalog.Register(new AbilityDef { id = SlamId, move = SlamMove(), cooldownFrames = 60, range = new Vector2(0f, 9f), chance = 0f });
            var f = new HeadlessFight(Seed, Foe.Ai, -0.9f, 0.9f, foeDef: WithAbility("boss"));
            _fights.Add(f);
            f.Kage.maxHealth = f.Kage.health = 1e7f; f.Ronin.maxHealth = f.Ronin.health = 1e7f;
            f.KageInput.Add(1000);
            f.Run(900);
            Assert.AreEqual(0, Starts(f).Count);
        }

        [Test]
        public void ARoninWithAbilitiesAndTheLegacyProfileNeverPressesOne()
        {
            var d = CharacterCatalog.Get("ronin");
            d.abilities = new List<string> { SlamId };
            var f = new HeadlessFight(Seed, Foe.Ai, -0.9f, 0.9f, foeDef: d);
            _fights.Add(f);
            f.KageInput.Add(1000);
            f.Run(900);
            Assert.AreEqual(0, Starts(f).Count, "the legacy AI has no ability slots (the Ronin has none)");
        }
    }
}
