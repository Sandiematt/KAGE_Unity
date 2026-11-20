using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Garan, the Stormbroken (SAN-104), the second Act I sub-boss, as data: his character entry (JS stats, the heavy fists set, the heavy
    /// profile, heavy armour, heavy mass), his boss data (the two JS phases, with his clinch added to both), and the numbers of his four
    /// abilities. The expected numbers are the JS game's (10-data.js ENEMIES.garan and RAIKON_VARIANTS.stormFist, 40-ai.js BOSS_ABILITIES.charge,
    /// 41-styles/garan.js). How he fights is in GaranFightTests.
    /// </summary>
    public class GaranDataTests
    {
        const string Id = "garan";

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
        public void Garan_is_a_catalog_entry_with_his_JS_stats()
        {
            var d = Def();
            Assert.IsNotNull(d, "no catalog entry 'garan'");
            Assert.AreEqual("Garan", d.displayName);
            Assert.AreEqual("the Stormbroken", d.title);
            Assert.AreEqual(395f, d.jsHealth, "41-styles/garan.js lowers 10-data.js's 420 to 395");
            Assert.AreEqual(395f * 1.25f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.1f, d.defense, 1e-6);
            Assert.AreEqual(1.1f, d.damageMul, 1e-6);
            Assert.AreEqual(0.92f, d.speedMul, 1e-6);
            Assert.AreEqual(1.18f, d.bodyScale, 1e-6);
            Assert.IsTrue((d.flags & CharacterFlags.Armour) != 0, "JS heavyArmor: light hits don't flinch him");
            Assert.IsTrue((d.flags & CharacterFlags.NoGrab) == 0, "he can be thrown");
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void He_fights_with_the_iron_fists_on_the_fists_set_and_the_heavy_profile_with_heavy_mass()
        {
            var d = Def();
            Assert.AreEqual("iron_fists", d.weaponId);
            Assert.AreEqual(WeaponFamilies.Fists, d.family);
            Assert.AreEqual(WeaponFamilies.Fists, WeaponFamilies.FamilyIdOf(d.weaponId), "the JS weapon's family is the fists set");
            Assert.AreEqual("heavy", d.aiProfile);
            Assert.Greater(d.mass, 1.5f, "heavy: well over Kage's 1");
            Assert.Greater(d.mass, CharacterCatalog.Get(Kuroe.Id).mass, "heavier than Kuroe");
        }

        [Test]
        public void He_is_a_boss_with_the_JS_abilities_in_slot_order_then_his_clinch()
        {
            var d = Def();
            Assert.AreEqual(Id, d.bossId);
            CollectionAssert.AreEqual(new[] { "stormFist", "garanTsuppari", "charge", "garan.clinch" }, d.abilities,
                "the JS phases' abilities in first-use order, then the clinch the SAN-104 comment adds");
        }

        // ---------------------------------------------------------------- phases

        [Test]
        public void His_boss_data_is_the_two_JS_phases_with_the_clinch_in_both()
        {
            var b = BossCatalog.Get(Id);
            Assert.IsNotNull(b);
            Assert.AreEqual("Garan", b.name);
            Assert.AreEqual("the Stormbroken", b.title);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual("Thunderstruck", b.phases[0].name);
            Assert.AreEqual(1f, b.phases[0].at);
            CollectionAssert.AreEquivalent(new[] { "stormFist", "garanTsuppari", "garan.clinch" }, b.phases[0].abilities);
            Assert.AreEqual("Stormbroken", b.phases[1].name);
            Assert.AreEqual(0.5f, b.phases[1].at, "the JS threshold");
            CollectionAssert.AreEquivalent(new[] { "stormFist", "charge", "garanTsuppari", "garan.clinch" }, b.phases[1].abilities, "phase two adds the charge");
            Assert.IsNull(b.phases[1].aiProfile, "he stays on the heavy profile");
            Assert.IsFalse(b.phases[0].aggressive || b.phases[1].aggressive);
        }

        // ---------------------------------------------------------------- the generated numbers

        [Test]
        public void The_generated_tables_carry_the_numbers_his_abilities_read()
        {
            var sf = Kage.Rules.JsTables.BossAbility("stormFist");
            Assert.AreEqual("stormBreak", sf.Base);
            Assert.AreEqual(5.6, sf.Cd, 1e-9);
            Assert.AreEqual(0.78, sf.Windup, 1e-9);
            Assert.AreEqual(0.65, sf.Dmg, 1e-9);
            Assert.AreEqual(0.5, sf.Shock, 1e-9);
            Assert.AreEqual(0.4, sf.ShockLife, 1e-9);
            Assert.AreEqual(170, sf.Reach, 1e-9);

            var shock = Kage.Rules.JsTables.Projectile("shock");
            Assert.AreEqual(760, shock.Speed, 1e-9);
            Assert.AreEqual(34, shock.R, 1e-9);
            Assert.AreEqual(13, shock.Dmg, 1e-9);
            Assert.AreEqual(0.75, shock.Life, 1e-9);
            Assert.IsTrue(shock.Kd && shock.Ground && !shock.Unb);

            var grab = Kage.Rules.JsTables.Move("grab");
            CollectionAssert.AreEqual(new[] { 0.06, 0.08, 0.3 }, grab.T);
            Assert.AreEqual(5, Kage.Rules.JsTables.Move("p1").Dmg, 1e-9, "the JS jab");
            Assert.AreEqual(0.98, Kage.Rules.JsTables.AiRole("sub").Windup, 1e-9);
            Assert.AreEqual(1, Kage.Rules.JsTables.Difficulty("normal").Windup, 1e-9);

            var style = Kage.Rules.JsTables.FightStyle(Id);
            Assert.IsNotNull(style);
            Assert.AreEqual("Karate · Sumō", style.Art);
            var clinch = style.Strings.Single(s => s.Seq.SequenceEqual(new[] { "grab" }));
            CollectionAssert.AreEqual(new[] { 0.0, 115.0 }, clinch.Range);
            Assert.IsTrue(clinch.When, "only on a guarding Kage");
        }

        [Test]
        public void His_abilities_have_the_JS_cooldown_range_and_chance()
        {
            var sf = Ability("stormFist");
            Assert.AreEqual(336, sf.cooldownFrames, "5.6 s");
            Assert.AreEqual(0f, sf.range.x, 1e-4);
            Assert.AreEqual(240f / 115f, sf.range.y, 1e-4);
            Assert.AreEqual(0.6f, sf.chance, 1e-6);

            var ts = Ability("garanTsuppari");
            Assert.AreEqual(360, ts.cooldownFrames, "6 s");
            Assert.AreEqual(230f / 115f, ts.range.y, 1e-4);
            Assert.AreEqual(0.55f, ts.chance, 1e-6);

            var ch = Ability("charge");
            Assert.AreEqual(390, ch.cooldownFrames, "6.5 s");
            Assert.AreEqual(200f / 115f, ch.range.x, 1e-4);
            Assert.AreEqual(1200f / 115f, ch.range.y, 1e-4);
            Assert.AreEqual(0.6f, ch.chance, 1e-6);

            var cl = Ability("garan.clinch");
            Assert.AreEqual(0f, cl.range.x, 1e-4);
            Assert.AreEqual(1f, cl.range.y, 1e-4, "the style's clinch string: 0 to 115 px");
            Assert.AreEqual(0.55f, cl.chance, 1e-6, "41-styles/garan.js decide(): chance(0.55)");
            Assert.AreEqual(0, cl.cooldownFrames, "the JS clinch has no cooldown");
            Assert.IsNotNull(cl.when, "he clinches only a guard");
        }

        // ---------------------------------------------------------------- the moves

        [Test]
        public void Storm_Fist_winds_up_overhead_armoured_then_slams_and_leaves_a_shock_area_hazard()
        {
            var m = Ability("stormFist").move;
            Assert.IsNotNull(m);
            var slam = m.hits.Single();
            Assert.AreEqual(46, slam.from + 1, "the wind-up: 0.78 x Normal 1 x the sub-boss role's 0.98 = 0.764 s, the fists land on frame 46");
            Assert.IsTrue(slam.knockdown && slam.heavy, "a crushing slam (JS kd)");
            Assert.AreNotEqual(HitLevel.Unblockable, slam.level, "it can be guarded (JS: no unb)...");
            Assert.Greater(slam.postureMul, 5f, "...but it leans on a guard like nothing else (JS gb 110 on a 12.35 blow)");
            Assert.IsTrue(m.Armoured(1) && m.Armoured(45), "armoured through the wind-up (JS superArmor)");
            Assert.IsFalse(m.Armoured(60), "the long recovery is open (the JS tip)");

            Assert.IsNotNull(m.hazard, "the shock area is a hazard (ADR-0007)");
            Assert.AreEqual("garan.stormFist.shock", m.hazard.id);
            Assert.AreEqual(46, m.hazardFrame + m.hazard.telegraphFrames + 1, "the ground mark shows through the wind-up and the shock strikes with the slam");
            Assert.AreEqual(18, m.hazard.strikeFrames, "the JS shockwave lives 0.75 x 0.4 s");
            Assert.AreEqual((228f + 68f) / 115f, m.hazard.size.x, 1e-3, "as long as the shockwave runs (760 px/s x 0.3 s) plus its width");
            Assert.AreEqual(68f / 115f, m.hazard.size.y, 1e-3, "a ground wave: jump it");
            Assert.AreEqual((120f * 1.18f + 114f) / 115f, m.hazardAhead, 1e-3, "from where the fists land, forward");
            Assert.AreNotEqual(HitLevel.Unblockable, m.hazard.hit.level, "the JS shock is blockable");
            Assert.IsTrue(m.hazard.hit.knockdown);
        }

        [Test]
        public void Tsuppari_is_a_crouch_then_four_walking_palms_that_lean_on_a_guard()
        {
            var m = Ability("garanTsuppari").move;
            Assert.IsNotNull(m);
            Assert.AreEqual(4, m.hits.Count, "four palms");
            Assert.AreEqual(29, m.hits[0].from, "the shikiri crouch: 0.5 x the role's 0.98 wind-up = 0.49 s");
            for (int i = 1; i < 4; i++) Assert.AreEqual(10f, m.hits[i].from - m.hits[i - 1].from, 1f, "a palm every 0.17 s");
            Assert.IsTrue(m.hits.All(h => h.level != HitLevel.Unblockable && h.postureMul > 1.5f), "blockable, and heavy on the guard (JS gb 24)");
            Assert.Greater(m.hits[3].dmg, m.hits[0].dmg, "the last palm is the hard one (JS 10 against 6)");
            Assert.IsTrue(m.motions.Any(e => e.vel.x > 1f), "he walks forward through them");
            Assert.IsTrue(m.Armoured(m.hits[0].from) && m.Armoured(m.hits[3].to), "an armoured string");
        }

        [Test]
        public void The_charge_is_telegraphed_then_runs_armoured_until_it_hits_or_meets_the_wall()
        {
            var m = Ability("charge").move;
            Assert.IsNotNull(m);
            var hit = m.hits.Single();
            Assert.AreEqual(36, hit.from, "the JS 0.6 s tell");
            Assert.AreEqual(192, m.total, "the JS 3.2 s");
            Assert.IsTrue(m.motions.Any(e => e.from >= 36 && e.vel.x >= 1050f / 115f - 1e-3f), "the run: 1050 px/s");
            Assert.IsTrue(hit.knockdown && hit.heavy);
            Assert.IsTrue(m.Armoured(40) && m.Armoured(150));
            Assert.IsNotNull(m.onContact, "on contact he skids to a stop");
            Assert.AreEqual(36, m.onContact.total, "the JS 0.6 s");
            Assert.AreEqual(102, m.wallStagger, "into the wall: staggered the JS 1.7 s");
        }

        [Test]
        public void The_clinch_is_an_armoured_grab_window_a_carry_and_a_throw()
        {
            var m = Ability("garan.clinch").move;
            Assert.IsNotNull(m);
            Assert.IsTrue(m.isThrow);
            Assert.AreEqual(4, m.grabFrame, "the JS grab's 0.06 s wind-up");
            Assert.AreEqual(8, m.grabTo, "...and its 0.08 s reach: he takes whoever he touches in it");
            Assert.AreEqual(m.grabTo + 30, m.releaseFrame, "the JS 0.5 s carry");
            Assert.AreEqual(1f, m.grabRange, 1e-4, "the clinch's 115 px");
            Assert.Greater(m.throwDmg, 0);
            Assert.IsTrue(Enumerable.Range(0, m.releaseFrame + 1).All(m.Armoured), "armoured until the release");
            Assert.IsNotNull(m.whiff);
        }

        // ---------------------------------------------------------------- look and arena

        [Test]
        public void His_arena_is_the_districts()
        {
            Assert.AreEqual("districts", BossArena.EnvFor(Id), "act1.sub2");
        }

        [Test]
        public void His_part_set_draws_him_on_Kages_skeleton_with_his_iron_gauntlets_in_his_own_palette()
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
            Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith("HandN_Tekko") && p.bone == B.HandN), "the near iron gauntlet");
            Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith("HandF_Tekko") && p.bone == B.HandF), "the far iron gauntlet");
            Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith("Menpo")), "the red oni menpo");
            Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith("ArmN_Sode")), "one huge iron sode on the near arm");
            var kuroe = CharacterCatalog.PartSet(Kuroe.Id).Build(CharacterCatalog.Get(Kuroe.Id));
            var footman = CharacterCatalog.PartSet("order_footman").Build(CharacterCatalog.Get("order_footman"));
            Assert.AreNotEqual(kuroe.rimColor, rig.rimColor, "not Kuroe's palette");
            Assert.AreNotEqual(footman.rimColor, rig.rimColor, "not the Order's palette");
        }
    }
}
