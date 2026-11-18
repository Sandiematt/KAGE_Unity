using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The chain sickle family in a fight (SAN-87, ADR-0006): Kage wielding Yamigama has the whole move set from the
    /// buttons, a chain lash that reaches mid range, a pull that drags the foe in through the physics knockback, a chain
    /// that is drawn and whose length follows the move, every move landing, and weapon levels scaling it like any weapon.
    /// </summary>
    public class SickleFamilyFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef KageWith(string weapon, int level = 1) => SpearFamilyFightTests.KageWith(weapon, level);

        HeadlessFight Alone(string weapon = "yamigama", int level = 1)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon, level));
            _fights.Add(f);
            return f;
        }

        /// <summary>Kage at x = 0 and an idle Ronin <paramref name="gap"/> metres in front of him.</summary>
        HeadlessFight Duel(float gap, string weapon = "yamigama", int level = 1, CharacterDef foe = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, 0f, gap, 0f, foeDef: foe, kageDef: KageWith(weapon, level));
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
        public void Kage_equips_Yamigama_and_fights_with_the_chain_set()
        {
            var f = Alone();
            Assert.AreEqual("yamigama", f.Kage.WeaponId);
            Assert.AreEqual("chain", f.Kage.Family.id);
            Assert.IsInstanceOf<ChainMoves>(f.Kage.moves);
            Assert.AreEqual(GripPose.OneHand, f.Kage.Family.grip);
            Assert.AreEqual(1f, f.Kage.PlaybackRate);
        }

        [Test]
        public void The_Chainthrowers_weapon_uses_the_same_set_and_every_move_id_is_in_the_chain_namespace()
        {
            var f = Alone("chain_hook");
            Assert.IsInstanceOf<ChainMoves>(f.Kage.moves);
            var set = (ChainMoves)f.Kage.moves;
            var body = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff };
            foreach (var m in set.All.Except(body)) StringAssert.StartsWith("chain.", m.id);
        }

        [Test]
        public void The_sickle_and_chain_are_drawn_in_place_of_the_katana_and_the_katana_comes_back()
        {
            var f = Alone();
            Assert.Greater(f.Kage.rig.FamilyPartCount, 0, "the sickle's parts");
            Assert.IsTrue(f.Kage.rig.BaseSwordHidden, "Kage's katana is hidden");
            Assert.IsNotNull(ChainVisual.LineOf(f.Kage.rig), "the chain is a drawn part of its own");
            f.Kage.EquipWeapon("kagetsu");
            Assert.AreEqual(0, f.Kage.rig.FamilyPartCount);
            Assert.IsFalse(f.Kage.rig.BaseSwordHidden);
            Assert.IsNull(ChainVisual.LineOf(f.Kage.rig), "no chain on a katana");
            f.Kage.EquipWeapon("yamigama");
            int parts = f.Kage.rig.FamilyPartCount;
            f.Kage.EquipWeapon("chain_hook");
            Assert.AreEqual(parts, f.Kage.rig.FamilyPartCount, "attaching again never doubles the parts");
            Assert.AreEqual(1, f.Kage.rig.transform.parent.GetComponentsInChildren<ChainLine>().Length, "or the chain");
        }

        [Test]
        public void A_broken_sickle_hides_the_chain_too_and_it_comes_back_next_fight()
        {
            var f = Alone();
            var line = ChainVisual.LineOf(f.Kage.rig);
            f.Kage.BreakSword();
            line.Refresh();
            Assert.IsFalse(line.Renderer.enabled, "the chain goes with the blade");
            f.Kage.ResetForFight();
            line.Refresh();
            Assert.IsTrue(line.Renderer.enabled);
        }

        // ---- the full set from the buttons ----

        [TestCase("P", 0, 0, "chain.jab")]
        [TestCase("P", 1, 0, "chain.pull")]
        [TestCase("P", 0, -1, "chain.crouchJab")]
        [TestCase("K", 0, 0, "chain.kick1")]
        [TestCase("K", 1, 0, "chain.pushKick")]
        [TestCase("K", -1, 0, "chain.backKick")]
        [TestCase("K", 0, -1, "chain.sweep")]
        [TestCase("S", 0, 0, "chain.reap")]
        [TestCase("S", 1, 0, "chain.lash")]
        [TestCase("S", 0, -1, "chain.graveHook")]
        [TestCase("S", -1, 0, "chain.crescentLift")]
        public void Each_ground_button_and_direction_starts_its_chain_move(string button, int fwd, int dy, string expected)
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, fwd: fwd, dy: dy, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault());
        }

        [TestCase("P", 0, "chain.airJab")]
        [TestCase("K", 0, "chain.airKick")]
        [TestCase("S", 0, "chain.airReap")]
        public void Each_air_button_starts_its_chain_air_move(string button, int dy, string expected)
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, buttons: button);
            f.Run(60);
            Assert.AreEqual(expected, Started(f).FirstOrDefault(id => id.StartsWith("chain.air")));
        }

        [Test]
        public void Weapon_and_down_in_the_air_is_the_falling_dive()
        {
            var f = Alone();
            f.KageInput.Add(10, fwd: 1, dy: 1).Add(1, dy: -1, buttons: "S");
            f.Run(60);
            CollectionAssert.Contains(Started(f), "chain.shadowFall");
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
        public void Close_up_forward_and_punch_is_the_throw_and_further_away_it_is_the_pull()
        {
            var f = Duel(0.6f);
            f.Run(20);
            f.KageInput.Add(1, fwd: 1, buttons: "P");
            f.Run(30);
            Assert.AreEqual("throw", Started(f).FirstOrDefault(), "the shared shoulder throw by reference");
            Done(f);
            f = Duel(1.6f);
            f.Run(20);
            f.KageInput.Add(1, fwd: 1, buttons: "P");
            f.Run(30);
            Assert.AreEqual("chain.pull", Started(f).FirstOrDefault());
        }

        [Test]
        public void Against_a_downed_foe_down_and_punch_is_the_ground_hook()
        {
            var f = Duel(1.0f);
            f.Run(20);
            f.KageInput.Add(1, dy: -1, buttons: "K");
            f.RunUntil(() => f.Ronin.state == FState.Down, 120);
            Assert.AreEqual(FState.Down, f.Ronin.state, "the sweep puts the Ronin on the floor");
            f.RunUntil(() => f.Kage.state != FState.Attack, 80);
            f.KageInput.Add(1, dy: -1, buttons: "P");
            f.Run(30);
            CollectionAssert.Contains(Started(f), "chain.groundHook");
        }

        [Test]
        public void Cuts_chain_into_the_rising_reap_and_the_pull_into_the_reap()
        {
            var f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "S").Add(9).Add(1, buttons: "S");
            f.Run(100);
            CollectionAssert.AreEqual(new[] { "chain.reap", "chain.reapUp" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "S");
            f.Run(90);
            CollectionAssert.AreEqual(new[] { "chain.jab", "chain.reap" }, Started(f));
            Done(f);

            f = Alone();
            f.Run(10);
            f.KageInput.Add(1, fwd: 1, buttons: "P").Add(15).Add(1, buttons: "S");
            f.Run(100);
            CollectionAssert.AreEqual(new[] { "chain.pull", "chain.reap" }, Started(f), "the pull drags the foe in, the reap cuts him");
        }

        [Test]
        public void Every_move_of_the_set_can_be_reached_from_a_button_or_a_chain()
        {
            var f = Duel(1.5f);
            var set = (ChainMoves)f.Kage.moves;
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
            // the ground hook needs a downed foe and the throw a close one that can be thrown
            var missing = set.All.Except(reachable).Except(body).Except(new[] { set.groundHook, set.throwGrab }).ToList();
            CollectionAssert.IsEmpty(missing.Select(m => m.id));
        }

        // ---- the lash: mid range, a Blade line ----

        /// <summary>Metres from the body to the farthest Blade hit point while the window is on (root motion not counted).</summary>
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
                    if (k.moveFrame < w.from || k.moveFrame > w.to || !w.points.Contains(HitPoint.Blade)) continue;
                    best = Mathf.Max(best, ((Vector2)k.rig.World(B.Sword, k.BladeTipLocal) - k.Pos).x * k.facing);
                }
            }
            return best;
        }

        [Test]
        public void The_lash_reaches_mid_range_and_the_pull_a_little_further_but_both_stay_below_the_spear()
        {
            var f = Alone();
            var set = (ChainMoves)f.Kage.moves;
            float lash = MoveReach(f, set.lash), pull = MoveReach(f, set.pull), reap = MoveReach(f, set.reap);
            Debug.Log($"chain reach: lash {lash:0.00} m, pull {pull:0.00} m, reap {reap:0.00} m");
            Assert.That(lash, Is.GreaterThan(1.75f).And.LessThan(2.15f), "mid range: beyond the katana's 1.45 m, short of the spear's 2.25 m");
            Assert.Greater(lash, reap + 0.3f, "the lash is the long reach of the set");
            Assert.That(pull, Is.GreaterThan(lash).And.LessThan(2.2f));
            foreach (var m in set.All.Where(m => m.hits.Count > 0 && !m.isThrow).Distinct())
                Assert.Less(MoveReach(f, m), 2.2f, $"{m.id} stays below the spear's 2.25 m");
        }

        /// <summary>The farthest the lash lands on the training bag (metres between Kage's start and the bag).</summary>
        static float FarthestHit(string moveId, int level, string weapon = "yamigama")
        {
            for (float dist = 3.0f; dist > 0.8f; dist -= 0.04f)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag, 1.6f - dist, kageDef: KageWith(weapon, level));
                f.Run(4);
                f.Kage.StartMove(f.Kage.moves.All.First(m => m.id == moveId));
                f.Run(60);
                if (f.Events(CombatEventType.Hit).Any(e => e.SourceId == "kage" && e.MoveId == moveId)) return dist;
            }
            return 0f;
        }

        [Test]
        public void The_lash_lands_on_a_target_at_mid_range_and_a_higher_level_reaches_further()
        {
            float l1 = FarthestHit("chain.lash", 1), l8 = FarthestHit("chain.lash", 8);
            Debug.Log($"lash lands from up to {l1:0.00} m (level 1) and {l8:0.00} m (level 8)");
            Assert.Greater(l1, 1.7f, "the lash is a long one");
            float spear = FarthestHit("spear.thrust", 1, "mizuchi");
            Debug.Log($"the spear's thrust lands from up to {spear:0.00} m");
            Assert.Less(l1, spear - 0.1f, "mid range, below the spear (root motion included)");
            Assert.Greater(l8 - l1, 0.08f, "the chain runs further at level 8");
        }

        [Test]
        public void The_lash_and_the_cuts_are_blade_windows_and_so_can_clash_while_kicks_and_punches_cannot()
        {
            var f = Alone();
            var set = (ChainMoves)f.Kage.moves;
            foreach (var m in new[] { set.reap, set.reapUp, set.lash, set.pull, set.graveHook, set.crescentLift, set.groundHook, set.airReap, set.shadowFall })
                foreach (var w in m.hits)
                {
                    CollectionAssert.AreEqual(new[] { HitPoint.Blade }, w.points, m.id);
                    Assert.IsTrue(w.sword, m.id + " is a sword-class window (clash eligible)");
                }
            foreach (var m in new[] { set.kick1, set.kick2, set.pushKick, set.backKick, set.sweep, set.jab, set.crouchJab, set.airJab, set.airKick })
                foreach (var w in m.hits) CollectionAssert.DoesNotContain(w.points, HitPoint.Blade, m.id);
        }

        // ---- the chain is drawn and its length follows the move ----

        /// <summary>The sickle's distance from the hand (world metres) as the chain line last drew it.</summary>
        static float HeadDistance(HeadlessFight f)
        {
            var line = ChainVisual.LineOf(f.Kage.rig);
            line.Refresh();
            return line.HeadDistance;
        }

        [Test]
        public void The_chain_extends_during_the_lash_and_comes_back()
        {
            var f = Alone();
            f.Run(10);
            var line = ChainVisual.LineOf(f.Kage.rig);
            Assert.Less(HeadDistance(f), 0.01f, "at rest the sickle sits in the hand");
            float idleWidth = line.Renderer.bounds.size.x;

            f.KageInput.Add(1, fwd: 1, buttons: "S");
            float best = 0f, bestWidth = 0f;
            int atRest = 0;
            for (int i = 0; i < 60; i++)
            {
                f.Run(1);
                if (f.Kage.state != FState.Attack) { if (best > 0.1f) break; else continue; }
                float d = HeadDistance(f);
                if (d > best) { best = d; bestWidth = line.Renderer.bounds.size.x; }
                if (f.Kage.moveFrame > 28 && d < 0.01f) atRest++;
            }
            Debug.Log($"lash: sickle {best:0.00} m from the hand, chain {bestWidth:0.00} m wide (idle {idleWidth:0.00} m)");
            Assert.Greater(best, 0.75f, "the sickle flies out on the chain");
            Assert.Greater(bestWidth, best * 0.8f, "the drawn chain spans it");
            f.Run(5);
            Assert.Less(HeadDistance(f), 0.01f, "and is reeled back by the end");
            Assert.Greater(atRest, 0, "the chain is back before the move ends");
        }

        [Test]
        public void The_drawn_sickle_sits_at_the_end_of_the_hit_line()
        {
            var f = Alone();
            f.Run(10);
            f.Kage.StartMove(((ChainMoves)f.Kage.moves).lash);
            f.RunUntil(() => f.Kage.moveFrame >= 10, 40);
            var line = ChainVisual.LineOf(f.Kage.rig);
            line.Refresh();
            Vector2 pivot = f.Kage.rig.World(B.Sword, Vector2.zero);
            Vector2 tip = f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal);
            Assert.AreEqual(f.Kage.BladeExtension, line.HeadDistance, 0.02f, "the line draws the extension the hit line has");
            Assert.Greater((tip - pivot).magnitude, 1.2f);
        }

        [Test]
        public void The_chain_length_follows_the_playback_rate_and_reach_of_the_weapon_level()
        {
            float Peak(int level)
            {
                var f = Alone("yamigama", level);
                f.Run(10);
                f.Kage.StartMove(((ChainMoves)f.Kage.moves).lash);
                float best = 0f;
                for (int i = 0; i < 50 && f.Kage.state == FState.Attack; i++) { f.Run(1); best = Mathf.Max(best, f.Kage.BladeExtension); }
                Done(f);
                return best;
            }
            Assert.AreEqual(1.105f, Peak(8) / Peak(1), 1e-3f, "reach x1.105 at level 8 lengthens the chain by the same");
        }

        // ---- the pull goes through the physics knockback ----

        /// <summary>The Ronin's x position each frame from the pull's start until he has settled.</summary>
        static List<float> PullTrack(HeadlessFight f, int frames = 90)
        {
            f.Run(20);
            f.KageInput.Add(1, fwd: 1, buttons: "P");
            var xs = new List<float> { f.Ronin.Pos.x };
            for (int i = 0; i < frames; i++) { f.Run(1); xs.Add(f.Ronin.Pos.x); }
            return xs;
        }

        [Test]
        public void The_pull_drags_the_foe_towards_Kage_through_knockback_not_a_teleport()
        {
            var f = Duel(1.9f);
            var xs = PullTrack(f);
            Assert.AreEqual("chain.pull", Started(f).First());
            Assert.IsNotEmpty(f.Events(CombatEventType.Hit).Where(e => e.MoveId == "chain.pull"), "the pull lands");
            float before = xs[0] - f.Kage.Pos.x, after = xs.Last() - f.Kage.Pos.x;
            Debug.Log($"pull: {before:0.00} m -> {after:0.00} m");
            Assert.Greater(before - after, 0.8f, "the foe is moved well towards Kage");
            Assert.Less(after, 1.1f);
            Assert.Greater(after, 0.6f, "the push boxes keep him out of Kage");
            float maxStep = 0f;
            for (int i = 1; i < xs.Count; i++) maxStep = Mathf.Max(maxStep, Mathf.Abs(xs[i] - xs[i - 1]));
            Assert.Less(maxStep, 0.12f, "no frame moves him further than a knockback velocity does (no teleport)");
            Assert.AreEqual(FState.Idle, f.Ronin.state, "he is dragged in hitstun, not thrown or knocked down");
        }

        [Test]
        public void The_pull_is_deterministic()
        {
            string Run()
            {
                var f = Duel(1.9f);
                PullTrack(f);
                string s = string.Join("\n", f.Frames);
                Done(f);
                return s;
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void A_heavy_foe_is_dragged_less_than_a_light_one()
        {
            float Moved(CharacterDef foe)
            {
                var f = Duel(1.9f, foe: foe);
                var xs = PullTrack(f);
                Done(f);
                return xs[0] - xs.Last();
            }
            float light = Moved(null), heavy = Moved(CharacterCatalog.Get("test_heavy"));
            Assert.Greater(light, heavy + 0.2f, "knockback is divided by mass (docs/physics.md)");
            Assert.Greater(heavy, 0f, "but he still comes");
        }

        // ---- every move lands on the training bag ----

        [Test]
        public void Every_hitting_move_of_the_set_lands_on_the_training_bag()
        {
            var f = Alone();
            var set = (ChainMoves)f.Kage.moves;
            var notHits = new[] { set.roll, set.backflip, set.getUp, set.getUpFront, set.throwGrab, set.throwWhiff };
            var report = new List<string>();
            var failed = new List<string>();
            foreach (var m in set.All.Distinct().Except(notHits))
            {
                Assert.IsNotEmpty(m.hits, $"{m.id} has a hit window");
                if (SpearFamilyFightTests.LandsOnBag(m, "yamigama", out string where)) report.Add($"{m.id}: {where}");
                else failed.Add(m.id);
            }
            Debug.Log("lands on the bag:\n" + string.Join("\n", report));
            CollectionAssert.IsEmpty(failed, "moves that never hit the bag");
        }

        [Test]
        public void The_point_never_digs_into_the_floor_on_the_ground()
        {
            var f = Alone();
            var set = (ChainMoves)f.Kage.moves;
            foreach (var m in new[] { set.jab, set.reap, set.reapUp, set.lash, set.pull, set.graveHook, set.crescentLift, set.groundHook })
            {
                f.Kage.EquipWeapon("yamigama");
                f.Run(30);
                f.Kage.StartMove(m);
                float lowest = float.MaxValue;
                for (int i = 0; i < m.total + 4 && f.Kage.state == FState.Attack; i++)
                {
                    f.Run(1);
                    // while a hit window is on (the blend between two keys may swing the point through the floor for a frame or two)
                    if (m.hits.Any(w => f.Kage.moveFrame >= w.from && f.Kage.moveFrame <= w.to))
                        lowest = Mathf.Min(lowest, f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal).y);
                }
                Assert.Greater(lowest, -0.06f, $"{m.id}: the tip's lowest point");
            }
        }

        // ---- weapon levels scale it like any weapon ----

        static CombatEvent LashHit(HeadlessFight f)
        {
            f.KageInput.Add(1, fwd: 1, buttons: "S");
            f.Run(70);
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == "chain.lash").ToList();
            Assert.IsNotEmpty(hits, "the lash lands");
            return hits[0];
        }

        [Test]
        public void Yamigama_level_8_hits_harder_by_the_JS_amount()
        {
            var f = Duel(1.4f);
            var l1 = LashHit(f);
            Assert.AreEqual(l1.Crit ? 9f : 6f, l1.Damage, "lash: 6, crit 9");
            Done(f);
            f = Duel(1.4f, level: 8);
            var l8 = LashHit(f);
            Assert.AreEqual(l8.Crit ? 14f : 9f, l8.Damage, "lash x1.56: 9.36 -> 9, crit 14.04 -> 14");
        }

        [Test]
        public void Yamigama_level_8_plays_its_moves_faster()
        {
            int Frames(int level)
            {
                var f = Alone("yamigama", level);
                f.KageInput.Add(1, fwd: 1, buttons: "S");
                int frames = 0;
                for (int i = 0; i < 200; i++)
                {
                    f.Run(1);
                    if (f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == "chain.lash") frames++;
                    else if (frames > 0) break;
                }
                Done(f);
                return frames;
            }
            Assert.AreEqual(34, Frames(1), "34 authored frames at rate 1");
            Assert.AreEqual(29, Frames(8), "34 frames at x1.175");
        }

        [Test]
        public void Yamigama_level_8_has_a_longer_sickle_and_a_longer_chain()
        {
            var f1 = Alone("yamigama", 1);
            float l1 = f1.Kage.BladeReach;
            Done(f1);
            var f8 = Alone("yamigama", 8);
            float l8 = f8.Kage.BladeReach;
            Assert.AreEqual(ChainMoves.BladeLength, l1, 1e-3f);
            Assert.AreEqual(1.105f, l8 / l1, 1e-3f);
        }
    }

    /// <summary>The player's buttons are family-agnostic: J / L and the pad's X, A, Y reach the chain set through Select.</summary>
    public class SickleInputTests : InputTestFixture
    {
        [Test]
        public void Chain_buttons_work_from_the_keyboard_and_the_gamepad_input_source()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: SpearFamilyFightTests.KageWith("yamigama"));
            var set = (ChainMoves)f.Kage.moves;

            Press(kb.lKey); src.Sample(); Release(kb.lKey);
            Assert.IsTrue(src.Poll(f.Kage).s, "L is the weapon button");
            Assert.AreSame(set.reap, set.Select(Btn.S, 0, 0, false, f.Kage));
            Assert.AreSame(set.lash, set.Select(Btn.S, 1, 0, false, f.Kage), "with forward held");

            Press(gp.buttonNorth); src.Sample(); Release(gp.buttonNorth);
            Assert.IsTrue(src.Poll(f.Kage).s, "Y / triangle is the weapon button");
            Assert.AreSame(set.graveHook, set.Select(Btn.S, 0, -1, false, f.Kage), "with the stick down");

            Press(gp.buttonWest); src.Sample(); Release(gp.buttonWest);
            Assert.IsTrue(src.Poll(f.Kage).p, "X / square is punch");
            Press(gp.buttonSouth); src.Sample(); Release(gp.buttonSouth);
            Assert.IsTrue(src.Poll(f.Kage).k, "A / cross is kick");
        }
    }
}
