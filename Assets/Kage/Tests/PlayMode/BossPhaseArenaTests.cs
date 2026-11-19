using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The dev arena (KageGame) with a boss and with a duel (SAN-102): the test boss is fought in the boss format (one round, no clock) with
    /// the boss bar, and an ordinary duel keeps its best of three and 60 s clock and shows no boss bar. A smoke level: the rules are in
    /// <see cref="BossPhaseSessionTests"/>, the phases in <see cref="BossPhaseTests"/>.
    /// </summary>
    public class BossPhaseArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            KageGame.NextGroupSize = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        KageGame Start(string foeId)
        {
            KageGame.NextFoeId = foeId;
            _game = new GameObject("KAGE");
            return _game.AddComponent<KageGame>();
        }

        [UnityTest]
        public IEnumerator A_boss_in_the_dev_arena_is_one_round_with_no_clock_and_a_boss_bar()
        {
            var game = Start(FootmanBoss.Id);
            yield return null;
            Assert.AreSame(FightFormat.Boss, game.Session.Format);
            Assert.AreEqual(1, game.Session.Format.RoundsToWin);
            Assert.IsFalse(game.Session.Format.HasClock);
            Assert.AreEqual(0f, game.Session.TimeLeft);

            var bar = game.Hud.Boss;
            Assert.IsNotNull(bar, "the boss bar is up");
            Assert.AreEqual("FOOTMAN CAPTAIN", bar.Name);
            Assert.AreEqual("Test boss", bar.Title);
            Assert.AreEqual(2, bar.PhaseCount);
            CollectionAssert.AreEqual(new[] { 0.5f }, bar.Marks.ToArray(), "a mark where phase two opens");
        }

        [UnityTest]
        public IEnumerator The_boss_bar_follows_the_boss_health_and_updates_on_a_phase_change()
        {
            var game = Start(FootmanBoss.Id);
            yield return null;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            var bar = game.Hud.Boss;
            Assert.AreEqual(0, bar.PhaseIndex);
            Assert.AreEqual("DRILL ORDER", bar.PhaseName);
            Assert.AreEqual(1f, bar.Fill, 1e-3);

            game.enemy.health = game.enemy.maxHealth * 0.4f;
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, bar.PhaseIndex, "the bar moved to phase two");
            Assert.AreEqual("CHARGE", bar.PhaseName);
            Assert.AreEqual(0.4f, bar.Fill, 0.02f, "and shows the health");
        }

        [UnityTest]
        public IEnumerator An_ordinary_duel_keeps_best_of_three_and_the_clock_and_has_no_boss_bar()
        {
            var game = Start("order_footman");
            yield return null;
            Assert.AreSame(FightFormat.Duel, game.Session.Format);
            Assert.AreEqual(2, game.Session.Format.RoundsToWin);
            Assert.AreEqual(60f, game.Session.TimeLeft, 0.5f);
            Assert.IsNull(game.Hud.Boss);
            Assert.IsNull(game.enemy.Phases);
        }

        [UnityTest]
        public IEnumerator The_group_fight_is_a_duel_format_too()
        {
            KageGame.NextGroupSize = KageGame.GroupFightSize;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            Assert.AreSame(FightFormat.Duel, game.Session.Format);
            Assert.IsTrue(game.Session.Group);
            Assert.IsNull(game.Hud.Boss);
        }

        [UnityTest]
        public IEnumerator Restarting_the_boss_fight_starts_round_one_again()
        {
            var game = Start(FootmanBoss.Id);
            yield return null;
            game.Session.Restart();
            Assert.AreEqual(FightSession.Stage.Intro, game.Session.State);
            Assert.AreEqual(1, game.Session.Round);
        }
    }
}
