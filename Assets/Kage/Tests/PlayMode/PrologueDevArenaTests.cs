using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The Prologue in the dev arena (SAN-96): what Shift+F10 sets (<see cref="KageGame.NextStretchId"/> = "prologue.1") builds the road
    /// with its tutorial. A plain title card opens it (no story scene), Kage stands still while it shows, then the first prompt shows
    /// above him with the bound keys.
    /// </summary>
    public class PrologueDevArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextStretchId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        KageGame Build()
        {
            KageGame.NextStretchId = "prologue.1";
            _game = new GameObject("KAGE");
            return _game.AddComponent<KageGame>();
        }

        [UnityTest]
        public IEnumerator The_Prologue_opens_with_a_plain_title_card_and_Kage_waits_for_it()
        {
            var game = Build();
            yield return null;
            Assert.AreEqual(KageGame.Mode.Stretch, game.mode);
            Assert.AreEqual("prologue.1", game.Stretch.Data.Id);
            Assert.IsNotNull(game.Tutorial, "a road with prompts has a tutorial");
            Assert.AreEqual(6, game.Tutorial.Count);
            Assert.IsTrue(game.Hud.TitleCardShowing, "the title card is up");
            Assert.AreEqual("The Empty Village", game.Hud.TitleCardName);
            Assert.AreEqual("PROLOGUE · STRETCH", game.Hud.TitleCardKicker);
            Assert.IsFalse(game.player.controlEnabled, "Kage stands still while the card shows");
            Assert.IsNull(game.Hud.PromptVerb, "no prompt over the card");
            float end = Time.realtimeSinceStartup + KageGame.TitleCardSeconds + 4f;
            while (game.Hud.TitleCardShowing && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(game.Hud.TitleCardShowing, "the card goes");
            yield return null;
            Assert.IsTrue(game.player.controlEnabled, "and Kage is free");
        }

        [UnityTest]
        public IEnumerator The_first_prompt_shows_the_bound_keys_after_the_card()
        {
            var game = Build();
            float end = Time.realtimeSinceStartup + KageGame.TitleCardSeconds + 4f;
            while ((game.Hud == null || game.Hud.PromptVerb == null) && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual("Move", game.Hud.PromptVerb);
            CollectionAssert.AreEqual(new[] { KeyBindings.Label(GameAction.Left, InputDeviceKind.Keyboard), KeyBindings.Label(GameAction.Right, InputDeviceKind.Keyboard) }, game.Hud.PromptKeys);
            Assert.IsNotEmpty(game.Hud.PromptHint);
            Assert.AreEqual(InputDeviceKind.Keyboard, game.Tutorial.Device);
        }

        [UnityTest]
        public IEnumerator Non_tutorial_roads_have_no_tutorial_and_do_not_hold_Kage()
        {
            KageGame.NextStretchId = StretchData.DevFootmenId;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            Assert.IsNull(game.Tutorial);
            Assert.IsTrue(game.player.controlEnabled);
            Assert.IsTrue(game.Hud.TitleCardShowing, "every stretch opens with its card");
        }
    }
}
