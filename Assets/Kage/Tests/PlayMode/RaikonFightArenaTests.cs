using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon in the dev arena (SAN-107): he is in the F5 cycle, fought in the boss format (one round, no clock) with the boss bar (his name, title and
    /// the two phase marks, all from his boss data), in his arena placeholder (Act I's rooftops under a red storm, from his JS stage). A smoke level:
    /// the rules are in <see cref="RaikonFightTests"/>.
    /// </summary>
    public class RaikonFightArenaTests
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

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        [Test]
        public void Raikon_is_in_the_dev_arena_F5_cycle()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Raikon.Id);
        }

        [UnityTest]
        public IEnumerator Raikon_is_a_boss_fight_with_a_boss_bar_of_three_phases_in_his_arena()
        {
            var game = Start(Raikon.Id);
            yield return null;
            Assert.AreEqual(Raikon.Id, game.enemy.CharacterId);
            Assert.AreSame(FightFormat.Boss, game.Session.Format);
            Assert.IsFalse(game.Session.Format.HasClock);
            Assert.AreEqual(1, game.Session.Format.RoundsToWin);
            var bar = game.Hud.Boss;
            Assert.IsNotNull(bar);
            Assert.AreEqual("RAIKON", bar.Name);
            Assert.AreEqual("the Lightning Ascendant", bar.Title);
            Assert.AreEqual("boss_raikon", bar.ArtId, "the JS boss card");
            Assert.AreEqual(3, bar.PhaseCount);
            CollectionAssert.AreEqual(new[] { 0.66f, 0.33f }, bar.Marks.ToArray(), "a mark where each later phase opens");

            Assert.IsNotNull(Find(game.transform, "Backdrop_" + EnvLook.RaikonStorm.id), "his arena placeholder");
            Assert.IsNull(Find(game.transform, "Pagoda"), "not the shrine");
            Assert.IsNotNull(Find(game.transform, "Ground"), "with the arena's floor");
        }
    }
}
