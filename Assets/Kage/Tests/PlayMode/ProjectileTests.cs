using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The generic projectile (SAN-75, ADR-0007): it moves on the fight loop, casts against hurtboxes,
    /// goes through the same defence order as a melee hit, never hits its owner or its owner's team,
    /// and publishes Hit / Blocked with its own id. Each test disposes its fight before the next.
    /// </summary>
    public class ProjectileTests
    {
        readonly List<HeadlessFight> _fights = new();
        readonly List<GameObject> _roots = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            foreach (var r in _roots) if (r != null) Object.DestroyImmediate(r);
            _roots.Clear();
        }

        /// <summary>Kage on the left, the Ronin standing still on the right (or holding Guard when <paramref name="guard"/>).</summary>
        HeadlessFight Duel(bool guard = false)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, HeadlessFight.KageX, 0.6f);
            f.RoninInput.Add(600, buttons: guard ? "G" : "");
            _fights.Add(f);
            return f;
        }

        HeadlessFight Alone()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            _fights.Add(f);
            return f;
        }

        static ProjectileDef Bolt(float speed = 6f, int life = 120, int maxHits = 1, HitLevel level = HitLevel.Mid) => new()
        {
            id = "testBolt",
            hit = new HitWindow { dmg = 7, level = level, kb = new Vector2(2f, 0.5f), stun = 18, hitstop = 4, shake = 0.1f },
            speed = speed, radius = 0.2f, lifeFrames = life, maxHits = maxHits,
        };

        static Projectile Fire(HeadlessFight f, ProjectileDef def, Vector2 pos, float dir = 1f) => Projectile.Spawn(f.Ctx, f.Kage, def, pos, dir);

        // ---------------------------------------------------------------------------------------
        [Test]
        public void AProjectileHitsTheFirstFighterInItsPathAndIsSpent()
        {
            var f = Duel();
            var p = Fire(f, Bolt(), new Vector2(-1.5f, 1f));
            Assert.IsTrue(p.Alive);
            Assert.AreEqual(1, f.Ctx.Projectiles.Count);

            f.Run(40);

            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreSame(f.Kage, hit.Source);
            Assert.AreSame(f.Ronin, hit.Target);
            Assert.AreEqual("testBolt", hit.MoveId);
            Assert.AreEqual(-1, hit.Window, "a projectile is not a window of a move");
            Assert.That(hit.Frame, Is.InRange(8, 20), "about 1.6 m at 6 m/s is 10 to 14 frames");
            Assert.AreEqual(93f, f.Ronin.health, "the projectile's damage");
            Assert.IsFalse(p.Alive);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
        }

        [Test]
        public void AProjectileMovesAtItsSpeedOnTheFightLoopAndExpiresAfterItsLife()
        {
            var f = Alone();
            var p = Fire(f, Bolt(speed: 6f, life: 30), new Vector2(-2f, 1f));

            f.Run(10);
            Assert.AreEqual(-2f + 6f / 60f * 10, p.Position.x, 1e-3f);
            Assert.AreEqual(1f, p.Position.y, 1e-4f, "no gravity: a straight line");
            Assert.AreEqual(p.Position.x, p.transform.position.x, 1e-3f);

            f.Run(19);
            Assert.IsTrue(p.Alive, "life frame 29 of 30");
            f.Run(1);
            Assert.IsFalse(p.Alive);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(), "it hit nothing");
        }

        [Test]
        public void AProjectileFacingLeftFliesLeft()
        {
            var f = Alone();
            var p = Fire(f, Bolt(), new Vector2(2f, 1f), dir: -1f);
            f.Run(6);
            Assert.AreEqual(2f - 6f / 60f * 6, p.Position.x, 1e-3f);
        }

        [Test]
        public void AProjectileNeverHitsItsOwnerEvenWhenItStartsInsideHim()
        {
            var f = Duel();
            Vector2 inside = f.Kage.Pos + Vector2.up * 1f;
            Fire(f, Bolt(), inside);

            f.Run(40);

            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count());
            Assert.IsFalse(f.Events(CombatEventType.Hit).Any(e => e.Target == f.Kage));
            Assert.AreEqual(100f, f.Kage.health);
            Assert.AreEqual(93f, f.Ronin.health);
        }

        [Test]
        public void AProjectileNeverHitsAFighterOnItsOwnersTeam()
        {
            var root = new GameObject("TeamFight");
            _roots.Add(root);
            var b = new FightBuilder(root.transform, KageMoveScenarios.Seed, manualClock: true);
            b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new ScriptedInput());
            var friend = b.Spawn(CharacterCatalog.Get(Ronin.Id), 0, new ScriptedInput());
            var foe = b.Spawn(CharacterCatalog.Get(Ronin.Id), 1, new ScriptedInput());
            var ctx = b.Build();
            using var log = new CombatEventLog(ctx.Events);
            ctx.Begin();
            kage.ResetForRound(-1.5f, 1f); kage.BeginFight();
            friend.ResetForRound(-0.4f, -1f); friend.BeginFight();
            foe.ResetForRound(0.8f, -1f); foe.BeginFight();

            Projectile.Spawn(ctx, kage, Bolt(), new Vector2(-1.2f, 1f), 1f);
            for (int i = 0; i < 40; i++) b.Loop.StepHeadless();

            var hits = log.All.Where(e => e.Type == CombatEventType.Hit).ToList();
            Assert.AreEqual(1, hits.Count);
            Assert.AreSame(foe, hits[0].Target, "it flew through the friend");
            Assert.AreEqual(100f, friend.health);
        }

        [Test]
        public void AGuardedProjectileIsBlockedAndStillSpent()
        {
            var f = Duel(guard: true);
            var p = Fire(f, Bolt(), new Vector2(-1.5f, 1f));

            f.Run(40);

            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.AreEqual("testBolt", blocked.MoveId);
            Assert.AreSame(f.Ronin, blocked.Target);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count());
            Assert.AreEqual(100f, f.Ronin.health, "a non-sword hit does no chip damage");
            Assert.IsFalse(p.Alive);
        }

        [Test]
        public void AnUnblockableProjectileGoesThroughAGuard()
        {
            var f = Duel(guard: true);
            Fire(f, Bolt(level: HitLevel.Unblockable), new Vector2(-1.5f, 1f));
            f.Run(40);
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count());
            Assert.AreEqual(0, f.Events(CombatEventType.Blocked).Count());
            Assert.AreEqual(93f, f.Ronin.health);
        }

        [Test]
        public void AProjectileThatCanHitTwoTargetsHitsEachOnceAndOneThatCannotStopsAtTheFirst()
        {
            var f = Duel();
            var behind = HurtProbe.Place(f.Root.transform, new Vector2(2.4f, 1f), 0.3f);
            Fire(f, Bolt(speed: 9f, maxHits: 2), new Vector2(-1.5f, 1f));
            f.Run(60);
            Assert.AreEqual(93f, f.Ronin.health);
            Assert.AreEqual(1, behind.Hits.Count, "the second target");
            Assert.AreEqual("testBolt", behind.Hits[0].moveId);

            var f2 = Duel();
            var behind2 = HurtProbe.Place(f2.Root.transform, new Vector2(2.4f, 1f), 0.3f);
            Fire(f2, Bolt(speed: 9f, maxHits: 1), new Vector2(-1.5f, 1f));
            f2.Run(60);
            Assert.AreEqual(0, behind2.Hits.Count, "spent on the first");
        }

        [Test]
        public void AProjectileHitsTheSameTargetOnlyOnceWhileItOverlapsIt()
        {
            var f = Alone();
            var probe = HurtProbe.Place(f.Root.transform, new Vector2(0f, 1f), 0.3f);
            Fire(f, Bolt(speed: 0.6f, maxHits: 5, life: 200), new Vector2(-1f, 1f));
            f.Run(150);
            Assert.AreEqual(1, probe.Hits.Count, "it keeps its own hit log");
        }

        [Test]
        public void AProjectileWithGravityIsSpentWhenItLandsAndAGroundWaveSlidesAlongTheFloor()
        {
            var f = Alone();
            var arc = ProjectileDefWith(Bolt(speed: 2f), gravity: 1f);
            var wave = ProjectileDefWith(Bolt(speed: 2f), ground: true);
            var a = Fire(f, arc, new Vector2(-2f, 1f));
            var w = Fire(f, wave, new Vector2(-2f, 0.2f));

            f.Run(10);
            Assert.Less(a.Position.y, 1f, "it falls");
            Assert.AreEqual(0.2f, w.Position.y, 1e-4f, "a ground wave keeps its height");

            f.Run(50);
            Assert.IsFalse(a.Alive, "it hit the ground");
            Assert.IsTrue(w.Alive);
            Assert.AreEqual(0.2f, w.Position.y, 1e-4f);
        }

        [Test]
        public void AProjectileIsABodyWithATriggerOnTheProjectileLayer()
        {
            var f = Alone();
            var p = Fire(f, Bolt(), new Vector2(0f, 1f));
            Assert.AreEqual(PhysicsLayers.Projectile, p.gameObject.layer);
            var col = p.GetComponent<CircleCollider2D>();
            Assert.IsTrue(col.isTrigger);
            Assert.AreEqual(0.2f, col.radius, 1e-5f);
            Assert.AreEqual(RigidbodyType2D.Kinematic, p.GetComponent<Rigidbody2D>().bodyType);
            Assert.AreSame(f.Kage, p.Owner);
        }

        static ProjectileDef ProjectileDefWith(ProjectileDef d, float gravity = 0f, bool ground = false)
        {
            d.gravityScale = gravity;
            d.groundWave = ground;
            return d;
        }
    }
}
