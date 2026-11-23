using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The cleaver family in a fight (SAN-79, ADR-0006): Kage wielding Onikiri has the whole move set from the buttons,
    /// a heavy two-handed grip, moves clearly slower and harder-hitting than the katana's, a reach below the spear's,
    /// every move landing on the training bag, and weapon levels scaling it like any weapon.
    /// </summary>
    public class CleaverFamilyFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        public static CharacterDef KageWith(string weapon, int level = 1) => SpearFamilyFightTests.KageWith(weapon, level);

        HeadlessFight Alone(string weapon = "onikiri", int level = 1)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon, level));
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel(string weapon = "onikiri", int level = 1)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, 0f, kageDef: KageWith(weapon, level));
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

        static MoveDef[] Body(MoveSet s) => new[] { s.roll, s.backflip, s.getUp, s.getUpFront, s.throwGrab, s.throwWhiff };

        // ---- the weapon picks the family ----

        [Test]
        public void Kage_equips_Onikiri_and_fights_with_the_cleaver_set()
        {
            var f = Alone();
            Assert.AreEqual("onikiri", f.Kage.WeaponId);
            Assert.AreEqual("cleaver", f.Kage.Family.id);
            Assert.IsInstanceOf<CleaverMoves>(f.Kage.moves);
            Assert.AreEqual(GripPose.HeavyTwoHand, f.Kage.Family.grip);
            Assert.AreEqual(1f, f.Kage.PlaybackRate, "Onikiri is its own reference: x1 at level 1");
        }

        [TestCase("raider_cleaver")]
        [TestCase("tyrant_cleaver")]
        [TestCase("iwakudaki")]
        public void The_cleaver_foes_use_the_same_set_and_each_move_id_is_in_the_cleaver_namespace(string weapon)
        {
            var f = Alone(weapon);
            Assert.IsInstanceOf<CleaverMoves>(f.Kage.moves);
            foreach (var m in ((CleaverMoves)f.Kage.moves).All.Except(Body(f.Kage.moves)))
                StringAssert.StartsWith("cleaver.", m.id);
        }

        [Test]
        public void The_cleaver_is_drawn_in_place_of_the_katana_and_the_katana_comes_back()
        {
            var f = Alone();
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0, "the cleaver's parts");
            Assert.IsTrue(f.Kage.rig.BaseSwordHidden, "Kage's katana is hidden");
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            f.Kage.EquipWeapon("kagetsu");
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0, "Kagetsu is drawn as the Armory shows it");
            Assert.IsTrue(f.Kage.rig.BaseSwordHidden, "over the hidden base katana");
            f.Kage.EquipWeapon("raiko_no_tachi");
            f.Kage.EquipWeapon("kagetsu");
            int katana = f.Kage.rig.FamilyPartCount;
            f.Kage.EquipWeapon("kagetsu");
            Assert.AreEqual(katana, f.Kage.rig.FamilyPartCount, "attaching Kagetsu again never doubles its parts");
            f.Kage.EquipWeapon("onikiri");
            f.Kage.EquipWeapon("raider_cleaver");
            int parts = f.Kage.rig.FamilyPartCount;
            f.Kage.EquipWeapon("raider_cleaver");
            Assert.AreEqual(parts, f.Kage.rig.FamilyPartCount, "attaching again never doubles the parts");
            f.Kage.EquipWeapon("mizuchi");
            f.Kage.EquipWeapon("onikiri");
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0, "swapping through another family and back draws the cleaver again");
        }

        [Test]
        public void A_broken_cleaver_is_not_drawn_and_comes_back_next_fight()
        {
            var f = Alone();
            f.Kage.BreakSword();
            Assert.IsTrue(f.Kage.SwordBroken);
            Assert.IsFalse(f.Kage.rig.SwordVisible);
            f.Kage.ResetForFight();
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0);
        }

        // ---- the full set from the buttons ----

        [TestCase("P", 0, 0, "cleaver.hack")]
        [TestCase("P", 1, 0, "cleaver.stepHack")]
        [TestCase("P", 0, -1, "cleaver.lowHack")]
        [TestCase("K", 0, 0, "cleaver.kick")]
        [TestCase("K", 1, 0, "cleaver.pushKick")]
        [TestCase("K", -1, 0, "cleaver.pommel")]
        [TestCase("K", 0, -1, "cleaver.lowKick")]
        [TestCase("S", 0, 0, "cleaver.overhead")]
        [TestCase("S", 1, 0, "cleaver.charge")]
        [TestCase("S", 0, -1, "cleaver.slam")]
        [TestCase("S", -1, 0, "cleaver.sweep")]
        public void Each_ground_button_and_direction_starts_its_cleaver_move(string button, int fwd, int dy, string expected)
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(70);
            Assert.AreEqual(expected, Started(f).FirstOrDefault());
        }

        [TestCase("P", "cleaver.airChop")]
        [TestCase("K", "cleaver.airKick")]
        [TestCase("S", "cleaver.airPlunge")]
        public void Each_air_button_starts_its_cleaver_air_move(string button, string expected)
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, buttons: button);
            f.Run(70);
            Assert.AreEqual(expected, Started(f).FirstOrDefault(id => id.StartsWith("cleaver.air")));
        }

        [Test]
        public void The_plain_special_is_not_part_of_the_set_and_does_nothing()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "X");
            f.Run(30);
            CollectionAssert.IsEmpty(Started(f));
        }

        [Test]
        public void Hacks_chain_into_the_return_cut_the_overhead_and_the_sweep()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(17).Add(1, buttons: "P").Add(19).Add(1, buttons: "S");
            f.Run(160);
            CollectionAssert.AreEqual(new[] { "cleaver.hack", "cleaver.hack2", "cleaver.overhead" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(17).Add(1, buttons: "S");
            f.Run(130);
            CollectionAssert.AreEqual(new[] { "cleaver.hack", "cleaver.sweep" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(17).Add(1, buttons: "K");
            f.Run(110);
            CollectionAssert.AreEqual(new[] { "cleaver.hack", "cleaver.kick" }, Started(f));
        }

        [Test]
        public void Every_move_of_the_set_can_be_reached_from_a_button_or_a_chain()
        {
            var f = Duel();
            var set = (CleaverMoves)f.Kage.moves;
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
            var body = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwWhiff };      // started by the fighter
            // the Hew needs a downed foe (below) and the throw a close one that can be thrown
            var missing = set.All.Except(reachable).Except(body).Except(new[] { set.hew, set.throwGrab }).ToList();
            CollectionAssert.IsEmpty(missing.Select(m => m.id));
        }

        [Test]
        public void Against_a_downed_foe_down_and_punch_is_the_hew()
        {
            var f = Duel();
            f.Run(20);
            f.KageInput.Add(1, fwd: -1, buttons: "S");     // the sweep puts the Ronin on the floor
            f.RunUntil(() => f.Ronin.state == FState.Down, 160);
            Assert.AreEqual(FState.Down, f.Ronin.state);
            f.RunUntil(() => f.Kage.state != FState.Attack, 100);
            Assert.AreEqual(FState.Down, f.Ronin.state, "still down");
            f.KageInput.Add(1, dy: -1, buttons: "P");
            f.Run(30);
            CollectionAssert.Contains(Started(f), "cleaver.hew");
        }

        [Test]
        public void The_body_moves_and_the_throw_are_the_katana_sets_by_reference()
        {
            var f = Duel();
            var katana = (KatanaMoves)f.Ronin.moves;
            var set = (CleaverMoves)f.Kage.moves;
            Assert.AreSame(katana.roll, set.roll);
            Assert.AreSame(katana.backflip, set.backflip);
            Assert.AreSame(katana.getUp, set.getUp);
            Assert.AreSame(katana.getUpFront, set.getUpFront);
            Assert.AreSame(katana.throwGrab, set.throwGrab);
            Assert.AreSame(katana.throwWhiff, set.throwWhiff);
            Assert.AreEqual(set.All.Count(), set.All.Distinct().Count(), "no move listed twice");
        }

        // ---- the heavy two-handed grip ----

        /// <summary>The near (rear) fist against the handle: metres behind the lead hand along the cleaver, and off its line.</summary>
        static void RearHand(Fighter k, out float along, out float off)
        {
            Vector2 pivot = k.rig.World(B.Sword, Vector2.zero);
            Vector2 axis = (k.rig.World(B.Sword, k.BladeTipLocal) - pivot).normalized;
            Vector2 d = (Vector2)k.rig.World(B.HandN, k.def.fistLocal) - pivot;
            along = Vector2.Dot(d, axis);
            off = Mathf.Abs(d.x * axis.y - d.y * axis.x);
        }

        [Test]
        public void Idle_holds_the_handle_with_both_hands_wider_than_the_katana_grip()
        {
            var f = Alone();
            f.Run(30);
            RearHand(f.Kage, out float along, out float off);
            Assert.Less(off, 0.05f, "the rear fist is on the handle");
            Assert.That(along, Is.LessThan(-0.17f).And.GreaterThan(-0.45f), "a heavy grip: wider than the katana's fist-width, short of the polearm's forearm");
        }

        [TestCase("S", 0, 0)]
        [TestCase("S", -1, 0)]
        [TestCase("P", 0, 0)]
        public void The_rear_hand_stays_on_the_handle_through_the_main_cleaver_moves(string button, int fwd, int dy)
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
            Assert.Less(worst, 0.12f, $"{id}: the rear fist's worst distance from the handle");
        }

        [Test]
        public void The_cleavers_edge_never_digs_into_the_floor_on_the_ground()
        {
            var f = Alone();
            var set = (CleaverMoves)f.Kage.moves;
            foreach (var m in set.All.Except(Body(set)).Where(m => !m.airborne))
            {
                f.Kage.EquipWeapon("onikiri");
                f.Run(30);
                f.Kage.StartMove(m);
                float lowest = float.MaxValue;
                for (int i = 0; i < m.total + 4 && f.Kage.state == FState.Attack; i++)
                {
                    f.Run(1);
                    lowest = Mathf.Min(lowest, f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y);
                }
                Assert.Greater(lowest, -0.05f, $"{m.id}: the tip's lowest point");
            }
        }

        // ---- slower and harder than the katana ----

        /// <summary>The pairs the same button and direction gives in each set.</summary>
        static IEnumerable<(string name, MoveDef cleaver, MoveDef katana)> Pairs(CleaverMoves c, KatanaMoves k) => new[]
        {
            ("P", c.hack, k.jab), ("P+fwd", c.stepHack, k.lunge), ("P+down", c.lowHack, k.crouchPunch),
            ("K", c.kick, k.kick1), ("K+fwd", c.pushKick, k.pushKick), ("K+back", c.pommel, k.axeKick), ("K+down", c.lowKick, k.sweep),
            ("S", c.overhead, k.cleave), ("S+fwd", c.charge, k.shadowStep), ("S+down", c.slam, k.lowReap), ("S+back", c.sweep, k.iai),
            ("air P", c.airChop, k.airPunch), ("air K", c.airKick, k.airKick), ("air S", c.airPlunge, k.airSlash),
        };

        static int Startup(MoveDef m) => m.hits.Min(w => w.from);
        static int PerHit(MoveDef m) => m.hits.Max(w => w.dmg);

        [Test]
        public void Every_cleaver_move_is_slower_and_hits_harder_than_the_katana_move_on_the_same_button()
        {
            var f = Duel();
            var c = (CleaverMoves)f.Kage.moves;
            var k = (KatanaMoves)f.Ronin.moves;
            var lines = new List<string>();
            foreach (var (name, cm, km) in Pairs(c, k))
            {
                lines.Add($"{name,-8} {cm.id,-18} {cm.total,3} f, first hit f{Startup(cm),2}, {PerHit(cm),2} dmg   |   {km.id,-10} {km.total,3} f, f{Startup(km),2}, {PerHit(km),2} dmg");
                Assert.Greater(cm.total, km.total, $"{name}: {cm.id} lasts longer than {km.id}");
                Assert.Greater(Startup(cm), Startup(km), $"{name}: {cm.id} starts its hit later than {km.id}");
                Assert.Greater(PerHit(cm), PerHit(km), $"{name}: {cm.id} hits harder per window than {km.id}");
            }
            Debug.Log("cleaver against katana:\n" + string.Join("\n", lines));
        }

        [Test]
        public void On_average_the_set_is_clearly_slower_and_clearly_harder_hitting()
        {
            var f = Duel();
            var c = (CleaverMoves)f.Kage.moves;
            var k = (KatanaMoves)f.Ronin.moves;
            var cleaver = Pairs(c, k).Select(p => p.cleaver).ToList();
            var katana = Pairs(c, k).Select(p => p.katana).ToList();
            double frames = cleaver.Average(m => m.total) / katana.Average(m => m.total);
            double startup = cleaver.Average(m => Startup(m)) / katana.Average(m => Startup(m));
            double dmg = cleaver.Average(m => PerHit(m)) / katana.Average(m => PerHit(m));
            Debug.Log($"cleaver / katana: length x{frames:0.00}, first hit x{startup:0.00}, damage per window x{dmg:0.00}");
            Assert.GreaterOrEqual(frames, 1.25, "at least a quarter longer");
            Assert.GreaterOrEqual(startup, 1.4, "the hit comes at least 40 % later");
            Assert.GreaterOrEqual(dmg, 1.5, "at least half as much damage again");
        }

        static CombatEvent FirstHit(HeadlessFight f, string move)
        {
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == move).ToList();
            Assert.IsNotEmpty(hits, move + " lands");
            return hits[0];
        }

        [Test]
        public void The_overhead_lands_for_more_than_the_katanas_cleave_on_the_same_foe_and_takes_longer_to_start()
        {
            var f = Duel("onikiri");
            f.KageInput.Add(1, buttons: "S");
            f.Run(90);
            var cleaver = FirstHit(f, "cleaver.overhead");
            int cleaverStart = f.Events(CombatEventType.MoveStarted).First(e => e.SourceId == "kage").Frame;
            Done(f);

            f = Duel("kagetsu");
            f.Run(2);
            f.Kage.StartMove(((KatanaMoves)f.Kage.moves).cleave);
            f.Run(90);
            var katana = FirstHit(f, "cleave");
            int katanaStart = f.Events(CombatEventType.MoveStarted).First(e => e.SourceId == "kage").Frame;
            Done(f);

            Assert.Greater(cleaver.Damage / (cleaver.Crit ? 1.5f : 1f), 1.5f * katana.Damage / (katana.Crit ? 1.5f : 1f), "per hit, before the crit roll");
            Assert.Greater(cleaver.Frame - cleaverStart, katana.Frame - katanaStart + 6, "the cleaver's hit lands later");
        }

        // ---- reach ----

        static float MoveReach(HeadlessFight f, MoveDef m)
        {
            var k = f.Kage;
            k.ResetForRound(0f, 1f);
            if (m.airborne) { f.KageInput.Add(10, fwd: 0, dy: 1); f.Run(10); }
            else f.Run(8);
            k.StartMove(m);
            float best = 0f;
            for (int i = 0; i < m.total + 2 && k.state == FState.Attack && k.move == m; i++)
            {
                f.Run(1);
                foreach (var w in m.hits)
                {
                    if (k.moveFrame < w.from || k.moveFrame > w.to) continue;
                    foreach (var hp in w.points)
                        foreach (var p in PointsOf(k, hp))
                            best = Mathf.Max(best, (p.x - k.Pos.x) * k.facing);
                }
            }
            return best;
        }

        static IEnumerable<Vector2> PointsOf(Fighter k, HitPoint hp)
        {
            var r = k.rig;
            var d = k.def;
            switch (hp)
            {
                case HitPoint.Fist: yield return r.World(B.HandN, d.fistLocal); break;
                case HitPoint.Hilt: yield return r.World(B.HandF, d.hiltLocal); break;
                case HitPoint.FistF: yield return r.World(B.HandF, d.BindLocal(B.Sword)); break;
                case HitPoint.FootN: yield return r.World(B.FootN, d.footTipLocal); break;
                case HitPoint.FootF: yield return r.World(B.FootF, d.footTipLocal); break;
                case HitPoint.ShinN: yield return r.World(B.LegNL, new Vector2(0f, -0.1f)); break;
                case HitPoint.ShinF: yield return r.World(B.LegFL, new Vector2(0f, -0.1f)); break;
                case HitPoint.KneeN: yield return r.World(B.LegNL, Vector2.zero); break;
                case HitPoint.Blade: yield return r.World(B.Sword, k.BladeTipLocal); break;
            }
        }

        [Test]
        public void The_cleaver_reaches_further_than_the_katana_and_stays_below_the_spears_2_25_m()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith("onikiri"));
            f.Run(2);
            var set = (CleaverMoves)f.Kage.moves;
            var reach = set.All.Where(m => m.hits.Count > 0 && !m.isThrow).Distinct().ToDictionary(m => m.id, m => MoveReach(f, m));
            Debug.Log("cleaver move reach (m): " + string.Join(", ", reach.Select(p => $"{p.Key} {p.Value:0.00}")));
            float best = reach.Values.Max();
            Assert.Greater(best, 1.45f, "longer than the katana's 1.45 m");
            Assert.Less(best, 2.25f, "the spear keeps the longest reach (the spear's own test compares every family too)");
        }

        // ---- each move lands on the training bag ----

        static readonly Vector2 HighBag = HeadlessFight.BagAnchor, LowBag = new(1.6f, 1.75f);
        static readonly float[] Distances = { 0.45f, 0.8f, 1.15f, 1.5f, 1.85f };

        static bool LandsOnBag(MoveDef m, string weapon, out string where)
        {
            foreach (var anchor in new[] { HighBag, LowBag })
                foreach (float dist in Distances)
                {
                    using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag, 1.6f - dist, kageDef: KageWith(weapon), bagAnchor: anchor);
                    if (m.airborne) { f.KageInput.Add(10, fwd: 1, dy: 1); f.Run(10); }
                    else f.Run(4);
                    var set = f.Kage.moves.All.First(x => x.id == m.id);
                    f.Kage.StartMove(set);
                    f.Run(set.total + 20);
                    if (f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == m.id && e.TargetId == "bag"))
                    {
                        where = $"{(anchor == HighBag ? "high" : "low")} bag at {dist:0.00} m";
                        return true;
                    }
                }
            where = null;
            return false;
        }

        [Test]
        public void Every_hitting_move_of_the_set_lands_on_the_training_bag()
        {
            var f = Alone();
            var set = (CleaverMoves)f.Kage.moves;
            var all = set.All.Distinct().ToList();
            var notHits = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff };   // no hit window (the throw needs a fighter)
            var report = new List<string>();
            var failed = new List<string>();
            foreach (var m in all.Except(notHits))
            {
                Assert.IsNotEmpty(m.hits, $"{m.id} has a hit window");
                if (LandsOnBag(m, "onikiri", out string where)) report.Add($"{m.id}: {where}");
                else failed.Add(m.id);
            }
            Debug.Log("lands on the bag:\n" + string.Join("\n", report));
            CollectionAssert.IsEmpty(failed, "moves that never hit the bag");
        }

        [Test]
        public void The_cleavers_edge_is_a_blade_point_and_so_can_clash_while_boots_pommel_and_forearm_never_do()
        {
            var f = Alone();
            var set = (CleaverMoves)f.Kage.moves;
            foreach (var m in new[] { set.hack, set.hack2, set.stepHack, set.lowHack, set.hew, set.overhead, set.sweep, set.slam, set.airChop, set.airPlunge })
                foreach (var w in m.hits)
                {
                    CollectionAssert.AreEqual(new[] { HitPoint.Blade }, w.points, m.id);
                    Assert.IsTrue(w.sword, m.id + " is a sword-class window (clash eligible)");
                }
            foreach (var m in new[] { set.kick, set.pushKick, set.lowKick, set.pommel, set.charge, set.airKick })
                foreach (var w in m.hits)
                {
                    CollectionAssert.DoesNotContain(w.points, HitPoint.Blade, m.id + ": no clash");
                    Assert.IsFalse(w.sword, m.id);
                }
        }

        [Test]
        public void The_heavy_swings_carry_armour_frames_and_the_guard_breaking_posture_as_data()
        {
            var f = Alone();
            var set = (CleaverMoves)f.Kage.moves;
            foreach (var m in new[] { set.overhead, set.sweep, set.charge, set.slam })
            {
                Assert.GreaterOrEqual(m.armorFrom, 0, m.id + " has armour frames");
                Assert.LessOrEqual(m.armorTo, m.hits.Max(w => w.to), m.id + ": the armour ends when the swing has landed");
                Assert.Less(m.armorFrom, Startup(m), m.id + ": the armour starts in the wind-up");
                Assert.IsTrue(m.hits.Any(w => w.heavy), m.id + " is a heavy hit");
                Assert.IsTrue(m.hits.Any(w => w.postureMul > 1f), m.id + " breaks guards");
            }
            Assert.AreEqual(-1, set.hack.armorFrom, "the quick hack has none");
        }

        static HitInfo Incoming(Fighter attacker, bool heavy) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = 6, level = HitLevel.Mid, kb = new Vector2(3f, 0f), stun = 16, heavy = heavy },
            dmg = 6, kb = new Vector2(-3f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), moveId = "test.hit", window = -1,
        };

        [Test]
        public void A_light_hit_in_the_overhead_wind_up_does_not_flinch_Kage_but_a_heavy_one_does()
        {
            var f = Duel();
            var set = (CleaverMoves)f.Kage.moves;
            f.Run(4);
            f.Kage.StartMove(set.overhead);
            f.RunUntil(() => f.Kage.moveFrame >= 14, 60);
            Assert.IsTrue(f.Kage.MoveArmoured, "armoured in the wind-up");
            float before = f.Kage.health;
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.Less(f.Kage.health, before, "the damage still lands");
            Assert.AreEqual(FState.Attack, f.Kage.state, "no flinch from a light hit");
            Assert.AreSame(set.overhead, f.Kage.move);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: true));
            Assert.AreNotEqual(FState.Attack, f.Kage.state, "a heavy hit still breaks the swing");
        }

        // ---- weapon levels scale it like any weapon ----

        CombatEvent OverheadHit(int level)
        {
            var f = Duel("onikiri", level);
            f.KageInput.Add(1, buttons: "S");
            f.Run(100);
            var hit = FirstHit(f, "cleaver.overhead");
            Done(f);
            return hit;
        }

        [Test]
        public void Onikiri_level_8_hits_harder_by_the_JS_amount()
        {
            int window = ((CleaverMoves)Alone().Kage.moves).overhead.hits[0].dmg;
            var l1 = OverheadHit(1);
            Assert.AreEqual(l1.Crit ? Mathf.RoundToInt(window * 1.5f) : window, l1.Damage, "level 1 is x1");
            var l8 = OverheadHit(8);
            Assert.AreEqual(l8.Crit ? Mathf.RoundToInt(window * 1.56f * 1.5f) : Mathf.RoundToInt(window * 1.56f), l8.Damage, "level 8 is x1.56");
        }

        [Test]
        public void Onikiri_level_8_plays_its_moves_faster()
        {
            int Frames(int level)
            {
                var f = Alone("onikiri", level);
                f.KageInput.Add(1, buttons: "S");
                int frames = 0;
                for (int i = 0; i < 200; i++)
                {
                    f.Run(1);
                    if (f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == "cleaver.overhead") frames++;
                    else if (frames > 0) break;
                }
                Done(f);
                return frames;
            }
            int total = ((CleaverMoves)Alone().Kage.moves).overhead.total;
            Assert.AreEqual(total, Frames(1), "the authored frames at rate 1");
            Assert.AreEqual(Mathf.CeilToInt(total / 1.175f), Frames(8), "the authored frames at x1.175");
        }

        [Test]
        public void Onikiri_level_8_reaches_further_along_the_blade()
        {
            var f1 = Alone("onikiri", 1);
            float l1 = f1.Kage.BladeReach;
            Done(f1);
            var f8 = Alone("onikiri", 8);
            float l8 = f8.Kage.BladeReach;
            Assert.AreEqual(CleaverMoves.BladeLength, l1, 1e-3f, "level 1 hits along the family's blade length");
            Assert.AreEqual(1.105f, l8 / l1, 1e-3f);
        }
    }

    /// <summary>The player's buttons are family-agnostic: J / L and the pad's X, A, Y reach the cleaver set through Select.</summary>
    public class CleaverInputTests : InputTestFixture
    {
        [Test]
        public void Cleaver_buttons_work_from_the_keyboard_and_the_gamepad_input_source()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: CleaverFamilyFightTests.KageWith("onikiri"));
            var set = (CleaverMoves)f.Kage.moves;

            Press(kb.lKey); src.Sample(); Release(kb.lKey);
            Assert.IsTrue(src.Poll(f.Kage).s, "L is the weapon button");
            Assert.AreSame(set.overhead, set.Select(Btn.S, 0, 0, false, f.Kage));

            Press(gp.buttonNorth); src.Sample(); Release(gp.buttonNorth);
            Assert.IsTrue(src.Poll(f.Kage).s, "Y / triangle is the weapon button");
            Assert.AreSame(set.slam, set.Select(Btn.S, 0, -1, false, f.Kage), "with the stick down");
            Assert.AreSame(set.charge, set.Select(Btn.S, 1, 0, false, f.Kage), "with the stick forward");
            Assert.AreSame(set.sweep, set.Select(Btn.S, -1, 0, false, f.Kage), "with the stick back");

            Press(gp.buttonWest); src.Sample(); Release(gp.buttonWest);
            Assert.IsTrue(src.Poll(f.Kage).p, "X / square is punch");
            Press(gp.buttonSouth); src.Sample(); Release(gp.buttonSouth);
            Assert.IsTrue(src.Poll(f.Kage).k, "A / cross is kick");
        }
    }
}
