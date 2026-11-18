namespace Kage
{
    /// <summary>Bare hands: the part set's sword is hidden. (Garan's iron fists get a drawing in SAN-80.)</summary>
    public sealed class FistsVisual : IWeaponVisual
    {
        public void Attach(FighterRig rig, string weaponId) => rig.SetSwordVisible(false);
        public void Detach(FighterRig rig) => rig.SetSwordVisible(true);
    }
}
