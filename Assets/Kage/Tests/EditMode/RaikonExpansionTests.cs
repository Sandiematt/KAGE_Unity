using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Lightning Ascendant's expansion (docs/raikon-ascendant-vfx.md, 2026-11-27): the nine new moves' frames, tells and hit levels against the merged
    /// spec, each phase's identity (no lightning or blinks in phase 1), the four cross-phase upgrades and the nine strings, phase 3b's inheritance, and
    /// the frame actions played in headless fights on a manual clock: Arc Anchor's blink to the thrown blade (and its cancel), Tempest Cage's ring and
    /// chords, Zenith Guillotine's vanish and drop, Mirage Severance's blink behind Kage, Fulmination Drag's rush, Tsuba Press's cleave on contact.
    /// </summary>
    public class RaikonExpansionTests
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

        (FightLoop loop, FightContext ctx, Fighter kage, Fighter raikon) Fight(float kageX = -1.5f, float raikonX = 1.5f)
        {
            _root = new GameObject("RaikonExpansion");
            var b = new FightBuilder(_root.transform, 4242, manualClock: true);
            b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new ScriptedInput());
            var raikon = b.Spawn(CharacterCatalog.Get(Raikon.Id), 1, new StandStill());
            var ctx = b.Build();
            ctx.Begin();
            float toRaikon = Mathf.Sign(raikonX - kageX);
            kage.ResetForRound(kageX, toRaikon); kage.BeginFight();
            raikon.ResetForRound(raikonX, -toRaikon); raikon.BeginFight();
            b.Loop.StepHeadless();
            return (b.Loop, ctx, kage, raikon);
        }

        static void Run(FightLoop loop, int frames, System.Action each = null)
        {
            for (int i = 0; i < frames; i++) { loop.StepHeadless(); each?.Invoke(); }
        }

        static bool Told(MoveDef m, string cue, int before) => m.cues.Any(c => c.cue == cue && c.frame < before && c.frame >= before - 20);

        /// <summary>
        /// Steps until Raikon's move has passed <paramref name="frame"/> or is no longer <paramref name="m"/> (hit-stop holds his frames, so loop steps are
        /// not his frames), Kage kept alive, <paramref name="each"/> after every step; at most <paramref name="max"/> steps.
        /// </summary>
        static void RunTo(FightLoop loop, Fighter raikon, Fighter kage, MoveDef m, int frame, System.Action each = null, int max = 400)
        {
            for (int i = 0; i < max && raikon.move == m && raikon.IsAttacking && raikon.moveFrame < frame; i++)
                Run(loop, 1, () => { kage.health = kage.maxHealth; each?.Invoke(); });
        }

        // ================================================================ data: the phases

        [Test]
        public void Each_phase_gets_its_three_new_moves_weighted_like_the_rest_of_its_kit()
        {
            CollectionAssert.IsSubsetOf(new[] { RaikonAscendant.SheatheCrossId, RaikonAscendant.TsubaPressId, RaikonAscendant.WhirlwindFeintId }, RaikonAscendant.Phase1);
            CollectionAssert.IsSubsetOf(new[] { RaikonAscendant.FulminationDragId, RaikonAscendant.ArcAnchorId, RaikonAscendant.GaleSeverId }, RaikonAscendant.Phase2Adds);
            CollectionAssert.IsSubsetOf(new[] { RaikonAscendant.TempestCageId, RaikonAscendant.ZenithGuillotineId, RaikonAscendant.MirageSeveranceId }, RaikonAscendant.Phase3Adds);
            Assert.AreEqual(8, RaikonAscendant.Phase1.Length);
            Assert.AreEqual(8, RaikonAscendant.Phase2Adds.Length);
            Assert.AreEqual(7, RaikonAscendant.Phase3Adds.Length);
            Assert.AreEqual(RaikonAscendant.ExpansionWeight, AbilityCatalog.Get(RaikonAscendant.SheatheCrossId).weight, 1e-5f, "featured over phase 1's older moves");
            Assert.AreEqual(RaikonAscendant.Phase2Weight * RaikonAscendant.ExpansionWeight, AbilityCatalog.Get(RaikonAscendant.ArcAnchorId).weight, 1e-5f);
            Assert.AreEqual(RaikonAscendant.Phase3Weight * RaikonAscendant.ExpansionWeight, AbilityCatalog.Get(RaikonAscendant.MirageSeveranceId).weight, 1e-5f);
            Assert.AreEqual(1f, AbilityCatalog.Get(RaikonAscendant.KesaGiriId).weight, "the older phase 1 moves keep theirs");
            var d = CharacterCatalog.Get(Raikon.Id);
            foreach (var id in RaikonAscendant.AllIds) Assert.Contains(id, d.abilities, id + " is one of his slots");
        }

        [Test]
        public void Phase_two_moves_use_no_sky_lightning_and_only_phase_three_calls_the_storm()
        {
            foreach (var m in new[] { RaikonAscendant.FulminationDrag, RaikonAscendant.ArcAnchor, RaikonAscendant.GaleSever, RaikonAscendant.SheatheCrossStorm, RaikonAscendant.OverheadCleaveStorm })
            {
                Assert.IsFalse(m.hazardOnTarget || m.extraHazards.Any(h => h.onTarget), m.id + ": no strike called onto Kage");
                Assert.IsFalse(m.cues.Any(c => c.cue is RaikonAscendant.CageCloudsCue or RaikonAscendant.MeteorCue or RaikonAscendant.EyeCloudCue or RaikonAscendant.MirageSplitCue), m.id + ": no clouds, no decoys");
                Assert.AreEqual(-1, m.vanishFrom, m.id);
            }
        }

        // ================================================================ data: phase 1

        [Test]
        public void Sheathe_Cross_backsteps_and_sheathes_then_draws_at_f24_and_reverse_draws_at_f35_and_phase_two_leaves_a_Static_Cross()
        {
            var m = RaikonAscendant.SheatheCross;
            Assert.Less(m.motions[0].vel.x, 0f, "the backstep");
            Assert.LessOrEqual(m.motions[0].to, 18);
            Assert.IsTrue(m.Sheathed(12) && !m.Sheathed(24), "sheathed, then drawn on the cut");
            Assert.AreEqual(2, m.hits.Count);
            Assert.AreEqual(24, m.hits[0].from);
            Assert.AreEqual(35, m.hits[1].from);
            Assert.IsTrue(m.hits.All(w => w.level == HitLevel.Mid && !w.unguardable), "both draws can be blocked and parried");
            Assert.AreEqual(2, m.cues.Count(c => c.cue is RaikonAscendant.SteelSayaCue or RaikonAscendant.SteelHandCue), "steel ×2");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.SteelSayaCue && c.frame == 12), "the scabbard mouth glints at f12");
            Assert.IsTrue(Told(m, RaikonAscendant.SteelHandCue, 35));

            var s = RaikonAscendant.SheatheCrossStorm;
            var cross = s.extraHazards.Single();
            Assert.AreSame(RaikonAscendant.StaticCross, cross.def);
            Assert.AreEqual(35, cross.frame, "laid where the X closes");
            Assert.AreEqual(24, cross.def.telegraphFrames, "it lingers 0.4 s, crackling");
            Assert.AreEqual(HitLevel.Unblockable, cross.def.hit.level);
            Assert.AreEqual(RaikonAscendant.StaticCrossAhead, cross.ahead);
        }

        [Test]
        public void Tsuba_Press_shoves_at_f15_for_posture_and_on_contact_cleaves_at_f46_and_its_storm_cleave_lays_a_V()
        {
            var p = RaikonAscendant.TsubaPress;
            var shove = p.hits.Single();
            Assert.AreEqual(15, shove.from);
            Assert.AreEqual(1, shove.dmg, "next to no damage");
            Assert.Greater(shove.postureMul, 10f, "but a heavy blow's posture on a guard");
            Assert.AreSame(RaikonAscendant.OverheadCleave, p.onContact);
            Assert.AreSame(RaikonAscendant.OverheadCleaveStorm, RaikonAscendant.TsubaPressStorm.onContact);

            var c = RaikonAscendant.OverheadCleave.hits.Single();
            Assert.AreEqual(46, shove.from + 1 + c.from, "the cleave lands at f46 for a f15 contact");
            Assert.AreEqual(HitLevel.Overhead, c.level);
            Assert.IsTrue(c.heavy && c.knockdown);
            Assert.IsFalse(c.unguardable, "a heavy overhead to parry");
            Assert.IsTrue(Told(RaikonAscendant.OverheadCleave, RaikonAscendant.SteelCue, c.from));

            var vee = RaikonAscendant.OverheadCleaveStorm.extraHazards.Single();
            Assert.AreSame(RaikonAscendant.CleaveVee, vee.def);
            Assert.AreEqual(18, vee.def.telegraphFrames, "erupting 0.3 s later");
            Assert.IsTrue(RaikonAscendant.OverheadCleaveStorm.cues.Any(x => x.cue == RaikonAscendant.SlamCue), "and Slam Thunder");
            Assert.IsTrue(RaikonAscendant.OverheadCleave.cues.Any(x => x.cue == RaikonAscendant.SlamCue), "phase 1's slam: its cracks only");
            Assert.IsEmpty(RaikonAscendant.OverheadCleave.extraHazards);
        }

        [Test]
        public void Whirlwind_Feint_baits_with_steel_then_tells_the_thrust_with_the_eye()
        {
            var m = RaikonAscendant.WhirlwindFeint;
            int steel = m.cues.First(c => c.cue == RaikonAscendant.SteelCue).frame, eye = m.cues.First(c => c.cue == RaikonAscendant.EyeCue).frame;
            Assert.Less(steel, eye, "steel → eye");
            Assert.AreEqual(15, eye, "the feint at f15");
            var thrust = m.hits.Single();
            Assert.AreEqual(29, thrust.from);
            Assert.IsTrue(thrust.unguardable, "round the guard");
            Assert.AreNotEqual(HitLevel.Unblockable, thrust.level, "a tight parry still stops it");
            Assert.AreEqual(15, m.motions[0].from, "the skid");
        }

        // ================================================================ data: phase 2

        [Test]
        public void Fulmination_Drag_rushes_6_m_and_flicks_an_unblockable_launch_at_f40_and_its_storm_version_detonates_three_pillars_0_35_s_behind()
        {
            var m = RaikonAscendant.FulminationDrag;
            var rush = m.motions.Single();
            Assert.AreEqual(18, rush.from);
            Assert.AreEqual(34, rush.to);
            Assert.AreEqual(6f, rush.vel.x * (rush.to - rush.from + 1) / 60f, 0.25f, "about 6 m");
            var flick = m.hits.Single();
            Assert.AreEqual(40, flick.from);
            Assert.AreEqual(HitLevel.Unblockable, flick.level);
            Assert.IsTrue(flick.launch);
            Assert.IsTrue(Told(m, RaikonAscendant.AuraCue, 40));
            Assert.AreEqual(9, m.cues.Count(c => c.cue == RaikonAscendant.RoosterCue), "a rooster tail every 2nd frame of the drag");

            var pillars = RaikonAscendant.FulminationDragStorm.extraHazards;
            Assert.AreEqual(3, pillars.Count);
            Assert.IsTrue(pillars.All(h => h.def == RaikonAscendant.GougePillar && h.ahead < 0f), "behind him on his path");
            Assert.AreEqual(21, RaikonAscendant.GougePillar.telegraphFrames, "0.35 s after he passed");
            var strikes = pillars.Select(h => h.frame + h.def.telegraphFrames).ToList();
            CollectionAssert.AreEqual(strikes.OrderBy(x => x).ToList(), strikes, "one after another");
            Assert.AreEqual(3, strikes.Distinct().Count());
        }

        [Test]
        public void Arc_Anchor_throws_the_tachi_9_m_at_f22_blinks_at_f50_and_extracts_with_an_unblockable_radial_slash()
        {
            var m = RaikonAscendant.ArcAnchor;
            Assert.AreEqual(22, m.projectileFrame);
            Assert.AreSame(RaikonAscendant.AnchorBlade, m.projectile);
            var blade = RaikonAscendant.AnchorBlade;
            Assert.AreEqual(9f, blade.speed * blade.lifeFrames / 60f, 0.01f, "9 m");
            Assert.AreEqual(36, blade.hit.stun, "a hit pins Kage 0.6 s");
            Assert.AreNotEqual(HitLevel.Unblockable, blade.hit.level, "the throw can be blocked or deflected");
            Assert.IsFalse(blade.hit.unguardable);
            Assert.IsTrue(Told(m, RaikonAscendant.SteelCue, 22), "steel for the throw");
            Assert.IsTrue(m.Sheathed(30) && m.Sheathed(49) && !m.Sheathed(51), "his hand is empty while the blade is out");
            Assert.IsTrue(m.steps.Any(s => s.frame == RaikonAscendant.AnchorBlinkFrame), "the blink at f50");
            Assert.AreEqual(HitLevel.Unblockable, RaikonAscendant.RadialSlash.hit.level);
            int slash = RaikonAscendant.AnchorBlinkFrame + RaikonAscendant.RadialSlash.telegraphFrames;
            Assert.AreEqual(52, slash);
            Assert.IsTrue(Told(m, RaikonAscendant.AuraCue, slash), "aura for the extraction");
            Assert.IsEmpty(RaikonAscendant.ArcAnchorRecall.hits, "the recall is open");
        }

        [Test]
        public void Gale_Sever_sends_a_parryable_vacuum_blade_at_f20_and_a_jumpable_low_crescent_at_f38_and_its_storm_version_an_X_crescent_with_residue()
        {
            var m = RaikonAscendant.GaleSever;
            Assert.AreEqual(20, m.projectileFrame);
            Assert.AreSame(RaikonAscendant.VacuumBlade, m.projectile);
            Assert.IsTrue(RaikonAscendant.VacuumBlade.hit.unguardable && RaikonAscendant.VacuumBlade.hit.level != HitLevel.Unblockable, "parry it");
            var low = m.extraThrows.Single();
            Assert.AreEqual(38, low.frame);
            Assert.AreSame(RaikonAscendant.LowCrescent, low.def);
            Assert.IsTrue(low.def.groundWave, "along the floor: jump it");
            Assert.AreEqual(HitLevel.Low, low.def.hit.level);
            Assert.IsTrue(Told(m, RaikonAscendant.EyeCue, 20));

            var s = RaikonAscendant.GaleSeverStorm;
            var x = s.extraThrows.Single(t => t.def == RaikonAscendant.XCrescent);
            Assert.AreEqual(54, x.frame, "the third, delayed X-crescent");
            Assert.IsTrue(Told(s, RaikonAscendant.EyeCue, 54));
            var residue = s.extraHazards.Single();
            Assert.AreSame(RaikonAscendant.StaticResidue, residue.def);
            Assert.AreEqual(72, residue.def.telegraphFrames + residue.def.strikeFrames, "burning 1.2 s");
            Assert.AreEqual(s.total - 54, m.total - 38, "the same recovery after its last crescent");
        }

        // ================================================================ data: phase 3

        [Test]
        public void Tempest_Cage_drops_spears_at_f30_and_runs_a_blockable_chord_at_f44_then_the_unblockable_cross_at_f58()
        {
            var m = RaikonAscendant.TempestCage;
            Assert.AreEqual(2, m.cues.Count(c => c.cue == RaikonAscendant.AuraCue), "aura ×2");
            Assert.AreEqual(2, m.hits.Count);
            Assert.AreEqual(44, m.hits[0].from);
            Assert.AreNotEqual(HitLevel.Unblockable, m.hits[0].level);
            Assert.AreEqual(58, m.hits[1].from);
            Assert.AreEqual(HitLevel.Unblockable, m.hits[1].level, "the final cross");
            Assert.IsTrue(Told(m, RaikonAscendant.AuraCue, 58));
            Assert.AreEqual(5f, RaikonAscendant.CageRadius * 2f, "a 5 m ring");
            CollectionAssert.IsSubsetOf(new[] { 15, 30, 40, 54 }, m.steps.Select(s => s.frame).ToList(), "lock, spears, the two chord ends");
            float chord = m.motions[0].vel.x * (m.motions[0].to - m.motions[0].from + 1) / 60f;
            Assert.AreEqual(RaikonAscendant.CageChordAt * 2f, chord, 0.05f, "a chord runs end to end");
            Assert.AreEqual(HitLevel.Unblockable, RaikonAscendant.CageSpear.hit.level);
        }

        [Test]
        public void Zenith_Guillotine_vanishes_invulnerable_f14_to_34_crashes_at_f38_and_discharges_both_ways_at_f52()
        {
            var m = RaikonAscendant.ZenithGuillotine;
            Assert.AreEqual(14, m.invulnFrom);
            Assert.AreEqual(34, m.invulnTo);
            Assert.IsTrue(m.Vanished(14) && m.Vanished(33) && !m.Vanished(34));
            var crash = m.hits.Single();
            Assert.That(crash.from, Is.InRange(36, 38));
            Assert.IsTrue(crash.unguardable && crash.heavy, "a heavy crash to parry");
            Assert.AreEqual(HitLevel.Overhead, crash.level);
            Assert.IsTrue(Told(m, RaikonAscendant.EyeCue, crash.from) || Told(m, RaikonAscendant.EyeCloudCue, crash.from));
            Assert.AreEqual(52, m.projectileFrame);
            Assert.AreSame(RaikonAscendant.Wave, m.projectile);
            Assert.IsTrue(m.extraThrows.Single().back, "a wave each way");
            Assert.IsTrue(Told(m, RaikonAscendant.AuraCue, 52), "aura for the discharge");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.SlamCue && c.frame == 38), "Slam Thunder at the crash");
        }

        [Test]
        public void Mirage_Severance_splits_at_f16_blinks_flat_and_cuts_unguardably_at_f36_with_a_mark_that_detonates_0_25_s_later()
        {
            var m = RaikonAscendant.MirageSeverance;
            Assert.AreEqual(2, m.cues.Count(c => c.cue == RaikonAscendant.EyeCue), "eye ×2");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.MirageSplitCue && c.frame == 16));
            Assert.AreEqual(26, m.vaultFrom);
            Assert.AreEqual(0f, m.vaultHeight, "flat along the floor");
            Assert.Greater(m.vaultPast, 0f, "to Kage's far side");
            var cut = m.hits.Single();
            Assert.AreEqual(36, cut.from);
            Assert.IsTrue(cut.unguardable);
            var burst = m.extraHazards.Single();
            Assert.AreEqual(36, burst.frame);
            Assert.AreEqual(15, burst.def.telegraphFrames, "0.25 s");
            Assert.AreEqual(16, m.invulnFrom);
            Assert.AreEqual(34, m.invulnTo);
        }

        // ================================================================ data: strings and phase 3b

        [Test]
        public void The_nine_new_strings_follow_the_spec()
        {
            var want = new Dictionary<string, string[]>
            {
                [RaikonAscendant.SheatheCrossId] = new[] { RaikonAscendant.PommelKickId },
                [RaikonAscendant.TsubaPressId] = new[] { RaikonAscendant.LowSweepId },
                [RaikonAscendant.WhirlwindFeintId] = new[] { RaikonAscendant.KesaGiriId },
                [RaikonAscendant.FulminationDragId] = new[] { RaikonAscendant.SkySplitterId, RaikonAscendant.TempestWheelId },
                [RaikonAscendant.ArcAnchorId] = new[] { RaikonAscendant.StaticPalmId, RaikonAscendant.ThunderStepId },
                [RaikonAscendant.GaleSeverId] = new[] { RaikonAscendant.RaijinArcId, RaikonAscendant.CrimsonFlashId },
                [RaikonAscendant.TempestCageId] = new[] { RaikonAscendant.HeavensVerdictId, RaikonAscendant.BlinkFlurryId },
                [RaikonAscendant.ZenithGuillotineId] = new[] { RaikonAscendant.AscendantRushId, RaikonAscendant.StaticPalmId },
                [RaikonAscendant.MirageSeveranceId] = new[] { RaikonAscendant.CrimsonFlashId, RaikonAscendant.BlinkFlurryId },
            };
            foreach (var kv in want)
            {
                var a = AbilityCatalog.Get(kv.Key);
                CollectionAssert.AreEqual(kv.Value, a.chain, kv.Key);
                Assert.IsNotNull(a.chainChance, kv.Key);
                Assert.IsNotNull(a.chainBeat, kv.Key);
            }
            Assert.AreEqual(21, RaikonAscendant.Strings.Length, "the twelve strings stay, nine more");
            var press = AbilityCatalog.Get(RaikonAscendant.TsubaPressId);
            Assert.IsTrue(press.chainIf(RaikonAscendant.TsubaPress), "a whiffed shove goes into Low Sweep");
            Assert.IsTrue(press.chainIf(RaikonAscendant.TsubaPressStorm));
            Assert.IsFalse(press.chainIf(RaikonAscendant.OverheadCleave), "a connected one became the cleave: no sweep after it");
            Assert.IsFalse(press.chainIf(RaikonAscendant.OverheadCleaveStorm));
        }

        [Test]
        public void Phase_3b_inherits_every_phase_3_move_every_upgrade_and_the_92_percent_3_frame_strings()
        {
            var b = BossCatalog.Get(Raikon.Id);
            CollectionAssert.AreEquivalent(b.phases[2].abilities, b.phases[3].abilities, "3b's list is phase 3's");
            foreach (var id in new[] { RaikonAscendant.TempestCageId, RaikonAscendant.ZenithGuillotineId, RaikonAscendant.MirageSeveranceId })
                Assert.Contains(id, b.phases[3].abilities);

            var (_, _, _, raikon) = Fight();
            var upgrades = new (string id, MoveDef p1, MoveDef p2, MoveDef p3)[]
            {
                (RaikonAscendant.SheatheCrossId, null, RaikonAscendant.SheatheCrossStorm, RaikonAscendant.SheatheCrossStorm),
                (RaikonAscendant.TsubaPressId, null, RaikonAscendant.TsubaPressStorm, RaikonAscendant.TsubaPressStorm),
                (RaikonAscendant.FulminationDragId, null, null, RaikonAscendant.FulminationDragStorm),
                (RaikonAscendant.GaleSeverId, null, null, RaikonAscendant.GaleSeverStorm),
            };
            float[] at = { 1f, 0.6f, 0.25f, 0.1f };
            for (int phase = 0; phase < 4; phase++)
            {
                raikon.health = raikon.maxHealth * at[phase];
                raikon.Phases.Tick();
                Assert.AreEqual(phase, RaikonAscendant.PhaseOf(raikon));
                foreach (var u in upgrades)
                {
                    var want = phase == 0 ? u.p1 : phase == 1 ? u.p2 : u.p3;
                    Assert.AreSame(want, AbilityCatalog.Get(u.id).move.variantFor(raikon), $"{u.id} in phase index {phase}");
                }
            }
            Assert.AreEqual(0.92f, RaikonAscendant.ChainChance(raikon), 1e-4f);
            Assert.AreEqual(3, RaikonAscendant.ChainBeat(raikon));
        }

        // ================================================================ headless fights

        [Test]
        public void Tsuba_Press_on_a_standing_Kage_goes_straight_into_the_cleave()
        {
            var (loop, _, kage, raikon) = Fight(-0.6f, 0.6f);
            raikon.StartMove(RaikonAscendant.TsubaPress);
            bool cleaved = false;
            Run(loop, 30, () => { kage.health = kage.maxHealth; if (raikon.move == RaikonAscendant.OverheadCleave) cleaved = true; });
            Assert.IsTrue(cleaved, "the shove connected and the cleave followed");
        }

        [Test]
        public void Fulmination_Drag_rushes_at_Kage_and_stops_at_his_body()
        {
            var (loop, _, kage, raikon) = Fight(-3f, 3f);
            float side = Mathf.Sign(raikon.Pos.x - kage.Pos.x), d0 = Mathf.Abs(raikon.Pos.x - kage.Pos.x);
            raikon.StartMove(RaikonAscendant.FulminationDrag);
            Run(loop, 36, () => kage.health = kage.maxHealth);
            Assert.Less(Mathf.Abs(raikon.Pos.x - kage.Pos.x), d0 - 3.5f, "the rush closes the distance");
            Assert.AreEqual(side, Mathf.Sign(raikon.Pos.x - kage.Pos.x), "but never through him");
        }

        [Test]
        public void Arc_Anchor_blinks_Raikon_to_the_blade_that_hit_Kage_and_lays_the_radial_slash()
        {
            var (loop, ctx, kage, raikon) = Fight(-3f, 3f);
            bool radial = false;
            raikon.StartMove(RaikonAscendant.ArcAnchor);
            RunTo(loop, raikon, kage, RaikonAscendant.ArcAnchor, RaikonAscendant.AnchorBlinkFrame + 1, () =>
            {
                foreach (var h in ctx.Hazards) if (h.Def == RaikonAscendant.RadialSlash) radial = true;
            });
            var s = RaikonAscendant.StateOf(raikon);
            Assert.IsTrue(s.anchorEnded, "the blade came down");
            Assert.IsFalse(s.anchorDeflected);
            Assert.AreEqual(RaikonAscendant.ArcAnchor, raikon.move, "no recall");
            Assert.Less(Mathf.Abs(raikon.Pos.x - kage.Pos.x), 1.6f, "he arrived at the hilt, beside Kage");
            Assert.IsTrue(radial, "and the extraction slash is laid round him");
        }

        [Test]
        public void A_deflected_anchor_cancels_the_blink_into_the_open_recall()
        {
            var (loop, _, kage, raikon) = Fight(-3f, 3f);
            raikon.StartMove(RaikonAscendant.ArcAnchor);
            RunTo(loop, raikon, kage, RaikonAscendant.ArcAnchor, 45);
            var s = RaikonAscendant.StateOf(raikon);
            Assert.IsTrue(s.anchorEnded);
            s.anchorDeflected = true;                                   // as a parry of the throw leaves it (Projectile.Deflected)
            float x0 = raikon.Pos.x;
            RunTo(loop, raikon, kage, RaikonAscendant.ArcAnchor, RaikonAscendant.AnchorBlinkFrame + 1);
            Assert.AreEqual(RaikonAscendant.ArcAnchorRecall, raikon.move, "the recall, not the blink");
            Assert.AreEqual(x0, raikon.Pos.x, 0.2f, "he stays where he threw from");
        }

        [Test]
        public void Tempest_Cage_rings_Kage_with_spears_and_runs_its_chords_from_one_side_to_the_other()
        {
            var (loop, ctx, kage, raikon) = Fight(-1.5f, 1.5f);
            float kx = kage.Pos.x;
            raikon.StartMove(RaikonAscendant.TempestCage);
            var spears = new HashSet<Hazard>();
            var at = new Dictionary<int, float>();                      // his x by his own move frame (the last step on it)
            RunTo(loop, raikon, kage, RaikonAscendant.TempestCage, 68, () =>
            {
                foreach (var h in ctx.Hazards) if (h.Def == RaikonAscendant.CageSpear) spears.Add(h);
                at[raikon.moveFrame] = raikon.Pos.x;
            });
            float atChord1 = at[43], afterChord1 = at[53], afterChord2 = at[67];
            var c = RaikonAscendant.StateOf(raikon).cageCentre;
            Assert.AreEqual(kx, c, 0.3f, "the ring locked on Kage");
            Assert.AreEqual(2, spears.Count, "both sides of the ring");
            var xs = spears.Select(h => h.Centre.x).OrderBy(x => x).ToArray();
            Assert.AreEqual(c - RaikonAscendant.CageSpearAt, xs[0], 0.05f);
            Assert.AreEqual(c + RaikonAscendant.CageSpearAt, xs[1], 0.05f);
            Assert.AreEqual(RaikonAscendant.CageChordAt, Mathf.Abs(atChord1 - c), 0.2f, "he blinked to a chord's end");
            Assert.AreEqual(-Mathf.Sign(atChord1 - c), Mathf.Sign(afterChord1 - c), "the first chord took him across");
            Assert.AreEqual(Mathf.Sign(atChord1 - c), Mathf.Sign(afterChord2 - c), "and the final cross back");
        }

        [Test]
        public void Zenith_Guillotine_hides_him_and_drops_him_onto_where_Kage_stood_at_the_lock()
        {
            var (loop, _, kage, raikon) = Fight(-2f, 2f);
            raikon.StartMove(RaikonAscendant.ZenithGuillotine);
            bool hidden = false, invulnerable = false;
            RunTo(loop, raikon, kage, RaikonAscendant.ZenithGuillotine, 22);
            hidden = raikon.Vanished;
            invulnerable = !raikon.HittableBy(new HitWindow { level = HitLevel.Mid });
            RunTo(loop, raikon, kage, RaikonAscendant.ZenithGuillotine, 50);
            Assert.IsTrue(hidden, "gone from sight while he is up");
            Assert.IsTrue(invulnerable);
            Assert.IsFalse(raikon.Vanished, "back in sight for the crash");
            var s = RaikonAscendant.StateOf(raikon);
            Assert.IsTrue(s.zenithLocked);
            Assert.AreEqual(RaikonAscendant.ZenithLandOffset, Mathf.Abs(raikon.Pos.x - s.zenithX), 0.25f, "he came down at the lock, blade over it");
            Assert.IsTrue(raikon.Grounded, "and landed");
        }

        [Test]
        public void Mirage_Severance_ends_with_the_real_Raikon_behind_Kage()
        {
            var (loop, _, kage, raikon) = Fight(-1.5f, 1.5f);
            float side = Mathf.Sign(raikon.Pos.x - kage.Pos.x);
            raikon.StartMove(RaikonAscendant.MirageSeverance);
            Run(loop, 36, () => kage.health = kage.maxHealth);
            Assert.AreEqual(-side, Mathf.Sign(raikon.Pos.x - kage.Pos.x), "the blink put him on Kage's far side");
            Assert.AreEqual(Mathf.Sign(kage.Pos.x - raikon.Pos.x), raikon.facing, "facing Kage for the cut");
        }
    }
}
