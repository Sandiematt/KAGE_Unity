using System;

namespace Kage
{
    /// <summary>The trail a weapon's footwork leaves on a dash (the JS <c>WeaponFootwork</c>): its element, never a recolour.</summary>
    public enum Footwork { None, Crimson, Lightning, Violet, Flame, Water, Shadow, Earth }

    /// <summary>
    /// A weapon's attack pattern (docs/weapon-styles.md): the small set of rules that make one weapon fight unlike another on
    /// top of its family's move set (ADR-0006). The family owns the moves, the grip and the drawing; the style owns the feel:
    /// how fast the feet are, how the tempo changes through a string, which traits its blows carry, which buttons it plays
    /// differently, and its rage art. One <see cref="WeaponStyle"/> per player weapon, registered in <see cref="WeaponStyles"/>;
    /// a weapon without one (every foe weapon) fights exactly as its family does.
    /// <para>
    /// Every field is neutral at its default (a style that sets nothing changes nothing), so a trait is read only where a
    /// weapon asks for it. The traits are the "pattern" part: each weapon gets one or two, chosen so that no two weapons win
    /// the same way.
    /// </para>
    /// </summary>
    public sealed class WeaponStyle
    {
        /// <summary>The JS weapon id this style is for.</summary>
        public string weaponId;
        /// <summary>The style's name, shown in the training move list and the docs ("Iaijutsu", "Twin Storm", ...).</summary>
        public string name;
        /// <summary>One line on how the weapon wins.</summary>
        public string summary;

        // ---------------- footwork ----------------
        /// <summary>Walk and run speed (× the character's speed). Heavy weapons walk slower, light ones faster.</summary>
        public float moveMul = 1f;
        /// <summary>Dash, back-dash and air-dash speed (× the character's speed and <see cref="moveMul"/>).</summary>
        public float dashMul = 1f;
        /// <summary>The trail a dash leaves.</summary>
        public Footwork footwork = Footwork.None;
        /// <summary>Every running footfall and landing thuds: dust and a small tremor (the heavy weapons).</summary>
        public bool heavyStep;

        // ---------------- tempo ----------------
        /// <summary>
        /// Momentum (the twin blades): each landed blow adds this much playback rate to the next attacks (0.06 = +6 %), up to
        /// <see cref="tempoMax"/>; it drains away after <see cref="tempoHoldFrames"/> without a landed blow. 0 = no momentum.
        /// </summary>
        public float tempoPerHit;
        public float tempoMax;
        public int tempoHoldFrames = 50;
        /// <summary>A landed blade blow lets a dash cancel the rest of the move from the frame after the hit (the twin blades' flow).</summary>
        public bool dashCancelOnHit;

        // ---------------- traits on the blows ----------------
        /// <summary>Damage × this on every blow (the style's weight).</summary>
        public float damageMul = 1f;
        /// <summary>Posture × this on every blow, guarded or not (on top of the window's own posture multiplier).</summary>
        public float postureMul = 1f;
        /// <summary>
        /// Armoured swings (the cleaver): every Weapon-button swing holds through light blows from its first frame to the end of its
        /// last hit window. The cleaver's own armour frames still apply as data.
        /// </summary>
        public bool armouredSwings;
        /// <summary>
        /// Guard crush (the cleaver): a guarded blade blow chips this fraction of its damage through the guard (on top of the
        /// guard's own chip) and pushes the guard back harder.
        /// </summary>
        public float guardCrushChip;
        public float guardCrushPush = 1f;
        /// <summary>
        /// The sweet spot (the spear): a blade blow that lands in the outer <see cref="tipZone"/> of the weapon's reach deals
        /// <see cref="tipDamage"/> × and pushes <see cref="tipKnockback"/> × harder; one that lands in the inner
        /// <see cref="innerZone"/> (too close for a polearm) deals <see cref="innerDamage"/> ×. 0 = no sweet spot.
        /// </summary>
        public float tipZone, tipDamage = 1f, tipKnockback = 1f, innerZone, innerDamage = 1f;
        /// <summary>
        /// The hook mark (the chain sickle): a landed blade blow marks the foe for <see cref="hookFrames"/>; the next blade blow on a
        /// marked foe spends the mark for <see cref="hookDamage"/> × damage and drags it <see cref="hookPull"/> m/s towards the
        /// wielder. 0 = no marks.
        /// </summary>
        public int hookFrames;
        public float hookDamage = 1f, hookPull;
        /// <summary>
        /// The riposte (Kagetsu): for <see cref="riposteFrames"/> after this fighter blocks or parries, a Weapon press plays
        /// <see cref="riposte"/> instead of the move it would pick. 0 = no riposte.
        /// </summary>
        public int riposteFrames;
        public MoveDef riposte;
        /// <summary>
        /// The silent cut (Shizuka, the JS <c>echo</c>): a move listed here that lands leaves a cut that opens on the foe
        /// <see cref="echoDelay"/> frames after the move's sheath frame (<see cref="EchoMove.sheathFrame"/>), for
        /// <see cref="EchoMove.damage"/>, unblockable.
        /// </summary>
        public EchoMove[] echoes;
        public int echoDelay = 18;
        /// <summary>
        /// The tremor (the hammer; the JS Earthshatter rule): on each listed move's impact frame the floor shakes, and every grounded foe
        /// within <see cref="quakeRadius"/> metres of the impact is stunned for <see cref="quakeStun"/> frames, guarding or not. A jump
        /// clears it. Null = no tremor.
        /// </summary>
        public QuakeMove[] quakes;
        public float quakeRadius = 2.4f;
        public int quakeStun = 22;

        /// <summary>A legendary weapon (the rainbow tier): its landed blows and dashes shed prismatic sparks, its art's name and word drift in a rainbow.</summary>
        public bool legendary;

        // ---------------- buttons and the rage art ----------------
        /// <summary>
        /// Builds a move set that plays some buttons differently (Shizuka's iai string over the katana set); null = the family's
        /// set. Called once per fight, like a family's build, with the family's own set to wrap.
        /// </summary>
        public Func<MoveSet, MoveSet> buildMoves;
        /// <summary>The rage art this weapon plays (Special + Down with full Rage); null = the family's.</summary>
        public string rageArt;

        /// <summary>True when the style changes anything a fight can see: a style that sets nothing is the family exactly.</summary>
        public bool HasMomentum => tempoPerHit > 0f && tempoMax > 0f;
        public bool HasSweetSpot => tipZone > 0f || innerZone > 0f;
        public bool HasHook => hookFrames > 0;
        public bool HasRiposte => riposteFrames > 0 && riposte != null;
        public bool HasQuake => quakes != null && quakes.Length > 0;
    }

    /// <summary>A move that shakes the floor (the hammer's tremor), the frame its head meets the floor and how far ahead of the feet.</summary>
    public struct QuakeMove
    {
        public string moveId;
        public int frame;
        public float ahead;
    }

    /// <summary>A move whose landed blows leave Shizuka's silent cut, and the frame its blade clicks home (the cut opens after it).</summary>
    public struct EchoMove
    {
        public string moveId;
        public int sheathFrame;
        public int damage;
    }
}
