using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// The per-fight services (ADR-0002). One per fight; nothing in a fight reaches for a static to
    /// get them. Built by <see cref="FightBuilder"/>.
    /// </summary>
    public sealed class FightContext
    {
        public readonly int Seed;
        public readonly FightRandom Random;
        public readonly FightClock Clock;
        public readonly CombatEvents Events = new();
        public readonly List<Fighter> Fighters = new();     // spawn order = tick order
        public readonly List<IHittable> Props = new();      // training bag, later breakables
        /// <summary>Live Crimson Crescents. On a manual clock the loop moves them; SAN-75 replaces this with projectiles.</summary>
        public readonly List<Crescent> Crescents = new();
        /// <summary>This frame's hit sweeps: recorded by the fighter tick, read by the clash and hit phases (ADR-0002).</summary>
        public readonly List<HitSweep> Sweeps = new();
        /// <summary>
        /// The legacy switch (combat.json), read once when the fight is built: hits resolve inside the
        /// attacker's tick with the old distance checks, and no sweeps are recorded; fighters are kept
        /// apart by the old manual push, and knockback ignores mass.
        /// </summary>
        public readonly bool LegacyHits;

        public FightContext(int seed, bool manualClock)
        {
            Seed = seed;
            LegacyHits = Tuning.Get<CombatTuning>().legacyHitResolution;
            Random = new FightRandom(seed);
            Clock = new FightClock(manualClock);
        }

        /// <summary>Publishes FightStarted (Index = seed). Call once, after listeners are attached.</summary>
        public void Begin() => Events.Publish(CombatEvent.FightStarted(Clock.Frame, Seed));

        /// <summary>Id of a hit target: the fighter id, or the prop id ("bag").</summary>
        public static string IdOf(IHittable t) => t switch
        {
            null => null,
            Fighter f => f.FighterId,
            PunchingBag b => b.PropId,
            _ => t.GetType().Name,
        };
    }
}
