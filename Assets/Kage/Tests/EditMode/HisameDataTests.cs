using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Hisame, the Rain Courier (SAN-105), the third Act I sub-boss, as data: her character entry (JS stats, the twin blades set with her
    /// Mist Fans, the agile profile, a light body), her boss data (the two JS phases, berserk in the second), and the numbers of her three
    /// abilities. The expected numbers are the JS game's (10-data.js ENEMIES.hisame and RAIKON_VARIANTS.lightningCut, 40-ai.js
    /// thunderSwing and BOSS_ABILITIES.smokeBomb, 30-combat.js PROJ.smoke, 41-styles/hisame.js). How she fights is in HisameFightTests.
    /// </summary>
    public class HisameDataTests
    {
        const string Id = "hisame";
        const float Px = 1f / 115f;      // metres per JS pixel (ADR-0010)

        static CharacterDef Def() => CharacterCatalog.Get(Id);

        static AbilityDef Ability(string id)
        {
            Def();     // registers her abilities
            var a = AbilityCatalog.Get(id);
            Assert.IsNotNull(a, id + " is registered");
            return a;
        }

        // ---------------------------------------------------------------- character

        [Test]
        public void Hisame_is_a_catalog_entry_with_her_JS_stats()
        {
            var d = Def();
            Assert.IsNotNull(d, "no catalog entry 'hisame'");
            Assert.AreEqual("Hisame", d.displayName);
            Assert.AreEqual("the Rain Courier", d.title);
            Assert.AreEqual(375f, d.jsHealth, "41-styles/hisame.js raises 10-data.js's 330 to 375");
            Assert.AreEqual(375f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.04f, d.defense, 1e-6);
            Assert.AreEqual(0.95f, d.damageMul, 1e-6);
            Assert.AreEqual(1.16f, d.speedMul, 1e-6);
            Assert.AreEqual(0.95f, d.bodyScale, 1e-6);
            Assert.AreEqual((CharacterFlags)0, d.flags & (CharacterFlags.Armour | CharacterFlags.NoGrab), "no heavy armour, and a throw takes her");
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void She_fights_with_the_Mist_Fans_on_the_twin_blades_set_and_the_agile_profile_with_a_light_body()
        {
            var d = Def();
            Assert.AreEqual("mist_fans", d.weaponId);
            Assert.AreEqual(WeaponFamilies.Twin, d.family);
            Assert.AreEqual(WeaponFamilies.Twin, WeaponFamilies.FamilyIdOf(d.weaponId), "the JS weapon's family is the twin blades");
            Assert.AreEqual("agile", d.aiProfile);
            Assert.Less(d.mass, 1f, "lighter than Kage");
            Assert.AreEqual(80f / 1.15f, d.postureMax, 1e-3, "a sub-boss's 80, filling 1.15 times as fast (JS poise 1.15: light)");
        }

        [Test]
        public void She_is_a_boss_with_the_JS_abilities_in_slot_order()
        {
            var d = Def();
            Assert.AreEqual(Id, d.bossId);
            CollectionAssert.AreEqual(new[] { "lightningCut", "hisameKocho", "smokeBomb" }, d.abilities, "the JS phases' abilities in first-use order");
        }

        // ---------------------------------------------------------------- phases

        [Test]
        public void Her_boss_data_is_the_two_JS_phases_with_berserk_in_the_second()
        {
            var b = BossCatalog.Get(Id);
            Assert.IsNotNull(b);
            Assert.AreEqual("Hisame", b.name);
            Assert.AreEqual("the Rain Courier", b.title);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual("Rainfall", b.phases[0].name);
            Assert.AreEqual(1f, b.phases[0].at);
            CollectionAssert.AreEquivalent(new[] { "lightningCut", "hisameKocho" }, b.phases[0].abilities);
            Assert.IsFalse(b.phases[0].berserk);
            Assert.AreEqual("Downpour", b.phases[1].name);
            Assert.AreEqual(0.5f, b.phases[1].at, "the JS threshold");
            CollectionAssert.AreEquivalent(new[] { "lightningCut", "smokeBomb", "hisameKocho" }, b.phases[1].abilities, "phase two adds Smoke Bomb");
            Assert.IsTrue(b.phases[1].berserk, "and the JS berserk flag");
            Assert.IsNull(b.phases[1].aiProfile, "she stays on the agile profile");
            Assert.IsFalse(b.phases[0].aggressive || b.phases[1].aggressive);
        }

        // ---------------------------------------------------------------- the generated numbers

        [Test]
        public void The_generated_tables_carry_the_numbers_her_abilities_read()
        {
            var lc = Kage.Rules.JsTables.BossAbility("lightningCut");
            Assert.AreEqual("thunderSwing", lc.Base);
            Assert.AreEqual(5, lc.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 120.0, 420.0 }, lc.Range);
            Assert.AreEqual(0.65, lc.Chance, 1e-9);
            Assert.AreEqual(0.56, lc.Windup, 1e-9);
            Assert.AreEqual(0.6, lc.Dmg, 1e-9);
            Assert.AreEqual(160, lc.Reach, 1e-9);
            Assert.AreEqual(900, lc.Dash, 1e-9);
            Assert.AreEqual(false, lc.Wave, "no ground wave");

            var kocho = Kage.Rules.JsTables.BossAbility("hisameKocho");
            Assert.AreEqual(4.5, kocho.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 60.0, 300.0 }, kocho.Range);
            Assert.AreEqual(0.6, kocho.Chance, 1e-9);

            var bomb = Kage.Rules.JsTables.BossAbility("smokeBomb");
            Assert.AreEqual(6, bomb.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 140.0, 700.0 }, bomb.Range);
            Assert.AreEqual(0.6, bomb.Chance, 1e-9);

            var smoke = Kage.Rules.JsTables.Projectile("smoke");
            Assert.AreEqual(14, smoke.R, 1e-9);
            Assert.AreEqual(4, smoke.Dmg, 1e-9);
            Assert.AreEqual(1.6, smoke.Life, 1e-9);
            Assert.AreEqual(1500, smoke.Grav, 1e-9);

            var tw1 = Kage.Rules.JsTables.Move("tw1");
            Assert.IsNotNull(tw1, "the export now carries the twin blades' first cut");
            CollectionAssert.AreEqual(new[] { 0.05, 0.05, 0.13 }, tw1.T);
            Assert.AreEqual(5, tw1.Dmg, 1e-9);

            Assert.AreEqual("Tessenjutsu", Kage.Rules.JsTables.FightStyle(Id).Art);
        }

        [Test]
        public void Her_abilities_have_the_JS_cooldown_range_and_chance()
        {
            var lc = Ability("lightningCut");
            Assert.AreEqual(300, lc.cooldownFrames, "5 s");
            Assert.AreEqual(120f * Px, lc.range.x, 1e-4);
            Assert.AreEqual(420f * Px, lc.range.y, 1e-4);
            Assert.AreEqual(0.65f, lc.chance, 1e-6);
            Assert.IsNull(lc.when);

            var k = Ability("hisameKocho");
            Assert.AreEqual(270, k.cooldownFrames, "4.5 s");
            Assert.AreEqual(60f * Px, k.range.x, 1e-4);
            Assert.AreEqual(300f * Px, k.range.y, 1e-4);
            Assert.AreEqual(0.6f, k.chance, 1e-6);
            Assert.IsNotNull(k.when, "only when Kage chases her (JS hisameChased)");

            var sb = Ability("smokeBomb");
            Assert.AreEqual(360, sb.cooldownFrames, "6 s");
            Assert.AreEqual(140f * Px, sb.range.x, 1e-4);
            Assert.AreEqual(700f * Px, sb.range.y, 1e-4);
            Assert.AreEqual(0.6f, sb.chance, 1e-6);
        }

        // ---------------------------------------------------------------- the moves

        [Test]
        public void Lightning_Cut_is_a_low_armoured_tell_then_one_cut_dashing_straight_through()
        {
            var m = Ability("lightningCut").move;
            Assert.IsNotNull(m);
            var cut = m.hits.Single();
            var dash = m.motions.Single();
            Assert.AreEqual(33, dash.from, "the tell: 0.56 x Normal 1 x the sub-boss role's 0.98 = 0.549 s");
            Assert.AreEqual(10, dash.to - dash.from + 1, "the dash runs the JS 0.16 s");
            Assert.AreEqual(900f * Px, dash.vel.x, 1e-3, "at 900 px/s");
            Assert.IsTrue(cut.from <= 37 && cut.to >= 37, "the cut is live 0.06 s into the dash");
            Assert.AreEqual(69, m.total, "the wind-up + the JS 0.6 s");
            Assert.AreEqual(7, cut.dmg, "15 x the variant's 0.6 = 9 JS, x 0.8");
            Assert.AreEqual(0.7f * 60f, cut.dmg * 1.5f * cut.postureMul, 1e-3, "JS gb 70 of the JS guard's 100: 70% of Kage's 60 posture on a guard");
            Assert.AreNotEqual(HitLevel.Unblockable, cut.level, "the JS cut can be blocked");
            Assert.IsFalse(cut.knockdown);
            Assert.IsTrue(m.Armoured(0) && m.Armoured(32), "armoured through the tell (JS super armour)");
            Assert.IsFalse(m.Armoured(33), "...not from the release");
            Assert.IsTrue(m.PassThrough(34) && m.PassThrough(42), "straight through him");
            Assert.AreEqual(-1, m.projectileFrame, "no ground wave");
            Assert.IsNull(m.hazard);
        }

        [Test]
        public void Kocho_is_invulnerable_through_the_step_and_cuts_a_beat_after_the_glint()
        {
            var m = Ability("hisameKocho").move;
            Assert.IsNotNull(m);
            Assert.AreEqual(8, m.vaultFrom, "the fans spread: 0.14 x 0.98 s");
            Assert.AreEqual(28, m.vaultTo, "...then the JS 0.34 s arc");
            Assert.AreEqual(110f * 0.95f * Px, m.vaultPast, 1e-4, "she lands 110 px x her scale past him");
            Assert.AreEqual(150f * 0.95f * Px, m.vaultHeight, 1e-4, "an arc 150 px x her scale high");
            Assert.IsTrue(Enumerable.Range(0, 29).All(m.Invulnerable), "invulnerable from the start to the landing (JS invuln W + E)");
            Assert.IsFalse(m.Invulnerable(29), "not through the cut");
            var glint = m.cues.Single(c => c.cue == HisameAbilities.KochoGlintCue);
            Assert.AreEqual(28, glint.frame, "the glint as she lands");
            var cut = m.hits.Single();
            Assert.AreEqual(12, cut.from - glint.frame, "the cut 0.2 s after the glint (JS tw1 held to 0.2 s)");
            Assert.AreEqual(3, cut.to - cut.from + 1, "tw1's 0.05 s");
            Assert.AreEqual(4, cut.dmg, "tw1's 5 JS, x 0.8");
            Assert.AreEqual(51, m.total, "and tw1's 0.13 s recovery");
        }

        [Test]
        public void Smoke_Bomb_lobs_the_JS_smoke_projectile()
        {
            var m = Ability("smokeBomb").move;
            Assert.IsNotNull(m);
            Assert.AreEqual(30, m.total, "the JS 0.5 s throw");
            Assert.AreEqual(11, m.projectileFrame, "the bomb leaves at 0.18 s");
            var p = m.projectile;
            Assert.IsNotNull(p, "a thrown projectile (ADR-0007)");
            Assert.AreEqual(380f * Px, p.speed, 1e-4, "JS vx rand(380, 560)");
            Assert.AreEqual(560f * Px, p.speedMax, 1e-4);
            Assert.AreEqual(520f * Px, p.launchVy, 1e-4, "thrown up at 520 px/s");
            Assert.AreEqual(1500f * Px, p.gravityScale * -Physics2D.gravity.y, 1e-3, "falling at the JS 1500 px/s²");
            Assert.AreEqual(14f * Px, p.radius, 1e-4);
            Assert.AreEqual(96, p.lifeFrames, "1.6 s");
            Assert.AreEqual(3, p.hit.dmg, "4 JS x 0.8");
            Assert.AreNotEqual(HitLevel.Unblockable, p.hit.level);
            Assert.AreEqual(HisameAbilities.SmokeBurstCue, p.endCue, "it bursts into smoke where it lands");
        }

        // ---------------------------------------------------------------- look and arena

        [Test]
        public void Her_arena_is_the_districts()
        {
            Assert.AreEqual("districts", BossArena.EnvFor(Id), "act1.sub3");
        }

        [Test]
        public void Her_part_set_draws_her_hood_visor_mino_and_tube_on_Kages_skeleton_in_her_own_palette()
        {
            var d = Def();
            Assert.AreEqual(Id, d.partSet);
            var set = CharacterCatalog.PartSet(d.partSet);
            Assert.IsNotNull(set);
            var rig = set.Build(d);
            Assert.IsNotEmpty(rig.parts);
            Assert.IsFalse(rig.scarf);
            foreach (var p in rig.parts)
            {
                bool drawn = p.pts != null ? p.pts.Length >= 3 : p.polys != null && p.polys.Count > 0;
                Assert.IsTrue(drawn, "part " + p.name + " has no shape");
            }
            foreach (var name in new[] { "Hood", "Visor", "FaceCloth", "Capelet", "Mino_Upper", "Mino_Lower", "Tube" })
                Assert.IsTrue(rig.parts.Any(p => p.name == name), "her " + name);
            foreach (var other in new[] { Kuroe.Id, Garan.Id, "order_footman" })
            {
                var o = CharacterCatalog.PartSet(other).Build(CharacterCatalog.Get(other));
                Assert.AreNotEqual(o.rimColor, rig.rimColor, "not " + other + "'s palette");
            }
        }
    }
}
