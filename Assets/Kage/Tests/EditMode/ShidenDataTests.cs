using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Shiden, the Last Lieutenant (SAN-106), the fourth Act I sub-boss, as data: his character entry (JS stats, the spear set with his
    /// Vanguard Yari, the boss profile, no-grab), his boss data (the two JS phases, aggressive in the second, the charging thrust in both),
    /// and the numbers of his abilities. The expected numbers are the JS game's (10-data.js ENEMIES.shiden and RAIKON_VARIANTS.vanguardBreak /
    /// vanguardGuard, 40-ai.js stormBreak, redlineCounter and charge, 30-combat.js PROJ.shock, 41-styles/shiden.js SHIDEN_BRACE and
    /// FIGHT_STYLES.shiden). How he fights is in ShidenFightTests.
    /// </summary>
    public class ShidenDataTests
    {
        const string Id = "shiden";
        const float Px = 1f / 115f;      // metres per JS pixel (ADR-0010)
        const float Scale = 1.12f;       // his JS build scale

        static CharacterDef Def() => CharacterCatalog.Get(Id);

        static AbilityDef Ability(string id)
        {
            Def();     // registers his abilities
            var a = AbilityCatalog.Get(id);
            Assert.IsNotNull(a, id + " is registered");
            return a;
        }

        // ---------------------------------------------------------------- character

        [Test]
        public void Shiden_is_a_catalog_entry_with_his_JS_stats()
        {
            var d = Def();
            Assert.IsNotNull(d, "no catalog entry 'shiden'");
            Assert.AreEqual("Shiden", d.displayName);
            Assert.AreEqual("the Last Lieutenant", d.title);
            Assert.AreEqual(400f, d.jsHealth, "41-styles/shiden.js lowers 10-data.js's 460 to 400");
            Assert.AreEqual(400f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.12f, d.defense, 1e-6);
            Assert.AreEqual(0.93f, d.damageMul, 1e-6, "41-styles/shiden.js lowers 1.05 to 0.93");
            Assert.AreEqual(1f, d.speedMul, 1e-6);
            Assert.AreEqual(Scale, d.bodyScale, 1e-6);
            Assert.AreEqual(CharacterFlags.NoGrab, d.flags & (CharacterFlags.Armour | CharacterFlags.NoGrab), "no-grab (JS noGrab), no heavy armour");
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void He_fights_with_the_Vanguard_Yari_on_the_spear_set_and_the_boss_profile()
        {
            var d = Def();
            Assert.AreEqual("vanguard_yari", d.weaponId);
            Assert.AreEqual(WeaponFamilies.Spear, d.family);
            Assert.AreEqual(WeaponFamilies.Spear, WeaponFamilies.FamilyIdOf(d.weaponId), "the JS weapon's family is the spear");
            Assert.AreEqual("boss", d.aiProfile);
            Assert.Greater(d.mass, 1f, "a broad armoured man (JS build w 11.4 at 1.12): heavier than Kage");
            Assert.AreEqual(80f / 0.9f, d.postureMax, 1e-3, "a sub-boss's 80, filling 0.9 times as fast (JS poise 0.9: disciplined)");
        }

        [Test]
        public void He_is_a_boss_with_the_JS_abilities_in_slot_order_and_the_charging_thrust_last()
        {
            var d = Def();
            Assert.AreEqual(Id, d.bossId);
            CollectionAssert.AreEqual(new[] { "vanguardBreak", "shidenBrace", "vanguardGuard", ShidenAbilities.ChargeId }, d.abilities,
                "the JS phases' abilities in first-use order, then the ticket's charging thrust");
        }

        // ---------------------------------------------------------------- phases

        [Test]
        public void His_boss_data_is_the_two_JS_phases_with_aggressive_in_the_second()
        {
            var b = BossCatalog.Get(Id);
            Assert.IsNotNull(b);
            Assert.AreEqual("Shiden", b.name);
            Assert.AreEqual("the Last Lieutenant", b.title);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual("Vanguard", b.phases[0].name);
            Assert.AreEqual(1f, b.phases[0].at);
            CollectionAssert.AreEquivalent(new[] { "vanguardBreak", "shidenBrace", ShidenAbilities.ChargeId }, b.phases[0].abilities);
            Assert.IsFalse(b.phases[0].aggressive);
            Assert.AreEqual("Hold the Line", b.phases[1].name);
            Assert.AreEqual(0.5f, b.phases[1].at, "the JS threshold");
            CollectionAssert.AreEquivalent(new[] { "vanguardBreak", "vanguardGuard", "shidenBrace", ShidenAbilities.ChargeId }, b.phases[1].abilities,
                "phase two adds Vanguard Guard");
            Assert.IsTrue(b.phases[1].aggressive, "and the JS aggressive flag");
            Assert.IsNull(b.phases[1].aiProfile, "he stays on the boss profile");
            Assert.IsFalse(b.phases[0].berserk || b.phases[1].berserk);
        }

        // ---------------------------------------------------------------- the generated numbers

        [Test]
        public void The_generated_tables_carry_the_numbers_his_abilities_read()
        {
            var vb = Kage.Rules.JsTables.BossAbility("vanguardBreak");
            Assert.AreEqual("stormBreak", vb.Base);
            Assert.AreEqual(5, vb.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 0.0, 300.0 }, vb.Range);
            Assert.AreEqual(0.6, vb.Chance, 1e-9);
            Assert.AreEqual(0.68, vb.Windup, 1e-9);
            Assert.AreEqual(0.8, vb.Dmg, 1e-9);
            Assert.AreEqual(0.7, vb.Shock, 1e-9);
            Assert.AreEqual(0.5, vb.ShockLife, 1e-9);

            var br = Kage.Rules.JsTables.BossAbility("shidenBrace");
            Assert.AreEqual(6.5, br.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 150.0, 420.0 }, br.Range);
            Assert.AreEqual(0.5, br.Chance, 1e-9);

            var b = Kage.Rules.JsTables.ShidenBrace;
            Assert.AreEqual(1.25, b.Hold, 1e-9);
            Assert.AreEqual(280, b.Reach, 1e-9);
            Assert.AreEqual(215, b.Trigger, 1e-9);
            Assert.AreEqual(15, b.Dmg, 1e-9);
            Assert.AreEqual(0.55, b.Open, 1e-9);

            var vg = Kage.Rules.JsTables.BossAbility("vanguardGuard");
            Assert.AreEqual("redlineCounter", vg.Base);
            Assert.AreEqual(7.5, vg.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 60.0, 280.0 }, vg.Range);
            Assert.AreEqual(0.5, vg.Chance, 1e-9);
            Assert.AreEqual(0.8, vg.Stance, 1e-9);
            Assert.AreEqual(0.75, vg.Dmg, 1e-9);
            Assert.AreEqual(false, vg.Unb, "the punish can be blocked");

            Assert.AreEqual(0.9, Kage.Rules.JsTables.FightStyle(Id).Poise, 1e-9);
            Assert.AreEqual("Sōjutsu · Hōzōin-ryū", Kage.Rules.JsTables.FightStyle(Id).Art);
        }

        [Test]
        public void His_JS_abilities_have_the_JS_cooldown_range_and_chance()
        {
            var vb = Ability("vanguardBreak");
            Assert.AreEqual(300, vb.cooldownFrames, "5 s");
            Assert.AreEqual(0f, vb.range.x, 1e-4);
            Assert.AreEqual(300f * Px, vb.range.y, 1e-4);
            Assert.AreEqual(0.6f, vb.chance, 1e-6);

            var br = Ability("shidenBrace");
            Assert.AreEqual(390, br.cooldownFrames, "6.5 s");
            Assert.AreEqual(150f * Px, br.range.x, 1e-4);
            Assert.AreEqual(420f * Px, br.range.y, 1e-4);
            Assert.AreEqual(0.5f, br.chance, 1e-6);

            var vg = Ability("vanguardGuard");
            Assert.AreEqual(450, vg.cooldownFrames, "7.5 s");
            Assert.AreEqual(60f * Px, vg.range.x, 1e-4);
            Assert.AreEqual(280f * Px, vg.range.y, 1e-4);
            Assert.AreEqual(0.5f, vg.chance, 1e-6);
        }

        // ---------------------------------------------------------------- the moves

        [Test]
        public void Vanguard_Break_is_an_armoured_overhead_wind_up_then_a_slam_that_lays_the_shockwave()
        {
            var m = Ability("vanguardBreak").move;
            Assert.IsNotNull(m);
            var slam = m.hits.Single();
            Assert.AreEqual(40, slam.from, "the spear lands on frame 40: 0.68 x Normal 1 x the sub-boss role's 0.98 = 0.666 s");
            Assert.AreEqual(85, m.total, "the wind-up + the JS 0.75 s");
            Assert.AreEqual(12, slam.dmg, "19 x the variant's 0.8 = 15.2 JS, x 0.8");
            Assert.AreEqual(HitLevel.Overhead, slam.level);
            Assert.IsTrue(slam.knockdown);
            Assert.AreEqual(1.1f * 60f, slam.dmg * 1.5f * slam.postureMul, 1e-3, "JS gb 110 of the JS guard's 100: a guarded slam breaks Kage's 60 (Hisame's share-of-posture rule)");
            Assert.IsTrue(m.Armoured(0) && m.Armoured(40), "the JS super armour through the wind-up");
            Assert.IsFalse(m.Armoured(41));

            var h = m.hazard;
            Assert.IsNotNull(h, "the shockwave is a hazard (ADR-0007)");
            Assert.AreEqual(1, m.hazardFrame, "laid with the wind-up: the ground mark shows through it");
            Assert.AreEqual(39, h.telegraphFrames, "...and strikes with the slam");
            var shock = Kage.Rules.JsTables.Projectile("shock");
            Assert.AreEqual(Mathf.RoundToInt((float)(shock.Life * 0.5 * 60)), h.strikeFrames, "the JS shockwave's life x the variant's 0.5");
            float run = (float)(shock.Speed * shock.Life * 0.5);
            Assert.AreEqual((run + 2f * (float)shock.R) * Px, h.size.x, 1e-4, "the shockwave's run plus its width");
            Assert.AreEqual((120f * Scale + run / 2f) * Px, m.hazardAhead, 1e-4, "starting 120 px x his scale in front");
            Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt((float)(shock.Dmg * 0.7) * 0.8f)), h.hit.dmg, "the JS shock damage x the variant's 0.7, x 0.8");
            Assert.AreNotEqual(HitLevel.Unblockable, h.hit.level);
        }

        [Test]
        public void Brace_sets_the_spear_holds_it_for_the_JS_hold_then_lifts_it_open()
        {
            var m = Ability("shidenBrace").move;
            Assert.IsNotNull(m);
            Assert.AreEqual(13, m.braceFrom, "set: 0.22 x 0.98 s");
            Assert.AreEqual(87, m.braceTo, "held the JS 1.25 s");
            Assert.AreEqual(121, m.total, "then open the JS 0.55 s");
            Assert.AreEqual(215f * Scale * Px, m.braceTrigger, 1e-4, "a rushing Kage within 215 px x his scale sets it off");
            Assert.IsEmpty(m.hits, "the brace itself strikes nothing");
            Assert.AreEqual(-1, m.counterFrom, "and it is not a counter window: it reads a rush, not a blow");

            var t = m.braceAnswer;
            Assert.IsNotNull(t, "the thrust");
            var w = t.hits.Single();
            Assert.AreEqual(0, w.from);
            Assert.AreEqual(14, w.to, "live the JS 0.24 s");
            Assert.AreEqual(30, t.total, "over at the JS 0.5 s");
            Assert.AreEqual(12, w.dmg, "the JS 15, x 0.8");
            Assert.IsTrue(w.critical, "a counter-hit critical (FIGHT_STYLES.shiden.crit)");
            Assert.AreNotEqual(HitLevel.Unblockable, w.level);
        }

        [Test]
        public void Hold_the_Line_is_a_counter_stance_held_the_JS_stance_with_a_blockable_punish()
        {
            var m = Ability("vanguardGuard").move;
            Assert.IsNotNull(m);
            Assert.AreEqual(48, m.counterTo - m.counterFrom + 1, "the JS stance 0.8 s");
            Assert.IsTrue(m.counterFrontOnly, "a counter stance (SAN-103)");
            Assert.Greater(m.total, m.counterTo + 1, "the guard drops after the window: an opening");
            var p = m.counterAnswer;
            Assert.IsNotNull(p, "the punish");
            var w = p.hits.Single();
            Assert.AreEqual(6, w.from, "the redlineCounter's default 0.1 s delay");
            Assert.AreEqual(9, w.dmg, "the Kage Counter's 12 x the variant's 0.75");
            Assert.AreEqual(HitLevel.Mid, w.level, "JS unb 0: blockable");
        }

        [Test]
        public void The_charging_thrust_winds_up_readably_then_runs_most_of_the_arena_point_first()
        {
            var a = Ability(ShidenAbilities.ChargeId);
            var charge = Kage.Rules.JsTables.BossAbility("charge");
            Assert.AreEqual(Mathf.RoundToInt((float)(charge.Cd * 60)), a.cooldownFrames, "the JS charge's cooldown");
            Assert.AreEqual((float)charge.Chance, a.chance, 1e-6, "and chance");
            Assert.AreEqual(320f * Px, a.range.x, 1e-4, "from the JS dash thrust's 320 px (FIGHT_STYLES.shiden ['dash', 'weapon'])");

            var m = a.move;
            var run = m.motions.Single(e => e.vel.x > 0f);
            Assert.AreEqual(35, run.from, "a readable wind-up: the JS charge's 0.6 s tell x 0.98");
            Assert.AreEqual(900f * Px, run.vel.x, 1e-3, "running at the JS dash's 900 px/s");
            float dist = run.vel.x * (run.to - run.from + 1) / 60f;
            Assert.Greater(dist, 7.6f, "more than half the arena's 15.2 m");
            Assert.AreEqual(dist, a.range.y, 0.05f, "used no further away than the run carries");
            var w = m.hits.Single();
            Assert.AreEqual(run.from, w.from, "the point is out the whole run");
            Assert.AreEqual(run.to, w.to);
            Assert.AreEqual(new[] { HitPoint.Blade }, w.points, "the spear only: nothing high or low to catch a jump or a roll");
            Assert.AreNotEqual(HitLevel.Unblockable, w.level, "it can be guarded or parried");
            Assert.IsFalse(m.Armoured(10), "no armour: a skilled spearman, not a wall");
            Assert.IsTrue(m.PassThrough(run.from + 10), "he runs on past a fighter who got out of the way");
            Assert.IsNotNull(m.onContact, "a blow that lands or is blocked stops him");
            Assert.IsTrue(m.cues.Any(c => c.cue == ShidenAbilities.ChargeGlintCue && c.frame < run.from), "a glint on the point before the run");
        }

        // ---------------------------------------------------------------- look and arena

        [Test]
        public void His_arena_is_the_districts()
        {
            Assert.AreEqual("districts", BossArena.EnvFor(Id), "act1.sub4");
        }

        [Test]
        public void His_part_set_draws_the_kabuto_maedate_do_sode_and_banner_on_Kages_skeleton_in_his_own_palette()
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
            foreach (var name in new[] { "Kabuto", "Maedate", "Hoate", "Do", "SodeN", "Kusazuri_Front", "Banner", "BannerPole" })
                Assert.IsTrue(rig.parts.Any(p => p.name == name), "his " + name);
            foreach (var other in new[] { Kuroe.Id, Garan.Id, Hisame.Id, "order_footman" })
            {
                var o = CharacterCatalog.PartSet(other).Build(CharacterCatalog.Get(other));
                Assert.AreNotEqual(o.rimColor, rig.rimColor, "not " + other + "'s palette");
            }
        }
    }
}
