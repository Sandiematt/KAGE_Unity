using NUnit.Framework;

namespace Kage.Tests
{
    // The weapon family contract (ADR-0006): which family a JS weapon fights with, the combat multipliers of a weapon
    // at a level measured against its family's reference weapon, and the one damage formula. Expected values are
    // worked out from the JS weapon table ([damage, speed, reach] at level 1) and the JS growth rule.
    public class WeaponFamilyTests
    {
        // ---- which family a weapon belongs to ----

        [TestCase("kagetsu", "katana")]
        [TestCase("shizuka", "katana")]
        [TestCase("mugen_no_ha", "katana")]
        [TestCase("cutter_blade", "katana")]
        [TestCase("fists", "fists")]
        [TestCase("iron_fists", "fists")]
        [TestCase("tsubame", "twin")]
        [TestCase("mist_fans", "twin")]
        [TestCase("mizuchi", "spear")]
        [TestCase("temple_staff", "spear")]
        [TestCase("sentinel_spear", "spear")]
        [TestCase("yamigama", "chain")]
        [TestCase("chain_hook", "chain")]
        [TestCase("onikiri", "cleaver")]
        [TestCase("raider_cleaver", "cleaver")]
        [TestCase("iwakudaki", "cleaver")]
        [TestCase("raiko_no_tachi", "tachi")]
        [TestCase("raikon_tachi", "tachi")]
        public void A_weapon_fights_with_the_family_of_its_JS_base(string weapon, string family)
        {
            Assert.AreEqual(family, WeaponFamilies.FamilyIdOf(weapon));
        }

        [Test]
        public void Katana_and_fists_are_registered_with_their_reference_weapon_grip_blade_and_rage_art()
        {
            var katana = WeaponFamilies.Get("katana");
            Assert.AreEqual("kagetsu", katana.referenceWeapon);
            Assert.AreEqual(GripPose.TwoHand, katana.grip);
            Assert.Greater(katana.bladeLength, 0.6f);
            Assert.AreEqual("crimsonMoon", katana.rageArt);

            var fists = WeaponFamilies.Get("fists");
            Assert.AreEqual("fists", fists.referenceWeapon);
            Assert.AreEqual(GripPose.Fists, fists.grip);
            Assert.AreEqual(0f, fists.bladeLength);
            Assert.AreEqual(Fighter.ThousandShadows, fists.rageArt);
        }

        [Test]
        public void Twin_blades_are_registered_with_Tsubame_as_reference_a_dual_grip_and_short_blades()
        {
            var twin = WeaponFamilies.Get("twin");
            Assert.AreEqual("tsubame", twin.referenceWeapon);
            Assert.AreEqual(GripPose.Dual, twin.grip);
            Assert.Greater(twin.bladeLength, 0f);
            Assert.Less(twin.bladeLength, WeaponFamilies.Get("katana").bladeLength);
            Assert.AreEqual(Fighter.ThousandShadows, twin.rageArt);
            Assert.IsNotNull(twin.visual);
        }

        [Test]
        public void Tsubame_at_level_1_is_x1_and_level_8_gains_the_JS_amounts()
        {
            AssertCombat("tsubame", 1, 1, 1, 1);
            AssertCombat("tsubame", 8, 1.56, 1.175, 1.105);
        }

        [Test]
        public void Foe_twin_weapons_are_measured_against_Tsubame()
        {
            AssertCombat("shadow_daggers", 1, 1, 1, 1);   // the same JS stats as Tsubame
        }

        [Test]
        public void The_spear_family_is_registered_with_Mizuchi_a_polearm_grip_and_the_longest_blade()
        {
            var spear = WeaponFamilies.Get("spear");
            Assert.IsNotNull(spear);
            Assert.AreEqual("mizuchi", spear.referenceWeapon);
            Assert.AreEqual(GripPose.Polearm, spear.grip);
            Assert.AreEqual(Fighter.ThousandShadows, spear.rageArt);
            Assert.IsNotNull(spear.visual);
            Assert.Greater(spear.bladeLength, 1.5f * WeaponFamilies.Get("katana").bladeLength, "the spear's line is far longer than the katana's");
        }

