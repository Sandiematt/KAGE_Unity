using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The engagement rule (SAN-83): at most <see cref="EngagementTuning.maxAttackers"/> foes are engaged, and only engaged
    /// foes attack. A foe is <b>attacking</b> on a frame when it is in an attack move (<see cref="Fighter.InAttackMove"/>:
    /// a move with a hit window, a throw, a projectile or a counter window). The rest wait at a distance and circle. When an
    /// engaged foe is knocked out, a waiting foe takes its place. Group fights are deterministic.
    /// </summary>
    public class EngagementTests
    {
        /// <summary>Kage in the middle, two Footmen on each side: the dev arena's group fight (F6).</summary>
        static readonly float[] FourFootmen = { 1.3f, -2.4f, 2.8f, -3.8f };

        readonly List<GroupFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        GroupFight Fight(int seed, bool kageAi = true, CharacterDef kageDef = null, float aiLevel = 0.6f)
        {
            var f = new GroupFight(seed, 0f, FourFootmen, kageAi: kageAi, kageDef: kageDef, aiLevel: aiLevel);
            _fights.Add(f);
            return f;
        }

        static EngagementTuning T => Tuning.Get<EngagementTuning>();

        /// <summary>Kage hits hard and lasts, so a long fight knocks Footmen out without Kage going down first.</summary>
        static CharacterDef StrongKage()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.damageMul = 4f;
            d.healthMul = 20f;
            return d;
        }

        [Test]
        public void Two_foes_at_most_are_engaged_from_the_first_frame()
        {
            Assert.AreEqual(2, T.maxAttackers, "the JS game's number");
            var f = Fight(83);
            f.Run(1);
            CollectionAssert.AreEquivalent(new[] { f.Foes[0], f.Foes[1] }, f.Ctx.Engagement.Engaged, "the nearest two");
        }

        [Test]
        public void No_more_than_two_foes_attack_on_any_frame()
        {
            // seed 86 (SAN-95): with Kage this strong the foes fall within about 1300 frames, and on seeds 83 and 89 the AI profile's foes
            // never happened to attack at the same moment before they did (8 of 10 seeds from 83 do, up to 206 frames with two)
            var f = Fight(86, kageDef: StrongKage(), aiLevel: 0.9f);
            int max = 0, framesWithTwo = 0;
            for (int i = 0; i < 3600; i++)
            {
                f.Run(1);
                int n = f.Attacking();
                Assert.LessOrEqual(n, T.maxAttackers, $"frame {i}: foes in an attack move");
                Assert.LessOrEqual(f.Ctx.Engagement.Engaged.Count, T.maxAttackers, $"frame {i}: engaged foes");
                foreach (var foe in f.Foes)
                    if (foe.InAttackMove) Assert.IsTrue(f.Ctx.Engagement.IsEngaged(foe), $"frame {i}: {foe.FighterId} attacks without being engaged");
                max = Mathf.Max(max, n);
                if (n == 2) framesWithTwo++;
            }
            Assert.AreEqual(2, max, "two foes did attack together (the limit was reached, not just never approached)");
            Assert.Greater(framesWithTwo, 0);
            Assert.Greater(f.Events(CombatEventType.MoveStarted).Count(e => e.Source != f.Kage), 10, "the foes fought (SAN-110: the bar is 10, it was 20; with the tougher Footmen the seed gives exactly 20 and the count is chaotic)");

            foreach (var e in f.Log.All)
                if (e.Type is CombatEventType.Hit or CombatEventType.Blocked && e.Source != null && e.Target != null)
                    Assert.AreNotEqual(e.Source.team, e.Target.team, $"{e.SourceId} hit {e.TargetId}, its own team");
        }

        [Test]
        public void Waiting_foes_keep_their_distance_and_circle()
        {
            var f = Fight(83, kageAi: false);
            f.KageInput.Add(900, buttons: "G");      // Kage guards in place; the two engaged Footmen press him
            float nearest = float.MaxValue;
            var walkedIn = new HashSet<Fighter>();
            var walkedOut = new HashSet<Fighter>();
            for (int i = 0; i < 900; i++)
            {
                f.Run(1);
                if (i < 120) continue;      // the waiting foes settle at their distance
                foreach (var foe in f.Foes)
                {
                    if (f.Ctx.Engagement.IsEngaged(foe) || foe.state == FState.KO) continue;
                    float d = Mathf.Abs(foe.Pos.x - f.Kage.Pos.x);
                    nearest = Mathf.Min(nearest, d);
                    if (foe.state == FState.Walk)
                    {
                        float toward = Mathf.Sign(f.Kage.Pos.x - foe.Pos.x);
                        if (foe.rb.linearVelocity.x * toward > 0.1f) walkedIn.Add(foe); else if (foe.rb.linearVelocity.x * toward < -0.1f) walkedOut.Add(foe);
                    }
                }
            }
            // Kage is driven back by the guarded hits and the push; the waiting foes step back from him
            Assert.Greater(nearest, T.waitDistance - T.retreatInside - 0.8f, "the nearest a waiting foe came to Kage");
            Assert.IsTrue(walkedIn.Overlaps(walkedOut), "a waiting foe circled: it walked both in and out");
        }

        [Test]
        public void When_an_engaged_foe_is_defeated_a_waiting_foe_takes_its_place()
        {
            var f = Fight(83, kageDef: StrongKage(), aiLevel: 0.9f);
            Fighter defeated = null;
            List<Fighter> before = null, waitingBefore = null;
            for (int i = 0; i < 7200 && defeated == null; i++)
            {
                var engaged = f.Ctx.Engagement.Engaged.ToList();
                var waiting = new List<Fighter>();
                f.Ctx.Engagement.Waiting(f.Ctx.Fighters, waiting);
                int kos = f.Log.Count(CombatEventType.KnockedOut);
                f.Run(1);
                if (f.Log.Count(CombatEventType.KnockedOut) == kos) continue;
                var ko = f.Events(CombatEventType.KnockedOut).Last().Target;
                Assert.AreNotSame(f.Kage, ko, "Kage went down first");
                if (!engaged.Contains(ko)) continue;     // Kage caught a waiting foe: look for the next one
                defeated = ko; before = engaged; waitingBefore = waiting;
            }
            Assert.IsNotNull(defeated, "an engaged foe was knocked out");
            Assume.That(waitingBefore.Count, Is.GreaterThan(0));
            var nearest = waitingBefore.OrderBy(EngagementManager.Distance).First();   // where they stand as the next frame starts

            f.Next();     // the KO's hitstop and slow motion pass first: the next frame that runs fills the place
            var now = f.Ctx.Engagement.Engaged;
            CollectionAssert.DoesNotContain(now, defeated);
            Assert.AreEqual(2, now.Count, "the place is filled at once");
            var newcomer = now.Single(e => !before.Contains(e));
            CollectionAssert.Contains(waitingBefore, newcomer, "a waiting foe took the place");
            Assert.AreSame(nearest, newcomer, "the nearest waiting foe");
        }

        [Test]
        public void A_group_fight_is_identical_every_run()
        {
            List<string> events, frames;
            (List<string>, List<string>) Once()
            {
                var f = Fight(8384);
                f.Run(1200);
                var r = (f.Log.Lines().ToList(), f.Frames.ToList());
                f.Dispose();
                _fights.Remove(f);
                return r;
            }
            (events, frames) = Once();
            var (events2, frames2) = Once();
            Assert.Greater(events.Count(l => l.Contains(" Hit ") || l.Contains(" Blocked ")), 0, "the fight lands hits");
            // ADR-0002: identical events; positions within 1e-4 m (with five bodies the physics step's contact order can
            // differ in the last digit between two fights in one session)
            CollectionAssert.AreEqual(events, events2);
            Assert.IsNull(BaselineReplay.Compare(BaselineReplay.Format("group", 8384, events, frames), BaselineReplay.Format("group", 8384, events2, frames2)));
        }
    }
}
