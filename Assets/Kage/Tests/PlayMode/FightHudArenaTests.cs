using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The JS-style HUD in the dev arena's modes (SAN-108): the duel, the group fight (F6), the boss (F5) and the stretch (F10, with the
    /// Prologue's title card and prompts). What shows follows the fight type, and Rage, kunai, health, combo, road progress and wave
    /// follow the fighters and the event stream live, on the real clock.
    /// </summary>
    public class FightHudArenaTests
    {
        GameObject _game;

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            KageGame.NextStretchId = null;
            KageGame.NextGroupSize = null;
            GameFeel.paused = false;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        KageGame Start()
        {
            _game = new GameObject("KAGE");
            return _game.AddComponent<KageGame>();
        }

        sealed class WorldWalk : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => new FrameInput { dx = 1 };
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator A_duel_shows_Kages_panel_the_foe_panel_and_the_clock_with_the_foes_name_and_title()
        {
            KageGame.NextFoeId = "order_footman";
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            Assert.AreEqual(HudLayout.Duel, m.Layout);
            Assert.IsTrue(m.Player.Visible);
            Assert.IsTrue(m.Foe.Visible);
            Assert.IsTrue(m.Clock.Visible);
            Assert.IsTrue(m.Clock.PipsVisible, "best of three");
            Assert.IsFalse(m.Boss.Visible);
            Assert.IsFalse(m.Road.Visible);
            Assert.AreEqual("KAGE", m.Player.Name);
            Assert.AreEqual("LV 1", m.Player.LevelText);
            StringAssert.Contains("FOOTMAN", m.Foe.Name.ToUpperInvariant());
            Assert.IsNotEmpty(m.Foe.Title, "the foe's title is shown beside its name");
            Assert.AreEqual("kage", m.Player.Id);
            Assert.AreEqual(game.enemy.FighterId, m.Foe.Id);
        }

        [UnityTest]
        public IEnumerator The_group_fight_keeps_the_duel_layout_and_names_the_group()
        {
            KageGame.NextGroupSize = KageGame.GroupFightSize;
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            Assert.AreEqual(HudLayout.Duel, m.Layout);
            Assert.IsTrue(m.Foe.Visible);
            Assert.IsTrue(m.Clock.Visible);
            StringAssert.Contains("×", m.Foe.Name);
        }

        [UnityTest]
        public IEnumerator A_boss_fight_shows_the_boss_bar_instead_of_the_foe_panel_and_has_no_clock()
        {
            KageGame.NextFoeId = FootmanBoss.Id;
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            Assert.AreEqual(HudLayout.Boss, m.Layout);
            Assert.IsTrue(m.Boss.Visible);
            Assert.IsFalse(m.Foe.Visible, "the boss bar replaces the foe panel");
            Assert.IsFalse(m.Clock.Visible, "no clock");
            Assert.IsTrue(m.Player.Visible);
            Assert.AreEqual("PHASE I", m.Boss.PhaseLabel);
        }

        [UnityTest]
        public IEnumerator A_stretch_shows_the_road_strip_and_no_foe_panel_clock_or_boss_bar_and_the_prologue_card_and_prompts_still_show()
        {
            KageGame.NextStretchId = PrologueAdditions.StageId;
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            Assert.AreEqual(HudLayout.Stretch, m.Layout);
            Assert.IsTrue(m.Road.Visible);
            Assert.IsFalse(m.Foe.Visible);
            Assert.IsFalse(m.Clock.Visible);
            Assert.IsFalse(m.Boss.Visible);
            StringAssert.StartsWith("ENCOUNTERS 0/", m.Road.Label);
            Assert.IsTrue(game.Hud.TitleCardShowing, "the Prologue's title card");
            float end = Time.realtimeSinceStartup + 10f;
            while (game.Hud.PromptVerb == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual("Move", game.Hud.PromptVerb, "and its first prompt");
            Assert.AreEqual(0f, m.Road.Fill, 0.05f, "Kage is at the start of the road");
        }

        [UnityTest]
        public IEnumerator On_the_road_progress_and_the_wave_label_update_as_Kage_walks_into_a_gate()
        {
            KageGame.NextStretchId = StretchData.DevFootmenId;
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            game.player.input = new WorldWalk();
            float end = Time.realtimeSinceStartup + 20f;
            float seen = 0f;
            while (game.Stretch.State != StretchState.Encounter && Time.realtimeSinceStartup < end) { seen = Mathf.Max(seen, m.Road.Fill); yield return null; }
            Assert.Greater(seen, 0.1f, "the strip filled as he walked");
            Assert.AreEqual(game.Stretch.Progress, m.Road.Fill, 0.05f);
            end = Time.realtimeSinceStartup + 10f;
            while (game.Stretch.StretchWave < 1 && Time.realtimeSinceStartup < end) yield return null;
            yield return Frames(2);
            StringAssert.StartsWith("FOES ", m.Road.Label);
            StringAssert.Contains("WAVE 1/2", m.Road.Label);
            Assert.AreEqual(1, m.Road.WaveNumber, "the wave event reached the HUD");
            Assert.AreEqual(game.Stretch.FoesLeft, int.Parse(m.Road.Label.Split(' ')[1]), "foes left from the session");
            Assert.AreEqual(game.Stretch.Foes.Count(f => f.state != FState.KO), m.MiniBars.Count, "a small bar over each live foe");
        }

        [UnityTest]
        public IEnumerator Rage_kunai_and_health_follow_the_fighters_live_and_a_hit_event_shakes_the_panel()
        {
            KageGame.NextFoeId = "order_footman";
            var game = Start();
            yield return Frames(3);
            var m = game.Hud.Model;
            Assert.AreEqual(game.player.KunaiCount, m.Player.Kunai);
            Assert.IsTrue(m.Player.KunaiShown);
            game.player.SetKunaiCount(2);
            game.player.meter = 100f;
            yield return Frames(2);
            Assert.AreEqual(2, m.Player.Kunai, "kunai count");
            Assert.IsTrue(m.Player.RageReady, "Rage ready at a full meter");
            Assert.AreEqual(1f, m.Player.RageFill, 1e-3f);
            game.player.meter = 40f;
            yield return Frames(2);
            Assert.IsFalse(m.Player.RageReady);
            Assert.AreEqual(0.4f, m.Player.RageFill, 1e-3f);

            // health: the bar eases down and its trail waits (the real clock)
            float before = m.Foe.Hp;
            game.enemy.health = game.enemy.maxHealth * 0.5f;
            yield return null;
            Assert.Less(m.Foe.Hp, before, "it started to move");
            Assert.Greater(m.Foe.Hp, 0.5f, "but did not jump");
            Assert.AreEqual(1f, m.Foe.Chip, 0.02f, "the trail waits");
            float end = Time.realtimeSinceStartup + 6f;
            while (m.Foe.Chip > 0.52f && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(0.5f, m.Foe.Hp, 0.02f);
            Assert.LessOrEqual(m.Foe.Chip, 0.55f, "and the trail caught up");

            // the stream: a hit on Kage shakes his panel
            Assert.AreEqual(0f, m.Player.Hurt, 0.01f);
            game.Fight.Events.Publish(CombatEvent.HitResolved(game.Fight.Clock.Frame, CombatEventType.Hit, game.enemy, game.player, null, "jab", 0, 6f, false, HitLevel.Mid, HitZone.Body, Vector2.zero, Vector2.zero));
            Assert.AreEqual(1f, m.Player.Hurt, 1e-3f);
        }

        [UnityTest]
        public IEnumerator The_HUDs_pause_button_pauses_the_arena_and_again_resumes()
        {
            KageGame.NextFoeId = "order_footman";
            var game = Start();
            yield return Frames(2);
            Assert.IsTrue(game.Hud.PauseButton, "the JS HUD has a pause button");
            Assert.IsFalse(GameFeel.paused);
            game.Hud.PressPause();
            Assert.IsTrue(GameFeel.paused);
            game.Hud.PressPause();
            Assert.IsFalse(GameFeel.paused);
        }
    }
}