        [Test]
        public void The_cleaver_family_is_registered_with_Onikiri_a_heavy_two_handed_grip_and_a_blade_shorter_than_the_spears()
        {
            var cleaver = WeaponFamilies.Get("cleaver");
            Assert.IsNotNull(cleaver);
            Assert.AreEqual("onikiri", cleaver.referenceWeapon);
            Assert.AreEqual(GripPose.HeavyTwoHand, cleaver.grip);
            Assert.AreEqual(Fighter.ThousandShadows, cleaver.rageArt);
            Assert.IsNotNull(cleaver.visual);
            Assert.Greater(cleaver.bladeLength, WeaponFamilies.Get("katana").bladeLength, "a cleaver is longer than the katana's blade");
            Assert.Less(cleaver.bladeLength, WeaponFamilies.Get("spear").bladeLength, "the spear keeps the longest line");
        }

        [Test]
        public void The_heavy_grip_holds_the_hands_further_apart_than_the_katana_and_closer_than_the_polearm()
        {
            Assert.Less(Pose.HeavySpacing, Pose.GripSpacing);
            Assert.Greater(Pose.HeavySpacing, Pose.PolearmSpacing);
            Assert.Less(Pose.HeavyPommel, Pose.HeavySpacing, "it slides back towards the pommel");
        }

        [Test]
        public void The_polearm_holds_its_hands_further_apart_than_the_katana()
        {
            Assert.Less(Pose.PolearmSpacing, 3f * Pose.GripSpacing, "a forearm or more behind the lead hand, against a fist's width");
            Assert.Less(Pose.PolearmPommel, Pose.PolearmSpacing, "it slides back towards the butt");
            var p = new Pose();
            Assert.AreEqual(Pose.GripSpacing, p.gripSpacing, "a pose grips like the katana unless the fighter says otherwise");
        }

        [Test]
        public void A_family_that_is_not_built_yet_is_not_registered()
        {
            Assert.IsNull(WeaponFamilies.Get("tachi"));
        }

        [Test]
        public void The_dev_weapon_cycle_offers_the_player_weapons_of_the_registered_families()
        {
            CollectionAssert.AreEqual(new[] { "fists", "tsubame", "kagetsu", "onikiri", "mizuchi", "yamigama", "iwakudaki", "shizuka", "shinsen", "kagehira_yurei", "mugen_no_ha" }, WeaponFamilies.PlayableWeapons());
        }

        // ---- combat multipliers (WeaponStats.Combat) ----

        static void AssertCombat(string id, int level, double dmg, double speed, double reach)
        {
            var c = WeaponStats.Combat(id, level);
            Assert.AreEqual(dmg, c.dmg, 1e-5, id + " damage at level " + level);
            Assert.AreEqual(speed, c.speed, 1e-5, id + " speed at level " + level);
            Assert.AreEqual(reach, c.reach, 1e-5, id + " reach at level " + level);
        }

        [Test]
        public void Kagetsu_at_level_1_is_exactly_x1_on_everything()
        {
            var c = WeaponStats.Combat("kagetsu", 1);
            Assert.AreEqual(1f, c.dmg);
            Assert.AreEqual(1f, c.speed);
            Assert.AreEqual(1f, c.reach);
        }

        [Test]
        public void Kagetsu_at_level_8_gains_the_JS_amounts()
        {
            AssertCombat("kagetsu", 8, 1.56, 1.175, 1.105);
        }

        [Test]
        public void Shizuka_is_measured_against_Kagetsu()
        {
            AssertCombat("shizuka", 1, 64.0 / 60, 68.0 / 64, 62.0 / 58);
        }

