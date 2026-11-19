using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// A scripted Kage for a stretch (SAN-89): he walks right along the road while nobody stands in his way, stands still behind a
    /// closed gate while the next wave is on its way, and fights with the AI (<see cref="AIInputSource"/>) while a foe stands.
    /// </summary>
    public sealed class RoadWalker : IInputSource
    {
        public StretchSession Session;
        public readonly AIInputSource Ai;
        public RoadWalker(float aiLevel) { Ai = new AIInputSource { level = aiLevel }; }

        public void Sample() { Ai.Sample(); }

        public FrameInput Poll(Fighter self)
        {
            var s = Session;
            if (s == null || s.State == StretchState.Walking) return new FrameInput { dx = 1 };
            if (s.FoesLeft == 0) return default;
            return Ai.Poll(self);
        }
    }

    /// <summary>
    /// The fight harness for stretches (SAN-89): a road (floor only), Kage driven by a <see cref="RoadWalker"/> and a
    /// <see cref="StretchSession"/> that spawns the waves, on a manual clock. Dispose it before building the next fight.
    /// </summary>
    public sealed class StretchFight : IDisposable
    {
        public readonly GameObject Root;
        public readonly FightContext Ctx;
        public readonly FightLoop Loop;
        public readonly CombatEventLog Log;
        public readonly Fighter Kage;
        public readonly StretchSession Session;
        public readonly StretchRoad Road;
        public readonly RoadWalker Walker;
        readonly List<string> _frames = new();

        /// <summary>Kage hits hard and lasts (as in the group-fight tests), so a long run reaches its end without him going down first.</summary>
        public static CharacterDef StrongKage()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.damageMul = 4f;
            d.healthMul = 20f;
            return d;
        }

        /// <param name="rig">With a camera rig the road is drawn too (review sheets); without, it is the floor only.</param>
        public StretchFight(int seed, StretchData data, CharacterDef kageDef = null, float aiLevel = 0.9f, float foeLevel = 0.6f, CameraRig rig = null)
        {
            Root = new GameObject("StretchFight");
            var b = new FightBuilder(Root.transform, seed, manualClock: true);
            Walker = new RoadWalker(aiLevel);
            Kage = b.Spawn(kageDef ?? StrongKage(), 0, Walker);
            Ctx = b.Build();
            Road = rig != null ? StretchRoad.Build(Root.transform, data, Ctx.Bounds, rig) : StretchRoad.BuildPhysics(Root.transform, data, Ctx.Bounds);
            Loop = b.Loop;
            Log = new CombatEventLog(Ctx.Events);
            Session = new StretchSession(Ctx, b, data, Kage, foeInput: _ => new AIInputSource { level = foeLevel });
            Walker.Session = Session;
            Road.Bind(Session);
            Ctx.Begin();
            Session.Begin();
        }

        /// <summary>Runs <paramref name="frames"/> loop frames, calling <paramref name="after"/> after each.</summary>
        public StretchFight Run(int frames, Action after = null)
        {
            for (int i = 0; i < frames; i++)
            {
                Loop.StepHeadless();
                _frames.Add(FrameLine());
                after?.Invoke();
            }
            return this;
        }

        /// <summary>Runs until <paramref name="until"/> is true or <paramref name="maxFrames"/> pass; returns whether it became true.</summary>
        public bool RunUntil(Func<bool> until, int maxFrames, Action after = null)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (until()) return true;
                Run(1, after);
            }
            return until();
        }

        public IEnumerable<CombatEvent> Events(CombatEventType t)
        {
            foreach (var e in Log.All) if (e.Type == t) yield return e;
        }

        /// <summary>The foes in an attack move (<see cref="Fighter.InAttackMove"/>) right now.</summary>
        public int Attacking()
        {
            int n = 0;
            foreach (var f in Session.Foes) if (f.InAttackMove) n++;
            return n;
        }

        /// <summary>One line per frame: frame, the session's state, then each fighter's id, x, y, state and health.</summary>
        public IReadOnlyList<string> Frames => _frames;

        string FrameLine()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append((Ctx.Clock.Frame - 1).ToString(ci)).Append(' ').Append(Session.State);
            foreach (var f in Ctx.Fighters)
            {
                var p = f.Pos;
                sb.Append(' ').Append(f.FighterId)
                  .Append(' ').Append(p.x.ToString("0.00000", ci))
                  .Append(' ').Append(p.y.ToString("0.00000", ci))
                  .Append(' ').Append(f.state)
                  .Append(" hp=").Append(f.health.ToString("0.###", ci));
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            Session.Dispose();
            Road.Dispose();
            Log.Dispose();
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
