using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Chainthrower in a fight (SAN-99): spawned from his catalog entry with the JS stats, the twin set and the agile
    /// profile; a stretch resolves him to the real character; a scripted Kage defeats him; a bot fight against him ends in a
    /// knock-out without a softlock. Each fight is disposed before the next is built.
    /// </summary>
    public class OrderChainthrowerFightTests
    {
        const string Id = "order_chainthrower";
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(float kageX = -0.37f, float foeX = 0.37f, Foe mode = Foe.Scripted)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, mode, kageX, foeX, foeDef: CharacterCatalog.Get(Id));
            _fights.Add(f);
            return f;
        }

        [Test]
        public void HeIsSpawnedFromCharacterDataWithTheTwinSetAndTheJsStats()
        {
            var foe = Duel().Ronin;
            Assert.AreEqual(Id, foe.FighterId);
            Assert.AreEqual(150f * 100f / 232f, foe.maxHealth, 1e-3);
            Assert.AreEqual(0.04f, foe.Defense, 1e-6);
            Assert.AreEqual(0.85f, foe.DamageMul, 1e-6);
            Assert.AreEqual(1f, foe.SpeedMul, 1e-6);
            Assert.AreEqual("chain", foe.Family.id);
            Assert.AreEqual("chain_hook", foe.WeaponId);
            Assert.IsTrue(foe.rig.FamilyPartCount > 0, "the sickle and its chain are drawn");
            Assert.IsTrue(foe.rig.BaseSwordHidden, "no katana drawn over it");
            Assert.IsFalse(foe.isPlayer);
        }

        [Test]
        public void HeBacksOffWhenKageClosesInAndLashesFromMidRange()
        {
            // Kage walks at him from 1.4 m: the ranged profile (preferred range 3.7 m) gives ground instead of trading, then throws chain moves
            var f = Duel(-1.2f, 0.2f, Foe.Ai);
            f.KageInput.Add(2400, fwd: 1);
            float foeStart = f.Ronin.Pos.x;
            int away = 0;
            for (int i = 0; i < 12; i++)
            {
                float x = f.Ronin.Pos.x;
                f.Run(20);
                if (f.Ronin.Pos.x > x + 0.02f) away++;
                Debug.Log($"SAN-99 t={i * 20} gap {f.Ronin.Pos.x - f.Kage.Pos.x:0.00} foe x {f.Ronin.Pos.x:0.00}");
            }
            Assert.Greater(f.Ronin.Pos.x, foeStart + 0.4f, "he backs off while Kage closes in");
            Assert.GreaterOrEqual(away, 3, "in several steps");
            f.KageInput.Add(2400);
            f.Run(2000);
            var chainMoves = f.Events(CombatEventType.MoveStarted).Where(e => e.Source == f.Ronin && e.MoveId != null && e.MoveId.StartsWith("chain.")).ToList();
            Assert.IsNotEmpty(chainMoves, "he used the chain set");
            Debug.Log("SAN-99 chain moves thrown: " + string.Join(",", chainMoves.Select(e => e.MoveId).Distinct()));
        }

        [Test]
        public void KageDefeatsTheChainthrowerWithScriptedInput()
        {
            var f = Duel();
            f.RoninInput.Add(3000);
            for (int i = 0; i < 40; i++) f.KageInput.Add(1, buttons: "S").Add(45).Add(10, fwd: 1);
            f.RunUntil(() => f.Ronin.state == FState.KO, 2400);
            Assert.AreEqual(FState.KO, f.Ronin.state, $"the Chainthrower is still on {f.Ronin.health:0.0} health");
            Assert.AreEqual(Id, f.Events(CombatEventType.KnockedOut).Single().TargetId);
            Assert.IsNotNull(f.Ronin.FallBody(B.Hips), "a ragdoll");
        }

        [Test]
        public void StretchWavesResolveTheChainthrowerToTheRealCharacter()
        {
            var def = StretchSession.ResolveFoe(Id);
            Assert.AreEqual(Id, def.id);
        }

        [Test]
        public void TheDevArenaHotkeyCyclesToTheChainthrower()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Id);
        }

        [Test]
        public void ABotFightAgainstTheChainthrowerEndsInAKnockOutWithoutASoftlock()
        {
            const int Cap = 7200, MaxQuiet = 600;
            var f = Duel(-0.8f, 0.8f, Foe.Ai);
            f.KageInput.fallback = new KageBot();
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);
            Assert.IsTrue(f.Kage.state == FState.KO || f.Ronin.state == FState.KO, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Chainthrower {f.Ronin.health:0.0})");
            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(e => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed))
            { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block or parry");
            Assert.Greater(f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), 3, "the fighters did meet");
            Debug.Log($"SAN-99 bot fight vs Chainthrower ended after {f.Frames.Count} frames, Kage {f.Kage.health:0} Chainthrower {f.Ronin.health:0}");
        }
    }
}
