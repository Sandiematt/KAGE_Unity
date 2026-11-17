using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Owns the 60 Hz step (ADR-0002). Fighters have no FixedUpdate of their own: one
    /// <see cref="Step"/> is one frame, and it runs every fighter in <c>ctx.Fighters</c> order:
    /// <list type="number">
    /// <item>If the clock freezes this frame (hitstop, slow motion), only end the frame.</item>
    /// <item>Push: keep fighters apart (moved from KageGame; SAN-68 replaces it with push colliders).</item>
    /// <item>Input: poll every fighter's input source, in fighter order.</item>
    /// <item>Fighter tick: state machine, root motion, move playback; each active hit point records a
    /// <see cref="HitSweep"/> in <c>ctx.Sweeps</c>.</item>
    /// <item>Clashes: <see cref="ResolveClashes"/> (SAN-93, FightLoop.Clash.cs).</item>
    /// <item>Hits: every sweep, in fighter order, is cast against the Hurtbox layer (SAN-65). A sweep
    /// whose owner was interrupted earlier in this phase is dropped, so the first fighter to hit wins a trade.
    /// With the legacy switch on, hits resolve inside the attacker's tick instead, as before SAN-65.</item>
    /// <item>Projectiles: on a manual clock, live Crescents move here (in play they still move in Update; SAN-75).</item>
    /// <item>End the frame: <c>Clock.Frame++</c>.</item>
    /// </list>
    /// In play, FixedUpdate calls Step and Unity simulates physics after it. On a manual clock nothing
    /// runs by itself: the harness calls <see cref="StepHeadless"/>, which also simulates physics and
    /// poses the rigs, so a fight is exactly repeatable.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public partial class FightLoop : MonoBehaviour
    {
        public FightContext Context { get; private set; }
        internal SimulationMode2D? restoreSimulationMode;
        readonly List<Crescent> _crescentTmp = new();

        public void Bind(FightContext ctx)
        {
            Context = ctx;
            if (ctx.Clock.Manual) GameFeel.ManualClock = ctx.Clock;
        }

        void FixedUpdate()
        {
            if (Context == null || Context.Clock.Manual) return;
            Step();
        }

        /// <summary>Runs one frame of fight logic. Returns false when the frame was frozen or skipped.</summary>
        public bool Step()
        {
            var ctx = Context;
            if (ctx == null) return false;
            bool run = ctx.Clock.BeginFrame();
            if (run)
            {
                var fs = ctx.Fighters;
                for (int i = 0; i < fs.Count; i++)
                    for (int j = i + 1; j < fs.Count; j++)
                        if (Live(fs[i]) && Live(fs[j])) Pushbox(fs[i], fs[j]);
                foreach (var f in fs) if (Live(f)) f.PollInput();
                ctx.Sweeps.Clear();
                foreach (var f in fs) if (Live(f)) f.Tick();
                if (!ctx.LegacyHits && ctx.Sweeps.Count > 0)
                {
                    ResolveClashes(ctx.Sweeps);
                    ResolveHits(ctx.Sweeps);
                }
                if (ctx.Clock.Manual && ctx.Crescents.Count > 0)
                {
                    _crescentTmp.Clear();
                    _crescentTmp.AddRange(ctx.Crescents);
                    foreach (var c in _crescentTmp) if (c != null) c.Advance(Time.fixedDeltaTime);
                }
            }
            ctx.Clock.EndFrame();
            return run;
        }

        /// <summary>
        /// One whole frame of a headless fight (manual clock): <see cref="Step"/>, then
        /// Physics2D.Simulate, then each fighter's pose output at that exact frame (what LateUpdate does in play).
        /// </summary>
        public bool StepHeadless()
        {
            bool ran = Step();
            if (!ran) return false;
            float dt = Time.fixedDeltaTime;
            Physics2D.Simulate(dt);
            float t = Context.Clock.Frame * dt;
            foreach (var f in Context.Fighters) if (Live(f)) f.PoseOutput(0f, dt, t);
            return true;
        }

        /// <summary>Phase 4 (ADR-0004 step 1). Declared here, implemented by SAN-93 in FightLoop.Clash.cs.</summary>
        partial void ResolveClashes(List<HitSweep> sweeps);

        /// <summary>Phase 5: each owner's sweeps, in fighter order (they are recorded in tick order).</summary>
        static void ResolveHits(List<HitSweep> sweeps)
        {
            int i = 0;
            while (i < sweeps.Count)
            {
                var owner = sweeps[i].owner;
                int j = i + 1;
                while (j < sweeps.Count && sweeps[j].owner == owner) j++;
                Hurtbox.SyncAll();      // again per owner: an earlier hit may have moved someone (Mirror Guard)
                if (owner != null) owner.ResolveSweeps(sweeps, i, j);
                i = j;
            }
        }

        static bool Live(Fighter f) => f != null && f.isActiveAndEnabled && f.def != null && f.rb != null;

        static void Pushbox(Fighter a, Fighter b)
        {
            if (a.state is FState.KO or FState.Thrown or FState.Ultimate || b.state is FState.KO or FState.Thrown or FState.Ultimate) return;
            if (a.IsDown || b.IsDown) return;
            if ((a.IsAttacking && a.move.PassThrough(a.moveFrame)) || (b.IsAttacking && b.move.PassThrough(b.moveFrame))) return;
            Vector2 pa = a.rb.position, pb = b.rb.position;
            if (Mathf.Abs(pa.y - pb.y) > 1.1f) return;
            float dx = pb.x - pa.x;
            const float min = 0.72f;
            if (Mathf.Abs(dx) >= min) return;
            float dir = Mathf.Abs(dx) < 0.001f ? a.facing : Mathf.Sign(dx);
            float overlap = min - Mathf.Abs(dx);
            float sa = 0.5f, sb = 0.5f;
            if (a.AtWall && Mathf.Sign(pa.x) == -dir) { sa = 0f; sb = 1f; }
            if (b.AtWall && Mathf.Sign(pb.x) == dir) { sa = 1f; sb = 0f; }
            a.rb.position = pa - new Vector2(dir * overlap * sa, 0f);
            b.rb.position = pb + new Vector2(dir * overlap * sb, 0f);
        }

        void OnDestroy()
        {
            if (Context != null && GameFeel.ManualClock == Context.Clock) GameFeel.ManualClock = null;
            if (restoreSimulationMode.HasValue) Physics2D.simulationMode = restoreSimulationMode.Value;
        }
    }
}
