using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The heavy fists family in a fight (SAN-80, ADR-0006): Kage fighting bare-handed has a real move on every button
    /// and context, an armoured swing, a heavy grapple that grabs, carries and releases into a physics launch, and
    /// each move lands. (Armour as a mechanism is <see cref="ArmourTests"/>.)
    /// </summary>
    public class FistsFamilyFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        static CharacterDef KageFists() => SpearFamilyFightTests.KageWith("fists");

        HeadlessFight Alone(float meter = 0f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageMeter: meter, kageDef: KageFists());
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage (bare hands) and an idle foe a little over half a metre apart; the foe's script is <paramref name="foeScript"/>.</summary>
        HeadlessFight Duel(float roninX = 0.37f, CharacterDef foe = null, float meter = 0f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, roninX, meter, foeDef: foe, kageDef: KageFists());
            _fights.Add(f);
            return f;
        }

        static List<string> Started(HeadlessFight f) =>
            f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();

        static HitInfo Incoming(Fighter attacker, bool heavy, int dmg = 5) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(3f, 0f), stun = 16, heavy = heavy },
            dmg = dmg, kb = new Vector2(-3f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), moveId = "test.hit", window = -1,
        };

        // ---- the weapon picks the family; ids ----

        [Test]
        public void Kage_with_bare_hands_fights_with_the_fists_set()
        {
            var f = Alone();
            Assert.AreEqual("fists", f.Kage.Family.id);
            Assert.IsInstanceOf<FistsMoves>(f.Kage.moves);
            Assert.IsFalse(f.Kage.rig.SwordVisible, "no katana drawn");
        }

        [Test]
        public void The_new_moves_are_in_the_fists_namespace()
        {
            var f = Alone();
            var set = (FistsMoves)f.Kage.moves;
            foreach (var m in new[] { set.haymaker, set.grapple, set.grappleWhiff, set.quake, set.ironGuard, set.shadowPalm, set.airSlam })
                StringAssert.StartsWith("fists.", m.id);
        }

        // ---- the full set from the buttons ----

        [TestCase("P", 0, 0, "jab")]
        [TestCase("P", 1, 0, "lunge")]
        [TestCase("P", 0, -1, "crouchPunch")]
        [TestCase("K", 0, 0, "kick1")]
        [TestCase("K", 1, 0, "push")]
        [TestCase("K", -1, 0, "axe")]
        [TestCase("K", 0, -1, "sweep")]
        [TestCase("S", 0, 0, "fists.haymaker")]
        [TestCase("S", 1, 0, "fists.grapple")]
        [TestCase("S", 0, -1, "fists.quake")]
        [TestCase("S", -1, 0, "fists.ironGuard")]
        [TestCase("X", 0, 0, "fists.shadowPalm")]
        public void Each_ground_button_and_direction_starts_its_fists_move(string button, int fwd, int dy, string expected)
        {
            var f = Alone(meter: 60f);
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault());
        }

        [TestCase("P", "airPunch")]
        [TestCase("K", "airKick")]
        [TestCase("S", "fists.airSlam")]
        public void Each_air_button_starts_its_fists_air_move(string button, string expected)
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault(id => id.ToLower().Contains("air")));
        }

        [Test]
        public void Special_costs_Shadow_and_does_nothing_without_it()
        {
            var f = Alone(meter: 10f);
            f.Run(10);
            f.KageInput.Add(1, buttons: "X");
            f.Run(30);
            CollectionAssert.IsEmpty(Started(f), "10 Rage is not enough");

            f = Alone(meter: 60f);
            f.Run(10);
            f.KageInput.Add(1, buttons: "X");
            f.Run(5);
            CollectionAssert.AreEqual(new[] { "fists.shadowPalm" }, Started(f));
            Assert.AreEqual(60f - ((FistsMoves)f.Kage.moves).shadowPalm.meterCost, f.Kage.meter, 1f, "the cost is paid");
        }

        [Test]
        public void Against_a_close_foe_forward_and_punch_is_the_shoulder_throw_and_against_a_downed_foe_down_and_punch_the_ground_strike()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.Run(10);
            f.KageInput.Add(1, fwd: 1, buttons: "P");
            f.Run(10);
            CollectionAssert.Contains(Started(f), "throw", "the light throw (shared with the katana set)");
        }

        [Test]
        public void Strings_chain_the_punches_the_kicks_and_the_armoured_swing()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(10).Add(1, buttons: "P");
            f.Run(120);
            CollectionAssert.AreEqual(new[] { "jab", "crossFist", "uppercut" }, Started(f), "jab, rear cross, rising fist");

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "S");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "jab", "fists.haymaker" }, Started(f), "the jab opens into the armoured swing");

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "K").Add(10).Add(1, buttons: "S");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "kick1", "fists.haymaker" }, Started(f), "the roundhouse opens into it too");

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "K").Add(10).Add(1, buttons: "K").Add(11).Add(1, buttons: "K");
            f.Run(130);
            CollectionAssert.AreEqual(new[] { "kick1", "kick2", "axe" }, Started(f));
        }

        [Test]
        public void Every_move_of_the_set_can_be_reached_from_a_button_or_a_chain()
        {
            var f = Duel();
            var set = (FistsMoves)f.Kage.moves;
            var reachable = new HashSet<MoveDef>();
            foreach (var b in new[] { Btn.P, Btn.K, Btn.S, Btn.X })
                foreach (int fwd in new[] { -1, 0, 1 })
                    foreach (int dy in new[] { -1, 0 })
                        foreach (bool air in new[] { false, true })
                        {
                            var m = set.Select(b, fwd, dy, air, f.Kage);
                            if (m != null) reachable.Add(m);
                        }
            var queue = new Queue<MoveDef>(reachable);
            while (queue.Count > 0)
                foreach (var chained in queue.Dequeue().chain.Values)
                {
                    var next = set.BareHanded(chained) ?? chained;      // what Fighter.Armed plays in its place
                    if (reachable.Add(next)) queue.Enqueue(next);
                }
            var body = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwWhiff, set.grappleWhiff };    // started by the fighter
            // the ground strike needs a downed foe, the shoulder throw a close one
            var missing = set.All.Except(reachable).Except(body).Where(m => m.id != "groundPound" && m.id != "throw").ToList();
            CollectionAssert.IsEmpty(missing.Select(m => m.id));
        }

        [Test]
        public void No_move_of_the_set_uses_a_blade_so_nothing_clashes()
        {
            var f = Alone();
            foreach (var m in ((FistsMoves)f.Kage.moves).All)
            {
                Assert.IsFalse(m.swordFront, m.id);
                Assert.AreEqual(-1, m.projectileFrame, m.id);
                foreach (var w in m.hits)
                {
                    Assert.IsFalse(w.sword, m.id);
                    CollectionAssert.DoesNotContain(w.points, HitPoint.Blade, m.id);
                }
            }
        }

        // ---- the armoured swing ----

        [Test]
        public void The_armoured_swing_has_armour_frames_a_heavy_window_and_the_guard_has_armour_and_no_window()
        {
            var f = Alone();
            var set = (FistsMoves)f.Kage.moves;
            Assert.GreaterOrEqual(set.haymaker.armorFrom, 0);
            var w = set.haymaker.hits.Single();
            Assert.IsTrue(w.heavy, "its own blow is heavy");
            Assert.LessOrEqual(set.haymaker.armorFrom, w.from - 4, "armour covers the wind-up");
            Assert.GreaterOrEqual(set.haymaker.armorTo, w.to, "and the swing");
            Assert.GreaterOrEqual(set.ironGuard.armorFrom, 0);
            Assert.IsEmpty(set.ironGuard.hits);
        }

        [Test]
        public void A_light_hit_during_the_swing_does_not_stop_it_and_the_swing_lands()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.Run(4);
            f.KageInput.Add(1, buttons: "S");
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.moveFrame >= 8, 30);
            float before = f.Kage.health;
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(before - 5f, f.Kage.health, "the damage lands");
            Assert.AreEqual(FState.Attack, f.Kage.state, "no flinch in the wind-up");
            float roninBefore = f.Ronin.health;
            f.Run(60);
            Assert.Less(f.Ronin.health, roninBefore, "the swing came out and hit");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == "fists.haymaker"));
        }

        [Test]
        public void A_light_kick_from_the_foe_in_the_middle_of_the_swing_trades_instead_of_interrupting_it()
        {
            var f = Duel();
            f.RoninInput.Add(1, buttons: "K").Add(600);
            f.KageInput.Add(5).Add(1, buttons: "S");      // the kick's blow lands in the swing's wind-up
            var states = new List<FState>();
            for (int i = 0; i < 60; i++) { f.Run(1); states.Add(f.Kage.state); }
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.SourceId == "ronin" && e.MoveId == "kick1" && e.Target == f.Kage), "the kick connected: " + string.Join(" | ", f.Log.All.Select(e => e.ToLine())) + " ## " + string.Join(" | ", f.Frames.Take(14)));
            CollectionAssert.DoesNotContain(states, FState.Hitstun, "the swing took it without a flinch");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == "fists.haymaker"), "and landed");
        }

        // ---- the heavy grapple: grab, carry, release ----

        /// <summary>The victim's positions on every frame it is held, and how it ended.</summary>
        sealed class GrappleRun
        {
            public readonly List<Vector2> carry = new();
            public Vector2 kage;
            public FState afterRelease;
            public float healthLost;
            public Vector2 landed, released;
            public int grabFrame, releaseFrame;
        }

        GrappleRun RunGrapple(CharacterDef foe = null, float roninX = 0.37f)
        {
            var f = Duel(roninX, foe);
            f.RoninInput.Add(600);
            var v = f.Ronin;
            float hp = v.health;
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            f.RunUntil(() => v.state == FState.Thrown, 80);
            Assert.AreEqual(FState.Thrown, v.state, "the grapple grabbed");
            var r = new GrappleRun { grabFrame = (int)f.Ctx.Clock.Frame, kage = f.Kage.Pos };
            while (v.state == FState.Thrown && r.carry.Count < 200) { r.carry.Add(v.Pos); f.Run(1); }
            r.releaseFrame = (int)f.Ctx.Clock.Frame;
            r.afterRelease = v.state;
            r.released = r.carry[^1];
            r.healthLost = hp - v.health;
            f.RunUntil(() => v.state == FState.Down || v.state == FState.KO, 200);
            r.landed = v.Pos;
            // done with it: a fight left alive keeps its bodies (and push colliders) where the next one is built
            f.Dispose();
            _fights.Remove(f);
            return r;
        }

        [Test]
        public void The_grapple_grabs_carries_overhead_and_releases_into_a_launch()
        {
            var r = RunGrapple();
            Assert.GreaterOrEqual(r.carry.Count, 25, "the victim is held and moved for a good while");
            float top = r.carry.Max(p => p.y);
            Assert.Greater(top, 1.5f, $"lifted overhead (peak {top:0.00} m)");
            Assert.Greater(r.carry[0].x - r.kage.x, 0.4f, "taken from in front of Kage");
            Assert.Less(r.carry[^1].x - r.kage.x, -0.5f, "and carried over to his back");
            Assert.AreEqual(FState.Launched, r.afterRelease, "released into a launch");
            Assert.GreaterOrEqual(r.healthLost, 14f, "a heavy throw's damage");
            Assert.Less(r.landed.x, r.kage.x - 0.8f, "thrown clear behind Kage");
        }

        [Test]
        public void A_heavy_victim_is_thrown_less_far_by_the_same_grapple()
        {
            var light = RunGrapple();
            var heavy = RunGrapple(CharacterCatalog.Get(TestHeavy.Id));
            float lightTravel = light.released.x - light.landed.x, heavyTravel = heavy.released.x - heavy.landed.x;
            Assert.Greater(lightTravel, 0.5f, "thrown on from where it was let go");
            Assert.Less(heavyTravel, lightTravel * 0.7f, $"the release is an impulse over the victim's mass (SAN-68): heavy {heavyTravel:0.00} m, light {lightTravel:0.00} m");
        }

        [Test]
        public void The_grapple_reaches_further_than_the_shoulder_throw()
        {
            // a foe 1.1 m away is out of the shoulder throw's grip and inside the grapple's
            var r = RunGrapple(roninX: 0.75f);
            Assert.AreEqual(FState.Launched, r.afterRelease);
        }

        [Test]
        public void A_grapple_with_nobody_in_reach_whiffs_and_is_not_armoured_after_the_grab_frame()
        {
            var f = Duel(roninX: 2.2f);
            f.RoninInput.Add(600);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            f.RunUntil(() => f.Kage.move != null && f.Kage.move.id == "fists.grappleWhiff", 60);
            Assert.AreEqual("fists.grappleWhiff", f.Kage.move?.id, "it carries on as the whiff");
            f.Run(60);
            Assert.AreNotEqual(FState.Thrown, f.Ronin.state);
            Assert.AreEqual(FState.Idle, f.Kage.state);
        }

        [Test]
        public void A_light_hit_during_the_carry_does_not_drop_the_victim_but_a_heavy_one_does()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            f.RunUntil(() => f.Ronin.state == FState.Thrown, 80);
            f.Run(8);
            Assert.AreEqual(FState.Thrown, f.Ronin.state);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(FState.Attack, f.Kage.state, "armoured through the carry");
            Assert.AreEqual(FState.Thrown, f.Ronin.state, "the victim is still held");
            f.RunUntil(() => f.Ronin.state != FState.Thrown, 80);
            Assert.AreEqual(FState.Launched, f.Ronin.state, "and the throw completes");
        }

        [Test]
        public void A_heavy_hit_during_the_carry_breaks_the_hold()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            f.RunUntil(() => f.Ronin.state == FState.Thrown, 80);
            f.Run(8);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: true));
            Assert.AreNotEqual(FState.Thrown, f.Ronin.state, "the victim is let go");
            Assert.AreNotEqual(FState.Attack, f.Kage.state);
        }

        // ---- the other new moves ----

        [Test]
        public void Shadow_Palm_hurls_the_foe_away()
        {
            var f = Duel(meter: 60f);
            f.RoninInput.Add(600);
            f.KageInput.Add(4).Add(1, buttons: "X");
            f.RunUntil(() => f.Ronin.state is FState.Launched or FState.Down, 80);
            Assert.AreEqual(FState.Launched, f.Ronin.state);
            f.Run(40);
            Assert.Greater(f.Ronin.Pos.x - 0.37f, 1.2f, "sent well away");
        }

        [Test]
        public void The_quake_fist_is_a_low_hit_that_knocks_down()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.KageInput.Add(4).Add(1, dy: -1, buttons: "S");
            f.RunUntil(() => f.Events(CombatEventType.Hit).Any(e => e.MoveId == "fists.quake"), 80);
            var hit = f.Events(CombatEventType.Hit).First(e => e.MoveId == "fists.quake");
            Assert.AreEqual(HitLevel.Low, hit.Level);
            f.Run(30);
            Assert.AreEqual(FState.Down, f.Ronin.state);
        }

        [Test]
        public void The_air_slam_spikes_the_foe_into_the_ground()
        {
            var f = Duel(roninX: 0.5f);
            f.RoninInput.Add(600);
            f.KageInput.Add(12, fwd: 1, dy: 1).Add(1, buttons: "S");
            f.Run(80);
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.MoveId == "fists.airSlam"), "it lands on a foe in front of a jumping Kage");
        }

        // ---- every hitting move lands on the training bag ----

        [Test]
        public void Every_hitting_move_of_the_set_lands_on_the_training_bag()
        {
            var f = Alone();
            var set = (FistsMoves)f.Kage.moves;
            var all = set.All.Distinct().ToList();
            var notHits = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff, set.grapple, set.grappleWhiff, set.ironGuard };    // no window, or a throw (it needs a fighter)
            var report = new List<string>();
            var failed = new List<string>();
            foreach (var m in all.Except(notHits))
            {
                Assert.IsNotEmpty(m.hits, $"{m.id} has a hit window");
                if (SpearFamilyFightTests.LandsOnBag(m, "fists", out string where)) report.Add($"{m.id}: {where}");
                else failed.Add(m.id);
            }
            Debug.Log("lands on the bag:\n" + string.Join("\n", report));
            CollectionAssert.IsEmpty(failed, "moves that never hit the bag");
            Assert.IsEmpty(set.ironGuard.hits);
        }

        // ---- upgrade levels and the foe's weapon ----

        [Test]
        public void Iron_fists_fight_with_the_same_set()
        {
            var d = SpearFamilyFightTests.KageWith("iron_fists");
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: d);
            Assert.IsInstanceOf<FistsMoves>(f.Kage.moves);
        }
    }

    /// <summary>The player's buttons reach every part of the fists set from the keyboard and the gamepad input sources.</summary>
    public class FistsInputTests : InputTestFixture
    {
        [Test]
        public void Fists_buttons_work_from_the_keyboard_and_the_gamepad()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: SpearFamilyFightTests.KageWith("fists"));
            var set = (FistsMoves)f.Kage.moves;
            var k = f.Kage;

            (Key key, Btn b, MoveDef expected)[] keyboard =
            {
                (Key.J, Btn.P, set.Select(Btn.P, 0, 0, false, k)), (Key.K, Btn.K, set.Select(Btn.K, 0, 0, false, k)),
                (Key.L, Btn.S, set.haymaker), (Key.I, Btn.X, set.shadowPalm),
            };
            foreach (var (key, b, expected) in keyboard)
            {
                Press(kb[key]); src.Sample(); Release(kb[key]);
                var fi = src.Poll(k);
                Assert.IsTrue(b switch { Btn.P => fi.p, Btn.K => fi.k, Btn.S => fi.s, _ => fi.x }, $"{key} is the {b} button");
                Assert.AreSame(expected, set.Select(b, 0, 0, false, k));
            }

            Press(gp.buttonNorth); src.Sample(); Release(gp.buttonNorth);
            Assert.IsTrue(src.Poll(k).s, "Y / triangle is the weapon button");
            Assert.AreSame(set.quake, set.Select(Btn.S, 0, -1, false, k), "with the stick down");
            Assert.AreSame(set.grapple, set.Select(Btn.S, 1, 0, false, k), "with the stick forward");
            Assert.AreSame(set.ironGuard, set.Select(Btn.S, -1, 0, false, k), "with the stick back");
            Press(gp.buttonEast); src.Sample(); Release(gp.buttonEast);
            Assert.IsTrue(src.Poll(k).x, "B / circle is the special");
            Press(gp.buttonWest); src.Sample(); Release(gp.buttonWest);
            Assert.IsTrue(src.Poll(k).p, "X / square is punch");
            Press(gp.buttonSouth); src.Sample(); Release(gp.buttonSouth);
            Assert.IsTrue(src.Poll(k).k, "A / cross is kick");
        }
    }
}
