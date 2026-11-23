using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The AI's wall checks read the fight's bounds (SAN-116): on a stretch road the walls are where <see cref="StageBounds"/> puts
    /// them (the road's start, an encounter arena's left end, a closed gate), so a foe pressed against one steps away from it instead
    /// of backing into it. In the dev arena (walls at ±7.6 m) the checks answer exactly as the old sign-of-x ones did, so duels and
    /// the baseline replay are unchanged. Built with <see cref="FightBuilder"/> on a manual clock; the AI is polled, the fight not stepped.
    /// </summary>
    public class AiWallSideTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        const int Polls = 900;
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
            // in edit mode the fight loop's OnDestroy doesn't run: put back what the builder changed
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
        }

        /// <summary>
        /// A footman (the "regular" profile) and Kage on the road act1.stretch1, the fight's bounds set to [left, right]. Engaged, the
        /// foe fights (its profile's spacing and strafing steps); not engaged, it waits its turn (backs off to its waiting distance, SAN-83).
        /// </summary>
        (Fighter foe, Fighter kage, AIInputSource ai) Road(float left, float right, float foeX, float kageX, bool engaged, int seed = 116)
        {
            _root = new GameObject("AiWallSideRoad");
            var b = new FightBuilder(_root.transform, seed, manualClock: true);
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var ai = new AIInputSource();
            var foe = b.Spawn(CharacterCatalog.Get("order_footman"), 1, ai);
            var ctx = b.Build();
            _road = StretchRoad.BuildPhysics(_root.transform, StretchData.FromStage("act1.stretch1"), ctx.Bounds);
            ctx.Bounds.Set(left, right);
            ctx.Begin();
            float toKage = Mathf.Sign(kageX - foeX);
            kage.ResetForRound(kageX, -toKage); kage.BeginFight();
            foe.ResetForRound(foeX, toKage); foe.BeginFight();
            Assert.IsNotNull(ai.profile, "the footman decides with its profile");
            if (engaged) ctx.Engagement.Update(ctx.Fighters);
            Assert.AreEqual(engaged, ctx.Engagement.MayAttack(foe), engaged ? "the foe is engaged" : "the foe waits its turn");
            return (foe, kage, ai);
        }

        /// <summary>Polls the foe's AI with both fighters held where they are; counts walking inputs into its wall and away from it.</summary>
        static (int into, int away) PollAtWall(Fighter foe, AIInputSource ai)
        {
            int wall = foe.WallSide, into = 0, away = 0;
            Assert.AreNotEqual(0, wall, "the foe stands at a wall");
            for (int i = 0; i < Polls; i++)
            {
                var o = ai.Poll(foe);
                if (o.dx == wall) into++;
                else if (o.dx == -wall) away++;
            }
            return (into, away);
        }

        static void AssertStepsAway(string where, Fighter foe, AIInputSource ai)
        {
            var (into, away) = PollAtWall(foe, ai);
            Assert.AreEqual(0, into, $"{where}: the foe never walks back into the wall ({into} of {Polls} frames did)");
            Assert.Greater(away, 0, $"{where}: the foe steps away from the wall");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AFoeAtAnEncounterArenasLeftEndStepsAwayFromTheWall(bool engaged)
        {
            var data = StretchData.FromStage("act1.stretch1");
            float left = (float)data.ArenaLeft(1), gate = (float)data.Encounters[1].Gate;
            Assert.Greater(left, 1f, "the second encounter's arena starts well along the road (x > 0)");
            var (foe, _, ai) = Road(left, gate, left, left + 0.7f, engaged);
            Assert.AreEqual(-1, foe.WallSide);
            AssertStepsAway("arena left end", foe, ai);
            Assert.IsTrue(AIInputSource.WallBehind(foe, foe.opponent), "the left wall is behind the foe");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AFoePressedAgainstAClosedGateStepsAwayFromIt(bool engaged)
        {
            var data = StretchData.FromStage("act1.stretch1");
            float left = (float)data.ArenaLeft(1), gate = (float)data.Encounters[1].Gate;
            var (foe, _, ai) = Road(left, gate, gate, gate - 0.7f, engaged);
            Assert.AreEqual(1, foe.WallSide);
            AssertStepsAway("closed gate", foe, ai);
            Assert.IsTrue(AIInputSource.WallBehind(foe, foe.opponent), "the gate is behind the foe");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AFoeAtTheRoadsStartStepsAwayFromTheWall(bool engaged)
        {
            var data = StretchData.FromStage("act1.stretch1");
            var (foe, _, ai) = Road(0f, (float)data.Limit(0), 0f, 0.7f, engaged);
            Assert.AreEqual(-1, foe.WallSide);
            AssertStepsAway("road start", foe, ai);
            Assert.IsTrue(AIInputSource.WallBehind(foe, foe.opponent), "the road's start is behind the foe");
        }

        /// <summary>
        /// In the dev arena the bounds-aware checks give the old sign-of-x answers at every position (walls, near them, the middle,
        /// the opponent on either side), so arena duels and the baseline replay decide exactly as before.
        /// </summary>
        [Test]
        public void InTheDevArenaTheChecksAnswerAsTheOldSignOfXOnes()
        {
            _root = new GameObject("AiWallSideArena");
            var b = new FightBuilder(_root.transform, 116, manualClock: true);
            b.AddGround();
            var a = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var c = b.Spawn(CharacterCatalog.Get("order_footman"), 1, new AIInputSource());
            var ctx = b.Build();
            Assert.AreEqual(-StageBounds.DefaultHalf, ctx.Bounds.Left);
            Assert.AreEqual(StageBounds.DefaultHalf, ctx.Bounds.Right);

            float h = StageBounds.DefaultHalf;
            float[] xs = { -h, -h + 0.02f, -h + 0.05f, -h + 0.06f, -h + 0.5f, -3f, -0.4f, 0f, 0.6f, 3f, h - 0.5f, h - 0.06f, h - 0.05f, h - 0.02f, h };
            int checkedPairs = 0, atWall = 0;
            foreach (float sx in xs)
            foreach (float ox in xs)
            {
                if (sx == ox) continue;
                a.ResetForRound(sx, 1f);
                c.ResetForRound(ox, -1f);
                bool oldWallBehind = a.AtWall && Mathf.Sign(a.Pos.x) == -Mathf.Sign(c.Pos.x - a.Pos.x);
                bool oldCornered = a.AtWall && Mathf.Sign(c.Pos.x - a.Pos.x) != Mathf.Sign(-a.Pos.x);
                Assert.AreEqual(oldWallBehind, AIInputSource.WallBehind(a, c), $"wall behind, self {sx} opp {ox}");
                Assert.AreEqual(oldCornered, AIInputSource.Cornered(a, c), $"cornered, self {sx} opp {ox}");
                checkedPairs++;
                if (a.AtWall) atWall++;
            }
            Assert.Greater(atWall, 0, "some positions are at a wall");
            Assert.AreEqual(xs.Length * (xs.Length - 1), checkedPairs);
        }
    }
}
