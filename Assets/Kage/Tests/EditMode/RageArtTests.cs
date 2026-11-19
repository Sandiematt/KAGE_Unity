using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>Rage art data (SAN-82): which art each family has, and Crimson Moon's tuning file.</summary>
    public class RageArtTests
    {
        [Test]
        public void The_katana_family_has_Crimson_Moon_and_every_other_family_Thousand_Shadows()
        {
            foreach (var id in new[] { "katana", "fists", "twin", "spear", "chain", "cleaver", "tachi" })
            {
                var fam = WeaponFamilies.Get(id);
                Assert.IsNotNull(fam, id);
                Assert.AreEqual(id == "katana" ? Fighter.CrimsonMoon : Fighter.ThousandShadows, fam.rageArt, id);
            }
        }

        [Test]
        public void Every_playable_weapon_gets_its_art_through_its_family()
        {
            foreach (var weapon in WeaponFamilies.PlayableWeapons())
            {
                var fam = WeaponFamilies.Get(WeaponFamilies.FamilyIdOf(weapon));
                Assert.IsNotNull(fam, weapon);
                Assert.AreEqual(fam.id == "katana" ? Fighter.CrimsonMoon : Fighter.ThousandShadows, fam.rageArt, weapon);
            }
            foreach (var katana in new[] { "kagetsu", "shizuka" })
                Assert.AreEqual("katana", WeaponFamilies.FamilyIdOf(katana), katana + " fights with the katana family, so it has Crimson Moon");
        }

        [Test]
        public void The_shipped_Crimson_Moon_tuning_is_the_JS_arts_numbers_at_60_frames_a_second()
        {
            var file = Tuning.Get<CrimsonMoonTuning>();
            var code = new CrimsonMoonTuning();
            Assert.AreEqual(JsonUtility.ToJson(code), JsonUtility.ToJson(file), "crimsonMoon.json matches the CrimsonMoonTuning defaults");
            // JS 67-rage-arts.js: freeze 0.55 s, cuts at 0.55, 0.74, 0.93 s for 7, step back 1.12 s, finisher 1.38 s for 15, 1.85 s in all
            Assert.AreEqual(33, file.anticipationFrames);
            CollectionAssert.AreEqual(new[] { 33, 44, 56 }, file.cutFrames);
            Assert.AreEqual(67, file.stepBackFrame);
            Assert.AreEqual(83, file.finisherFrame);
            Assert.AreEqual(111, file.totalFrames);
            Assert.AreEqual(7, file.cutDamage);
            Assert.AreEqual(15, file.finisherDamage);
            Assert.AreEqual(78, file.bindFrames, "1.3 s");
            Assert.AreEqual(7, file.finisherHitstop, "0.12 s");
            Assert.AreEqual(0.3f, file.finisherSlowScale);
            Assert.AreEqual(0.45f, file.finisherSlowSeconds);
            Assert.AreEqual(0.8f, file.standDistance, 0.01f, "92 px at 115 px a metre");
            Assert.AreEqual(3, file.cutAngles.Length);
            Assert.IsTrue(file.cutFrames.Zip(file.cutFrames.Skip(1), (a, b) => b > a).All(x => x), "cuts in order");
            Assert.Less(file.cutFrames.Last(), file.stepBackFrame);
            Assert.Less(file.stepBackFrame, file.finisherFrame);
            Assert.Less(file.finisherFrame, file.totalFrames);
        }
    }
}
