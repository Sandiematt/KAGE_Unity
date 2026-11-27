using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon's thunder pass (docs/raikon-ascendant-vfx.md): the boss strings (<see cref="AbilityDef.chain"/>) and the AI that follows them, the phase
    /// weights of his kit, and the storm's per-phase state (<see cref="RaikonStorm.Phase"/>, <see cref="RaikonAura"/>, <see cref="RaikonSkyStorm"/>).
    /// The effects themselves draw nothing headless; their look is the screenshot fixture's.
    /// </summary>
    public class RaikonThunderTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        GameObject _root;
        SimulationMode2D _simMode;
        readonly List<(AbilityDef a, System.Func<Fighter, float> chance)> _restore = new();

        [SetUp]
        public void SetUp() { _simMode = Physics2D.simulationMode; CharacterCatalog.Get(Raikon.Id); }

        [TearDown]
        public void TearDown()
        {
            foreach (var (a, chance) in _restore) a.chainChance = chance;
            _restore.Clear();
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
        }

        (FightLoop loop, FightContext ctx, Fighter kage, Fighter raikon) Fight(float kageX, float raikonX, IInputSource raikonIn)
        {
            _root = new GameObject("RaikonThunder");
            var b = new FightBuilder(_root.transform, 4242, manualClock: true);
            b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var raikon = b.Spawn(CharacterCatalog.Get(Raikon.Id), 1, raikonIn);
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

        // ================================================================ data

        [Test]
        public void Every_string_links_his_own_abilities_and_never_an_escape_or_itself()
        {
            var def = CharacterCatalog.Get(Raikon.Id);
            Assert.IsNotEmpty(RaikonAscendant.Strings);
            foreach (var (from, next) in RaikonAscendant.Strings)
            {
                var a = AbilityCatalog.Get(from);
                Assert.IsNotNull(a, from);
                CollectionAssert.AreEqual(next, a.chain, from + " carries its string");
                Assert.IsNotNull(a.chainChance, from);
                Assert.IsNotNull(a.chainBeat, from);
                foreach (var id in next)
                {
                    Assert.IsTrue(def.abilities.Contains(id), $"{from} → {id}: one of his slots");
                    Assert.AreNotEqual(from, id, "no string repeats its own ability");
                    Assert.AreEqual(0, AbilityCatalog.Get(id).escapeAfterHits, $"{from} → {id}: the escape is never chained into");
                    // a reaction is chained only after a move that makes its situation (Fulmination Drag's launch → the anti-air); the AI still checks
                    // the reaction's own situation before it takes the string (AIInputSource.ChainStep asks AbilityDef.when)
                    if (AbilityCatalog.Get(id).reactive)
                        Assert.IsTrue(a.move.hits.Any(w => w.launch), $"{from} → {id}: a reaction follows only a launcher, and waits for its own situation");
                }
            }
            Assert.AreEqual(RaikonAscendant.Strings.Length, RaikonAscendant.Strings.Select(s => s.from).Distinct().Count(), "one string per ability");
        }

        [Test]
        public void Every_phase_has_a_string_it_can_follow_with_its_own_abilities()
        {
            var b = RaikonAscendant.Boss();
            foreach (var p in b.phases)
            {
                bool any = RaikonAscendant.Strings.Any(s => p.abilities.Contains(s.from) && s.next.Any(n => p.abilities.Contains(n)));
                Assert.IsTrue(any, p.name + " has a string");
            }
            // phase 1's own: the push kick throws Kage out to the iaijutsu's range
            var kick = AbilityCatalog.Get(RaikonAscendant.PommelKickId);
            Assert.AreEqual(RaikonAscendant.CrimsonFlashId, kick.chain[0]);
        }

        [Test]
        public void The_strings_come_faster_and_more_often_each_phase()
        {
            var (_, _, _, raikon) = Fight(-1.5f, 1.5f, new StandStill());
            float lastChance = -1f;
            int lastBeat = int.MaxValue;
            for (int phase = 0; phase < 4; phase++)
            {
                raikon.health = raikon.maxHealth * new[] { 1f, 0.6f, 0.25f, 0.1f }[phase];
                raikon.Phases.Tick();
                Assert.AreEqual(phase, RaikonAscendant.PhaseOf(raikon));
                float c = RaikonAscendant.ChainChance(raikon);
                int beat = RaikonAscendant.ChainBeat(raikon);
                Assert.Greater(c, lastChance, "phase " + phase);
                Assert.Less(beat, lastBeat, "phase " + phase);
                Assert.LessOrEqual(c, 1f);
                Assert.GreaterOrEqual(beat, 1);
                lastChance = c; lastBeat = beat;
            }
        }

        [Test]
        public void A_phase_weights_its_own_new_abilities_up()
        {
            float Featured(string id) => System.Array.IndexOf(RaikonAscendant.ExpansionIds, id) >= 0 ? RaikonAscendant.ExpansionWeight : 1f;
            foreach (var id in RaikonAscendant.Phase1) Assert.AreEqual(Featured(id), AbilityCatalog.Get(id).weight, id);
            foreach (var id in RaikonAscendant.Phase2Adds) Assert.AreEqual(RaikonAscendant.Phase2Weight * Featured(id), AbilityCatalog.Get(id).weight, 1e-5f, id);
            foreach (var id in RaikonAscendant.Phase3Adds) Assert.AreEqual(RaikonAscendant.Phase3Weight * Featured(id), AbilityCatalog.Get(id).weight, 1e-5f, id);
            Assert.Greater(RaikonAscendant.ExpansionWeight, 1f, "the expansion's moves are featured in their phase");
            Assert.Greater(RaikonAscendant.Phase3Weight, RaikonAscendant.Phase2Weight);
            Assert.Greater(RaikonAscendant.Phase2Weight, 1f);
        }

        [Test]
        public void The_thunder_cues_are_registered_once_each()
        {
            RaikonAscendant.RegisterCues();
            Assert.AreEqual(RaikonAscendant.CueIds.Length, RaikonAscendant.CueIds.Distinct().Count(), "no cue id twice");
            foreach (var id in new[] { RaikonAscendant.GatherKindleCue, RaikonAscendant.GatherAscendCue, RaikonAscendant.GatherOverloadCue, RaikonAscendant.SlamCue })
            {
                Assert.Contains(id, RaikonAscendant.CueIds);
                Assert.IsTrue(EffectCues.IsRegistered(id), id);
            }
            // each phase shift gathers its cloud before its bolt, and the slams carry the slam's thunder
            Assert.IsTrue(RaikonAscendant.StormKindling.cues.Any(c => c.cue == RaikonAscendant.GatherKindleCue && c.frame < RaikonAscendant.StormKindling.cues.First(k => k.cue == RaikonAscendant.BladeIgniteCue).frame));
            Assert.IsTrue(RaikonAscendant.Ascension.cues.Any(c => c.cue == RaikonAscendant.GatherAscendCue));
            Assert.IsTrue(RaikonAscendant.FinalOverload.cues.Any(c => c.cue == RaikonAscendant.GatherOverloadCue && c.frame == 0));
            foreach (var m in new[] { RaikonAscendant.KesaGiri, RaikonAscendant.KesaGiriStorm, RaikonAscendant.RaijinArc, RaikonAscendant.AscendantRush, RaikonAscendant.BlinkFlurry3, RaikonAscendant.FinalOverload })
                Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAscendant.SlamCue), m.id + " slams with thunder");
        }

        [Test]
        public void The_gather_times_end_on_the_bolts_they_announce()
        {
            int ignite = RaikonAscendant.StormKindling.cues.First(c => c.cue == RaikonAscendant.BladeIgniteCue).frame;
            int kindle = RaikonAscendant.StormKindling.cues.First(c => c.cue == RaikonAscendant.GatherKindleCue).frame;
            Assert.AreEqual(ignite - kindle, Mathf.RoundToInt(RaikonAscendant.KindleGather * 60f), "Storm Kindling's cloud is whole when the bolt strikes the blade");
            int burst = RaikonAscendant.Ascension.hazard != null ? RaikonAscendant.Ascension.hazardFrame : 20;
            int ascend = RaikonAscendant.Ascension.cues.First(c => c.cue == RaikonAscendant.GatherAscendCue).frame;
            Assert.AreEqual(burst - ascend, Mathf.RoundToInt(RaikonAscendant.AscendGather * 60f), "Ascension's cloud is whole when the burst breaks it");
            int release = RaikonAscendant.FinalOverload.cues.First(c => c.cue == RaikonAscendant.ReleaseCue).frame;
            Assert.AreEqual(release, Mathf.RoundToInt(RaikonAscendant.OverloadGather * 60f), "the Overload's cloud is whole at the release");
        }

        // ================================================================ the storm's state

        [Test]
        public void Each_phase_sets_the_storm_the_aura_and_the_sky_to_its_level()
        {
            var (loop, _, kage, raikon) = Fight(-1.5f, 1.5f, new StandStill());
            var storm = raikon.GetComponent<RaikonStorm>();
            Assert.AreEqual(0, storm.Phase);
            Assert.AreEqual(0, raikon.GetComponent<RaikonAura>().Level, "the aura is on from the first phase");
            Assert.AreEqual(0, raikon.GetComponent<RaikonSkyStorm>().Level, "and the storm in the backdrop");
            float[] at = { 0.6f, 0.25f, 0.1f };
            for (int i = 0; i < at.Length; i++)
            {
                kage.health = kage.maxHealth;
                raikon.health = raikon.maxHealth * at[i];
                Run(loop, 2);
                Assert.AreEqual(i + 1, raikon.Phases.Index);
                Assert.AreEqual(i + 1, storm.Phase);
                Assert.AreEqual(Mathf.Min(i + 1, 2), storm.Level, "the blade static stops at its top level");
                Assert.AreEqual(i + 1, raikon.GetComponent<RaikonAura>().Level);
                Assert.AreEqual(i + 1, raikon.GetComponent<RaikonSkyStorm>().Level);
                Assert.AreEqual(i + 1, RaikonStorm.PowerOf(raikon));
                Run(loop, 230, () => kage.health = kage.maxHealth);         // the entry move plays out
            }
        }

        // ================================================================ the AI follows a string

        [Test]
        public void The_AI_goes_from_the_push_kick_straight_into_Crimson_Flash_on_the_strings_beat()
        {
            var ai = new AIInputSource();
            var (loop, _, kage, raikon) = Fight(-1.5f, 2.0f, ai);           // the kick whiffs: it ends ~3.1 m off, in the iaijutsu's band
            Assert.IsNotNull(ai.profile, "Raikon decides with the boss profile");
            var kick = AbilityCatalog.Get(RaikonAscendant.PommelKickId);
            _restore.Add((kick, kick.chainChance));
            kick.chainChance = _ => 1f;                                       // follow it every time, for the test
            raikon.QueueForcedMove(RaikonAscendant.PommelKick);
            Run(loop, 2, () => kage.health = kage.maxHealth);
            Assert.AreEqual(RaikonAscendant.PommelKick, raikon.move, "the kick runs");

            int endedAt = -1, flashAt = -1;
            for (int f = 0; f < 200 && flashAt < 0; f++)
            {
                Run(loop, 1, () => kage.health = kage.maxHealth);
                if (endedAt < 0 && raikon.move != RaikonAscendant.PommelKick) endedAt = f;
                if (raikon.IsAttacking && (raikon.move == RaikonAscendant.CrimsonFlash || raikon.move == RaikonAscendant.CrimsonFlashStorm)) flashAt = f;
                else if (endedAt >= 0 && raikon.IsAttacking && raikon.move != RaikonAscendant.PommelKick)
                    Assert.Fail($"went into {raikon.move.id} instead (distance {Mathf.Abs(kage.Pos.x - raikon.Pos.x):0.00} m)");
            }
            Assert.GreaterOrEqual(flashAt, 0, $"Crimson Flash followed the kick (distance {Mathf.Abs(kage.Pos.x - raikon.Pos.x):0.00} m)");
            int beat = RaikonAscendant.ChainBeat(raikon);
            Assert.GreaterOrEqual(flashAt - endedAt, beat, "after the string's beat");
            Assert.LessOrEqual(flashAt - endedAt, beat + 4, "and straight after it");
        }
    }
}
