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

        /// <summary>The family holds a second blade in the near hand (twin blades) and has a blade to hold.</summary>
        public bool Dual => !BareHanded && Family != null && Family.grip == GripPose.Dual;

        /// <summary>Where the near hand sits from the far hand when a held-grip pose is split for dual wield (root space, facing right).</summary>
        static readonly Vector2 DualHandOffset = new(0.14f, 0.16f);

        /// <summary>Where the near hand sits from the far hand in a one-handed family's held stance (root space, facing right).</summary>
        static readonly Vector2 OneHandOffset = new(-0.08f, -0.2f);

        Vector2 _bladeTip, _bladeBase;
        bool _bladeSet;

        /// <summary>HandN-local start of the second (near hand) blade: just past the fist.</summary>
        public Vector2 NearBladeBaseLocal => def.fistLocal + Vector2.right * 0.05f;
        /// <summary>HandN-local tip of the second blade: the same length as the first. The near hand's own axis is the blade's.</summary>
        public Vector2 NearBladeTipLocal => NearBladeBaseLocal + Vector2.right * BladeReach;

        Vector2 StaticBladeTip => _bladeSet ? _bladeTip : def.bladeTipLocal;

        /// <summary>
        /// Sword-bone local tip of the Blade hit points: the family's blade length x the weapon's reach, plus the move's
        /// current extension along the blade (<see cref="BladeExtension"/>, the chain sickle's lash).
        /// </summary>
        public Vector2 BladeTipLocal
        {
            get
            {
                Vector2 t = StaticBladeTip;
                float e = BladeExtension;
                return e > 0f ? t + BladeDirLocal * e : t;
            }
        }

        /// <summary>Sword-bone local direction of the blade line (unit).</summary>
        public Vector2 BladeDirLocal => (StaticBladeTip - BladeBaseLocal).normalized;

        /// <summary>Metres the move has run the Blade line past the blade at the current move time (x the weapon's reach); 0 outside moves that extend.</summary>
        public float BladeExtension => BladeExtensionAt(0f);

        /// <summary>The extension <paramref name="alpha"/> of a logic frame past the current move time (a rendered frame between two logic frames).</summary>
        public float BladeExtensionAt(float alpha)
        {
            if (state != FState.Attack || move == null || move.extension.Count == 0) return 0f;
            return move.ExtensionAt(_moveTime + alpha * PlaybackRate) * Weapon.reach;
        }

        /// <summary>
        /// Sword-bone local start of the Blade hit points: the guard. A polearm's line starts a little behind its lead
        /// hand, so a foe closer than the hand is met by the shaft instead of being passed by the point.
        /// </summary>
        public Vector2 BladeBaseLocal => _bladeSet ? _bladeBase : def.bladeBaseLocal;

        /// <summary>Metres from guard to the last Blade hit point of the blade itself (the extension isn't counted).</summary>
        public float BladeReach => (StaticBladeTip - def.bladeBaseLocal).magnitude;

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
            float want = Character != null && Character.bladeLength > 0f ? Character.bladeLength : Family.bladeLength * Weapon.reach;     // a character's own blade, Raikon's painted tachi (SAN-107)
            bool polearm = Family.grip == GripPose.Polearm;
            _bladeSet = Family.HasBlade && drawn > 0f && (polearm || Mathf.Abs(want - drawn) > 1e-3f);
            // a polearm's line is its straight shaft (the katana's blade is drawn with a slight curve)
            Vector2 dir = polearm ? KMath.Dir(def.bladeBindAngle) : (t - b) / drawn;
            _bladeTip = _bladeSet ? b + dir * want : t;
            _bladeBase = polearm ? b - dir * 0.38f : b;
        }

        /// <summary>
        /// Final touches on a posed hand grip: no handle to hold or a family that holds the weapon one-handed lets go;
        /// a polearm holds its hands wide apart (<see cref="Pose.PolearmSpacing"/>), the heavy cleaver grip a hand apart
        /// (<see cref="Pose.HeavySpacing"/>), and a polearm's point never dips below the
        /// floor (a low guard or a sweep tilts the shaft up instead of driving it into the ground).
        /// </summary>
        void ApplyGrip(Pose p)
        {
            if (!GripHeld)
            {
                // no handle to hold, or a family that holds its weapon(s) one-handed; a dual wielder's hands split and
                // each holds a blade (the near blade is posed with the pose)
                bool dual = Dual;
                if (dual) p.SplitHands(DualHandOffset);
                else if (Family != null && Family.grip == GripPose.OneHand && !BareHanded) p.SplitHands(OneHandOffset);     // the near hand holds the chain's loose end
                p.grip = false;
                p.dual = dual;
                return;
            }
            bool polearm = Family != null && Family.grip == GripPose.Polearm;
            bool heavy = Family != null && Family.grip == GripPose.HeavyTwoHand;
            float spacing = polearm ? Pose.PolearmSpacing : heavy ? Pose.HeavySpacing : Pose.GripSpacing;
            float pommel = polearm ? Pose.PolearmPommel : heavy ? Pose.HeavyPommel : Pose.GripPommel;
            if (p.gripSpacing != spacing || p.gripPommel != pommel) { p.gripSpacing = spacing; p.gripPommel = pommel; p.MarkDirty(); }
            if ((polearm || (Family != null && Family.keepPointOffFloor)) && p.ik[(int)Limb.ArmF])
            {
                const float floor = 0.05f;
                p.Resolve(def);       // bakes the arm angles; the shaft's drawn direction is their sum
                float len = BladeReach + 0.08f;
                float y0 = p.target[(int)Limb.ArmF].y;
                float deg = p.a[(int)B.Hips] + p.a[(int)B.Chest] + p.a[(int)B.ArmFU] + p.a[(int)B.ArmFL] + p.a[(int)B.HandF] + p.a[(int)B.Sword] + def.bladeBindAngle;
                float ang = deg * Mathf.Deg2Rad;
                if (y0 + len * Mathf.Sin(ang) < floor)
                {
                    float asin = Mathf.Asin(Mathf.Clamp((floor - y0) / len, -1f, 1f));
                    p.hasSwAbs = true;
                    p.swAbs = KMath.Wrap((Mathf.Cos(ang) >= 0f ? asin : Mathf.PI - asin) * Mathf.Rad2Deg);
                    p.MarkDirty();
                }
            }
        }
    }
}
