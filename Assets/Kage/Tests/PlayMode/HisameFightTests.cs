using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Hisame in a fight (SAN-105), through the fight harness: Lightning Cut (a low armoured tell, then one cut dashing straight through Kage;
    /// it lands, a guard blocks it, a jump or a roll gets out of it), Kocho (invulnerable through the step, lands at Kage's back, or beside
    /// him when a wall leaves no room; a glint, then the cut 0.2 s later, time enough to turn and guard; her AI uses it only on a Kage who
    /// chases her), Smoke Bomb (a thrown projectile, only in phase two) and her two phases (berserk cools her abilities down in 0.7 of the
    /// time). Hisame is scripted to press her ability slots; Kage is scripted. Only events and public state are read.
    /// </summary>
    public class HisameFightTests
    {
        const int Seed = 105;
        const int LightningCutSlot = 1, KochoSlot = 2, SmokeBombSlot = 3;
        const string LightningCut = "hisame.lightningCut", Kocho = "hisame.kocho", SmokeBomb = "hisame.smokeBomb";
        readonly List<HeadlessFight> _fights = new();
        readonly List<(int frame, string cue, Fighter owner)> _cues = new();
        HeadlessFight _current;

        [SetUp]
        public void SetUp() => EffectCues.Played += OnCue;

        [TearDown]
        public void TearDown()
        {
            EffectCues.Played -= OnCue;
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            _cues.Clear();
        }

        void OnCue(string id, Vector2 pos, in CueArgs a) { if (_current != null) _cues.Add((_current.Ctx.Clock.Frame, id, a.owner)); }

        HeadlessFight Fight(Foe foe = Foe.Scripted, float kageX = -1.0f, float hisameX = 1.0f)
        {
            var f = new HeadlessFight(Seed, foe, kageX, hisameX, foeDef: CharacterCatalog.Get(Hisame.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            _fights.Add(f);
            _current = f;
            return f;
        }

        static bool In(Fighter g, string moveId) => g.state == FState.Attack && g.move != null && g.move.id == moveId;

        /// <summary>Hisame presses <paramref name="slot"/> on frame 2, then stands.</summary>
        static void Press(HeadlessFight f, int slot) => f.RoninInput.Add(2).Add(1, ability: slot).Add(5000);

        static void PhaseTwo(HeadlessFight f) => f.Ronin.health = f.Ronin.maxHealth * 0.5f;

        IEnumerable<CombatEvent> From(HeadlessFight f, CombatEventType t, string moveId) => f.Events(t).Where(e => e.Source == f.Ronin && e.MoveId == moveId);

        static HitInfo LightHit(Fighter attacker) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = 4, level = HitLevel.High, kb = new Vector2(1f, 0f), stun = 14 },
            dmg = 4, kb = new Vector2(1f, 0f), zone = HitZone.Body, contact = Vector2.up, moveId = "test.jab", window = -1,
        };

        // ================================================================ Lightning Cut

        [Test]
        public void Lightning_Cut_drops_low_then_dashes_straight_through_Kage_with_one_cut()
        {
            var f = Fight(kageX: -0.3f, hisameX: 1.0f);
            Press(f, LightningCutSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, LightningCut), 10);
            Assert.IsTrue(In(f.Ronin, LightningCut));
            int start = f.Ctx.Clock.Frame;
            float x0 = f.Ronin.Pos.x;
            f.RunUntil(() => !In(f.Ronin, LightningCut), 120);
            var hits = From(f, CombatEventType.Hit, LightningCut).ToList();
            Assert.AreEqual(1, hits.Count, "one cut");
            Assert.AreSame(f.Kage, hits[0].Target);
            Assert.GreaterOrEqual(hits[0].Frame - start, 33, "nothing during the low tell");
            Assert.Greater(x0 - f.Ronin.Pos.x, 1.0f, "the dash carried her through");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin && e.MoveId != LightningCut), "no wave, nothing else");
        }

        [Test]
        public void A_light_hit_during_the_tell_does_not_stop_Lightning_Cut()
        {
            var f = Fight(kageX: -0.3f, hisameX: 1.0f);
            Press(f, LightningCutSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, LightningCut) && f.Ronin.moveFrame >= 12, 30);
            Assert.AreEqual(HitResult.Hit, f.Ronin.ReceiveHit(LightHit(f.Kage)));
            Assert.IsTrue(In(f.Ronin, LightningCut), "armoured through the tell (JS super armour)");
        }

        [Test]
        public void A_guard_blocks_Lightning_Cut()
        {
            var f = Fight(kageX: -0.3f, hisameX: 1.0f);
            Press(f, LightningCutSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(90);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, LightningCut).Count(), "the JS tip: block it");
            Assert.AreEqual(0, From(f, CombatEventType.Hit, LightningCut).Count());
            Assert.IsFalse(f.Events(CombatEventType.GuardBroken).Any(e => e.Target == f.Kage), "a full guard holds (JS gb 70 of 100)");
            Assert.Greater(f.Kage.Posture, 0.5f * f.Kage.PostureMax, "...though it leans hard on it");
        }

        /// <summary>Searches Kage's timing for <paramref name="dodge"/> against the cut; returns the first frame it avoided it, or -1.</summary>
        static int FindTiming(System.Action<ScriptedInput, int> dodge, out bool perfect)
        {
            perfect = false;
            for (int at = 20; at <= 44; at++)
            {
                using var f = new HeadlessFight(Seed, Foe.Scripted, -0.3f, 1.0f, foeDef: CharacterCatalog.Get(Hisame.Id));
                f.Kage.maxHealth = f.Kage.health = 1e6f;
                Press(f, LightningCutSlot);
                dodge(f.KageInput, at);
                f.Run(90);
                bool touched = f.Events(CombatEventType.Hit).Concat(f.Events(CombatEventType.Blocked)).Any(e => e.Source == f.Ronin && e.MoveId == LightningCut);
                if (touched) continue;
                perfect = f.Events(CombatEventType.PerfectDodged).Any();
                return at;
            }
            return -1;
        }

        [Test]
        public void A_jump_clears_Lightning_Cut()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(10, dy: 1).Add(5000), out _);
            Assert.GreaterOrEqual(at, 0, "the JS tip: jump it");
        }

        [Test]
        public void A_roll_gets_through_Lightning_Cut()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(1, tapFwd: true).Add(5000), out _);
            Assert.GreaterOrEqual(at, 0, "rolling through her as she comes");
        }

        // ================================================================ Kocho

        [Test]
        public void Kocho_is_invulnerable_from_the_start_to_the_landing()
        {
            var f = Fight(kageX: -0.6f, hisameX: 0.6f);
            Press(f, KochoSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Kocho), 10);
            var probe = new HitWindow { dmg = 1, level = HitLevel.Mid };
            int land = f.Ronin.move.vaultTo;
            while (In(f.Ronin, Kocho) && f.Ronin.moveFrame <= land)
            {
                Assert.IsFalse(f.Ronin.HittableBy(probe), $"untouchable on frame {f.Ronin.moveFrame} of the step");
                Assert.IsFalse(f.Ronin.CanBeThrown, "and no throw takes her");
                f.Run(1);
            }
            f.RunUntil(() => In(f.Ronin, Kocho) && f.Ronin.moveFrame > land + 1, 10);
            Assert.IsTrue(f.Ronin.HittableBy(probe), "the cut after it is open");
        }

        [Test]
        public void Kages_blade_passes_through_her_during_the_step()
        {
            var f = Fight(kageX: -0.6f, hisameX: 0.6f);
            Press(f, KochoSlot);
            // Kage cuts at her as she springs (his fang string, every few frames)
            f.KageInput.Add(3);
            for (int i = 0; i < 6; i++) f.KageInput.Add(1, buttons: "S").Add(4);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Kocho), 10);
            int land = f.Ctx.Clock.Frame + f.Ronin.move.vaultTo - f.Ronin.moveFrame;
            f.Run(40);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage && e.Frame <= land), "nothing lands on the butterfly step");
        }

        [Test]
        public void Kocho_arcs_over_Kage_and_lands_at_his_back_facing_him()
        {
            var f = Fight(kageX: -0.6f, hisameX: 0.8f);
            Press(f, KochoSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Kocho), 10);
            float top = 0f;
            f.RunUntil(() => { top = Mathf.Max(top, f.Ronin.Pos.y); return f.Ronin.moveFrame >= f.Ronin.move.vaultTo; }, 60);
            f.Run(2);
            Assert.Greater(top, 1.0f, "an arc over his head (JS 150 px x 0.95)");
            Assert.Less(f.Ronin.Pos.x, f.Kage.Pos.x, "she came down on his other side: behind him");
            Assert.AreEqual(110f * Hisame.BodyScale / 115f, f.Kage.Pos.x - f.Ronin.Pos.x, 0.15f, "110 px x her scale past him");
            Assert.Less(f.Ronin.Pos.y, 0.05f, "on the ground");
            Assert.AreEqual(1f, f.Ronin.facing, "facing his back");
        }

        [Test]
        public void With_a_wall_at_his_back_Kocho_comes_down_beside_him_on_her_side()
        {
            var f = Fight(kageX: -7.3f, hisameX: -5.9f);
            Press(f, KochoSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Kocho), 10);
            f.RunUntil(() => f.Ronin.moveFrame >= f.Ronin.move.vaultTo, 60);
            f.Run(2);
            Assert.Greater(f.Ronin.Pos.x, f.Kage.Pos.x, "no room behind him: beside him, on her own side");
            Assert.AreEqual(110f * Hisame.BodyScale / 115f, f.Ronin.Pos.x - f.Kage.Pos.x, 0.15f);
            Assert.AreEqual(-1f, f.Ronin.facing, "facing him");
        }

        [Test]
        public void A_glint_on_her_fan_then_the_cut_a_beat_later()
        {
            var f = Fight(kageX: -0.6f, hisameX: 0.8f);
            Press(f, KochoSlot);
            f.KageInput.Add(5000);
            f.Run(80);
            var glint = _cues.Where(c => c.cue == HisameAbilities.KochoGlintCue && c.owner == f.Ronin).ToList();
            Assert.AreEqual(1, glint.Count, "one glint");
            var hit = From(f, CombatEventType.Hit, Kocho).ToList();
            Assert.AreEqual(1, hit.Count, "the cut lands on a Kage who stands");
            Assert.AreSame(f.Kage, hit[0].Target);
            Assert.AreEqual(12, hit[0].Frame - glint[0].frame, "0.2 s from the glint to the cut (JS tw1 held to 0.2 s)");
            int land = _cues.Single(c => c.cue == HisameAbilities.KochoSmokeCue && c.owner == f.Ronin).frame;
            Assert.AreEqual(glint[0].frame, land, "the glint as she lands");
        }

        [Test]
        public void Turning_to_guard_after_the_glint_blocks_the_cut()
        {
            int glintAt;
            using (var probe = new HeadlessFight(Seed, Foe.Scripted, -0.6f, 0.8f, foeDef: CharacterCatalog.Get(Hisame.Id)))
            {
                _current = probe;
                Press(probe, KochoSlot);
                probe.KageInput.Add(5000);
                probe.RunUntil(() => _cues.Any(c => c.cue == HisameAbilities.KochoGlintCue), 80);
                glintAt = probe.Frames.Count;
                _cues.Clear();
            }
            var f = Fight(kageX: -0.6f, hisameX: 0.8f);
            Press(f, KochoSlot);
            f.KageInput.Add(glintAt).Add(1, fwd: -1).Add(5000, buttons: "G");     // the glint: turn round, guard
            f.Run(80);
            int hit = From(f, CombatEventType.Hit, Kocho).Count(), blocked = From(f, CombatEventType.Blocked, Kocho).Count();
            int parried = f.Events(CombatEventType.Parried).Count(e => e.Source == f.Ronin);
            Assert.AreEqual(0, hit, $"time enough to turn and guard (blocked {blocked}, parried {parried}, Kage facing {f.Kage.facing}, she is at {f.Ronin.Pos.x - f.Kage.Pos.x:0.00})");
            Assert.AreEqual(1, blocked, $"guarded, not parried: the guard came well before the cut (parried {parried})");
        }

        [Test]
        public void Her_AI_uses_Kocho_on_a_Kage_who_chases_her_and_not_on_one_who_waits()
        {
            var still = Fight(Foe.Ai, -1.6f, 0.6f);
            still.KageInput.Add(900);
            bool stepped = false;
            still.RunUntil(() => stepped |= In(still.Ronin, Kocho), 900);
            Assert.IsFalse(stepped, "her answer to being chased, never an opener");

            var chase = Fight(Foe.Ai, -3.0f, 1.0f);
            chase.KageInput.Add(1600, fwd: 1);                       // Kage walks at her
            stepped = false;
            int kageWalks = 0;
            chase.RunUntil(() =>
            {
                stepped |= In(chase.Ronin, Kocho);
                if (chase.Kage.state == FState.Walk || chase.Kage.state == FState.Run) kageWalks++;
                return stepped;
            }, 1600);
            Assert.Greater(kageWalks, 0, "he chased her");
            Assert.IsTrue(stepped, "chased, she springs over him");
        }

        // ================================================================ Smoke Bomb

        [Test]
        public void Smoke_Bomb_is_only_hers_in_phase_two()
        {
            var f = Fight(kageX: -2.4f, hisameX: 1.2f);
            Press(f, SmokeBombSlot);
            f.KageInput.Add(5000);
            f.Run(20);
            Assert.IsFalse(In(f.Ronin, SmokeBomb), "phase one has no Smoke Bomb");
        }

        [Test]
        public void Smoke_Bomb_is_a_thrown_projectile_that_bursts_into_smoke()
        {
            var f = Fight(kageX: -2.4f, hisameX: 1.2f);
            PhaseTwo(f);
            Press(f, SmokeBombSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, SmokeBomb), 10);
            Assert.IsTrue(In(f.Ronin, SmokeBomb), "phase two: enabled");
            f.RunUntil(() => f.Ctx.Projectiles.Count > 0, 20);
            var p = f.Ctx.Projectiles.Single();
            Assert.AreEqual(HisameAbilities.SmokeProjectileId, p.Def.id);
            Assert.AreSame(f.Ronin, p.Owner);
            float y0 = p.Position.y;
            float top = y0;
            f.RunUntil(() => { if (p != null && p.Alive) top = Mathf.Max(top, p.Position.y); return p == null || !p.Alive; }, 120);
            Assert.Greater(top, y0 + 0.3f, "a lob: thrown up first");
            Assert.AreEqual(0, f.Ctx.Projectiles.Count, "spent: on Kage or on the ground");
            Assert.IsTrue(_cues.Any(c => c.cue == HisameAbilities.SmokeBurstCue && c.owner == f.Ronin), "it bursts into smoke");
            var hits = f.Events(CombatEventType.Hit).Concat(f.Events(CombatEventType.Blocked)).Where(e => e.Source == f.Ronin && e.MoveId == HisameAbilities.SmokeProjectileId).ToList();
            Assert.AreEqual(1, hits.Count, "from 3.6 m it comes down on Kage");
            Assert.AreSame(f.Kage, hits[0].Target);
        }

        [Test]
        public void A_guard_blocks_the_smoke_bomb()
        {
            var f = Fight(kageX: -2.4f, hisameX: 1.2f);
            PhaseTwo(f);
            Press(f, SmokeBombSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(120);
            Assert.AreEqual(1, f.Events(CombatEventType.Blocked).Count(e => e.Source == f.Ronin && e.MoveId == HisameAbilities.SmokeProjectileId));
        }

        // ================================================================ phases

        [Test]
        public void Phase_two_Downpour_opens_at_half_health_adds_Smoke_Bomb_and_berserk()
        {
            var f = Fight(Foe.Ai, -1.5f, 1.5f);
            f.KageInput.Add(5000);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.IsTrue(f.Ronin.AbilityEnabled("lightningCut") && f.Ronin.AbilityEnabled("hisameKocho"));
            Assert.IsFalse(f.Ronin.AbilityEnabled("smokeBomb"), "phase one has no Smoke Bomb");
            Assert.AreEqual(1f, f.Ronin.AbilityCooldownMul);

            f.Ronin.health = f.Ronin.maxHealth * 0.51f;
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "51% is above the JS threshold");
            f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "the JS threshold: half health");
            Assert.IsTrue(f.Ronin.AbilityEnabled("smokeBomb"), "Smoke Bomb is added");
            Assert.IsTrue(f.Ronin.AbilityEnabled("lightningCut") && f.Ronin.AbilityEnabled("hisameKocho"));
            Assert.AreEqual(0.7f, f.Ronin.AbilityCooldownMul, 1e-6, "berserk (SAN-102): cooldowns x 0.7");
            Assert.AreEqual(AiProfiles.Get("agile").id, f.Ai.profile.id, "still the agile profile");
            CollectionAssert.AreEqual(new[] { "Rainfall", "Downpour" }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Name).ToArray());
        }

        [Test]
        public void Berserk_cools_Lightning_Cut_down_in_seven_tenths_of_the_time()
        {
            int Left(bool berserk)
            {
                var f = Fight(kageX: -0.3f, hisameX: 1.0f);
                if (berserk) PhaseTwo(f);
                Press(f, LightningCutSlot);
                f.KageInput.Add(5000);
                f.RunUntil(() => In(f.Ronin, LightningCut), 10);
                return f.Ronin.AbilityCooldownLeft("lightningCut");
            }
            int calm = Left(false), wild = Left(true);
            Assert.AreEqual(300, calm, 2, "5 s");
            Assert.AreEqual(210, wild, 2, "5 s x 0.7");
        }

        [Test]
        public void Her_AI_uses_Lightning_Cut_in_its_range()
        {
            var f = Fight(Foe.Ai, -1.2f, 1.0f);
            f.KageInput.Add(3000);
            bool cut = false;
            f.RunUntil(() => cut |= In(f.Ronin, LightningCut), 3000);
            Assert.IsTrue(cut);
        }
    }
}
