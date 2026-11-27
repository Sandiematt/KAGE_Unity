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
        /// <summary>Live projectiles (Crescent, kunai...) and hazards: the fight loop ticks them (ADR-0002, step 6).</summary>
        public readonly List<Projectile> Projectiles = new();
        public readonly List<Hazard> Hazards = new();
        /// <summary>This frame's hit sweeps: recorded by the fighter tick, read by the clash and hit phases (ADR-0002).</summary>
        public readonly List<HitSweep> Sweeps = new();
        /// <summary>Each weapon family's move set, built on first use and shared by its fighters (ADR-0006).</summary>
        public readonly MoveSets MoveSets = new();
        /// <summary>Which foes may attack: at most two engaged at once, the rest wait and circle (SAN-83).</summary>
        public readonly EngagementManager Engagement = new();
        /// <summary>The floor's left and right limit: ±7.6 m unless a stretch session moves it (SAN-89).</summary>
        public readonly StageBounds Bounds = new();
        /// <summary>Things that react to the frame in the late tick (ADR-0002): a stretch's waves, gates and events (SAN-89).</summary>
        public readonly List<ILateTick> LateTicks = new();
        /// <summary>
        /// The legacy switch (combat.json), read once when the fight is built: hits resolve inside the
        /// attacker's tick with the old distance checks, and no sweeps are recorded; fighters are kept
        /// apart by the old manual push, and knockback ignores mass.
        /// </summary>
        public readonly bool LegacyHits;
        /// <summary>
        /// The camera and the HUD the fight is shown with, for what a rage art needs from the screen (SAN-82: the camera's override and the
        /// cut-in band). Null in the headless harness and wherever nothing is shown; the art then only changes the fight.
        /// </summary>
        public CameraRig Camera;
        public Hud Hud;
        public KageAudio Audio;

        /// <summary>
        /// The physics scene the fight's bodies live in: the fight loop simulates it and every hit query (melee sweeps,
        /// projectiles, hazards) runs in it. The default physics scene, unless the fight was built with a physics world of
        /// its own (<see cref="FightBuilder"/>'s <c>ownPhysics</c>, SAN-115), whose <see cref="OwnScene"/> it then is.
        /// </summary>
        public UnityEngine.PhysicsScene2D Physics { get; private set; } = UnityEngine.Physics2D.defaultPhysicsScene;
        /// <summary>The scene holding a fight built with its own physics world; not valid otherwise. Unload it when the fight is over.</summary>
        public UnityEngine.SceneManagement.Scene OwnScene { get; private set; }

        public FightContext(int seed, bool manualClock)
        {
            Seed = seed;
            LegacyHits = Tuning.Get<CombatTuning>().legacyHitResolution;
            Random = new FightRandom(seed);
            Clock = new FightClock(manualClock);
        }

        internal void UseOwnScene(UnityEngine.SceneManagement.Scene scene)
        {
            OwnScene = scene;
            Physics = UnityEngine.PhysicsSceneExtensions2D.GetPhysicsScene2D(scene);
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
