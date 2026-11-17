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
    }
}
