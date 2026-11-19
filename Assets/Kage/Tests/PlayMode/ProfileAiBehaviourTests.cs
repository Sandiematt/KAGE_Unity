using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// How an AI profile makes a foe fight (SAN-95): the Footman with the regular, agile and defensive profiles behaves measurably
    /// differently over a fixed-seed fight, guards and parries at its profile's rates through the Guard input, reacts after its
    /// profile's delay, and the Ronin's legacy AI is a separate thing. Every fight is built from character data in the harness
    /// and disposed before the next.
    /// </summary>
    public class ProfileAiBehaviourTests
    {
        const int Seed = 95;

        [TearDown]
        public void TearDown()
        {
            Unregister();
            Tuning.ClearOverrides();
        }

        // ---------------------------------------------------------------- spacing, pace

        struct Stats { public float meanDistance, attacksPerSecond; }

        /// <summary>
        /// The Footman fights a Kage who stands still and cannot be hurt, for 2400 frames: how far apart they stand while he is not
        /// in the middle of a move, a dash or a hit reaction (where he stands between attacks is what the preferred range sets), and how often he attacks.
        /// </summary>
        static Stats SpacingOnce(string profile, int seed, bool movementOnly)
        {
            using var f = new HeadlessFight(seed, Foe.Ai, -1.2f, 1.2f, foeDef: Footman(profile));
            if (movementOnly)
            {
                // the shipped profile with its attacking switched off: what is left is where it chooses to stand
                var p = AiProfiles.Get(profile);
                p.aggression = p.grabRate = p.dashInRate = p.jumpRate = p.kunaiRate = 0f;
                f.Ai.SetProfile(p);
            }
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            f.Ronin.maxHealth = f.Ronin.health = 1e7f;
            f.KageInput.Add(2500);
            double dist = 0;
            int starts = 0, standing = 0; bool was = false;
            for (int i = 0; i < 2400; i++)
            {
                f.Run(1);
                if (!f.Ronin.Busy && f.Ronin.state != FState.Dash && f.Ronin.state != FState.Air) { dist += Mathf.Abs(f.Ronin.Pos.x - f.Kage.Pos.x); standing++; }
                bool atk = f.Ronin.InAttackMove;
                if (atk && !was) starts++;
                was = atk;
            }
            return new Stats { meanDistance = standing == 0 ? 0f : (float)(dist / standing), attacksPerSecond = starts / 40f };
        }

        /// <summary>The mean over five fixed seeds (95 to 99), so one lucky fight doesn't decide.</summary>
        static Stats Spacing(string profile, bool movementOnly = false)
        {
            var sum = new Stats();
            for (int seed = Seed; seed < Seed + 5; seed++)
            {
                var s = SpacingOnce(profile, seed, movementOnly);
                sum.meanDistance += s.meanDistance / 5f;
                sum.attacksPerSecond += s.attacksPerSecond / 5f;
            }
            return sum;
        }

        [Test]
        public void TheAgileFootmanStandsFartherOffAndTheDefensiveOneCloserThanTheRegularOne()
        {
            var reg = Spacing("regular", true); var agile = Spacing("agile", true); var def = Spacing("defensive", true);     // aggression 0: where it stands when it is not attacking
            Debug.Log($"SAN-95 standing distance: regular {reg.meanDistance:0.00} m, agile {agile.meanDistance:0.00} m, defensive {def.meanDistance:0.00} m");
            Assert.Greater(agile.meanDistance, reg.meanDistance + 0.05f, "agile keeps its distance (preferred range 1.39 m against 1.09 m)");
            Assert.Less(def.meanDistance, reg.meanDistance - 0.03f, "defensive stands closer (1.0 m)");
        }

        [Test]
        public void TheDefensiveFootmanAttacksLessOftenThanTheRegularOne()
        {
            var reg = Spacing("regular"); var def = Spacing("defensive");
            Assert.Greater(reg.attacksPerSecond, 0.3f, "the regular Footman does attack");
            Debug.Log($"SAN-95 attacks a second: regular {reg.attacksPerSecond:0.00}, defensive {def.attacksPerSecond:0.00}, agile {Spacing("agile").attacksPerSecond:0.00}");
            Assert.Less(def.attacksPerSecond, reg.attacksPerSecond * 0.85f, $"defensive aggression 0.34 against 0.55: {def.attacksPerSecond:0.00} against {reg.attacksPerSecond:0.00} attacks a second");
        }

        // ---------------------------------------------------------------- guard and parry rates

        /// <summary>Raised-guard rate per profile: parry + guard (the JS parry and block columns), at the default stage level.</summary>
        [TestCase("regular", 0.44f)]
        [TestCase("agile", 0.23f)]
        [TestCase("defensive", 0.78f)]
        [TestCase("heavy", 0.12f)]
        public void TheFootmanGuardsAtItsProfilesRateThroughTheGuardInput(string profile, float expected)
        {
            var r = Probe(AiProfiles.Get(profile), Seed, 140);
            Debug.Log($"SAN-95 guard rate {profile}: {r.raised}/{r.attacks} = {r.Raised:0.00} (profile {expected:0.00}), parry attempts {r.Attempts:0.00}, mean delay {r.Delay:0.0} frames");
            Assert.GreaterOrEqual(r.attacks, 70, "enough attacks were thrown at a foe that could answer");
            Assert.AreEqual(expected, r.Raised, 0.12f, $"{profile}: guard raised on {r.raised} of {r.attacks} attacks");
        }

        [Test]
        public void TheDefensiveFootmanGuardsMoreOftenThanTheRegularOneAndTheAgileOneLess()
        {
            float reg = Probe(AiProfiles.Get("regular"), Seed, 120).Raised;
            float agile = Probe(AiProfiles.Get("agile"), Seed, 120).Raised;
            float def = Probe(AiProfiles.Get("defensive"), Seed, 120).Raised;
            Assert.Greater(def, reg + 0.15f);
            Assert.Greater(reg, agile + 0.08f);
        }

        [Test]
        public void TheParryRateComesFromTheProfileAndTheAIAttemptsThatShareOfAttacks()
        {
            var p = AiProfiles.Get("regular");
            p.parryRate = 0.4f; p.guardRate = 0.3f;                     // a profile variant with a rate big enough to count
            var r = Probe(p, Seed, 140);
            Debug.Log($"SAN-95 parry: {r.attempts}/{r.attacks} = {r.Attempts:0.00} attempts (rate 0.40), {r.parried} parried");
            Assert.AreEqual(0.4f, r.Attempts, 0.12f);
            Assert.AreEqual(0.7f, r.Raised, 0.12f, "a parry attempt is a raised guard too: parry + guard");
        }

        [Test]
        public void TheShippedDefensiveProfileAttemptsParriesAtItsSixteenPercentAndHeavyNever()
        {
            var def = Probe(AiProfiles.Get("defensive"), Seed, 160);
            var heavy = Probe(AiProfiles.Get("heavy"), Seed, 160);
            Debug.Log($"SAN-95 shipped parry attempts: defensive {def.Attempts:0.00} (0.16), heavy {heavy.Attempts:0.00} (0)");
            Assert.AreEqual(0.16f, def.Attempts, 0.1f);
            Assert.AreEqual(0, heavy.attempts, "the heavy profile's parry rate is 0");
        }

        [Test]
        public void AParryAttemptThatLandsInTheWindowIsAParry()
        {
            var p = AiProfiles.Get("regular");
            p.parryRate = 1f;
            var r = Probe(p, Seed, 100);
            Debug.Log($"SAN-95 parry landing: {r.attempts}/{r.attacks} attempts, {r.parried} parried, {r.blocked} blocked, {r.hit} hit");
            Assert.GreaterOrEqual(r.Attempts, 0.85f, "it tries to parry (nearly) every blow: the rate caps at 0.95");
            Assert.Greater(r.parried, 0, "and the blows that land in its window are parried");
            Assert.Greater(r.parried, r.blocked, "a parry attempt holds Guard back until half a window before the blow, so it parries more than it blocks");
        }

        [Test]
        public void TheAgileProfileAnswersAnAttackSoonerThanTheHeavyOne()
        {
            var agile = AiProfiles.Get("agile"); agile.guardRate = 1f; agile.parryRate = 0f;
            var heavy = AiProfiles.Get("heavy"); heavy.guardRate = 1f; heavy.parryRate = 0f;
            var a = Probe(agile, Seed, 60); var h = Probe(heavy, Seed, 60);
            Debug.Log($"SAN-95 reaction: agile {a.Delay:0.0} frames, heavy {h.Delay:0.0} frames");
            Assert.Greater(a.raised, 40);
            Assert.Greater(h.raised, 40);
            Assert.Greater(h.Delay, a.Delay + 3f, "agile reacts in 9 to 16 JS-frames, heavy in 19 to 31");
        }

        // ---------------------------------------------------------------- the Ronin is unchanged

        [Test]
        public void TheLegacyProfileNeverGuardsOrParriesThroughTheProfileRolls()
        {
            var r = Probe(AiProfiles.Get(AiProfiles.LegacyId), Seed, 60);
            Assert.AreEqual(0, r.attempts, "the Ronin's AI has no parry rate");
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void AProfiledFightIsTheSameFightUnderTheSameSeed()
        {
            string Run(string profile, int seed)
            {
                using var f = new HeadlessFight(seed, Foe.Ai, -0.8f, 0.8f, foeDef: Footman(profile));
                f.KageInput.Add(1200);
                f.Run(1200);
                return string.Join("\n", f.Log.Lines());       // the event log: positions can differ in the fifth decimal between runs of the physics engine
            }
            foreach (var profile in new[] { "regular", "agile", "boss" })
            {
                Assert.AreEqual(Run(profile, Seed), Run(profile, Seed), profile);
            }
            Assert.AreNotEqual(Run("regular", Seed), Run("regular", Seed + 1), "another seed is another fight");
        }
    }
}
