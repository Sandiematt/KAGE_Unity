using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The AI's wall checks read the fight's bounds (SAN-116): on a stretch road the walls are where <see cref="StageBounds"/> puts
    /// them (the road's start, an encounter arena's left end, a closed gate), so a foe pressed against one steps away from it instead
    /// of backing into it. In the dev arena (walls at ±7.6 m) <see cref="AIInputSource.WallBehind"/> answers exactly as the old sign-of-x
    /// check did. <see cref="AIInputSource.Cornered"/> does not (SAN-130): the old check was inverted (true only with the opponent on the
    /// wall's side), so a foe with its back to the wall backflipped and stepped back into it; it now means "wall behind, opponent in front".
    /// Built with <see cref="FightBuilder"/> on a manual clock; the AI is polled, the fight not stepped.
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
        /// In the dev arena the bounds-aware wall-behind check gives the old sign-of-x answer at every position (walls, near them, the
        /// middle, the opponent on either side). Cornered is that same condition (SAN-130), no longer the old inverted sign-of-x
        /// "cornered" (at a wall with the opponent on the wall's side), which held a backflip back only when it would have gone away
        /// from the wall; that change is intended and moves arena duels and the baseline replay.
        /// </summary>
        [Test]
        public void InTheDevArenaWallBehindAnswersAsTheOldSignOfXCheckAndCorneredIsWallBehind()
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
            int checkedPairs = 0, atWall = 0, changedCornered = 0;
            foreach (float sx in xs)
            foreach (float ox in xs)
            {
                if (sx == ox) continue;
                a.ResetForRound(sx, 1f);
                c.ResetForRound(ox, -1f);
                bool oldWallBehind = a.AtWall && Mathf.Sign(a.Pos.x) == -Mathf.Sign(c.Pos.x - a.Pos.x);
                bool oldInvertedCornered = a.AtWall && Mathf.Sign(c.Pos.x - a.Pos.x) != Mathf.Sign(-a.Pos.x);
                Assert.AreEqual(oldWallBehind, AIInputSource.WallBehind(a, c), $"wall behind, self {sx} opp {ox}");
                Assert.AreEqual(oldWallBehind, AIInputSource.Cornered(a, c), $"cornered = wall behind, self {sx} opp {ox}");
                if (oldInvertedCornered != oldWallBehind) changedCornered++;
                checkedPairs++;
                if (a.AtWall) atWall++;
            }
            Assert.Greater(atWall, 0, "some positions are at a wall");
            Assert.Greater(changedCornered, 0, "the old inverted cornered check differed at the walls (SAN-130)");
            Assert.AreEqual(xs.Length * (xs.Length - 1), checkedPairs);
        }

        // ---------------------------------------------------------------- SAN-130: cornered

        /// <summary>
        /// A foe with its back to a wall and Kage 0.7 m in front. With <paramref name="legacy"/> the foe decides with the pre-SAN-95 AI
        /// (the Ronin's); otherwise with the footman's profile made to dodge every attack it answers (dodge 1, guard and parry 0), so
        /// every answer is the backflip the cornered check has to hold back. Where: "arenaLeft", "arenaRight" (the dev arena's walls),
        /// "roadStart", "gate" (act1.stretch1's start and its second encounter's closed gate).
        /// </summary>
        (Fighter foe, Fighter kage, AIInputSource ai) Cornered(string where, bool legacy)
        {
            var data = StretchData.FromStage("act1.stretch1");
            float h = StageBounds.DefaultHalf;
            bool road = where == "roadStart" || where == "gate";
            float foeX = where == "arenaLeft" ? -h : where == "arenaRight" ? h : where == "roadStart" ? 0f : (float)data.Encounters[1].Gate;
            int wall = where == "arenaLeft" || where == "roadStart" ? -1 : 1;
            float kageX = foeX - wall * 0.7f;

            _root = new GameObject("AiCornered");
            var b = new FightBuilder(_root.transform, 130, manualClock: true);
            if (!road) b.AddGround();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, new StandStill());
            var ai = new AIInputSource();
            var foe = b.Spawn(CharacterCatalog.Get("order_footman"), 1, ai);
            var ctx = b.Build();
            if (road)
            {
                _road = StretchRoad.BuildPhysics(_root.transform, data, ctx.Bounds);
                if (where == "roadStart") ctx.Bounds.Set(0f, (float)data.Limit(0));
                else ctx.Bounds.Set((float)data.ArenaLeft(1), (float)data.Encounters[1].Gate);
            }
            ctx.Begin();
            if (legacy) ai.SetProfile(AiProfiles.Get(AiProfiles.LegacyId));
            else
            {
                var p = AiProfiles.Get("regular").Clone();
                p.dodgeRate = 1f; p.guardRate = 0f; p.parryRate = 0f;
                ai.SetProfile(p);
            }
            kage.ResetForRound(kageX, wall); kage.BeginFight();
            foe.ResetForRound(foeX, -wall); foe.BeginFight();
            foe.Tick();                                         // one still frame: the foe reads as standing on the ground
            Assert.IsTrue(foe.Grounded, "the foe stands on the ground");
            Assert.AreEqual(wall, foe.WallSide, $"{where}: the foe stands at the wall");
            Assert.IsTrue(AIInputSource.WallBehind(foe, kage), $"{where}: the wall is behind the foe, Kage in front");
            ctx.Engagement.Update(ctx.Fighters);
            Assert.IsTrue(ctx.Engagement.MayAttack(foe), "the foe is engaged");
            return (foe, kage, ai);
        }

        /// <summary>
        /// SAN-130: the cornered foe's AI is polled <see cref="Polls"/> times. Against the dodging profile Kage punches over and over (each
        /// punch started afresh, so the foe answers each one with the dodge); against the legacy AI he stands, so the foe decides its own
        /// moves every few frames (the legacy AI guards nearly every punch it sees and would rarely decide). Either way it never backflips
        /// (a double tap toward the wall) and never walks back into the wall, and it still does something (guards, attacks, rolls out,
        /// steps in) rather than freezing.
        /// </summary>
        [TestCase("arenaLeft", false)]
        [TestCase("arenaRight", false)]
        [TestCase("roadStart", false)]
        [TestCase("gate", false)]
        [TestCase("arenaLeft", true)]
        [TestCase("arenaRight", true)]
        [TestCase("roadStart", true)]
        [TestCase("gate", true)]
        public void ACorneredFoeNeverBackflipsOrStepsBackIntoTheWallAndStillActs(string where, bool legacy)
        {
            var (foe, kage, ai) = Cornered(where, legacy);
            int wall = foe.WallSide;
            var attack = kage.moves.Select(Btn.P, 0, 0, false, kage);
            Assert.IsNotNull(attack, "Kage has a punch");
            Assert.Greater(attack.hits.Count, 0, "the punch has a hit window");

            int backflips = 0, stepsBack = 0, acts = 0;
            for (int i = 0; i < Polls; i++)
            {
                // Kage throws a punch, holds it 12 frames, stands for 3: every punch is a new attack for the foe to answer
                int phase = i % 15;
                if (!legacy && phase == 0) kage.StartMove(attack);
                else if (!legacy && phase == 12) kage.BeginFight();
                var o = ai.Poll(foe);
                if (wall < 0 ? o.tapLeft2 : o.tapRight2) backflips++;
                if (o.dx == wall) stepsBack++;
                if (o.dx != 0 || o.dy != 0 || o.p || o.k || o.s || o.x || o.guard || o.tapLeft2 || o.tapRight2 || o.dash || o.ability > 0) acts++;
            }
            string who = legacy ? "legacy AI" : "dodging profile";
            Assert.AreEqual(0, backflips, $"{where}, {who}: the cornered foe never backflips into the wall ({backflips} backflips)");
            Assert.AreEqual(0, stepsBack, $"{where}, {who}: the cornered foe never steps back into the wall ({stepsBack} of {Polls} frames did)");
            Assert.Greater(acts, Polls / 10, $"{where}, {who}: the cornered foe still acts ({acts} of {Polls} frames had an input)");
        }
    }
}
