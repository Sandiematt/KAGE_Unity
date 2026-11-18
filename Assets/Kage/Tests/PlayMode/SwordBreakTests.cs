using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Sword break (SAN-113): a guarded hit from a <c>breaksWeapon</c> window shatters the katana; Kage is
    /// then unarmed and punches with both hands until the next fight restores the sword.
    /// </summary>
    public class SwordBreakTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.RoninInput.Add(1200);
            _fights.Add(f);
            return f;
        }

        static HitInfo Heavy(HeadlessFight f, bool breaksWeapon) => new()
        {
            attacker = f.Ronin, dmg = 10, kb = new Vector2(-1.5f, 0f), zone = HitZone.Body,
            contact = f.Kage.Pos + Vector2.up, moveId = "test_heavy", window = 0,
            w = new HitWindow { dmg = 10, level = HitLevel.Mid, kb = new Vector2(1.5f, 0f), heavy = true, breaksWeapon = breaksWeapon },
        };

        /// <summary>Kage holds back (guard) for a few frames, then takes the hit.</summary>
        static HitResult GuardedHit(HeadlessFight f, bool breaksWeapon)
        {
            f.KageInput.Add(20, fwd: -1);
            f.Run(6);
            return f.Kage.ReceiveHit(Heavy(f, breaksWeapon));
        }

        static bool SwordShown(Fighter k) => k.rig.SwordVisible;

        [Test]
        public void AFightStartsWithTheSwordWhole()
        {
            var f = Duel();
            Assert.IsFalse(f.Kage.SwordBroken);
            Assert.IsTrue(SwordShown(f.Kage));
        }

        [Test]
        public void AGuardedBreaksWeaponHitShattersTheSword()
        {
            var f = Duel();
            Assert.AreEqual(HitResult.Blocked, GuardedHit(f, breaksWeapon: true));
            Assert.IsTrue(f.Kage.SwordBroken);
            Assert.IsFalse(SwordShown(f.Kage), "the katana is gone from the rig");
        }

        [Test]
        public void AGuardedHitWithoutTheFlagLeavesTheSword()
        {
            var f = Duel();
            Assert.AreEqual(HitResult.Blocked, GuardedHit(f, breaksWeapon: false));
            Assert.IsFalse(f.Kage.SwordBroken);
        }

        [Test]
        public void ABreaksWeaponHitThatIsNotGuardedLeavesTheSword()
        {
            var f = Duel();
            f.Run(6);
            Assert.AreEqual(HitResult.Hit, f.Kage.ReceiveHit(Heavy(f, breaksWeapon: true)));
            Assert.IsFalse(f.Kage.SwordBroken, "only a guarded hit breaks the sword");
        }

        [Test]
        public void WithTheSwordBrokenTheSecondPunchIsTheOtherHand()
        {
            var f = Duel();
            f.Kage.BreakSword();
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P");
            f.Run(40);
            var started = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.AreEqual(new[] { "jab", "crossFist" }, started);
            var hits = f.Events(CombatEventType.Hit).Concat(f.Events(CombatEventType.Blocked)).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.Contains(hits, "crossFist", "the far fist connects");
        }

        [Test]
        public void TheRearCrossPunchesWithTheFarHand()
        {
            var f = Duel();
            f.Kage.BreakSword();
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P");
            f.RunUntil(() => f.Kage.move != null && f.Kage.move.id == "crossFist" && f.Kage.moveFrame == 9, 40);
            float x = f.Kage.Pos.x;
            Assert.Greater(f.Kage.rig.World(B.HandF, Vector2.zero).x - x, 0.45f, "far fist thrown forward");
            Assert.Less(f.Kage.rig.World(B.HandN, Vector2.zero).x - x, 0.35f, "near fist back on guard");
        }

        [Test]
        public void WithTheSwordBrokenKatanaMovesDoNothing()
        {
            var f = Duel();
            f.Kage.BreakSword();
            f.KageInput.Add(1, buttons: "S").Add(20).Add(1, dy: -1, buttons: "S").Add(20).Add(1, buttons: "X");
            f.Run(70);
            Assert.IsEmpty(f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage"));
        }

        [Test]
        public void TheSwordStaysBrokenForTheRestOfTheFightAndIsBackForTheNext()
        {
            var f = Duel();
            f.Kage.BreakSword();
            f.Kage.ResetForRound(-0.37f, 1f);
            Assert.IsTrue(f.Kage.SwordBroken, "a new round of the same fight keeps it broken");
            f.Kage.ResetForFight();
            Assert.IsFalse(f.Kage.SwordBroken);
            Assert.IsTrue(SwordShown(f.Kage));
        }

        [Test]
        public void ABrokenSwordFallsBackToOneHandFreeAndNoGrip()
        {
            var f = Duel();
            f.Kage.BreakSword();
            f.Run(30);
            Assert.Greater(TwoHandedGripTests.NearFistToHandle(f.Kage), 0.1f, "no two-handed grip without a sword: fists up");
        }
    }

    /// <summary>The dev arena's sword-break hotkey (F7).</summary>
    public class SwordBreakHotkeyTests : InputTestFixture
    {
        GameObject _game;

        public override void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator F7BreaksThePlayersSword()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            Assert.IsFalse(game.player.SwordBroken);
            Press(kb.f7Key);
            yield return null;
            Release(kb.f7Key);
            yield return null;
            Assert.IsTrue(game.player.SwordBroken);
        }
    }
}
