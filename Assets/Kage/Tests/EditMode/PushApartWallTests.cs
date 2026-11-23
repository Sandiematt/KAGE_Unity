using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The legacy push-apart reads the fight's bounds at a wall (SAN-131, like SAN-116 for the AI): on a stretch road a fighter
    /// pressed against a left wall at x &gt;= 0 (an encounter arena's left end, the road's start) is not pushed into it, and the
    /// other fighter takes the whole push. In the dev arena (walls at ±7.6 m) the push moves fighters exactly as the old sign-of-x
    /// check did, so arena duels and the baseline replay are unchanged. Built with <see cref="FightBuilder"/> on a manual clock;
    /// <see cref="FightLoop.Pushbox"/> is called directly, the fight not stepped.
    /// </summary>
    public class PushApartWallTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        const float Min = 0.72f;        // FightLoop.Pushbox's minimum separation
        const float Gap = 0.3f;
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

        /// <summary>Kage and a footman on the road act1.stretch1, the fight's bounds set to [left, right].</summary>
        (Fighter kage, Fighter foe) Road(float left, float right)
        {
            _root = new GameObject("PushApartRoad");
            var b = new FightBuilder(_root.transform, 131, manualClock: true);
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var foe = b.Spawn(CharacterCatalog.Get("order_footman"), 1, new StandStill());
            var ctx = b.Build();
            _road = StretchRoad.BuildPhysics(_root.transform, StretchData.FromStage("act1.stretch1"), ctx.Bounds);
            ctx.Bounds.Set(left, right);
            ctx.Begin();
            kage.BeginFight();
            foe.BeginFight();
            return (kage, foe);
        }

        /// <summary>
        /// Puts <paramref name="atWall"/> on the wall at <paramref name="wallX"/> and <paramref name="other"/> just off it (overlapping),
        /// pushes them apart with the wall fighter as Pushbox's first or second argument, and checks the wall fighter stays put.
        /// </summary>
        static void AssertPushedOffWall(string where, Fighter atWall, Fighter other, float wallX, int wallSide, bool wallFirst)
        {
            float otherX = wallX - wallSide * Gap;
            atWall.ResetForRound(wallX, -wallSide);
            other.ResetForRound(otherX, wallSide);
            Assert.AreEqual(wallSide, atWall.WallSide, $"{where}: the fighter stands at the wall");
            Assert.AreEqual(0, other.WallSide, $"{where}: the other fighter is off the wall");

            if (wallFirst) FightLoop.Pushbox(atWall, other);
            else FightLoop.Pushbox(other, atWall);

            Assert.AreEqual(wallX, atWall.Pos.x, 1e-4f, $"{where} (wall fighter {(wallFirst ? "first" : "second")}): the fighter at the wall is not pushed into it");
            Assert.AreEqual(wallX - wallSide * Min, other.Pos.x, 1e-4f, $"{where} (wall fighter {(wallFirst ? "first" : "second")}): the other fighter takes the whole push, out from the wall");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AtAnEncounterArenasLeftEndTheFighterAtTheWallIsNotPushedIntoIt(bool wallFirst)
        {
            var data = StretchData.FromStage("act1.stretch1");
            float left = (float)data.ArenaLeft(1), gate = (float)data.Encounters[1].Gate;
            Assert.Greater(left, 1f, "the second encounter's arena starts well along the road (x > 0)");
            var (kage, foe) = Road(left, gate);
            AssertPushedOffWall("arena left end, foe at the wall", foe, kage, left, -1, wallFirst);
            AssertPushedOffWall("arena left end, Kage at the wall", kage, foe, left, -1, wallFirst);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AtTheRoadsStartTheFighterAtTheWallIsNotPushedIntoIt(bool wallFirst)
        {
            var data = StretchData.FromStage("act1.stretch1");
            var (kage, foe) = Road(0f, (float)data.Limit(0));
            AssertPushedOffWall("road start, foe at the wall", foe, kage, 0f, -1, wallFirst);
            AssertPushedOffWall("road start, Kage at the wall", kage, foe, 0f, -1, wallFirst);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AtAClosedGateTheFighterAtTheWallIsNotPushedThroughIt(bool wallFirst)
        {
            var data = StretchData.FromStage("act1.stretch1");
            float left = (float)data.ArenaLeft(1), gate = (float)data.Encounters[1].Gate;
            var (kage, foe) = Road(left, gate);
            AssertPushedOffWall("closed gate, foe at the wall", foe, kage, gate, 1, wallFirst);
        }

        /// <summary>The push as it was before SAN-131: the wall's side taken from the sign of x (dev arena only).</summary>
        static (float a, float b) OldPush(Fighter a, Fighter b)
        {
            Vector2 pa = a.rb.position, pb = b.rb.position;
            float dx = pb.x - pa.x;
            if (Mathf.Abs(dx) >= Min) return (pa.x, pb.x);
            float dir = Mathf.Abs(dx) < 0.001f ? a.facing : Mathf.Sign(dx);
            float overlap = Min - Mathf.Abs(dx);
            float sa = 0.5f, sb = 0.5f;
            if (a.AtWall && Mathf.Sign(pa.x) == -dir) { sa = 0f; sb = 1f; }
            if (b.AtWall && Mathf.Sign(pb.x) == dir) { sa = 1f; sb = 0f; }
            return (pa.x - dir * overlap * sa, pb.x + dir * overlap * sb);
        }

        /// <summary>
        /// In the dev arena the push moves both fighters exactly (bit for bit) as the old sign-of-x one did, at every pair of
        /// positions in a sweep across the arena, near and at both walls, overlapping or not, both facings.
        /// </summary>
        [Test]
        public void InTheDevArenaThePushMovesFightersAsTheOldSignOfXOne()
        {
            _root = new GameObject("PushApartArena");
            var bld = new FightBuilder(_root.transform, 131, manualClock: true);
            bld.AddGround();
            var a = bld.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var c = bld.Spawn(CharacterCatalog.Get("order_footman"), 1, new StandStill());
            var ctx = bld.Build();
            Assert.AreEqual(-StageBounds.DefaultHalf, ctx.Bounds.Left);
            Assert.AreEqual(StageBounds.DefaultHalf, ctx.Bounds.Right);

            float h = StageBounds.DefaultHalf;
            float[] anchors = { -h, -h + 0.03f, -h + 0.3f, -2f, 0f, 2f, h - 0.3f, h - 0.03f, h };
            float[] offsets = { -0.8f, -0.71f, -0.5f, -0.3f, -0.05f, -0.0005f, 0f, 0.0005f, 0.05f, 0.3f, 0.5f, 0.71f, 0.8f };
            int checkedPairs = 0, wallPushes = 0;
            foreach (float face in new[] { 1f, -1f })
            foreach (float ax in anchors)
            foreach (float off in offsets)
            {
                float cx = Mathf.Clamp(ax + off, -h, h);
                a.ResetForRound(ax, face);
                c.ResetForRound(cx, -face);
                var want = OldPush(a, c);
                if ((a.AtWall || c.AtWall) && Mathf.Abs(cx - ax) < Min) wallPushes++;
                FightLoop.Pushbox(a, c);
                Assert.AreEqual(want.a, a.Pos.x, $"first fighter, at {ax} / {cx}, facing {face}");
                Assert.AreEqual(want.b, c.Pos.x, $"second fighter, at {ax} / {cx}, facing {face}");
                checkedPairs++;
            }
            Assert.Greater(wallPushes, 0, "some pushes happen at a wall");
            Assert.AreEqual(2 * anchors.Length * offsets.Length, checkedPairs);
        }
    }
}
