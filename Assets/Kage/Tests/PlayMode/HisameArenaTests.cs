using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Hisame in the dev arena (SAN-105): she is in the F5 cycle, fought in the boss format (one round, no clock) with the boss bar, and in
    /// her arena placeholder (the districts backdrop, which BossArena gives her from her JS stage, act1.sub3). A smoke level: the rules are in
    /// <see cref="HisameFightTests"/>.
    /// </summary>
    public class HisameArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            KageGame.NextGroupSize = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        KageGame Start(string foeId)
        {
            KageGame.NextFoeId = foeId;
            _game = new GameObject("KAGE");
            return _game.AddComponent<KageGame>();
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        [Test]
        public void Hisame_is_in_the_dev_arena_F5_cycle()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Hisame.Id);
        }

        [UnityTest]
        public IEnumerator Hisame_is_a_boss_fight_with_a_boss_bar_in_her_districts_arena()
        {
            var game = Start(Hisame.Id);
            yield return null;
            Assert.AreEqual(Hisame.Id, game.enemy.CharacterId);
            Assert.AreSame(FightFormat.Boss, game.Session.Format);
            Assert.IsFalse(game.Session.Format.HasClock);
            Assert.AreEqual(1, game.Session.Format.RoundsToWin);
            var bar = game.Hud.Boss;
            Assert.IsNotNull(bar);
            Assert.AreEqual("HISAME", bar.Name);
            Assert.AreEqual("the Rain Courier", bar.Title);
            Assert.AreEqual(2, bar.PhaseCount);
            CollectionAssert.AreEqual(new[] { 0.5f }, bar.Marks.ToArray());

            Assert.IsNotNull(Find(game.transform, "Backdrop_districts"), "her arena placeholder");
            Assert.IsNull(Find(game.transform, "Pagoda"), "not the shrine");
            Assert.IsNotNull(Find(game.transform, "Ground"), "with the arena's floor");
        }
    }
}
