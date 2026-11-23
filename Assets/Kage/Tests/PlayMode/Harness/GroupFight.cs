using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The fight harness for group fights (SAN-83): Kage against several foes on a manual clock. Kage is driven by
    /// <see cref="KageInput"/>, which falls back to <see cref="KageAi"/> when <c>kageAi</c> is set; each foe by its own
    /// scripted input, which falls back to its own AI (or stands still with <c>foesStill</c>). Dispose it before
    /// building the next fight.
    /// </summary>
    public sealed class GroupFight : IDisposable
    {
        public readonly GameObject Root;
        public readonly FightContext Ctx;
        public readonly FightLoop Loop;
        public readonly CombatEventLog Log;
        public readonly Fighter Kage;
        public readonly List<Fighter> Foes = new();
        public readonly ScriptedInput KageInput;
        public readonly AIInputSource KageAi;
        public readonly List<ScriptedInput> FoeInputs = new();
        public readonly List<AIInputSource> FoeAis = new();
        readonly List<string> _frames = new();

        public GroupFight(int seed, float kageX, float[] foeXs, string foeId = "order_footman", bool kageAi = false,
                          bool foesStill = false, CharacterDef kageDef = null, float aiLevel = 0.6f, bool ownPhysics = false)
        {
            Root = new GameObject("GroupFight");
            var b = new FightBuilder(Root.transform, seed, manualClock: true, ownPhysics: ownPhysics);
            b.AddGround();
            KageInput = new ScriptedInput();
            if (kageAi)
            {
                KageAi = new AIInputSource { level = aiLevel };
                KageInput.fallback = KageAi;
            }
            Kage = b.Spawn(kageDef ?? CharacterCatalog.Get(KageCharacter.Id), 0, KageInput);
            var def = CharacterCatalog.Get(foeId);
            foreach (var _ in foeXs)
            {
                var ai = new AIInputSource { level = aiLevel };
                ai.Configure(def);
                var input = new ScriptedInput { fallback = foesStill ? null : ai };
                FoeAis.Add(ai);
                FoeInputs.Add(input);
                Foes.Add(b.Spawn(def, 1, input));
            }
            Ctx = b.Build();
            Loop = b.Loop;
            Log = new CombatEventLog(Ctx.Events);
            Ctx.Begin();

            Kage.ResetForRound(kageX, 1f);
            Kage.BeginFight();
            for (int i = 0; i < Foes.Count; i++)
            {
                Foes[i].ResetForRound(foeXs[i], foeXs[i] > kageX ? -1f : 1f);
                Foes[i].BeginFight();
            }
        }

        /// <summary>Runs <paramref name="frames"/> loop frames (a frame frozen by hitstop or slow motion counts), calling <paramref name="after"/> after each.</summary>
        public GroupFight Run(int frames, Action after = null)
        {
            for (int i = 0; i < frames; i++)
            {
                Loop.StepHeadless();
                _frames.Add(FrameLine());
                after?.Invoke();
            }
            return this;
        }

        /// <summary>Runs until one frame has actually run (frames frozen by hitstop or slow motion are passed over).</summary>
        public GroupFight Next()
        {
            for (int i = 0; i < 600; i++)
            {
                bool ran = Loop.StepHeadless();
                _frames.Add(FrameLine());
                if (ran) break;
            }
            return this;
        }

        public IEnumerable<CombatEvent> Events(CombatEventType t)
        {
            foreach (var e in Log.All) if (e.Type == t) yield return e;
        }

        /// <summary>The foes in an attack move (<see cref="Fighter.InAttackMove"/>) right now.</summary>
        public int Attacking()
        {
            int n = 0;
            foreach (var f in Foes) if (f.InAttackMove) n++;
            return n;
        }

        /// <summary>One line per frame: frame, then per fighter id x y state move:moveFrame hp, then the engaged foes and Kage's target.</summary>
        public IReadOnlyList<string> Frames => _frames;

        string FrameLine()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append((Ctx.Clock.Frame - 1).ToString(ci));
            foreach (var f in Ctx.Fighters)
            {
                var p = f.Pos;
                sb.Append(' ').Append(f.FighterId)
                  .Append(' ').Append(p.x.ToString("0.00000", ci))
                  .Append(' ').Append(p.y.ToString("0.00000", ci))
                  .Append(' ').Append(f.state)
                  .Append(' ').Append(f.state == FState.Attack && f.move != null ? f.move.id + ":" + f.moveFrame : "-")
                  .Append(" hp=").Append(f.health.ToString("0.###", ci));
            }
            sb.Append(" engaged=");
            foreach (var e in Ctx.Engagement.Engaged) sb.Append(e.FighterId).Append(',');
            sb.Append(" target=").Append(Kage.opponent != null ? Kage.opponent.FighterId : "-");
            return sb.ToString();
        }

        public void Dispose()
        {
            Log.Dispose();
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            var own = Ctx.OwnScene;
            if (own.IsValid() && own.isLoaded) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(own);
        }
    }
}
