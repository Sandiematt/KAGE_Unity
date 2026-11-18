using System.Collections.Generic;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The registered weapon families (ADR-0006) and which family each JS weapon fights with. A family ticket adds its
    /// files in Combat/Families and one line to <see cref="Register"/>.
    /// </summary>
    public static class WeaponFamilies
    {
        public const string Katana = "katana", Fists = "fists", Twin = "twin", Spear = "spear", Chain = "chain", Cleaver = "cleaver", Tachi = "tachi";

        static Dictionary<string, WeaponFamilyDef> _byId;

        static void Register()
        {
            Add(KatanaMoves.Family);
            Add(FistsMoves.Family);
            Add(TwinMoves.Family);
            Add(SpearMoves.Family);
            Add(CleaverMoves.Family);
            Add(TachiMoves.Family);
        }

        static void Add(WeaponFamilyDef f)
        {
            if (!ReferenceOf.TryGetValue(f.id, out var reference) || reference != f.referenceWeapon)
                throw new System.InvalidOperationException($"weapon family {f.id}: reference weapon must be {reference} (ADR-0006)");
            _byId[f.id] = f;
        }

        /// <summary>A registered family, or null (unknown id, or a family whose ticket hasn't landed yet).</summary>
        public static WeaponFamilyDef Get(string familyId)
        {
            if (_byId == null) { _byId = new Dictionary<string, WeaponFamilyDef>(); Register(); }
            return familyId != null && _byId.TryGetValue(familyId, out var f) ? f : null;
        }

        /// <summary>Each family's reference weapon (ADR-0006 table), registered or not.</summary>
        static readonly Dictionary<string, string> ReferenceOf = new()
        {
            { Katana, "kagetsu" }, { Fists, "fists" }, { Twin, "tsubame" }, { Spear, "mizuchi" },
            { Chain, "yamigama" }, { Cleaver, "onikiri" }, { Tachi, "raiko_no_tachi" },
        };

        /// <summary>The family of a JS root weapon (one with no <c>base</c>). Iwakudaki (hammer) fights as a cleaver until a hammer family exists.</summary>
        static readonly Dictionary<string, string> RootFamily = new()
        {
            { "kagetsu", Katana }, { "fists", Fists }, { "tsubame", Twin }, { "mizuchi", Spear },
            { "yamigama", Chain }, { "onikiri", Cleaver }, { "iwakudaki", Cleaver },
        };

        /// <summary>Weapons whose family isn't their JS base's: the heavy tachi has its own family in Unity (its JS base is Onikiri).</summary>
        static readonly Dictionary<string, string> Exceptions = new()
        {
            { "raiko_no_tachi", Tachi }, { "raikon_tachi", Tachi },
        };

        /// <summary>The family a JS weapon fights with, through its JS <c>base</c> (ADR-0006 table). Null for an unknown weapon.</summary>
        public static string FamilyIdOf(string weaponId)
        {
            if (weaponId == null) return null;
            if (Exceptions.TryGetValue(weaponId, out var fam)) return fam;
            var w = JsTables.Weapon(weaponId);
            if (w == null) return null;
            return RootFamily.TryGetValue(w.Base ?? w.Id, out fam) ? fam : null;
        }

        /// <summary>The reference weapon of a family, registered or not; null for an unknown family.</summary>
        public static string ReferenceWeapon(string familyId) => familyId != null && ReferenceOf.TryGetValue(familyId, out var r) ? r : null;

        /// <summary>True when <paramref name="weaponId"/> is some family's reference weapon (it is measured against itself).</summary>
        public static bool IsReference(string weaponId) => weaponId != null && ReferenceOf.ContainsValue(weaponId);

        /// <summary>The player weapons (not enemy-only) whose family is registered, in JS table order: the dev arena's weapon cycle.</summary>
        public static string[] PlayableWeapons()
        {
            var list = new List<string>();
            foreach (var w in JsTables.Weapons)
                if (!w.Enemy && Get(FamilyIdOf(w.Id)) != null) list.Add(w.Id);
            return list.ToArray();
        }
    }
}
