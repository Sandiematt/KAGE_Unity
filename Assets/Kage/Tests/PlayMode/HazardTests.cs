using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The generic hazard (SAN-75, ADR-0007): a telegraph for a set time, then a strike that checks
    /// a floor box against hurtboxes and hits each target once. A fighter who left the box isn't hit.
    /// </summary>
    public class HazardTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        /// <summary>Kage left, the Ronin right at 0.6. <paramref name="ronin"/> scripts the Ronin (default: stands still).</summary>
        HeadlessFight Duel(System.Action<ScriptedInput> ronin = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, HeadlessFight.KageX, 0.6f);
            if (ronin != null) ronin(f.RoninInput); else f.RoninInput.Add(600);
            _fights.Add(f);
            return f;
        }

        static HazardDef Shock(int telegraph = 20, int strike = 5, float width = 1f) => new()
        {
            id = "testShock",
            telegraphFrames = telegraph, strikeFrames = strike,
            size = new Vector2(width, 2.4f),
            hit = new HitWindow { dmg = 12, level = HitLevel.Unblockable, kb = new Vector2(1.5f, 3f), stun = 24, hitstop = 6, knockdown = true },
        };

        // ---------------------------------------------------------------------------------------
        [Test]
        public void AHazardShowsItsTelegraphWithoutHittingThenStrikesOnce()
        {
            var f = Duel();
            var h = Hazard.Spawn(f.Ctx, f.Kage, Shock(telegraph: 20, strike: 5), new Vector2(0.6f, 0f));
            Assert.IsTrue(h.Alive);
            Assert.AreEqual(HazardPhase.Telegraph, h.Phase);
            Assert.AreEqual(1, f.Ctx.Hazards.Count);

            f.Run(20);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(), "the telegraph never hits");
            Assert.AreEqual(100f, f.Ronin.health);

            f.Run(1);
            Assert.AreEqual(HazardPhase.Strike, h.Phase);
            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreEqual("testShock", hit.MoveId);
            Assert.AreSame(f.Kage, hit.Source);
            Assert.AreSame(f.Ronin, hit.Target);
            Assert.AreEqual(20, hit.Frame, "the first strike frame");
            Assert.AreEqual(88f, f.Ronin.health);

            f.Run(30);
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count(), "a target is hit at most once");
            Assert.IsFalse(h.Alive, "telegraph + strike frames are over");
            Assert.AreEqual(0, f.Ctx.Hazards.Count);
        }

        [Test]
        public void AFighterWhoLeftTheAreaDuringTheTelegraphIsNotHit()
        {
            // walks away (1.3 m/s) for the whole telegraph: well outside a 1 m wide box by the strike
            var f = Duel(r => r.Add(60, fwd: -1).Add(600));
            Hazard.Spawn(f.Ctx, f.Kage, Shock(telegraph: 60, strike: 5), new Vector2(0.6f, 0f));

            f.Run(80);

            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count());
            Assert.AreEqual(100f, f.Ronin.health);
            Assert.AreEqual(0, f.Ctx.Hazards.Count);
        }

        [Test]
        public void AHazardDoesNotHitItsOwnerOrAnyoneOutsideTheBoxHeight()
        {
            var f = Duel();
            // centred on Kage and the Ronin both stand in it, but only the Ronin is a target
            Hazard.Spawn(f.Ctx, f.Kage, Shock(telegraph: 2, strike: 3, width: 4f), new Vector2(0.1f, 0f));
            f.Run(10);
            Assert.AreEqual(100f, f.Kage.health, "never the owner");
            Assert.AreEqual(88f, f.Ronin.health);

            // a box that ends below the fighter's head and sits well above the floor can't reach him
            var f2 = Duel();
            var high = Shock(telegraph: 2, strike: 3, width: 4f);
            high.size = new Vector2(4f, 0.1f);
            Hazard.Spawn(f2.Ctx, f2.Kage, high, new Vector2(0.6f, 3.5f));
            f2.Run(10);
            Assert.AreEqual(100f, f2.Ronin.health, "the box is in the air above him");
        }

        [Test]
        public void AHazardKnocksTargetsAwayFromItsCentre()
        {
            var f = Duel();
            Hazard.Spawn(f.Ctx, f.Kage, Shock(telegraph: 2, strike: 3, width: 3f), new Vector2(0.1f, 0f));
            f.Run(8);
            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.Greater(hit.Knockback.x, 0f, "the Ronin stands right of the centre, so he goes right");
        }

        [Test]
        public void AHazardHitsEachOfSeveralTargetsOnce()
        {
            var f = Duel();
            var probe = HurtProbe.Place(f.Root.transform, new Vector2(0.9f, 0.5f), 0.3f);
            Hazard.Spawn(f.Ctx, f.Kage, Shock(telegraph: 2, strike: 6, width: 2f), new Vector2(0.7f, 0f));
            f.Run(15);
            Assert.AreEqual(1, probe.Hits.Count);
            Assert.AreEqual("testShock", probe.Hits[0].moveId);
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin), "once per fighter");
        }

        [Test]
        public void AHazardTelegraphsAndStrikesThroughItsCues()
        {
            var seen = new List<(string id, Vector2 pos, CueArgs args)>();
            void Rec(string id, Vector2 pos, in CueArgs a) => seen.Add((id, pos, a));
            EffectCues.Played += Rec;
            try
            {
                var f = Duel();
                var def = Shock(telegraph: 6, strike: 2);
                def.telegraphCue = "VFX_Test_Telegraph";
                def.strikeCue = "VFX_Test_Strike";
                Hazard.Spawn(f.Ctx, f.Kage, def, new Vector2(0.6f, 0f));

                Assert.AreEqual(new[] { "VFX_Test_Telegraph" }, seen.Select(s => s.id).ToArray(), "the telegraph shows at once");
                Assert.AreEqual(def.size, seen[0].args.size);
                Assert.AreEqual(6 / 60f, seen[0].args.seconds, 1e-5f);

                f.Run(6);
                Assert.AreEqual(1, seen.Count, "no strike cue yet");
                f.Run(1);
                Assert.AreEqual(new[] { "VFX_Test_Telegraph", "VFX_Test_Strike" }, seen.Select(s => s.id).ToArray());
                Assert.AreEqual(0.6f, seen[1].pos.x, 1e-4f);
            }
            finally { EffectCues.Played -= Rec; }
        }
    }
}
