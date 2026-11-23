using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage.Tests
{
    /// <summary>
    /// Frontal armour (SAN-98, ADR-0004 step 7, ADR-0005): a character with <see cref="CharacterFlags.FrontalArmour"/> takes a light hit
    /// from the front (damage and posture) without flinching. A hit from behind, a heavy hit, and any hit after a guard-break flinch as
    /// always, and a hit that kills is never resisted. Tested at the <see cref="IHittable.ReceiveHit"/> seam and with real moves in the
    /// fight harness, on the Order Footman carrying the flag: the rule is the flag's, not the Shieldbearer's.
    /// </summary>
    public class FrontalArmourTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef Footman(CharacterFlags flags)
        {
            var d = CharacterCatalog.Get("order_footman");
            d.flags = flags;
            return d;
        }

        /// <summary>Kage at -0.37 facing right, the foe at +0.37 facing left: Kage is in front of the foe.</summary>
        HeadlessFight Duel(CharacterFlags foeFlags = CharacterFlags.FrontalArmour)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, foeDef: Footman(foeFlags));
            f.RoninInput.Add(1200);
            f.Ronin.maxHealth = f.Ronin.health = 1e5f;      // long enough that nothing here kills it
            _fights.Add(f);
            return f;
        }

        /// <summary>Turns the foe's back on Kage (who stands on its left). A foe in a neutral state turns back by itself next frame, so call this just before the hit.</summary>
        static void TurnAway(HeadlessFight f) => f.Ronin.SetFacing(1f, true);

        /// <summary>
        /// Puts the foe in a plain 90-frame move with its back to Kage. A fighter in a move doesn't turn to face its opponent, so Kage's
        /// real blows land on its back for as long as the move lasts.
        /// </summary>
        static void FoeBusyWithBackTurned(HeadlessFight f)
        {
            f.Run(2);
            f.Ronin.StartMove(new MoveDef("test.plain", "Plain", 90).Key(10, Stance().Chest(-12f)).Key(90, Stance(), Ease.InOutQuad));
            TurnAway(f);
        }

        /// <summary>A hit that pushes the foe by <paramref name="kbX"/> (positive = to the right, away from Kage on the left).</summary>
        static HitInfo Incoming(Fighter attacker, float kbX, bool heavy = false, int dmg = 6, bool knockdown = false) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(Mathf.Abs(kbX), 0f), stun = 16, heavy = heavy, knockdown = knockdown },
            dmg = dmg, kb = new Vector2(kbX, 0f), zone = HitZone.Body, contact = new Vector2(0.3f, 1f), moveId = "test.hit", window = -1,
        };

        // ---------------------------------------------------------------- the seam

        [Test]
        public void A_light_hit_from_the_front_lands_its_damage_but_does_not_flinch()
        {
            var f = Duel();
            f.Run(4);
            float before = f.Ronin.health;
            var res = f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(HitResult.Hit, res);
            Assert.AreEqual(before - 6f, f.Ronin.health, "the damage lands");
            Assert.Greater(f.Ronin.Posture, 0f, "and so does the posture");
            Assert.AreEqual(FState.Idle, f.Ronin.state, "no flinch");
            Assert.Less(Mathf.Abs(f.Ronin.rb.linearVelocity.x), 0.5f, "and no knockback");
        }

        [Test]
        public void A_light_hit_from_the_front_flinches_a_character_without_the_flag()
        {
            var f = Duel(CharacterFlags.None);
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state, "control: the same hit flinches an ordinary foe");
        }

        [Test]
        public void A_light_hit_from_behind_flinches()
        {
            var f = Duel();
            f.Run(4);
            TurnAway(f);
            float before = f.Ronin.health;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));       // Kage is on the left, the foe faces right: the blow lands on its back
            Assert.AreEqual(before - 6f, f.Ronin.health, "the damage lands either way");
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
            Assert.Greater(f.Ronin.rb.linearVelocity.x, 0.5f, "and it is knocked along");
        }

        [Test]
        public void A_heavy_hit_from_the_front_flinches()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f, heavy: true, dmg: 9));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state, "the shield turns light cuts, not heavy blows");
        }

        [Test]
        public void A_light_hit_with_a_knockdown_but_no_heavy_flag_is_still_light_from_the_front()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f, knockdown: true));
            Assert.AreEqual(FState.Idle, f.Ronin.state, "the glossary's rule: heavy is the window's flag and nothing else");
        }

        [Test]
        public void A_hit_from_the_front_that_takes_the_last_health_still_knocks_out()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.maxHealth = f.Ronin.health = 5f;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(FState.KO, f.Ronin.state, "armour stops the flinch, never the damage");
        }

        [Test]
        public void A_hit_with_no_push_is_judged_by_where_the_attacker_stands()
        {
            var front = Duel();
            front.Run(4);
            front.Ronin.ReceiveHit(Incoming(front.Kage, 0f));
            Assert.AreEqual(FState.Idle, front.Ronin.state, "Kage is in front");
            // SAN-117: disposed before the next fight is built. Left in place, its bodies stand exactly where the next fight's do,
            // and the physics step pushes the two fights' fighters apart in whatever direction it picks: up to 0.7 m in 4 frames,
            // sometimes carrying Kage past the foe ("Kage is behind" failed once).
            front.Dispose();
            _fights.Remove(front);

            var back = Duel();
            back.Run(4);
            Assert.Less(back.Kage.Pos.x, back.Ronin.Pos.x, "Kage still stands on the foe's left");
            TurnAway(back);
            back.Ronin.ReceiveHit(Incoming(back.Kage, 0f));
            Assert.AreEqual(FState.Hitstun, back.Ronin.state, "Kage is behind");
        }

        [Test]
        public void Removing_the_flag_at_run_time_makes_front_hits_flinch_again()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.Flags &= ~CharacterFlags.FrontalArmour;       // what a boss phase does through Fighter.Flags
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        // ---------------------------------------------------------------- after a guard-break

        [Test]
        public void A_guard_break_staggers_a_frontally_armoured_fighter_and_the_next_front_hit_is_a_critical_that_flinches()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.AddPosture(f.Ronin.PostureMax, f.Kage, "test.break");
            Assert.IsTrue(f.Ronin.GuardBroken, "the shield does not stop a guard-break");
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
            f.Ronin.rb.linearVelocity = Vector2.zero;

            float before = f.Ronin.health;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.IsTrue(f.Ronin.LastHitCrit, "the guaranteed critical");
            Assert.AreEqual(before - 9f, f.Ronin.health, "6 x 1.5 = 9");
            Assert.Greater(f.Ronin.rb.linearVelocity.x, 0.5f, "and the hit knocks it along: it flinches although it came from the front");
        }

        [Test]
        public void After_the_break_is_spent_the_shield_works_again()
        {
            var f = Duel();
            f.Run(4);
            f.Ronin.AddPosture(f.Ronin.PostureMax, f.Kage, "test.break");
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));                 // spends the break
            f.RunUntil(() => f.Ronin.state == FState.Idle, 200);
            Assert.AreEqual(FState.Idle, f.Ronin.state);
            f.Ronin.rb.linearVelocity = Vector2.zero;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(FState.Idle, f.Ronin.state, "the next front hit is turned again");
        }

        // ---------------------------------------------------------------- real moves

        static int HitsOn(HeadlessFight f) => f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin);

        [Test]
        public void Kages_jab_from_the_front_hurts_the_foe_but_never_flinches_it()
        {
            var f = Duel();
            for (int i = 0; i < 4; i++) f.KageInput.Add(1, buttons: "P").Add(24);
            bool everStunned = false;
            for (int i = 0; i < 120; i++) { f.Run(1); everStunned |= f.Ronin.state == FState.Hitstun; }
            Assert.GreaterOrEqual(HitsOn(f), 2, "the jabs landed");
            Assert.Less(f.Ronin.health, f.Ronin.maxHealth, "and cost it health");
            Assert.IsFalse(everStunned, "without once putting it in hitstun");
        }

        [Test]
        public void Kages_jab_from_behind_flinches_the_foe()
        {
            var f = Duel();
            FoeBusyWithBackTurned(f);
            f.KageInput.Add(1, buttons: "P").Add(24);
            bool stunned = false;
            for (int i = 0; i < 60; i++) { f.Run(1); stunned |= f.Ronin.state == FState.Hitstun; }
            Assert.GreaterOrEqual(HitsOn(f), 1, "the jab landed");
            Assert.IsTrue(stunned, "from behind it is a flinch");
        }

        [Test]
        public void Without_the_flag_the_same_jab_flinches()
        {
            var f = Duel(CharacterFlags.None);
            f.KageInput.Add(1, buttons: "P").Add(24);
            bool stunned = false;
            for (int i = 0; i < 60; i++) { f.Run(1); stunned |= f.Ronin.state == FState.Hitstun; }
            Assert.IsTrue(stunned, "control");
        }
    }
}
