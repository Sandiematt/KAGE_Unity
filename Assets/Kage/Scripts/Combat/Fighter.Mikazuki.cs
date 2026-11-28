using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Mikazuki, 三日月 (the plain starter Mumei's rage art, the basic one): no cut-in cinematic and no freeze. Kage holds the blade in hassō
    /// while the dark gathers, cuts once, and the cut leaves as one giant crescent moon that slides down the floor through every foe in its path
    /// (<see cref="KatanaMoves.mikazuki"/>). It is an ordinary move with a projectile: the wave is unblockable and can't be parried, and the
    /// fighter is untouchable until the cut is done.
    /// </summary>
    public partial class Fighter
    {
        public const string Mikazuki = "mikazuki";

        /// <summary>The giant crescent: the Crimson Crescent at twice the size and three times the damage, passing through up to three foes.</summary>
        public static readonly ProjectileDef MikazukiWave = new()
        {
            id = Mikazuki,
            hit = new HitWindow { dmg = 30, level = HitLevel.Unblockable, kb = new Vector2(4.2f, 3.4f), stun = 34, hitstop = 14, shake = 0.5f, heavy = true, knockdown = true, radius = 0.6f, sword = true },
            speed = 10f,
            radius = 0.6f,
            lifeFrames = 84,
            maxHits = 3,
            samples = new[] { new Vector2(0.55f, -0.62f), new Vector2(0.65f, 0f), new Vector2(0.55f, 0.62f) },    // its leading arc
            flightCue = CrescentCues.MoonFlight, hitCue = CrescentCues.MoonHit, endCue = CrescentCues.Fade,
        };

        /// <summary>Starts Mikazuki: the Rage is spent and the move plays. False when this fighter's set has no Mikazuki (it falls back to the generic art).</summary>
        bool StartMikazuki()
        {
            var mv = (moves as KatanaMoves)?.mikazuki;
            if (mv == null) return false;
            meter = 0f;
            _artId = Mikazuki;
            StartMove(mv);
            ctx?.Events.Publish(CombatEvent.RageArt(ctx.Clock.Frame, this, Mikazuki));
            var look = RageArtLook.Moon;
            ArtHud?.CutIn(look.name.ToUpperInvariant(), look.kanji, RageArtCues.FormatCutInCaption(look, WeaponId), look.col, look.hot, 0.6f, look.legendary, look.deep);
            GameFeel.Punch(0.1f);
            KageGame.I?.Audio?.Play(SfxKind.Ultimate, 1f);
            return true;
        }
    }
}
