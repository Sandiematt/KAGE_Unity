using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Builds a fight from code, with no game bootstrap (ADR-0002): creates the fight context and its
    /// fight loop, spawns fighters into it from character data (ADR-0005) and wires their input sources
    /// and opponents. The dev arena (KageGame), the campaign stage and the headless harness all use it.
    /// <code>
    /// var b = new FightBuilder(root, seed: 61, manualClock: true);
    /// b.AddGround();
    /// var kage = b.Spawn(CharacterCatalog.Get("kage"), team: 0, input);
    /// var bag = b.AddBag(root, new Vector2(1.6f, 2.65f));
    /// var ctx = b.Build();       // attach listeners, then ctx.Begin()
    /// </code>
    /// </summary>
    public sealed class FightBuilder
    {
        /// <summary>Draw order of the player's side and of the foes (the player is drawn in front).</summary>
        public const int PlayerSortOrder = 1500, FoeSortOrder = 1000;

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

        /// <summary>
        /// The 60 Hz step. The layer collision matrix and the physics materials are project settings and
        /// assets (ProjectSettings/Physics2DSettings.asset, Resources/Physics; docs/physics.md), not code.
        /// </summary>
        public static void ConfigurePhysics()
        {
            Time.fixedDeltaTime = 1f / 60f;
        }

        /// <summary>Ground and walls only, for a fight with no arena visuals.</summary>
        public void AddGround() => ArenaBuilder.BuildPhysics(_root);

        /// <summary>
        /// Spawns a fighter from character data under the root, in tick order: its part set draws it, and
        /// its mass, body scale, health (JS health × healthMul × the health scale), multipliers and flags are
        /// set. Ids get "#n" from the second copy on. Place it with <see cref="Fighter.ResetForRound"/>
        /// (or use the overload that does).
        /// </summary>
        public Fighter Spawn(CharacterDef def, int team, IInputSource input)
        {
            var partSet = CharacterCatalog.PartSet(def.partSet);
            if (partSet == null) throw new System.ArgumentException($"character {def.id}: no part set '{def.partSet}'");
            var rigDef = partSet.Build(def);
            if (def.hurt != null) { rigDef.hurt.Clear(); rigDef.hurt.AddRange(def.hurt); }

            var go = new GameObject(def.displayName);
            go.transform.SetParent(_root, false);
            var f = go.AddComponent<Fighter>();
            f.Setup(def, rigDef, MovesFor(def), team == 0 ? PlayerSortOrder : FoeSortOrder);
            if (def.bodyScale != 1f) go.transform.localScale = new Vector3(def.bodyScale, def.bodyScale, 1f);
            f.input = input;
            f.team = team;
            int copies = 0;
            foreach (var o in Context.Fighters) if (o.CharacterId == def.id) copies++;
            f.CharacterId = def.id;
            f.FighterId = copies == 0 ? def.id : def.id + "#" + (copies + 1);
            f.ctx = Context;
            Context.Fighters.Add(f);
            if (Context.Clock.Manual) Headless(go);
            return f;
        }

        /// <summary>Spawns a fighter from character data and places it at <paramref name="x"/>, facing <paramref name="facing"/>.</summary>
        public Fighter Spawn(CharacterDef def, int team, float x, float facing, IInputSource input)
        {
            var f = Spawn(def, team, input);
            f.ResetForRound(x, facing);
            return f;
        }

        /// <summary>The move set of the character's weapon family. Every character uses Kage's katana set until the families land (ADR-0006, SAN-70).</summary>
        MoveSet MovesFor(CharacterDef def) => Moves;

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
