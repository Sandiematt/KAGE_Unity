using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Owns the 60 Hz step (ADR-0002). Fighters have no FixedUpdate of their own: one
    /// <see cref="Step"/> is one frame, and it runs every fighter in <c>ctx.Fighters</c> order:
    /// <list type="number">
    /// <item>If the clock freezes this frame (hitstop, slow motion), only end the frame.</item>
    /// <item>World time scale (SAN-92): bodies the last frame slowed get their logic velocity back
    /// (<see cref="Fighter.RestoreWorldTime"/>). A fighter the slowed world doesn't advance this frame
    /// (<see cref="FightClock.Advances"/>) skips its input and tick, so it records no sweeps either.</item>
    /// <item>Push settle: a fighter the last physics step pushed gets its own horizontal speed back
    /// (SAN-68, Fighter.SettlePush). Under the legacy switch the old manual push runs here instead.</item>
    /// <item>Targets (SAN-83): each fighter refreshes its target list and picks its current target
    /// (<see cref="Fighter.PickTarget"/>); then the engagement manager decides which foes may attack
    /// (<see cref="EngagementManager.Update"/>).</item>
    /// <item>Input: poll every fighter's input source, in fighter order.</item>
    /// <item>Fighter tick: state machine, root motion, move playback; each active hit point records a
    /// <see cref="HitSweep"/> in <c>ctx.Sweeps</c>.</item>
    /// <item>Clashes: <see cref="ResolveClashes"/> (SAN-93, FightLoop.Clash.cs).</item>
    /// <item>Hits: every sweep, in fighter order, is cast against the Hurtbox layer (SAN-65). A sweep
    /// whose owner was interrupted earlier in this phase is dropped, so the first fighter to hit wins a trade.
    /// With the legacy switch on, hits resolve inside the attacker's tick instead, as before SAN-65.</item>
    /// <item>Projectiles and hazards: each live <see cref="Projectile"/> moves and casts, each <see cref="Hazard"/>
    /// counts its telegraph or strikes (SAN-75). Both run in play and on a manual clock alike.</item>
    /// <item>Boss phases (SAN-102): each boss's <see cref="PhaseController"/> opens the phases its health has fallen into and publishes
    /// <see cref="CombatEventType.BossPhaseChanged"/>; the AI sees the new phase on the next frame's poll.</item>
    /// <item>Late tick (ADR-0002 step 7): each <see cref="ILateTick"/> in <c>ctx.LateTicks</c> reacts to the frame: a stretch
    /// spawns its next wave, opens a gate, publishes its events (SAN-89). A foe it spawns takes part from the next frame.</item>
    /// <item>Push: each fighter puts its push collider on the Push or PushPassThrough layer for this frame
    /// (SAN-68); the physics step that follows keeps fighters apart. Off under the legacy switch.</item>
    /// <item>World time scale: each slowed body's velocity and gravity are scaled for the physics step that follows
    /// (<see cref="Fighter.ScaleWorldTime"/>); at scale 0 it is held still.</item>
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
        readonly List<Projectile> _projectileTmp = new();
        readonly List<Hazard> _hazardTmp = new();

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
                var clock = ctx.Clock;
                foreach (var f in fs) if (Live(f)) f.RestoreWorldTime();
                if (ctx.LegacyHits)
                {
                    for (int i = 0; i < fs.Count; i++)
                        for (int j = i + 1; j < fs.Count; j++)
                            if (Live(fs[i]) && Live(fs[j])) Pushbox(fs[i], fs[j]);
                }
                else foreach (var f in fs) if (Live(f)) f.SettlePush();
                foreach (var f in fs) if (Live(f)) f.PickTarget();
                ctx.Engagement.Update(fs);
                foreach (var f in fs) if (Live(f) && clock.Advances(f)) f.PollInput();
                ctx.Sweeps.Clear();
                foreach (var f in fs) if (Live(f) && clock.Advances(f)) f.Tick();
                if (!ctx.LegacyHits && ctx.Sweeps.Count > 0)
                {
                    ResolveClashes(ctx.Sweeps);
                    ResolveHits(ctx.Sweeps);
                }
                if (ctx.Projectiles.Count > 0)
                {
                    _projectileTmp.Clear();
                    _projectileTmp.AddRange(ctx.Projectiles);
                    foreach (var p in _projectileTmp) if (p != null) p.Tick();
                }
                if (ctx.Hazards.Count > 0)
                {
                    _hazardTmp.Clear();
                    _hazardTmp.AddRange(ctx.Hazards);
                    foreach (var h in _hazardTmp) if (h != null) h.Tick();
                }
                foreach (var f in fs) if (Live(f)) f.Phases?.Tick();      // boss phases, after this frame's hits and hazards (SAN-102, ADR-0008)
                for (int i = 0; i < ctx.LateTicks.Count; i++) ctx.LateTicks[i].LateTick();
                foreach (var f in fs) if (Live(f)) f.SyncPush();
                foreach (var f in fs) if (Live(f)) f.ScaleWorldTime(clock.ScaleFor(f));
            }
            ctx.Clock.EndFrame();
            return run;
        }

        /// <summary>
        /// One whole frame of a headless fight (manual clock): <see cref="Step"/>, then
        /// a physics step of the fight's physics scene (<see cref="FightContext.Physics"/>), then each fighter's pose output at that exact frame (what LateUpdate does in play).
        /// </summary>
        public bool StepHeadless()
        {
            bool ran = Step();
            if (!ran) return false;
            float dt = Time.fixedDeltaTime;
            Context.Physics.Simulate(dt);
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

        /// <summary>
        /// The legacy push: moves overlapping fighters apart by position, half each (all of it for the one not cornered).
        /// A fighter pressed against the wall it would be pushed into (<see cref="Fighter.WallSide"/>) stays put.
        /// Public so EditMode tests can push two fighters apart without stepping a fight.
        /// </summary>
        public static void Pushbox(Fighter a, Fighter b)
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
            // the wall's side comes from the fight's bounds, not the sign of x, which only said it in the dev arena (SAN-131)
            if (a.WallSide == -dir) { sa = 0f; sb = 1f; }
            if (b.WallSide == dir) { sa = 1f; sb = 0f; }
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
