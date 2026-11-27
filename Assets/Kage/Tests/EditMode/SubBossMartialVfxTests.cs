using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Act I sub-boss martial-arts kits, Kuroe's Ichimonji calligraphy ink brush-pen, Raikon's lightning lineage, and sub-boss VFX
    /// (docs/act1-subbosses-vfx.md): verifies weapon geometry, ability registration, cue registration, phase wind/aura escalation,
    /// and headless combat execution of the new hazards and projectiles.
    /// </summary>
    public class SubBossMartialVfxTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        readonly List<GameObject> _roots = new();
        SimulationMode2D _simMode;

        [SetUp]
        public void SetUp()
        {
            _simMode = Physics2D.simulationMode;
            foreach (var id in new[] { Kuroe.Id, Garan.Id, Hisame.Id, Shiden.Id, Raikon.Id })
                CharacterCatalog.Get(id);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
        }

        (FightLoop loop, FightContext ctx, Fighter kage, Fighter boss) SpawnBoss(string bossId, float kageX = -1.5f, float bossX = 1.5f)
        {
            var root = new GameObject("SubBossFight_" + bossId);
            _roots.Add(root);
            var b = new FightBuilder(root.transform, 5150, manualClock: true);
            b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var boss = b.Spawn(CharacterCatalog.Get(bossId), 1, new StandStill());
            var ctx = b.Build();
            ctx.Begin();
            kage.ResetForRound(kageX, 1f); kage.BeginFight();
            boss.ResetForRound(bossX, -1f); boss.BeginFight();
            b.Loop.StepHeadless();
            return (b.Loop, ctx, kage, boss);
        }

        static void Run(FightLoop loop, int frames, System.Action each = null)
        {
            for (int i = 0; i < frames; i++) { loop.StepHeadless(); each?.Invoke(); }
        }

        [Test]
        public void Kuroes_weapon_draws_an_ink_brush_pen_with_ferrule_nib_and_ink_soaked_bristles()
        {
            var (_, _, _, kuroe) = SpawnBoss(Kuroe.Id);
            var names = kuroe.rig.Renderers.Where(r => r != null).Select(r => r.name).ToHashSet();
            Assert.IsTrue(names.Contains("Spear_PenFerrule"), "stepped calligraphy ferrule");
            Assert.IsTrue(names.Contains("Spear_PenNib"), "fountain-nib collar");
            Assert.IsTrue(names.Contains("Spear_PenSlit"), "ink channel slit");
            Assert.IsTrue(names.Contains("Spear_Blade"), "tapered calligraphy brush head");
            Assert.IsTrue(names.Contains("Spear_BrushInk"), "sumi-soaked black ink core");
            Assert.IsTrue(names.Contains("Spear_BrushSheen"), "wet ink sheen");
            Assert.IsFalse(names.Contains("Spear_CapTip"), "no longer a plain capped wooden staff");
        }

        [Test]
        public void Every_new_sub_boss_ability_is_registered_with_moves_cooldowns_and_registered_cues()
        {
            KuroeAbilities.RegisterCues();
            GaranAbilities.RegisterCues();
            HisameAbilities.RegisterCues();
            ShidenAbilities.RegisterCues();
            RaikonAscendant.RegisterCues();

            var allExtra = KuroeAbilities.ExtraIds
                .Concat(GaranAbilities.ExtraIds)
                .Concat(HisameAbilities.ExtraIds)
                .Concat(ShidenAbilities.ExtraIds)
                .ToArray();
            Assert.AreEqual(13, allExtra.Length);
            foreach (var id in allExtra)
            {
                var a = AbilityCatalog.Get(id);
                Assert.IsNotNull(a, id + " registered");
                Assert.IsNotNull(a.move, id + " has MoveDef");
                Assert.Greater(a.cooldownFrames, 0, id + " has cooldown");
                Assert.IsNotEmpty(a.move.impulses, id + " drives rig secondary motion");
            }

            var cueIds = KuroeAbilities.CueIds
                .Concat(GaranAbilities.CueIds)
                .Concat(HisameAbilities.CueIds)
                .Concat(ShidenAbilities.CueIds);
            foreach (var cue in cueIds)
                Assert.IsTrue(EffectCues.IsRegistered(cue), cue + " is registered in EffectCues");
        }

        [TestCase("kuroe", SubBossAura.Kind.Kuroe)]
        [TestCase("garan", SubBossAura.Kind.Garan)]
        [TestCase("hisame", SubBossAura.Kind.Hisame)]
        [TestCase("shiden", SubBossAura.Kind.Shiden)]
        public void Dropping_to_half_health_escalates_each_sub_boss_aura_to_phase_two_and_ramps_wind(string id, SubBossAura.Kind kind)
        {
            var (loop, _, kage, boss) = SpawnBoss(id);
            var aura = boss.GetComponent<SubBossAura>();
            Assert.IsNotNull(aura, id + " has SubBossAura");
            Assert.AreEqual(kind, aura.BossKind);
            Assert.AreEqual(0, aura.Level, "phase 1 starts at level 0");

            float wind0 = boss.rig.wind.steady.magnitude;
            boss.health = boss.maxHealth * 0.49f;
            Run(loop, 60);
            Assert.AreEqual(1, boss.Phases.Index, id + " entered phase 2");
            Assert.AreEqual(1, aura.Level, id + " escalated aura to Raikon-trained phase 2");
            Assert.Greater(boss.rig.wind.steady.magnitude, wind0 + 0.3f, id + " phase 2 increased arena wind");
            Assert.AreEqual(boss.rig.wind.steady, kage.rig.wind.steady, "wind applies to Kage's rig as well");
        }

        [Test]
        public void Kuroes_Bokuseki_launches_an_ink_wave_and_Phase_2_unlocks_ink_lightning_line_and_Thunder_Redaction()
        {
            var (loop, ctx, kage, kuroe) = SpawnBoss(Kuroe.Id, -2.5f, 1.5f);
            kuroe.StartMove(KuroeAbilities.BokusekiMove);
            bool waveSeen = false;
            Run(loop, 40, () =>
            {
                kage.health = kage.maxHealth;
                waveSeen |= ctx.Projectiles.Any(p => p.Def.id == KuroeAbilities.InkWaveProjectileId);
            });
            Assert.IsTrue(waveSeen, "Bokuseki Wave spawned the sumi ink crescent projectile");

            kuroe.Phases.JumpTo(1);
            var stormMove = AbilityCatalog.Get(KuroeAbilities.SumiNidanId).move.variantFor(kuroe);
            Assert.AreSame(KuroeAbilities.SumiNidanStormMove, stormMove, "Phase 2 upgrades Sumi Nidan with Raikon's floor spark line");

            kuroe.StartMove(KuroeAbilities.ThunderRedactionMove);
            bool sealSeen = false;
            Run(loop, 45, () =>
            {
                kage.health = kage.maxHealth;
                sealSeen |= ctx.Hazards.Any(h => h.Def == KuroeAbilities.RedactionSeal);
            });
            Assert.IsTrue(sealSeen, "Thunder Redaction placed the tracking Censor Seal under Kage");
        }

        [Test]
        public void Garans_Shiko_Stomp_Hisames_Inazuma_Step_and_Shidens_Raijin_Lance_echo_Raikons_lightning_lessons()
        {
            var (gLoop, gCtx, gKage, garan) = SpawnBoss(Garan.Id, -1.8f, 1.2f);
            garan.StartMove(GaranAbilities.ShikoStompMove);
            bool shikoSpark = false;
            Run(gLoop, 40, () => { gKage.health = gKage.maxHealth; shikoSpark |= gCtx.Hazards.Any(h => h.Def == GaranAbilities.ShikoSparkLine); });
            Assert.IsTrue(shikoSpark, "Garan's Shiko Raijin Stomp laid an apprentice floor spark line");

            var (hLoop, hCtx, hKage, hisame) = SpawnBoss(Hisame.Id, -1.2f, 1.2f);
            hisame.Phases.JumpTo(1);
            hisame.StartMove(HisameAbilities.InazumaStepMove);
            bool delayedSlash = false;
            Run(hLoop, 40, () => { hKage.health = hKage.maxHealth; delayedSlash |= hCtx.Hazards.Any(h => h.Def == HisameAbilities.InazumaDelayedSlash); });
            Assert.IsTrue(delayedSlash, "Hisame's Inazuma Mist Step left a delayed electrified rain slash");

            var (sLoop, sCtx, sKage, shiden) = SpawnBoss(Shiden.Id, -2.5f, 1.5f);
            shiden.Phases.JumpTo(1);
            shiden.StartMove(ShidenAbilities.RaijinLanceMove);
            bool raijinWave = false;
            Run(sLoop, 45, () => { sKage.health = sKage.maxHealth; raijinWave |= sCtx.Projectiles.Any(p => p.Def.id == ShidenAbilities.RaijinWaveId); });
            Assert.IsTrue(raijinWave, "Shiden's Vanguard Raijin Arc launched a ground-skimming spark wave");
        }
    }
}
