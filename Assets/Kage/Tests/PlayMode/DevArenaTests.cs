using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The dev arena (KageGame) still builds and runs its fight through FightBuilder and FightLoop.
    /// A smoke test only: how the game plays is checked by hand in the Editor.
    /// </summary>
    public class DevArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            // immediately: the next test may build a headless fight in this same frame
            if (_game != null) Object.DestroyImmediate(_game);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        [UnityTest]
        public IEnumerator TheDuelRunsOnTheFightLoopInPlay()
        {
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;

            var ctx = game.Fight;
            Assert.IsNotNull(ctx);
            Assert.IsFalse(ctx.Clock.Manual, "play uses the real clock");
            CollectionAssert.AreEqual(new[] { game.player, game.enemy }, ctx.Fighters);
            Assert.AreSame(ctx, game.player.ctx);
            Assert.AreSame(game.enemy, game.player.opponent);
            Assert.AreEqual("kage", game.player.FighterId);
            Assert.AreEqual("ronin", game.enemy.FighterId);

            int frame = ctx.Clock.Frame;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(ctx.Clock.Frame, frame, "the loop steps on FixedUpdate");
            Assert.IsTrue(game.player.Grounded);
        }

        [UnityTest]
        public IEnumerator TheDuelCanBeAgainstTheOrderFootman()
        {
            KageGame.NextFoeId = "order_footman";     // what F5 sets before it reloads the arena
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;

            Assert.AreEqual("order_footman", game.enemy.FighterId);
            Assert.AreEqual("order_footman", game.foeId);
            Assert.AreSame(game.enemy, game.player.opponent);
            Assert.AreEqual(0.9f, game.enemy.PlaybackRate, 1e-3);
            Assert.AreEqual(1, game.enemy.team);
            Assert.IsNull(KageGame.NextFoeId, "the choice is used once");
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(game.enemy.Grounded);
        }

        [Test]
        public void TheFoeHotkeyCyclesThroughTheCharactersKageCanDuel()
        {
            var ids = KageGame.DevFoeIds();
            CollectionAssert.Contains(ids, "ronin");
            CollectionAssert.Contains(ids, "order_footman");
            CollectionAssert.Contains(ids, "hollow_shade");
            CollectionAssert.Contains(ids, "hollow_tutorial");
            CollectionAssert.DoesNotContain(ids, "kage", "the player");
            CollectionAssert.DoesNotContain(ids, "test_heavy", "a test dummy");
            Assert.AreEqual("ronin", ids[0], "the Ronin is the default sparring partner");

            string id = ids[0];
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ids.Count; i++) { seen.Add(id); id = KageGame.NextFoe(id); }
            CollectionAssert.AreEquivalent(ids, seen);
            Assert.AreEqual(ids[0], id, "it wraps round");
            Assert.AreEqual(ids[0], KageGame.NextFoe("nobody"), "an unknown id starts again");
        }
    }
}
