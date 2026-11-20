using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Footman in a fight (SAN-69), through the fight harness: he is spawned from his catalog entry, plays the
    /// katana move set (no copy) at his move rate of 0.9, and Kage can defeat him with scripted input, which throws his
    /// ragdoll. Each fight is disposed before the next is built.
    /// </summary>
    public class OrderFootmanFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        static CharacterDef Footman() => CharacterCatalog.Get("order_footman");

        HeadlessFight Duel(CharacterDef foe, float kageX = -0.37f, float foeX = 0.37f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: foe);
            _fights.Add(f);
            return f;
        }

        [Test]
        public void HeIsSpawnedFromCharacterDataWithTheJsStatsAndTheKatanaFamily()
        {
            var f = Duel(Footman());
            var foe = f.Ronin;
            Assert.AreEqual("order_footman", foe.FighterId);
            Assert.AreEqual(170f * 1.65f * 100f / 232f, foe.maxHealth, 1e-3);
            Assert.AreEqual(0.054f, foe.Defense, 1e-6);
            Assert.AreEqual(0.9f, foe.DamageMul, 1e-6);
            Assert.AreEqual(0.9f, foe.MoveRate, 1e-6);
            Assert.AreEqual(0.9f, foe.PlaybackRate, 1e-3, "cutter_blade is level 1: no weapon speed on top");
            Assert.AreEqual("katana", foe.Family.id);
            Assert.AreEqual("cutter_blade", foe.WeaponId);
            Assert.AreSame(f.Kage.moves, foe.moves, "the one katana move set, not a copy");
            Assert.IsFalse(foe.isPlayer);
            Assert.IsFalse(foe.Has(CharacterFlags.CanRageArt));
        }

        /// <summary>Frames the foe spends in a move started on the first frame by <paramref name="buttons"/> (nobody to hit).</summary>
        int FramesInMove(CharacterDef foe, string buttons)
        {
            var f = Duel(foe, kageX: -3f, foeX: 3f);
            f.KageInput.Add(600);
            f.RoninInput.Add(1, buttons: buttons).Add(600);
            int frames = 0;
            for (int i = 0; i < 300; i++)
            {
                f.Run(1);
                if (f.Ronin.state == FState.Attack) frames++;
                else if (frames > 0) break;
            }
            Assert.Greater(frames, 5, buttons + " never started");
            return frames;
        }

        [Test]
        public void HisKatanaMovesPlayAtNinetyPercentOfKagesPace()
        {
            foreach (var buttons in new[] { "P", "S" })
            {
                int kage = FramesInMove(CharacterCatalog.Get("ronin"), buttons);   // move rate 1: the katana set as authored
                int footman = FramesInMove(Footman(), buttons);
                Assert.AreEqual(kage / 0.9f, footman, 1.5f, $"'{buttons}' took {footman} frames against {kage} at the authored rate");
                Assert.Greater(footman, kage, buttons);
            }
        }

        [Test]
        public void HisDamageIsKagesKatanaDamageTimesZeroPointNine()
        {
            // an iai cut on a Kage with no defence (damage is rounded once, so a big hit shows the multiplier)
            float Iai(CharacterDef foe)
            {
                var kage = CharacterCatalog.Get("kage");
                kage.defense = 0f;
                var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, foeDef: foe, kageDef: kage);
                _fights.Add(f);
                f.KageInput.Add(600);
                f.RoninInput.Add(1, buttons: "S").Add(600);
                f.Run(60);
                var hit = f.Events(CombatEventType.Hit).First(e => e.Source == f.Ronin);
                f.Dispose();
                _fights.Remove(f);
                return hit.Damage;
            }

            float ronin = Iai(CharacterCatalog.Get("ronin")), footman = Iai(Footman());
            Assert.GreaterOrEqual(ronin, 8f, "a heavy hit");
            Assert.AreEqual(0.9f, footman / ronin, 0.06f, $"{footman:0.00} against {ronin:0.00}");
            Assert.Less(footman, ronin);
        }

        [Test]
        public void KageDefeatsTheFootmanWithScriptedInput()
        {
            var f = Duel(Footman());
            f.RoninInput.Add(3000);   // he stands and takes it
            // iai, then a short walk to keep him in reach; repeated
            for (int i = 0; i < 40; i++) f.KageInput.Add(1, buttons: "S").Add(45).Add(10, fwd: 1);
            f.RunUntil(() => f.Ronin.state == FState.KO, 2400);

            Assert.AreEqual(FState.KO, f.Ronin.state, $"the Footman is still on {f.Ronin.health:0.0} health");
            var ko = f.Events(CombatEventType.KnockedOut).Single();
            Assert.AreEqual("kage", ko.SourceId);
            Assert.AreEqual("order_footman", ko.TargetId);
            Assert.AreEqual(0f, f.Ronin.health);
            Assert.Greater(f.Events(CombatEventType.Hit).Count(e => e.TargetId == "order_footman"), 3, "it takes several cuts, not one");
        }

        [Test]
        public void TheKnockOutThrowsTheFootmansRagdoll()
        {
            var f = Duel(Footman(), -0.4f, 0.4f);
            f.Ronin.health = 1f;
            f.RoninInput.Add(300);
            f.KageInput.Add(1, dy: -1, buttons: "K");   // leg sweep
            f.RunUntil(() => f.Ronin.state == FState.KO, 60);
            Assert.AreEqual(FState.KO, f.Ronin.state);
            var hips = f.Ronin.FallBody(B.Hips);
            Assert.IsNotNull(hips, "the K.O. gives the Footman a ragdoll");
            Vector2 start = hips.position;
            f.Run(45);
            Assert.Greater(Vector2.Distance(start, hips.position), 0.3f, "the body is thrown");
            foreach (var r in f.Ronin.rig.GetComponentsInChildren<Renderer>(true))
            {
                var c = r.bounds.center;
                Assert.IsFalse(float.IsNaN(c.x) || float.IsNaN(c.y), r.name + " has NaN bounds");
            }
            Assert.IsNotNull(f.Ronin.FallBody(B.Head));
            Assert.IsNotNull(f.Ronin.FallBody(B.ArmFL), "the sword arm is a body too");
        }
    }
}
