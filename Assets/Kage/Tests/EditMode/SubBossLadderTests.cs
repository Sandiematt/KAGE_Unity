using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Act I sub-boss ladder (<see cref="SubBossTier"/>, docs/act1-subbosses-vfx.md §5): sub-boss I is the gentlest and IV the hardest on every
    /// knob; they walk and run faster than before; their abilities string together on their rank's chance and beat; and each fights in its own
    /// combat look (<see cref="CombatLook"/>), whose effects build without throwing over their whole life.
    /// </summary>
    public class SubBossLadderTests
    {
        static readonly string[] ActOrder = { Kuroe.Id, Garan.Id, Hisame.Id, Shiden.Id };

        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        /// <summary>Holds toward the opponent: a plain walk (or, held long enough, a run).</summary>
        sealed class HoldForward : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => new() { dx = self.opponent != null && self.opponent.Pos.x < self.Pos.x ? -1 : 1 };
        }

        readonly List<GameObject> _roots = new();
        SimulationMode2D _simMode;
        Vfx _prevVfx;

        [SetUp]
        public void SetUp()
        {
            _simMode = Physics2D.simulationMode;
            _prevVfx = Vfx.I;
            foreach (var id in ActOrder) CharacterCatalog.Get(id);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
            GameFeel.ManualClock = null;
            SampledTrail.TestClock = null;
            Vfx.I = _prevVfx;      // never leave this test's destroyed effects layer behind for the next test
            Physics2D.simulationMode = _simMode;
        }

        (FightLoop loop, Fighter kage, Fighter boss) Spawn(string bossId, IInputSource bossInput, float kageX, float bossX, IInputSource kageInput = null)
        {
            var root = new GameObject("Ladder_" + bossId);
            _roots.Add(root);
            var b = new FightBuilder(root.transform, 7071, manualClock: true);
            b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, kageInput ?? new StandStill());
            var boss = b.Spawn(CharacterCatalog.Get(bossId), 1, bossInput);
            var ctx = b.Build();
            ctx.Begin();
            kage.ResetForRound(kageX, 1f); kage.BeginFight();
            boss.ResetForRound(bossX, -1f); boss.BeginFight();
            b.Loop.StepHeadless();
            return (b.Loop, kage, boss);
        }

        // ------------------------------------------------------------------------------------------------ the ladder

        [Test]
        public void The_ladder_is_the_four_sub_bosses_in_Act_I_order()
        {
            CollectionAssert.AreEqual(ActOrder, SubBossTier.Ladder.Select(t => t.id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, SubBossTier.Ladder.Select(t => t.rank).ToArray());
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(ActOrder[i], Rules.JsTables.Stage("act1.sub" + (i + 1)).Enemy, "act1.sub" + (i + 1));
        }

        [Test]
        public void Every_rung_is_harder_than_the_one_before()
        {
            var t = SubBossTier.Ladder;
            var defs = ActOrder.Select(CharacterCatalog.Get).ToArray();
            for (int i = 1; i < 4; i++)
            {
                string who = t[i].id + " over " + t[i - 1].id;
                Assert.Greater(t[i].skill, t[i - 1].skill, who + ": AI skill");
                Assert.Greater(defs[i].damageMul * t[i].damage1, defs[i - 1].damageMul * t[i - 1].damage1, who + ": damage (JS dmg x tier)");
                Assert.Greater(defs[i].MaxHealth(), defs[i - 1].MaxHealth(), who + ": health");
                Assert.Less(t[i].cooldown1, t[i - 1].cooldown1, who + ": cooldowns");
                Assert.Greater(t[i].moveRate1, t[i - 1].moveRate1, who + ": attack speed");
                Assert.Greater(t[i].chain1, t[i - 1].chain1, who + ": strings more often");
                Assert.Less(t[i].beat1, t[i - 1].beat1, who + ": less time between links");
                Assert.Less(t[i].runInBeyond, t[i - 1].runInBeyond, who + ": runs the gap down sooner");
                Assert.GreaterOrEqual(defs[i].speedMul * defs[i].groundSpeedMul, defs[i - 1].speedMul * defs[i - 1].groundSpeedMul - 1e-4f, who + ": footwork");
            }
            foreach (var tier in t)
            {
                Assert.Greater(tier.chain2, tier.chain1, tier.id + ": Phase 2 strings more");
                Assert.Less(tier.cooldown2, tier.cooldown1, tier.id + ": Phase 2 recovers sooner");
                Assert.Greater(tier.damage2, tier.damage1, tier.id + ": Phase 2 hits harder");
            }
        }

        [Test]
        public void Each_sub_boss_carries_its_rung_into_the_fight()
        {
            foreach (var tier in SubBossTier.Ladder)
            {
                var ai = new AIInputSource();
                var (_, _, boss) = Spawn(tier.id, ai, -3f, 3f);
                Assert.AreEqual(tier.groundSpeed, boss.GroundMul, 1e-6, tier.id + " walk/run");
                Assert.AreEqual(tier.skill, ai.skill, 1e-6, tier.id + " AI skill");
                Assert.AreEqual(tier.runInBeyond, ai.runInBeyond, 1e-6, tier.id + " run-in");
                Assert.AreEqual(Mathf.Clamp01(ai.level + tier.skill), ai.Level, 1e-6);
                Assert.AreEqual(tier.cooldown1, boss.AbilityCooldownMul, 1e-6, tier.id + " Phase 1 cooldowns");
                Assert.AreEqual(boss.Character.moveRate * tier.moveRate1, boss.MoveRate, 1e-5, tier.id + " Phase 1 attack speed");
                var p = BossCatalog.Get(tier.id).phases;
                Assert.AreEqual(tier.damage2, p[1].damageMul, 1e-6);
                Assert.AreEqual(tier.phase2Speed, p[1].speedMul, 1e-6);
            }
        }

        [TestCase("kuroe")]
        [TestCase("garan")]
        [TestCase("hisame")]
        [TestCase("shiden")]
        public void A_sub_boss_walks_faster_than_Kage(string id)
        {
            // 22 frames of forward: up to walking speed, still under the run threshold (25 frames), so both only walk
            var (loopK, kage, _) = Spawn(id, new StandStill(), -5f, 5f, new HoldForward());
            for (int i = 0; i < 22; i++) loopK.StepHeadless();
            Assert.AreEqual(FState.Walk, kage.state);
            float kageWalk = Mathf.Abs(kage.LogicVelocity.x);

            var (loopB, _, boss) = Spawn(id, new HoldForward(), -5f, 5f);
            for (int i = 0; i < 22; i++) loopB.StepHeadless();
            Assert.AreEqual(FState.Walk, boss.state, id + " walks");
            float bossWalk = Mathf.Abs(boss.LogicVelocity.x);
            Assert.Greater(bossWalk, kageWalk * 1.05f,
                $"{id} walks faster than Kage: {bossWalk:0.00} vs {kageWalk:0.00} m/s (boss SpeedMul {boss.SpeedMul:0.00} x GroundMul {boss.GroundMul:0.00})");
        }

        [Test]
        public void A_far_off_sub_boss_runs_the_gap_down_instead_of_shuffling_in()
        {
            var ai = new AIInputSource();
            var (loop, kage, boss) = Spawn(Kuroe.Id, ai, -5f, 4f);
            float d0 = Mathf.Abs(boss.Pos.x - kage.Pos.x);
            bool ran = false;
            for (int i = 0; i < 100; i++)
            {
                loop.StepHeadless();
                kage.health = kage.maxHealth;
                ran |= boss.state == FState.Run;
            }
            Assert.IsTrue(ran, "broke into a run from " + d0.ToString("0.0") + " m");
            Assert.Less(Mathf.Abs(boss.Pos.x - kage.Pos.x), d0 - 3f, "closed most of the gap in 100 frames");
        }

        // ------------------------------------------------------------------------------------------------ attack patterns

        [Test]
        public void Each_kit_strings_its_abilities_on_its_rungs_chance_and_beat()
        {
            var kits = new (SubBossTier tier, string[] ids)[]
            {
                (SubBossTier.Kuroe, new[] { KuroeAbilities.SumiBaraiId, KuroeAbilities.BokusekiWaveId, KuroeAbilities.SumiNidanId, KuroeAbilities.ThunderRedactionId }),
                (SubBossTier.Garan, new[] { GaranAbilities.GedanComboId, GaranAbilities.TsuppariId, GaranAbilities.ShikoStompId, GaranAbilities.StormFistId, GaranAbilities.BrokenPalmId }),
                (SubBossTier.Hisame, new[] { HisameAbilities.RainGustId, HisameAbilities.LightningCutId, HisameAbilities.KasumiMaiId, HisameAbilities.SmokeBombId, HisameAbilities.InazumaStepId }),
                (SubBossTier.Shiden, new[] { ShidenAbilities.MakiOtoshiId, ShidenAbilities.SojutsuSweepId, ShidenAbilities.VanguardBreakId, ShidenAbilities.RaijinLanceId, ShidenAbilities.ChargeId }),
            };
            foreach (var (tier, ids) in kits)
            {
                var own = CharacterCatalog.Get(tier.id).abilities;
                foreach (var id in ids)
                {
                    var a = AbilityCatalog.Get(id);
                    Assert.IsNotNull(a, id);
                    Assert.IsNotEmpty(a.chain, id + " strings into a follow-up");
                    foreach (var next in a.chain) CollectionAssert.Contains(own, next, id + " -> " + next + " is " + tier.id + "'s own");
                    Assert.AreEqual(tier.chain1, a.chainChance(null), 1e-6, id + " Phase 1 chance");
                    Assert.AreEqual(tier.beat1, a.chainBeat(null), id + " Phase 1 beat");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the looks

        [Test]
        public void Each_sub_boss_fights_in_its_own_look_and_Kage_in_the_generic_one()
        {
            var expect = new Dictionary<string, System.Type>
            {
                [Kuroe.Id] = typeof(KuroeLook), [Garan.Id] = typeof(GaranLook), [Hisame.Id] = typeof(HisameLook), [Shiden.Id] = typeof(ShidenLook),
            };
            foreach (var kv in expect)
            {
                var (_, kage, boss) = Spawn(kv.Key, new StandStill(), -2f, 2f);
                Assert.IsInstanceOf(kv.Value, boss.Look, kv.Key);
                Assert.IsNull(kage.Look, "Kage keeps the generic trails and bursts");
            }
            var parent = new GameObject("Trails");
            _roots.Add(parent);
            Assert.IsInstanceOf<KuroeInkTrail>(new KuroeLook().Trail(null, TrailSlot.Blade, parent.transform, 0));
            Assert.IsNull(new KuroeLook().Trail(null, TrailSlot.Fist, parent.transform, 0));
            Assert.IsInstanceOf<GaranSmearTrail>(new GaranLook().Trail(null, TrailSlot.Fist, parent.transform, 0));
            Assert.IsInstanceOf<GaranSmearTrail>(new GaranLook().Trail(null, TrailSlot.FootN, parent.transform, 0));
            Assert.IsInstanceOf<HisameFanTrail>(new HisameLook().Trail(null, TrailSlot.BladeN, parent.transform, 0));
            Assert.IsInstanceOf<ShidenSpearTrail>(new ShidenLook().Trail(null, TrailSlot.Blade, parent.transform, 0));
        }

        /// <summary>Every sub-boss effect and trail builds without throwing at every point of its life (the build callbacks run u = 0..1).</summary>
        [Test]
        public void Every_sub_boss_effect_builds_over_its_whole_life()
        {
            var go = new GameObject("LadderVfx");
            _roots.Add(go);
            var v = go.AddComponent<Vfx>();
            typeof(Vfx).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, null);
            Vfx.I = v;
            Vector2 p = new(0f, 1f), d = Vector2.right;

            v.KuroeInkHit(p, d, 1.3f, 0f, true); v.KuroeInkGuarded(p, d); v.KuroeInkFling(p, d, true); v.KuroeNibGlint(p); v.KuroeInkGather(p, true);
            v.KuroeIchimonji(p, p + new Vector2(1.5f, -1f), true); v.KuroeFloorStroke(Vector2.zero, Vector2.right * 1.5f); v.KuroeInkSlam(Vector2.zero, 1f, true);
            v.KuroeInkLineMark(Vector2.zero, Vector2.right * 2f, 0.4f); v.KuroeInkLineStrike(Vector2.zero, Vector2.right * 2f); v.KuroeRedactionStrike(Vector2.zero, 0.6f);
            v.GaranConcussion(p, d, 1.3f, true, 0f, true); v.GaranGuardDent(p, d, 0f); v.GaranGroundSlam(Vector2.zero, 1f, true);
            v.GaranFissure(Vector2.zero, Vector2.right * 3f, 0.3f, true); v.GaranFissureMark(Vector2.zero, Vector2.right * 1.5f, 0.3f); v.GaranFissureStrike(Vector2.zero, Vector2.right * 1.5f);
            v.GaranPalmBlast(p, 1f, true, true); v.GaranSeiken(p, -1f); v.GaranLowKick(Vector2.zero, 1f); v.GaranScarCharge(p, p + d, true); v.GaranShikoCharge(p, p + d, Vector2.zero);
            v.HisameRainCut(p, d, 1.3f, true); v.HisameGuardSpray(p, d); v.HisameGust(p, -1f, -10f, true); v.HisameVortex(p, 1f, true); v.HisameAtemi(p, 1f);
            v.HisameSuspendedRain(p, p + Vector2.one, 0.4f); v.HisameRainCleave(p, p + Vector2.one); v.HisameMistScreen(Vector2.zero, 1f, true); v.HisameLanding(Vector2.zero, 1f);
            v.HisameCrackle(p); v.HisameGlint(p, Color.red, 1f);
            v.ShidenPierce(p, d, 1.25f, true, true); v.ShidenScreech(p, d); v.ShidenGlint(p); v.ShidenCrackle(p, true); v.ShidenMachRelease(p, p + d, true);
            v.ShidenRisingFang(p, p + Vector2.one, true); v.ShidenStrikeRow(Vector2.zero, Vector2.right * 2f, true); v.ShidenBindCross(p, 1f); v.ShidenLowSweep(Vector2.zero, -1f);
            // the signature pieces each look is built from (docs/act1-subbosses-vfx.md §3)
            v.InkSplat(p, d, 0.6f, 0.8f, false, Vfx.Order, true); v.KuroeSeal(p, Vfx.GlyphSeal, 0.5f, 1f, 0.3f, true, go.transform, Vector2.up);
            v.KuroeSeal(p, Vfx.GlyphCut, 0.5f, 1f); v.KuroeInkBlades(Vector2.zero, 0.8f, 5, 1.2f, true, 0.05f);
            v.GaranThunderStrike(Vector2.zero, 4f, 1f, true); v.GaranThunderDome(Vector2.zero, 1.4f, 0.3f, Color.white); v.GaranVoltBurst(p, d, 1f, 5, true);
            v.HisameWaterArc(p, 0f, 0.5f, 0.4f, -60f, 120f, 0.12f, 0.3f, d, 1.2f, false, Vfx.Order, true); v.HisameWaterRibbon(p, p + d * 2f, 0.1f, 0.08f, 0.4f);
            v.HisameRainVeil(p, 1f, 0.6f, 10, 0.4f, new Vector2(-0.3f, -1f));
            v.ShidenEnergyCrescent(p, 0f, 1f, 0.5f, 200f, 140f, 0.2f, 0.3f, d, 1.1f, true); v.ShidenThunderBlast(p, d, 1f, true, true); v.ShidenLance(p, d, 2.4f, true);

            var live = (System.Collections.IList)typeof(Vfx).GetField("_live", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(v);
            Assert.Greater(live.Count, 60, "the effects are alive");
            for (int step = 0; step <= 20; step++)
            {
                float u = step / 20f;
                for (int i = 0; i < live.Count; i++)
                    live[i].GetType().GetMethod("Tick").Invoke(live[i], new object[] { u });
            }

            // the trails: a sweep of 30 frames through each, then 30 frames of drying
            float clock = 0f;
            SampledTrail.TestClock = () => clock;
            var trails = new WeaponTrail[]
            {
                new KuroeInkTrail(go.transform, 0, null), new GaranSmearTrail(go.transform, 0, null, false), new GaranSmearTrail(go.transform, 0, null, true),
                new HisameFanTrail(go.transform, 0, null), new ShidenSpearTrail(go.transform, 0, null),
            };
            for (int f = 0; f < 60; f++)
            {
                clock = f / 60f;
                float ang = f * 7f * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang));
                foreach (var t in trails) t.Tick(f < 30, p + dir * 0.4f, p + dir * 1.2f);
            }
            foreach (var t in trails) t.Dispose();
        }
    }
}
