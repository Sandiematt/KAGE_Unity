using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The weapons' attack patterns (docs/weapon-styles.md): one style per shop weapon, each winning a different way, foe weapons
    /// untouched, and Shizuka's iai string over the katana set.
    /// </summary>
    public class WeaponStyleTests
    {
        /// <summary>The weapons with a pattern of their own; Mumei, the plain starter, has a style with no trait at all.</summary>
        static readonly string[] Shop = { "kagetsu", "shizuka", "tsubame", "onikiri", "mizuchi", "yamigama", "iwakudaki" };

        readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        Fighter Bare()
        {
            var go = new GameObject("StyleTestFighter");
            _objects.Add(go);
            return go.AddComponent<Fighter>();
        }

        [Test]
        public void Every_shop_weapon_has_a_style_with_its_own_name()
        {
            foreach (var w in Shop)
            {
                var s = WeaponStyles.Of(w);
                Assert.IsNotNull(s, w);
                Assert.AreEqual(w, s.weaponId);
                Assert.IsFalse(string.IsNullOrEmpty(s.name), w);
                Assert.IsFalse(string.IsNullOrEmpty(s.summary), w);
            }
            Assert.AreEqual(Shop.Length, Shop.Select(w => WeaponStyles.Of(w).name).Distinct().Count(), "seven styles, seven names");
            CollectionAssert.AreEquivalent(Shop.Append("mumei"), WeaponStyles.All.Select(s => s.weaponId));
        }

        [Test]
        public void Foe_weapons_and_bare_hands_have_no_style()
        {
            foreach (var w in new[] { "fists", "cutter_blade", "shadow_daggers", "mist_fans", "sentinel_spear", "temple_staff", "raider_cleaver", "chain_hook", "raikon_tachi", null })
                Assert.IsNull(WeaponStyles.Of(w), w ?? "null");
        }

        /// <summary>The traits a style carries, as a set of names: no two weapons may carry the same set.</summary>
        static string Traits(WeaponStyle s)
        {
            var t = new List<string>();
            if (s.HasRiposte) t.Add("riposte");
            if (s.echoes != null && s.echoes.Length > 0) t.Add("silentCut");
            if (s.buildMoves != null) t.Add("ownButtons");
            if (s.HasMomentum) t.Add("momentum");
            if (s.dashCancelOnHit) t.Add("dashCancel");
            if (s.armouredSwings) t.Add("armour");
            if (s.guardCrushChip > 0f) t.Add("guardCrush");
            if (s.heavyStep) t.Add("heavyStep");
            if (s.HasSweetSpot) t.Add("sweetSpot");
            if (s.HasHook) t.Add("hook");
            if (s.HasQuake) t.Add("tremor");
            return string.Join("+", t);
        }

        [Test]
        public void No_two_weapons_win_the_same_way()
        {
            var sets = Shop.Select(w => Traits(WeaponStyles.Of(w))).ToList();
            foreach (var (w, t) in Shop.Zip(sets, (a, b) => (a, b))) Assert.IsNotEmpty(t, w + " carries at least one trait");
            Assert.AreEqual(Shop.Length, sets.Distinct().Count(), string.Join(", ", sets));
            Assert.AreEqual(Shop.Length, Shop.Select(w => WeaponStyles.Of(w).footwork).Distinct().Count(), "each weapon's dash leaves its own trail");
        }

        [Test]
        public void The_plain_starter_carries_no_trait_and_the_basic_art()
        {
            var plain = WeaponStyles.Of("mumei");
            Assert.IsNotNull(plain);
            Assert.AreEqual("", Traits(plain), "Mumei is the katana and nothing else");
            Assert.AreEqual(Footwork.None, plain.footwork);
            Assert.AreEqual(1f, plain.moveMul);
            Assert.AreEqual(1f, plain.damageMul);
            Assert.IsNull(plain.buildMoves);
            Assert.AreEqual(Fighter.ThousandShadows, plain.rageArt);
            Assert.IsFalse(plain.legendary);
        }

        [Test]
        public void The_two_legendary_weapons_are_marked_and_no_other()
        {
            foreach (var s in WeaponStyles.All)
                Assert.AreEqual(s.weaponId == "yamigama" || s.weaponId == "iwakudaki", s.legendary, s.weaponId);
        }

        [Test]
        public void The_hammer_plays_its_smash_string_and_shakes_the_floor_on_its_impacts()
        {
            var cleaver = CleaverMoves.Build(KatanaMoves.Build());
            var set = (HammerMoves)WeaponStyles.Of("iwakudaki").buildMoves(cleaver);
            var self = Bare();
            Assert.AreSame(set.smash, set.Select(Btn.S, 0, 0, false, self), "a neutral Weapon press is Crushing Overhead Smash");
            Assert.AreSame(set.rise, set.smash.chain[Btn.S], "then Earth-Scoop Uppercut");
            Assert.AreSame(set.haymaker, set.rise.chain[Btn.S], "then Battering Haymaker");
            Assert.AreSame(set.shinBreaker, set.Select(Btn.S, 0, -1, false, self), "Down + Weapon is Shin-Breaker Drag");
            Assert.AreSame(set.meteorSunder, set.Select(Btn.S, 1, 0, false, self), "Forward + Weapon is Meteor Sunder");
            Assert.AreSame(set.risingAnvil, set.Select(Btn.S, 0, 1, false, self), "Up + Weapon is Rising Anvil");
            Assert.AreSame(cleaver.Select(Btn.P, 0, 0, false, self), set.Select(Btn.P, 0, 0, false, self));
            Assert.AreSame(cleaver.roll, set.roll);
            // the smash splits the ground on its impact frame, and is armoured up to its blow
            Assert.AreEqual(HammerMoves.SmashImpact, set.smash.projectileFrame);
            Assert.AreSame(HammerMoves.Fissure, set.smash.projectile);
            Assert.IsTrue(HammerMoves.Fissure.groundWave);
            Assert.AreEqual(HitLevel.Low, HammerMoves.Fissure.hit.level, "the split runs along the floor: guard it low");
            Assert.IsTrue(set.smash.Armoured(set.smash.hits[0].from - 1));
            Assert.That(HammerMoves.SmashImpact, Is.InRange(set.smash.hits[0].from, set.smash.hits[0].to));
            Assert.IsTrue(set.rise.hits[0].sword && set.smash.hits[0].sword && set.haymaker.hits[0].sword, "hammer blows are Blade windows: they clash");
            // the tremor fires on each quake move's impact
            var style = WeaponStyles.Of("iwakudaki");
            Assert.IsTrue(style.HasQuake);
            var meteorShock = set.meteorSunder.fx.First(e => e.kind == FxKind.Shockwave).from;
            foreach (var q in style.quakes)
            {
                if (q.moveId == HammerMoves.SmashId) Assert.AreEqual(HammerMoves.SmashImpact, q.frame);
                else { Assert.AreEqual(HammerMoves.MeteorSunderId, q.moveId); Assert.AreEqual(meteorShock, q.frame, "Meteor Sunder shakes the floor on impact"); }
            }
            Assert.Less(style.moveMul, WeaponStyles.Of("onikiri").moveMul, "the hammer is slower on its feet than the cleaver");
        }

        [Test]
        public void The_feet_follow_the_weight_of_the_weapon()
        {
            var twin = WeaponStyles.Of("tsubame");
            var cleaver = WeaponStyles.Of("onikiri");
            var katana = WeaponStyles.Of("kagetsu");
            Assert.Greater(twin.moveMul, katana.moveMul, "the twin blades walk and run faster than the reference");
            Assert.Greater(twin.dashMul, 1f);
            Assert.Less(cleaver.moveMul, katana.moveMul, "the cleaver is the slowest on its feet");
            Assert.Less(cleaver.dashMul, 1f);
            Assert.IsTrue(cleaver.heavyStep);
            Assert.AreEqual(1f, katana.moveMul, "Kagetsu is the reference: its feet are Kage's own");
            Assert.AreEqual(1f, katana.dashMul);
            Assert.AreEqual(1f, katana.damageMul);
            Assert.AreEqual(1f, katana.postureMul);
        }

        [Test]
        public void The_traits_have_sane_numbers()
        {
            var twin = WeaponStyles.Of("tsubame");
            Assert.That(twin.tempoPerHit, Is.InRange(0.02f, 0.1f));
            Assert.That(twin.tempoMax, Is.InRange(0.2f, 0.5f), "momentum caps well short of double speed");
            Assert.Less(twin.postureMul, 1f, "many light blows: less posture each");
            var cleaver = WeaponStyles.Of("onikiri");
            Assert.IsTrue(cleaver.armouredSwings);
            Assert.That(cleaver.guardCrushChip, Is.InRange(0.1f, 0.5f));
            Assert.Greater(cleaver.postureMul, 1f);
            var spear = WeaponStyles.Of("mizuchi");
            Assert.That(spear.tipZone, Is.InRange(0.2f, 0.5f));
            Assert.Greater(spear.tipDamage, 1f);
            Assert.Less(spear.innerDamage, 1f, "a foe inside the shaft takes less");
            Assert.Less(spear.innerZone + spear.tipZone, 1f, "the inner part and the sweet spot don't overlap");
            var chain = WeaponStyles.Of("yamigama");
            Assert.That(chain.hookFrames, Is.InRange(30, 180));
            Assert.Greater(chain.hookDamage, 1f);
            Assert.Greater(chain.hookPull, 0f);
            foreach (var w in new[] { "kagetsu", "shizuka" })
            {
                var s = WeaponStyles.Of(w);
                Assert.AreEqual(45, s.riposteFrames, w + ": the JS 0.75 s window after a guard or a parry");
                Assert.IsNotNull(s.riposte, w);
            }
        }

        [Test]
        public void The_iai_counters_are_the_fastest_and_heaviest_blows()
        {
            var katana = KatanaMoves.Build();
            var kc = WeaponStyles.KagetsuCounter;
            var sc = ShizukaMoves.Counter;
            Assert.Less(kc.hits[0].from, katana.iai.hits[0].from, "Kagetsu's counter lands before its Iai Draw");
            Assert.Less(sc.hits[0].from, kc.hits[0].from, "Shizuka's is the fastest draw in the Armory");
            foreach (var m in new[] { kc, sc })
            {
                Assert.IsTrue(m.hits[0].critical, m.id + " always crits");
                Assert.IsTrue(m.hits[0].sword, m.id + " is a blade blow: it can clash");
                Assert.IsTrue(m.Invulnerable(1), m.id + " can't be traded with on its first frames");
                Assert.Greater(m.hits[0].dmg, katana.iai.hits[0].dmg, m.id + " hits harder than a plain draw");
            }
        }

        [Test]
        public void Shizuka_plays_the_Weapon_button_as_four_draws_and_every_other_button_as_the_katana()
        {
            var katana = KatanaMoves.Build();
            var set = (ShizukaMoves)WeaponStyles.Of("shizuka").buildMoves(katana);
            var self = Bare();
            Assert.AreSame(set.draw1, set.Select(Btn.S, 0, 0, false, self), "a neutral Weapon press draws");
            Assert.AreSame(set.draw2, set.draw1.chain[Btn.S]);
            Assert.AreSame(set.draw3, set.draw2.chain[Btn.S]);
            Assert.AreSame(set.noto, set.draw3.chain[Btn.S]);
            Assert.IsFalse(set.noto.chain.ContainsKey(Btn.S), "sheathing ends the string");
            // every other button: the katana set's own move, by reference
            Assert.AreSame(katana.Select(Btn.P, 0, 0, false, self), set.Select(Btn.P, 0, 0, false, self));
            Assert.AreSame(katana.Select(Btn.K, 0, 0, false, self), set.Select(Btn.K, 0, 0, false, self));
            Assert.AreSame(katana.Select(Btn.S, 1, 0, false, self), set.Select(Btn.S, 1, 0, false, self), "forward + Weapon: Shadow Step");
            Assert.AreSame(katana.Select(Btn.S, -1, 0, false, self), set.Select(Btn.S, -1, 0, false, self), "back + Weapon: Mirror Guard");
            Assert.AreSame(katana.Select(Btn.S, 0, -1, false, self), set.Select(Btn.S, 0, -1, false, self), "down + Weapon: Crown Wheel");
            Assert.AreSame(katana.Select(Btn.S, 0, 0, true, self), set.Select(Btn.S, 0, 0, true, self), "in the air: the katana's");
            Assert.AreSame(katana.roll, set.roll);
            Assert.AreSame(katana.counterSlash, set.counterSlash);
            foreach (var m in new[] { set.draw1, set.draw2, set.draw3, set.noto, ShizukaMoves.Counter })
                CollectionAssert.Contains(set.All.ToList(), m);
        }

        [Test]
        public void Shizukas_first_draw_is_the_quickest_first_blow_and_the_noto_sheathes_after_its_cut()
        {
            var katana = KatanaMoves.Build();
            var set = new ShizukaMoves(katana);
            Assert.Less(set.draw1.hits[0].from, katana.iai.hits[0].from, "Nukitsuke lands before Kagetsu's Iai Draw");
            Assert.AreEqual(2, set.draw3.hits.Count, "Tsuki strikes twice");
            var noto = set.noto;
            Assert.AreEqual(ShizukaMoves.NotoId, noto.id);
            Assert.AreEqual(ShizukaMoves.NotoSheathFrame, noto.sheathFrom);
            Assert.Less(noto.hits.Max(h => h.to), noto.sheathFrom, "the blade clicks home after the cut, never during it");
            Assert.Less(noto.sheathTo, noto.total, "and it is drawn again before the move ends");
            Assert.IsFalse(noto.Sheathed(noto.sheathFrom - 1));
            Assert.IsTrue(noto.Sheathed(noto.sheathFrom));
            Assert.Less(ShizukaMoves.Counter.hits.Max(h => h.to), ShizukaMoves.Counter.sheathFrom);
            var echo = WeaponStyles.Of("shizuka").echoes;
            Assert.AreEqual(2, echo.Length);
            Assert.AreEqual(ShizukaMoves.NotoId, echo[0].moveId);
            Assert.AreEqual(ShizukaMoves.NotoSheathFrame, echo[0].sheathFrame);
            Assert.AreEqual(12, echo[0].damage, "the JS sz4 echo");
            Assert.AreEqual(ShizukaMoves.CounterId, echo[1].moveId);
            Assert.AreEqual(18, WeaponStyles.Of("shizuka").echoDelay, "the JS 0.3 s");
        }

        [Test]
        public void A_move_without_a_scabbard_is_never_sheathed()
        {
            foreach (var m in KatanaMoves.Build().All)
                for (int f = 0; f <= m.total; f++)
                    Assert.IsFalse(m.Sheathed(f), m.id);
        }
    }
}
