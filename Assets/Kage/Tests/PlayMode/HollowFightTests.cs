using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Shade and the tutorial Hollow in a fight (SAN-76), through the fight harness: spawned from their catalog
    /// entries, on the one katana move set, the tutorial Hollow's slow and rare attacks, and a scripted K.O. for each.
    /// </summary>
    public class HollowFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(string id, float kageX = -0.37f, float foeX = 0.37f, Foe foe = Foe.Scripted)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, foe, kageX, foeX, foeDef: CharacterCatalog.Get(id));
            _fights.Add(f);
            return f;
        }

        [Test]
        public void TheyAreSpawnedFromCharacterDataOnTheSharedKatanaSet()
        {
            var sf = Duel("hollow_shade");
            var shade = sf.Ronin;
            Assert.AreSame(sf.Kage.moves, shade.moves, "the Shade plays the one katana move set");
            Assert.AreEqual("hollow_shade", shade.FighterId);
            Assert.AreEqual(160f * 1.4f * 100f / 232f, shade.maxHealth, 1e-3);
            Assert.AreEqual(0.85f, shade.DamageMul, 1e-6);
            Assert.AreEqual("katana", shade.Family.id);
            var f = Duel("hollow_tutorial");
            Assert.AreEqual(95f * 100f / 232f, f.Ronin.maxHealth, 1e-3);
            Assert.AreEqual(0.3f, f.Ronin.DamageMul, 1e-6);
            Assert.AreSame(f.Kage.moves, f.Ronin.moves, "the one katana move set, not a copy");
        }

        [Test]
        public void TheTutorialHollowsAttacksPlaySlowerThanTheShades()
        {
            int Frames(string id)
            {
                var f = Duel(id, -3f, 3f);
                f.KageInput.Add(600);
                f.RoninInput.Add(1, buttons: "S").Add(600);
                int n = 0;
                for (int i = 0; i < 300; i++)
                {
                    f.Run(1);
                    if (f.Ronin.state == FState.Attack) n++;
                    else if (n > 0) break;
                }
                return n;
            }
            Assert.Greater(Frames("hollow_tutorial"), Frames("hollow_shade") * 1.2f);
        }

        /// <summary>How many separate attacks the AI starts in 1800 frames against a Kage who stands there and cannot die.</summary>
        int AttackStarts(string id)
        {
            var f = Duel(id, -0.6f, 0.6f, Foe.Ai);
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            f.KageInput.Add(2000);
            int starts = 0;
            bool was = false;
            for (int i = 0; i < 1800; i++)
            {
                f.Run(1);
                bool a = f.Ronin.state == FState.Attack;
                if (a && !was) starts++;
                was = a;
                if (f.Ronin.state == FState.KO) break;
            }
            return starts;
        }

        [Test]
        public void TheTutorialHollowAttacksRarely()
        {
            int shade = AttackStarts("hollow_shade"), tutorial = AttackStarts("hollow_tutorial");
            Assert.Greater(shade, 8, "the Shade is a regular fighter");
            Assert.Greater(tutorial, 0, "the tutorial Hollow does attack");
            Assert.Less(tutorial, shade * 0.5f, $"tutorial {tutorial} attacks against the Shade's {shade}");
        }

        [TestCase("hollow_shade")]
        [TestCase("hollow_tutorial")]
        public void KageDefeatsEachHollowWithScriptedInput(string id)
        {
            var f = Duel(id);
            f.RoninInput.Add(3000);   // it stands and takes it
            for (int i = 0; i < 40; i++) f.KageInput.Add(1, buttons: "S").Add(45).Add(10, fwd: 1);
            f.RunUntil(() => f.Ronin.state == FState.KO, 2400);

            Assert.AreEqual(FState.KO, f.Ronin.state, $"{id} is still on {f.Ronin.health:0.0} health");
            var ko = f.Events(CombatEventType.KnockedOut).Single();
            Assert.AreEqual("kage", ko.SourceId);
            Assert.AreEqual(id, ko.TargetId);
            Assert.IsNotNull(f.Ronin.FallBody(B.Hips), "the K.O. throws a ragdoll");
        }
    }
}
