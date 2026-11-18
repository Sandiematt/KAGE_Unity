using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>What Kage fights in a harness run.</summary>
    public enum Foe { Bag, Ai, Scripted, None }

    /// <summary>
    /// The fight harness: builds a fight from code with no game bootstrap, on a manual clock, with Kage
    /// on the left and the training bag or the Ronin on the right. Drive it with scripted input,
    /// step it a fixed number of frames, and observe it only through the event log and public state.
    /// Dispose it (or let the test tear down) before building the next one.
    /// </summary>
    public sealed class HeadlessFight : IDisposable
    {
        public const float KageX = -0.4f;
        public static readonly Vector2 BagAnchor = new(1.6f, 2.65f);    // the dev arena's training bag

        public readonly GameObject Root;
        public readonly FightContext Ctx;
        public readonly FightLoop Loop;
        public readonly CombatEventLog Log;
        public readonly Fighter Kage;
        public readonly Fighter Ronin;          // null against the bag
        public readonly PunchingBag Bag;        // null against the Ronin and with no foe
        public readonly ScriptedInput KageInput;
        public readonly ScriptedInput RoninInput;   // set for Foe.Scripted (falls back to the AI when its script ends)
        public readonly AIInputSource Ai;
        readonly List<string> _frames = new();

        /// <param name="foeDef">Who the foe is (default the Ronin's catalog entry), for example the heavy test dummy.</param>
        /// <param name="kageDef">Kage's character data (default his catalog entry), for example with a helmet equipped.</param>
        public HeadlessFight(int seed, Foe foe, float kageX = KageX, float roninX = 0.6f, float kageMeter = 0f, CharacterDef foeDef = null, CharacterDef kageDef = null)
        {
            Root = new GameObject("HeadlessFight");
            var b = new FightBuilder(Root.transform, seed, manualClock: true);
            b.AddGround();
            KageInput = new ScriptedInput();
            Kage = b.Spawn(kageDef ?? CharacterCatalog.Get(KageCharacter.Id), 0, KageInput);
            if (foe == Foe.Bag)
            {
                Bag = b.AddBag(Root.transform, BagAnchor);
            }
            else if (foe != Foe.None)
            {
                Ai = new AIInputSource();
                IInputSource input = Ai;
                if (foe == Foe.Scripted) { RoninInput = new ScriptedInput { fallback = Ai }; input = RoninInput; }
                Ronin = b.Spawn(foeDef ?? CharacterCatalog.Get(global::Kage.Ronin.Id), 1, input);
            }
            Ctx = b.Build();
            Loop = b.Loop;
            Log = new CombatEventLog(Ctx.Events);
            Ctx.Begin();

            Kage.ResetForRound(kageX, 1f);
            Kage.BeginFight();
            Kage.meter = kageMeter;
            if (Ronin != null)
            {
                Ronin.ResetForRound(roninX, -1f);
                Ronin.BeginFight();
            }
        }

        /// <summary>Runs <paramref name="frames"/> frames, recording positions and state after each.</summary>
        public HeadlessFight Run(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                Loop.StepHeadless();
                _frames.Add(FrameLine());
            }
            return this;
        }

        /// <summary>Runs until <paramref name="until"/> is true or <paramref name="maxFrames"/> pass.</summary>
        public HeadlessFight RunUntil(Func<bool> until, int maxFrames = 600)
        {
            for (int i = 0; i < maxFrames && !until(); i++) Run(1);
            return this;
        }

        public IEnumerable<CombatEvent> Events(CombatEventType t)
        {
            foreach (var e in Log.All) if (e.Type == t) yield return e;
        }

        /// <summary>One line per frame: frame, then each body's position (5 decimals) and each fighter's state and move.</summary>
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
            if (Bag != null)
            {
                var p = Bag.BagPosition;
                sb.Append(" bag ").Append(p.x.ToString("0.00000", ci))
                  .Append(' ').Append(p.y.ToString("0.00000", ci))
                  .Append(' ').Append(Bag.BagRotation.ToString("0.00000", ci));
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            Log.Dispose();
            // immediately, so the next fight's Physics2D.Simulate never sees this fight's bodies
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
