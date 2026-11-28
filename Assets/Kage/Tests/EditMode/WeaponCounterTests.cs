using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Back + Weapon is a counter stance for every weapon, as the katana's Mirror Guard is: the spear's Tide Turn, the chain sickle's Reaper's
    /// Snare, the cleaver's Oni Wall and the hammer's Anvil Guard each catch a blow in their window, turn it aside (the attacker reels) and answer
    /// with their own riposte. And Mumei's rage art is Mikazuki: one giant crescent moon sent down the floor. Headless fights on a manual clock.
    /// </summary>
    public class WeaponCounterTests
    {
        sealed class Duel
        {
            public FightLoop loop;
            public Fighter kage, foe;
            public ScriptedInput kageIn, foeIn;
            public CombatEventLog log;
        }

        readonly List<GameObject> _roots = new();
        readonly List<CombatEventLog> _logs = new();
        SimulationMode2D _simMode;

        [SetUp]
        public void SetUp() => _simMode = Physics2D.simulationMode;

        [TearDown]
        public void TearDown()
        {
            foreach (var l in _logs) l.Dispose();
            _logs.Clear();
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
            Tuning.ClearOverrides();
            GameFeel.ManualClock = null;
            GameFeel.trauma = GameFeel.punch = 0f;
            Physics2D.simulationMode = _simMode;
        }

        Duel Fight(string weaponId, float kageX, float foeX)
        {
            var root = new GameObject("WeaponCounter_" + weaponId);
            _roots.Add(root);
            var b = new FightBuilder(root.transform, 4242, manualClock: true);
            b.AddGround();
            var kageIn = new ScriptedInput();
            var foeIn = new ScriptedInput();
            var kageDef = CharacterCatalog.Get(KageCharacter.Id).Clone();
            kageDef.weaponId = weaponId;                                     // the weapon is equipped at spawn (no re-equip: edit mode can't Destroy)
            var kage = b.Spawn(kageDef, 0, kageIn);
            var foe = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 1, foeIn);
            var ctx = b.Build();
            var log = new CombatEventLog(ctx.Events);
            _logs.Add(log);
            ctx.Begin();
            float toFoe = Mathf.Sign(foeX - kageX);
            kage.ResetForRound(kageX, toFoe); kage.BeginFight();
            foe.ResetForRound(foeX, -toFoe); foe.BeginFight();
            b.Loop.StepHeadless();
            return new Duel { loop = b.Loop, kage = kage, foe = foe, kageIn = kageIn, foeIn = foeIn, log = log };
        }

        static void Run(Duel d, int frames) { for (int i = 0; i < frames; i++) d.loop.StepHeadless(); }

        /// <summary>A slow test blow: the fist lands on frames 40 to 44. Nothing in it is invulnerable or a counter.</summary>
        static MoveDef SlowBlow() => new MoveDef("test.slowBlow", "Slow blow", 70)
            .Key(0, PoseLib.Stance()).Key(70, PoseLib.Stance())
            .Hit(40, 44, new[] { HitPoint.Fist }, 4, HitLevel.Mid, new Vector2(0.6f, 0f), stun: 12, hitstop: 3, radius: 0.9f);

        [TestCase("mizuchi", "spear.counterGuard", "spear.counterThrust")]
        [TestCase("yamigama", "chain.counterGuard", "chain.counterReap")]
        [TestCase("onikiri", "cleaver.counterGuard", "cleaver.counterHack")]
        [TestCase("iwakudaki", HammerMoves.CounterGuardId, HammerMoves.CounterSmashId)]
        public void Back_and_Weapon_catches_a_blow_and_answers_it(string weaponId, string stanceId, string answerId)
        {
            var d = Fight(weaponId, -0.4f, 0.4f);
            Run(d, 10);
            d.foe.StartMove(SlowBlow());
            Run(d, 22);                                                       // the foe is 22 frames into its wind-up
            d.kageIn.Add(1, fwd: -1, buttons: "S").Add(900);                // Back + Weapon
            Run(d, 2);
            Assert.AreEqual(stanceId, d.kage.move?.id, weaponId + ": Back + Weapon starts the counter stance");
            Assert.GreaterOrEqual(d.kage.move.counterFrom, 0, weaponId + ": the stance has a counter window");

            float before = d.kage.health;
            for (int i = 0; i < 60 && d.kage.move?.id != answerId; i++) d.loop.StepHeadless();
            Assert.AreEqual(answerId, d.kage.move?.id, weaponId + ": the caught blow is answered by the riposte. state " + d.kage.state + ", foe " + d.foe.state + " " + d.foe.move?.id + "@" + d.foe.moveFrame
                + ", gap " + Mathf.Abs(d.foe.Pos.x - d.kage.Pos.x) + ", events " + string.Join(",", d.log.All.Select(e => e.Type + ":" + e.MoveId)));
            Assert.AreEqual(before, d.kage.health, 1e-4, weaponId + ": the caught blow did no damage");

            float foeBefore = d.foe.health;
            Run(d, 30);
            Assert.Less(d.foe.health, foeBefore, weaponId + ": the riposte lands on the reeling foe");
        }

        [Test]
        public void Mumeis_rage_art_sends_one_giant_crescent_moon()
        {
            var d = Fight("mumei", -2.5f, 1.5f);
            Assert.AreEqual(Fighter.Mikazuki, d.kage.RageArtId);
            d.kage.meter = 100f;
            d.kageIn.Add(1, dy: -1, buttons: "X").Add(900);
            Run(d, 2);
            Assert.AreEqual(Fighter.Mikazuki, d.kage.move?.id, "the art is the Mikazuki move");
            Assert.AreEqual(0f, d.kage.meter, 1e-4, "the Rage is spent");
            Assert.AreEqual(1, d.log.Count(CombatEventType.RageArt), "a RageArt event");
            Assert.IsFalse(d.kage.HittableBy(new HitWindow { level = HitLevel.Mid }), "untouchable through the cut");

            float before = d.foe.health;
            Run(d, 90);
            var hits = d.log.All.Where(e => e.Type == CombatEventType.Hit && e.MoveId == Fighter.Mikazuki).ToList();
            Assert.AreEqual(1, hits.Count, "the moon struck the foe once");
            Assert.Less(d.foe.health, before - 20f, "a rage art's damage");
        }
    }
}
