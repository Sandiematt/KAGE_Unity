using System;

namespace Kage.Rules
{
    /// <summary>One encounter on a road (SAN-89): where it starts, where its gate is, and its waves. Distances are metres.</summary>
    public sealed class StretchEncounter
    {
        /// <summary>Road position that starts the encounter (JS encounter x).</summary>
        public double X;
        /// <summary>Road position of the gate: Kage cannot go past it until the encounter is cleared.</summary>
        public double Gate;
        /// <summary>Foe ids, one list per wave, in the order the waves arrive.</summary>
        public string[][] Waves = new string[0][];
    }

    /// <summary>
    /// The road of a stretch stage in metres (ADR-0010: JS pixels / 115), and the JS rules for it (Logic.encounterAt, stretchLimit,
    /// isStretchComplete). Read from the generated JS tables with <see cref="FromStage"/>; <see cref="FromId"/> also knows the
    /// dev test stretch of Footmen. Pure data and arithmetic: the session that plays it is <c>StretchSession</c>.
    /// </summary>
    public sealed class StretchData
    {
        /// <summary>The id of the test stretch of Footmen (not a campaign stage).</summary>
        public const string DevFootmenId = "dev.footmen";

        /// <summary>JS STRETCH.arena: the width of a locked encounter arena, ending at its gate (1500 px).</summary>
        public static readonly double ArenaWidth = JsUnits.PxToMetres(1500);

        /// <summary>Kage is at the end of the road when this close to it (the JS isStretchComplete default, 60 px).</summary>
        public static readonly double EndReach = JsUnits.PxToMetres(60);

        public string Id, Name, Env;
        /// <summary>Length of the road and where Kage starts on it (metres from its left end).</summary>
        public double Length, Start;
        public StretchEncounter[] Encounters = new StretchEncounter[0];

        /// <summary>The road of a stretch stage of the JS campaign, or null when the stage doesn't exist or isn't a stretch.</summary>
        public static StretchData FromStage(string stageId)
        {
            var st = JsTables.Stage(stageId);
            if (st == null || st.Kind != "stretch" || st.Stretch == null) return null;
            var s = st.Stretch;
            var d = new StretchData
            {
                Id = st.Id,
                Name = st.Name,
                Env = s.Env,
                Length = JsUnits.PxToMetres(s.Length),
                Start = JsUnits.PxToMetres(s.Start),
                Encounters = new StretchEncounter[s.Encounters.Length],
            };
            for (int i = 0; i < s.Encounters.Length; i++)
            {
                var e = s.Encounters[i];
                var waves = new string[e.Waves.Length][];
                for (int w = 0; w < waves.Length; w++) waves[w] = (string[])e.Waves[w].Clone();
                d.Encounters[i] = new StretchEncounter { X = JsUnits.PxToMetres(e.X), Gate = JsUnits.PxToMetres(e.Gate), Waves = waves };
            }
            return d;
        }

        /// <summary>A campaign stretch stage or the dev test stretch (<see cref="DevFootmenId"/>); null for anything else.</summary>
        public static StretchData FromId(string id) => id == DevFootmenId ? DevFootmen() : FromStage(id);

        /// <summary>
        /// The test stretch of Footmen: a 3000 px road with two encounters (a pair, then three; a pair, then four), for the dev arena
        /// and the fight-harness tests. Not JS data; it exists to be played with the one foe that is built.
        /// </summary>
        public static StretchData DevFootmen()
        {
            string[] f2 = { "order_footman", "order_footman" };
            string[] f3 = { "order_footman", "order_footman", "order_footman" };
            string[] f4 = { "order_footman", "order_footman", "order_footman", "order_footman" };
            return new StretchData
            {
                Id = DevFootmenId,
                Name = "Footmen Road",
                Env = "rooftops",
                Length = JsUnits.PxToMetres(3000),
                Start = JsUnits.PxToMetres(160),
                Encounters = new[]
                {
                    new StretchEncounter { X = JsUnits.PxToMetres(900), Gate = JsUnits.PxToMetres(1550), Waves = new[] { (string[])f2.Clone(), (string[])f3.Clone() } },
                    new StretchEncounter { X = JsUnits.PxToMetres(1900), Gate = JsUnits.PxToMetres(2550), Waves = new[] { (string[])f2.Clone(), (string[])f4.Clone() } },
                },
            };
        }

        /// <summary>The furthest Kage may walk: the gate of the first uncleared encounter, else the end of the road (JS stretchLimit).</summary>
        public double Limit(int cleared) => cleared >= 0 && cleared < Encounters.Length ? Math.Min(Encounters[cleared].Gate, Length) : Length;

        /// <summary>The index of the encounter Kage starts by standing at <paramref name="x"/>, or -1: the next uncleared one once he crosses its trigger (JS encounterAt).</summary>
        public int EncounterAt(int cleared, double x) => cleared >= 0 && cleared < Encounters.Length && x >= Encounters[cleared].X ? cleared : -1;

        /// <summary>Every encounter is cleared and Kage is within <paramref name="reach"/> of the end (default <see cref="EndReach"/>; JS isStretchComplete).</summary>
        public bool IsComplete(int cleared, double x, double? reach = null) => cleared >= Encounters.Length && x >= Length - (reach ?? EndReach);

        /// <summary>Left edge of the arena that locks when encounter <paramref name="encounter"/> starts: its gate less <see cref="ArenaWidth"/>, never before the road's left end.</summary>
        public double ArenaLeft(int encounter) => Math.Max(0, Encounters[encounter].Gate - ArenaWidth);

        /// <summary>Waves in the whole road.</summary>
        public int TotalWaves
        {
            get
            {
                int n = 0;
                foreach (var e in Encounters) n += e.Waves.Length;
                return n;
            }
        }
    }
}
