using System.Collections.Generic;

namespace Kage.Rules
{
    // Lookups by id over the generated tables (JsTables.g.cs). Hand-written; the data itself is never edited.
    public static partial class JsTables
    {
        static Dictionary<string, JsWeapon> weaponById;
        static Dictionary<string, JsGear> helmetById, armorById;
        static Dictionary<string, JsEnemy> enemyById;
        static Dictionary<string, JsBossAbility> bossAbilityById;
        static Dictionary<string, JsMove> moveById;
        static Dictionary<string, JsProjectile> projectileById;
        static Dictionary<string, JsAiRole> aiRoleById;
        static Dictionary<string, JsDifficulty> difficultyById;
        static Dictionary<string, JsFightStyle> fightStyleById;
        static Dictionary<string, JsStage> stageById;
        static Dictionary<string, JsTier> tierById;
        static JsStage[] allStages;

        static Dictionary<string, T> Index<T>(IEnumerable<T> rows, System.Func<T, string> id)
        {
            var map = new Dictionary<string, T>();
            foreach (var r in rows) map[id(r)] = r;
            return map;
        }

        static T Find<T>(Dictionary<string, T> map, string id) where T : class
        {
            T row;
            return id != null && map.TryGetValue(id, out row) ? row : null;
        }

        /// <summary>The weapon with this id, or null.</summary>
        public static JsWeapon Weapon(string id)
        {
            if (weaponById == null) weaponById = Index(Weapons, w => w.Id);
            return Find(weaponById, id);
        }

        /// <summary>The helmet with this id, or null.</summary>
        public static JsGear Helmet(string id)
        {
            if (helmetById == null) helmetById = Index(Helmets, g => g.Id);
            return Find(helmetById, id);
        }

        /// <summary>The armour with this id, or null.</summary>
        public static JsGear Armor(string id)
        {
            if (armorById == null) armorById = Index(Armors, g => g.Id);
            return Find(armorById, id);
        }

        /// <summary>The foe with this id (the Prologue and Act I foes only), or null.</summary>
        public static JsEnemy Enemy(string id)
        {
            if (enemyById == null) enemyById = Index(Enemies, e => e.Id);
            return Find(enemyById, id);
        }

        /// <summary>The tier with this id (starter, common, rare, epic, trophy, boss), or null.</summary>
        public static JsTier Tier(string id)
        {
            if (tierById == null) tierById = Index(GearTiers, t => t.Id);
            return Find(tierById, id);
        }

        /// <summary>Every stage of the Prologue and Act I, in play order.</summary>
        public static JsStage[] Stages
        {
            get
            {
                if (allStages == null)
                {
                    var list = new List<JsStage>();
                    foreach (var ch in Chapters) list.AddRange(ch.Stages);
                    allStages = list.ToArray();
                }
                return allStages;
            }
        }

        /// <summary>The boss ability with this id (one the exported foes use), or null.</summary>
        public static JsBossAbility BossAbility(string id)
        {
            if (bossAbilityById == null) bossAbilityById = Index(BossAbilities, a => a.Id);
            return Find(bossAbilityById, id);
        }

        /// <summary>The basic move with this id (the exported ones: <c>p1</c>, <c>grab</c>), or null.</summary>
        public static JsMove Move(string id)
        {
            if (moveById == null) moveById = Index(Moves, m => m.Id);
            return Find(moveById, id);
        }

        /// <summary>The projectile with this id (<c>shock</c>, <c>kunai</c>...), or null.</summary>
        public static JsProjectile Projectile(string id)
        {
            if (projectileById == null) projectileById = Index(Projectiles, p => p.Id);
            return Find(projectileById, id);
        }

        /// <summary>The foe role with this id (<c>regular</c>, <c>sub</c>, <c>boss</c>), or null.</summary>
        public static JsAiRole AiRole(string id)
        {
            if (aiRoleById == null) aiRoleById = Index(AiRoles, r => r.Id);
            return Find(aiRoleById, id);
        }

        /// <summary>The difficulty mode with this id (<c>easy</c>, <c>normal</c>, <c>hard</c>, <c>impossible</c>), or null.</summary>
        public static JsDifficulty Difficulty(string id)
        {
            if (difficultyById == null) difficultyById = Index(Difficulties, d => d.Id);
            return Find(difficultyById, id);
        }

        /// <summary>The fighting style of the foe with this id (the exported foes that have one), or null.</summary>
        public static JsFightStyle FightStyle(string id)
        {
            if (fightStyleById == null) fightStyleById = Index(FightStyles, s => s.Id);
            return Find(fightStyleById, id);
        }

        /// <summary>The stage with this id, or null.</summary>
        public static JsStage Stage(string id)
        {
            if (stageById == null) stageById = Index(Stages, s => s.Id);
            return Find(stageById, id);
        }

        /// <summary>The chapter with this id (prologue or act1), or null.</summary>
        public static JsChapter Chapter(string id)
        {
            foreach (var ch in Chapters) if (ch.Id == id) return ch;
            return null;
        }
    }
}
