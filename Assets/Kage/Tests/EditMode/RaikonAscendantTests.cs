using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon, the Lightning Ascendant (docs/raikon-ascendant-vfx.md): the three phases and the Final Overload as data (thresholds, ability lists,
    /// entry moves, the wind), the moves' timings against the brief, the three-colour telegraph rule over every hit window of the kit, and the new
    /// hooks played in headless fights on a manual clock: a phase's entry move out of hit-stun with a knockback-only shockwave and the wind ramped
    /// onto both rigs, the escape after a run of blows taken through his armour, the reactive triggers, the inward pull, the twin waves, the
    /// tracked sky bolts, the chained dashes and a hit that a guard cannot stop but a parry can.
    /// </summary>
    public class RaikonAscendantTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        GameObject _root;
        SimulationMode2D _simMode;

        [SetUp]
        public void SetUp() { _simMode = Physics2D.simulationMode; CharacterCatalog.Get(Raikon.Id); }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
        }

        /// <summary>Kage at <paramref name="kageX"/> and Raikon at <paramref name="raikonX"/> facing each other, both on scripted inputs that do nothing yet.</summary>
        (FightLoop loop, FightContext ctx, Fighter kage, Fighter raikon, ScriptedInput kageIn) Fight(float kageX = -1.5f, float raikonX = 1.5f)
        {
            _root = new GameObject("RaikonAscendant");
            var b = new FightBuilder(_root.transform, 4242, manualClock: true);
            b.AddGround();
            var kageIn = new ScriptedInput();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, kageIn);
            var raikon = b.Spawn(CharacterCatalog.Get(Raikon.Id), 1, new StandStill());
            var ctx = b.Build();
            ctx.Begin();
            float toRaikon = Mathf.Sign(raikonX - kageX);
            kage.ResetForRound(kageX, toRaikon); kage.BeginFight();
            raikon.ResetForRound(raikonX, -toRaikon); raikon.BeginFight();
            b.Loop.StepHeadless();
            return (b.Loop, ctx, kage, raikon, kageIn);
        }

        static void Run(FightLoop loop, int frames, System.Action each = null)
        {
            for (int i = 0; i < frames; i++) { loop.StepHeadless(); each?.Invoke(); }
        }

        static HitInfo Blow(Fighter attacker, Fighter victim, HitWindow w = null)
        {
            w ??= new HitWindow { dmg = 1, level = HitLevel.Mid, stun = 12, kb = new Vector2(0.3f, 0f) };
            return new HitInfo { attacker = attacker, w = w, dmg = w.dmg, kb = new Vector2(w.kb.x * Mathf.Sign(victim.Pos.x - attacker.Pos.x), w.kb.y), contact = victim.Pos + Vector2.up, moveId = "test", window = -1 };
        }

        // ================================================================ data

        [Test]
        public void His_slots_are_the_four_SAN107_abilities_then_the_Ascendant_kit_and_every_one_is_registered_with_a_move()
        {
            var d = CharacterCatalog.Get(Raikon.Id);
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "skyfall", "redlineCounter" }, d.abilities.Take(4).ToArray(), "the old slots keep their numbers");
            CollectionAssert.AreEqual(RaikonAscendant.AllIds.ToArray(), d.abilities.Skip(4).ToArray());
            Assert.AreEqual(14, RaikonAscendant.AllIds.Count());
            foreach (var id in RaikonAscendant.AllIds)
            {
                var a = AbilityCatalog.Get(id);
                Assert.IsNotNull(a, id);
                Assert.IsNotNull(a.move, id + " has a move");
                Assert.Greater(a.cooldownFrames, 0, id + " cools down");
            }
            Assert.IsTrue(AbilityCatalog.Get(RaikonAscendant.IronGuardId).reactive, "Iron Guard answers spam the moment it happens");
            Assert.IsTrue(AbilityCatalog.Get(RaikonAscendant.SkySplitterId).reactive, "Sky-Splitter is an anti-air reaction");
            var feint = AbilityCatalog.Get(RaikonAscendant.AfterimageFeintId);
            Assert.AreEqual(4, feint.escapeAfterHits, "out of a combo of 4+ hits");
            Assert.That(feint.cooldownFrames, Is.InRange(600, 720), "a 10–12 s cooldown");
        }

        [Test]
        public void His_boss_data_is_Pure_Samurai_Storm_Blade_Lightning_Ascendant_and_the_Final_Overload()
        {
            var b = BossCatalog.Get(Raikon.Id);
            Assert.AreEqual(4, b.Count);
            CollectionAssert.AreEqual(new[] { "Pure Samurai", "Storm Blade", "Lightning Ascendant", "Final Overload" }, b.phases.Select(p => p.name).ToArray());
            CollectionAssert.AreEqual(new[] { 1f, 0.65f, 0.30f, 0.12f }, b.phases.Select(p => p.at).ToArray());
            CollectionAssert.AreEquivalent(RaikonAscendant.Phase1, b.phases[0].abilities);
            CollectionAssert.AreEquivalent(RaikonAscendant.Phase1.Concat(RaikonAscendant.Phase2Adds), b.phases[1].abilities);
            CollectionAssert.AreEquivalent(RaikonAscendant.AllIds, b.phases[2].abilities);
            CollectionAssert.AreEquivalent(RaikonAscendant.AllIds, b.phases[3].abilities);
            foreach (var old in new[] { "thunderSwing", "stormBreak", "skyfall", "redlineCounter" })
                Assert.IsFalse(b.phases.Any(p => p.abilities.Contains(old)), old + " is no longer enabled");

            Assert.IsNull(b.phases[0].enterMove, "the fight opens on the first phase");
            Assert.AreSame(RaikonAscendant.StormKindling, b.phases[1].enterMove);
            Assert.AreSame(RaikonAscendant.Ascension, b.phases[2].enterMove);
            Assert.AreSame(RaikonAscendant.FinalOverload, b.phases[3].enterMove);
            Assert.IsFalse(b.phases[0].aggressive || b.phases[1].aggressive);
            Assert.IsTrue(b.phases[2].aggressive && b.phases[3].aggressive, "the last phase is high tempo");

            Assert.IsTrue(b.phases[0].wind.Value.IsCalm, "phase 1: calm");
            float gust = b.phases[1].wind.Value.steady.magnitude, gale = b.phases[2].wind.Value.steady.magnitude;
            Assert.Greater(gust, 1f, "phase 2: a moderate gust");
            Assert.Greater(gale, gust * 1.5f, "phase 3: a storm gale");
            Assert.Greater(b.phases[2].wind.Value.gust, b.phases[1].wind.Value.gust);
            Assert.IsTrue(b.phases.All(p => p.onEnter != null), "each phase sets the storm's look");
        }

        [Test]
        public void Phase_one_throws_no_lightning()
        {
            foreach (var m in new[] { RaikonAscendant.KesaGiri, RaikonAscendant.PommelKick, RaikonAscendant.CrimsonFlash, RaikonAscendant.LowSweep, RaikonAscendant.RisingCut, RaikonAscendant.IronGuard, RaikonAscendant.IronGuardSlash })
            {
                Assert.IsNull(m.projectile, m.id);
                Assert.IsEmpty(m.extraThrows, m.id);
                Assert.IsNull(m.hazard, m.id);
                Assert.IsEmpty(m.extraHazards, m.id);
                Assert.IsFalse(m.cues.Any(c => c.cue == RaikonAscendant.AuraCue), m.id + ": no unblockable lightning");
            }
        }

        [Test]
        public void Kesa_Giri_cuts_at_0_35_s_holds_0_45_s_before_the_cleave_and_recovers_0_6_s_and_its_storm_version_lays_a_spark_line()
        {
            var m = RaikonAscendant.KesaGiri;
            Assert.AreEqual(3, m.hits.Count);
            Assert.AreEqual(21, m.hits[0].from, "0.35 s to the first cut");
            Assert.AreEqual(HitLevel.Overhead, m.hits[2].level, "the cleave is an overhead");
            Assert.GreaterOrEqual(m.hits[2].from - m.hits[1].from, 27, "a 0.45 s delay on the third");
            Assert.AreEqual(36, m.total - m.hits[2].to, "0.6 s of recovery");
            var s = RaikonAscendant.KesaGiriStorm;
            Assert.AreEqual(1, s.extraHazards.Count);
            Assert.AreSame(RaikonAscendant.FloorSparkLine, s.extraHazards[0].def);
            Assert.AreEqual(m.hits[2].from, s.extraHazards[0].frame, "laid with the cleave");
            Assert.AreEqual(21, RaikonAscendant.FloorSparkLine.telegraphFrames, "and struck 0.35 s later");
        }

        [Test]
        public void Crimson_Flash_is_a_0_55_s_crouch_then_an_unguardable_dash_across_60_percent_of_the_arena_with_0_8_s_open()
        {
            var m = RaikonAscendant.CrimsonFlash;
            Assert.AreEqual(33, m.motions[0].from, "0.55 s crouch");
            float dash = m.motions[0].vel.x * (m.motions[0].to - m.motions[0].from + 1) / 60f;
            Assert.AreEqual(8.4f, dash, 0.3f, "about 60 % of a 14 m arena");
            Assert.IsTrue(m.hits[0].unguardable, "the red-eye strike: no guard stops it");
            Assert.AreNotEqual(HitLevel.Unblockable, m.hits[0].level, "but a parry does");
            Assert.AreEqual(48, m.total - m.hits[0].to, "0.8 s of recovery");
            Assert.AreSame(RaikonAscendant.TrailArc, RaikonAscendant.CrimsonFlashStorm.extraHazards.Single().def, "phase 2: the trailing arc");
        }

        [Test]
        public void Iron_Guard_holds_a_half_second_light_only_counter_that_answers_with_a_slash()
        {
            var m = RaikonAscendant.IronGuard;
            Assert.AreEqual(30, m.counterTo - m.counterFrom, "0.5 s");
            Assert.IsTrue(m.counterLightOnly, "a heavy blow or a guard-break beats it");
            Assert.IsTrue(m.counterFrontOnly);
            Assert.AreSame(RaikonAscendant.IronGuardSlash, m.counterAnswer);
            Assert.AreNotEqual(HitLevel.Unblockable, RaikonAscendant.IronGuardSlash.hits[0].level, "the answer can be parried");
        }

        [Test]
        public void Ascendant_Rush_is_elbow_low_sweep_rising_kick_air_spin_heavy_slam()
        {
            var h = RaikonAscendant.AscendantRush.hits;
            Assert.AreEqual(5, h.Count);
            Assert.AreEqual(21, h[0].from, "0.35 s stance");
            Assert.AreEqual(HitLevel.Low, h[1].level, "the leg sweep is low");
            Assert.IsTrue(h[2].launch, "the rising kick lifts");
            Assert.IsTrue(h[4].knockdown && h[4].heavy && h[4].level == HitLevel.Overhead, "the slam");
            for (int i = 1; i < h.Count; i++) Assert.Greater(h[i].from, h[i - 1].to, "in order");
            Assert.IsFalse(h.Any(w => w.level == HitLevel.Unblockable), "every link can be parried, so a parry breaks the chain");
        }

        [Test]
        public void Blink_Flurry_is_three_linked_dashes_each_with_a_slash_that_detonates_0_3_s_after_and_the_blade_sticks_for_1_1_s()
        {
            var a = RaikonAscendant.BlinkFlurry;
            Assert.AreSame(RaikonAscendant.BlinkFlurry2, a.then);
            Assert.AreSame(RaikonAscendant.BlinkFlurry3, a.then.then);
            Assert.IsNull(a.then.then.then);
            Assert.AreEqual(3, a.cues.Count(c => c.cue == RaikonAscendant.EyeCue), "three rhythmic eye flashes");
            Assert.AreEqual(33, a.vaultFrom, "after 0.55 s");
            Assert.AreEqual(HitLevel.Low, a.hits[0].level, "low-mid, then mid, then the dive");
            Assert.AreEqual(HitLevel.Mid, a.then.hits[0].level);
            Assert.AreEqual(HitLevel.Overhead, a.then.then.hits[0].level);
            foreach (var m in new[] { a, a.then })
            {
                var h = m.extraHazards.Single();
                Assert.AreSame(RaikonAscendant.FlurrySlash, h.def);
                int dash = m.vaultTo - m.vaultFrom;
                Assert.AreEqual(dash + 18, h.def.telegraphFrames, m.id + ": 0.3 s after the dash passes");
            }
            Assert.GreaterOrEqual(RaikonAscendant.BlinkFlurry3.total - RaikonAscendant.BlinkFlurry3.hits[0].to, 66, "1.1 s punish window");
        }

        [Test]
        public void Heavens_Verdict_strikes_three_tracking_bolts_0_55_s_apart_with_the_flying_kick_between_the_second_and_third()
        {
            var m = RaikonAscendant.HeavensVerdict;
            var bolts = new List<int> { m.hazardFrame + m.hazard.telegraphFrames };
            bolts.AddRange(m.extraHazards.Select(h => h.frame + h.def.telegraphFrames));
            Assert.IsTrue(m.hazardOnTarget && m.extraHazards.All(h => h.onTarget && h.def == RaikonAscendant.SkyBolt));
            Assert.AreEqual(3, bolts.Count);
            Assert.AreEqual(33, RaikonAscendant.SkyBolt.telegraphFrames, "0.55 s each");
            Assert.Greater(RaikonAscendant.SkyBolt.followFrames, 0, "the ring tracks Kage");
            var kick = m.hits.Single();
            Assert.That(kick.from, Is.InRange(bolts[1], bolts[2]), "the flying kick lands between bolts 2 and 3");
        }

        [Test]
        public void Final_Overload_pulls_dims_throws_twin_waves_and_two_bolts_thrusts_unblockably_and_kneels_for_1_5_s()
        {
            var m = RaikonAscendant.FinalOverload;
            Assert.AreEqual(0, m.pullFrom);
            Assert.AreEqual(48, m.pullTo, "0.8 s of pull");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.DimCue && c.frame == 0), "the screen dims");
            Assert.AreSame(RaikonAscendant.Wave, m.projectile);
            Assert.IsTrue(m.extraThrows.Single().back, "and the twin wave runs the other way");
            Assert.AreEqual(m.projectileFrame, m.extraThrows[0].frame, "left and right at once");
            Assert.AreEqual(2, 1 + m.extraHazards.Count, "two sky bolts");
            var thrust = m.hits.Last();
            Assert.AreEqual(HitLevel.Unblockable, thrust.level);
            Assert.Greater(m.motions.Single().vel.x * (m.motions[0].to - m.motions[0].from) / 60f, 10f, "full-arena");
            Assert.GreaterOrEqual(m.total - thrust.to, 90, "1.5 s exhaustion window");
            Assert.Less(m.armorTo, thrust.to + 1, "no armour while he kneels");
            Assert.Less(m.invulnTo, thrust.from, "nor invulnerability");
        }

        /// <summary>
        /// The three-colour rule over every hit window of the kit: an unblockable window is told by the crimson-white aura, an unguardable one by the
        /// red eye, and a move whose windows can all be blocked by a steel glint and never by the aura (the eye only when it blinks or dashes).
        /// </summary>
        [Test]
        public void Every_window_is_told_by_its_colour_before_it_lands()
        {
            var kit = new[]
            {
                RaikonAscendant.KesaGiri, RaikonAscendant.KesaGiriStorm, RaikonAscendant.PommelKick, RaikonAscendant.CrimsonFlash, RaikonAscendant.CrimsonFlashStorm,
                RaikonAscendant.LowSweep, RaikonAscendant.IronGuardSlash, RaikonAscendant.RaijinArc, RaikonAscendant.ThunderStep, RaikonAscendant.SkySplitter,
                RaikonAscendant.StaticPalm, RaikonAscendant.TempestWheel, RaikonAscendant.BlinkFlurry, RaikonAscendant.BlinkFlurry2, RaikonAscendant.BlinkFlurry3,
                RaikonAscendant.HeavensVerdict, RaikonAscendant.AscendantRush, RaikonAscendant.AfterimageFeint, RaikonAscendant.FinalOverload,
            };
            var steel = new[] { RaikonAscendant.SteelCue, RaikonAscendant.SteelHandCue, RaikonAscendant.SteelFootCue };
            foreach (var m in kit)
            {
                bool Told(string[] ids, int before) => m.cues.Any(c => ids.Contains(c.cue) && c.frame < before);
                foreach (var w in m.hits)
                {
                    if (w.level == HitLevel.Unblockable) Assert.IsTrue(Told(new[] { RaikonAscendant.AuraCue }, w.from), $"{m.id} f{w.from}: an unblockable window under the aura");
                    if (w.unguardable) Assert.IsTrue(Told(new[] { RaikonAscendant.EyeCue }, w.from), $"{m.id} f{w.from}: an unguardable window under the eye");
                }
                bool lightning = m.hits.Any(w => w.level == HitLevel.Unblockable) || m.projectile != null || m.hazard != null;
                bool blockableOnly = m.hits.Count > 0 && m.hits.All(w => w.level != HitLevel.Unblockable && !w.unguardable);
                if (blockableOnly)
                {
                    // a move that also throws or lays lightning (Raijin Arc's wave) is told by the aura, the stronger warning
                    if (!lightning) Assert.IsTrue(Told(steel, m.hits[0].from), $"{m.id}: a blockable move shows the steel glint first");
                    if (!lightning) Assert.IsFalse(m.cues.Any(c => c.cue == RaikonAscendant.AuraCue), $"{m.id}: no aura on a blockable move");
                    if (m.vaultFrom < 0) Assert.IsFalse(m.cues.Any(c => c.cue == RaikonAscendant.EyeCue), $"{m.id}: the eye is for blinks and iaijutsu");
                }
            }
        }

        [Test]
        public void Every_cue_the_kit_plays_is_registered()
        {
            RaikonAscendant.RegisterCues();
            var used = new HashSet<string>();
            foreach (var m in new[]
            {
                RaikonAscendant.KesaGiri, RaikonAscendant.KesaGiriStorm, RaikonAscendant.PommelKick, RaikonAscendant.CrimsonFlash, RaikonAscendant.LowSweep,
                RaikonAscendant.RisingCut, RaikonAscendant.IronGuard, RaikonAscendant.IronGuardSlash, RaikonAscendant.RaijinArc, RaikonAscendant.ThunderStep,
                RaikonAscendant.SkySplitter, RaikonAscendant.StaticPalm, RaikonAscendant.TempestWheel, RaikonAscendant.BlinkFlurry, RaikonAscendant.BlinkFlurry2,
                RaikonAscendant.BlinkFlurry3, RaikonAscendant.HeavensVerdict, RaikonAscendant.AscendantRush, RaikonAscendant.AfterimageFeint,
                RaikonAscendant.StormKindling, RaikonAscendant.Ascension, RaikonAscendant.FinalOverload,
            })
                foreach (var c in m.cues) used.Add(c.cue);
            foreach (var h in new[] { RaikonAscendant.FloorSparkLine, RaikonAscendant.TrailArc, RaikonAscendant.VerticalBolt, RaikonAscendant.SkyBolt, RaikonAscendant.FlurrySlash, RaikonAscendant.FlurrySlam, RaikonAscendant.PlantShock, RaikonAscendant.SpiralKnock })
            {
                if (!string.IsNullOrEmpty(h.telegraphCue)) used.Add(h.telegraphCue);
                if (!string.IsNullOrEmpty(h.strikeCue)) used.Add(h.strikeCue);
            }
            foreach (var id in used) Assert.IsTrue(EffectCues.IsRegistered(id), id);
        }

        [Test]
        public void The_phase_shifts_knock_back_without_damage()
        {
            Assert.AreEqual(0, RaikonAscendant.PlantShock.hit.dmg);
            Assert.AreEqual(0, RaikonAscendant.SpiralKnock.hit.dmg);
            Assert.Greater(RaikonAscendant.PlantShock.hit.kb.x, 4f);
            Assert.IsTrue(RaikonAscendant.StormKindling.Invulnerable(1) && RaikonAscendant.StormKindling.Invulnerable(RaikonAscendant.StormKindling.total));
            Assert.IsTrue(RaikonAscendant.Ascension.Invulnerable(1) && RaikonAscendant.Ascension.Invulnerable(RaikonAscendant.Ascension.total));
        }

        [Test]
        public void Bursts_dashes_blinks_and_slams_kick_the_soft_parts()
        {
            foreach (var m in new[] { RaikonAscendant.CrimsonFlash, RaikonAscendant.ThunderStep, RaikonAscendant.RaijinArc, RaikonAscendant.BlinkFlurry, RaikonAscendant.BlinkFlurry3, RaikonAscendant.AfterimageFeint, RaikonAscendant.StormKindling, RaikonAscendant.Ascension, RaikonAscendant.FinalOverload, RaikonAscendant.KesaGiri })
                Assert.IsNotEmpty(m.impulses, m.id);
            foreach (var m in new[] { RaikonAscendant.CrimsonFlash, RaikonAscendant.ThunderStep, RaikonAscendant.BlinkFlurry, RaikonAscendant.AfterimageFeint, RaikonAscendant.FinalOverload })
                Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.GhostCue), m.id + " leaves afterimages");
        }

        [Test]
        public void The_wind_ramps_linearly_and_keeps_the_start_seed_into_a_calm()
        {
            var a = Wind.Calm;
            var b = RaikonAscendant.StormGale;
            var half = PhaseController.LerpWind(a, b, 0.5f);
            Assert.AreEqual(b.steady.x * 0.5f, half.steady.x, 1e-5f);
            Assert.AreEqual(b.gust * 0.5f, half.gust, 1e-5f);
            Assert.AreEqual(b.seed, half.seed);
            Assert.AreEqual(b.seed, PhaseController.LerpWind(b, Wind.Calm, 0.5f).seed, "into a calm: the start's seed");
            Assert.IsTrue(PhaseController.LerpWind(a, b, 0f).IsCalm);
        }

        // ================================================================ in a fight

        [Test]
        public void Crossing_65_percent_opens_Storm_Blade_plays_Storm_Kindling_out_of_hit_stun_knocks_Kage_back_unhurt_and_brings_the_gust_onto_both_rigs()
        {
            var (loop, ctx, kage, raikon, _) = Fight(-0.5f, 1.5f);         // inside the shockwave's 2.6 m
            Assert.AreEqual(0, raikon.Phases.Index);
            Assert.IsTrue(raikon.rig.wind.IsCalm, "phase 1 is calm");
            Assert.AreEqual(0, raikon.GetComponent<RaikonStorm>().Level, "the faint blade static");
            float kageHp = kage.health, kageX = kage.Pos.x;
            raikon.Stagger(40);
            raikon.health = raikon.maxHealth * 0.64f;
            Run(loop, 3);
            Assert.AreEqual(1, raikon.Phases.Index);
            Assert.AreEqual(RaikonAscendant.StormKindling, raikon.move, "the entry move, out of the stagger");
            Assert.AreEqual(1, raikon.GetComponent<RaikonStorm>().Level);
            bool knocked = false;
            Run(loop, 40, () => knocked |= kage.state is FState.Launched or FState.Down);
            Assert.IsTrue(knocked, "the shockwave knocks Kage down");
            Assert.AreEqual(kageHp, kage.health, 1e-4f, "and costs him nothing");
            Assert.Greater(Mathf.Abs(kage.Pos.x - raikon.Pos.x), Mathf.Abs(kageX - raikon.Pos.x), "pushed away");
            Run(loop, RaikonAscendant.WindRampFrames);
            var want = RaikonAscendant.ModerateGust;
            Assert.AreEqual(want.steady.x * -1f, raikon.rig.wind.steady.x, 1e-3f, "Raikon faced left: the wind blows into his face");
            Assert.AreEqual(want.gust, raikon.rig.wind.gust, 1e-3f);
            Assert.AreEqual(raikon.rig.wind.steady, kage.rig.wind.steady, "the same storm on Kage's rig");
        }

        [Test]
        public void Ascension_lights_the_eye_trail_and_the_Final_Overload_plays_once_at_12_percent()
        {
            var (loop, _, kage, raikon, _) = Fight();
            System.Action keepKage = () => kage.health = kage.maxHealth;       // a KO would end the fight mid-move
            raikon.health = raikon.maxHealth * 0.29f;
            Run(loop, 2);
            Assert.AreEqual(2, raikon.Phases.Index, "a blow across both thresholds opens both");
            Assert.AreEqual(RaikonAscendant.Ascension, raikon.move, "the later entry replaces the earlier one still waiting");
            Assert.IsTrue(raikon.GetComponent<RaikonStorm>().EyeTrail, "the permanent red eye trail");
            Run(loop, RaikonAscendant.Ascension.total + 4, keepKage);
            raikon.health = raikon.maxHealth * 0.11f;
            Run(loop, 2);
            Assert.AreEqual(3, raikon.Phases.Index);
            Assert.AreEqual(RaikonAscendant.FinalOverload, raikon.move);
            Run(loop, RaikonAscendant.FinalOverload.total + 4, keepKage);
            raikon.health = raikon.maxHealth * 0.05f;
            Run(loop, 2);
            Assert.AreNotEqual(RaikonAscendant.FinalOverload, raikon.move, "once only");
        }

        [Test]
        public void A_forced_move_starts_out_of_hit_stun()
        {
            var (loop, _, _, raikon, _) = Fight();
            raikon.Stagger(30);
            raikon.QueueForcedMove(RaikonAscendant.IronGuard);
            Run(loop, 1);
            Assert.AreEqual(RaikonAscendant.IronGuard, raikon.move);
            Assert.IsNull(raikon.ForcedMove);
        }

        [Test]
        public void Afterimage_Feint_breaks_out_after_four_blows_in_a_row_even_through_his_armour_and_only_in_phase_three()
        {
            var (loop, _, kage, raikon, _) = Fight(-0.4f, 0.6f);
            raikon.Phases.JumpTo(1);
            for (int i = 0; i < 4; i++) { raikon.ReceiveHit(Blow(kage, raikon)); Run(loop, 2); }
            Assert.AreNotEqual(RaikonAscendant.AfterimageFeint, raikon.move, "not before phase 3");

            raikon.Phases.JumpTo(2);
            Run(loop, 60);                                         // the run of blows is broken
            for (int i = 0; i < 3; i++) { raikon.ReceiveHit(Blow(kage, raikon)); Run(loop, 2); }
            Assert.AreEqual(3, raikon.HitsTakenInARow);
            Assert.IsNull(raikon.ForcedMove, "three are not enough");
            raikon.ReceiveHit(Blow(kage, raikon));
            Assert.AreEqual(RaikonAscendant.AfterimageFeint, raikon.ForcedMove, "the fourth");
            Run(loop, 1);
            Assert.AreEqual(RaikonAscendant.AfterimageFeint, raikon.move);
            Assert.Greater(raikon.AbilityCooldownLeft(RaikonAscendant.AfterimageFeintId), 500, "and it cools down");
        }

        [Test]
        public void Iron_Guard_answers_three_light_attacks_in_a_row_and_Sky_Splitter_a_jump_in_front()
        {
            var (loop, _, kage, raikon, _) = Fight(-0.4f, 1.0f);
            var iron = AbilityCatalog.Get(RaikonAscendant.IronGuardId);
            var jab = kage.moves.Select(Btn.P, 0, 0, false, kage);
            Assert.IsTrue(Fighter.IsLightAttack(jab));
            for (int i = 0; i < 3; i++) { kage.StartMove(jab); Run(loop, 1); }
            Assert.AreEqual(3, kage.LightAttacksWithin(RaikonAscendant.SpamWindowFrames));
            Assert.IsTrue(kage.IsAttacking);
            Assert.IsTrue(iron.when(raikon, kage), "three light attacks: the iron guard");
            Run(loop, RaikonAscendant.SpamWindowFrames + 2);
            Assert.AreEqual(0, kage.LightAttacksWithin(RaikonAscendant.SpamWindowFrames), "the window passes");

            var sky = AbilityCatalog.Get(RaikonAscendant.SkySplitterId);
            Assert.IsFalse(sky.when(raikon, kage), "Kage on the ground");
            kage.rb.position = new Vector2(kage.Pos.x, 1.2f);
            kage.rb.linearVelocity = new Vector2(0f, 2f);
            Run(loop, 1);
            Assert.IsFalse(kage.Grounded);
            Assert.IsTrue(sky.when(raikon, kage), "Kage in the air in front of him");
        }

        [Test]
        public void Final_Overload_draws_Kage_in_and_throws_one_wave_each_way()
        {
            var (loop, ctx, kage, raikon, _) = Fight(-3.5f, 1.5f);
            float d0 = Mathf.Abs(raikon.Pos.x - kage.Pos.x);
            raikon.StartMove(RaikonAscendant.FinalOverload);
            Run(loop, 40);
            float d1 = Mathf.Abs(raikon.Pos.x - kage.Pos.x);
            Assert.Less(d1, d0 - 1.5f, "the inward pull");
            Assert.GreaterOrEqual(d1, Fighter.PullStopDistance - 1e-3f, "never into him");
            var dirs = new HashSet<float>();
            Run(loop, 12, () => { foreach (var p in ctx.Projectiles) dirs.Add(p.Dir); });
            CollectionAssert.AreEquivalent(new[] { 1f, -1f }, dirs, "twin Raijin Arcs, left and right");
        }

        [Test]
        public void Heavens_Verdict_lays_three_sky_bolts_under_Kage()
        {
            var (loop, ctx, kage, raikon, _) = Fight(-3f, 3f);
            raikon.StartMove(RaikonAscendant.HeavensVerdict);
            var seen = new HashSet<Hazard>();
            Run(loop, RaikonAscendant.HeavensVerdict.total, () => { kage.health = kage.maxHealth; foreach (var h in ctx.Hazards) if (h.Def == RaikonAscendant.SkyBolt) seen.Add(h); });
            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void Blink_Flurry_plays_its_three_dashes_in_a_row()
        {
            var (loop, _, kage, raikon, _) = Fight(-1.5f, 2.5f);
            raikon.StartMove(RaikonAscendant.BlinkFlurry);
            var order = new List<MoveDef>();
            Run(loop, 46 + 16 + 30, () => { kage.health = kage.maxHealth; if (raikon.move != null && (order.Count == 0 || order[^1] != raikon.move)) order.Add(raikon.move); });
            CollectionAssert.AreEqual(new[] { RaikonAscendant.BlinkFlurry, RaikonAscendant.BlinkFlurry2, RaikonAscendant.BlinkFlurry3 }, order);
        }

        [Test]
        public void An_unguardable_window_goes_through_a_held_guard_but_is_parried_in_the_window()
        {
            var (loop, _, kage, raikon, kageIn) = Fight(-0.4f, 0.6f);
            var w = RaikonAscendant.CrimsonFlash.hits[0];
            kageIn.Add(300, buttons: "G");
            Run(loop, 2);
            Assert.AreEqual(HitResult.Parried, kage.ReceiveHit(Blow(raikon, kage, w)), "a tight parry stops it");
            Run(loop, 60);
            Assert.IsTrue(kage.IsGuarding);
            var plain = new HitWindow { dmg = 1, level = HitLevel.Mid, stun = 12 };
            Assert.AreEqual(HitResult.Blocked, kage.ReceiveHit(Blow(raikon, kage, plain)), "a plain cut is guarded");
            Run(loop, 30);
            Assert.AreEqual(HitResult.Hit, kage.ReceiveHit(Blow(raikon, kage, w)), "the iaijutsu goes through the held guard");
        }
    }
}
