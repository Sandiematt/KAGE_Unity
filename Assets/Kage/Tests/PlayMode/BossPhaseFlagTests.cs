using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The character-wide flags a boss carries (SAN-102, ADR-0004 SAN-80 notes, ADR-0005): <see cref="CharacterFlags.Armour"/> makes light
    /// hits land their damage without a flinch (the same check as a move's armour frames, <c>Fighter.Armour.cs</c>), and
    /// <see cref="CharacterFlags.NoGrab"/> makes every throw fail, the shoulder throw and the fists family's heavy grapple alike.
    /// </summary>
    public class BossPhaseFlagTests
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

        HeadlessFight Duel(CharacterFlags foeFlags, float foeX = 0.37f, CharacterDef kage = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, foeX, foeDef: Footman(foeFlags), kageDef: kage);
            f.RoninInput.Add(1200);
            f.Ronin.maxHealth = f.Ronin.health = 1e5f;      // long enough that nothing here kills it
            _fights.Add(f);
            return f;
        }

        static HitInfo Incoming(Fighter attacker, bool heavy, int dmg = 6) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(3f, 0f), stun = 16, heavy = heavy },
            dmg = dmg, kb = new Vector2(-3f, 0f), zone = HitZone.Body, contact = new Vector2(0.3f, 1f), moveId = "test.hit", window = -1,
        };

        // ---- Armour ----

        [Test]
        public void A_light_hit_lands_its_damage_but_does_not_flinch_an_armoured_character()
        {
            var f = Duel(CharacterFlags.Armour);
            f.Run(4);
            float before = f.Ronin.health;
            var res = f.Ronin.ReceiveHit(Incoming(f.Kage, heavy: false));
            Assert.AreEqual(HitResult.Hit, res);
            Assert.AreEqual(before - 6f, f.Ronin.health, "the damage lands");
            Assert.AreEqual(FState.Idle, f.Ronin.state, "no flinch");
            Assert.Less(Mathf.Abs(f.Ronin.rb.linearVelocity.x), 0.5f, "and no knockback");
        }

        [Test]
        public void A_heavy_hit_still_flinches_an_armoured_character()
        {
            var f = Duel(CharacterFlags.Armour);
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, heavy: true, dmg: 9));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        [Test]
        public void A_light_hit_flinches_a_character_without_the_flag()
        {
            var f = Duel(CharacterFlags.None);
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, heavy: false));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        [Test]
        public void An_armoured_character_that_is_hit_to_death_still_goes_down()
        {
            var f = Duel(CharacterFlags.Armour);
            f.Run(4);
            f.Ronin.maxHealth = f.Ronin.health = 5f;
            f.Ronin.ReceiveHit(Incoming(f.Kage, heavy: false));
            Assert.AreEqual(FState.KO, f.Ronin.state, "armour never saves the last hit");
        }

        [Test]
        public void Removing_the_flag_at_run_time_makes_light_hits_flinch_again()
        {
            var f = Duel(CharacterFlags.Armour);
            f.Run(4);
            f.Ronin.Flags &= ~CharacterFlags.Armour;        // what a boss phase does through Fighter.Flags
            f.Ronin.ReceiveHit(Incoming(f.Kage, heavy: false));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        // ---- NoGrab ----

        static int Thrown(HeadlessFight f) => f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin && e.MoveId != null && e.MoveId.Contains("hrow"));

        [Test]
        public void The_shoulder_throw_grabs_a_character_without_the_flag()
        {
            var f = Duel(CharacterFlags.None);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");
            f.RunUntil(() => f.Ronin.state == FState.Thrown, 60);
            Assert.AreEqual(FState.Thrown, f.Ronin.state, "control: the throw works on an ordinary foe");
        }

        [Test]
        public void The_shoulder_throw_fails_on_a_no_grab_character()
        {
            var f = Duel(CharacterFlags.NoGrab);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");
            bool everThrown = false;
            for (int i = 0; i < 90; i++) { f.Run(1); everThrown |= f.Ronin.state == FState.Thrown; }
            Assert.IsFalse(everThrown, "never held");
            Assert.AreEqual(0, Thrown(f), "and no throw damage");
            Assert.IsFalse(f.Kage.Busy, "the whiff played out and Kage is free again");
        }

        [Test]
        public void The_heavy_grapple_fails_on_a_no_grab_character()
        {
            var f = Duel(CharacterFlags.NoGrab, kage: SpearFamilyFightTests.KageWith("fists"));
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            bool everThrown = false, grappleStarted = false;
            for (int i = 0; i < 120; i++)
            {
                f.Run(1);
                everThrown |= f.Ronin.state == FState.Thrown;
                grappleStarted |= f.Kage.move != null && f.Kage.move.id.StartsWith("fists.grapple");
            }
            Assert.IsTrue(grappleStarted, "Kage did try the grapple");
            Assert.IsFalse(everThrown, "but the no-grab foe is never held");
            Assert.AreEqual(f.Ronin.maxHealth, f.Ronin.health, "and takes nothing from it");
        }

        [Test]
        public void The_heavy_grapple_grabs_a_character_without_the_flag()
        {
            var f = Duel(CharacterFlags.None, kage: SpearFamilyFightTests.KageWith("fists"));
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            f.RunUntil(() => f.Ronin.state == FState.Thrown, 80);
            Assert.AreEqual(FState.Thrown, f.Ronin.state, "control: the grapple works on an ordinary foe");
        }

        [Test]
        public void NoGrab_is_read_through_Fighter_Has_so_a_phase_can_change_it()
        {
            var f = Duel(CharacterFlags.None);
            f.Ronin.Flags |= CharacterFlags.NoGrab;
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");
            bool everThrown = false;
            for (int i = 0; i < 90; i++) { f.Run(1); everThrown |= f.Ronin.state == FState.Thrown; }
            Assert.IsFalse(everThrown);
        }
    }
}
