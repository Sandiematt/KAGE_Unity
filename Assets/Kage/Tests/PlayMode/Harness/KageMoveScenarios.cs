using System;
using System.Collections.Generic;

namespace Kage.Tests
{
    /// <summary>One scripted run of the baseline replay.</summary>
    public sealed class MoveScenario
    {
        public string name;
        public Foe foe;
        public float kageX = HeadlessFight.KageX, roninX = 0.6f, meter;
        public int frames = 150;
        public Action<ScriptedInput> kage;
        public Action<ScriptedInput> ronin;     // Foe.Scripted only; the AI takes over when it ends

        public override string ToString() => name;   // the test case name

        public HeadlessFight Play(int seed)
        {
            var f = new HeadlessFight(seed, foe, kageX, roninX, meter);
            kage?.Invoke(f.KageInput);
            if (f.RoninInput != null) ronin?.Invoke(f.RoninInput);
            f.Run(frames);
            return f;
        }
    }

    /// <summary>
    /// The baseline replay's scripts: every current Kage move, against the training bag and against
    /// the AI. Each script gives the inputs that start the move (chains are pressed inside their cancel
    /// windows): against the bag after a short wait, against the AI on the first frame, before the AI's
    /// opening move lands. A few moves need the Ronin to do something first; those script him for the
    /// opening frames and then hand him to the AI.
    /// </summary>
    public static class KageMoveScenarios
    {
        public const int Seed = 61;

        /// <summary>The moves the replay must start at least once (every MoveDef in KageMoves, plus the rage art).</summary>
        public static readonly string[] AllKageMoves =
        {
            "jab", "cross", "uppercut", "crouchPunch", "lunge", "kick1", "kick2", "axe", "sweep", "push",
            "throwWhiff", "throw", "airKick", "airPunch", "airSlash", "roll", "backflip", "groundPound", "getUp", "getUpFront",
            "iai", "risingMoon", "cleave", "airCleave", "shadowStep", "lowReap", "counterStance", "counterSlash", "crescent", Fighter.CrimsonMoon,
        };

        static readonly (string name, float x, int frames, Action<ScriptedInput> script)[] Moves =
        {
            ("jab", 0f, 90, s => s.Add(1, buttons: "P")),
            ("jab_cross_uppercut", 0f, 150, s => s.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(9).Add(1, buttons: "P")),
            ("crouchPunch_uppercut", 0f, 120, s => s.Add(1, dy: -1, buttons: "P").Add(6, dy: -1).Add(1, buttons: "P")),
            ("lunge_cross", -0.4f, 120, s => s.Add(1, fwd: 1, buttons: "P").Add(9).Add(1, buttons: "P")),
            ("kick1_kick2_axe", 0f, 150, s => s.Add(1, buttons: "K").Add(10).Add(1, buttons: "K").Add(11).Add(1, buttons: "K")),
            ("jab_kick1_iai", 0f, 150, s => s.Add(1, buttons: "P").Add(7).Add(1, buttons: "K").Add(11).Add(1, buttons: "S")),
            ("cross_kick2", 0f, 120, s => s.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(9).Add(1, buttons: "K")),
            ("axe", 0f, 100, s => s.Add(1, fwd: -1, buttons: "K")),
            ("sweep", 0f, 100, s => s.Add(1, dy: -1, buttons: "K")),
            ("push", 0f, 100, s => s.Add(1, fwd: 1, buttons: "K")),
            ("airKick", 0f, 120, s => s.Add(10, fwd: 1, dy: 1).Add(1, buttons: "K")),
            ("airPunch", 0f, 120, s => s.Add(10, fwd: 1, dy: 1).Add(1, buttons: "P")),
            ("airSlash", 0f, 120, s => s.Add(10, fwd: 1, dy: 1).Add(1, buttons: "S")),
            ("airCleave", 0f, 120, s => s.Add(10, fwd: 1, dy: 1).Add(1, dy: -1, buttons: "S")),
            ("roll", -0.4f, 90, s => s.Add(1, tapFwd: true)),
            ("backflip", 0f, 100, s => s.Add(1, tapBack: true)),
            ("iai_risingMoon_cleave", 0f, 160, s => s.Add(1, buttons: "S").Add(10).Add(1, buttons: "S").Add(10).Add(1, buttons: "S")),
            ("shadowStep", -0.6f, 110, s => s.Add(1, fwd: 1, buttons: "S")),
            ("lowReap", 0f, 100, s => s.Add(1, dy: -1, buttons: "S")),
            ("counterStance", 0f, 90, s => s.Add(1, fwd: -1, buttons: "S")),
            ("crescent", -1.2f, 150, s => s.Add(1, buttons: "X")),
            ("crimsonMoon", 0f, 160, s => s.Add(1, dy: -1, buttons: "X")),
        };

        /// <summary>Kage's offset from the bag or the Ronin: x below is added to these.</summary>
        const float BagKageX = 0.95f, AiKageX = -0.37f, AiRoninX = 0.37f;
        const int BagWait = 4;

        public static IEnumerable<MoveScenario> All()
        {
            foreach (var (name, x, frames, script) in Moves)
                yield return new MoveScenario
                {
                    name = name + "_vs_bag", foe = Foe.Bag, kageX = BagKageX + x, frames = frames, meter = 100f,
                    kage = s => script(s.Add(BagWait)),
                };
            foreach (var (name, x, frames, script) in Moves)
                yield return new MoveScenario { name = name + "_vs_ai", foe = Foe.Ai, kageX = AiKageX + x, roninX = AiRoninX, frames = frames, kage = script, meter = 100f };

            // moves that need the Ronin to be (or do) something first
            yield return new MoveScenario
            {
                name = "throw_vs_ai", foe = Foe.Scripted, kageX = -0.38f, roninX = 0.38f, frames = 140,
                kage = s => s.Add(4).Add(1, fwd: 1, buttons: "P"), ronin = s => s.Add(12),
            };
            yield return new MoveScenario
            {
                name = "throwWhiff_vs_ai", foe = Foe.Scripted, kageX = -0.38f, roninX = 0.38f, frames = 120,
                kage = s => s.Add(4).Add(1, fwd: 1, buttons: "P"), ronin = s => s.Add(5).Add(14, dy: 1),
            };
            yield return new MoveScenario
            {
                name = "sweep_groundPound_vs_ai", foe = Foe.Scripted, kageX = -0.4f, roninX = 0.4f, frames = 170,
                kage = s => s.Add(4).Add(1, dy: -1, buttons: "K").Add(44, dy: -1).Add(1, dy: -1, buttons: "P"),
                ronin = s => s.Add(90),
            };
            yield return new MoveScenario
            {
                name = "getUp_after_sweep_vs_ai", foe = Foe.Scripted, kageX = -0.4f, roninX = 0.4f, frames = 200,
                ronin = s => s.Add(4).Add(1, dy: -1, buttons: "K").Add(80),
            };
            yield return new MoveScenario
            {
                name = "getUpFront_after_throw_vs_ai", foe = Foe.Scripted, kageX = -0.38f, roninX = 0.38f, frames = 220,
                ronin = s => s.Add(4).Add(1, fwd: 1, buttons: "P").Add(120),
            };
            yield return new MoveScenario
            {
                name = "counterSlash_vs_ai", foe = Foe.Scripted, kageX = -0.38f, roninX = 0.38f, frames = 120,
                kage = s => s.Add(1, fwd: -1, buttons: "S"), ronin = s => s.Add(2).Add(1, buttons: "P").Add(60),
            };
        }
    }
}
