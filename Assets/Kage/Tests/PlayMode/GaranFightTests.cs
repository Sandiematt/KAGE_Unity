using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Garan in a fight (SAN-104), through the fight harness: the clinch (armoured, takes a guard on contact and throws; a backflip or a roll
    /// avoids it; his AI clinches only a guard), Storm Fist (the shock area is a hazard: a ground mark through the wind-up, then a strike
    /// with the slam; a jump clears it; the slam crushes a guard), Tsuppari (guarding through the palms breaks posture; a parried first palm
    /// ends it), the charge (only in phase two; a crush on contact and a skid; dodged, he meets the wall and is staggered) and his two
    /// phases. Garan is scripted to press his ability slots; Kage is scripted. Only events and public state are read.
    /// </summary>
    public class GaranFightTests
    {
        const int Seed = 104;
        const int StormFistSlot = 1, TsuppariSlot = 2, ChargeSlot = 3, ClinchSlot = 4;
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef GaranDef(bool armourFlag = true)
        {
            var d = CharacterCatalog.Get(Garan.Id);
            if (!armourFlag) d.flags &= ~CharacterFlags.Armour;
            return d;
        }

        HeadlessFight Fight(Foe foe = Foe.Scripted, float kageX = -0.4f, float garanX = 0.45f, CharacterDef def = null)
        {
            var f = new HeadlessFight(Seed, foe, kageX, garanX, foeDef: def ?? GaranDef());
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            _fights.Add(f);
            return f;
        }

        static bool In(Fighter g, string moveId) => g.state == FState.Attack && g.move != null && g.move.id == moveId;

        /// <summary>Garan presses <paramref name="slot"/> on frame 2, then stands.</summary>
        static void Press(HeadlessFight f, int slot) => f.RoninInput.Add(2).Add(1, ability: slot).Add(5000);

        /// <summary>Opens phase two (half health) so the charge is enabled.</summary>
        static void PhaseTwo(HeadlessFight f)
        {
            f.Ronin.health = f.Ronin.maxHealth * 0.5f;
        }

        IEnumerable<CombatEvent> From(HeadlessFight f, CombatEventType t, string moveId) => f.Events(t).Where(e => e.Source == f.Ronin && e.MoveId == moveId);

        static HitInfo LightHit(Fighter attacker) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = 4, level = HitLevel.High, kb = new Vector2(1f, 0f), stun = 14 },
            dmg = 4, kb = new Vector2(1f, 0f), zone = HitZone.Body, contact = Vector2.up, moveId = "test.jab", window = -1,
        };

        // ================================================================ the clinch

        [Test]
        public void The_clinch_takes_a_guarding_Kage_on_contact_and_throws_him()
        {
            var f = Fight();
            Press(f, ClinchSlot);
            f.KageInput.Add(5000, buttons: "G");
            bool thrown = false;
            f.RunUntil(() => thrown |= f.Kage.state == FState.Thrown, 30);
            Assert.IsTrue(thrown, "taken in the grab window");
            Assert.IsTrue(In(f.Ronin, "garan.clinch"), "carried, not the whiff");
            f.Run(80);
            var hit = From(f, CombatEventType.Hit, "garan.clinch").ToList();
            Assert.AreEqual(1, hit.Count, "thrown");
            Assert.AreSame(f.Kage, hit[0].Target);
            Assert.Greater(hit[0].Damage, 0f);
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Launched ") || l.Contains(" Down ")), "into a physics launch");
            Assert.Greater(f.Kage.Pos.x, f.Ronin.Pos.x, "thrown down behind him");
        }

        [Test]
        public void The_clinch_reaches_over_a_window_and_takes_Kage_as_he_comes_into_reach()
        {
            // Kage starts just outside the reach; Garan's step in brings him into it a frame or two into the window
            var probe = Fight(kageX: -0.62f, garanX: 0.45f);
            Press(probe, ClinchSlot);
            probe.KageInput.Add(5000, buttons: "G");
            bool thrown = false;
            probe.RunUntil(() => thrown |= probe.Kage.state == FState.Thrown, 30);
            Assert.IsTrue(thrown, "taken on contact, not only on one frame");
        }

        [Test]
        public void Light_hits_during_the_clinch_do_not_flinch_him_and_he_keeps_his_hold()
        {
            var f = Fight(def: GaranDef(armourFlag: false));      // the move's own armour frames, without his heavy armour
            Press(f, ClinchSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.RunUntil(() => f.Kage.state == FState.Thrown, 30);
            Assert.AreEqual(FState.Thrown, f.Kage.state);
            for (int i = 0; i < 3; i++)
            {
                float before = f.Ronin.health;
                var res = f.Ronin.ReceiveHit(LightHit(f.Kage));
                Assert.AreEqual(HitResult.Hit, res);
                Assert.Less(f.Ronin.health, before, "the damage lands");
                Assert.IsTrue(In(f.Ronin, "garan.clinch"), "no flinch: the clinch carries on");
                Assert.AreEqual(FState.Thrown, f.Kage.state, "and Kage stays held");
                f.Run(6);
            }
            f.Run(60);
            Assert.AreEqual(1, From(f, CombatEventType.Hit, "garan.clinch").Count(), "the throw still lands");
        }

        [Test]
        public void Light_hits_do_not_flinch_his_clinch_with_his_heavy_armour_either()
        {
            var f = Fight();
            Press(f, ClinchSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.RunUntil(() => f.Kage.state == FState.Thrown, 30);
            f.Ronin.ReceiveHit(LightHit(f.Kage));
            Assert.IsTrue(In(f.Ronin, "garan.clinch"));
            Assert.AreEqual(FState.Thrown, f.Kage.state);
        }

        [Test]
        public void A_backflip_avoids_the_clinch()
        {
            var f = Fight();
            Press(f, ClinchSlot);
            f.KageInput.Add(2).Add(1, tapBack: true).Add(5000);      // the backflip as he steps in
            bool thrown = false, whiffed = false;
            f.RunUntil(() => { thrown |= f.Kage.state == FState.Thrown; whiffed |= In(f.Ronin, "garan.clinchWhiff"); return false; }, 80);
            Assert.IsFalse(thrown, "never taken");
            Assert.IsTrue(whiffed, "the arms close on nothing");
            Assert.AreEqual(0, From(f, CombatEventType.Hit, "garan.clinch").Count());
        }

        [Test]
        public void A_roll_avoids_the_clinch()
        {
            var f = Fight();
            Press(f, ClinchSlot);
            f.KageInput.Add(2).Add(1, tapFwd: true).Add(5000);       // rolling through him as he reaches
            bool thrown = false;
            f.RunUntil(() => thrown |= f.Kage.state == FState.Thrown, 80);
            Assert.IsFalse(thrown, "the roll's invulnerability: never taken");
            Assert.AreEqual(0, From(f, CombatEventType.Hit, "garan.clinch").Count());
        }

        [Test]
        public void His_AI_clinches_a_guarding_Kage_and_not_one_who_stands_open()
        {
            var open = Fight(Foe.Ai, -0.4f, 0.45f);
            open.KageInput.Add(600);
            bool clinched = false;
            open.RunUntil(() => clinched |= In(open.Ronin, "garan.clinch"), 600);
            Assert.IsFalse(clinched, "the JS clinch is for a guard (or a flurry), not an open man");

            var guard = Fight(Foe.Ai, -0.4f, 0.45f);
            guard.KageInput.Add(1200, buttons: "G");
            clinched = false;
            guard.RunUntil(() => clinched |= In(guard.Ronin, "garan.clinch") || In(guard.Ronin, "garan.clinchWhiff"), 1200);
            Assert.IsTrue(clinched, "a guard in front of him is clinched");
        }

        // ================================================================ Storm Fist

        [Test]
        public void Storm_Fist_lays_a_ground_mark_through_its_wind_up_and_the_shock_strikes_with_the_slam()
        {
            var f = Fight(kageX: -1.0f, garanX: 1.0f);          // 2 m: out of his fists' reach, inside the shock
            Press(f, StormFistSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => f.Ctx.Hazards.Count > 0, 10);
            Assert.AreEqual(1, f.Ctx.Hazards.Count, "the shock area is laid down at once");
            var h = f.Ctx.Hazards[0];
            Assert.AreEqual(GaranAbilities.ShockId, h.Def.id);
            Assert.AreEqual(HazardPhase.Telegraph, h.Phase);
            Assert.Less(h.Centre.x, f.Ronin.Pos.x, "in front of him");

            f.RunUntil(() => h == null || !h.Alive || h.Phase == HazardPhase.Strike, 80);
            int strikeFrame = f.Ctx.Clock.Frame - 1;
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Frame < strikeFrame), "the mark never hits");
            int slamFrame = f.Ronin.moveFrame;
            Assert.AreEqual(f.Ronin.move.hits[0].from, slamFrame, "it strikes on the slam's first frame");
            f.Run(30);
            var shock = f.Events(CombatEventType.Hit).Where(e => e.MoveId == GaranAbilities.ShockId).ToList();
            Assert.AreEqual(1, shock.Count, "the shock struck Kage once");
            Assert.AreSame(f.Kage, shock[0].Target);
            Assert.AreSame(f.Ronin, shock[0].Source);
            Assert.GreaterOrEqual(shock[0].Frame, strikeFrame);
            Assert.AreEqual(0, From(f, CombatEventType.Hit, "garan.stormFist").Count(), "out of the fists' reach");
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Launched ") || l.Contains(" Down ")), "the shock knocks down (JS kd)");
        }

        [Test]
        public void Kage_behind_him_or_out_of_its_length_is_not_struck_by_the_shock()
        {
            var far = Fight(kageX: -3.2f, garanX: 1.0f);         // 4.2 m: past the end of the shock
            Press(far, StormFistSlot);
            far.KageInput.Add(5000);
            far.Run(100);
            Assert.AreEqual(0, far.Events(CombatEventType.Hit).Count(e => e.MoveId == GaranAbilities.ShockId));
        }

        [Test]
        public void The_slam_crushes_a_guard()
        {
            var f = Fight(kageX: -0.45f, garanX: 0.45f);
            Press(f, StormFistSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(70);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, "garan.stormFist").Count(), "guarded");
            Assert.IsTrue(f.Events(CombatEventType.GuardBroken).Any(e => e.Target == f.Kage), "and the guard broke (JS gb 110)");
        }

        // ================================================================ Tsuppari

        [Test]
        public void Guarding_through_the_palms_breaks_posture()
        {
            var f = Fight(kageX: -0.45f, garanX: 0.45f);
            Press(f, TsuppariSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(110);
            var blocked = From(f, CombatEventType.Blocked, "garan.tsuppari").ToList();
            var broken = f.Events(CombatEventType.GuardBroken).Where(e => e.Target == f.Kage).ToList();
            Assert.GreaterOrEqual(blocked.Count, 2, "the first palms are guarded");
            Assert.AreEqual(1, broken.Count, "the palms crush the guard");
            Assert.AreEqual("garan.tsuppari", broken[0].MoveId);
            Assert.Greater(broken[0].Frame, blocked[0].Frame, "worn down palm by palm, not in one blow");
            Assert.IsTrue(f.Kage.GuardBroken || f.Events(CombatEventType.Hit).Any(e => e.Source == f.Ronin && e.MoveId == "garan.tsuppari"),
                "a palm then lands on the broken guard");
        }

        [Test]
        public void He_walks_forward_through_the_palms()
        {
            var f = Fight(kageX: -1.6f, garanX: 0.45f);
            Press(f, TsuppariSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, "garan.tsuppari"), 10);
            float x0 = f.Ronin.Pos.x;
            f.Run(80);
            Assert.Less(f.Ronin.Pos.x, x0 - 0.5f, "a walking barrage");
        }

        [Test]
        public void A_parried_first_palm_ends_the_barrage()
        {
            int at;
            using (var probe = new HeadlessFight(Seed, Foe.Scripted, -0.45f, 0.45f, foeDef: GaranDef()))
            {
                Press(probe, TsuppariSlot);
                probe.KageInput.Add(5000);
                probe.RunUntil(() => In(probe.Ronin, "garan.tsuppari") && probe.Ronin.moveFrame >= probe.Ronin.move.hits[0].from - 3, 60);
                at = probe.Frames.Count;
            }
            var f = Fight(kageX: -0.45f, garanX: 0.45f);
            Press(f, TsuppariSlot);
            f.KageInput.Add(at).Add(5000, buttons: "G");           // Guard pressed just before the first palm
            f.Run(120);
            var parried = f.Events(CombatEventType.Parried).Where(e => e.Source == f.Ronin).ToList();
            Assert.AreEqual(1, parried.Count, "the first palm is parried");
            Assert.AreEqual("garan.tsuppari", parried[0].MoveId);
            Assert.AreEqual(0, From(f, CombatEventType.Hit, "garan.tsuppari").Count() + From(f, CombatEventType.Blocked, "garan.tsuppari").Count(),
                "and the barrage is over");
        }

        // ================================================================ the charge

        [Test]
        public void The_charge_crushes_on_contact_and_he_skids_to_a_stop()
        {
            var f = Fight(kageX: -3.0f, garanX: 2.0f);
            PhaseTwo(f);
            Press(f, ChargeSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, "garan.charge"), 10);
            Assert.IsTrue(In(f.Ronin, "garan.charge"), "phase two: the charge is enabled");
            bool stopped = false;
            f.RunUntil(() => stopped |= In(f.Ronin, "garan.chargeStop"), 200);
            var hit = From(f, CombatEventType.Hit, "garan.charge").ToList();
            Assert.AreEqual(1, hit.Count, "one crushing blow");
            Assert.AreSame(f.Kage, hit[0].Target);
            Assert.IsTrue(stopped, "then the skid");
            Assert.IsFalse(f.Ronin.AtWall, "stopped well short of the wall");
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Launched ")), "Kage is knocked down");
        }

        [Test]
        public void A_guarded_charge_breaks_the_guard_and_he_stops_too()
        {
            var f = Fight(kageX: -3.0f, garanX: 2.0f);
            PhaseTwo(f);
            Press(f, ChargeSlot);
            f.KageInput.Add(5000, buttons: "G");
            bool stopped = false;
            f.RunUntil(() => stopped |= In(f.Ronin, "garan.chargeStop"), 200);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, "garan.charge").Count());
            Assert.IsTrue(f.Events(CombatEventType.GuardBroken).Any(e => e.Target == f.Kage), "JS gb 120");
            Assert.IsTrue(stopped);
        }

        [Test]
        public void Dodged_the_charge_runs_him_into_the_wall_and_he_is_staggered()
        {
            // Kage rolls through him as he comes: search the roll's timing on the run (deterministic)
            for (int roll = 40; roll <= 90; roll += 2)
            {
                using var f = new HeadlessFight(Seed, Foe.Scripted, -3.0f, 2.0f, foeDef: GaranDef());
                f.Kage.maxHealth = f.Kage.health = 1e6f;
                PhaseTwo(f);
                Press(f, ChargeSlot);
                f.KageInput.Add(roll).Add(1, tapFwd: true).Add(5000);
                bool walled = false;
                f.RunUntil(() => walled |= f.Ronin.state == FState.Hitstun && f.Ronin.AtWall, 260);
                if (From(f, CombatEventType.Hit, "garan.charge").Any() || From(f, CombatEventType.Blocked, "garan.charge").Any()) continue;
                Assert.IsTrue(walled, $"rolled through on frame {roll}: he ran on into the wall and stood staggered");
                Assert.Less(f.Ronin.Pos.x, -6f, "the left wall");
                int stunned = 0;
                while (f.Ronin.state == FState.Hitstun && stunned < 200) { f.Run(1); stunned++; }
                Assert.GreaterOrEqual(stunned, 90, "staggered about the JS 1.7 s: the opening");
                return;
            }
            Assert.Fail("no roll timing got through the charge");
        }

        // ================================================================ phases and the AI

        [Test]
        public void Phase_two_Stormbroken_opens_at_half_health_and_adds_the_charge()
        {
            var f = Fight(Foe.Ai, -1.5f, 1.5f);
            f.KageInput.Add(5000);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.IsTrue(f.Ronin.AbilityEnabled("stormFist") && f.Ronin.AbilityEnabled("garanTsuppari") && f.Ronin.AbilityEnabled("garan.clinch"));
            Assert.IsFalse(f.Ronin.AbilityEnabled("charge"), "phase one has no charge");

            f.Ronin.health = f.Ronin.maxHealth * 0.51f;
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "51% is above the JS threshold");
            f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "the JS threshold: half health");
            Assert.IsTrue(f.Ronin.AbilityEnabled("charge"), "the charge is added");
            Assert.IsTrue(f.Ronin.AbilityEnabled("stormFist") && f.Ronin.AbilityEnabled("garanTsuppari") && f.Ronin.AbilityEnabled("garan.clinch"));
            Assert.AreEqual(AiProfiles.Get("heavy").id, f.Ai.profile.id, "still the heavy profile");

            var names = f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "Thunderstruck", "Stormbroken" }, names);
        }

        [Test]
        public void A_scripted_charge_press_in_phase_one_does_nothing()
        {
            var f = Fight(kageX: -3.0f, garanX: 2.0f);
            Press(f, ChargeSlot);
            f.KageInput.Add(5000);
            f.Run(20);
            Assert.IsFalse(In(f.Ronin, "garan.charge"));
        }

        [Test]
        public void His_AI_uses_Storm_Fist_and_Tsuppari_in_their_range()
        {
            var f = Fight(Foe.Ai, -1.0f, 0.6f);
            f.KageInput.Add(3000);
            bool storm = false, palms = false;
            f.RunUntil(() => { storm |= In(f.Ronin, "garan.stormFist"); palms |= In(f.Ronin, "garan.tsuppari"); return storm && palms; }, 3000);
            Assert.IsTrue(storm, "Storm Fist");
            Assert.IsTrue(palms, "Tsuppari");
        }
    }
}
