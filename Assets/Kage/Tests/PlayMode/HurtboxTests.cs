using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Hurtboxes and swept hit casts (SAN-65): hurt circles are trigger colliders on the Hurtbox layer,
    /// and each active hit point is swept from its last-frame to its this-frame position.
    /// </summary>
    public class HurtboxTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Fight(Foe foe, float kageX = 0f, float roninX = 0.6f) { var f = new HeadlessFight(KageMoveScenarios.Seed, foe, kageX, roninX); _fights.Add(f); return f; }

        static Hurtbox HurtboxAt(Vector2 p)
        {
            Hurtbox.SyncAll();
            var c = Physics2D.OverlapPoint(p, PhysicsLayers.HurtboxMask);
            return c != null ? c.GetComponent<Hurtbox>() : null;
        }

        // ---------------------------------------------------------------------------------------
        [Test]
        public void EveryHurtCircleOfAFighterIsAHurtboxOnItsBone()
        {
            var f = Fight(Foe.Scripted, HeadlessFight.KageX);
            f.RoninInput.Add(1, fwd: 1);
            f.Run(20);
            Hurtbox.SyncAll();

            foreach (var fighter in new[] { f.Kage, f.Ronin })
            {
                // by owner, not by hierarchy: a fighter in a physical fall (SAN-113) has its bones on ragdoll bodies outside its object
                var boxes = Object.FindObjectsByType<Hurtbox>(FindObjectsSortMode.None).Where(b => ReferenceEquals(b.Owner, fighter)).ToArray();
                Assert.AreEqual(fighter.def.hurt.Count, boxes.Length, fighter.FighterId);
                foreach (var b in boxes)
                {
                    var h = fighter.def.hurt[b.Index];
                    Assert.AreSame(fighter, b.Owner);
                    Assert.AreEqual(h.zone, b.Zone);
                    Assert.AreEqual(PhysicsLayers.Hurtbox, b.gameObject.layer);
                    Assert.IsTrue(b.Collider.isTrigger);
                    Assert.AreSame(fighter.rig.Bone(h.bone), b.transform.parent, "parented to its bone");
                    Vector2 c = fighter.rig.World(h.bone, h.local);
                    Assert.AreEqual(c.x, b.Collider.bounds.center.x, 1e-4f, $"{fighter.FighterId} hurt {b.Index} x");
                    Assert.AreEqual(c.y, b.Collider.bounds.center.y, 1e-4f, $"{fighter.FighterId} hurt {b.Index} y");
                    // the hurt radius is in world metres, whatever the fighter's scale (the Ronin is 1.04)
                    Assert.AreEqual(h.radius, b.Collider.bounds.extents.y, 1e-4f, $"{fighter.FighterId} hurt {b.Index} radius");
                }
            }
        }

        [Test]
        public void FightersAndTheBagAreFoundThroughTheSameHurtboxQuery()
        {
            var duel = Fight(Foe.Scripted, HeadlessFight.KageX).Run(2);
            var bag = Fight(Foe.Bag).Run(2);

            var head = duel.Ronin.def.hurt.First(h => h.zone == HitZone.Head);
            var ronin = HurtboxAt(duel.Ronin.rig.World(head.bone, head.local));
            Assert.IsNotNull(ronin);
            Assert.AreSame(duel.Ronin, ronin.Owner);
            Assert.AreEqual(HitZone.Head, ronin.Zone);

            var top = HurtboxAt(bag.Bag.BagPosition + new Vector2(0f, -0.14f));
            Assert.IsNotNull(top);
            Assert.AreSame(bag.Bag, top.Owner);
            Assert.AreEqual(HitZone.Head, top.Zone);
            var bottom = HurtboxAt(bag.Bag.BagPosition + new Vector2(0f, -0.14f - 3 * 0.29f));
            Assert.AreSame(bag.Bag, bottom.Owner);
            Assert.AreEqual(HitZone.Legs, bottom.Zone);
        }

        [Test]
        public void HurtboxesTouchNothingInTheSimulation()
        {
            FightBuilder.ConfigurePhysics();
            for (int layer = 0; layer < 32; layer++)
                Assert.IsTrue(Physics2D.GetIgnoreLayerCollision(PhysicsLayers.Hurtbox, layer), $"layer {layer}");
        }

        // ---------------------------------------------------------------------------------------
        //  fast moves: a target between two frames' sample points is still hit
        // ---------------------------------------------------------------------------------------
        public sealed class FastMove
        {
            public string move;
            public System.Action<ScriptedInput> script;
            public int frames;
            /// <summary>The move's hit points step further in one frame than their reach, so point tests can miss.</summary>
            public bool tunnels;
            public override string ToString() => move;
        }

        static IEnumerable<FastMove> FastMoves()
        {
            yield return new FastMove { move = "cleave", tunnels = true, frames = 120, script = s => s.Add(1, buttons: "S").Add(10).Add(1, buttons: "S").Add(10).Add(1, buttons: "S") };
            yield return new FastMove { move = "shadowStep", frames = 80, script = s => s.Add(1, fwd: 1, buttons: "S") };
            yield return new FastMove { move = "airKick", frames = 80, script = s => s.Add(10, fwd: 1, dy: 1).Add(1, buttons: "K") };
        }

        const float ProbeRadius = 0.005f;

        [Test, TestCaseSource(nameof(FastMoves))]
        public void AFastMoveHitsATargetItsPathCrossesBetweenTwoFrames(FastMove fm)
        {
            // 1. where the move's hit points go, with nothing to hit
            var sweeps = new List<HitSweep>();
            using (var dry = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f))
            {
                fm.script(dry.KageInput);
                for (int i = 0; i < fm.frames; i++)
                {
                    dry.Run(1);
                    sweeps.AddRange(dry.Ctx.Sweeps.Where(s => s.owner == dry.Kage));
                }
            }
            var own = sweeps.Where(s => s.move.id == fm.move).ToList();
            Assert.IsNotEmpty(own, $"{fm.move} never had an active hit point");

            // 2. the point between two frames' sample points farthest from every sampled position
            Vector2 target = default;
            float clear = float.MinValue;
            foreach (var s in own)
                for (float u = 0.3f; u <= 0.71f; u += 0.05f)
                {
                    Vector2 c = Vector2.Lerp(s.from, s.to, u);
                    float d = sweeps.Min(o => Vector2.Distance(o.to, c) - o.radius);
                    if (d > clear) { clear = d; target = c; }
                }
            float step = own.Max(s => Vector2.Distance(s.from, s.to));
            string what = $"{fm.move}: longest step {step:0.000} m, reach {own[0].radius:0.000} m, gap {clear:0.000} m";
            bool gap = clear > ProbeRadius + 0.002f;
            if (fm.tunnels) Assert.IsTrue(gap, $"{what}: expected a gap that point tests step over");

            // 3. the sweep hits it, exactly once ...
            using (var swept = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f))
            {
                var probe = HurtProbe.Place(swept.Root.transform, target, ProbeRadius);
                fm.script(swept.KageInput);
                swept.Run(fm.frames);
                Assert.AreEqual(1, probe.Hits.Count(h => h.moveId == fm.move), $"{what}: should hit the target once");
                Assert.AreEqual(1, swept.Events(CombatEventType.Hit).Count(e => e.MoveId == fm.move && e.SourceId == "kage"), what);
            }

            // 4. ... where the legacy point tests step over it (when the move is fast enough to leave a gap)
            if (!gap) return;
            Tuning.Override(new CombatTuning { legacyHitResolution = true });
            using (var legacy = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f))
            {
                var probe = HurtProbe.Place(legacy.Root.transform, target, ProbeRadius);
                legacy.Kage.extraTarget = probe;
                fm.script(legacy.KageInput);
                legacy.Run(fm.frames);
                Assert.IsFalse(probe.Hits.Any(h => h.moveId == fm.move), $"{what}: the legacy point tests should miss it");
            }
        }

        // ---------------------------------------------------------------------------------------
        [Test]
        public void EachHitWindowHitsATargetAtMostOnce()
        {
            // a target that every hit point of every frame of the chain touches
            var f = Fight(Foe.None);
            var probe = HurtProbe.Place(f.Root.transform, f.Kage.Pos + new Vector2(0.4f, 1.0f), 1.6f);
            f.KageInput.Add(1, buttons: "S").Add(10).Add(1, buttons: "S").Add(10).Add(1, buttons: "S");
            f.Run(120);

            var perWindow = probe.Hits.GroupBy(h => (h.moveId, h.window)).ToDictionary(g => g.Key, g => g.Count());
            CollectionAssert.AreEquivalent(new[] { ("iai", 0), ("risingMoon", 0), ("cleave", 0) }, perWindow.Keys);
            foreach (var kv in perWindow) Assert.AreEqual(1, kv.Value, $"{kv.Key} hit more than once");
        }

        [Test]
        public void NoReplayWindowHitsATargetTwice()
        {
            foreach (var s in KageMoveScenarios.All())
            {
                using var f = s.Play(KageMoveScenarios.Seed);
                var moveIndex = new Dictionary<string, int>();
                var seen = new HashSet<(string, int, string, int, string)>();
                int n = 0;
                foreach (var e in f.Log.All)
                {
                    n++;
                    if (e.Type == CombatEventType.MoveStarted) { moveIndex[e.SourceId] = n; continue; }
                    if (e.Type is not (CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Countered) || e.Window < 0) continue;
                    moveIndex.TryGetValue(e.SourceId, out int start);
                    Assert.IsTrue(seen.Add((e.SourceId, start, e.MoveId, e.Window, e.TargetId)), $"{s.name}: {e.ToLine()} is a second hit of one window");
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        [Test]
        public void HitsNeverLandOnTheSameTeam()
        {
            var f = Fight(Foe.Scripted, -0.37f, 0.37f);
            f.Ronin.team = f.Kage.team;
            f.KageInput.Add(1, buttons: "P");
            f.Run(40);
            Assert.IsEmpty(f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage"));
            Assert.AreEqual(100f, f.Ronin.health);
        }
    }
}
