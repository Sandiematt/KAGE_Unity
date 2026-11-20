using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Shiden in the dev arena (SAN-106): he is in the F5 cycle, fought in the boss format (one round, no clock) with the boss bar, and in
    /// his arena placeholder (the districts backdrop, which BossArena gives him from his JS stage, act1.sub4). A smoke level: the rules are in
    /// <see cref="ShidenFightTests"/>.
    /// </summary>
    public class ShidenArenaTests
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
        public void Shiden_is_in_the_dev_arena_F5_cycle()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Shiden.Id);
        }

        [UnityTest]
        public IEnumerator Shiden_is_a_boss_fight_with_a_boss_bar_in_his_districts_arena()
        {
            var game = Start(Shiden.Id);
            yield return null;
            Assert.AreEqual(Shiden.Id, game.enemy.CharacterId);
            Assert.AreSame(FightFormat.Boss, game.Session.Format);
            Assert.IsFalse(game.Session.Format.HasClock);
            Assert.AreEqual(1, game.Session.Format.RoundsToWin);
            var bar = game.Hud.Boss;
            Assert.IsNotNull(bar);
            Assert.AreEqual("SHIDEN", bar.Name);
            Assert.AreEqual("the Last Lieutenant", bar.Title);
            Assert.AreEqual(2, bar.PhaseCount);
            CollectionAssert.AreEqual(new[] { 0.5f }, bar.Marks.ToArray());

            Assert.IsNotNull(Find(game.transform, "Backdrop_districts"), "his arena placeholder");
            Assert.IsNull(Find(game.transform, "Pagoda"), "not the shrine");
            Assert.IsNotNull(Find(game.transform, "Ground"), "with the arena's floor");
        }
    }
}
