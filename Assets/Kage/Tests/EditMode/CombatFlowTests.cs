using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The combat-flow pass (docs/combat-flow.md): crisp facing and its lock, launched bodies that lie flat instead of somersaulting, the back step
    /// (a flat slide; every whole-body spin is gone, UprightGroundedTests); the two-way parry by tier (Kage parries every tier; a foe's parry leaves him a stagger he can guard or dodge
    /// out of, and a tell on its answer; the AI's read of a repeated attack and its stance parry); recovery cancels on contact, the whiff punish
    /// and guard pressure. Headless fights on a manual clock (<see cref="FightBuilder"/>), stepped with <see cref="FightLoop.StepHeadless"/>.
    /// </summary>
    public class CombatFlowTests
    {
        sealed class StandStill : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        GameObject _root;
        SimulationMode2D _simMode;

        static CombatFlowTuning Flow => Tuning.Get<CombatFlowTuning>();
        static ParryTuning ParryT => Tuning.Get<ParryTuning>();

        [SetUp]
        public void SetUp() { _simMode = Physics2D.simulationMode; }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
            Tuning.ClearOverrides();
        }

        sealed class Duel
        {
            public FightLoop loop;
            public FightContext ctx;
            public Fighter kage, foe;
            public ScriptedInput kageIn;
            public IInputSource foeIn;
        }

        /// <summary>Kage at <paramref name="kageX"/> and the foe at <paramref name="foeX"/>, facing each other, on the dev arena's floor.</summary>
        Duel Fight(string foeId = "order_footman", float kageX = -1.5f, float foeX = 1.5f, IInputSource foeIn = null)
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = new GameObject("CombatFlow");
            var b = new FightBuilder(_root.transform, 1337, manualClock: true);
            b.AddGround();
            var kageIn = new ScriptedInput();
            foeIn ??= new StandStill();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, kageIn);
            var foe = b.Spawn(CharacterCatalog.Get(foeId), 1, foeIn);
            var ctx = b.Build();
            ctx.Begin();
            float toFoe = Mathf.Sign(foeX - kageX);
            kage.ResetForRound(kageX, toFoe); kage.BeginFight();
            foe.ResetForRound(foeX, -toFoe); foe.BeginFight();
            b.Loop.StepHeadless();
            return new Duel { loop = b.Loop, ctx = ctx, kage = kage, foe = foe, kageIn = kageIn, foeIn = foeIn };
        }

        /// <summary>Steps until <paramref name="frames"/> frames have run (frames frozen by hit-stop don't count).</summary>
        static void Live(FightLoop loop, int frames)
        {
            int ran = 0, guard = 0;
            while (ran < frames && guard++ < frames + 400) if (loop.StepHeadless()) ran++;
        }

        static void LiveUntil(FightLoop loop, System.Func<bool> done, int max = 300)
        {
            for (int i = 0; i < max && !done(); i++) Live(loop, 1);
        }

        static HitInfo Blow(Fighter attacker, Fighter victim, HitWindow w = null)
        {
            w ??= new HitWindow { dmg = 4, level = HitLevel.Mid, stun = 12, kb = new Vector2(0.3f, 0f) };
            return new HitInfo { attacker = attacker, w = w, dmg = w.dmg, kb = new Vector2(w.kb.x * Mathf.Sign(victim.Pos.x - attacker.Pos.x), w.kb.y), contact = victim.Pos + Vector2.up, moveId = "test", window = -1 };
        }

        static void Place(Fighter f, float x, float face) { f.ResetForRound(x, face); f.BeginFight(); }

        static MoveDef Jab(Fighter f) => f.moves.Select(Btn.P, 0, 0, false, f);

        // ================================================================ tuning and data

        [Test]
        public void The_shipped_flow_and_ai_tuning_files_match_their_code_defaults()
        {
            Assert.AreEqual(JsonUtility.ToJson(new CombatFlowTuning()), JsonUtility.ToJson(Tuning.Get<CombatFlowTuning>()), "flow.json matches the CombatFlowTuning defaults");
            Assert.AreEqual(JsonUtility.ToJson(new AiTuning()), JsonUtility.ToJson(Tuning.Get<AiTuning>()), "ai.json matches the AiTuning defaults");
        }

        [TestCase(KageCharacter.Id, CombatTier.Player)]
        [TestCase("order_footman", CombatTier.Regular)]
        [TestCase("order_runner", CombatTier.Regular)]
        [TestCase(OrderIronclad.Id, CombatTier.Elite)]
        [TestCase("order_shieldbearer", CombatTier.Elite)]
        [TestCase("hollow_husk", CombatTier.Elite)]
        [TestCase(Kuroe.Id, CombatTier.Sub)]
        [TestCase(Hisame.Id, CombatTier.Sub)]
        [TestCase(Raikon.Id, CombatTier.Boss)]
        public void Every_fighter_has_a_parry_tier(string id, CombatTier tier)
        {
            Assert.AreEqual(tier, Fighter.TierOf(CharacterCatalog.Get(id)));
        }

        [Test]
        public void A_tier_higher_up_reels_less_from_a_parry_and_loses_less_of_its_posture()
        {
            var t = ParryT;
            var foes = new[] { CombatTier.Regular, CombatTier.Elite, CombatTier.Sub, CombatTier.Boss };
            for (int i = 1; i < foes.Length; i++)
            {
                Assert.Less(t.StaggerFor(foes[i]), t.StaggerFor(foes[i - 1]), $"{foes[i]} reels less than {foes[i - 1]}");
                Assert.Less(t.PostureShareFor(foes[i]), t.PostureShareFor(foes[i - 1]), $"{foes[i]} loses less posture than {foes[i - 1]}");
            }
        }

        [Test]
        public void Every_foes_answer_to_its_parry_waits_until_Kage_can_defend()
        {
            var t = ParryT;
            Assert.Greater(t.playerRecoverFrames, 0, "Kage's stagger from a parried blow can be left early");
            Assert.Less(t.playerRecoverFrames, t.playerStaggerFrames, "earlier than it runs out");
            foreach (var tier in new[] { CombatTier.Regular, CombatTier.Elite, CombatTier.Sub, CombatTier.Boss })
                Assert.GreaterOrEqual(t.AnswerDelayFor(tier), t.playerRecoverFrames, $"a {tier} foe only winds up its answer once Kage can guard or dodge");
        }

        [Test]
        public void The_back_step_has_no_somersault()
        {
            var d = Fight();
            var hop = d.kage.moves.backflip;
            Assert.AreEqual("Back Step", hop.display);
            Assert.IsFalse(hop.airborne, "it stays on the floor");
            var start = PoseLib.Stance();
            var pose = new Pose();
            for (int f = 0; f <= hop.total; f++)
            {
                hop.Evaluate(d.kage.def, f, start, pose);
                Assert.LessOrEqual(Mathf.Abs(KMath.Wrap(pose.a[(int)B.Hips])), 15f, $"frame {f}: the body stays upright");
            }
        }

        // ================================================================ facing

        [Test]
        public void A_turn_is_a_crisp_flip_of_the_rig_on_the_same_frame()
        {
            var d = Fight();
            Assert.Greater(d.kage.rig.transform.localScale.x, 0f);
            d.kage.SetFacing(-1f);
            Assert.Less(d.kage.rig.transform.localScale.x, 0f, "flipped at once");
            for (int i = 0; i < 10; i++)
            {
                d.kage.rig.Secondary(1f / 60f, Vector2.zero);
                Assert.Less(d.kage.rig.transform.localScale.x, -0.5f, $"render frame {i}: never squeezed through zero");
            }
        }

        [Test]
        public void In_neutral_a_fighter_turns_to_its_target_but_not_straight_back()
        {
            Tuning.Override(new CombatFlowTuning { turnHoldFrames = 8, turnForceDistance = 99f });
            var d = Fight(kageX: 0f, foeX: 1.5f);
            Place(d.foe, -1.5f, 1f);
            Live(d.loop, 1);
            Assert.AreEqual(-1f, d.kage.facing, "turned to the foe behind");
            Place(d.foe, 1.5f, -1f);
            Live(d.loop, 1);
            Assert.AreEqual(-1f, d.kage.facing, "not straight back: the hold");
            Live(d.loop, 8);
            Assert.AreEqual(1f, d.kage.facing, "then it turns");
        }

        [Test]
        public void An_attack_keeps_its_facing_through_its_wind_up_and_blows()
        {
            var d = Fight(kageX: 0f, foeX: 1.2f);
            var jab = Jab(d.kage);
            int last = Fighter.LastActiveFrame(jab);
            d.kage.StartMove(jab);
            Place(d.foe, -1.2f, 1f);
            while (d.kage.state == FState.Attack && d.kage.moveFrame <= last)
            {
                Assert.IsTrue(d.kage.FacingLocked, $"move frame {d.kage.moveFrame}: locked");
                Assert.AreEqual(1f, d.kage.facing, $"move frame {d.kage.moveFrame}: no turn mid-swing");
                Live(d.loop, 1);
            }
            LiveUntil(d.loop, () => d.kage.state != FState.Attack);
            Live(d.loop, 2);
            Assert.AreEqual(-1f, d.kage.facing, "turned once the move was over");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void A_new_attack_turns_to_a_target_behind_before_its_wind_up(bool turnOnMoveStart)
        {
            Tuning.Override(new CombatFlowTuning { turnHoldFrames = 9999, turnForceDistance = 99f, turnOnMoveStart = turnOnMoveStart });
            var d = Fight(kageX: 0f, foeX: 1.2f);
            d.kage.SetFacing(-1f); d.kage.SetFacing(1f);         // just turned: neutral holds the facing
            Place(d.foe, -1.2f, 1f);
            Live(d.loop, 1);
            Assert.AreEqual(1f, d.kage.facing, "neutral doesn't turn back yet");
            d.kageIn.Add(1, buttons: "P");
            Live(d.loop, 1);
            Assert.AreEqual(FState.Attack, d.kage.state);
            Assert.AreEqual(turnOnMoveStart ? -1f : 1f, d.kage.facing, turnOnMoveStart ? "the jab faces its target" : "the switch off: as before");
        }

        [TestCase("order_footman", true)]
        [TestCase("order_shieldbearer", false)]
        public void A_reel_from_a_blow_from_behind_turns_to_the_attacker(string foeId, bool turns)
        {
            var d = Fight(foeId, kageX: -1.0f, foeX: 1.0f);
            d.foe.SetFacing(1f);                                  // its back to Kage
            Assert.AreEqual(HitResult.Hit, d.foe.ReceiveHit(Blow(d.kage, d.foe)));
            Assert.AreEqual(FState.Hitstun, d.foe.state);
            Assert.AreEqual(turns ? -1f : 1f, d.foe.facing, turns ? "it reels facing the blade" : "a shield bearer keeps its back turned");
        }

        [Test]
        public void A_launched_body_turns_onto_its_back_and_stops_there_and_a_juggle_keeps_its_side()
        {
            var d = Fight(kageX: -1.0f, foeX: 0.2f);
            var launch = new HitWindow { dmg = 1, level = HitLevel.Mid, stun = 20, kb = new Vector2(2.5f, 5.5f), launch = true };
            d.foe.ReceiveHit(Blow(d.kage, d.foe, launch));
            Assert.AreEqual(FState.Launched, d.foe.state);
            float most = 0f;
            for (int i = 0; i < 15; i++) { Live(d.loop, 1); most = Mathf.Max(most, Mathf.Abs(d.foe.TumbleAngle)); }
            float side = Mathf.Sign(d.foe.TumbleAngle);
            Assert.AreEqual(1f, side, "knocked away from its facing: onto its back");
            var info = Blow(d.kage, d.foe, launch);
            info.kb = new Vector2(-info.kb.x, info.kb.y);       // the juggle knocks it the other way
            d.foe.ReceiveHit(info);
            for (int i = 0; i < 120 && d.foe.state == FState.Launched; i++)
            {
                Live(d.loop, 1);
                most = Mathf.Max(most, Mathf.Abs(d.foe.TumbleAngle));
                Assert.AreEqual(side, Mathf.Sign(d.foe.TumbleAngle), $"frame {i}: it doesn't flip over to the other side");
            }
            Assert.LessOrEqual(most, Flow.tumbleRestAngle + 0.01f, "never past lying: no somersault");
            Assert.Greater(most, 45f, "it did turn towards lying");
        }

        // ================================================================ the two-way parry

        [TestCase("order_footman", CombatTier.Regular)]
        [TestCase(OrderIronclad.Id, CombatTier.Elite)]
        [TestCase(Kuroe.Id, CombatTier.Sub)]
        [TestCase(Raikon.Id, CombatTier.Boss)]
        public void Kage_parries_a_blow_from_every_tier(string foeId, CombatTier tier)
        {
            CharacterCatalog.Get(foeId);
            var d = Fight(foeId, kageX: -0.5f, foeX: 0.5f);
            d.kageIn.Add(300, buttons: "G");
            Live(d.loop, 2);
            Assert.IsTrue(d.kage.InParryWindow);
            float hp = d.kage.health;
            Assert.AreEqual(HitResult.Parried, d.kage.ReceiveHit(Blow(d.foe, d.kage)), $"a {tier} foe's blow is parried");
            Assert.AreEqual(hp, d.kage.health, "no damage");
            Assert.AreEqual(FState.Hitstun, d.foe.state, "the foe reels");
            Assert.AreEqual(d.foe.PostureMax * ParryT.PostureShareFor(tier), d.foe.Posture, 0.01f, "and loses its tier's share of posture");
            Assert.IsTrue(d.foe.CounterOpenFor(d.kage), "open to Kage's counter hit");
        }

        [Test]
        public void A_foe_that_parries_Kage_leaves_him_a_stagger_he_can_guard_out_of_and_shows_a_tell_on_its_answer()
        {
            var foeIn = new ScriptedInput().Add(600, buttons: "G");
            var d = Fight(kageX: -0.5f, foeX: 0.5f, foeIn: foeIn);
            Live(d.loop, 2);
            Assert.AreEqual(HitResult.Parried, d.foe.ReceiveHit(Blow(d.kage, d.foe)), "the guarding foe parries");
            Assert.AreEqual(FState.Hitstun, d.kage.state, "Kage staggers");
            Assert.AreEqual(ParryT.playerRecoverFrames, d.kage.StaggerRecoverAt);
            Assert.AreEqual(ParryT.answerTellWindow, d.foe.AnswerTellLeft, "its answer will show the tell");

            d.kageIn.Add(600, buttons: "G");
            Live(d.loop, ParryT.playerRecoverFrames - 1);
            Assert.AreEqual(FState.Hitstun, d.kage.state, "no way out yet");
            Live(d.loop, 1);
            Assert.AreEqual(FState.Idle, d.kage.state, "Guard held: out of the stagger early");
            Assert.IsTrue(d.kage.IsGuarding, "and guarding");

            d.foe.StartMove(Jab(d.foe));
            Assert.AreEqual(0, d.foe.AnswerTellLeft, "the tell played as the answer wound up");
        }

        [Test]
        public void Without_a_guard_or_a_dodge_the_stagger_runs_its_course()
        {
            var foeIn = new ScriptedInput().Add(600, buttons: "G");
            var d = Fight(kageX: -0.5f, foeX: 0.5f, foeIn: foeIn);
            Live(d.loop, 2);
            Assert.AreEqual(HitResult.Parried, d.foe.ReceiveHit(Blow(d.kage, d.foe)));
            d.kageIn.Add(600, buttons: "P");                    // mashing an attack doesn't get out
            Live(d.loop, ParryT.playerRecoverFrames + 4);
            Assert.AreEqual(FState.Hitstun, d.kage.state);
        }

        [Test]
        public void The_AI_parries_a_repeated_attack_more_and_a_boss_reads_it_better_than_a_grunt()
        {
            float Gain(string foeId, out float plain, out float stance)
            {
                CharacterCatalog.Get(foeId);
                var ai = new AIInputSource();
                var d = Fight(foeId, kageX: -0.6f, foeX: 0.6f, foeIn: ai);
                var jab = Jab(d.kage);
                Assume.That(ai.profile != null && !ai.profile.legacy, $"{foeId} decides with a profile");
                plain = ai.ParryChance(d.foe, jab, false);
                for (int i = 0; i < 4; i++)
                {
                    d.kage.StartMove(jab);
                    ai.Poll(d.foe);
                    d.kage.BeginFight();
                    ai.Poll(d.foe);
                }
                Assert.AreEqual(3, ai.Repeats(jab), $"{foeId}: three earlier jabs remembered");
                float read = ai.ParryChance(d.foe, jab, false);
                stance = ai.ParryChance(d.foe, jab, true);
                Assert.Greater(read, plain, $"{foeId}: a repeated jab is parried more");
                Assert.GreaterOrEqual(stance, read, $"{foeId}: more again into a held guard");
                Assert.LessOrEqual(stance, Mathf.Max(Tuning.Get<AiTuning>().parryCap, plain) + 1e-4f, $"{foeId}: never past the cap");
                return read - plain;
            }
            float grunt = Gain("order_footman", out _, out _);
            float boss = Gain(Raikon.Id, out _, out _);
            Assert.Greater(boss, grunt * 2f, "a boss reads a repeated attack far better than a grunt");
        }

        // ================================================================ cancels, punish, pressure

        /// <summary>Kage's jab, with or without a blow that connected, run into its recovery.</summary>
        static Duel JabIntoRecovery(CombatFlowTests t, HitResult? contact)
        {
            var d = t.Fight(kageX: -1.5f, foeX: 1.5f);
            var jab = Jab(d.kage);
            d.kage.StartMove(jab);
            LiveUntil(d.loop, () => d.kage.moveFrame >= jab.hits[0].from);
            if (contact.HasValue) d.kage.ResolveExternal(d.foe, Blow(d.kage, d.foe), contact.Value);
            int last = Fighter.LastActionFrame(jab);
            LiveUntil(d.loop, () => d.kage.state != FState.Attack || d.kage.moveFrame > last);
            Assume.That(d.kage.state == FState.Attack && d.kage.move == jab, "still recovering");
            return d;
        }

        [TestCase(HitResult.Hit, true)]
        [TestCase(HitResult.Blocked, true)]
        [TestCase(null, false)]
        public void A_dash_cancels_the_recovery_of_a_move_that_connected(HitResult? contact, bool cancels)
        {
            var d = JabIntoRecovery(this, contact);
            d.kageIn.Add(1, dash: true);
            Live(d.loop, 1);
            Assert.AreEqual(cancels ? FState.Dash : FState.Attack, d.kage.state, cancels ? "dashed out" : "a whiff recovers in full");
        }

        [TestCase(HitResult.Hit, true)]
        [TestCase(HitResult.Blocked, false)]
        [TestCase(null, false)]
        public void Guard_cancels_the_recovery_only_of_a_move_that_landed(HitResult? contact, bool cancels)
        {
            var d = JabIntoRecovery(this, contact);
            d.kageIn.Add(5, buttons: "G");
            Live(d.loop, 1);
            Assert.AreEqual(cancels ? FState.Idle : FState.Attack, d.kage.state);
            if (cancels) Assert.IsTrue(d.kage.IsGuarding);
        }

        [Test]
        public void Kage_punishes_a_foe_recovering_from_a_whiff_but_not_one_whose_blow_connected()
        {
            var d = Fight(kageX: -3f, foeX: 1.5f);
            var jab = Jab(d.foe);
            int last = Fighter.LastActiveFrame(jab);
            var w = new HitWindow { dmg = 8, level = HitLevel.Mid, stun = 12, kb = new Vector2(0.3f, 0f) };

            d.foe.StartMove(jab);
            LiveUntil(d.loop, () => d.foe.state != FState.Attack || d.foe.moveFrame > last);
            Assume.That(d.foe.state == FState.Attack, "still recovering");
            Assert.AreEqual(HitResult.Hit, d.foe.ReceiveHit(Blow(d.kage, d.foe, w)));
            Assert.IsTrue(d.foe.LastHitPunish, "a whiff punish");
            Assert.AreEqual(Mathf.RoundToInt(8 * Flow.whiffPunishDamageMul), d.foe.LastDamageTaken, 1e-3f);

            Place(d.foe, 1.5f, -1f);
            d.foe.StartMove(jab);
            LiveUntil(d.loop, () => d.foe.moveFrame >= jab.hits[0].from);
            d.foe.ResolveExternal(d.kage, Blow(d.foe, d.kage), HitResult.Blocked);
            LiveUntil(d.loop, () => d.foe.state != FState.Attack || d.foe.moveFrame > last);
            Assume.That(d.foe.state == FState.Attack, "still recovering");
            Assert.AreEqual(HitResult.Hit, d.foe.ReceiveHit(Blow(d.kage, d.foe, w)));
            Assert.IsFalse(d.foe.LastHitPunish, "its blow was guarded: no punish");
            Assert.AreEqual(8f, d.foe.LastDamageTaken, 1e-3f);
        }

        [Test]
        public void A_foe_hitting_Kages_whiff_deals_plain_damage_by_default()
        {
            var d = Fight(kageX: -1.5f, foeX: 3f);
            var jab = Jab(d.kage);
            int last = Fighter.LastActiveFrame(jab);
            d.kage.StartMove(jab);
            LiveUntil(d.loop, () => d.kage.state != FState.Attack || d.kage.moveFrame > last);
            Assume.That(d.kage.state == FState.Attack, "still recovering");
            Assert.AreEqual(HitResult.Hit, d.kage.ReceiveHit(Blow(d.foe, d.kage, new HitWindow { dmg = 8, level = HitLevel.Mid, stun = 12 })));
            Assert.IsFalse(d.kage.LastHitPunish);
            Assert.AreEqual(8f, d.kage.LastDamageTaken, 1e-3f);
        }

        [Test]
        public void A_string_of_guarded_blows_weighs_more_on_the_guard_than_single_pokes()
        {
            var foeIn = new ScriptedInput().Add(900, buttons: "G");
            var d = Fight(kageX: -0.5f, foeX: 0.5f, foeIn: foeIn);
            Live(d.loop, 30);                                    // past the guard press's parry window and lockout
            float Block()
            {
                Assert.AreEqual(HitResult.Blocked, d.foe.ReceiveHit(Blow(d.kage, d.foe)));
                return d.foe.LastPostureTaken;
            }
            float p1 = Block(); Live(d.loop, 6);
            float p2 = Block(); Live(d.loop, 6);
            float p3 = Block();
            Assert.AreEqual(p1 * (1f + Flow.pressureStep), p2, 1e-3f, "the second blow of the string");
            Assert.AreEqual(p1 * (1f + 2f * Flow.pressureStep), p3, 1e-3f, "the third");
            Live(d.loop, Flow.pressureWindowFrames + 4);
            Assert.AreEqual(p1, Block(), 1e-3f, "after a pause the next blow starts a new string");
        }
    }
}
