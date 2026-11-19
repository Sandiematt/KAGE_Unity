using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Kuroe in the dev arena (SAN-103): she is in the F5 cycle, fought in the boss format (one round, no clock) with the boss bar, and in
    /// her arena placeholder (the rooftops backdrop, not the shrine). A smoke level: the rules are in <see cref="KuroeFightTests"/>.
    /// </summary>
    public class KuroeArenaTests
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
        public void Kuroe_is_in_the_dev_arena_F5_cycle()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Kuroe.Id);
        }

        [UnityTest]
        public IEnumerator Kuroe_is_a_boss_fight_with_a_boss_bar_in_her_rooftops_arena()
        {
            var game = Start(Kuroe.Id);
            yield return null;
            Assert.AreEqual(Kuroe.Id, game.enemy.CharacterId);
            Assert.AreSame(FightFormat.Boss, game.Session.Format);
            Assert.IsFalse(game.Session.Format.HasClock);
            Assert.AreEqual(1, game.Session.Format.RoundsToWin);
            var bar = game.Hud.Boss;
            Assert.IsNotNull(bar);
            Assert.AreEqual("KUROE", bar.Name);
            Assert.AreEqual("the Ink Censor", bar.Title);
            Assert.AreEqual(2, bar.PhaseCount);
            CollectionAssert.AreEqual(new[] { 0.5f }, bar.Marks.ToArray());

            Assert.IsNotNull(Find(game.transform, "Backdrop_rooftops"), "her arena placeholder");
            Assert.IsNull(Find(game.transform, "Pagoda"), "not the shrine");
            Assert.IsNotNull(Find(game.transform, "Ground"), "with the arena's floor");
        }

        [UnityTest]
        public IEnumerator A_regular_foe_keeps_the_shrine()
        {
            var game = Start("order_footman");
            yield return null;
            Assert.IsNull(game.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Backdrop_")));
            Assert.IsNotNull(Find(game.transform, "Pagoda"));
        }
    }
}
