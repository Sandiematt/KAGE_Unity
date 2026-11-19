using System.Collections.Generic;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Helpers for the AI profile tests (SAN-95): a closed-loop Kage bot, a slow telegraphed test ability, and the probe that
    /// measures how a foe answers a stream of attacks.
    /// </summary>
    static class ProfileAiTestKit
    {
        public const string SlowId = "test.slow";
        public const string SlamId = "test.slam";

        /// <summary>A scripted-style Kage with no randomness: walks to the foe and cycles punch, punch, kick, weapon, kick, weapon.</summary>
        public sealed class KageBot : IInputSource
        {
            int _cool, _i;
            public void Sample() { }
            public FrameInput Poll(Fighter self)
            {
                var o = new FrameInput();
                var opp = self.opponent;
                if (opp == null || self.Busy) return o;
                if (_cool > 0) { _cool--; return o; }
                float d = Mathf.Abs(opp.Pos.x - self.Pos.x);
                if (d > 1.0f) { o.dx = (int)Mathf.Sign(opp.Pos.x - self.Pos.x); return o; }
                switch (_i++ % 6)
                {
                    case 0: case 1: o.p = true; break;
                    case 2: case 4: o.k = true; break;
                    default: o.s = true; break;
                }
                _cool = 14 + (_i % 3) * 8;
                return o;
            }
        }

        /// <summary>A slow, clearly telegraphed fist blow: first hit at frame 40 of 50.</summary>
        public static MoveDef SlowMove() => new MoveDef(SlowId, "Slow blow", 50)
            .Key(0, PoseLib.Stance()).Key(50, PoseLib.Stance())
            .Hit(40, 44, new[] { HitPoint.Fist }, 4, HitLevel.Mid, new Vector2(0.6f, 0f), stun: 12, hitstop: 3, radius: 0.5f).Sword();       // a blade move: the AI reads it as reaching 1.6 m, so a foe standing off still answers it

        /// <summary>A quick test move for the foe's own ability.</summary>
        public static MoveDef SlamMove() => new MoveDef(SlamId, "Slam", 30)
            .Key(0, PoseLib.Stance()).Key(30, PoseLib.Stance())
            .Hit(12, 15, new[] { HitPoint.Fist }, 5, HitLevel.Mid, new Vector2(0.6f, 0f), stun: 12, hitstop: 3, radius: 0.5f);

        public static void RegisterSlow() => AbilityCatalog.Register(new AbilityDef { id = SlowId, move = SlowMove(), cooldownFrames = 0 });
        public static void Unregister() { AbilityCatalog.Unregister(SlowId); AbilityCatalog.Unregister(SlamId); }

        public static CharacterDef Footman(string profile = null, float attackRate = 1f)
        {
            var d = CharacterCatalog.Get("order_footman");
            if (profile != null) d.aiProfile = profile;
            d.attackRate = attackRate;
            return d;
        }

        public struct ProbeResult
        {
            public int attacks, raised, attempts, parried, blocked, hit;
            public float delaySum;
            public float Raised => attacks == 0 ? 0f : (float)raised / attacks;
            public float Attempts => attacks == 0 ? 0f : (float)attempts / attacks;
            public float Delay => raised == 0 ? 0f : delaySum / raised;
        }

        /// <summary>
        /// Kage throws the slow blow at the foe <paramref name="attacks"/> times, 80 frames apart, with nobody able to die. A foe that
        /// never attacks (attackRate 0) answers each blow it was ready for: counts how often its Guard went up, how often that was a
        /// parry attempt, and how many frames after the blow began.
        /// </summary>
        public static ProbeResult Probe(AiProfile profile, int seed, int attacks, float parryOverride = -1f)
        {
            RegisterSlow();
            Tuning.Override(new AiTuning { holdGuardChance = 0f });        // a guard held for its own sake would inflate the count
            var foe = Footman(attackRate: 0f);
            var kage = CharacterCatalog.Get("kage");
            kage.abilities = new List<string> { SlowId };
            var res = new ProbeResult();
            using (var f = new HeadlessFight(seed, Foe.Ai, -0.3f, 0.3f, foeDef: foe, kageDef: kage))
            {
                f.Ai.SetProfile(profile);
                if (parryOverride >= 0f) f.Ai.parryRate = parryOverride;
                f.Kage.maxHealth = f.Kage.health = 1e7f;
                f.Ronin.maxHealth = f.Ronin.health = 1e7f;
                for (int i = 0; i < attacks; i++) f.KageInput.Add(1, buttons: "", ability: 1).Add(79);

                bool was = false, eligible = false, raisedSeen = false, attemptSeen = false;
                int start = 0, total = attacks * 80 + 10;
                for (int fr = 0; fr < total; fr++)
                {
                    f.Run(1);
                    bool atk = f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == SlowId;
                    int now = f.Ctx.Clock.Frame;
                    if (atk && !was)
                    {
                        start = now;
                        float dist = Mathf.Abs(f.Ronin.Pos.x - f.Kage.Pos.x);
                        eligible = !f.Ronin.Busy && f.Ronin.Grounded && !f.Ronin.IsGuarding && dist < 1.5f;
                        raisedSeen = attemptSeen = false;
                        if (eligible) res.attacks++;
                    }
                    was = atk;
                    if (eligible && now - start < 60)
                    {
                        if (!raisedSeen && f.Ronin.IsGuarding) { raisedSeen = true; res.raised++; res.delaySum += now - start; }
                        if (!attemptSeen && f.Ai.ParryAttempt) { attemptSeen = true; res.attempts++; }
                    }
                }
                foreach (var e in f.Events(CombatEventType.Parried)) if (e.Target == f.Ronin) res.parried++;
                foreach (var e in f.Events(CombatEventType.Blocked)) if (e.Target == f.Ronin) res.blocked++;
                foreach (var e in f.Events(CombatEventType.Hit)) if (e.Target == f.Ronin) res.hit++;
            }
            Tuning.ClearOverrides();
            Unregister();
            return res;
        }
    }
}
