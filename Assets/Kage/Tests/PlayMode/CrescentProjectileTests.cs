using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Crescent is the generic projectile with its old numbers (SAN-75): the baseline replay checks
    /// the whole fight; these name the numbers and the block behaviour.
    /// </summary>
    public class CrescentProjectileTests
    {
        [Test]
        public void TheCrescentKeepsItsDamageSpeedLifeAndRadius()
        {
            var d = Fighter.CrescentProjectile;
            Assert.AreEqual("crescent", d.id);
            Assert.AreEqual(10, d.hit.dmg);
            Assert.AreEqual(HitLevel.Mid, d.hit.level);
            Assert.AreEqual(new Vector2(2.6f, 1.2f), d.hit.kb);
            Assert.AreEqual(22, d.hit.stun);
            Assert.AreEqual(7, d.hit.hitstop);
            Assert.AreEqual(8.2f, d.speed);
            Assert.AreEqual(69, d.lifeFrames, "1.15 s at 60 Hz");
            Assert.AreEqual(0.32f, d.radius);
            Assert.AreEqual(1, d.maxHits);
            Assert.AreEqual(0f, d.gravityScale);
        }

        [Test]
        public void KageCastsTheCrescentAsAProjectileThatFliesStraightAndExpires()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageMeter: 35f);
            f.KageInput.Add(1, buttons: "X");
            f.RunUntil(() => f.Ctx.Projectiles.Count > 0, 40);

            Assert.AreEqual(1, f.Ctx.Projectiles.Count, "the move spawns it on its projectile frame");
            var p = f.Ctx.Projectiles[0];
            Assert.AreSame(f.Kage, p.Owner);
            Assert.AreEqual(1.05f, p.Position.y, 1e-4f);
            float x0 = p.Position.x;
            f.Run(1);
            Assert.AreEqual(x0 + 8.2f / 60f, p.Position.x, 1e-3f);
            f.Run(100);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count());
        }

        [Test]
        public void ARoninHoldingBackBlocksTheCrescentAndTakesOnlyChipDamage()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -1.2f, 0.9f, kageMeter: 35f);
            f.RoninInput.Add(300, buttons: "G");
            f.KageInput.Add(1, buttons: "X");
            f.Run(60);

            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.AreEqual("crescent", blocked.MoveId);
            Assert.AreEqual(98f, f.Ronin.health, "a blocked sword hit chips 15% of its damage, rounded up (as before)");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count());
        }

        [Test]
        public void TheCrescentHitsAFighterForTenDamage()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -1.2f, 0.9f, kageMeter: 35f);
            f.RoninInput.Add(300);
            f.KageInput.Add(1, buttons: "X");
            f.Run(60);

            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreEqual("crescent", hit.MoveId);
            Assert.AreEqual(10f, hit.Damage);
            Assert.AreEqual(90f, f.Ronin.health);
        }
    }
}
