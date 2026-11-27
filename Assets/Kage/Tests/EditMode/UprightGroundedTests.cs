using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Fighters stay upright and dodges stay on the floor (docs/combat-flow.md). No move of any weapon family, and no ability of any boss or
    /// sub-boss, turns the whole body round: there is no whole-body spin hook any more, and no pose key leans the hips past
    /// <see cref="MaxLean"/> (the kip-ups, which rise from lying, aside). Every dodge (the forward slide, the back step, the ground dash, Raikon's
    /// blinks) moves along X only: no vertical root motion, not airborne, no vault arc, and the hips hold one height through the slide.
    /// </summary>
    public class UprightGroundedTests
    {
        /// <summary>The most a pose key may lean the hips, degrees: a lunge or a flying kick, never a flip.</summary>
        const float MaxLean = 45f;

        static readonly string[] Families =
        {
            WeaponFamilies.Katana, WeaponFamilies.Fists, WeaponFamilies.Twin, WeaponFamilies.Spear,
            WeaponFamilies.Chain, WeaponFamilies.Cleaver, WeaponFamilies.Tachi,
        };

        /// <summary>The kip-ups start from lying on the floor (90 degrees and more) and rise to standing: not a spin.</summary>
        static bool RisesFromLying(MoveDef m) => m.id == "getUp" || m.id == "getUpFront";

        /// <summary>
        /// Every move a fighter can play: each family's set, every registered ability (each character is built once, which registers its
        /// abilities), every static move field of the runtime, and everything those lead to (chains, follow-ups, counters, braces, whiffs).
        /// </summary>
        static List<MoveDef> AllMoves()
        {
            foreach (var id in CharacterCatalog.Ids.ToList()) CharacterCatalog.Get(id);
            var found = new HashSet<MoveDef>();
            var todo = new Stack<MoveDef>();
            void Add(MoveDef m) { if (m != null && found.Add(m)) todo.Push(m); }

            var sets = new MoveSets();
            foreach (var fam in Families)
            {
                var set = sets.Get(fam);
                foreach (var m in set.All) Add(m);
                Add(set.roll); Add(set.backflip); Add(set.counterSlash);
            }
            foreach (var a in AbilityCatalog.All) Add(a.move);
            const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var t in typeof(Fighter).Assembly.GetTypes())
            {
                if (t.ContainsGenericParameters) continue;
                foreach (var f in t.GetFields(S)) if (f.FieldType == typeof(MoveDef)) Add(f.GetValue(null) as MoveDef);
                foreach (var p in t.GetProperties(S))
                    if (p.PropertyType == typeof(MoveDef) && p.GetIndexParameters().Length == 0 && p.GetMethod != null)
                        try { Add(p.GetValue(null) as MoveDef); } catch (TargetInvocationException) { }
            }
            while (todo.Count > 0)
            {
                var m = todo.Pop();
                Add(m.then); Add(m.onContact); Add(m.counterAnswer); Add(m.braceAnswer); Add(m.whiff);
                foreach (var c in m.chain.Values) Add(c);
            }
            return found.ToList();
        }

        [Test]
        public void MoveDef_has_no_whole_body_spin_hook()
        {
            var t = typeof(MoveDef);
            const BindingFlags I = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.IsNull(t.GetMethod("Spin", I), "no MoveDef.Spin");
            Assert.IsEmpty(t.GetFields(I).Where(f => f.Name.ToLowerInvariant().Contains("spin")).Select(f => f.Name), "no spin fields");
        }

        [Test]
        public void No_move_of_any_family_or_boss_turns_the_body_round()
        {
            var moves = AllMoves();
            Assert.Greater(moves.Count, 150, "the sweep finds the family sets and the boss abilities");
            foreach (var id in new[] { "raikon.thunderStep", "raikon.tempestWheel", "raikon.ascendantRush", HisameAbilities.KasumiMaiId, "airSlash", "twin.airSpin", "spear.airSweep" })
                Assert.IsTrue(moves.Any(m => m.id == id), $"the sweep covers {id}");
            var bad = new List<string>();
            foreach (var m in moves.Where(m => !RisesFromLying(m)))
                foreach (var k in m.keys)
                {
                    float lean = Mathf.Abs(KMath.Wrap(k.pose.a[(int)B.Hips]));
                    if (lean > MaxLean) bad.Add($"{m.id} key {k.frame}: hips at {k.pose.a[(int)B.Hips]:0} degrees");
                }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void The_kip_ups_only_rise_from_lying()
        {
            var set = new MoveSets().Get(WeaponFamilies.Katana);
            foreach (var m in new[] { set.getUp, set.getUpFront })
            {
                Assert.LessOrEqual(Mathf.Abs(m.keys[^1].pose.a[(int)B.Hips]), 10f, $"{m.id} ends standing (the stance's slight lean)");
                for (int i = 1; i < m.keys.Count; i++)
                    Assert.LessOrEqual(Mathf.Abs(m.keys[i].pose.a[(int)B.Hips]), Mathf.Max(10f, Mathf.Abs(m.keys[i - 1].pose.a[(int)B.Hips])),
                        $"{m.id} key {m.keys[i].frame}: only ever turns back towards upright");
            }
        }

        /// <summary>The forward slide and the back step of every family (every family shares the katana's).</summary>
        static IEnumerable<(string fam, MoveDef m, float dir)> Dodges()
        {
            var sets = new MoveSets();
            foreach (var fam in Families)
            {
                var s = sets.Get(fam);
                yield return (fam, s.roll, 1f);
                yield return (fam, s.backflip, -1f);
            }
        }

        [Test]
        public void Every_dodge_slides_flat_along_the_floor()
        {
            foreach (var (fam, m, dir) in Dodges())
            {
                string who = $"{fam}.{m.id}";
                Assert.IsFalse(m.airborne, $"{who}: never airborne");
                Assert.IsNotEmpty(m.motions, $"{who}: it travels");
                foreach (var e in m.motions)
                {
                    Assert.AreEqual(0f, e.vel.y, $"{who}: no vertical root motion");
                    Assert.IsFalse(e.setY, $"{who}: no jump impulse");
                    Assert.AreEqual(dir, Mathf.Sign(e.vel.x), $"{who}: one straight line, {(dir > 0 ? "forward" : "back")}");
                }
                Assert.Less(m.vaultTo, 0, $"{who}: no vault arc");
                Assert.Greater(m.invulnTo, m.invulnFrom, $"{who}: still invulnerable through the slide");

                // the hips hold one height while it slides (no bob): every key inside the root motion is within a centimetre of the others
                int from = m.motions.Min(e => e.from), to = m.motions.Max(e => e.to);
                var heights = m.keys.Where(k => k.frame > from + 2 && k.frame <= to).Select(k => k.pose.hip.y).ToList();
                Assert.GreaterOrEqual(heights.Count, 2, $"{who}: keyed through the slide");
                Assert.LessOrEqual(heights.Max() - heights.Min(), 0.01f, $"{who}: the hips don't bob");
                foreach (var k in m.keys)
                    Assert.LessOrEqual(Mathf.Abs(k.pose.a[(int)B.Hips]), 20f, $"{who} key {k.frame}: a lean, not a roll");
            }
        }

        [Test]
        public void The_dodges_snap_burst_glide_brake()
        {
            foreach (var (fam, m, _) in Dodges())
            {
                var speeds = m.motions.OrderBy(e => e.from).Select(e => Mathf.Abs(e.vel.x)).ToList();
                Assert.GreaterOrEqual(speeds.Count, 3, $"{fam}.{m.id}: a burst, a glide, a brake");
                for (int i = 1; i < speeds.Count; i++) Assert.Less(speeds[i], speeds[i - 1], $"{fam}.{m.id}: each phase slower than the last");
            }
        }

        [Test]
        public void Raikons_blinks_stay_on_the_floor()
        {
            CharacterCatalog.Get(Raikon.Id);
            foreach (var m in new[] { RaikonAscendant.ThunderStep, RaikonAscendant.AfterimageFeint, RaikonAscendant.BlinkFlurry, RaikonAscendant.BlinkFlurry2, RaikonAscendant.MirageSeverance })
            {
                Assert.GreaterOrEqual(m.vaultTo, 0, $"{m.id} blinks");
                Assert.AreEqual(0f, m.vaultHeight, $"{m.id}: a flat blink, no hop");
            }
        }
    }
}
