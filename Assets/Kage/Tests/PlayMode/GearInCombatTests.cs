using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// What Kage has equipped changes how he fights (SAN-84, ADR-0005): the loadout in the save reaches the fighter through the real
    /// spawn path, and its health, damage reduction, weapon level and helmet and armour perks act in a fight. Expected numbers are
    /// worked by hand from the JS data (see GearLoadoutTests).
    /// </summary>
    public class GearInCombatTests
    {
        const float Scale = 100f / 232f;
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static SaveData SaveWith(string weapon = "kagetsu", int weaponLevel = 1, string helmet = "shiro_maki", int helmetLevel = 1,
            string armor = "tachibana_yoroi", int armorLevel = 1, int level = 1)
        {
            var s = GameRules.DefaultSave(false);
            s.level = level;
            s.owned.weapons[weapon] = weaponLevel;
            s.owned.helmets[helmet] = helmetLevel;
            s.owned.armors[armor] = armorLevel;
            s.equipped.weapon = weapon;
            s.equipped.helmet = helmet;
            s.equipped.armor = armor;
            return s;
        }

        static CharacterDef Kage(SaveData s) => KageCharacter.Def(s);

        /// <summary>Kage and an idle Ronin close enough for a jab to land.</summary>
        HeadlessFight Duel(SaveData save, float roninX = 0.3f, float kageX = -0.3f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, roninX, 0f, kageDef: Kage(save));
            _fights.Add(f);
            return f;
        }

        void Done(HeadlessFight f)
        {
            f.Dispose();
            _fights.Remove(f);
        }

        static CombatEvent First(HeadlessFight f, string source, string move)
        {
            var hits = f.Events(CombatEventType.Hit).Where(e => e.SourceId == source && e.MoveId == move).ToList();
            Assert.IsNotEmpty(hits, source + " " + move + " lands");
            return hits[0];
        }

        /// <summary>Kage's first jab on an idle Ronin.</summary>
        (CombatEvent hit, float meter) KageJab(SaveData save)
        {
            var f = Duel(save);
            f.RoninInput.Add(600);
            f.KageInput.Add(1, buttons: "P");
            f.Run(30);
            var r = (First(f, "kage", "jab"), f.Kage.meter);
            Done(f);
            return r;
        }

        /// <summary>The Ronin's first jab on an idle Kage; also the frames Kage then spends in hit-stun and his meter.</summary>
        (CombatEvent hit, int stunFrames, float meter) RoninJab(SaveData save)
        {
            var f = Duel(save);
            f.KageInput.Add(600);
            f.RoninInput.Add(1, buttons: "P").Add(600);
            f.RunUntil(() => f.Events(CombatEventType.Hit).Any(e => e.SourceId == "ronin"), 40);
            var hit = First(f, "ronin", "jab");
            int stun = 0;
            for (int i = 0; i < 120 && stun < 100; i++)
            {
                if (f.Kage.state == FState.Hitstun) stun++;
                else if (stun > 0) break;
                f.Run(1);
            }
            var r = (hit, stun, f.Kage.meter);
            Done(f);
            return r;
        }

        // ---- health and damage reduction ----

        [Test]
        public void KagesHealthAndDefenceInAFightComeFromTheLoadout()
        {
            var cases = new[]
            {
                (SaveWith(), 232f, 0.03f),
                (SaveWith(helmet: "tetsu_kabuto", helmetLevel: 5, armor: "mugen_yoroi", armorLevel: 5), 317f, 0.346f),
                (SaveWith(helmet: "raijin_kabuto", helmetLevel: 5, armor: "shinsei_yoroi", armorLevel: 5), 410f, 0.5f),
                (SaveWith(level: 11), 272f, 0.03f),
            };
            foreach (var (save, jsHp, defence) in cases)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(save));
                Assert.AreEqual(jsHp * Scale, f.Kage.maxHealth, 1e-3f, save.equipped.helmet + "/" + save.equipped.armor);
                Assert.AreEqual(f.Kage.maxHealth, f.Kage.health, 1e-3f);
                Assert.AreEqual(defence, f.Kage.Defense, 1e-5f);
            }
        }

        [Test]
        public void UpgradedArmourLowersTheDamageKageTakes()
        {
            // the Ronin's jab is 4: 4 x (1 - 0.03) = 3.88 -> 4 (starter gear); x (1 - 0.346) = 2.6 -> 3; x (1 - 0.5) = 2
            var plain = RoninJab(SaveWith());
            var upgraded = RoninJab(SaveWith(helmet: "tetsu_kabuto", helmetLevel: 5, armor: "mugen_yoroi", armorLevel: 5));
            var capped = RoninJab(SaveWith(helmet: "raijin_kabuto", helmetLevel: 5, armor: "shinsei_yoroi", armorLevel: 5));
            float crit(CombatEvent e) => e.Crit ? 1.5f : 1f;
            Assert.AreEqual(Mathf.RoundToInt(4f * crit(plain.hit) * 0.97f), plain.hit.Damage);
            Assert.AreEqual(Mathf.RoundToInt(4f * crit(upgraded.hit) * 0.654f), upgraded.hit.Damage);
            Assert.AreEqual(Mathf.RoundToInt(4f * crit(capped.hit) * 0.5f), capped.hit.Damage);
            Assert.Less(upgraded.hit.Damage, plain.hit.Damage, "upgraded armour: less damage taken");
            Assert.Less(capped.hit.Damage, upgraded.hit.Damage);
        }

        // ---- weapon level ----

        [Test]
        public void ALevelEightWeaponDealsMoreDamage()
        {
            CombatEvent Iai(SaveData s)
            {
                var f = Duel(s, 0.37f, -0.37f);
                f.RoninInput.Add(600);
                f.KageInput.Add(1, buttons: "S");
                f.Run(60);
                var h = First(f, "kage", "iai");
                Assert.AreEqual(s.owned.weapons["kagetsu"], f.Kage.WeaponLevel);
                Done(f);
                return h;
            }
            var l1 = Iai(SaveWith("kagetsu", 1));
            var l8 = Iai(SaveWith("kagetsu", 8));
            Assert.Greater(l8.Damage, l1.Damage);
            Assert.GreaterOrEqual(l8.Damage, Mathf.Floor(l1.Damage * 1.5f), "+8 % a level: x1.56 at level 8");
        }

        // ---- perks ----

        [Test]
        public void TheDamagePerkAndPlayerLevelRaiseDamageDealt()
        {
            // Kage's jab is 4: x1.12 x1.12 (level 11, Raijin Kabuto 5 % + Mugen 7 % = 12 %) = 5.02 -> 5
            var plain = KageJab(SaveWith()).hit;
            var strong = KageJab(SaveWith(helmet: "raijin_kabuto", armor: "mugen_yoroi", level: 11)).hit;
            Assert.AreEqual(4f, plain.Damage);
            Assert.AreEqual(Mathf.RoundToInt(4f * 1.2544f * (strong.Crit ? 1.5f : 1f)), strong.Damage);
            Assert.Greater(strong.Damage, plain.Damage);
        }

        [Test]
        public void TheRagePerkRaisesTheMeterKageGainsFromHittingAndFromBeingHit()
        {
            // Oni Menpo: rage 0.2. A hit gives 5 + 0.6 x damage; being hit 3 + 0.3 x damage (Unity's meter, unchanged), times 1.2
            var plain = KageJab(SaveWith());
            var rage = KageJab(SaveWith(helmet: "oni_menpo"));
            Assert.AreEqual(plain.hit.Damage, rage.hit.Damage);
            Assert.AreEqual((5f + plain.hit.Damage * 0.6f), plain.meter, 1e-3f);
            Assert.AreEqual((5f + rage.hit.Damage * 0.6f) * 1.2f, rage.meter, 1e-3f);

            var hurtPlain = RoninJab(SaveWith());
            var hurtRage = RoninJab(SaveWith(helmet: "oni_menpo"));
            Assert.AreEqual(3f + hurtPlain.hit.Damage * 0.3f, hurtPlain.meter, 1e-3f);
            Assert.AreEqual((3f + hurtRage.hit.Damage * 0.3f) * 1.2f, hurtRage.meter, 1e-3f);
        }

        [Test]
        public void TheStunResistPerkShortensHitStun()
        {
            // Tetsu Kabuto: 30 % stun resist, hit-stun x (1 - 0.3 x 0.4)
            var plain = RoninJab(SaveWith());
            var tough = RoninJab(SaveWith(helmet: "tetsu_kabuto"));
            Assert.Greater(plain.stunFrames, 5, "the jab stuns him");
            Assert.Less(tough.stunFrames, plain.stunFrames);
            Assert.AreEqual(plain.stunFrames * 0.88f, tough.stunFrames, 1.5f);
        }

        [Test]
        public void TheAmmoPerkRaisesTheKunaiCountAndTheKunaiPerkTheirDamage()
        {
            // Sugegasa: +2 kunai. Raiko Armor: +40 % kunai damage (and 4 % speed): 3 x 1.4 = 4.2 -> 4
            var s = SaveWith(helmet: "sugegasa", armor: "raiko_yoroi");
            var f = Duel(s, 0.6f, HeadlessFight.KageX);
            Assert.AreEqual(7, f.Kage.KunaiMax);
            Assert.AreEqual(7, f.Kage.KunaiCount);
            f.RoninInput.Add(1200);
            f.KageInput.Add(1, dy: -1, buttons: "X").Add(60);
            f.Run(70);
            Assert.AreEqual(6, f.Kage.KunaiCount);
            var hit = First(f, "kage", "kunai");
            Assert.AreEqual(Mathf.RoundToInt(3f * 1.4f * (hit.Crit ? 1.5f : 1f)), hit.Damage);
            Done(f);

            var g = Duel(SaveWith(), 0.6f, HeadlessFight.KageX);
            Assert.AreEqual(5, g.Kage.KunaiMax);
            g.RoninInput.Add(1200);
            g.KageInput.Add(1, dy: -1, buttons: "X").Add(60);
            g.Run(70);
            Assert.AreEqual(3f, First(g, "kage", "kunai").Damage, "no perk: 3 as always");
        }

        [Test]
        public void TheSpeedPerkQuickensWalking()
        {
            // Yami Armor: +4 % speed, a retired enemy armour's -8 % slows him
            float Walk(SaveData s)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(s));
                float x0 = f.Kage.Pos.x;
                f.KageInput.Add(60, fwd: 1);
                f.Run(60);
                return f.Kage.Pos.x - x0;
            }
            float plain = Walk(SaveWith()), quick = Walk(SaveWith(armor: "yami_yoroi")), slow = Walk(SaveWith(armor: "kurogane_do"));
            Assert.Greater(quick, plain);
            Assert.Less(slow, plain);
            using var g = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(SaveWith(armor: "yami_yoroi")));
            Assert.AreEqual(1.04f, g.Kage.SpeedMul, 1e-6f);
        }

        /// <summary>Dash starts (frames) and the invulnerable frames of the first one, with a stream of dash taps.</summary>
        (List<int> starts, int invulnerable) Dashes(SaveData s)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(s));
            for (int i = 0; i < 200; i++) f.KageInput.Add(1, dash: true);
            var probe = new HitWindow { dmg = 1 };
            var starts = new List<int>();
            int invulnerable = 0;
            bool was = false;
            for (int i = 0; i < 200; i++)
            {
                f.Run(1);
                bool dash = f.Kage.state == FState.Dash;
                if (dash && !was) starts.Add(i);
                if (dash && starts.Count == 1 && !f.Kage.HittableBy(probe)) invulnerable++;
                was = dash;
            }
            return (starts, invulnerable);
        }

        [Test]
        public void TheDashPerkGivesInvulnerableFramesAndAShorterCooldown()
        {
            // Kuro Zukin 0.12 + Ronin Armor 0.1 = 0.22: 0.22 x 0.5 s = 6.6 -> 7 frames of invulnerability, cooldown x 0.78
            var plain = Dashes(SaveWith());
            var quick = Dashes(SaveWith(helmet: "kuro_zukin", armor: "ronin_yoroi"));
            Assert.GreaterOrEqual(plain.starts.Count, 3);
            Assert.AreEqual(0, plain.invulnerable, "a plain forward dash is hittable");
            Assert.AreEqual(7, quick.invulnerable);
            Assert.GreaterOrEqual(quick.starts.Count, 3);
            Assert.Less(quick.starts[2] - quick.starts[1], plain.starts[2] - plain.starts[1], "dashes come sooner after one another");
        }

        [Test]
        public void TheDashPerkExtendsTheInvulnerabilityOfARoll()
        {
            int RollInvulnerable(SaveData s)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(s));
                f.KageInput.Add(1, tapFwd: true).Add(100);
                var probe = new HitWindow { dmg = 1 };
                int n = 0;
                for (int i = 0; i < 80; i++)
                {
                    f.Run(1);
                    if (f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == "roll" && !f.Kage.HittableBy(probe)) n++;
                }
                return n;
            }
            int plain = RollInvulnerable(SaveWith());
            Assert.Greater(plain, 10, "a roll is untouchable for most of its length");
            Assert.AreEqual(plain + 7, RollInvulnerable(SaveWith(helmet: "kuro_zukin", armor: "ronin_yoroi")), "0.22 x 0.5 s = 7 more frames");
        }

        [Test]
        public void ThePerksThatNeedAMechanicNotBuiltYetAreHookedOnTheFighter()
        {
            // Kagehira Armor: blocking builds double rage (the block's rage gain belongs to the guard ticket)
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, kageDef: Kage(SaveWith(armor: "kagehira_yoroi")));
            Assert.AreEqual(2f, f.Kage.BlockRageMul, 1e-6f);
            Assert.AreEqual(1f, f.Kage.RageMul);
            using var g = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            Assert.AreEqual(1f, g.Kage.BlockRageMul);
            // the block's rage gain, JS damage x 0.4 x rage x (1 + blockRage), for the guard ticket to call
            Assert.AreEqual(10f * 0.4f * 2f, f.Kage.BlockRageGain(10f), 1e-5f);
            Assert.AreEqual(10f * 0.4f, g.Kage.BlockRageGain(10f), 1e-5f);
        }

        [Test]
        public void TheStarterLoadoutChangesNothingOnTheFighter()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            var k = f.Kage;
            Assert.AreEqual(100f, k.maxHealth, 1e-3f);
            Assert.AreEqual(1f, k.SpeedMul);
            Assert.AreEqual(1f, k.DamageMul);
            Assert.AreEqual(1f, k.RageMul);
            Assert.AreEqual(0f, k.StunRes);
            Assert.AreEqual(0f, k.DashBonus);
            Assert.AreEqual(5, k.KunaiMax);
        }

        [Test]
        public void ThePlayModeSuiteNeverReadsTheRealSaveFile()
        {
            Assert.IsNotNull(KageGame.SaveSource, "the isolation fixture sets it");
        }
    }
}
