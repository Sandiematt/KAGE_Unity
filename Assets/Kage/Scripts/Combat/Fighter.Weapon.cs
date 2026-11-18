using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The equipped weapon (ADR-0006): its family (move set, grip, blade, rage art, drawing), its level's combat
    /// multipliers, and the attack playback rate they give with the character's move rate.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>The family the equipped weapon fights with.</summary>
        public WeaponFamilyDef Family { get; private set; }
        /// <summary>JS id of the equipped weapon.</summary>
        public string WeaponId { get; private set; }
        public int WeaponLevel { get; private set; } = 1;
        /// <summary>The weapon's damage, speed and reach multipliers at its level, against its family's reference weapon.</summary>
        public WeaponCombat Weapon { get; private set; } = WeaponCombat.One;

        /// <summary>Attack playback rate: the character's move rate × the weapon's speed. 1 = authored frames.</summary>
        public float PlaybackRate => MoveRate * Weapon.speed;

        /// <summary>True when the family has a blade (every family but the fists).</summary>
        public bool HasBlade => Family == null || Family.HasBlade;

        /// <summary>No blade to fight with: the fists family, or a broken sword (SAN-113).</summary>
        bool BareHanded => SwordBroken || !HasBlade;

        /// <summary>The stance's two-handed grip is held: a whole blade held with both hands (katana, heavy tachi, polearm).</summary>
        bool GripHeld => !BareHanded && (Family == null || Family.grip is GripPose.TwoHand or GripPose.HeavyTwoHand or GripPose.Polearm);

        Vector2 _bladeTip;
        bool _bladeSet;

        /// <summary>Sword-bone local tip of the Blade hit points: the family's blade length × the weapon's reach.</summary>
        public Vector2 BladeTipLocal => _bladeSet ? _bladeTip : def.bladeTipLocal;

        /// <summary>Metres from guard to the last Blade hit point.</summary>
        public float BladeReach => (BladeTipLocal - def.bladeBaseLocal).magnitude;

        MoveSets _ownSets;

        /// <summary>
        /// Equips a weapon: its family (by its JS base, ADR-0006) gives the move set, grip, blade and drawing, its
        /// level the multipliers. Null = the reference weapon of the character's family. A weapon whose family isn't
        /// registered yet falls back to the character's family, then to the katana. A move in progress is dropped.
        /// </summary>
        public void EquipWeapon(string weaponId, int level = 1)
        {
            var fam = WeaponFamilies.Get(WeaponFamilies.FamilyIdOf(weaponId));
            if (fam == null)
            {
                fam = WeaponFamilies.Get(Character?.family) ?? WeaponFamilies.Get(WeaponFamilies.Katana);
                if (weaponId != null && WeaponFamilies.FamilyIdOf(weaponId) != fam.id)
                    Debug.LogWarning($"{FighterId ?? fighterName}: weapon '{weaponId}' has no registered family yet; fighting with the {fam.id} set");
            }
            weaponId ??= fam.referenceWeapon;

            if (state == FState.Attack) SetState(FState.Idle);
            if (Family != null && Family != fam) Family.visual?.Detach(rig);
            Family = fam;
            WeaponId = weaponId;
            WeaponLevel = Mathf.Max(1, level);
            Weapon = WeaponStats.Combat(weaponId, WeaponLevel);
            moves = (ctx != null ? ctx.MoveSets : _ownSets ??= new MoveSets()).Get(fam.id);
            SetBlade();
            fam.visual?.Attach(rig, weaponId);
            if (SwordBroken) rig.SetSwordVisible(false);
        }

        /// <summary>
        /// The Blade hit points run from the guard to bladeLength × reach along the drawn blade. Within a millimetre
        /// of the drawn blade they use it exactly (Kagetsu at level 1 hits from the same points as always).
        /// </summary>
        void SetBlade()
        {
            Vector2 b = def.bladeBaseLocal, t = def.bladeTipLocal;
            float drawn = (t - b).magnitude;
            float want = Family.bladeLength * Weapon.reach;
            _bladeSet = Family.HasBlade && drawn > 0f && Mathf.Abs(want - drawn) > 1e-3f;
            _bladeTip = _bladeSet ? b + (t - b) * (want / drawn) : t;
        }
    }
}
