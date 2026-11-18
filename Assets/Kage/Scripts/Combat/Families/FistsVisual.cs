namespace Kage
{
    /// <summary>Bare hands: the part set's sword is hidden. Garan's gauntlets (iron_fists) are part of his own part set, not of the family.</summary>
    public sealed class FistsVisual : IWeaponVisual
    {
        public void Attach(FighterRig rig, string weaponId) => rig.SetSwordVisible(false);
        public void Detach(FighterRig rig) => rig.SetSwordVisible(true);
    }
}
