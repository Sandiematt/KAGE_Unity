using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// The determinism contract (ADR-0002): with the same seed, the same scripted inputs and a manual
    /// clock, a fight produces an identical event log and identical positions.
    /// </summary>
    public class DeterminismTests
    {
        [Test]
        public void HitstopFreezesTheFightForTheWindowsHitstopFrames()
        {
            using var f = new HeadlessFight(61, Foe.Bag, kageX: 0.95f);
            f.KageInput.Add(1, buttons: "P");
            f.RunUntil(() => f.Log.Count(CombatEventType.Hit) > 0);
            var hit = f.Events(CombatEventType.Hit).Single();
            int frozenAt = f.Kage.moveFrame;
            int expected = 4 + (hit.Crit ? 3 : 0);    // the jab's hitstop, +3 on a crit

            for (int i = 0; i < expected; i++)
            {
                Assert.IsFalse(f.Loop.StepHeadless(), $"frame {i + 1} after the hit is frozen");
                Assert.AreEqual(frozenAt, f.Kage.moveFrame);
            }
            Assert.IsTrue(f.Loop.StepHeadless(), "the fight resumes");
            Assert.AreEqual(frozenAt + 1, f.Kage.moveFrame);
        }

        [Test]
        public void TheSameSeedAndScriptGiveAnIdenticalFight()
        {
            var a = Duel(61, unityRandomSeed: 1);
            var b = Duel(61, unityRandomSeed: 999);

            Assert.Greater(a.events.Count(l => l.Contains(" Hit ")), 0, "the duel lands hits");
            CollectionAssert.AreEqual(a.events, b.events);
            CollectionAssert.AreEqual(a.frames, b.frames);
        }

        [Test]
        public void AiChoicesComeFromTheSeed()
        {
            var a = Duel(1, unityRandomSeed: 5);
            var b = Duel(2, unityRandomSeed: 5);

            CollectionAssert.AreNotEqual(a.frames, b.frames);
        }

        [Test]
        public void CritsComeFromTheSeedNotFromUnityRandom()
        {
            var crits = new List<bool>();
            for (int seed = 1; seed <= 40; seed++)
            {
                bool first = JabCrit(seed, unityRandomSeed: seed);
                bool second = JabCrit(seed, unityRandomSeed: 1000 + seed);
                Assert.AreEqual(first, second, $"seed {seed}");
                crits.Add(first);
            }
            Assert.That(crits, Has.Some.True, "some seeds crit");
            Assert.That(crits, Has.Some.False, "some seeds don't");
        }

        static bool JabCrit(int seed, int unityRandomSeed)
        {
            UnityEngine.Random.InitState(unityRandomSeed);
            using var f = new HeadlessFight(seed, Foe.Bag, kageX: 0.95f);
            f.KageInput.Add(1, buttons: "P");
            f.Run(30);
            return f.Events(CombatEventType.Hit).Single().Crit;
        }

        /// <summary>Kage plays a fixed combo script against the AI.</summary>
        static (List<string> events, List<string> frames) Duel(int seed, int unityRandomSeed)
        {
            UnityEngine.Random.InitState(unityRandomSeed);
            using var f = new HeadlessFight(seed, Foe.Ai, kageX: -0.5f, roninX: 0.5f, kageMeter: 50f);
            f.KageInput
                .Add(4).Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(9).Add(1, buttons: "P")
                .Add(30).Add(1, dy: -1, buttons: "K")
                .Add(40).Add(1, buttons: "K").Add(10).Add(1, buttons: "K")
                .Add(40).Add(12, fwd: 1, dy: 1).Add(1, buttons: "K")
                .Add(40).Add(1, buttons: "X");
            f.Run(360);
            return (f.Log.Lines().ToList(), f.Frames.ToList());
        }
    }
}
