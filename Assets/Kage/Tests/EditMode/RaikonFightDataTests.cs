using System.Linq;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon, the Lightning Ascendant (SAN-107), the Act I boss, as data: his character entry (JS stats, the tachi set, the boss profile, heavy
    /// armour, no-grab), his three phases (the JS table, the second wave and aggression in the last), the numbers of his four abilities, and the
    /// blade-length override that puts his hit line on his painted blade. The expected numbers are the JS game's (10-data.js ENEMIES.raikon,
    /// RAIKON_SKYFALL, 40-ai.js thunderSwing, stormBreak, skyfall and redlineCounter, 41-styles/raikon.js RAIKON_STYLE.redline, 30-combat.js
    /// PROJ.thunderwave and PROJ.shock). How he fights is in RaikonFightTests.
    /// </summary>
    public class RaikonFightDataTests
    {
        const string Id = "raikon";
        const float Px = 1f / 115f;      // metres per JS pixel (ADR-0010)
        const float Scale = 1.24f;       // his JS build scale
        const double Tell = 0.93;        // JS ai.p.windup for a boss on Normal: Normal 1 x the boss role's 0.93

        static CharacterDef Def() => CharacterCatalog.Get(Id);

        static AbilityDef Ability(string id)
        {
            Def();     // registers his abilities
            var a = AbilityCatalog.Get(id);
            Assert.IsNotNull(a, id + " is registered");
            return a;
        }

        static int F(double s) => Mathf.RoundToInt((float)(s * 60.0));

        // ---------------------------------------------------------------- character

        [Test]
        public void Raikon_has_his_JS_stats()
        {
            var d = Def();
            Assert.AreEqual("Raikon", d.displayName);
            Assert.AreEqual("the Lightning Ascendant", d.title);
            Assert.AreEqual(640f, d.jsHealth, "10-data.js and 41-styles/raikon.js: base 640 hp");
            Assert.AreEqual(640f * 1.4f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.16f, d.defense, 1e-6);
            Assert.AreEqual(1.1f, d.damageMul, 1e-6);
            Assert.AreEqual(0.9f, d.speedMul, 1e-6);
            Assert.AreEqual(Scale, d.bodyScale, 1e-6);
            Assert.AreEqual(CharacterFlags.Armour | CharacterFlags.NoGrab, d.flags & (CharacterFlags.Armour | CharacterFlags.NoGrab), "heavy armour and no-grab");
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void He_fights_with_Raikon_tachi_on_the_tachi_set_and_the_boss_profile()
        {
            var d = Def();
            Assert.AreEqual("raikon_tachi", d.weaponId);
            Assert.AreEqual(WeaponFamilies.Tachi, d.family);
            Assert.AreEqual("boss", d.aiProfile);
            Assert.Greater(d.mass, CharacterCatalog.Get(Garan.Id).mass, "the heaviest of Act I: he is pushed back least");
            Assert.AreEqual(100f / 0.75f, d.postureMax, 1e-3, "a boss's 100, filling 0.75 times as fast (JS poise 0.75: a quarter harder to stagger)");
        }

        [Test]
        public void His_abilities_are_the_JS_four_in_slot_order()
        {
            var d = Def();
            Assert.AreEqual(Id, d.bossId);
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "skyfall", "redlineCounter" }, d.abilities);
        }

        // ---------------------------------------------------------------- phases

        [Test]
        public void His_boss_data_is_the_three_JS_phases()
        {
            var b = BossCatalog.Get(Id);
            Assert.IsNotNull(b);
            Assert.AreEqual("Raikon", b.name);
            Assert.AreEqual("the Lightning Ascendant", b.title);
            Assert.AreEqual(3, b.Count);

            Assert.AreEqual("The Storm Gathers", b.phases[0].name);
            Assert.AreEqual(1f, b.phases[0].at);
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "skyfall" }, b.phases[0].abilities);
            Assert.AreEqual(0f, b.phases[0].secondWave);
            Assert.IsFalse(b.phases[0].aggressive);

            Assert.AreEqual("Redline", b.phases[1].name);
            Assert.AreEqual(0.66f, b.phases[1].at, "the JS threshold");
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "redlineCounter", "skyfall" }, b.phases[1].abilities, "phase two adds Redline Counter");
            Assert.AreEqual(0f, b.phases[1].secondWave);
            Assert.IsFalse(b.phases[1].aggressive);

            Assert.AreEqual("Lightning Ascendant", b.phases[2].name);
            Assert.AreEqual(0.33f, b.phases[2].at, "the JS threshold");
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "redlineCounter", "skyfall" }, b.phases[2].abilities);
            Assert.AreEqual(0.7f, b.phases[2].secondWave, 1e-6, "a late second Thunder Swing wave");
            Assert.IsTrue(b.phases[2].aggressive, "and the aggressive flag");
            Assert.IsFalse(b.phases.Any(p => p.berserk));
            Assert.IsTrue(b.phases.All(p => p.aiProfile == null), "he stays on the boss profile");
        }

        // ---------------------------------------------------------------- the generated numbers

        [Test]
        public void The_generated_tables_carry_the_numbers_his_abilities_read()
        {
            var ts = JsTables.BossAbility("thunderSwing");
            Assert.AreEqual(7, ts.Cd, 1e-9, "41-styles/raikon.js: zoning, thrown less often than the base 5.2");
            CollectionAssert.AreEqual(new[] { 210.0, 520.0 }, ts.Range);
            Assert.AreEqual(0.6, ts.Chance, 1e-9);

            var sb = JsTables.BossAbility("stormBreak");
            Assert.AreEqual(6.5, sb.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 0.0, 340.0 }, sb.Range);
            Assert.AreEqual(0.5, sb.Chance, 1e-9);

            var sf = JsTables.BossAbility("skyfall");
            Assert.AreEqual(7, sf.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 460.0, 2400.0 }, sf.Range);
            Assert.AreEqual(0.8, sf.Chance, 1e-9);

            var rl = JsTables.BossAbility("redlineCounter");
            Assert.AreEqual(6.5, rl.Cd, 1e-9);
            CollectionAssert.AreEqual(new[] { 60.0, 300.0 }, rl.Range);
            Assert.AreEqual(0.3, rl.Chance, 1e-9);

            var s = JsTables.RaikonSkyfall;
            Assert.AreEqual(0.5, s.Charge, 1e-9);
            Assert.AreEqual(0.5, s.Fall, 1e-9);
            Assert.AreEqual(130, s.Width, 1e-9);
            Assert.AreEqual(21, s.Dmg, 1e-9);
            Assert.AreEqual(1.1, s.Zap, 1e-9);

            var r = JsTables.RaikonRedline;
            CollectionAssert.AreEqual(new[] { 1.5, 1.25 }, r.Stance);
            Assert.AreEqual(0.24, r.Settle, 1e-9);
            Assert.AreEqual(0.1, r.Punish, 1e-9);
            Assert.AreEqual(17, r.Dmg, 1e-9);
            Assert.AreEqual(0.62, r.Spent, 1e-9);
            Assert.AreEqual(38, r.SpentPosture, 1e-9);

            var w = JsTables.Projectile("thunderwave");
            Assert.AreEqual(640, w.Speed, 1e-9);
            Assert.AreEqual(15, w.Dmg, 1e-9);
            Assert.IsTrue(w.Ground && w.Unb && w.Kd, "a ground wave: unblockable, knocks down");
            Assert.AreEqual(0.75, JsTables.FightStyle(Id).Poise, 1e-9);
        }

        [Test]
        public void His_abilities_have_the_JS_cooldown_range_and_chance()
        {
            var ts = Ability("thunderSwing");
            Assert.AreEqual(420, ts.cooldownFrames, "7 s");
            Assert.AreEqual(210f * Px, ts.range.x, 1e-4);
            Assert.AreEqual(520f * Px, ts.range.y, 1e-4);
            Assert.AreEqual(0.6f, ts.chance, 1e-6);

            var sb = Ability("stormBreak");
            Assert.AreEqual(390, sb.cooldownFrames, "6.5 s");
            Assert.AreEqual(0f, sb.range.x, 1e-4);
            Assert.AreEqual(340f * Px, sb.range.y, 1e-4);
            Assert.AreEqual(0.5f, sb.chance, 1e-6);

            var sf = Ability("skyfall");
            Assert.AreEqual(420, sf.cooldownFrames, "7 s");
            Assert.AreEqual(460f * Px, sf.range.x, 1e-4, "only from far: his answer to a Kage who keeps his distance");
            Assert.AreEqual(2400f * Px, sf.range.y, 1e-3);
            Assert.AreEqual(0.8f, sf.chance, 1e-6);

            var rl = Ability("redlineCounter");
            Assert.AreEqual(390, rl.cooldownFrames, "6.5 s");
            Assert.AreEqual(60f * Px, rl.range.x, 1e-4);
            Assert.AreEqual(300f * Px, rl.range.y, 1e-4);
            Assert.AreEqual(0.3f, rl.chance, 1e-6);
        }

        // ---------------------------------------------------------------- the moves

        [Test]
        public void Thunder_Swing_is_an_armoured_wind_up_then_a_blockable_cut_and_an_unblockable_ground_wave()
        {
            var m = Ability("thunderSwing").move;
            int w = F(0.66 * Tell);
            Assert.AreEqual(37, w, "0.66 x Normal 1 x the boss role's 0.93");
            var cut = m.hits.Single();
            Assert.AreEqual(w, cut.from, "released after the wind-up");
            Assert.AreEqual(w + F(0.6), m.total, "the wind-up + the JS 0.6 s");
            Assert.AreEqual(12, cut.dmg, "the JS 15, x 0.8");
            Assert.AreEqual(HitLevel.Mid, cut.level, "the cut itself can be blocked (JS: no unb)");
            Assert.IsTrue(m.Armoured(0) && m.Armoured(w - 1), "the JS super armour through the wind-up");
            Assert.IsFalse(m.Armoured(w), "off at the release");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAbilities.BladeChargeCue && c.frame < w), "red sparks on the blade through the wind-up");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAbilities.GlintCue && c.frame == w - F(0.2)), "a glint 0.2 s before the release");

            var wave = m.projectile;
            Assert.IsNotNull(wave, "the wave is a projectile (ADR-0007)");
            Assert.AreEqual(w, m.projectileFrame, "thrown with the cut");
            Assert.IsTrue(wave.groundWave, "along the floor");
            Assert.AreEqual(HitLevel.Unblockable, wave.hit.level);
            Assert.IsTrue(wave.hit.knockdown);
            Assert.AreEqual(640f * Px, wave.speed, 1e-3);
            Assert.AreEqual(30f * Px, wave.radius, 1e-4);
            Assert.AreEqual(F(1.7), wave.lifeFrames, "the JS wave's 1.7 s");
            Assert.AreEqual(12, wave.hit.dmg, "the JS 15, x 0.8");
            Assert.AreEqual(110f * Scale * Px, m.projectileFrom.x, 1e-3, "starting 110 px x his scale in front");
            Assert.AreEqual(ThunderWaveCueId, wave.flightCue);
            Assert.IsEmpty(m.extraThrows, "one wave until the last phase");
        }

        const string ThunderWaveCueId = "VFX_Raikon_Thunder_Wave";

        [Test]
        public void In_the_last_phase_the_blade_is_drawn_back_again_and_a_second_cut_and_wave_follow_late()
        {
            var a = Ability("thunderSwing");
            var early = a.move;
            Assert.IsNull(RaikonAbilities.SecondWaveSwing(0f), "no second wave before the last phase");
            Assert.IsNotNull(early.variantFor, "the move asks the fighter's phase which swing to start");
            var m = RaikonAbilities.SecondWaveSwing(0.7f);
            Assert.IsNotNull(m, "the last phase starts the two-wave swing");
            Assert.AreNotSame(early, m);
            int w = F(0.66 * Tell), delay = F(0.7 * System.Math.Max(1.0, Tell));
            Assert.AreEqual(42, delay, "the JS secondWave 0.7 s x max(1, tell)");
            Assert.AreEqual(2, m.hits.Count, "two cuts");
            Assert.AreEqual(w, m.hits[0].from);
            Assert.AreEqual(w + delay, m.hits[1].from, "the second a beat later: it punishes a dodge made too early");
            Assert.AreEqual(w + delay + F(0.6), m.total);
            Assert.AreEqual(w, m.projectileFrame);
            Assert.AreEqual(1, m.extraThrows.Count, "and a second wave");
            Assert.AreEqual(w + delay, m.extraThrows[0].frame);
            Assert.AreSame(m.projectile, m.extraThrows[0].def, "the same wave");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAbilities.GlintCue && c.frame == w + delay - F(0.2)), "the blade glints again before the second");
            Assert.IsFalse(m.Armoured(w + F(0.2)), "no armour for the second charge (JS: the super armour is dropped at the first release)");
        }

        [Test]
        public void Storm_Break_is_an_armoured_overhead_wind_up_then_a_slam_that_lays_the_shock()
        {
            var m = Ability("stormBreak").move;
            int land = F(0.58 * Tell);
            Assert.AreEqual(32, land);
            var slam = m.hits.Single();
            Assert.AreEqual(land, slam.from);
            Assert.AreEqual(land + F(0.75), m.total, "the wind-up + the JS 0.75 s");
            Assert.AreEqual(15, slam.dmg, "19, x 0.8");
            Assert.AreEqual(HitLevel.Overhead, slam.level);
            Assert.IsTrue(slam.knockdown);
            Assert.AreEqual(1.1f * 60f, slam.dmg * 1.5f * slam.postureMul, 1e-3, "JS gb 110 of the JS guard's 100: a guarded slam breaks Kage's 60");
            Assert.IsTrue(m.Armoured(0) && m.Armoured(land));
            Assert.IsFalse(m.Armoured(land + 1));

            var h = m.hazard;
            Assert.IsNotNull(h, "the shock is a hazard (ADR-0007)");
            Assert.AreEqual(1, m.hazardFrame);
            Assert.IsFalse(m.hazardOnTarget);
            Assert.AreEqual(land - 1, h.telegraphFrames, "laid with the wind-up, strikes with the slam");
            var shock = JsTables.Projectile("shock");
            Assert.AreEqual(F(shock.Life * 0.6), h.strikeFrames, "the JS shockwave's life x 0.6");
            float run = (float)(shock.Speed * shock.Life * 0.6);
            Assert.AreEqual((run + 2f * (float)shock.R) * Px, h.size.x, 1e-4, "its run plus its width");
            Assert.AreEqual((120f * Scale + run / 2f) * Px, m.hazardAhead, 1e-4, "starting 120 px x his scale in front");
            Assert.AreEqual(Mathf.RoundToInt((float)(shock.Dmg * 0.9) * 0.8f), h.hit.dmg, "the JS shock x 0.9");
            Assert.AreNotEqual(HitLevel.Unblockable, h.hit.level, "a shock can be blocked (JS: no unb)");
        }

        [Test]
        public void Skyfall_is_a_mark_that_follows_Kage_while_he_charges_then_locks_and_a_bolt_strikes_it()
        {
            var m = Ability("skyfall").move;
            int charge = F(0.5 * Tell), fall = F(0.5);
            Assert.AreEqual(28, charge);
            Assert.AreEqual(30, fall);
            var h = m.hazard;
            Assert.IsNotNull(h);
            Assert.IsTrue(m.hazardOnTarget, "the mark opens under Kage, not under Raikon");
            Assert.AreEqual(1, m.hazardFrame, "at once");
            Assert.AreEqual(charge - 1, h.followFrames, "it follows him for the charge");
            Assert.AreEqual(charge + fall - 1, h.telegraphFrames, "then the bolt a half second after the lock");
            Assert.AreEqual(130f * Px, h.size.x, 1e-4, "the JS strike's 130 px width");
            Assert.AreEqual(490f * Px, h.size.y, 1e-3, "a bolt from the sky: no jumping it");
            Assert.AreEqual(17, h.hit.dmg, "the JS 21, x 0.8");
            Assert.AreEqual(RaikonAbilities.SkyStrikeCue, h.strikeCue);
            Assert.IsTrue(m.Armoured(0) && m.Armoured(charge + fall), "super armour while he calls it");
            Assert.AreEqual(charge + fall + F(0.6), m.total, "the JS dur: charge + fall + 0.6 s");
            Assert.IsTrue(m.cues.Any(c => c.cue == RaikonAbilities.StormMantleCue), "the storm gathers round him");
            Assert.IsEmpty(m.hits, "he strikes nothing himself");
        }

        [Test]
        public void Redline_Counter_is_a_settled_stance_that_catches_a_blow_and_is_spent_if_left_alone()
        {
            var a = Ability("redlineCounter");
            var m = a.move;
            Assert.AreEqual(F(1.5), m.counterTo + 1, "held 1.5 s in phase two");
            Assert.AreEqual(F(0.24), m.counterFrom, "the stance settles before it counters: an earlier blow lands as a plain hit");
            Assert.IsTrue(m.counterFrontOnly, "a counter stance (SAN-103): a melee blow from the front");
            Assert.AreEqual(F(1.5) + F(0.62), m.total, "then the spent stance: he is open for the JS 0.62 s");
            Assert.AreEqual(1, m.selfPosture.Count, "and the spent stance costs him posture");
            Assert.AreEqual(F(1.5), m.selfPosture[0].frame);
            Assert.AreEqual(0.38f, m.selfPosture[0].fraction, 1e-5, "38 of the JS guard's 100");
            Assert.IsEmpty(m.hits, "the stance strikes nothing");

            var p = m.counterAnswer;
            Assert.IsNotNull(p, "the punish");
            var w = p.hits.Single();
            Assert.AreEqual(F(0.1 * Tell), w.from, "the JS 0.1 s delay");
            Assert.AreEqual(14, w.dmg, "the JS 17, x 0.8");
            Assert.AreEqual(HitLevel.Unblockable, w.level, "JS unb 1: the punish breaks guards");
            Assert.IsTrue(w.critical, "and is a critical (FIGHT_STYLES.raikon.crit)");
            Assert.IsTrue(w.knockdown);

            Assert.IsNull(RaikonAbilities.RedlineForPhase(1), "phase two holds the long stance");
            var shortStance = RaikonAbilities.RedlineForPhase(2);
            Assert.IsNotNull(shortStance, "the last phase holds it shorter");
            Assert.AreEqual(F(1.25), shortStance.counterTo + 1, "1.25 s");
            Assert.AreSame(m.counterAnswer, shortStance.counterAnswer, "answered by the same punish");
        }

        // ---------------------------------------------------------------- the blade override

        [Test]
        public void His_hit_line_is_his_painted_blade_not_the_tachi_familys_1_5_m()
        {
            var d = Def();
            Assert.Greater(d.bladeLength, 0f, "a per-character blade length overrides the family's");
            Assert.AreEqual(RaikonPartSet.PaintedBladeLength, d.bladeLength, 1e-5f);
            Assert.AreEqual(0.875f, d.bladeLength, 0.01f, "his painted blade, guard to tip, in Sword-bone units");
            Assert.Less(d.bladeLength, TachiMoves.BladeLength * 0.7f, "far shorter than the family's blade");
            Assert.AreEqual(0f, CharacterCatalog.Get("order_footman").bladeLength, "every other character keeps its family's blade");
        }

        // ---------------------------------------------------------------- look and arena

        [Test]
        public void His_arena_is_the_rooftops_of_his_JS_stage()
        {
            Assert.AreEqual("rooftops", BossArena.EnvFor(Id), "act1.boss has no env of its own: Act I's");
        }
    }
}
