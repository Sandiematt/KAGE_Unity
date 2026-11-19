using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The stretch in the dev arena (SAN-89): what F10 sets (<see cref="KageGame.NextStretchId"/>) builds the road and the session
    /// instead of the duel, and a Kage who walks right reaches the first encounter, the gate closes and its first wave arrives, all on the
    /// real clock in play.
    /// </summary>
    public class StretchDevArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextStretchId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        sealed class WorldWalk : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => new FrameInput { dx = 1 };
        }

        [UnityTest]
        public IEnumerator F10_builds_the_test_stretch_of_Footmen_and_Kage_walks_into_its_first_wave()
        {
            KageGame.NextStretchId = StretchData.DevFootmenId;     // what F10 sets before it reloads the arena
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;

            Assert.IsNull(KageGame.NextStretchId, "the request is used once");
            Assert.AreEqual(KageGame.Mode.Stretch, game.mode);
            Assert.IsNotNull(game.Stretch);
            Assert.IsNotNull(game.Road);
            Assert.AreEqual(StretchData.DevFootmenId, game.Stretch.Data.Id);
            Assert.AreEqual(0, game.foes.Count, "no foe of its own: the session brings them");
            CollectionAssert.AreEqual(new[] { game.player }, game.Fight.Fighters);
            Assert.AreSame(game.Stretch, game.Fight.Sessions.Single());
            Assert.AreEqual((float)game.Stretch.Data.Start, game.player.Pos.x, 0.05f, "Kage starts at the road's start");
            Assert.IsNull(game.player.extraTarget, "no training bag on the road");

            game.player.input = new WorldWalk();
            float end = Time.realtimeSinceStartup + 20f;
            while (game.Stretch.State != StretchState.Encounter && Time.realtimeSinceStartup < end) yield return new WaitForFixedUpdate();
            Assert.AreEqual(StretchState.Encounter, game.Stretch.State, "he reached the first encounter");
            Assert.AreEqual((float)game.Stretch.Data.ArenaLeft(0), game.Fight.Bounds.Left, 1e-3f, "the gate closed on the arena");
            end = Time.realtimeSinceStartup + 10f;
            while (game.Stretch.StretchWave < 1 && Time.realtimeSinceStartup < end) yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, game.Stretch.StretchWave, "its first wave arrived");
            Assert.AreEqual(2, game.Fight.Fighters.Count(f => f.team == 1), "a wave of two Footmen");
            StringAssert.Contains("WAVE 1/2", game.Stretch.HudText);
        }

        [UnityTest]
        public IEnumerator Without_a_request_the_dev_arena_is_still_the_duel()
        {
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            Assert.AreEqual(KageGame.Mode.Duel, game.mode);
            Assert.IsNull(game.Stretch);
            Assert.IsNotNull(game.enemy);
            Assert.AreEqual(7.6f, game.Fight.Bounds.Right, 1e-4f, "the arena's own walls");
        }

        [Test]
        public void The_road_hotkey_cycles_through_the_test_stretch_and_the_JS_stretches()
        {
            var ids = KageGame.DevStretchIds();
            Assert.AreEqual(StretchData.DevFootmenId, ids[0]);
            CollectionAssert.IsSubsetOf(new[] { "prologue.1", "act1.stretch1", "act1.stretch2", "act1.stretch3", "act1.stretch4" }, ids);
            Assert.AreEqual(6, ids.Count);
            string id = ids[0];
            for (int i = 1; i < ids.Count; i++) { id = KageGame.NextStretch(id); Assert.AreEqual(ids[i], id); }
            Assert.AreEqual(ids[0], KageGame.NextStretch(id), "it wraps round");
            Assert.AreEqual(ids[0], KageGame.NextStretch("nowhere"));
        }
    }
}
