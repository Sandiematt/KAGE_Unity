using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// A weapon family's full move set (ADR-0006): every button on the ground, crouching, forward, back and in the
    /// air, with chains. Families share moves by reference (never by copying a <see cref="MoveDef"/>); Special + Down
    /// is not here (ADR-0004: rage art, kunai).
    /// </summary>
    public abstract class MoveSet
    {
        // body moves: built once and shared by every family of a fight (references, not copies)
        public MoveDef roll, backflip, getUp, getUpFront, throwGrab, throwWhiff;
        /// <summary>The Mirror Guard answer: katana family only; null elsewhere.</summary>
        public MoveDef counterSlash;

        /// <summary>Punch, Kick, Weapon and plain Special, by direction (relative to facing) and context. Null = the press does nothing.</summary>
        public abstract MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self);

        /// <summary>A named ability move for foes and bosses (ADR-0008); null when the family has none.</summary>
        public virtual MoveDef Ability(string abilityId) => null;

        /// <summary>Every move of the set, for "each move lands on the training bag" tests.</summary>
        public abstract IEnumerable<MoveDef> All { get; }

        /// <summary>
        /// The move to play instead of <paramref name="m"/> when the fighter has no blade (a broken sword or the fists
        /// family); null = no stand-in (the fighter then drops blade moves and keeps the rest).
        /// </summary>
        public virtual MoveDef BareHanded(MoveDef m) => null;

        /// <summary>
        /// The fighter is driven by a legacy AI (no profile, <see cref="AIInputSource.IsProfiled"/>): a Select keeps to the moves that AI always
        /// had (no reference kata, <see cref="MoveDef.profiledOnly"/>), so its fights replay as before.
        /// </summary>
        protected static bool LegacyAi(Fighter self) => self != null && self.input is AIInputSource ai && !ai.IsProfiled;

        /// <summary>Copies the shared body moves of <paramref name="from"/> (references).</summary>
        protected void ShareBodyMoves(MoveSet from)
        {
            roll = from.roll; backflip = from.backflip; getUp = from.getUp; getUpFront = from.getUpFront;
            throwGrab = from.throwGrab; throwWhiff = from.throwWhiff;
        }
    }

    /// <summary>
    /// The move sets of one fight (on <see cref="FightContext.MoveSets"/>): each family's set is built the first time a
    /// fighter of that family needs it, then shared by every fighter of the family.
    /// </summary>
    public sealed class MoveSets
    {
        readonly Dictionary<string, MoveSet> _sets = new();

        /// <summary>The set of a registered family; null for an unknown or unregistered one.</summary>
        public MoveSet Get(string familyId)
        {
            if (familyId == null) return null;
            if (_sets.TryGetValue(familyId, out var s)) return s;
            var fam = WeaponFamilies.Get(familyId);
            if (fam == null) return null;
            s = fam.build(this);
            _sets[familyId] = s;
            return s;
        }

        /// <summary>
        /// The set a fighter with <paramref name="style"/> fights with: the style's own set over its family's (Shizuka's iai string),
        /// built once per fight and shared; the family's set when the style plays no button differently (docs/weapon-styles.md).
        /// </summary>
        public MoveSet Get(string familyId, WeaponStyle style)
        {
            var family = Get(familyId);
            if (family == null || style == null || style.buildMoves == null) return family;
            string key = familyId + "+" + style.weaponId;
            if (_sets.TryGetValue(key, out var s)) return s;
            s = style.buildMoves(family);
            _sets[key] = s;
            return s;
        }
    }
}
