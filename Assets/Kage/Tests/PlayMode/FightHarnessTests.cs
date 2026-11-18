using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The fight harness seam: a fight built from code with no game bootstrap, driven by scripted
    /// input on a manual clock, observed only through the event stream and the fighters' public state.
    /// </summary>
    public class FightHarnessTests
    {
        HeadlessFight _fight;

        [TearDown]
        public void TearDown()
        {
            _fight?.Dispose();
            _fight = null;
        }

        HeadlessFight Fight(int seed, Foe foe, float kageX = HeadlessFight.KageX, float roninX = 0.6f, float meter = 0f) =>
            _fight = new HeadlessFight(seed, foe, kageX, roninX, meter);

        [Test]
        public void BuildsKageAndTheBagWithNoGameBootstrap()
        {
            var f = Fight(61, Foe.Bag).Run(30);

            Assert.IsNull(KageGame.I, "no dev arena");
            Assert.IsNull(Vfx.I, "no VFX");
            Assert.AreEqual(1, f.Ctx.Fighters.Count);
            Assert.AreEqual(1, f.Ctx.Props.Count);
            Assert.AreEqual("kage", f.Kage.FighterId);
            Assert.AreSame(f.Bag, f.Kage.extraTarget);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.IsTrue(f.Kage.Grounded, "Kage stands on the ground");
            Assert.AreEqual(HeadlessFight.KageX, f.Kage.Pos.x, 1e-4f);
            Assert.AreEqual(30, f.Ctx.Clock.Frame);
        }

        [Test]
        public void BuildsKageAndTheRoninOnOpposingTeams()
        {
            var f = Fight(61, Foe.Scripted).Run(10);

            Assert.AreEqual("ronin", f.Ronin.FighterId);
            Assert.AreEqual(0, f.Kage.team);
            Assert.AreEqual(1, f.Ronin.team);
            Assert.AreSame(f.Ronin, f.Kage.opponent);
            Assert.AreSame(f.Kage, f.Ronin.opponent);
            Assert.AreSame(f.Ctx, f.Ronin.ctx);
        }

        [Test]
        public void TheFirstEventIsFightStartedWithTheSeed()
        {
            var f = Fight(1234, Foe.Bag);

            var first = f.Log.All[0];
            Assert.AreEqual(CombatEventType.FightStarted, first.Type);
            Assert.AreEqual(1234, first.Index);
            Assert.AreEqual(0, first.Frame);
        }

        [Test]
        public void AJabOnTheBagPublishesMoveStartedThenHit()
        {
            var f = Fight(61, Foe.Bag, kageX: 0.95f);
            f.KageInput.Add(1, buttons: "P");
            f.Run(40);

            var started = f.Events(CombatEventType.MoveStarted).Single();
            Assert.AreEqual("kage", started.SourceId);
            Assert.AreEqual("jab", started.MoveId);

            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreEqual("kage", hit.SourceId);
            Assert.AreEqual("bag", hit.TargetId);
            Assert.IsNull(hit.Target, "the bag is a prop, not a fighter");
            Assert.AreEqual("jab", hit.MoveId);
            Assert.AreEqual(0, hit.Window);
            Assert.AreEqual(HitLevel.High, hit.Level);
            Assert.AreEqual(hit.Crit ? 6f : 4f, hit.Damage);    // jab: 4, a crit ×1.5 rounded
            Assert.That(hit.Frame, Is.InRange(started.Frame + 6, started.Frame + 9), "inside the jab's window (frames 6-9)");
            Assert.AreEqual(hit.Damage, f.Bag.totalDamage);
        }

        [Test]
        public void AGuardedHitPublishesBlockedAndTheOnHitCallbackStillFires()
        {
            var f = Fight(61, Foe.Scripted, kageX: -0.4f, roninX: 0.4f);
            f.RoninInput.Add(200, dy: -1, buttons: "G");      // low guard
            f.KageInput.Add(10).Add(1, dy: -1, buttons: "K");   // leg sweep (low)
            var results = new List<HitResult>();
            f.Kage.onHit += (att, def, info, res) => results.Add(res);
            f.Run(60);

            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.AreEqual("kage", blocked.SourceId);
            Assert.AreEqual("ronin", blocked.TargetId);
            Assert.AreEqual("sweep", blocked.MoveId);
            Assert.AreEqual(0f, blocked.Damage, "a kick does no chip damage");
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
            CollectionAssert.AreEqual(new[] { HitResult.Blocked }, results);
        }

        [Test]
        public void AFinishingHitPublishesKnockedOutAndTheOnKOCallbackStillFires()
        {
            var f = Fight(61, Foe.Scripted, kageX: -0.4f, roninX: 0.4f);
            f.Ronin.health = 1f;
            f.RoninInput.Add(300);
            f.KageInput.Add(1, dy: -1, buttons: "K");
            Fighter knockedOut = null;
            f.Ronin.onKO += who => knockedOut = who;
            f.Run(40);

            var ko = f.Events(CombatEventType.KnockedOut).Single();
            Assert.AreEqual("kage", ko.SourceId);
            Assert.AreEqual("ronin", ko.TargetId);
            Assert.AreEqual("sweep", ko.MoveId);
            Assert.AreSame(f.Ronin, knockedOut);
            Assert.AreEqual(FState.KO, f.Ronin.state);
            Assert.AreEqual(1f, f.Events(CombatEventType.Hit).Single().Damage);
        }

        [Test]
        public void MirrorGuardPublishesCounteredAndStartsTheCounterSlash()
        {
            var f = Fight(61, Foe.Scripted, kageX: -0.38f, roninX: 0.38f);
            f.KageInput.Add(1, fwd: -1, buttons: "S");   // Mirror Guard
            f.RoninInput.Add(2).Add(1, buttons: "P").Add(300);
            f.Run(60);

            var countered = f.Events(CombatEventType.Countered).Single();
            Assert.AreEqual("ronin", countered.SourceId, "the attacker whose move was caught");
            Assert.AreEqual("kage", countered.TargetId, "the fighter whose counter window caught it");
            Assert.AreEqual("jab", countered.MoveId);
            var kageMoves = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.AreEqual(new[] { "counterStance", "counterSlash" }, kageMoves);
        }

        [Test]
        public void ThousandShadowsPublishesRageArtThenItsCuts()
        {
            var f = Fight(61, Foe.Scripted, kageX: -0.6f, roninX: 0.6f, meter: 100f);
            f.RoninInput.Add(400);
            f.KageInput.Add(1, dy: -1, buttons: "X");
            f.Run(140);

            var rage = f.Events(CombatEventType.RageArt).Single();
            Assert.AreEqual("kage", rage.SourceId);
            Assert.AreEqual(Fighter.ThousandShadows, rage.MoveId);
            var cuts = f.Events(CombatEventType.Hit).Where(e => e.MoveId == Fighter.ThousandShadows).ToList();
            Assert.AreEqual(6, cuts.Count, "five cuts and the final blow");
            Assert.IsTrue(cuts.All(e => e.Frame > rage.Frame));
            Assert.AreEqual(4f * 5 + 14f, cuts.Sum(e => e.Damage));
        }
    }
}
