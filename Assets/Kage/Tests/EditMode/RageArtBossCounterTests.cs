using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The bosses read the rage art (Fighter.RageArtCounter.cs, rageArtCounter.json): Kuroe, Garan, Hisame, Shiden and Raikon turn it aside
    /// unless they are open the moment Kage starts it (reeling, thrown up, committed to an attack), and a counter stance answers it with its own
    /// move; every other foe takes it wherever it stands. Headless fights on a manual clock.
    /// </summary>
    public class RageArtBossCounterTests
    {
        static readonly string[] Bosses = { Kuroe.Id, Garan.Id, Hisame.Id, Shiden.Id, Raikon.Id };

        sealed class Duel
        {
            public FightLoop loop;
            public FightContext ctx;
            public Fighter kage, foe;
            public ScriptedInput kageIn, foeIn;
            public CombatEventLog log;
        }

        readonly List<GameObject> _roots = new();
        readonly List<CombatEventLog> _logs = new();
        readonly List<string> _cues = new();
        SimulationMode2D _simMode;

        void OnCue(string id, Vector2 pos, in CueArgs a) => _cues.Add(id);

        [SetUp]
        public void SetUp()
        {
            _simMode = Physics2D.simulationMode;
            foreach (var id in Bosses) CharacterCatalog.Get(id);
            EffectCues.Played += OnCue;
        }

        [TearDown]
        public void TearDown()
        {
            EffectCues.Played -= OnCue;
            foreach (var l in _logs) l.Dispose();
            _logs.Clear();
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
            _cues.Clear();
            Tuning.ClearOverrides();
            GameFeel.ManualClock = null;
            GameFeel.trauma = GameFeel.punch = 0f;
            Physics2D.simulationMode = _simMode;
        }

        /// <summary>Kage at <paramref name="kageX"/> and the foe at <paramref name="foeX"/> facing each other, both on scripted inputs that do nothing yet.</summary>
        Duel Fight(string foeId, float kageX = -2.5f, float foeX = 1.5f)
        {
            // one fight at a time: an earlier fight's bodies share the physics scene and would shove this one's fighters
            foreach (var l in _logs) l.Dispose();
            _logs.Clear();
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
            var root = new GameObject("RageCounter_" + foeId);
            _roots.Add(root);
            var b = new FightBuilder(root.transform, 8282, manualClock: true);
            b.AddGround();
            var kageIn = new ScriptedInput();
            var foeIn = new ScriptedInput();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, kageIn);
            var foe = b.Spawn(CharacterCatalog.Get(foeId), 1, foeIn);
            var ctx = b.Build();
            var log = new CombatEventLog(ctx.Events);
            _logs.Add(log);
            ctx.Begin();
            float toFoe = Mathf.Sign(foeX - kageX);
            kage.ResetForRound(kageX, toFoe); kage.BeginFight();
            foe.ResetForRound(foeX, -toFoe); foe.BeginFight();
            b.Loop.StepHeadless();
            return new Duel { loop = b.Loop, ctx = ctx, kage = kage, foe = foe, kageIn = kageIn, foeIn = foeIn, log = log };
        }

        static void Run(Duel d, int frames) { for (int i = 0; i < frames; i++) d.loop.StepHeadless(); }

        /// <summary>Kage presses Special + Down with a full Rage bar, then holds nothing (or Guard); runs until the art or the reel begins.</summary>
        static void Press(Duel d, bool guardAfter = false)
        {
            d.kage.meter = 100f;
            d.kageIn.Add(1, dy: -1, buttons: "X").Add(900, buttons: guardAfter ? "G" : "");
            for (int i = 0; i < 12 && d.kage.state is not (FState.Ultimate or FState.Hitstun); i++) d.loop.StepHeadless();
        }

        static void AssertTurnedAside(Duel d, string who)
        {
            Assert.AreEqual(FState.Hitstun, d.kage.state, who + ": Kage reels");
            Assert.IsNull(d.kage.ActiveRageArt, who + ": no art plays");
            Assert.AreEqual(0f, d.kage.meter, 1e-4, who + ": the Rage is spent");
            Assert.AreEqual(0, d.log.Count(CombatEventType.RageArt), who + ": the art never started");
            var c = d.log.All.Where(e => e.Type == CombatEventType.Countered).ToList();
            Assert.AreEqual(1, c.Count, who + ": one Countered");
            Assert.AreSame(d.kage, c[0].Source, who);
            Assert.AreSame(d.foe, c[0].Target, who);
            Assert.AreEqual(d.kage.RageArtId, c[0].MoveId, who + ": the event names the art");
            Assert.AreEqual(d.foe.maxHealth, d.foe.health, 1e-4, who + ": the art landed nothing");
            // from 4 m away he lunges to 1.1 m × the boss's body scale (1.36 m for Raikon), and the push back moves him a little further
            float gap = Mathf.Abs(d.kage.Pos.x - d.foe.Pos.x);
            Assert.Less(gap, 2f, who + ": Kage lunged in to strike");
            Assert.Greater(gap, 0.9f, who + ": at a blade's length, not inside the boss");
            Assert.AreEqual(Mathf.Sign(d.foe.Pos.x - d.kage.Pos.x), d.kage.facing, who + ": facing the foe that read him");
        }

        static void AssertArtLands(Duel d, string who)
        {
            Assert.AreEqual(FState.Ultimate, d.kage.state, who + ": the art plays");
            Assert.AreEqual(1, d.log.Count(CombatEventType.RageArt), who);
            Assert.AreEqual(0, d.log.Count(CombatEventType.Countered), who + ": nothing turned it aside");
        }

        /// <summary>A slow test blow: wind-up to frame 40, the blow 40 to 44, recovery to 70. Nothing in it is invulnerable or a counter.</summary>
        static MoveDef SlowBlow() => new MoveDef("test.slowBlow", "Slow blow", 70)
            .Key(0, PoseLib.Stance()).Key(70, PoseLib.Stance())
            .Hit(40, 44, new[] { HitPoint.Fist }, 4, HitLevel.Mid, new Vector2(0.6f, 0f), stun: 12, hitstop: 3, radius: 0.3f);

        /// <summary>A test counter stance: it settles to frame 20, catches 20 to 60 (from the front only) and answers with <see cref="TestAnswer"/>.</summary>
        static MoveDef TestStance()
        {
            var m = new MoveDef("test.stance", "Stance", 80).Key(0, PoseLib.Stance()).Key(80, PoseLib.Stance());
            m.counterFrom = 20;
            m.counterTo = 60;
            m.counterFrontOnly = true;
            m.counterAnswer = new MoveDef("test.answer", "Answer", 30).Key(0, PoseLib.Stance()).Key(30, PoseLib.Stance())
                .Hit(8, 12, new[] { HitPoint.Fist }, 5, HitLevel.Mid, new Vector2(1f, 0f), stun: 20, radius: 0.4f);
            return m;
        }

        static void RunMoveTo(Duel d, int frame)
        {
            for (int i = 0; i < 200 && d.foe.state == FState.Attack && d.foe.moveFrame < frame; i++) d.loop.StepHeadless();
            Assert.AreEqual(FState.Attack, d.foe.state, "the foe is still in its move");
        }

        // ================================================================ who reads it

        [Test]
        public void Only_the_four_sub_bosses_and_Raikon_read_the_rage_art()
        {
            foreach (var id in Bosses)
            {
                var d = Fight(id);
                Assert.IsTrue(d.foe.CountersRageArts, id);
                Assert.IsFalse(d.kage.CountersRageArts, "Kage reads nobody's");
            }
            foreach (var id in new[] { Ronin.Id, OrderFootman.Id, FootmanBoss.Id })
                Assert.IsFalse(Fight(id).foe.CountersRageArts, id + " takes the art wherever it stands");
        }

        // ================================================================ turned aside

        [Test]
        public void Every_boss_standing_free_turns_the_art_aside_and_answers_it_as_a_parry()
        {
            foreach (var id in Bosses)
            {
                _cues.Clear();
                var d = Fight(id);
                Assert.IsFalse(d.foe.OpenToRageArt(d.kage), id + " in neutral is not open");
                Press(d);
                AssertTurnedAside(d, id);
                Assert.AreEqual(1, d.foe.ParriesMade, id + ": its AI answers it as a parry");
                Assert.Greater(d.foe.AnswerTellLeft, 0, id + ": its answer shows the tell");
                Assert.Contains(Fighter.FoeParryCue, _cues, id + ": the foe's crimson parry plays");
            }
        }

        [Test]
        public void A_guarding_boss_turns_the_art_aside()
        {
            foreach (var id in Bosses)
            {
                var d = Fight(id);
                d.foeIn.Add(900, buttons: "G");
                Run(d, 3);
                Assert.IsTrue(d.foe.IsGuarding, id + " is guarding");
                Press(d);
                AssertTurnedAside(d, id);
            }
        }

        [Test]
        public void The_reel_from_a_turned_aside_art_has_no_early_way_out()
        {
            var t = Tuning.Get<RageArtCounterTuning>();
            var d = Fight(Kuroe.Id);
            Press(d, guardAfter: true);
            AssertTurnedAside(d, Kuroe.Id);
            Assert.AreEqual(-1, d.kage.StaggerRecoverAt, "a guard or a dodge can't end it (a parried blow's reel can, from frame 12)");
            for (int i = 0; i < 60 && d.ctx.Clock.InHitstop; i++) d.loop.StepHeadless();
            Run(d, t.staggerFrames - 6);
            Assert.AreEqual(FState.Hitstun, d.kage.state, "still reeling with Guard held, long after a parried blow's reel could be left");
            for (int i = 0; i < 40 && d.kage.state == FState.Hitstun; i++) d.loop.StepHeadless();
            Assert.AreNotEqual(FState.Hitstun, d.kage.state, "the reel runs its course and ends");
        }

        /// <summary>A started fight HUD whose player is <paramref name="d"/>'s Kage (by fighter id), against its foe.</summary>
        static FightHudModel Hud(Duel d)
        {
            var m = new FightHudModel();
            m.Tick(1f / 60f, new HudFrame
            {
                Layout = HudLayout.Duel,
                Player = new HudFighterState { Id = d.kage.FighterId, Name = "Kage", Level = 1, Health = 100f, MaxHealth = 100f, HealthScale = 1f },
                Foe = new HudFighterState { Id = d.foe.FighterId, Name = "Boss", Health = 100f, MaxHealth = 100f, HealthScale = 1f },
                HasFoe = true,
            });
            return m;
        }

        [Test]
        public void The_HUD_says_RAGE_COUNTERED_over_the_clash_in_PARRIEDs_red_brush()
        {
            foreach (var id in Bosses)
            {
                var d = Fight(id);
                var hud = Hud(d);
                Press(d);
                AssertTurnedAside(d, id);
                var e = d.log.All.First(x => x.Type == CombatEventType.Countered);
                hud.Handle(e);
                Assert.AreEqual(CombatCalloutKind.RageCountered, hud.Callouts.LatestKind, id);
                Assert.AreEqual("RAGE COUNTERED", hud.Callouts.LatestLabel, id);
                Assert.IsTrue(hud.Callouts.Entries[0].HasWorldPos, id);
                Assert.AreEqual(e.Contact, hud.Callouts.Entries[0].WorldPos, id + ": over the clash between them");
            }
        }

        [Test]
        public void An_ordinary_blow_caught_by_a_counter_stance_still_says_COUNTERED()
        {
            var d = Fight(Kuroe.Id);
            var hud = Hud(d);
            hud.Handle(CombatEvent.HitResolved(10, CombatEventType.Countered, d.kage, d.foe, null, "katana1", 0, 0f, false, HitLevel.Mid, HitZone.Body,
                new Vector2(0.5f, 1.1f), Vector2.zero));
            Assert.AreEqual(CombatCalloutKind.Countered, hud.Callouts.LatestKind);
        }

        // ================================================================ the cue over the boss

        [Test]
        public void The_cue_lights_over_every_boss_that_is_open_while_the_Rage_is_full()
        {
            foreach (var id in Bosses)
            {
                var d = Fight(id);
                d.kage.meter = 100f;
                Assert.IsFalse(HudReader.RageCue(d.kage).On, id + " standing free: no cue (it would turn the art aside)");

                d.foe.Stagger(90);
                var cue = HudReader.RageCue(d.kage);
                Assert.IsTrue(cue.On, id + " reeling: the cue says now");
                Assert.AreEqual(d.foe.Pos.x, cue.World.x, 1e-4, id + ": over the boss");
                Assert.Greater(cue.World.y, d.foe.Pos.y + 2f, id + ": above its head");

                d.kage.meter = 99f;
                Assert.IsFalse(HudReader.RageCue(d.kage).On, id + ": no cue without a full Rage bar");
            }
        }

        [Test]
        public void The_cue_follows_the_boss_into_an_attack_and_goes_out_under_a_counter_stance()
        {
            var d = Fight(Garan.Id);
            d.kage.meter = 100f;
            d.foe.StartMove(SlowBlow());
            RunMoveTo(d, 4);
            Assert.IsTrue(HudReader.RageCue(d.kage).On, "winding up: open");

            var s = Fight(Garan.Id);
            s.kage.meter = 100f;
            s.foe.StartMove(TestStance());
            RunMoveTo(s, 25);
            Assert.IsFalse(HudReader.RageCue(s.kage).On, "a stance that would catch it: no cue");
        }

        [Test]
        public void No_cue_over_a_foe_that_takes_the_art_anywhere_or_while_the_art_plays()
        {
            var footman = Fight(OrderFootman.Id);
            footman.kage.meter = 100f;
            footman.foe.Stagger(90);
            Assert.IsFalse(HudReader.RageCue(footman.kage).On, "a regular foe needs no cue");

            var d = Fight(Kuroe.Id);
            d.foe.Stagger(200);
            Press(d);
            AssertArtLands(d, "Kuroe reeling");
            Assert.IsFalse(HudReader.RageCue(d.kage).On, "the art is playing: the cue is gone");
        }

        [Test]
        public void The_cue_pops_in_at_each_opening_and_fades_out_where_it_was()
        {
            var v = new HudRageCueView();
            const float dt = 1f / 60f;
            var at = new Vector2(1.5f, 2.6f);
            v.Step(dt, new HudRageCue { On = true, World = at });
            Assert.IsTrue(v.On && v.Visible);
            Assert.AreEqual(1, v.Openings);
            Assert.Greater(v.Scale, 1.3f, "it pops in big");
            for (int i = 0; i < 10; i++) v.Step(dt, new HudRageCue { On = true, World = at });
            Assert.AreEqual(1f, v.Alpha, 1e-4, "fully in within a few frames");
            Assert.Less(v.Scale, 1.2f, "and settles");

            v.Step(dt, new HudRageCue { On = false });
            Assert.IsFalse(v.On);
            Assert.IsTrue(v.Visible, "the opening closed: it fades rather than blinks out");
            Assert.AreEqual(at, v.World, "fading where it was");
            for (int i = 0; i < 20; i++) v.Step(dt, new HudRageCue { On = false });
            Assert.IsFalse(v.Visible, "gone after its fade");

            v.Step(dt, new HudRageCue { On = true, World = at });
            Assert.AreEqual(2, v.Openings, "a new opening pops again");
            v.Reset();
            Assert.IsFalse(v.Visible);
            Assert.AreEqual(0, v.Openings);
        }

        [Test]
        public void The_fight_HUD_carries_the_cue_from_its_frame()
        {
            var m = new FightHudModel();
            var f = new HudFrame { Layout = HudLayout.Boss, Player = new HudFighterState { Id = "kage", MaxHealth = 100f, Health = 100f, Rage = 100f, CanRageArt = true } };
            m.Tick(1f / 60f, f);
            Assert.IsFalse(m.RageCue.Visible);
            f.RageCue = new HudRageCue { On = true, World = new Vector2(2f, 2.5f) };
            m.Tick(1f / 60f, f);
            Assert.IsTrue(m.RageCue.On && m.RageCue.Visible);
            Assert.AreEqual(new Vector2(2f, 2.5f), m.RageCue.World);
            m.Reset();
            Assert.IsFalse(m.RageCue.Visible, "a new fight starts without it");
        }

        // ================================================================ open: the art lands

        [Test]
        public void A_reeling_boss_takes_the_art()
        {
            foreach (var id in Bosses)
            {
                var d = Fight(id);
                d.foe.Stagger(90);
                Assert.IsTrue(d.foe.OpenToRageArt(d.kage), id + " reeling is open");
                Press(d);
                AssertArtLands(d, id);
            }
        }

        [Test]
        public void A_boss_thrown_up_by_a_blow_takes_the_art()
        {
            var d = Fight(Kuroe.Id);
            var w = new HitWindow { dmg = 1, level = HitLevel.Mid, knockdown = true, heavy = true, stun = 20, kb = new Vector2(1.5f, 4f) };
            d.foe.ReceiveHit(new HitInfo { attacker = d.kage, w = w, dmg = 1, kb = new Vector2(1.5f, 4f), contact = d.foe.Pos + Vector2.up, moveId = "test", window = -1 });
            Assert.AreEqual(FState.Launched, d.foe.state);
            Press(d);
            AssertArtLands(d, "launched Kuroe");
        }

        [Test]
        public void A_boss_committed_to_an_attack_takes_the_art_in_its_wind_up_and_its_recovery()
        {
            foreach (var id in Bosses)
            {
                var windup = Fight(id);
                windup.foe.StartMove(SlowBlow());
                RunMoveTo(windup, 4);
                Assert.IsTrue(windup.foe.OpenToRageArt(windup.kage), id + " winding up is open");
                Press(windup);
                AssertArtLands(windup, id + " in the wind-up");

                var recovery = Fight(id);
                recovery.foe.StartMove(SlowBlow());
                RunMoveTo(recovery, 50);
                Press(recovery);
                AssertArtLands(recovery, id + " in the recovery");
            }
        }

        // ================================================================ counter stances

        [Test]
        public void A_counter_stance_catches_the_art_from_the_front_and_answers_with_its_own_move()
        {
            var d = Fight(Garan.Id);
            var stance = TestStance();
            d.foe.StartMove(stance);
            RunMoveTo(d, 25);
            Press(d);
            AssertTurnedAside(d, "the stance");
            Assert.AreEqual(FState.Attack, d.foe.state);
            Assert.AreSame(stance.counterAnswer, d.foe.move, "the stance's answer plays at once");
            Assert.AreEqual(0, d.foe.ParriesMade, "answered by the stance, not parried");
        }

        [Test]
        public void A_counter_stance_still_settling_or_caught_from_behind_takes_the_art()
        {
            var settling = Fight(Garan.Id);
            settling.foe.StartMove(TestStance());
            RunMoveTo(settling, 3);
            Press(settling);
            AssertArtLands(settling, "a stance still settling");

            var behind = Fight(Garan.Id);
            behind.foe.SetFacing(1f, true);                 // its back to Kage, who stands to its left
            behind.foe.StartMove(TestStance());
            RunMoveTo(behind, 25);
            Assert.AreEqual(1f, behind.foe.facing, "the stance keeps its back to Kage");
            Press(behind);
            AssertArtLands(behind, "a stance facing away");
        }

        [Test]
        public void Kuroes_Censure_catches_the_art_with_its_own_punish()
        {
            var d = Fight(Kuroe.Id);
            var censure = AbilityCatalog.Get(KuroeAbilities.CensureId).move;
            Assert.IsNotNull(censure.counterAnswer);
            d.foe.StartMove(censure);
            RunMoveTo(d, censure.counterFrom + 2);
            Press(d);
            AssertTurnedAside(d, "Censure");
            Assert.AreSame(censure.counterAnswer, d.foe.move, "Censure's punish answers the art");
        }

        // ================================================================ everyone else, and the switch

        [Test]
        public void A_regular_foe_takes_the_art_standing_or_guarding()
        {
            var standing = Fight(OrderFootman.Id);
            Press(standing);
            AssertArtLands(standing, "a Footman standing");

            var guarding = Fight(OrderFootman.Id);
            guarding.foeIn.Add(900, buttons: "G");
            Run(guarding, 3);
            Press(guarding);
            AssertArtLands(guarding, "a Footman guarding");
        }

        [Test]
        public void With_the_rule_off_a_boss_takes_the_art_wherever_it_stands()
        {
            Tuning.Override(new RageArtCounterTuning { enabled = false });
            var d = Fight(Raikon.Id);
            Assert.IsFalse(d.foe.CountersRageArts);
            Press(d);
            AssertArtLands(d, "Raikon with the rule off");
        }

        [Test]
        public void The_shipped_tuning_file_is_the_code_defaults()
        {
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(new RageArtCounterTuning()), UnityEngine.JsonUtility.ToJson(Tuning.Get<RageArtCounterTuning>()),
                "rageArtCounter.json matches the RageArtCounterTuning defaults");
            Assert.IsTrue(Tuning.Get<RageArtCounterTuning>().enabled);
        }
    }
}
