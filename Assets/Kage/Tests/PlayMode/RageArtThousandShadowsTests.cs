using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Thousand Shadows for a weapon with no art of its own (SAN-82, ADR-0006; docs/weapon-styles.md): bare hands and the heavy tachi.
    /// Every shop weapon has its own art now (<c>WeaponRageArtTests</c>). Its own timeline (five cuts and a finisher, unblockable) is the
    /// fight-harness test <c>ThousandShadowsPublishesRageArtThenItsCuts</c>.
    /// </summary>
    public class RageArtThousandShadowsTests
    {
        readonly List<IDisposable> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Duel(string weapon, float meter = 100f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, meter, kageDef: SpearFamilyFightTests.KageWith(weapon));
            _fights.Add(f);
            f.RoninInput.Add(2400);
            return f;
        }

        // the weapons with no style of their own: their family's art
        [TestCase("fists", "fists")]
        [TestCase("raiko_no_tachi", "tachi")]
        public void A_full_meter_with_a_weapon_without_its_own_art_triggers_Thousand_Shadows(string weapon, string family)
        {
            var f = Duel(weapon);
            Assert.AreEqual(family, f.Kage.Family.id);
            f.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);
            f.Run(3);

            var rage = f.Events(CombatEventType.RageArt).Single();
            Assert.AreEqual(f.Kage.Family.rageArt, rage.MoveId, "the family's art");
            Assert.AreEqual(Fighter.ThousandShadows, rage.MoveId);
            Assert.AreEqual(0f, f.Kage.meter, "the meter is spent");
            Assert.AreEqual(Fighter.ThousandShadows, f.Kage.ActiveRageArt);

            f.Run(160);
            var cuts = f.Events(CombatEventType.Hit).Where(e => e.MoveId == Fighter.ThousandShadows).ToList();
            Assert.AreEqual(6, cuts.Count, "five cuts and the final blow");
            Assert.IsTrue(cuts.All(e => e.Level == HitLevel.Unblockable));
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.MoveId == Fighter.CrimsonMoon), "no Crimson Moon");
            var k = f.Kage;
            float expected = 5 * DamageCalc.Damage(4, k.DamageMul, k.Weapon.dmg, false, f.Ronin.Defense) + DamageCalc.Damage(14, k.DamageMul, k.Weapon.dmg, false, f.Ronin.Defense);
            Assert.AreEqual(expected, cuts.Sum(e => e.Damage), "4 a cut and 14 for the finisher, scaled by the weapon");
        }

        [Test]
        public void Without_a_full_meter_the_other_families_throw_a_kunai()
        {
            var f = Duel("mizuchi", meter: 99f);
            f.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);
            f.Run(30);
            Assert.AreEqual(0, f.Events(CombatEventType.RageArt).Count());
            Assert.AreEqual(4, f.Kage.KunaiCount);
        }
    }
}
