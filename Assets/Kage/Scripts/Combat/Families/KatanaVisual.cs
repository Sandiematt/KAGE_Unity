namespace Kage
{
    /// <summary>The katana is drawn by the character's part set (Kage's katana on the Sword bone): attaching just shows it.</summary>
    public sealed class KatanaVisual : IWeaponVisual
    {
        public void Attach(FighterRig rig, string weaponId) => rig.SetSwordVisible(true);
        public void Detach(FighterRig rig) { }
    }
}
