using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Weapon families in a fight (SAN-70, ADR-0006): the equipped weapon picks the move set, its level scales damage,
    /// playback speed and blade reach, and the character's move rate and damage multiplier vary one shared set.
    /// Kagetsu at level 1 playing exactly as before is the baseline replay's job.
    /// </summary>
    public class WeaponFamilyFightTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static CharacterDef KageWith(string weapon = "kagetsu", int level = 1, float moveRate = 1f, float damageMul = 1f)
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.weaponId = weapon;
            d.weaponLevel = level;
            d.moveRate = moveRate;
            d.damageMul = damageMul;
            return d;
        }

        /// <summary>Kage and an idle Ronin at the baseline replay's distance.</summary>
        HeadlessFight Duel(CharacterDef kage = null, CharacterDef foe = null, float meter = 0f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, meter, foeDef: foe, kageDef: kage);
            f.RoninInput.Add(1200);
            _fights.Add(f);
            return f;
        }

        /// <summary>Disposes a fight now, so the next fight of the same test never meets its bodies or hurtboxes.</summary>
        void Done(HeadlessFight f)
        {
            f.Dispose();
            _fights.Remove(f);
        }

        static CombatEvent FirstKageHit(HeadlessFight f, string move)
        {
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == "kage" && e.MoveId == move).ToList();
            Assert.IsNotEmpty(hits, move + " lands");
            return hits[0];
        }

        /// <summary>Kage's iai on the idle Ronin: the damage of its first hit.</summary>
        CombatEvent IaiHit(CharacterDef kage, CharacterDef foe = null)
        {
            var f = Duel(kage, foe);
            f.KageInput.Add(1, buttons: "S");
            f.Run(60);
            var hit = FirstKageHit(f, "iai");
            Done(f);
            return hit;
        }

        /// <summary>Frames Kage spends in a move started by <paramref name="buttons"/> on the first frame, alone (no hitstop).</summary>
        int FramesInMove(CharacterDef kage, string buttons, string move)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: kage);
            _fights.Add(f);
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

        // ---- the weapon picks the family ----

        [Test]
        public void Kage_starts_with_Kagetsu_at_level_1_in_the_katana_family()
        {
            var f = Duel();
            Assert.AreEqual("kagetsu", f.Kage.WeaponId);
            Assert.AreEqual(1, f.Kage.WeaponLevel);
            Assert.AreEqual("katana", f.Kage.Family.id);
            Assert.IsInstanceOf<KatanaMoves>(f.Kage.moves);
            Assert.AreEqual(1f, f.Kage.PlaybackRate);
            Assert.AreEqual(f.Kage.def.bladeTipLocal, f.Kage.BladeTipLocal, "Kagetsu's blade points are the drawn katana's");
        }

        [Test]
        public void Bare_hands_fight_with_the_fists_family_and_the_existing_fist_moves()
        {
            var f = Duel(KageWith("fists"));
            Assert.AreEqual("fists", f.Kage.Family.id);
            Assert.IsFalse(f.Kage.rig.SwordVisible, "no katana drawn");
            f.KageInput.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(10).Add(1, buttons: "P").Add(60).Add(1, dy: -1, buttons: "P");     // the uppercut has floored the foe: down + punch is the ground strike
            f.Run(200);
            var started = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.AreEqual(new[] { "jab", "crossFist", "uppercut", "groundPound" }, started);
        }

        [Test]
        public void Bare_hands_have_no_blade_moves_and_no_two_handed_grip()
        {
            var f = Duel(KageWith("fists"), meter: 100f);
            f.Run(30);
            Assert.Greater(TwoHandedGripTests.NearFistToHandle(f.Kage), 0.1f, "fists up, no grip on a handle");
            f.KageInput.Add(1, buttons: "X").Add(30).Add(1, fwd: -1, buttons: "S");
            f.Run(60);
            var started = f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();
            CollectionAssert.DoesNotContain(started, "crescent");
            CollectionAssert.DoesNotContain(started, "counterStance");
        }

        [Test]
        public void Switching_weapon_mid_fight_swaps_the_move_set_and_the_drawing()
        {
            var f = Duel();
            f.Kage.EquipWeapon("fists");
            Assert.IsInstanceOf<FistsMoves>(f.Kage.moves);
            Assert.IsFalse(f.Kage.rig.SwordVisible);
            f.Kage.EquipWeapon("shizuka", 3);
            Assert.AreEqual("katana", f.Kage.Family.id);
            Assert.AreEqual(3, f.Kage.WeaponLevel);
            Assert.IsTrue(f.Kage.rig.SwordVisible);
            Assert.AreSame(f.Ronin.moves, f.Kage.moves, "one katana set per fight, shared");
        }

        [Test]
        public void A_broken_sword_comes_back_as_bare_hands_when_the_family_has_no_blade()
        {
            var f = Duel(KageWith("fists"));
            f.Kage.BreakSword();
            Assert.IsFalse(f.Kage.SwordBroken, "nothing to break");
            f.Kage.ResetForFight();
            Assert.IsFalse(f.Kage.rig.SwordVisible, "restoring the sword doesn't draw a katana in bare hands");
        }

        // ---- weapon level: damage, speed, reach ----

        [Test]
        public void Kagetsu_level_8_hits_harder_than_level_1_by_the_JS_amount()
        {
            var l1 = IaiHit(KageWith(level: 1));
            Assert.AreEqual(l1.Crit ? 14f : 9f, l1.Damage, "iai: 9, crit 13.5 -> 14");
            var l8 = IaiHit(KageWith(level: 8));
            Assert.AreEqual(l8.Crit ? 21f : 14f, l8.Damage, "iai x1.56: 14.04 -> 14, crit 21.06 -> 21");
        }

        [Test]
        public void Kagetsu_level_8_plays_its_moves_faster()
        {
            Assert.AreEqual(30, FramesInMove(KageWith(level: 1), "S", "iai"), "30 authored frames at rate 1");
            Assert.AreEqual(26, FramesInMove(KageWith(level: 8), "S", "iai"), "30 frames at x1.175");
        }

        [Test]
        public void Kagetsu_level_8_reaches_further_with_the_blade()
        {
            var f1 = Duel(KageWith(level: 1));
            var l1 = f1.Kage.BladeReach;
            Done(f1);
            var l8 = Duel(KageWith(level: 8)).Kage.BladeReach;
            Assert.AreEqual(1.105f, l8 / l1, 1e-3f);
        }

        // ---- character move rate and damage multiplier on a shared set ----

        [Test]
        public void A_characters_move_rate_slows_the_family_set_without_copying_it()
        {
            var f = Duel(KageWith(moveRate: 0.9f));
            Assert.AreSame(f.Ronin.moves, f.Kage.moves, "Kage at 0.9 and the Ronin at 1 share one katana set");
            Done(f);
            Assert.AreEqual(60, FramesInMove(KageWith(moveRate: 0.5f), "S", "iai"), "30 frames at half rate");
        }

        [Test]
        public void At_half_rate_the_hit_lands_later_and_still_once()
        {
            int Offset(float rate, out int hits)
            {
                var f = Duel(KageWith(moveRate: rate));
                f.KageInput.Add(1, buttons: "S");
                f.Run(90);
                int start = f.Events(CombatEventType.MoveStarted).First(e => e.SourceId == "kage").Frame;
                hits = f.Events(CombatEventType.Hit).Count(e => e.SourceId == "kage");
                int offset = FirstKageHit(f, "iai").Frame - start;
                Done(f);
                return offset;
            }
            int full = Offset(1f, out int fullHits);
            int half = Offset(0.5f, out int halfHits);
            Assert.AreEqual(1, fullHits);
            Assert.AreEqual(1, halfHits, "one hit per window at any rate");
            Assert.GreaterOrEqual(half, 2 * full - 2, $"rate 1 lands {full} frames in, rate 0.5 {half}");
        }

        [TestCase(0.5f)]
        [TestCase(1.5f)]
        public void One_frame_triggers_fire_exactly_once_at_any_rate(float rate)
        {
            int flights = 0;
            void Count(string id, Vector2 pos, in CueArgs args) { if (id == CrescentCues.Flight) flights++; }
            EffectCues.Played += Count;
            try
            {
                var f = Duel(KageWith(moveRate: rate), meter: 40f);
                f.KageInput.Add(1, buttons: "X");
                f.Run(90);
                Assert.AreEqual(1, f.Events(CombatEventType.MoveStarted).Count(e => e.SourceId == "kage" && e.MoveId == "crescent"));
            }
            finally { EffectCues.Played -= Count; }
            Assert.AreEqual(1, flights, "the Crescent leaves once");
        }

        [Test]
        public void A_characters_damage_multiplier_scales_the_family_set()
        {
            var e = IaiHit(KageWith(damageMul: 2f));
            Assert.AreEqual(e.Crit ? 27f : 18f, e.Damage);
        }

        [Test]
        public void The_defenders_defence_reduces_the_damage()
        {
            var ronin = CharacterCatalog.Get(Ronin.Id);
            ronin.defense = 0.25f;
            var e = IaiHit(KageWith(), ronin);
            Assert.AreEqual(e.Crit ? 10f : 7f, e.Damage, "9 x 0.75 = 6.75 -> 7; crit 10.125 -> 10");
        }
    }

    /// <summary>The dev arena's weapon hotkey (F8, Shift+F8 for the level).</summary>
    public class WeaponHotkeyTests : InputTestFixture
    {
        GameObject _game;

        public override void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator F8CyclesThePlayersWeaponAndShiftF8ItsLevel()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            Assert.AreEqual("kagetsu", game.player.WeaponId);
            Press(kb.f8Key);
            yield return null;
            Release(kb.f8Key);
            yield return null;
            Assert.AreEqual("mizuchi", game.player.WeaponId, "the next registered player weapon: the spear family's Mizuchi");
            Assert.AreEqual("spear", game.player.Family.id);
            Press(kb.leftShiftKey);
            yield return null;
            Press(kb.f8Key);
            yield return null;
            Release(kb.f8Key);
            Release(kb.leftShiftKey);
            yield return null;
            Assert.AreEqual(2, game.player.WeaponLevel);
            int cycled = 0;
            while (game.player.WeaponId != "fists" && cycled++ < 20) game.CycleWeapon();
            Assert.AreEqual("fists", game.player.WeaponId, "the cycle wraps to bare hands");
            var list = WeaponFamilies.PlayableWeapons();
            Assert.AreEqual(list.Length - System.Array.IndexOf(list, "mizuchi"), cycled, "from the second weapon on through the rest of the list");
            Assert.AreEqual("fists", game.player.Family.id);
        }
    }
}
