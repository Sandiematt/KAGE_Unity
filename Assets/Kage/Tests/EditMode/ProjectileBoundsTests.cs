using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// A projectile ends where the fight's bounds end (SAN-132), not at the dev arena's fixed |x| = 9.5 m: on a stretch road a
    /// kunai thrown well past x = 9.5 flies and hits a foe down the road, and it ends only once it is past the road's wall by
    /// <see cref="Projectile.BoundsMargin"/>. In the dev arena (bounds ±7.6 m) a projectile lives exactly as long and ends exactly
    /// where it did with the old fixed limit, so arena fights and the baseline replay are unchanged. Built with
    /// <see cref="FightBuilder"/> on a manual clock; the projectile is ticked directly, the fight not stepped.
    /// </summary>
    public class ProjectileBoundsTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        const int Cap = 1000;       // ticks: every loop here stops by then
        GameObject _root;
        StretchRoad _road;
        SimulationMode2D _simMode;

        [SetUp]
        public void SetUp() { _simMode = Physics2D.simulationMode; }

        [TearDown]
        public void TearDown()
        {
            _road?.Dispose();
            _road = null;
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
        }

        /// <summary>An encounter arena of act1.stretch1 with at least 6 m of floor past x = 10 m: its left end and its gate.</summary>
        static (float left, float gate) FarArena()
        {
            var data = StretchData.FromStage("act1.stretch1");
            for (int i = 0; i < data.Encounters.Length; i++)
            {
                float left = (float)data.ArenaLeft(i), gate = (float)data.Encounters[i].Gate;
                if (gate - Mathf.Max(left, 10f) > 6f) return (left, gate);
            }
            Assert.Fail("act1.stretch1 has an encounter arena reaching 6 m past x = 10 m");
            return default;
        }

        /// <summary>Kage and a footman on the road act1.stretch1 at <paramref name="kageX"/> / <paramref name="foeX"/>, the fight's bounds [left, right].</summary>
        (FightContext ctx, Fighter kage, Fighter foe) Road(float left, float right, float kageX, float foeX)
        {
            _root = new GameObject("ProjectileBoundsRoad");
            var b = new FightBuilder(_root.transform, 132, manualClock: true);
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var foe = b.Spawn(CharacterCatalog.Get("order_footman"), 1, new StandStill());
            var ctx = b.Build();
            _road = StretchRoad.BuildPhysics(_root.transform, StretchData.FromStage("act1.stretch1"), ctx.Bounds);
            ctx.Bounds.Set(left, right);
            ctx.Begin();
            float toFoe = Mathf.Sign(foeX - kageX);
            kage.ResetForRound(kageX, toFoe); kage.BeginFight();
            foe.ResetForRound(foeX, -toFoe); foe.BeginFight();
            return (ctx, kage, foe);
        }

        /// <summary>A kunai from Kage's hand, as <c>Fighter.ThrowKunai</c> throws it.</summary>
        static Projectile Kunai(FightContext ctx, Fighter kage) =>
            Projectile.Spawn(ctx, kage, Fighter.KunaiProjectile, new Vector2(kage.Pos.x + kage.facing * 0.26f, 1.15f), kage.facing);

        [TestCase(1f)]
        [TestCase(-1f)]
        public void AKunaiThrownPastTheArenaLimitOnARoadFliesAndHitsAFoeDownTheRoad(float dir)
        {
            var (left, gate) = FarArena();
            float kageX = dir > 0f ? Mathf.Max(left + 1f, 10f) : gate - 1f, foeX = kageX + dir * 3f;
            Assert.Greater(Mathf.Min(kageX, foeX), Projectile.BoundsMargin + StageBounds.DefaultHalf, "both fighters stand past the dev arena's limit");
            var (ctx, kage, foe) = Road(left, gate, kageX, foeX);
            using var log = new CombatEventLog(ctx.Events);

            var p = Kunai(ctx, kage);
            p.Tick();
            Assert.IsTrue(p.Alive, $"a kunai thrown at x = {kageX:0.00} inside the road's bounds [{left:0.00}, {gate:0.00}] still flies after its first frame");

            int ticks = 1;
            while (p.Alive && ticks < Cap) { p.Tick(); ticks++; }

            var hit = log.All.Where(e => e.Type == CombatEventType.Hit).ToList();
            Assert.AreEqual(1, hit.Count, "the kunai hit the foe down the road");
            Assert.AreSame(kage, hit[0].Source);
            Assert.AreSame(foe, hit[0].Target);
            Assert.AreEqual("kunai", hit[0].MoveId);
            Assert.Less(foe.health, foe.maxHealth, "the foe took the kunai's damage");
            Assert.IsFalse(p.Alive, "the kunai is spent on its one target");
            Assert.That(ticks, Is.InRange(10, 30), "about 2.7 m at 10.87 m/s is about 15 frames");
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void OnARoadAProjectileEndsOnlyPastTheRoadsWallByTheMargin(float dir)
        {
            var (left, gate) = FarArena();
            float kageX = dir > 0f ? gate - 1f : Mathf.Max(left + 1f, 10f);
            var (ctx, kage, foe) = Road(left, gate, kageX, kageX - dir * 1.5f);      // the foe behind Kage: nothing in its path
            var bolt = new ProjectileDef
            {
                id = "testBolt", hit = new HitWindow { dmg = 1, level = HitLevel.Mid }, speed = 6f, radius = 0.2f, lifeFrames = Cap,
            };
            var p = Projectile.Spawn(ctx, kage, bolt, new Vector2(kageX, 1f), dir);
            float wall = dir > 0f ? gate : left, end = wall + dir * Projectile.BoundsMargin;

            float last = p.Position.x;
            int ticks = 0;
            while (p.Alive && ticks < Cap) { last = p.Position.x; p.Tick(); ticks++; }

            Assert.IsFalse(p.Alive, "it ended before its life ran out");
            Assert.Less(ticks, Cap);
            Assert.That(dir * (p.Position.x - end), Is.GreaterThan(0f), "it ended past the wall by more than the margin");
            Assert.That(dir * (last - end), Is.LessThanOrEqualTo(0f), "and not a frame before");
            Assert.AreEqual(foe.maxHealth, foe.health, "it hit nothing");
        }

        [Test]
        public void TheMarginPutsTheDevArenasEndAtExactlyTheOldLimit()
        {
            float right = StageBounds.DefaultHalf + Projectile.BoundsMargin, left = -StageBounds.DefaultHalf - Projectile.BoundsMargin;
            Assert.AreEqual(9.5f, right, 0f);
            Assert.AreEqual(-9.5f, left, 0f);
        }

        /// <summary>
        /// The old rule (before SAN-132), as <see cref="Projectile.Tick"/> ran it for a level projectile at full speed: the same
        /// float steps, spent at its life or past |x| = 9.5 m. Returns its age and where it ended.
        /// </summary>
        static (int age, Vector2 pos) OldFlight(Vector2 pos, float dir, float speed, int life)
        {
            Vector2 vel = new Vector2(dir * speed, 0f);
            float dt = Time.fixedDeltaTime * 1f;
            int age = 0;
            for (int i = 0; i < Cap; i++)
            {
                age++;
                pos = pos + vel * dt;
                if (age >= life || Mathf.Abs(pos.x) > 9.5f) return (age, pos);
            }
            Assert.Fail("the old flight ended within the cap");
            return default;
        }

        /// <summary>
        /// In the dev arena every level projectile lives exactly as many frames and ends exactly (bit for bit) where the old
        /// fixed-limit rule ended it: a sweep of start points across and past the arena, both directions, several speeds and lives.
        /// </summary>
        [Test]
        public void InTheDevArenaAProjectileEndsWhereAndWhenTheOldFixedLimitEndedIt()
        {
            _root = new GameObject("ProjectileBoundsArena");
            var bld = new FightBuilder(_root.transform, 132, manualClock: true);
            bld.AddGround();
            var kage = bld.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var foe = bld.Spawn(CharacterCatalog.Get("order_footman"), 1, new StandStill());
            var ctx = bld.Build();
            ctx.Begin();
            kage.ResetForRound(-2f, 1f); kage.BeginFight();
            foe.ResetForRound(2f, -1f); foe.BeginFight();
            Assert.AreEqual(-StageBounds.DefaultHalf, ctx.Bounds.Left);
            Assert.AreEqual(StageBounds.DefaultHalf, ctx.Bounds.Right);

            float[] starts = { -9.7f, -9.5f, -9.49f, -9.4f, -7.6f, -5f, -0.3f, 0f, 0.3f, 5f, 7.6f, 9.4f, 9.49f, 9.5f, 9.7f };
            float[] speeds = { 3f, 6f, Fighter.KunaiProjectile.speed, 17.3f };
            int[] lives = { 30, Fighter.KunaiProjectile.lifeFrames, 600 };
            int flights = 0, edgeEnds = 0, lifeEnds = 0;
            foreach (float dir in new[] { 1f, -1f })
            foreach (float x0 in starts)
            foreach (float speed in speeds)
            foreach (int life in lives)
            {
                var def = new ProjectileDef
                {
                    id = "testBolt", hit = new HitWindow { dmg = 1, level = HitLevel.Mid }, speed = speed, radius = 0.2f, lifeFrames = life,
                };
                var start = new Vector2(x0, 4f);         // above everyone: nothing in its path
                var want = OldFlight(start, dir, speed, life);
                var p = Projectile.Spawn(ctx, kage, def, start, dir);
                int ticks = 0;
                while (p.Alive && ticks < Cap) { p.Tick(); ticks++; }
                string at = $"from {x0} dir {dir} at {speed} m/s, life {life}";
                Assert.IsFalse(p.Alive, $"{at}: it ended");
                Assert.AreEqual(want.age, p.Age, $"{at}: its age at the end");
                Assert.AreEqual(want.age, ticks, $"{at}: the frames it flew");
                Assert.AreEqual(want.pos.x, p.Position.x, 0f, $"{at}: where it ended (x)");
                Assert.AreEqual(want.pos.y, p.Position.y, 0f, $"{at}: where it ended (y)");
                if (want.age < life) edgeEnds++; else lifeEnds++;
                flights++;
            }
            Assert.AreEqual(0, ctx.Projectiles.Count);
            Assert.Greater(edgeEnds, 0, "some flights end at the edge");
            Assert.Greater(lifeEnds, 0, "some end at their life");
            Assert.AreEqual(2 * starts.Length * speeds.Length * lives.Length, flights);
            Assert.AreEqual(foe.maxHealth, foe.health, "nothing was hit");
            Assert.AreEqual(kage.maxHealth, kage.health);
        }
    }
}
