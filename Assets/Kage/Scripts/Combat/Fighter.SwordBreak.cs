using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Sword break (SAN-113): a guarded hit from a <see cref="HitWindow.breaksWeapon"/> window shatters the
    /// katana. The fighter is then unarmed for the rest of the fight: sword moves do nothing, the second punch
    /// is the far fist (<c>crossFist</c>) and both fists guard. <see cref="ResetForFight"/> restores it.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>True from a sword break until the next fight.</summary>
        public bool SwordBroken { get; private set; }

        /// <summary>Shatters the katana (a guarded breaksWeapon hit, or the dev hotkey).</summary>
        public void BreakSword()
        {
            if (SwordBroken) return;
            SwordBroken = true;
            Vector2 mid = rig.World(B.Sword, Vector2.Lerp(def.bladeBaseLocal, def.bladeTipLocal, 0.4f));
            Vfx.I?.BlockBurst(mid, new Vector2(-facing, 0.3f));
            Vfx.I?.Glint(mid, Color.white);
            KageGame.I?.Audio?.Play(SfxKind.Block, 1f);
            rig.SetSwordVisible(false);
            rig.SetSwordFront(false);
        }

        public void RestoreSword()
        {
            SwordBroken = false;
            rig.SetSwordVisible(true);
        }

        /// <summary>The start of a fight (not a round): the full kunai count and the sword back.</summary>
        public void ResetForFight()
        {
            ResetKunai();
            RestoreSword();
        }

        /// <summary>The move to play for a selected one: with the sword broken the Pommel Strike becomes the far-fist cross, and sword moves are out.</summary>
        MoveDef Armed(MoveDef m)
        {
            if (m == null || !SwordBroken) return m;
            if (m == moves.cross) return moves.crossFist;
            if (m.swordFront || (m.projectileFrame >= 0 && !IsKunaiThrow(m))) return null;
            foreach (var w in m.hits) if (w.sword) return null;
            return m;
        }

        readonly Pose _unarmed = new();

        /// <summary>With the sword broken a held stance shows both fists up instead of the two-handed grip.</summary>
        Pose Unarmed(Pose p)
        {
            if (!SwordBroken) return p;
            _unarmed.CopyFrom(p);
            if (p.hip.y < -0.2f) _unarmed.HandN(0.26f, 1.06f, 10f).HandF(0.18f, 0.96f, 10f);
            else FistGuard(_unarmed);
            _unarmed.MarkDirty();
            return _unarmed;
        }
    }
}
