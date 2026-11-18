using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The spear and staff family in a fight (SAN-78, ADR-0006): Kage wielding Mizuchi has the whole move set from the
    /// buttons, a two-handed polearm grip with its own spacing, the longest reach of the registered families, every
    /// move landing on the training bag, and weapon levels scaling it like any weapon.
    /// </summary>
    public class SpearFamilyFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        public static CharacterDef KageWith(string weapon, int level = 1)
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.weaponId = weapon;
            d.weaponLevel = level;
            return d;
        }

        HeadlessFight Alone(string weapon = "mizuchi", int level = 1)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon, level));
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel(string weapon = "mizuchi", int level = 1)
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

        // ---- the weapon picks the family ----

        [Test]
        public void Kage_equips_Mizuchi_and_fights_with_the_spear_set()
        {
            var f = Alone();
            Assert.AreEqual("mizuchi", f.Kage.WeaponId);
            Assert.AreEqual("spear", f.Kage.Family.id);
            Assert.IsInstanceOf<SpearMoves>(f.Kage.moves);
            Assert.AreEqual(GripPose.Polearm, f.Kage.Family.grip);
            Assert.AreEqual(1f, f.Kage.PlaybackRate);
        }

        [Test]
        public void The_spear_foes_use_the_same_set_and_each_move_id_is_in_the_spear_namespace()
        {
            var f = Alone("temple_staff");
            Assert.IsInstanceOf<SpearMoves>(f.Kage.moves);
            foreach (var m in ((SpearMoves)f.Kage.moves).All.Except(new[] { f.Kage.moves.roll, f.Kage.moves.backflip, f.Kage.moves.getUp, f.Kage.moves.getUpFront, f.Kage.moves.throwGrab, f.Kage.moves.throwWhiff }))
                StringAssert.StartsWith("spear.", m.id);
        }

        [Test]
        public void The_spear_is_drawn_in_place_of_the_katana_and_the_katana_comes_back()
        {
            var f = Alone();
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0, "the polearm's parts");
            Assert.IsTrue(f.Kage.rig.BaseSwordHidden, "Kage's katana is hidden");
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            f.Kage.EquipWeapon("kagetsu");
            Assert.AreEqual(0, f.Kage.rig.FamilyPartCount);
            Assert.IsFalse(f.Kage.rig.BaseSwordHidden);
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            f.Kage.EquipWeapon("mizuchi");
            f.Kage.EquipWeapon("temple_staff");
            int staffParts = f.Kage.rig.FamilyPartCount;
            f.Kage.EquipWeapon("temple_staff");
            Assert.AreEqual(staffParts, f.Kage.rig.FamilyPartCount, "attaching again never doubles the parts");
        }

        [Test]
        public void A_broken_spear_is_not_drawn_and_comes_back_next_fight()
        {
            var f = Alone();
            f.Kage.BreakSword();
            Assert.IsTrue(f.Kage.SwordBroken);
            Assert.IsFalse(f.Kage.rig.SwordVisible, "the shaft is hidden with the blade");
            f.Kage.ResetForFight();
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0);
        }

        // ---- the full set from the buttons ----

        [TestCase("P", 0, 0, "spear.jab")]
        [TestCase("P", 1, 0, "spear.stepPoke")]
        [TestCase("P", 0, -1, "spear.lowPoke")]
        [TestCase("K", 0, 0, "spear.kick")]
        [TestCase("K", 1, 0, "spear.pushKick")]
        [TestCase("K", -1, 0, "spear.vault")]
        [TestCase("K", 0, -1, "spear.lowKick")]
        [TestCase("S", 0, 0, "spear.thrust")]
        [TestCase("S", 1, 0, "spear.lunge")]
        [TestCase("S", 0, -1, "spear.sweep")]
        [TestCase("S", -1, 0, "spear.guardRaise")]
        public void Each_ground_button_and_direction_starts_its_spear_move(string button, int fwd, int dy, string expected)
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault());
        }

        [TestCase("P", "spear.airPoke")]
        [TestCase("K", "spear.airDive")]
        [TestCase("S", "spear.airSweep")]
        public void Each_air_button_starts_its_spear_air_move(string button, string expected)
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault(id => id.StartsWith("spear.air")));
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
        public void Pokes_chain_into_the_rising_poke_the_thrust_and_the_sweep()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(10).Add(1, buttons: "S");
            f.Run(120);
            CollectionAssert.AreEqual(new[] { "spear.jab", "spear.poke2", "spear.sweep" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "S");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "spear.jab", "spear.thrust" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "K");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "spear.jab", "spear.kick" }, Started(f));
        }

        [Test]
        public void Every_move_of_the_set_can_be_reached_from_a_button_or_a_chain()
        {
            var f = Duel();
            var set = (SpearMoves)f.Kage.moves;
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
            var body = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwWhiff };      // body moves, started by the fighter
            // Skewer needs a downed foe (below) and the throw a close one that can be thrown
            var missing = set.All.Except(reachable).Except(body).Except(new[] { set.skewer, set.throwGrab }).ToList();
            CollectionAssert.IsEmpty(missing.Select(m => m.id));
        }

        [Test]
        public void Against_a_downed_foe_down_and_punch_is_the_skewer()
        {
            var f = Duel();
            f.Run(20);
            f.KageInput.Add(1, dy: -1, buttons: "S");
            f.RunUntil(() => f.Ronin.state == FState.Down, 120);
            Assert.AreEqual(FState.Down, f.Ronin.state, "the sweep puts the Ronin on the floor");
            f.RunUntil(() => f.Kage.state != FState.Attack, 80);
            Assert.AreEqual(FState.Down, f.Ronin.state, "still down");
            f.KageInput.Add(1, dy: -1, buttons: "P");
            f.Run(30);
            CollectionAssert.Contains(Started(f), "spear.skewer");
        }

        // ---- the polearm grip ----

        /// <summary>The near (rear) fist against the shaft: metres behind the lead hand along it, and off its line.</summary>
        static void RearHand(Fighter k, out float along, out float off)
        {
            Vector2 pivot = k.rig.World(B.Sword, Vector2.zero);
            Vector2 axis = (k.rig.World(B.Sword, k.BladeTipLocal) - pivot).normalized;
            Vector2 d = (Vector2)k.rig.World(B.HandN, k.def.fistLocal) - pivot;
            along = Vector2.Dot(d, axis);
            off = Mathf.Abs(d.x * axis.y - d.y * axis.x);
        }

        [Test]
        public void Idle_holds_the_shaft_with_both_hands_a_forearm_apart()
        {
            var f = Alone();
            f.Run(30);
            RearHand(f.Kage, out float along, out float off);
            Assert.Less(off, 0.05f, "the rear fist is on the shaft");
            Assert.That(along, Is.LessThan(-0.3f).And.GreaterThan(-0.62f), "a polearm's wider grip, not the katana's fist-width one");
        }

        [Test]
        public void The_katana_grip_is_still_a_fist_apart()
        {
            var f = Alone("kagetsu");
            f.Run(30);
            RearHand(f.Kage, out float along, out _);
            Assert.That(along, Is.GreaterThan(-0.16f), "unchanged");
        }

        [TestCase("S", 0, 0)]
        [TestCase("S", 1, 0)]
        [TestCase("S", 0, -1)]
        [TestCase("P", 0, 0)]
        public void The_rear_hand_stays_on_the_shaft_through_the_main_spear_moves(string button, int fwd, int dy)
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
            // the sweep's fully extended low cut is past the rear arm's reach: it may let the fist drift a hand's width off the shaft
            Assert.Less(worst, id == "spear.sweep" ? 0.12f : 0.06f, $"{id}: the rear fist's worst distance from the shaft");
        }

        [Test]
        public void The_point_never_digs_into_the_floor_on_the_ground()
        {
            var f = Alone();
            var set = (SpearMoves)f.Kage.moves;
            foreach (var m in new[] { set.jab, set.poke2, set.stepPoke, set.lowPoke, set.skewer, set.kick, set.pushKick, set.lowKick, set.thrust, set.lunge, set.sweep, set.guardRaise })
            {
                f.Kage.EquipWeapon("mizuchi");
                f.Run(30);
                f.Kage.StartMove(m);
                float lowest = float.MaxValue;
                for (int i = 0; i < m.total + 4 && f.Kage.state == FState.Attack; i++)
                {
                    f.Run(1);
                    lowest = Mathf.Min(lowest, f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y);
                }
                Assert.Greater(lowest, -0.03f, $"{m.id}: the tip's lowest point");
            }
        }

        [Test]
        public void A_low_guard_with_the_spear_keeps_the_point_off_the_floor()
        {
            var f = Alone();
            f.KageInput.Add(40, dy: -1, buttons: "G");     // Guard with Down: the low guard
            f.Run(40);
            var cur = f.Kage.rig.current;
            Assert.Greater(f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y, -0.03f,
                $"state {f.Kage.state} hasSwAbs {cur.hasSwAbs} swAbs {cur.swAbs} handF {cur.target[(int)Limb.ArmF]} ik {cur.ik[(int)Limb.ArmF]} reach {f.Kage.BladeReach}");
        }

        // ---- reach ----

        /// <summary>
        /// The family's farthest hit point in front of the body over every hit window of every move of its set, when the
        /// window is on (metres from the fighter's position, root motion not counted).
        /// </summary>
        static float Reach(string familyId, out string longestMove)
        {
            var fam = WeaponFamilies.Get(familyId);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(fam.referenceWeapon));
            f.Run(2);
            float best = 0f;
            longestMove = null;
            foreach (var m in f.Kage.moves.All.Where(m => m.hits.Count > 0 && !m.isThrow).Distinct())
            {
                float moveBest = MoveReach(f, m);
                if (moveBest > best) { best = moveBest; longestMove = m.id; }
            }
            return best;
        }

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

        static readonly string[] AllFamilies =
        {
            WeaponFamilies.Katana, WeaponFamilies.Fists, WeaponFamilies.Twin, WeaponFamilies.Spear,
            WeaponFamilies.Chain, WeaponFamilies.Cleaver, WeaponFamilies.Tachi,
        };

        [Test]
        public void The_spear_set_has_the_longest_reach_of_every_registered_family()
        {
            float spear = Reach(WeaponFamilies.Spear, out string longest);
            Debug.Log($"spear reach {spear:0.00} m (longest: {longest})");
            StringAssert.StartsWith("spear.", longest);
            int compared = 0;
            foreach (var id in AllFamilies)
            {
                if (id == WeaponFamilies.Spear || WeaponFamilies.Get(id) == null) continue;
                float other = Reach(id, out string otherMove);
                Debug.Log($"{id} reach {other:0.00} m ({otherMove})");
                Assert.Greater(spear, other, $"the spear reaches further than the {id} family ({otherMove} {other:0.00} m)");
                compared++;
            }
            Assert.GreaterOrEqual(compared, 2, "at least the katana and the fists to compare with");
        }

        [Test]
        public void The_long_thrust_is_the_longest_move_of_the_set()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith("mizuchi"));
            f.Run(2);
            var set = (SpearMoves)f.Kage.moves;
            float thrust = MoveReach(f, set.thrust);
            Assert.Greater(thrust, 1.8f, "from the body to the tip");
            Debug.Log("spear move reach (m): " + string.Join(", ", set.All.Where(m => m.hits.Count > 0 && !m.isThrow).Distinct().Select(m => $"{m.id} {MoveReach(f, m):0.00}")));
            foreach (var m in set.All.Where(m => m.hits.Count > 0 && !m.isThrow && m != set.thrust && !m.airborne).Distinct())
                Assert.GreaterOrEqual(thrust + 0.05f, MoveReach(f, m), m.id);
        }

        // ---- each move lands on the training bag ----

        static readonly Vector2 HighBag = HeadlessFight.BagAnchor, LowBag = new(1.6f, 1.75f);
        static readonly float[] Distances = { 0.45f, 0.8f, 1.15f, 1.5f, 1.85f, 2.2f };

        /// <summary>Starts <paramref name="m"/> against the training bag from several distances (and the dev arena's high bag and a low-hung one); true when it hits.</summary>
        public static bool LandsOnBag(MoveDef m, string weapon, out string where)
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
            var set = (SpearMoves)f.Kage.moves;
            var all = set.All.Distinct().ToList();
            var notHits = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff, set.guardRaise };   // no hit window (the throw needs a fighter)
            var report = new List<string>();
            var failed = new List<string>();
            foreach (var m in all.Except(notHits))
            {
                Assert.IsNotEmpty(m.hits, $"{m.id} has a hit window");
                if (LandsOnBag(m, "mizuchi", out string where)) report.Add($"{m.id}: {where}");
                else failed.Add(m.id);
            }
            Debug.Log("lands on the bag:\n" + string.Join("\n", report));
            CollectionAssert.IsEmpty(failed, "moves that never hit the bag");
            foreach (var m in new[] { set.guardRaise }) Assert.IsEmpty(m.hits, "the guard-raise has no hit window");
        }

        [Test]
        public void The_spear_shaft_and_head_are_blade_points_and_so_can_clash()
        {
            var f = Alone();
            var set = (SpearMoves)f.Kage.moves;
            foreach (var m in new[] { set.jab, set.poke2, set.stepPoke, set.lowPoke, set.skewer, set.thrust, set.lunge, set.sweep, set.vault, set.airPoke, set.airDive, set.airSweep })
                foreach (var w in m.hits)
                {
                    CollectionAssert.AreEqual(new[] { HitPoint.Blade }, w.points, m.id);
                    Assert.IsTrue(w.sword, m.id + " is a sword-class window (clash eligible)");
                }
            foreach (var m in new[] { set.kick, set.pushKick, set.lowKick })
                foreach (var w in m.hits) CollectionAssert.DoesNotContain(w.points, HitPoint.Blade, m.id + ": kicks never clash");
        }

        // ---- weapon levels scale it like any weapon ----

        static CombatEvent FirstHit(HeadlessFight f, string move)
        {
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == move).ToList();
            Assert.IsNotEmpty(hits, move + " lands");
            return hits[0];
        }

        CombatEvent ThrustHit(int level)
        {
            var f = Duel("mizuchi", level);
            f.KageInput.Add(1, buttons: "S");
            f.Run(60);
            var hit = FirstHit(f, "spear.thrust");
            Done(f);
            return hit;
        }

        [Test]
        public void Mizuchi_level_8_hits_harder_by_the_JS_amount()
        {
            var l1 = ThrustHit(1);
            Assert.AreEqual(l1.Crit ? 14f : 9f, l1.Damage, "thrust: 9, crit 13.5 -> 14");
            var l8 = ThrustHit(8);
            Assert.AreEqual(l8.Crit ? 21f : 14f, l8.Damage, "thrust x1.56: 14.04 -> 14, crit 21.06 -> 21");
        }

        [Test]
        public void Mizuchi_level_8_plays_its_moves_faster()
        {
            int Frames(int level)
            {
                var f = Alone("mizuchi", level);
                f.KageInput.Add(1, buttons: "S");
                int frames = 0;
                for (int i = 0; i < 200; i++)
                {
                    f.Run(1);
                    if (f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == "spear.thrust") frames++;
                    else if (frames > 0) break;
                }
                Done(f);
                return frames;
            }
            Assert.AreEqual(34, Frames(1), "34 authored frames at rate 1");
            Assert.AreEqual(29, Frames(8), "34 frames at x1.175");
        }

        [Test]
        public void Mizuchi_level_8_reaches_further_along_the_shaft()
        {
            var f1 = Alone("mizuchi", 1);
            float l1 = f1.Kage.BladeReach;
            Done(f1);
            var f8 = Alone("mizuchi", 8);
            float l8 = f8.Kage.BladeReach;
            Assert.AreEqual(SpearMoves.BladeLength, l1, 1e-3f, "level 1 hits along the family's blade length");
            Assert.AreEqual(1.105f, l8 / l1, 1e-3f);
        }

        /// <summary>The farthest the thrust lands on the training bag (metres between Kage's start and the bag).</summary>
        static float FarthestThrustHit(int level)
        {
            for (float dist = 3.0f; dist > 1.0f; dist -= 0.04f)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag, 1.6f - dist, kageDef: KageWith("mizuchi", level));
                f.KageInput.Add(4).Add(1, buttons: "S");
                f.Run(60);
                if (f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == "spear.thrust")) return dist;
            }
            return 0f;
        }

        [Test]
        public void A_higher_level_thrust_hits_a_target_further_away()
        {
            float l1 = FarthestThrustHit(1), l8 = FarthestThrustHit(8);
            Debug.Log($"thrust lands from up to {l1:0.00} m (level 1) and {l8:0.00} m (level 8)");
            Assert.Greater(l1, 1.9f, "the thrust is a long one");
            Assert.Greater(l8 - l1, 0.1f, "the Blade points run further along the shaft at level 8 (+0.15 m of 1.45 m)");
        }
    }

    /// <summary>The player's buttons are family-agnostic: J / L and the pad's X, A, Y reach the spear set through Select.</summary>
    public class SpearInputTests : InputTestFixture
    {
        [Test]
        public void Spear_buttons_work_from_the_keyboard_and_the_gamepad_input_source()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: SpearFamilyFightTests.KageWith("mizuchi"));
            var set = (SpearMoves)f.Kage.moves;

            Press(kb.lKey); src.Sample(); Release(kb.lKey);
            var fi = src.Poll(f.Kage);
            Assert.IsTrue(fi.s, "L is the weapon button");
            Assert.AreSame(set.thrust, set.Select(Btn.S, 0, 0, false, f.Kage));

            Press(gp.buttonNorth); src.Sample(); Release(gp.buttonNorth);
            Assert.IsTrue(src.Poll(f.Kage).s, "Y / triangle is the weapon button");
            Assert.AreSame(set.sweep, set.Select(Btn.S, 0, -1, false, f.Kage), "with the stick down");

            Press(gp.buttonWest); src.Sample(); Release(gp.buttonWest);
            Assert.IsTrue(src.Poll(f.Kage).p, "X / square is punch");
            Press(gp.buttonSouth); src.Sample(); Release(gp.buttonSouth);
            Assert.IsTrue(src.Poll(f.Kage).k, "A / cross is kick");
        }
    }
}
