using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The twin blades family (SAN-77, ADR-0006): Tsubame picks it, both blades are drawn in a dual grip, every
    /// button has a move on the ground and in the air, the set is faster and lighter than the katana set, every
    /// move lands on the training bag through the hurtbox path, and the weapon's level scales it like any weapon.
    /// </summary>
    public class TwinBladesTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef Tsubame(int level = 1, float moveRate = 1f)
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.weaponId = "tsubame";
            d.weaponLevel = level;
            d.moveRate = moveRate;
            return d;
        }

        HeadlessFight Alone(CharacterDef kage = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: kage ?? Tsubame());
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel(CharacterDef kage = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, 0f, kageDef: kage ?? Tsubame());
            f.RoninInput.Add(1200);
            _fights.Add(f);
            return f;
        }

        void Done(HeadlessFight f)
        {
            f.Dispose();
            _fights.Remove(f);
        }

        // ---- the family ----

        [Test]
        public void Tsubame_fights_with_the_twin_family_in_a_dual_grip()
        {
            var f = Alone();
            Assert.AreEqual("tsubame", f.Kage.WeaponId);
            Assert.AreEqual("twin", f.Kage.Family.id);
            Assert.AreEqual("tsubame", f.Kage.Family.referenceWeapon);
            Assert.AreEqual(GripPose.Dual, f.Kage.Family.grip);
            Assert.AreEqual(Fighter.ThousandShadows, f.Kage.Family.rageArt);
            Assert.Greater(f.Kage.Family.bladeLength, 0.3f);
            Assert.Less(f.Kage.Family.bladeLength, KatanaMoves.BladeLength, "short blades");
            Assert.IsInstanceOf<TwinMoves>(f.Kage.moves);
        }

        [Test]
        public void Every_twin_move_has_a_twin_id_except_the_shared_body_moves()
        {
            var f = Duel();
            var katana = (KatanaMoves)f.Ronin.moves;
            var twin = f.Kage.moves;
            var shared = new HashSet<MoveDef> { twin.roll, twin.backflip, twin.getUp, twin.getUpFront, twin.throwGrab, twin.throwWhiff };
            foreach (var m in twin.All)
                if (!shared.Contains(m)) StringAssert.StartsWith("twin.", m.id);
            Assert.AreSame(katana.roll, twin.roll, "body moves are shared by reference, never copied");
            Assert.AreSame(katana.backflip, twin.backflip);
            Assert.AreSame(katana.getUp, twin.getUp);
            Assert.AreSame(katana.getUpFront, twin.getUpFront);
            Assert.AreSame(katana.throwGrab, twin.throwGrab);
            Assert.AreSame(katana.throwWhiff, twin.throwWhiff);
            Assert.IsNull(twin.counterSlash, "Mirror Guard is the katana's");
        }

        [Test]
        public void Twin_move_ids_are_unique()
        {
            var ids = Alone().Kage.moves.All.Select(m => m.id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        // ---- every button, on the ground and in the air ----

        [TestCase(Btn.P, 0, 0, false, "twin.jab")]
        [TestCase(Btn.P, 0, -1, false, "twin.crouchJab")]
        [TestCase(Btn.P, 1, 0, false, "twin.lunge")]
        [TestCase(Btn.K, 0, 0, false, "twin.kick1")]
        [TestCase(Btn.K, 0, -1, false, "twin.sweep")]
        [TestCase(Btn.K, 1, 0, false, "twin.pushKick")]
        [TestCase(Btn.K, -1, 0, false, "twin.backKick")]
        [TestCase(Btn.S, 0, 0, false, "twin.fang1")]
        [TestCase(Btn.S, 1, 0, false, "twin.dashThrough")]
        [TestCase(Btn.S, -1, 0, false, "twin.crossSlash")]
        [TestCase(Btn.S, 0, -1, false, "twin.lowSlice")]
        [TestCase(Btn.P, 0, 0, true, "twin.airJab")]
        [TestCase(Btn.K, 0, 0, true, "twin.airKick")]
        [TestCase(Btn.S, 0, 0, true, "twin.airSpin")]
        [TestCase(Btn.S, 0, -1, true, "twin.dive")]
        public void The_button_and_direction_pick_the_move(Btn b, int fwd, int dy, bool air, string expected)
        {
            var f = Alone();
            var m = f.Kage.moves.Select(b, fwd, dy, air, f.Kage);
            Assert.IsNotNull(m, $"{b} fwd {fwd} dy {dy} air {air}");
            Assert.AreEqual(expected, m.id);
        }

        [Test]
        public void Plain_Special_does_nothing_for_now()
        {
            var f = Alone();
            Assert.IsNull(f.Kage.moves.Select(Btn.X, 0, 0, false, f.Kage));
            Assert.IsNull(f.Kage.moves.Select(Btn.X, 0, 0, true, f.Kage));
        }

        [Test]
        public void The_four_hit_string_flows_on_the_Weapon_button()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "S").Add(8).Add(1, buttons: "S").Add(8).Add(1, buttons: "S").Add(8).Add(1, buttons: "S").Add(60);
            f.Run(160);
            var started = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.AreEqual(new[] { "twin.fang1", "twin.fang2", "twin.fang3", "twin.fang4" }, started);
        }

        [Test]
        public void Punch_and_kick_chains_go_on_into_the_blades()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(7).Add(1, buttons: "S").Add(60);
            f.Run(120);
            var started = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.AreEqual(new[] { "twin.jab", "twin.hook", "twin.fang3" }, started);
        }

        // ---- faster and lighter than the katana set ----

        static (int total, float damagePerHit)? Stats(MoveDef m)
        {
            if (m == null || m.hits.Count == 0) return null;
            return (m.total, (float)m.hits.Average(w => w.dmg));
        }

        [Test]
        public void The_twin_set_is_faster_and_lighter_than_the_katana_set_on_every_ground_button()
        {
            var tf = Alone();
            var kf = Alone(CharacterCatalog.Get(KageCharacter.Id));
            var contexts = new[]
            {
                (Btn.P, 0, 0), (Btn.P, 0, -1), (Btn.K, 0, 0), (Btn.K, 0, -1), (Btn.K, 1, 0), (Btn.K, -1, 0),
                (Btn.S, 0, 0), (Btn.S, 0, -1), (Btn.S, 1, 0),
            };
            foreach (var (b, fwd, dy) in contexts)
            {
                var t = Stats(tf.Kage.moves.Select(b, fwd, dy, false, tf.Kage));
                var k = Stats(kf.Kage.moves.Select(b, fwd, dy, false, kf.Kage));
                Assert.IsTrue(t.HasValue && k.HasValue, $"{b} {fwd} {dy} both attack");
                Assert.Less(t.Value.total, k.Value.total, $"{b} fwd {fwd} dy {dy}: total frames");
                Assert.Less(t.Value.damagePerHit, k.Value.damagePerHit, $"{b} fwd {fwd} dy {dy}: damage per hit");
            }
        }

        [Test]
        public void The_string_hits_four_times_in_the_time_the_katana_string_hits_three_times_for_less_damage_per_hit()
        {
            var tf = Alone();
            var kf = Alone(CharacterCatalog.Get(KageCharacter.Id));
            var twin = ((TwinMoves)tf.Kage.moves);
            var katana = (KatanaMoves)kf.Kage.moves;
            var string4 = new[] { twin.fang1, twin.fang2, twin.fang3, twin.fang4 };
            var string3 = new[] { katana.iai, katana.risingMoon, katana.cleave };
            int twinFrames = string4.Sum(m => m.total), katanaFrames = string3.Sum(m => m.total);
            Assert.Less(twinFrames, katanaFrames, "the whole four-hit string ends sooner than the three-hit katana string");
            Assert.Less((float)twinFrames / string4.Length, 0.6f * katanaFrames / string3.Length, "clearly quicker per hit");
            float twinDmg = string4.Sum(m => m.hits.Sum(w => w.dmg)), katanaDmg = string3.Sum(m => m.hits.Sum(w => w.dmg));
            Assert.Less(twinDmg / string4.Sum(m => m.hits.Count), 0.6f * katanaDmg / string3.Sum(m => m.hits.Count), "lighter per hit");
        }

        [Test]
        public void Recovery_after_the_last_hit_is_shorter_than_the_katanas()
        {
            var tf = Alone();
            var kf = Alone(CharacterCatalog.Get(KageCharacter.Id));
            int Recovery(MoveDef m) => m.total - m.hits.Max(w => w.to);
            var twin = (TwinMoves)tf.Kage.moves;
            var katana = (KatanaMoves)kf.Kage.moves;
            Assert.Less(Recovery(twin.fang1), Recovery(katana.iai));
            Assert.Less(Recovery(twin.crossSlash), Recovery(katana.risingMoon));
            Assert.Less(Recovery(twin.dashThrough), Recovery(katana.shadowStep));
        }

        // ---- the drawing: both blades, dual grip ----

        static IEnumerable<MeshRenderer> Named(Fighter k, string prefix) => k.rig.Renderers.Where(r => r.name.StartsWith(prefix));

        [Test]
        public void Both_blades_are_drawn_one_on_each_hand()
        {
            var f = Alone();
            var far = Named(f.Kage, "Twin_FarBlade").ToList();
            var near = Named(f.Kage, "Twin_NearBlade").ToList();
            Assert.IsNotEmpty(far, "a blade on the Sword bone (far hand)");
            Assert.IsNotEmpty(near, "a blade on HandN (second blade)");
            Assert.IsTrue(far.All(r => r.enabled && r.transform.parent == f.Kage.rig.Bone(B.Sword)));
            Assert.IsTrue(near.All(r => r.enabled && r.transform.parent == f.Kage.rig.Bone(B.HandN)));
            Assert.IsTrue(f.Kage.rig.Renderers.Where(r => r.name == "Blade" || r.name == "Tsuba").All(r => !r.enabled), "Kage's katana is hidden");
        }

        [Test]
        public void Switching_back_to_the_katana_removes_the_twin_blades_and_shows_the_katana()
        {
            var f = Alone();
            f.Kage.EquipWeapon("kagetsu");
            Assert.IsEmpty(Named(f.Kage, "Twin_").Where(r => r != null && r.enabled));
            Assert.IsTrue(f.Kage.rig.Renderers.Where(r => r.name == "Blade").All(r => r.enabled));
            f.Kage.EquipWeapon("tsubame");
            int parts = Named(f.Kage, "Twin_").Count();
            f.Kage.EquipWeapon("tsubame");
            Assert.AreEqual(parts, Named(f.Kage, "Twin_").Count(), "equipping twice never adds parts twice");
            Assert.IsTrue(Named(f.Kage, "Twin_").All(r => r.enabled));
        }

        [Test]
        public void The_dual_grip_keeps_the_hands_apart_with_a_blade_in_each()
        {
            var f = Alone();
            f.Run(30);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.Greater(TwoHandedGripTests.NearFistToHandle(f.Kage), 0.1f, "the near hand is not on the far hand's handle");
            Vector2 farHand = f.Kage.rig.World(B.HandF, Vector2.zero), nearHand = f.Kage.rig.World(B.HandN, Vector2.zero);
            Assert.Greater(Vector2.Distance(farHand, nearHand), 0.1f);
        }

        [Test]
        public void The_dual_grip_holds_through_walking()
        {
            var f = Alone();
            f.KageInput.Add(40, fwd: 1);
            int walked = 0;
            for (int i = 0; i < 40; i++)
            {
                f.Run(1);
                if (i > 6 && f.Kage.state == FState.Walk)      // the sprint trails the blades in swinging hands, on purpose
                {
                    walked++;
                    Assert.Greater(Vector2.Distance(f.Kage.rig.World(B.HandF, Vector2.zero), f.Kage.rig.World(B.HandN, Vector2.zero)), 0.1f, $"walk frame {i}");
                }
            }
            Assert.Greater(walked, 3, "walked long enough to judge");
        }

        // ---- every move lands on the training bag through the hurtbox path ----

        /// <summary>Starts <paramref name="id"/> (in the air for an airborne move) and reports whether it hit the bag, trying a few distances.</summary>
        bool LandsOnBag(string id, out string detail)
        {
            var xs = new[] { 0.95f, 0.7f, 1.2f, 0.45f, 0.2f };
            var waits = new[] { 6, 12, 18, 24 };
            detail = "";
            bool airborne;
            using (var probe = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Tsubame()))
                airborne = probe.Kage.moves.All.Single(m => m.id == id).airborne;
            // the dev arena's bag hangs high (head height): low cuts and kicks only reach the same bag hung a little lower
            foreach (var anchor in new[] { HeadlessFight.BagAnchor, new Vector2(HeadlessFight.BagAnchor.x, 1.65f) })
                foreach (float x in xs)
                    foreach (int wait in airborne ? waits : new[] { 4 })
                    {
                        using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag, x, kageDef: Tsubame(), bagAnchor: anchor);
                        if (airborne) f.KageInput.Add(wait, fwd: 1, dy: 1);
                        f.Run(wait);
                        f.Kage.StartMove(f.Kage.moves.All.Single(m => m.id == id));
                        f.Run(100);
                        if (f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.TargetId == "bag" && e.MoveId == id)) return true;
                        detail = $"started at x {x}" + (airborne ? $" after {wait} jump frames" : "");
                    }
            return false;
        }

        [Test]
        public void Every_attack_of_the_set_lands_on_the_training_bag()
        {
            string[] ids;
            using (var probe = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Tsubame()))
                ids = probe.Kage.moves.All.Where(m => m.hits.Count > 0 && !m.isThrow).Select(m => m.id).ToArray();
            Assert.GreaterOrEqual(ids.Length, 20, "a full set");
            var missed = new List<string>();
            foreach (var id in ids)
                if (!LandsOnBag(id, out var detail)) missed.Add($"{id} ({detail})");
            Assert.IsEmpty(missed, "moves that never hit the bag: " + string.Join(", ", missed));
        }

        [Test]
        public void Every_blade_window_can_clash_and_blade_moves_use_blade_points()
        {
            var set = Alone().Kage.moves;
            foreach (var m in set.All)
                foreach (var w in m.hits)
                {
                    bool blade = w.points.Contains(HitPoint.Blade) || w.points.Contains(HitPoint.BladeN);
                    Assert.AreEqual(blade, w.sword, $"{m.id}: a window with blade points is a sword window (clash-eligible)");
                }
            foreach (var id in new[] { "fang1", "fang2", "fang3", "fang4", "crossSlash", "dashThrough", "lowSlice", "airSpin", "dive", "groundStab" })
            {
                var m = set.All.Single(x => x.id == "twin." + id);
                Assert.IsTrue(m.hits.All(w => w.points.Contains(HitPoint.Blade)), $"{m.id} hits with the blade line, so it can clash");
            }
        }

        [Test]
        public void The_dash_through_carries_Kage_past_the_foe_and_hits_it()
        {
            var f = Duel();
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "S");
            float start = f.Kage.Pos.x;
            f.Run(80);
            Assert.Greater(f.Kage.Pos.x - start, 1.2f, "a long pass");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == "twin.dashThrough"));
        }

        // ---- weapon level scales it like any weapon ----

        CombatEvent Fang1Hit(CharacterDef kage)
        {
            var f = Duel(kage);
            f.KageInput.Add(1, buttons: "S");
            f.Run(60);
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == "twin.fang1").ToList();
            Assert.IsNotEmpty(hits, "twin.fang1 lands");
            var e = hits[0];
            Done(f);
            return e;
        }

        int FramesInMove(CharacterDef kage, string buttons, string move)
        {
            var f = Alone(kage);
            f.KageInput.Add(1, buttons: buttons);
            int frames = 0;
            for (int i = 0; i < 200; i++)
            {
                f.Run(1);
                if (f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == move) frames++;
                else if (frames > 0) break;
            }
            Done(f);
            return frames;
        }

        [Test]
        public void Tsubame_level_8_hits_harder_than_level_1_by_the_JS_amount()
        {
            var l1 = Fang1Hit(Tsubame(1));
            Assert.AreEqual(l1.Crit ? 4f : 3f, l1.Damage, "fang1: 3, crit 4.5 -> 4");
            var l8 = Fang1Hit(Tsubame(8));
            Assert.AreEqual(l8.Crit ? 7f : 5f, l8.Damage, "fang1 x1.56: 4.68 -> 5, crit 7.02 -> 7");
        }

        [Test]
        public void Tsubame_level_8_plays_the_set_faster()
        {
            int authored = FramesInMove(Tsubame(1), "S", "twin.fang1");
            Assert.AreEqual(14, authored, "14 authored frames at rate 1");
            Assert.AreEqual(12, FramesInMove(Tsubame(8), "S", "twin.fang1"), "14 frames at x1.175");
        }

        [Test]
        public void Tsubame_level_8_reaches_further_with_both_blades()
        {
            var f1 = Alone(Tsubame(1));
            float l1 = f1.Kage.BladeReach;
            Assert.AreEqual(f1.Kage.Family.bladeLength, l1, 1e-3f);
            Done(f1);
            var f8 = Alone(Tsubame(8));
            Assert.AreEqual(1.105f, f8.Kage.BladeReach / l1, 1e-3f);
        }

        [Test]
        public void The_character_move_rate_slows_the_twin_set_too()
        {
            Assert.AreEqual(28, FramesInMove(Tsubame(1, 0.5f), "S", "twin.fang1"), "14 frames at half rate");
        }

        [Test]
        public void Foe_twin_weapons_use_the_same_set()
        {
            foreach (var weapon in new[] { "shadow_daggers", "mist_fans" })
            {
                var d = CharacterCatalog.Get(KageCharacter.Id);
                d.weaponId = weapon;
                var f = Alone(d);
                Assert.AreEqual("twin", f.Kage.Family.id, weapon);
                Assert.IsInstanceOf<TwinMoves>(f.Kage.moves, weapon);
                Done(f);
            }
        }
    }

    /// <summary>Kage with Tsubame from the keyboard and the gamepad in the dev arena (F8 picks the weapon).</summary>
    public class TwinBladesInputTests : InputTestFixture
    {
        GameObject _game;

        public override void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
            base.TearDown();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator F8_picks_Tsubame_and_the_full_set_plays_from_keyboard_and_gamepad()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20 && game.player.WeaponId != "tsubame"; i++)
            {
                Press(kb.f8Key);
                yield return null;
                Release(kb.f8Key);
                yield return null;
            }
            Assert.AreEqual("tsubame", game.player.WeaponId, "the F8 cycle reaches Tsubame");
            Assert.AreEqual("twin", game.player.Family.id);
            for (int i = 0; i < 600 && game.player.state != FState.Idle; i++) yield return new WaitForFixedUpdate();   // the fight's intro
            Assert.AreEqual(FState.Idle, game.player.state, "ready to fight");

            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.L));      // keyboard Weapon
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Frames(4);
            Assert.AreEqual(FState.Attack, game.player.state);
            Assert.AreEqual("twin.fang1", game.player.move.id);
            yield return Frames(40);

            InputSystem.QueueStateEvent(gp, new GamepadState(GamepadButton.North));                   // gamepad Weapon (Y / triangle)
            yield return null;
            InputSystem.QueueStateEvent(gp, new GamepadState());
            yield return Frames(4);
            Assert.AreEqual("twin.fang1", game.player.move.id);
            yield return Frames(40);

            InputSystem.QueueStateEvent(gp, new GamepadState(GamepadButton.West));                    // gamepad Punch
            yield return null;
            InputSystem.QueueStateEvent(gp, new GamepadState());
            yield return Frames(4);
            Assert.AreEqual("twin.jab", game.player.move.id);
            yield return Frames(40);

            InputSystem.QueueStateEvent(gp, new GamepadState(GamepadButton.South));                   // gamepad Kick
            yield return null;
            InputSystem.QueueStateEvent(gp, new GamepadState());
            yield return Frames(4);
            Assert.AreEqual("twin.kick1", game.player.move.id);
        }
    }
}
