using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The heavy tachi family in a fight (SAN-81, ADR-0006): Kage wielding Raikō no Tachi has the whole move set from the
    /// buttons, a heavy two-handed grip, the longest blade of the families without out-reaching the spear, every move
    /// landing on the training bag, and the weapon fixed at level 1. Raikon's own copy keeps its JS multiplier.
    /// </summary>
    public class TachiFamilyFightTests
    {
        const string Tachi = "raiko_no_tachi";
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Alone(string weapon = Tachi, int level = 1)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: SpearFamilyFightTests.KageWith(weapon, level));
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel(string weapon = Tachi)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, 0f, kageDef: SpearFamilyFightTests.KageWith(weapon));
            f.RoninInput.Add(1200);
            _fights.Add(f);
            return f;
        }

        void Done(HeadlessFight f)
        {
            f.Dispose();
            _fights.Remove(f);
        }

        static List<string> Started(HeadlessFight f) =>
            f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();

        static readonly string[] BodyMoveIds = { "roll", "backflip", "getUp", "getUpFront", "throw", "throwWhiff" };

        // ---- the weapon picks the family ----

        [Test]
        public void Kage_equips_Raiko_no_Tachi_and_fights_with_the_tachi_set()
        {
            var f = Alone();
            Assert.AreEqual(Tachi, f.Kage.WeaponId);
            Assert.AreEqual("tachi", f.Kage.Family.id);
            Assert.IsInstanceOf<TachiMoves>(f.Kage.moves);
            Assert.AreEqual(GripPose.HeavyTwoHand, f.Kage.Family.grip);
            Assert.AreEqual(1f, f.Kage.PlaybackRate);
            Assert.AreEqual(TachiMoves.BladeLength, f.Kage.BladeReach, 1e-3f);
        }

        [Test]
        public void Every_tachi_move_has_a_tachi_id_except_the_shared_body_moves_and_the_ids_are_unique()
        {
            var f = Alone();
            var all = ((TachiMoves)f.Kage.moves).All.Distinct().ToList();
            Assert.AreEqual(all.Count, all.Select(m => m.id).Distinct().Count(), "unique ids");
            foreach (var m in all.Where(m => !BodyMoveIds.Contains(m.id))) StringAssert.StartsWith("tachi.", m.id);
        }

        [Test]
        public void Raikons_own_tachi_fights_with_the_same_set()
        {
            var f = Alone("raikon_tachi");
            Assert.IsInstanceOf<TachiMoves>(f.Kage.moves);
        }

        [Test]
        public void The_tachi_is_drawn_in_place_of_the_katana_and_the_katana_comes_back()
        {
            var f = Alone();
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0);
            Assert.IsTrue(f.Kage.rig.BaseSwordHidden);
            int parts = f.Kage.rig.FamilyPartCount;
            f.Kage.EquipWeapon(Tachi);
            Assert.AreEqual(parts, f.Kage.rig.FamilyPartCount, "attaching again never doubles the parts");
            f.Kage.EquipWeapon("kagetsu");
            Assert.AreEqual(0, f.Kage.rig.FamilyPartCount);
            Assert.IsFalse(f.Kage.rig.BaseSwordHidden);
            f.Kage.EquipWeapon("raikon_tachi");
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0);
        }

        [Test]
        public void A_broken_tachi_is_not_drawn_and_comes_back_next_fight()
        {
            var f = Alone();
            f.Kage.BreakSword();
            Assert.IsFalse(f.Kage.rig.SwordVisible);
            f.Kage.ResetForFight();
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0);
        }

        // ---- the full set from the buttons ----

        [TestCase("P", 0, 0, "tachi.pommel")]
        [TestCase("P", 1, 0, "tachi.stepPommel")]
        [TestCase("P", 0, -1, "tachi.lowPommel")]
        [TestCase("K", 0, 0, "tachi.kick")]
        [TestCase("K", 1, 0, "tachi.pushKick")]
        [TestCase("K", -1, 0, "tachi.heelDrop")]
        [TestCase("K", 0, -1, "tachi.lowKick")]
        [TestCase("S", 0, 0, "tachi.cut1")]
        [TestCase("S", 1, 0, "tachi.thrust")]
        [TestCase("S", 0, -1, "tachi.sweep")]
        [TestCase("S", -1, 0, "tachi.guard")]
        public void Each_ground_button_and_direction_starts_its_tachi_move(string button, int fwd, int dy, string expected)
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(70);
            Assert.AreEqual(expected, Started(f).FirstOrDefault());
        }

        [TestCase("P", "tachi.airHilt")]
        [TestCase("K", "tachi.airKick")]
        [TestCase("S", "tachi.airCleave")]
        public void Each_air_button_starts_its_tachi_air_move(string button, string expected)
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault(id => id.StartsWith("tachi.air")));
        }

        [Test]
        public void The_plain_special_is_not_part_of_the_set_yet_and_does_nothing()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "X");
            f.Run(30);
            CollectionAssert.IsEmpty(Started(f));
        }

        [Test]
        public void The_three_cuts_chain_on_the_Weapon_button()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "S").Add(24).Add(1, buttons: "S").Add(26).Add(1, buttons: "S");
            f.Run(240);
            CollectionAssert.AreEqual(new[] { "tachi.cut1", "tachi.cut2", "tachi.overhead" }, Started(f));
        }

        [Test]
        public void Pommel_strikes_chain_into_the_rising_hilt_and_the_cuts()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(11).Add(1, buttons: "P");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "tachi.pommel", "tachi.hiltRise" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(11).Add(1, buttons: "S");
            f.Run(120);
            CollectionAssert.AreEqual(new[] { "tachi.pommel", "tachi.cut1" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "K").Add(15).Add(1, buttons: "S");
            f.Run(120);
            CollectionAssert.AreEqual(new[] { "tachi.kick", "tachi.cut1" }, Started(f));
        }

        [Test]
        public void Every_move_of_the_set_can_be_reached_from_a_button_or_a_chain()
        {
            var f = Duel();
            var set = (TachiMoves)f.Kage.moves;
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
                foreach (var next in queue.Dequeue().chain.Values)
                    if (reachable.Add(next)) queue.Enqueue(next);
            var body = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwWhiff };
            var missing = set.All.Except(reachable).Except(body).Except(new[] { set.groundStab, set.throwGrab }).ToList();
            CollectionAssert.IsEmpty(missing.Select(m => m.id));
        }

        [Test]
        public void Against_a_downed_foe_down_and_punch_is_the_earthsplit()
        {
            var f = Duel();
            f.Run(20);
            f.KageInput.Add(1, dy: -1, buttons: "S");
            f.RunUntil(() => f.Ronin.state == FState.Down, 160);
            Assert.AreEqual(FState.Down, f.Ronin.state, "the sweep puts the Ronin on the floor");
            f.RunUntil(() => f.Kage.state != FState.Attack, 100);
            Assert.AreEqual(FState.Down, f.Ronin.state, "still down");
            f.KageInput.Add(1, dy: -1, buttons: "P");
            f.Run(30);
            CollectionAssert.Contains(Started(f), "tachi.groundStab");
        }

        [Test]
        public void The_unused_Ability_slots_are_empty_until_a_boss_adds_them()
        {
            var f = Alone();
            var set = (TachiMoves)f.Kage.moves;
            Assert.IsNull(set.Ability("raikon.thunderSwing"));
            var m = new MoveDef("raikon.thunderSwing", "Thunder Swing", 30);
            set.AddAbility("raikon.thunderSwing", m);
            Assert.AreSame(m, set.Ability("raikon.thunderSwing"));
            CollectionAssert.Contains(set.All.ToList(), m);
        }

        // ---- the heavy two-handed grip ----

        static void RearHand(Fighter k, out float along, out float off)
        {
            Vector2 pivot = k.rig.World(B.Sword, Vector2.zero);
            Vector2 axis = (k.rig.World(B.Sword, k.BladeTipLocal) - pivot).normalized;
            Vector2 d = (Vector2)k.rig.World(B.HandN, k.def.fistLocal) - pivot;
            along = Vector2.Dot(d, axis);
            off = Mathf.Abs(d.x * axis.y - d.y * axis.x);
        }

        [Test]
        public void Idle_holds_the_hilt_with_both_hands_a_hand_and_a_half_apart()
        {
            var f = Alone();
            f.Run(30);
            RearHand(f.Kage, out float along, out float off);
            Assert.Less(off, 0.05f, "the rear fist is on the hilt");
            Assert.That(along, Is.LessThan(-0.17f).And.GreaterThan(-0.45f), "wider than the katana's fist-width grip, narrower than the spear's");
        }

        [TestCase("S", 0, 0)]
        [TestCase("S", 1, 0)]
        [TestCase("S", 0, -1)]
        [TestCase("P", 0, 0)]
        public void The_rear_hand_stays_on_the_hilt_through_the_main_tachi_moves(string button, int fwd, int dy)
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(1);
            string id = f.Kage.move?.id;
            Assert.IsNotNull(id);
            var worst = 0f;
            while (f.Kage.state == FState.Attack)
            {
                RearHand(f.Kage, out _, out float off);
                worst = Mathf.Max(worst, off);
                f.Run(1);
            }
            Assert.Less(worst, 0.12f, $"{id}: the rear fist's worst distance from the hilt");
        }

        [Test]
        public void The_point_never_digs_into_the_floor_on_the_ground()
        {
            var f = Alone();
            var set = (TachiMoves)f.Kage.moves;
            foreach (var m in new[] { set.pommel, set.hiltRise, set.stepPommel, set.lowPommel, set.groundStab, set.kick, set.pushKick, set.lowKick, set.cut1, set.cut2, set.overhead, set.thrust, set.sweep, set.guard })
            {
                f.Kage.EquipWeapon(Tachi);
                f.Run(30);
                f.Kage.StartMove(m);
                float lowest = float.MaxValue;
                for (int i = 0; i < m.total + 4 && f.Kage.state == FState.Attack; i++)
                {
                    f.Run(1);
                    lowest = Mathf.Min(lowest, f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y);
                }
                Assert.Greater(lowest, -0.04f, $"{m.id}: the tip's lowest point");
            }
        }

        [Test]
        public void A_low_guard_with_the_tachi_keeps_the_point_off_the_floor()
        {
            var f = Alone();
            f.KageInput.Add(40, dy: -1, buttons: "G");     // Guard with Down: the low guard
            f.Run(40);
            Assert.Greater(f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y, -0.04f);
        }

        // ---- Raikon (SAN-71) wields the family and keeps his painted tachi ----

        [Test]
        public void Raikon_fights_with_the_tachi_family_and_keeps_his_own_painted_tachi()
        {
            var def = CharacterCatalog.Get("raikon");
            Assert.AreEqual("tachi", def.family);
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, 0f, foeDef: def);
            _fights.Add(f);
            f.RoninInput.Add(600);
            f.Run(3);
            var r = f.Ronin;
            Assert.AreEqual("raikon_tachi", r.WeaponId);
            Assert.AreEqual("tachi", r.Family.id, "no 'no registered family' fallback to the katana");
            Assert.IsInstanceOf<TachiMoves>(r.moves);
            Assert.AreEqual(0, r.rig.FamilyPartCount, "his painted Tachi_Hilt / Tachi_Blade stay; the family adds no blade of its own");
            Assert.IsTrue(r.rig.HasOwnPart("Tachi_Blade"));
            Assert.IsFalse(r.rig.BaseSwordHidden);
            Assert.AreEqual(TachiMoves.BladeLength * r.Weapon.reach, r.BladeReach, 1e-3f, "the family's blade length drives his Blade hit points");
            Debug.Log($"Raikon: hit line {r.BladeReach:0.00} m, painted blade {(r.def.bladeTipLocal - r.def.bladeBaseLocal).magnitude:0.00} m, body scale {r.Character.bodyScale:0.00}");
        }

        // ---- slow and heavy ----

        [Test]
        public void The_cuts_start_up_slower_than_the_katanas_and_hit_harder()
        {
            var tachi = (TachiMoves)Alone().Kage.moves;
            var katana = (KatanaMoves)new MoveSets().Get("katana");
            foreach (var m in new[] { tachi.cut1, tachi.cut2, tachi.overhead, tachi.thrust, tachi.sweep })
            {
                Assert.Greater(m.hits.Min(w => w.from), katana.iai.hits.Min(w => w.from), m.id + " starts up slower than the Iai Draw");
                Assert.Greater(m.hits.Max(w => w.dmg), katana.iai.hits.Max(w => w.dmg), m.id + " hits harder");
            }
        }

        // ---- reach ----

        [Test]
        public void The_tachis_total_reach_stays_below_the_spears()
        {
            float spear = SpearFamilyFightTests.Reach(WeaponFamilies.Spear, out string spearMove);
            float tachi = SpearFamilyFightTests.Reach(WeaponFamilies.Tachi, out string tachiMove);
            Debug.Log($"tachi reach {tachi:0.00} m ({tachiMove}), spear {spear:0.00} m ({spearMove})");
            Assert.Less(tachi, spear, $"the spear ({spearMove}) still has the longest total reach");
            Assert.Greater(tachi, SpearFamilyFightTests.Reach(WeaponFamilies.Cleaver, out _), "but the tachi outreaches the cleaver");
            StringAssert.StartsWith("tachi.", tachiMove);
        }

        // ---- each move lands on the training bag ----

        [Test]
        public void Every_hitting_move_of_the_set_lands_on_the_training_bag()
        {
            var f = Alone();
            var set = (TachiMoves)f.Kage.moves;
            var notHits = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff, set.guard };
            var report = new List<string>();
            var failed = new List<string>();
            foreach (var m in set.All.Distinct().Except(notHits))
            {
                Assert.IsNotEmpty(m.hits, $"{m.id} has a hit window");
                if (SpearFamilyFightTests.LandsOnBag(m, Tachi, out string where)) report.Add($"{m.id}: {where}");
                else failed.Add(m.id);
            }
            Debug.Log("lands on the bag:\n" + string.Join("\n", report));
            CollectionAssert.IsEmpty(failed, "moves that never hit the bag");
            Assert.IsEmpty(set.guard.hits, "the guard has no hit window");
        }

        [Test]
        public void The_cuts_and_stabs_are_blade_windows_and_so_can_clash_but_hilts_and_kicks_never_do()
        {
            var set = (TachiMoves)Alone().Kage.moves;
            foreach (var m in new[] { set.cut1, set.cut2, set.overhead, set.thrust, set.sweep, set.groundStab, set.airCleave })
                foreach (var w in m.hits)
                {
                    CollectionAssert.AreEqual(new[] { HitPoint.Blade }, w.points, m.id);
                    Assert.IsTrue(w.sword, m.id + " is a sword-class window (clash eligible)");
                }
            foreach (var m in new[] { set.pommel, set.hiltRise, set.stepPommel, set.lowPommel, set.airHilt, set.kick, set.pushKick, set.lowKick, set.heelDrop, set.airKick })
                foreach (var w in m.hits) CollectionAssert.DoesNotContain(w.points, HitPoint.Blade, m.id);
        }

        [Test]
        public void The_big_swings_carry_armour_frames_and_the_armour_check_honours_them()
        {
            var set = (TachiMoves)Alone().Kage.moves;
            foreach (var m in new[] { set.overhead, set.thrust, set.guard })
            {
                Assert.GreaterOrEqual(m.armorFrom, 0, m.id);
                Assert.Greater(m.armorTo, m.armorFrom, m.id);
            }
            Assert.IsTrue(set.overhead.Armoured(set.overhead.armorFrom));
            Assert.IsFalse(set.overhead.Armoured(0));
            foreach (var w in set.overhead.hits) Assert.Less(set.overhead.armorFrom, w.from, "armoured before the cut lands");
        }

        // ---- the weapon is fixed at level 1; Raikon's copy keeps its JS multiplier ----

        static CombatEvent ThrustHit(HeadlessFight f)
        {
            f.KageInput.Add(1, fwd: 1, buttons: "S");
            f.Run(90);
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == "tachi.thrust").ToList();
            Assert.IsNotEmpty(hits, "the thrust lands");
            return hits[0];
        }

        [Test]
        public void Raiko_no_Tachi_hits_with_the_authored_damage_at_level_1()
        {
            var hit = ThrustHit(Duel());
            Assert.AreEqual(hit.Crit ? 33f : 22f, hit.Damage, "thrust 22, crit 33");
        }

        [Test]
        public void Raikons_tachi_hits_x0_78_as_hard_as_the_players_by_its_JS_stats()
        {
            var hit = ThrustHit(Duel("raikon_tachi"));
            Assert.AreEqual(hit.Crit ? 26f : 17f, hit.Damage, "22 x 0.78 = 17.2, crit 25.8");
        }

        [Test]
        public void The_dev_level_key_keeps_the_boss_weapon_at_level_1()
        {
            int max = Kage.Rules.GameRules.MaxLevel(Kage.Rules.GearKind.Weapons, Tachi);
            Assert.AreEqual(1, max);
            var f = Alone();
            Assert.AreEqual(1, f.Kage.WeaponLevel);
            f.Kage.EquipWeapon(Tachi, f.Kage.WeaponLevel >= max ? 1 : f.Kage.WeaponLevel + 1);   // what Shift+F8 does
            Assert.AreEqual(1, f.Kage.WeaponLevel);
            Assert.AreEqual(1f, f.Kage.Weapon.dmg);
        }
    }

    /// <summary>The player's buttons are family-agnostic: J / L and the pad's X, A, Y reach the tachi set through Select.</summary>
    public class TachiInputTests : InputTestFixture
    {
        [Test]
        public void Tachi_buttons_work_from_the_keyboard_and_the_gamepad_input_source()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: SpearFamilyFightTests.KageWith("raiko_no_tachi"));
            var set = (TachiMoves)f.Kage.moves;

            Press(kb.lKey); src.Sample(); Release(kb.lKey);
            Assert.IsTrue(src.Poll(f.Kage).s, "L is the weapon button");
            Assert.AreSame(set.cut1, set.Select(Btn.S, 0, 0, false, f.Kage));

            Press(gp.buttonNorth); src.Sample(); Release(gp.buttonNorth);
            Assert.IsTrue(src.Poll(f.Kage).s, "Y / triangle is the weapon button");
            Assert.AreSame(set.sweep, set.Select(Btn.S, 0, -1, false, f.Kage), "with the stick down");
            Assert.AreSame(set.thrust, set.Select(Btn.S, 1, 0, false, f.Kage), "with the stick forward");

            Press(gp.buttonWest); src.Sample(); Release(gp.buttonWest);
            Assert.IsTrue(src.Poll(f.Kage).p, "X / square is punch");
            Press(gp.buttonSouth); src.Sample(); Release(gp.buttonSouth);
            Assert.IsTrue(src.Poll(f.Kage).k, "A / cross is kick");
        }
    }
}
