using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Who a fighter is. A stop-gap until character data lands (CharacterDef, SAN-66): today's two
    /// looks, with the ids the event stream uses.
    /// </summary>
    public sealed class FighterSpec
    {
        public string id, displayName;
        public KageModel.Look look;
        public int sortOrder;
        public Color main, core, dark;
        public float scale = 1f;
        public bool isPlayer, canUltimate;

        public static FighterSpec Kage => new()
        {
            id = "kage", displayName = "KAGE", look = KageModel.Look.Kage, sortOrder = 1500,
            main = Palette.Trim, core = Color.white, dark = Palette.TrimDeep, isPlayer = true, canUltimate = true,
        };

        public static FighterSpec Ronin => new()
        {
            id = "ronin", displayName = "RONIN", look = KageModel.Look.Ronin, sortOrder = 1000,
            main = Palette.Amber, core = KMath.Hex("#FFF2D6"), dark = Palette.AmberDeep, scale = 1.04f,
        };
    }

    /// <summary>
    /// Builds a fight from code, with no game bootstrap (ADR-0002): creates the fight context and its
    /// fight loop, spawns fighters into it and wires their input sources and opponents. The dev arena
    /// (KageGame) and the headless harness both use it.
    /// <code>
    /// var b = new FightBuilder(root, seed: 61, manualClock: true);
    /// b.AddGround();
    /// var kage = b.AddFighter(FighterSpec.Kage, team: 0, input);
    /// var bag = b.AddBag(root, new Vector2(1.6f, 2.65f));
    /// var ctx = b.Build();       // attach listeners, then ctx.Begin()
    /// </code>
    /// </summary>
    public sealed class FightBuilder
    {
        readonly Transform _root;

        public FightContext Context { get; }
        public FightLoop Loop { get; }
        public MoveSet Moves { get; }

        public FightBuilder(Transform root, int seed, bool manualClock)
        {
            _root = root;
            ConfigurePhysics();
            Context = new FightContext(seed, manualClock);
            Loop = root.gameObject.AddComponent<FightLoop>();
            if (manualClock && Physics2D.simulationMode != SimulationMode2D.Script)
            {
                Loop.restoreSimulationMode = Physics2D.simulationMode;
                Physics2D.simulationMode = SimulationMode2D.Script;
            }
            Loop.Bind(Context);
            Moves = MoveSet.Build();
        }

        /// <summary>The 60 Hz step, the frictionless fighter material and the layer collision rules.</summary>
        public static void ConfigurePhysics()
        {
            Time.fixedDeltaTime = 1f / 60f;
            if (KageGame.NoFriction == null) KageGame.NoFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            Physics2D.IgnoreLayerCollision(KageGame.LayerFighter, KageGame.LayerFighter, true);
            Physics2D.IgnoreLayerCollision(KageGame.LayerRagdoll, KageGame.LayerRagdoll, true);
            Physics2D.IgnoreLayerCollision(KageGame.LayerRagdoll, KageGame.LayerFighter, true);
            Physics2D.IgnoreLayerCollision(KageGame.LayerProp, KageGame.LayerFighter, true);
            Physics2D.IgnoreLayerCollision(KageGame.LayerProp, KageGame.LayerRagdoll, true);
            Physics2D.IgnoreLayerCollision(KageGame.LayerProp, KageGame.LayerProp, true);
            Physics2D.IgnoreLayerCollision(0, KageGame.LayerRagdoll, true);
            Physics2D.IgnoreLayerCollision(0, KageGame.LayerFighter, true);
        }

        /// <summary>Ground and walls only, for a fight with no arena visuals.</summary>
        public void AddGround() => ArenaBuilder.BuildPhysics(_root);

        /// <summary>Spawns a fighter under the root, in tick order. Ids get "#n" from the second copy on.</summary>
        public Fighter AddFighter(FighterSpec spec, int team, IInputSource input)
        {
            var go = new GameObject(spec.displayName);
            go.transform.SetParent(_root, false);
            var f = go.AddComponent<Fighter>();
            f.Setup(spec.displayName, KageModel.Build(spec.look), Moves, spec.sortOrder, spec.main, spec.core, spec.dark);
            if (spec.scale != 1f) go.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);
            f.isPlayer = spec.isPlayer;
            f.canUltimate = spec.canUltimate;
            f.input = input;
            f.team = team;
            int copies = 0;
            foreach (var o in Context.Fighters) if (o.CharacterId == spec.id) copies++;
            f.CharacterId = spec.id;
            f.FighterId = copies == 0 ? spec.id : spec.id + "#" + (copies + 1);
            f.ctx = Context;
            Context.Fighters.Add(f);
            if (Context.Clock.Manual) Headless(go);
            return f;
        }

        /// <summary>The training bag, registered as a prop.</summary>
        public PunchingBag AddBag(Transform parent, Vector2 anchor)
        {
            var bag = PunchingBag.Build(parent, anchor);
            Context.Props.Add(bag);
            if (Context.Clock.Manual) Headless(bag.gameObject);
            return bag;
        }

        /// <summary>
        /// Wires each fighter's opponent to the first fighter of another team, and the first prop as
        /// the extra target of a fighter with no opponent. Returns the context; attach listeners, then call Begin.
        /// </summary>
        public FightContext Build()
        {
            foreach (var f in Context.Fighters)
            {
                f.opponent = null;
                foreach (var o in Context.Fighters) if (o.team != f.team) { f.opponent = o; break; }
                f.extraTarget = f.opponent == null && Context.Props.Count > 0 ? Context.Props[0] : null;
            }
            return Context;
        }

        /// <summary>
        /// On a manual clock transforms must follow the bodies exactly at each simulated frame, so body
        /// interpolation (a rendering aid) is off.
        /// </summary>
        static void Headless(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.interpolation = RigidbodyInterpolation2D.None;
        }
    }
}
