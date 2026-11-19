using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A fighter's side of a weapon clash (SAN-93, FightLoop.Clash.cs): the recoil. The move is over, the fighter is helpless in
    /// hitstun for <see cref="ClashTuning.recoilFrames"/> and thrown back from the other blade. Armour doesn't stop it (armour
    /// is for hits; a clash isn't one) and neither does the stun-resist perk, so both fighters always recoil alike.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>The move is over and the fighter staggers back from <paramref name="other"/> (the fighter it clashed with).</summary>
        public void ClashRecoil(Fighter other, ClashTuning t)
        {
            if (state == FState.KO) return;
            if (_victim != null) ReleaseVictim(false);
            float away = other != null ? Mathf.Sign(Pos.x - other.Pos.x) : 0f;
            if (away == 0f) away = -facing;
            SetState(FState.Hitstun);
            _hurt = _hurtBody;
            _stun = t.recoilFrames;
            Knock(KnockVelocity(new Vector2(away * t.recoilSpeed, 0f)));
            rig.Squash(0.06f);
        }
    }
}
