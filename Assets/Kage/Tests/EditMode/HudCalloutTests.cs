using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Unit tests for the world-anchored brush-calligraphy combat callouts, Perfect Parry detection,
    /// 3-phase animation flow (Pop-in → Hold → Fade-out), and the minimal 3-dot HUD timing indicator.
    /// </summary>
    public class HudCalloutTests
    {
        const float Dt = 1f / 60f;

        static HudFrame DuelFrame(bool inWindow = false) => new HudFrame
        {
            Layout = HudLayout.Duel,
            Player = new HudFighterState { Id = "kage", Name = "Kage", Level = 1, Health = 100f, MaxHealth = 100f, HealthScale = 1f, InTimingWindow = inWindow },
            Foe = new HudFighterState { Id = "ronin", Name = "Ronin", Health = 100f, MaxHealth = 100f, HealthScale = 1f },
            HasFoe = true,
            TimeLeft = 50f,
            ClockShown = true,
            ClockRuns = true,
            RoundsShown = true,
            RoundsToWin = 2,
        };

        static FightHudModel StartedModel()
        {
            var m = new FightHudModel();
            m.Tick(Dt, DuelFrame());
            return m;
        }

        static CombatEvent HitEvent(CombatEventType type, string targetId, Vector2 contact, float dmg = 10f) =>
            CombatEvent.HitResolved(10, type, null, null, targetId, "katana1", 0, dmg, false, HitLevel.Mid, HitZone.Body, contact, Vector2.zero);

        [Test]
        public void Core_player_events_emit_ALL_CAPS_English_callouts_at_exact_world_contact_point()
        {
            var m = StartedModel();
            var clashPoint = new Vector2(1.25f, 1.4f);

            m.Handle(HitEvent(CombatEventType.Parried, "kage", clashPoint, 0f), perfectParry: false);
            Assert.AreEqual("PARRY", m.Callouts.LatestLabel);
            Assert.AreEqual(CombatCalloutKind.Parry, m.Callouts.LatestKind);
            Assert.IsTrue(m.Callouts.Entries[0].HasWorldPos);
            Assert.AreEqual(clashPoint, m.Callouts.Entries[0].WorldPos);

            m.Handle(HitEvent(CombatEventType.Parried, "kage", clashPoint, 0f), perfectParry: true);
            Assert.AreEqual("PERFECT PARRY", m.Callouts.LatestLabel);
            Assert.AreEqual(CombatCalloutKind.PerfectParry, m.Callouts.LatestKind);

            var dodgePoint = new Vector2(-0.8f, 1.1f);
            m.Handle(HitEvent(CombatEventType.PerfectDodged, "kage", dodgePoint, 0f));
            Assert.AreEqual("DODGE", m.Callouts.LatestLabel);
            Assert.AreEqual(dodgePoint, m.Callouts.Entries[0].WorldPos);

            var counterPoint = new Vector2(0.4f, 1.2f);
            m.Handle(HitEvent(CombatEventType.Countered, "kage", counterPoint, 0f));
            Assert.AreEqual("COUNTER", m.Callouts.LatestLabel);
            Assert.AreEqual(counterPoint, m.Callouts.Entries[0].WorldPos);
        }

        [Test]
        public void Minimal_HUD_timing_dots_follow_Ready_Window_and_Perfect_states()
        {
            var m = StartedModel();
            Assert.AreEqual(HudTimingState.Ready, m.Player.TimingState, "neutral ready state: ○ ○ ○");

            m.Tick(Dt, DuelFrame(inWindow: true));
            Assert.AreEqual(HudTimingState.Window, m.Player.TimingState, "active parry/dodge window: ○ ● ○");

            m.Handle(HitEvent(CombatEventType.Parried, "kage", new Vector2(0.2f, 1.2f), 0f), perfectParry: true);
            Assert.AreEqual(HudTimingState.Perfect, m.Player.TimingState, "perfect timing burst: ● ● ●");
            Assert.Greater(m.Player.TimingPulse, 0.9f);

            for (int i = 0; i < 45; i++) m.Tick(Dt, DuelFrame(inWindow: false));
            Assert.AreEqual(HudTimingState.Ready, m.Player.TimingState, "returns cleanly to Ready after pulse");
        }

        [Test]
        public void Minor_combat_events_do_not_emit_callouts()
        {
            var m = StartedModel();
            var pt = new Vector2(0.5f, 1.2f);

            m.Handle(HitEvent(CombatEventType.Hit, "ronin", pt, 12f));
            m.Handle(HitEvent(CombatEventType.Hit, "kage", pt, 8f));
            m.Handle(HitEvent(CombatEventType.Blocked, "kage", pt, 0f));
            m.Handle(HitEvent(CombatEventType.Blocked, "ronin", pt, 0f));
            m.Handle(CombatEvent.MoveStarted(12, null, "jab"));
            m.Handle(CombatEvent.RageArt(14, null, "crimsonMoon"));
            m.Handle(CombatEvent.WaveStarted(16, 1, "e1"));

            Assert.AreEqual(0, m.Callouts.Count, "plain hits, ordinary guards, move starts and rage arts must not trigger callouts");
        }

        [Test]
        public void Queue_caps_at_two_for_zero_combo_clutter_and_lifts_nearby_callouts()
        {
            var q = new HudCalloutQueue();
            var spot = new Vector2(0.2f, 1.3f);

            q.Push(CombatCalloutKind.Parry, spot, true);
            q.Push(CombatCalloutKind.GuardBreak, spot + new Vector2(0.1f, 0f), true);
            Assert.AreEqual(2, q.Count);
            Assert.AreEqual(0, q.Entries[0].StackIndex);
            Assert.AreEqual(1, q.Entries[1].StackIndex);

            q.Push(CombatCalloutKind.Counter, spot, true);
            Assert.AreEqual(HudCalloutQueue.MaxActive, q.Count, "capped at 2 to prevent combo clutter");

            for (int i = 0; i < 100; i++) q.Tick(Dt);
            Assert.AreEqual(0, q.Count, "all callouts dissolve cleanly");
        }

        [Test]
        public void Animation_flow_follows_PopIn_Hold_and_FadeOut_phases()
        {
            var q = new HudCalloutQueue();
            q.Push(CombatCalloutKind.Parry, new Vector2(0f, 1.2f), true);
            var start = q.Entries[0];
            Assert.Less(start.Scale, 0.9f, "pops in from compact scale");
            Assert.Less(start.Alpha, 0.1f, "fades swiftly in over pop-in");

            // the phases by the callout's own durations (Parry: pop-in 0.10 s, hold 0.25 s, a smooth fade of 0.48 s), not by fixed frame counts
            var spec = CombatCalloutCatalog.Get(CombatCalloutKind.Parry);
            void TickTo(float age) { while (q.Count > 0 && q.Entries[0].Age < age) q.Tick(Dt); }

            // Hold phase: past the pop-in, before the fade
            TickTo(spec.PopInDuration + spec.HoldDuration * 0.5f);
            var hold = q.Entries[0];
            Assert.AreEqual(1f, hold.Alpha, 1e-3f, "full opacity during hold");
            Assert.AreEqual(1f, hold.Scale, 0.02f, "settled at 1.0 during hold");

            // Fade-out phase: still mostly there early in the fade, mostly gone late in it, and never gone before its time (whatever the curve)
            TickTo(spec.PopInDuration + spec.HoldDuration + spec.FadeOutDuration * 0.3f);
            float early = q.Entries[0].Alpha;
            Assert.Less(early, 1f, "the fade has begun");
            Assert.Greater(early, 0.6f, "still readable early in the fade");
            TickTo(spec.PopInDuration + spec.HoldDuration + spec.FadeOutDuration * 0.8f);
            var fade = q.Entries[0];
            Assert.Less(fade.Alpha, early, "it keeps fading");
            Assert.Less(fade.Alpha, 0.6f, "fading smoothly out");
            Assert.Greater(fade.Alpha, 0f, "and not gone before its time");
        }

        [Test]
        public void Every_callout_spec_is_ALL_CAPS_English_with_sprite_key()
        {
            foreach (CombatCalloutKind kind in System.Enum.GetValues(typeof(CombatCalloutKind)))
            {
                var spec = CombatCalloutCatalog.Get(kind);
                Assert.IsNotEmpty(spec.Label);
                Assert.IsNotEmpty(spec.SpriteId);
                foreach (char c in spec.Label)
                    Assert.IsTrue(c == ' ' || (c >= 'A' && c <= 'Z'), $"ALL-CAPS English label expected for {kind}: '{spec.Label}'");
            }
        }

        [Test]
        public void Every_rage_art_id_reads_as_a_rage_art_and_nothing_else_does()
        {
            foreach (var art in new[] { Fighter.CrimsonMoon, Fighter.SilentMoon, Fighter.SwallowStorm, Fighter.HellSplitter, Fighter.WaterDragon,
                                        Fighter.ReaperRequiem, Fighter.Earthsplitter, Fighter.ThousandShadows, Fighter.Mikazuki })
                Assert.IsTrue(Fighter.IsRageArtId(art), art);
            foreach (var other in new[] { "katana1", "kuroe.censurePunish", "", null })
                Assert.IsFalse(Fighter.IsRageArtId(other), other ?? "null");
        }

        [Test]
        public void RAGE_COUNTERED_is_PARRIEDs_red_brush_held_longer_and_its_wordmark_ships()
        {
            var rage = CombatCalloutCatalog.Get(CombatCalloutKind.RageCountered);
            var parried = CombatCalloutCatalog.Get(CombatCalloutKind.Parried);
            Assert.AreEqual(CombatCalloutKind.RageCountered, rage.Kind, "the spec table lines up with the enum");
            Assert.AreEqual("callout_rage_countered", rage.SpriteId);
            Assert.AreEqual(parried.GlowColor, rage.GlowColor, "PARRIED's red glow");
            Assert.Greater(rage.HoldDuration, parried.HoldDuration, "held long enough to read through Kage's reel");
            var tex = Resources.Load<Texture2D>("UI/Img/" + rage.SpriteId);
            Assert.IsNotNull(tex, "the brushed wordmark is in Resources/UI/Img");
            Assert.Greater(tex.width, tex.height, "a wide wordmark");
        }
    }
}
