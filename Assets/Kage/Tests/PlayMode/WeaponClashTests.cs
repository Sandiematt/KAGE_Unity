using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Weapon clashes (SAN-93, ADR-0004 step 1). Fight-harness runs: Kage and a scripted foe press their buttons on known frames at a
    /// known distance, so the blades' windows overlap and the swept blades cross (or pass, or aren't blades at all). The
    /// distances and delays are measured scenarios (a move's reach and timing are the move set's business); every clash
    /// number is read from <see cref="ClashTuning"/> (Resources/Tuning/clash.json), never typed here.
    /// </summary>
    public class WeaponClashTests
    {
        readonly List<HeadlessFight> _fights = new();
        static ClashTuning T => Tuning.Get<ClashTuning>();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef Kage(string weapon = null)
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            if (weapon != null) d.weaponId = weapon;
            return d;
        }

        /// <summary>The Ronin with <paramref name="weapon"/> (his own timing aside, he fights with that family's set).</summary>
        static CharacterDef Foe(string weapon)
        {
            var d = CharacterCatalog.Get(global::Kage.Ronin.Id);
            d.weaponId = weapon;
            return d;
        }

        static void S(ScriptedInput s) => s.Add(1, buttons: "S");
        static void SDown(ScriptedInput s) => s.Add(1, dy: -1, buttons: "S");
        static void SFwd(ScriptedInput s) => s.Add(1, fwd: 1, buttons: "S");
        static void P(ScriptedInput s) => s.Add(1, buttons: "P");
        static void K(ScriptedInput s) => s.Add(1, buttons: "K");

        /// <summary>Kage presses <paramref name="kage"/> on frame 1 + <paramref name="delay"/>, the foe presses <paramref name="foe"/> on frame 1; <paramref name="gap"/> metres apart.</summary>
        HeadlessFight Duel(float gap, Action<ScriptedInput> kage, Action<ScriptedInput> foe, string kageWeapon = null, string foeWeapon = "kagetsu", int delay = 0)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, global::Kage.Tests.Foe.Scripted, -gap / 2f, gap / 2f, 0f, foeDef: Foe(foeWeapon), kageDef: Kage(kageWeapon));
            f.KageInput.Add(1 + delay); kage(f.KageInput); f.KageInput.Add(600);
            f.RoninInput.Add(1); foe(f.RoninInput); f.RoninInput.Add(600);
            _fights.Add(f);
            return f;
        }

        static CombatEvent TheClash(HeadlessFight f) => f.Events(CombatEventType.Clashed).Single();

        /// <summary>Runs until the clash, or the window of the attacks is over.</summary>
        static HeadlessFight Clashed(HeadlessFight f)
        {
            f.RunUntil(() => f.Log.Count(CombatEventType.Clashed) > 0, 90);
            return f;
        }

        // ---------------------------------------------------------------- two crossing blades clash

        [Test]
        public void Two_katana_cuts_that_cross_clash_and_neither_hits()
        {
            var f = Clashed(Duel(1.8f, S, S));
            f.Run(60);
            Assert.AreEqual(1, f.Log.Count(CombatEventType.Clashed));
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit), "no hit");
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Blocked));
            Assert.AreEqual(f.Kage.maxHealth, f.Kage.health, "Kage took no damage");
            Assert.AreEqual(f.Ronin.maxHealth, f.Ronin.health, "the Ronin took no damage");
        }

        [Test]
        public void The_clash_event_names_both_fighters_both_moves_and_where_the_blades_met()
        {
            var f = Clashed(Duel(1.8f, S, S));
            var e = TheClash(f);
            Assert.AreSame(f.Kage, e.Source, "the earlier fighter in fight order is the source");
            Assert.AreSame(f.Ronin, e.Target);
            Assert.AreEqual("iai", e.MoveId);
            Assert.AreEqual("iai", e.Name, "the target's move");
            Assert.AreEqual(0, e.Window);
            Assert.AreEqual(0f, e.Damage);
            Assert.AreEqual(T.posture, e.Posture, 1e-4f);
            Assert.That(e.Contact.y, Is.InRange(0.4f, 2f), "at blade height");
            Assert.That(e.Contact.x, Is.InRange(-2f, 2f));
            StringAssert.Contains(" Clashed kage>ronin iai#0 dmg=0 pos=", e.ToLine());
        }

        [Test]
        public void Both_fighters_recoil_apart_in_hitstun_for_the_set_frames()
        {
            var f = Duel(1.8f, S, S);
            f.RunUntil(() => f.Log.Count(CombatEventType.Clashed) > 0, 90);
            Assert.AreEqual(FState.Hitstun, f.Kage.state);
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
            Assert.IsNull(f.Kage.move, "the move is over");
            Assert.IsNull(f.Ronin.move);
            float k0 = f.Kage.Pos.x, r0 = f.Ronin.Pos.x;
            // hit-stop passes (the loop frames it freezes don't run), then the recoil carries each away from the other
            f.RunUntil(() => !f.Ctx.Clock.InHitstop, 60);
            f.Run(4);
            Assert.Less(f.Kage.Pos.x, k0 - 0.02f, "Kage is thrown back (left)");
            Assert.Greater(f.Ronin.Pos.x, r0 + 0.02f, "the Ronin is thrown back (right)");
            Assert.AreEqual(FState.Hitstun, f.Kage.state);
            f.Run(T.recoilFrames + 2);
            Assert.AreNotEqual(FState.Hitstun, f.Kage.state, "the recoil ends after recoilFrames");
            Assert.AreNotEqual(FState.Hitstun, f.Ronin.state);
        }

        [Test]
        public void Both_fighters_lose_the_set_posture()
        {
            var f = Clashed(Duel(1.8f, S, S));
            Assert.AreEqual(T.posture, f.Kage.Posture, 1e-4f);
            Assert.AreEqual(T.posture, f.Ronin.Posture, 1e-4f);
        }

        [Test]
        public void The_clash_freezes_the_fight_for_the_set_hitstop()
        {
            var f = Duel(1.8f, S, S);
            f.RunUntil(() => f.Log.Count(CombatEventType.Clashed) > 0, 90);
            Assert.IsTrue(f.Ctx.Clock.InHitstop);
            int frozen = 0;
            while (!f.Loop.StepHeadless() && frozen < 100) frozen++;
            Assert.AreEqual(T.hitstopFrames, frozen);
        }

        [Test]
        public void The_clash_plays_the_placeholder_cue_where_the_blades_met()
        {
            var seen = new List<(string id, Vector2 pos)>();
            void Rec(string id, Vector2 pos, in CueArgs a) => seen.Add((id, pos));
            EffectCues.Played += Rec;
            HeadlessFight f;
            try { f = Clashed(Duel(1.8f, S, S)); }
            finally { EffectCues.Played -= Rec; }
            Assert.IsTrue(EffectCues.IsRegistered(ClashCues.Clash), "VFX_Kage_Clash is registered");
            Assert.AreEqual("VFX_Kage_Clash", seen.Single(c => c.id.StartsWith("VFX_Kage_Clash")).id);
            Assert.AreEqual(TheClash(f).Contact, seen.Single(c => c.id == ClashCues.Clash).pos);
        }

        [Test]
        public void A_low_reap_against_a_low_reap_clashes_too()
        {
            var f = Clashed(Duel(1.6f, SDown, SDown));
            Assert.AreEqual("crownWheel", TheClash(f).MoveId);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
        }

        [Test]
        public void A_full_posture_bar_breaks_the_guard_and_that_is_published_after_the_clash()
        {
            var f = Duel(1.8f, S, S);
            f.Kage.AddPosture(f.Kage.PostureMax - 1f, f.Ronin);
            f.RunUntil(() => f.Log.Count(CombatEventType.Clashed) > 0, 90);
            f.Run(1);
            Assert.IsTrue(f.Kage.GuardBroken, "the clash filled Kage's bar");
            Assert.IsFalse(f.Ronin.GuardBroken);
            var types = f.Log.All.Select(e => e.Type).Where(t => t is CombatEventType.Clashed or CombatEventType.GuardBroken).ToArray();
            CollectionAssert.AreEqual(new[] { CombatEventType.Clashed, CombatEventType.GuardBroken }, types);
            var gb = f.Events(CombatEventType.GuardBroken).Single();
            Assert.AreSame(f.Kage, gb.Target);
            Assert.AreSame(f.Ronin, gb.Source);
            Assert.AreEqual("iai", gb.MoveId, "the foe's blade broke it");
            Assert.AreEqual(TheClash(f).Frame, gb.Frame, "on the same frame");
        }

        // ---------------------------------------------------------------- what is not a clash

        [Test]
        public void Blades_that_pass_each_other_without_crossing_are_a_near_miss_not_a_clash()
        {
            // at 1.0 m both iai draws dash through each other, each blade passing the other on the far side of the body
            var f = Duel(1.0f, S, S);
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
            Assert.Greater(f.Log.Count(CombatEventType.Hit), 0, "the cuts land as an ordinary trade");
        }

        [Test]
        public void Windows_too_far_apart_to_meet_do_not_clash()
        {
            var f = Duel(3.4f, S, S);
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
        }

        [TestCase(0.6f)]
        [TestCase(1.0f)]
        [TestCase(1.4f)]
        public void A_blade_against_a_punch_never_clashes(float gap)
        {
            var f = Duel(gap, S, P);
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
            Assert.Greater(f.Log.Count(CombatEventType.Hit), 0, "the blade or the fist hit");
        }

        [TestCase(1.0f)]
        [TestCase(1.8f)]
        public void A_blade_against_a_kick_never_clashes(float gap)
        {
            var f = Duel(gap, S, K);
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
        }

        [Test]
        public void A_punch_against_a_blade_never_clashes()
        {
            var f = Duel(1.0f, P, S);
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
        }

        [Test]
        public void Fists_against_a_blade_never_clash()
        {
            // the fists family has no Blade point at all (ADR-0006)
            var f = Duel(1.0f, P, S, kageWeapon: "fists");
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
            Assert.Greater(f.Log.Count(CombatEventType.Hit), 0);
        }

        [Test]
        public void Fighters_on_the_same_team_never_clash()
        {
            var f = Duel(1.8f, S, S);
            f.Ronin.team = f.Kage.team;
            f.Run(60);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Clashed));
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit), "and they don't hit each other either");
        }

        // ---------------------------------------------------------------- the weapon families

        [Test]
        public void A_spear_thrust_against_a_spear_thrust_clashes()
        {
            var f = Clashed(Duel(1.6f, P, P, kageWeapon: "mizuchi", foeWeapon: "mizuchi"));
            Assert.AreEqual("spear.jab", TheClash(f).MoveId);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
        }

        [Test]
        public void The_chain_lash_clashes_beyond_the_reach_of_the_blade()
        {
            // at 2 m only the lash (the blade line run out along the chain) meets the foe's blade
            var f = Duel(2.0f, SFwd, S, kageWeapon: "yamigama", delay: 1);
            Fighter kage = f.Kage;
            var lashTips = new List<float>();
            for (int i = 0; i < 90 && f.Log.Count(CombatEventType.Clashed) == 0; i++)
            {
                f.Run(1);
                for (int k = f.Ctx.Sweeps.Count - 1; k >= 0; k--)
                    if (f.Ctx.Sweeps[k].owner == kage) { lashTips.Add(Mathf.Abs(f.Ctx.Sweeps[k].to.x - kage.Pos.x)); break; }
            }
            var e = TheClash(f);
            Assert.AreEqual("chain.lash", e.MoveId);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
            Assert.Greater(lashTips.Max(), kage.BladeReach + 0.7f, "the lash's tip reached well past the blade's own length");
        }

        [Test]
        public void The_near_blade_of_a_twin_pair_clashes_when_the_far_blade_does_not()
        {
            // twin.fang1 cuts with the near blade (BladeN) level across the front; the far blade is held up behind. The spear's
            // thrust crosses only the near one, so a clash here means BladeN samples count as blade samples (ADR-0006, SAN-77).
            var f = Duel(1.6f, S, S, kageWeapon: "tsubame", foeWeapon: "mizuchi", delay: 4);
            var runs = new List<ClashGeometry.BladeRun>();
            bool nearCrossed = false, farCrossed = false;
            for (int i = 0; i < 90 && f.Log.Count(CombatEventType.Clashed) == 0; i++)
            {
                f.Run(1);
                runs.Clear();
                ClashGeometry.CollectBlades(f.Ctx.Sweeps, runs);
                foreach (var a in runs.Where(r => r.owner == f.Kage))
                    foreach (var b in runs.Where(r => r.owner == f.Ronin))
                        if (ClashGeometry.BladesCross(f.Ctx.Sweeps, a.start, a.end, b.start, b.end, out _))
                        {
                            if (f.Ctx.Sweeps[a.start].point == HitPoint.BladeN) nearCrossed = true; else farCrossed = true;
                        }
            }
            Assert.AreEqual(1, f.Log.Count(CombatEventType.Clashed));
            Assert.IsTrue(nearCrossed, "the near blade crossed the spear");
            Assert.IsFalse(farCrossed, "the far blade didn't");
            Assert.AreEqual("twin.fang1", TheClash(f).MoveId);
            Assert.AreEqual(0, f.Log.Count(CombatEventType.Hit));
        }

        // ---------------------------------------------------------------- repeatable

        [Test]
        public void The_same_clash_replays_identically()
        {
            string[] Play()
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, global::Kage.Tests.Foe.Scripted, -0.9f, 0.9f, 0f, foeDef: Foe("kagetsu"));
                f.KageInput.Add(1); S(f.KageInput); f.KageInput.Add(600);
                f.RoninInput.Add(1); S(f.RoninInput); f.RoninInput.Add(600);
                f.Run(120);
                Assert.AreEqual(1, f.Log.Count(CombatEventType.Clashed));
                return f.Log.Lines().Concat(f.Frames).ToArray();
            }
            CollectionAssert.AreEqual(Play(), Play());
        }
    }
}
