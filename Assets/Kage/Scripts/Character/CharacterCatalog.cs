using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// Every character entry and part set (ADR-0005), one line each. A new character adds its roster file,
    /// its part-set file and one line to each list below.
    /// </summary>
    public static class CharacterCatalog
    {
        static Dictionary<string, CharacterDef> _defs;
        static Dictionary<string, IPartSet> _partSets;

        /// <summary>
        /// A fresh save holding Kagetsu, the katana family's reference weapon (ADR-0006). A new player starts with the plain Mumei
        /// (<see cref="Rules.GameRules.StarterWeaponId"/>), but the catalog's Kage is the reference fighter every fight test, the baseline
        /// replay and the dev arena measure against, so he keeps the weapon the numbers are x1 for.
        /// </summary>
        static Rules.SaveData ReferenceSave()
        {
            var s = Rules.GameRules.DefaultSave(false);
            s.owned.weapons["kagetsu"] = 1;
            s.equipped.weapon = "kagetsu";
            return s;
        }

        static void Register()
        {
            // ---- characters ----
            Add(KageCharacter.Def(ReferenceSave()));     // the reference Kage: Kagetsu, the katana family's x1 weapon (tests, the dev arena's default)
            Add(Ronin.Def());              // dev only: today's recolour
            Add(TestHeavy.Def());          // dev only: the mass-3 dummy (SAN-68)
            Add(OrderFootman.Def());
            Add(OrderRunner.Def());
            Add(OrderShieldbearer.Def());
            Add(OrderIronclad.Def());
            Add(HollowHusk.Def());
            Add(OrderChainthrower.Def());
            Add(HollowShade.Def());
            Add(HollowFlicker.Def());
            Add(HollowTutorial.Def());
            Add(Raikon.Def());             // the Act I boss (look: SAN-71; fight: SAN-107)
            Add(Kuroe.Def());              // the first Act I sub-boss (SAN-103)
            Add(Garan.Def());              // the second Act I sub-boss (SAN-104)
            Add(Hisame.Def());             // the third Act I sub-boss (SAN-105)
            Add(Shiden.Def());             // the fourth Act I sub-boss (SAN-106)
            Add(FootmanBoss.Def());        // dev only: the two-phase test boss (SAN-102)

            // ---- part sets ----
            Add(new KagePartSet());
            Add(new RoninPartSet());
            Add(new OrderFootmanPartSet());
            Add(new OrderRunnerPartSet());
            Add(new OrderShieldbearerPartSet());
            Add(new OrderIroncladPartSet());
            Add(new HollowHuskPartSet());
            Add(new OrderChainthrowerPartSet());
            Add(new HollowShadePartSet());
            Add(new HollowFlickerPartSet());
            Add(new HollowTutorialPartSet());
            Add(new RaikonPartSet());
            Add(new KuroePartSet());
            Add(new GaranPartSet());
            Add(new HisamePartSet());
            Add(new ShidenPartSet());
        }

        static void Add(CharacterDef d) => _defs[d.id] = d;
        static void Add(IPartSet p) => _partSets[p.Id] = p;

        static void Ensure()
        {
            if (_defs != null) return;
            _defs = new Dictionary<string, CharacterDef>();
            _partSets = new Dictionary<string, IPartSet>();
            Register();
        }

        /// <summary>A copy of the entry with this id (change it freely), or null.</summary>
        public static CharacterDef Get(string id)
        {
            Ensure();
            return id != null && _defs.TryGetValue(id, out var d) ? d.Clone() : null;
        }

        /// <summary>The part set with this id, or null.</summary>
        public static IPartSet PartSet(string id)
        {
            Ensure();
            return id != null && _partSets.TryGetValue(id, out var p) ? p : null;
        }

        /// <summary>Every registered character id.</summary>
        public static IEnumerable<string> Ids
        {
            get { Ensure(); return _defs.Keys; }
        }
    }
}