        [Test]
        public void Mizuchi_at_level_1_is_x1_and_gains_the_JS_amounts_per_level()
        {
            AssertCombat("mizuchi", 1, 1, 1, 1);
            AssertCombat("mizuchi", 8, 1.56, 1.175, 1.105);
            AssertCombat("mizuchi", 3, 1.16, 1.05, 1.03);
        }

        [Test]
        public void Onikiri_at_level_1_is_x1_and_gains_the_JS_amounts_per_level()
        {
            AssertCombat("onikiri", 1, 1, 1, 1);
            AssertCombat("onikiri", 8, 1.56, 1.175, 1.105);
            AssertCombat("onikiri", 3, 1.16, 1.05, 1.03);
        }

        [TestCase("raider_cleaver")]
        [TestCase("tyrant_cleaver")]
        public void The_cleaver_foes_weapons_are_measured_against_Onikiri(string weapon)
        {
            AssertCombat(weapon, 1, 1, 1, 1);     // JS stats [82, 38, 54], the same as Onikiri
        }

        [TestCase("sentinel_spear")]
        [TestCase("temple_staff")]
        [TestCase("vanguard_yari")]
        public void The_spear_foes_weapons_are_measured_against_Mizuchi(string weapon)
        {
            AssertCombat(weapon, 1, 1, 1, 1);     // JS stats [52, 60, 96], the same as Mizuchi
        }

        [Test]
        public void The_chain_sickle_family_is_registered_and_its_weapons_are_measured_against_Yamigama()
        {
            var fam = WeaponFamilies.Get("chain");
            Assert.IsNotNull(fam);
            Assert.AreEqual("yamigama", fam.referenceWeapon);
            Assert.AreEqual(GripPose.OneHand, fam.grip);
            Assert.AreEqual(Fighter.ThousandShadows, fam.rageArt);
            Assert.IsInstanceOf<IWeaponVisual>(fam.visual);
            AssertCombat("yamigama", 1, 1, 1, 1);
            AssertCombat("yamigama", 8, 1.56, 1.175, 1.105);
            AssertCombat("chain_hook", 1, 1, 1, 1);      // JS base Yamigama: the Chainthrower's weapon is x1 against it
        }

        [Test]
        public void Speed_and_reach_are_capped()
        {
            AssertCombat("shinsen", 1, 150.0 / 60, 1.25, 1.2);
        }

        [Test]
        public void Raiko_no_Tachi_is_measured_against_itself()
        {
            AssertCombat("raiko_no_tachi", 1, 1, 1, 1);
        }

        [Test]
        public void Raikons_own_tachi_keeps_its_JS_multiplier()
        {
            AssertCombat("raikon_tachi", 1, 64.0 / 82, 40.0 / 38, 60.0 / 54);
        }

        // ---- the damage formula (DamageCalc) ----

        [TestCase(9, 1f, 1f, false, 0f, 9)]
        [TestCase(9, 1f, 1f, true, 0f, 14)]       // today's crit: RoundToInt(13.5)
        [TestCase(5, 1f, 1f, true, 0f, 8)]        // RoundToInt(7.5)
        [TestCase(9, 1f, 1.56f, false, 0f, 14)]   // Kagetsu level 8: 14.04
        [TestCase(12, 0.9f, 1f, false, 0f, 11)]   // a Footman's damage multiplier: 10.8
        [TestCase(10, 1f, 1f, false, 0.14f, 9)]   // an Ironclad's defence: 8.6
        [TestCase(12, 1f, 1f, true, 0.03f, 17)]   // Kage's starter defence on a crit: 17.46
        [TestCase(1, 1f, 1f, false, 0.5f, 1)]     // never below 1
        public void Damage_is_window_times_character_times_weapon_times_crit_less_defence_rounded_once(
            int window, float damageMul, float weaponDmg, bool crit, float defense, int expected)
        {
            Assert.AreEqual(expected, DamageCalc.Damage(window, damageMul, weaponDmg, crit, defense));
        }
    }
}
