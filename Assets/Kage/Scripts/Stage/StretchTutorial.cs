using System;
using System.Collections.Generic;
using UnityEngine;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The tutorial prompts of a stretch (SAN-96, JS <c>updateTutorial</c>): the road's prompts (<see cref="StretchData.Prompts"/>) shown
    /// one at a time, in order. A prompt shows once every earlier one is done, Kage has reached its position and the road is being walked
    /// (not during an encounter); it is done when Kage performs its step while it shows. Completion is read from what the fight did
    /// (ADR-0003: the event stream, and Kage's own state), never from the keys:
    /// <list type="table">
    /// <item><term>move</term><description>Kage walked or ran 140 px (1.2 m) on the ground while it showed (JS <c>tutorialWalk</c>).</description></item>
    /// <item><term>jump</term><description>Kage left the ground into the air state.</description></item>
    /// <item><term>attack</term><description><c>MoveStarted</c> by Kage on an attack move (a hit, throw, projectile or counter move; not a dash or roll).</description></item>
    /// <item><term>guard</term><description><c>Blocked</c> with Kage the target (a <c>Parried</c> hit counts too: it was guarded).</description></item>
    /// <item><term>parry</term><description><c>Parried</c> with Kage the target.</description></item>
    /// <item><term>dodge</term><description><c>PerfectDodged</c> with Kage the target (a plain dash or roll does not count).</description></item>
    /// </list>
    /// A step with a <see cref="StretchPrompt.Partner"/> (guard, parry, dodge) is practised against that character: while such a prompt
    /// shows, a <b>practice partner</b> stands ahead of Kage, walks up and throws one slow attack at a time at fixed intervals
    /// (<see cref="PracticeInput"/>, no random numbers), with so much health that a stray combo cannot finish it. It leaves the moment
    /// the prompt is done (so it never stands in his way to the next one), or when the prompt stops showing (an encounter starts, or
    /// Kage goes back before its position).
    /// Runs in the late tick (<see cref="ILateTick"/>) after the session, so it is deterministic.
    /// </summary>
    public sealed class StretchTutorial : ILateTick, IDisposable
    {
        /// <summary>JS STRETCH.tutorialWalk: how far Kage must walk to clear the move prompt (140 px).</summary>
        public static readonly float WalkDistance = (float)JsUnits.PxToMetres(140);

        /// <summary>A cleared prompt stays on screen, crossed out, this many frames before it is gone (JS 1.2 s).</summary>
        public const int ClearedFrames = 72;

        /// <summary>The practice partner is placed this far ahead of Kage, or at the wall when that is nearer (metres).</summary>
        public const float PartnerAhead = 3.2f;

        /// <summary>A partner farther than this from Kage is put back ahead of him (he outran it).</summary>
        public const float PartnerLeash = 9f;

        /// <summary>The partner's move rate: half speed, so the draw that tells the blow is coming lasts about 16 frames (the tutorial Hollow's own 0.75 gives 11, a window of 8 is hard on a first parry).</summary>
        public const float PartnerMoveRate = 0.5f;

        /// <summary>The partner's health multiplier: the stray hits of a practice cannot knock it out.</summary>
        public const float PartnerHealthMul = 60f;

        readonly FightContext _ctx;
        readonly StretchSession _session;
        readonly Fighter _kage;
        readonly StretchPrompt[] _prompts;

        // what happened since the last late tick (set by OnEvent, read and cleared in LateTick)
        bool _attacked, _guarded, _parried, _dodged;
        bool _wasAir;
        float _lastX;
        bool _seen;
        float _walked;
        int _showFrames, _clearedAge = int.MaxValue;
        Fighter _partner;
        readonly List<(string step, int frame)> _done = new();

        public StretchData Data => _session.Data;
        /// <summary>The input device the prompts name (the last one used); the host sets it from <see cref="InputDeviceTracker"/>.</summary>
        public InputDeviceKind Device { get; set; } = InputDeviceKind.Keyboard;

        /// <summary>Prompts done so far.</summary>
        public int Done { get; private set; }
        public int Count => _prompts.Length;
        public bool Finished => Done >= _prompts.Length;

        /// <summary>The prompt on show, or null (none left, not reached yet, or an encounter is on).</summary>
        public StretchPrompt Current { get; private set; }
        /// <summary>Frames the current prompt has shown.</summary>
        public int ShowFrames => _showFrames;
        /// <summary>The prompt just done, for its crossed-out fade; null once it has gone. <see cref="ClearedAge"/> counts its frames.</summary>
        public StretchPrompt JustCleared { get; private set; }
        public int ClearedAge => _clearedAge;
        /// <summary>The practice partner standing now, or null.</summary>
        public Fighter Partner => _partner;
        /// <summary>Every prompt done, in order, with the frame it was done on.</summary>
        public IReadOnlyList<(string step, int frame)> Completed => _done;

        /// <summary>Raised when a prompt is done (the step id); for sound and the HUD.</summary>
        public event Action<string> PromptCleared;

        public StretchTutorial(FightContext ctx, StretchSession session, Fighter kage)
        {
            _ctx = ctx;
            _session = session;
            _kage = kage;
            _prompts = session.Data.Prompts ?? new StretchPrompt[0];
            ctx.Events.Published += OnEvent;
            ctx.LateTicks.Add(this);
        }

        public void Dispose()
        {
            _ctx.Events.Published -= OnEvent;
            _ctx.LateTicks.Remove(this);
            if (_partner != null) { _session.DismissExtra(_partner); _partner = null; }
        }

        // ---------------- what the player is asked to press ----------------

        /// <summary>The keys or buttons a prompt shows for <paramref name="device"/>, each from <see cref="KeyBindings.Label"/>.</summary>
        public static string[] Keys(string step, InputDeviceKind device)
        {
            switch (step)
            {
                case PromptStep.Move: return new[] { KeyBindings.Label(GameAction.Left, device), KeyBindings.Label(GameAction.Right, device) };
                case PromptStep.Jump: return new[] { KeyBindings.Label(GameAction.Jump, device) };
                case PromptStep.Attack: return new[] { KeyBindings.Label(GameAction.Weapon, device) };
                case PromptStep.Guard:
                case PromptStep.Parry: return new[] { KeyBindings.Label(GameAction.Guard, device) };
                case PromptStep.Dodge: return new[] { KeyBindings.Label(GameAction.Dash, device) };
                default: return new string[0];
            }
        }

        /// <summary>The current prompt's keys for the device in use; empty with no prompt.</summary>
        public string[] CurrentKeys => Current == null ? new string[0] : Keys(Current.Step, Device);

        /// <summary>What the HUD draws for the prompt on show (it fades in over 20 frames), or null.</summary>
        public PromptView? NowView() => Current == null ? null : new PromptView
        {
            verb = Current.Verb, hint = Current.Hint, keys = CurrentKeys, alpha = Mathf.Clamp01(_showFrames / 20f),
        };

        /// <summary>What the HUD draws for the prompt just done: gold, crossed out, floating up and fading (JS 1.2 s), or null.</summary>
        public PromptView? DoneView()
        {
            if (JustCleared == null) return null;
            float age = _clearedAge / 60f;
            return new PromptView
            {
                verb = JustCleared.Verb, hint = JustCleared.Hint, keys = Keys(JustCleared.Step, Device), done = true,
                alpha = 1f - Mathf.Clamp01((age - 0.45f) / 0.7f), rise = age * 75f,
            };
        }

        // ---------------- events: record only ----------------

        void OnEvent(CombatEvent e)
        {
            switch (e.Type)
            {
                case CombatEventType.MoveStarted:
                    if (e.Source == _kage && _kage.InAttackMove) _attacked = true;
                    break;
                case CombatEventType.Blocked:
                    if (e.Target == _kage) _guarded = true;
                    break;
                case CombatEventType.Parried:
                    if (e.Target == _kage) { _parried = true; _guarded = true; }
                    break;
                case CombatEventType.PerfectDodged:
                    if (e.Target == _kage) _dodged = true;
                    break;
            }
        }

        // ---------------- the late tick ----------------

        public void LateTick()
        {
            float x = _kage.Pos.x;
            bool air = _kage.state == FState.Air;
            bool jumped = air && !_wasAir;
            bool walking = _kage.Grounded && (_kage.state is FState.Walk or FState.Run);
            // the first frame, and a placement (a restart, a teleport), are not walking
            float dx = _seen && Mathf.Abs(x - _lastX) < 1f ? Mathf.Abs(x - _lastX) : 0f;
            _seen = true;
            _lastX = x;
            _wasAir = air;

            if (_clearedAge < int.MaxValue && ++_clearedAge > ClearedFrames) { JustCleared = null; _clearedAge = int.MaxValue; }

            bool road = _session.State == StretchState.Walking;
            var cur = road && Done < _prompts.Length && x >= (float)_prompts[Done].At ? _prompts[Done] : null;
            if (cur != Current) { Current = cur; _walked = 0f; _showFrames = 0; }
            if (cur != null)
            {
                _showFrames++;
                if (walking) _walked += dx;
                bool did = cur.Step switch
                {
                    PromptStep.Move => _walked >= WalkDistance,
                    PromptStep.Jump => jumped,
                    PromptStep.Attack => _attacked,
                    PromptStep.Guard => _guarded,
                    PromptStep.Parry => _parried,
                    PromptStep.Dodge => _dodged,
                    _ => false,
                };
                if (did) Complete(cur);
            }
            _attacked = _guarded = _parried = _dodged = false;

            TendPartner(road, x);
        }

        void Complete(StretchPrompt p)
        {
            Done++;
            Current = null;
            _walked = 0f;
            _showFrames = 0;
            JustCleared = p;
            _clearedAge = 0;
            _done.Add((p.Step, _ctx.Clock.Frame));
            PromptCleared?.Invoke(p.Step);
        }

        // ---------------- the practice partner ----------------

        void TendPartner(bool road, float x)
        {
            bool want = road && Current != null && Current.Partner != null;
            if (!want)
            {
                if (_partner != null) { _session.DismissExtra(_partner); _partner = null; }
                return;
            }
            if (_partner != null && _partner.state == FState.KO) { _session.DismissExtra(_partner); _partner = null; }
            if (_partner == null)
            {
                SpawnPartner(x);
                return;
            }
            if (Mathf.Abs(_partner.Pos.x - x) > PartnerLeash && _partner.Grounded) PlacePartner(x);
        }

        void SpawnPartner(float x)
        {
            string id = Current.Partner;
            var def = CharacterCatalog.Get(id) ?? StretchSession.ResolveFoe(id, out _);
            def.healthMul = PartnerHealthMul;
            def.moveRate = PartnerMoveRate;
            float px = PartnerX(x);
            _partner = _session.SpawnExtra(def, new PracticeInput(), px, px > x ? -1f : 1f);
        }

        void PlacePartner(float x)
        {
            float px = PartnerX(x);
            _partner.ResetForRound(px, px > x ? -1f : 1f);
            _partner.BeginFight();
        }

        float PartnerX(float kageX)
        {
            var b = _ctx.Bounds;
            float ahead = kageX + PartnerAhead;
            return ahead <= b.Right - 0.5f ? ahead : Mathf.Max(b.Left + 0.5f, kageX - PartnerAhead);
        }
    }

    /// <summary>
    /// The practice partner's input (SAN-96): it walks up to Kage and throws one weapon attack, waits, and does it again, on fixed frame
    /// counts, so a step practised against it is the same every run and the player can learn the timing. It never guards, stands still
    /// during the wait and rolls no random numbers.
    /// </summary>
    public sealed class PracticeInput : IInputSource
    {
        /// <summary>Frames before its first attack once it is in range.</summary>
        public const int FirstWait = 70;
        /// <summary>Frames it stands idle in range between one attack and the next.</summary>
        public const int Wait = 110;
        /// <summary>It attacks from this far (metres); farther, it walks up.</summary>
        public const float Reach = 1.25f;

        int _cool = FirstWait;

        public void Sample() { }

        public FrameInput Poll(Fighter self)
        {
            var t = self.opponent;
            if (t == null || t.state == FState.KO) return default;
            float d = t.Pos.x - self.Pos.x;
            if (Mathf.Abs(d) > Reach) return new FrameInput { dx = d > 0f ? 1 : -1 };
            if (self.Busy) return default;
            if (_cool > 0) { _cool--; return default; }
            _cool = Wait;
            return new FrameInput { s = true };
        }
    }
}
