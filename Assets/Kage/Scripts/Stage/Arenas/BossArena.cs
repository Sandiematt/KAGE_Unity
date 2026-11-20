using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The arena placeholder of a sub-boss (SAN-103): the dev arena's floor and walls (<see cref="ArenaBuilder.BuildPhysics"/>) under the
    /// backdrop of the JS environment of the sub-boss's stage (<see cref="EnvBackdrop"/>, <see cref="EnvLook"/>). The stage is found in the
    /// generated campaign by its enemy (act1.sub1 is Kuroe's, env "rooftops"), so Garan, Hisame and Shiden get theirs (the districts) the
    /// moment they are in the catalog: nothing to add per boss. A foe with no sub-boss stage gets none, and the caller builds the shrine.
    /// </summary>
    public static class BossArena
    {
        /// <summary>
        /// The JS env id of the sub-boss or boss stage whose enemy is <paramref name="characterId"/>, or null. A stage with no env of its own fights in its
        /// chapter's (SAN-107: act1.boss, Raikon's, has none, so it is Act I's rooftops, as in the JS <c>st.env || ch.env</c>).
        /// </summary>
        public static string EnvFor(string characterId)
        {
            if (characterId == null) return null;
            foreach (var st in JsTables.Stages)
                if ((st.Kind == "subboss" || st.Kind == "boss") && st.Enemy == characterId) return st.Env ?? JsTables.Chapter(st.Chapter)?.Env;
            return null;
        }

        /// <summary>
        /// Builds the arena of <paramref name="foeId"/> under <paramref name="root"/> when the foe is a sub-boss; false (and nothing built)
        /// otherwise.
        /// </summary>
        public static bool TryBuild(Transform root, CameraRig cam, string foeId)
        {
            var env = EnvFor(foeId);
            if (env == null) return false;
            ArenaBuilder.BuildPhysics(root);
            EnvBackdrop.Build(root, cam, foeId == Raikon.Id ? EnvLook.RaikonStorm : EnvLook.For(env));      // Raikon fights under his storm (SAN-107)
            return true;
        }

        /// <summary>
        /// Builds the arena of a campaign boss stage (SAN-109) under <paramref name="root"/>: the stage's JS environment (its own, else its
        /// chapter's: <see cref="GameRules.BossEnvOf"/>), so Raikon fights on the Act I rooftops too. False (nothing built) for a stretch or an
        /// unknown stage, and the caller builds the shrine. The dev arena keeps <see cref="TryBuild"/> (by foe), so it is unchanged.
        /// </summary>
        public static bool TryBuildForStage(Transform root, CameraRig cam, string stageId)
        {
            var env = GameRules.BossEnvOf(stageId);
            if (env == null) return false;
            ArenaBuilder.BuildPhysics(root);
            EnvBackdrop.Build(root, cam, GameRules.Stage(stageId)?.Enemy == Raikon.Id ? EnvLook.RaikonStorm : EnvLook.For(env));      // Raikon fights under his storm (SAN-107)
            return true;
        }
    }
}
