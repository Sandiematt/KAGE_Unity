using System;

namespace Kage
{
    /// <summary>
    /// How a family holds its weapon (ADR-0006). Only the grip the stance poses ask for with <see cref="Pose.Grip"/> is
    /// honoured, and only by families that hold the weapon with both hands (TwoHand, HeavyTwoHand, Polearm); every other
    /// grip lets the near hand go. <see cref="Fists"/> is bare hands: no blade, both fists up.
    /// </summary>
    public enum GripPose { OneHand, TwoHand, Dual, Polearm, HeavyTwoHand, Fists }

    /// <summary>Draws a family's weapon on the hand bones (ADR-0006).</summary>
    public interface IWeaponVisual
    {
        /// <summary>
        /// Shows <paramref name="weaponId"/> on the rig. Called when the weapon is equipped and again when a broken
        /// sword is restored, so it must be idempotent (re-hide what it hides, never add parts twice).
        /// </summary>
        void Attach(FighterRig rig, string weaponId);
        /// <summary>Undoes <see cref="Attach"/> before another family's weapon is equipped.</summary>
        void Detach(FighterRig rig);
    }

    /// <summary>
    /// A weapon family (ADR-0006): the move set, grip, blade length, rage art and drawing that the equipped weapon
    /// picks. One file per family in Combat/Families, registered with one line in <see cref="WeaponFamilies"/>.
    /// </summary>
    public sealed class WeaponFamilyDef
    {
        /// <summary>"katana", "twin", "spear", "chain", "cleaver", "fists", "tachi".</summary>
        public string id;
        /// <summary>JS weapon id whose stats count as ×1 for this family (<see cref="WeaponStats.Combat"/>).</summary>
        public string referenceWeapon;
        /// <summary>
        /// Builds the family's move set. Called once per fight (<see cref="MoveSets"/>) and shared by every fighter of
        /// the family; <paramref name="sets"/> gives the other families' sets of the same fight, so a family can share
        /// moves by reference (fists reuse the katana set's punches and kicks).
        /// </summary>
        public Func<MoveSets, MoveSet> build;
        public GripPose grip;
        /// <summary>Metres from guard to tip of the reference weapon; the Blade hit points reach bladeLength × weapon reach. 0 = no blade.</summary>
        public float bladeLength;
        /// <summary>"crimsonMoon" for the katana, <see cref="Fighter.ThousandShadows"/> for every other family.</summary>
        public string rageArt;
        /// <summary>Draws the weapon; null draws nothing.</summary>
        public IWeaponVisual visual;

        /// <summary>
        /// A blade long enough to dig into the floor in the low poses (the tachi's): the grip keeps its point above the
        /// floor, as for a polearm (<see cref="Fighter"/> ApplyGrip). The spear does it through its grip.
        /// </summary>
        public bool keepPointOffFloor;

        public bool HasBlade => bladeLength > 0f;
    }
}
