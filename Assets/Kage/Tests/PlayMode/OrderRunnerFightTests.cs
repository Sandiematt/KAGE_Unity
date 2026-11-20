using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Runner in a fight (SAN-97): spawned from his catalog entry with the JS stats, the twin set and the agile
    /// profile; a stretch resolves him to the real character; a scripted Kage defeats him; a bot fight against him ends in a
    /// knock-out without a softlock. Each fight is disposed before the next is built.
    /// </summary>
    public class OrderRunnerFightTests
    {
        const string Id = "order_runner";
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
            Assert.AreEqual(150f * 1.35f * 100f / 232f, foe.maxHealth, 1e-3);
            Assert.AreEqual(0.03f, foe.Defense, 1e-6);
            Assert.AreEqual(0.85f, foe.DamageMul, 1e-6);
            Assert.AreEqual(1.12f, foe.SpeedMul, 1e-6);
            Assert.AreEqual("twin", foe.Family.id);
            Assert.AreEqual("shadow_daggers", foe.WeaponId);
            Assert.IsTrue(foe.rig.FamilyPartCount > 0, "the twin daggers are drawn");
            Assert.IsTrue(foe.rig.BaseSwordHidden, "no katana drawn over them");
            Assert.IsFalse(foe.isPlayer);
        }

        [Test]
        public void HeIsFasterThanKageOnTheGround()
        {
            float Distance(CharacterDef def)
            {
                var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 3f, foeDef: def);
                _fights.Add(f);
                f.KageInput.Add(300);
                f.RoninInput.Add(60, fwd: 1);
                float x0 = f.Ronin.Pos.x;
                f.Run(60);
                float d = Mathf.Abs(f.Ronin.Pos.x - x0);
                f.Dispose(); _fights.Remove(f);
                return d;
            }
            Assert.Greater(Distance(CharacterCatalog.Get(Id)), Distance(CharacterCatalog.Get("order_footman")), "1.12 against 1.0 speed");
        }

        [Test]
        public void KageDefeatsTheRunnerWithScriptedInput()
        {
            var f = Duel();
            f.RoninInput.Add(3000);
            for (int i = 0; i < 40; i++) f.KageInput.Add(1, buttons: "S").Add(45).Add(10, fwd: 1);
            f.RunUntil(() => f.Ronin.state == FState.KO, 2400);
            Assert.AreEqual(FState.KO, f.Ronin.state, $"the Runner is still on {f.Ronin.health:0.0} health");
            Assert.AreEqual(Id, f.Events(CombatEventType.KnockedOut).Single().TargetId);
            Assert.IsNotNull(f.Ronin.FallBody(B.Hips), "a ragdoll");
        }

        [Test]
        public void StretchWavesResolveTheRunnerToTheRealCharacter()
        {
            var def = StretchSession.ResolveFoe(Id);
            Assert.AreEqual(Id, def.id);
        }

        [Test]
        public void TheDevArenaHotkeyCyclesToTheRunner()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Id);
        }

        [Test]
        public void ABotFightAgainstTheRunnerEndsInAKnockOutWithoutASoftlock()
        {
            const int Cap = 7200, MaxQuiet = 600;
            var f = Duel(-0.8f, 0.8f, Foe.Ai);
            f.KageInput.fallback = new KageBot();
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);
            Assert.IsTrue(f.Kage.state == FState.KO || f.Ronin.state == FState.KO, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Runner {f.Ronin.health:0.0})");
            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(e => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed))
            { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block or parry");
            Assert.Greater(f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), 3, "the fighters did meet");
            Debug.Log($"SAN-97 bot fight vs Runner ended after {f.Frames.Count} frames, Kage {f.Kage.health:0} Runner {f.Ronin.health:0}");
        }
    }
}
