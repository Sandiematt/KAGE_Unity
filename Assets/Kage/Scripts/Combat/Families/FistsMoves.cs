using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// The fists family (ADR-0006): bare hands, with the katana set's existing fist and kick techniques shared by
    /// reference. Punch and Kick are the katana set's; the second punch is the far-fist cross (as with a broken sword);
    /// Weapon throws a punch technique (Rising Fist, Stepping Punch forward, Low Jab down). No plain Special yet: the
    /// full fists set (Garan's iron fists) is SAN-80.
    /// </summary>
    public sealed class FistsMoves : MoveSet
    {
        readonly KatanaMoves _k;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Fists,
            referenceWeapon = "fists",
            build = sets => new FistsMoves((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.Fists,
            bladeLength = 0f,
            rageArt = Fighter.ThousandShadows,
            visual = new FistsVisual(),
        };

        FistsMoves(KatanaMoves katana)
        {
            _k = katana;
            ShareBodyMoves(katana);
        }

        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P or Btn.S => _k.airPunch, Btn.K => _k.airKick, _ => null };
            switch (b)
            {
                case Btn.P:
                case Btn.K:
                    return _k.Select(b, fwd, dy, false, self);
                case Btn.S:
                    if (dy < 0) return _k.crouchPunch;
                    if (fwd > 0) return _k.lunge;
                    return _k.uppercut;
            }
            return null;
        }

        public override MoveDef BareHanded(MoveDef m) => _k.BareHanded(m);

        public override IEnumerable<MoveDef> All => new[]
        {
            _k.jab, _k.crossFist, _k.uppercut, _k.crouchPunch, _k.lunge, _k.kick1, _k.kick2, _k.axeKick, _k.sweep, _k.pushKick,
            throwGrab, throwWhiff, _k.airKick, _k.airPunch, roll, backflip, _k.groundPound, getUp, getUpFront,
        };
    }
}
