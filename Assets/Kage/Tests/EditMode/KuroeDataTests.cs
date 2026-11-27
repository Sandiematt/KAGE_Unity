using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Kuroe, the Ink Censor (SAN-103), the first Act I sub-boss, as data: her character entry (JS stats, the spear/staff family, the
    /// defensive profile, her one ability), her boss data (the two JS phases) and Censure's ability entry (JS cooldown, range, chance).
    /// The expected numbers are the JS game's (10-data.js: ENEMIES.kuroe, RAIKON_VARIANTS.censure). How she fights is in KuroeFightTests.
    /// </summary>
    public class KuroeDataTests
    {
        const string Id = "kuroe";

        [Test]
        public void Kuroe_is_a_catalog_entry_with_her_JS_stats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'kuroe'");
            Assert.AreEqual("Kuroe", d.displayName);
            Assert.AreEqual("the Ink Censor", d.title);
            Assert.AreEqual(360f, d.jsHealth);
            Assert.AreEqual(360f * 1.15f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.08f, d.defense, 1e-6);
            Assert.AreEqual(1.15f, d.damageMul, 1e-6, "41-styles/kuroe.js raises her 10-data.js dmg 1 to 1.15");
            Assert.AreEqual(1f, d.speedMul, 1e-6);
            Assert.AreEqual(1.21f, d.bodyScale, 1e-6);
            Assert.AreEqual(CharacterFlags.None, d.flags, "no heavy armour, no no-grab: the JS tip says throw him");
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void She_fights_with_the_temple_staff_on_the_spear_set_and_the_defensive_profile()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("temple_staff", d.weaponId);
            Assert.AreEqual(WeaponFamilies.Spear, d.family);
            Assert.AreEqual(WeaponFamilies.Spear, WeaponFamilies.FamilyIdOf(d.weaponId), "the JS weapon's family is the spear/staff set");
            Assert.AreEqual("defensive", d.aiProfile);
        }

        [Test]
        public void She_is_a_boss_with_Censure_first_then_her_calligraphy_and_Raikon_kit()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(Id, d.bossId);
            Assert.AreEqual("censure", d.abilities[0], "the JS Censure stays in slot 1");
            CollectionAssert.AreEqual(KuroeAbilities.ExtraIds, d.abilities.Skip(1).ToArray());
        }

        [Test]
        public void Her_boss_data_keeps_the_two_JS_phases_and_adds_her_ink_and_Raikon_abilities()
        {
            var js = BossDef.FromJs(Id);
            CollectionAssert.AreEqual(new[] { "censure" }, js.phases[0].abilities);
            CollectionAssert.AreEqual(new[] { "censure" }, js.phases[1].abilities);

            var b = BossCatalog.Get(Id);
            Assert.IsNotNull(b);
            Assert.AreEqual("Kuroe", b.name);
            Assert.AreEqual("the Ink Censor", b.title);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual("The Black Line", b.phases[0].name);
            Assert.AreEqual(1f, b.phases[0].at);
            CollectionAssert.AreEquivalent(new[] { "censure", KuroeAbilities.SumiNidanId, KuroeAbilities.SumiBaraiId, KuroeAbilities.BokusekiWaveId }, b.phases[0].abilities);
            Assert.IsFalse(b.phases[0].aggressive);
            Assert.AreEqual("Redacted", b.phases[1].name);
            Assert.AreEqual(0.5f, b.phases[1].at);
            CollectionAssert.AreEquivalent(new[] { "censure", KuroeAbilities.SumiNidanId, KuroeAbilities.SumiBaraiId, KuroeAbilities.BokusekiWaveId, KuroeAbilities.ThunderRedactionId }, b.phases[1].abilities);
            Assert.IsTrue(b.phases[1].aggressive, "phase two: the JS aggressive flag");
            Assert.IsNull(b.phases[1].aiProfile, "she stays on the defensive profile");
        }

        [Test]
        public void Censure_has_the_JS_cooldown_range_and_chance()
        {
            CharacterCatalog.Get(Id);      // registers her abilities
            var a = AbilityCatalog.Get("censure");
            Assert.IsNotNull(a, "Censure is registered");
            Assert.AreEqual(420, a.cooldownFrames, "7 s");
            Assert.AreEqual(60f / 115f, a.range.x, 1e-4, "60 px");
            Assert.AreEqual(260f / 115f, a.range.y, 1e-4, "260 px");
            Assert.AreEqual(0.5f, a.chance, 1e-6);
        }

        [Test]
        public void The_generated_tables_carry_the_Censure_variant_numbers()
        {
            var c = Kage.Rules.JsTables.BossAbility("censure");
            Assert.IsNotNull(c);
            Assert.AreEqual("redlineCounter", c.Base);
            Assert.AreEqual(7.0, c.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 60.0, 260.0 }, c.Range);
            Assert.AreEqual(0.5, c.Chance, 1e-9);
            Assert.AreEqual(0.7, c.Stance, 1e-9, "the guard is held 0.7 s");
            Assert.AreEqual(0.14, c.Punish, 1e-9, "the punish comes 0.14 s after the blow");
            Assert.AreEqual(0.6, c.Dmg, 1e-9);
            Assert.AreEqual(false, c.Unb, "her punish can be blocked");
            Assert.IsNull(Kage.Rules.JsTables.BossAbility("nobody"));
        }

        [Test]
        public void Censure_is_a_counter_window_answered_by_a_blockable_strike()
        {
            CharacterCatalog.Get(Id);
            var m = AbilityCatalog.Get("censure").move;
            Assert.IsNotNull(m);
            Assert.IsEmpty(m.hits, "the stance itself strikes nothing");
            Assert.GreaterOrEqual(m.counterFrom, 0);
            Assert.AreEqual(42, m.counterTo - m.counterFrom + 1, "the counter window is the JS stance, 0.7 s");
            Assert.Greater(m.total, m.counterTo + 1, "after the stance she drops the guard: frames with no window");
            Assert.IsTrue(m.counterFrontOnly, "only a melee blow from the front is turned aside");
            var answer = m.counterAnswer;
            Assert.IsNotNull(answer, "the counter is answered by her own punish, not the katana's teleport");
            Assert.IsNotEmpty(answer.hits);
            Assert.AreEqual(8, answer.hits[0].from, "the punish strikes 0.14 s after the blow");
            Assert.IsTrue(answer.hits.All(h => h.level != HitLevel.Unblockable), "blockable (JS unb 0)");
            Assert.IsTrue(answer.hits.Any(h => h.knockdown), "it knocks down (JS kd)");
        }

        [Test]
        public void Her_arena_is_the_env_of_her_JS_stage_and_the_other_sub_bosses_get_theirs()
        {
            Assert.AreEqual("rooftops", BossArena.EnvFor(Id), "act1.sub1");
            Assert.AreEqual("districts", BossArena.EnvFor("garan"), "act1.sub2");
            Assert.AreEqual("districts", BossArena.EnvFor("shiden"), "act1.sub4");
            Assert.IsNull(BossArena.EnvFor("order_footman"), "a regular foe has no arena of its own");
            Assert.AreEqual("rooftops", BossArena.EnvFor("raikon"), "the boss stage (SAN-107) has no env of its own: Act I's");
            Assert.AreEqual("rooftops", EnvLook.For("rooftops").id);
            Assert.AreEqual("districts", EnvLook.For("districts").id);
            Assert.AreEqual("rooftops", EnvLook.For("hoshimura").id, "an env with no look of its own paints as the rooftops");
        }

        [Test]
        public void Her_part_set_draws_her_on_Kages_skeleton_in_her_own_palette()
        {
            var d = CharacterCatalog.Get(Id);
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
            var footman = CharacterCatalog.PartSet("order_footman").Build(CharacterCatalog.Get("order_footman"));
            Assert.AreNotEqual(footman.rimColor, rig.rimColor, "not the Order's palette");
            Assert.IsTrue(rig.parts.Any(p => p.name == "Ponytail" && p.bone == B.Head && p.soft == SoftKind.Flow), "the ponytail flows from the crown");
        }
    }
}
