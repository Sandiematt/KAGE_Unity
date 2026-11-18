using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kage
{
    /// <summary>Combat event types (ADR-0003). New types go at the end so earlier values never change.</summary>
    public enum CombatEventType
    {
        FightStarted,     // Index = seed
        MoveStarted,      // Source started MoveId
        Hit,              // Source's MoveId hit Target
        Blocked,          // Target guarded Source's MoveId
        Countered,        // Target's counter window caught Source's MoveId (Mirror Guard, Censure, Redline Counter)
        Parried,          // Target parried Source's MoveId (SAN-91)
        Clashed,          // Source's and Target's blades met (SAN-93)
        GuardBroken,      // Target's posture filled (SAN-86)
        PerfectDodged,    // Target perfect-dodged Source's MoveId (SAN-92)
        KnockedOut,       // Target was knocked out; Source landed the last hit
        RageArt,          // Source started a rage art: MoveId = "crimsonMoon" | "thousandShadows"
        BossPhaseChanged, // Target entered phase Index (Name = JS phase name) (SAN-102)
        WaveStarted,      // Index = wave number in the stretch, Name = encounter id (SAN-89)
        WaveCleared,      // same fields (SAN-89)
        StageCleared,     // Name = stage id
        StageFailed       // Name = stage id
    }

    /// <summary>
    /// One thing that happened in a fight. A flat read-only struct, so a log is easy to filter,
    /// store and compare, and publishing allocates nothing. Build it with the factory methods.
    /// </summary>
    public readonly struct CombatEvent
    {
        public readonly int Frame;               // ctx.Clock.Frame when it happened
        public readonly CombatEventType Type;
        public readonly Fighter Source;          // the actor (attacker); null for stage events
        public readonly Fighter Target;          // the one acted on; null for props and stage events
        public readonly string SourceId, TargetId; // FighterId or prop id ("bag")
        public readonly string MoveId;           // move, projectile, hazard, ability or rage-art id
        public readonly int Window;              // index of the hit window in the move; -1 when not a window
        public readonly float Damage;            // health removed from Target after every multiplier, crit and defence
        public readonly float Posture;           // posture added: to Target (Hit, Blocked); the filled bar (GuardBroken) (SAN-86)
        public readonly bool Crit;
        public readonly HitLevel Level;
        public readonly HitZone Zone;
        public readonly Vector2 Contact;
        public readonly Vector2 Knockback;       // world velocity or impulse given to Target
        public readonly int Index;               // seed, phase index, wave number; 0 when unused
        public readonly string Name;             // phase name, encounter id, stage id; null when unused

        CombatEvent(int frame, CombatEventType type, Fighter source, Fighter target, string sourceId, string targetId,
            string moveId, int window = -1, float damage = 0f, float posture = 0f, bool crit = false,
            HitLevel level = HitLevel.Mid, HitZone zone = HitZone.Body, Vector2 contact = default, Vector2 knockback = default,
            int index = 0, string name = null)
        {
            Frame = frame; Type = type; Source = source; Target = target;
            SourceId = sourceId ?? (source != null ? source.FighterId : null);
            TargetId = targetId ?? (target != null ? target.FighterId : null);
            MoveId = moveId; Window = window; Damage = damage; Posture = posture; Crit = crit;
            Level = level; Zone = zone; Contact = contact; Knockback = knockback; Index = index; Name = name;
        }

        public static CombatEvent FightStarted(int frame, int seed) =>
            new(frame, CombatEventType.FightStarted, null, null, null, null, null, index: seed);

        public static CombatEvent MoveStarted(int frame, Fighter source, string moveId) =>
            new(frame, CombatEventType.MoveStarted, source, null, null, null, moveId);

        /// <summary>
        /// A hit result: <paramref name="type"/> is Hit, Blocked or Countered (later Parried, PerfectDodged).
        /// <paramref name="posture"/> is the posture the target took (SAN-86): damage x factor on Blocked, a little on Hit.
        /// </summary>
        public static CombatEvent HitResolved(int frame, CombatEventType type, Fighter source, Fighter target, string targetId,
            string moveId, int window, float damage, bool crit, HitLevel level, HitZone zone, Vector2 contact, Vector2 knockback, float posture = 0f) =>
            new(frame, type, source, target, null, targetId, moveId, window, damage, posture, crit, level, zone, contact, knockback);

        /// <summary>
        /// Target's posture filled and its guard broke (SAN-86). Source is the fighter whose hit (or parry, or clash) filled it;
        /// MoveId and Window are that hit's; Posture is the bar that filled (the target's posture maximum).
        /// </summary>
        public static CombatEvent GuardBroken(int frame, Fighter source, Fighter target, string moveId, int window, float postureMax, Vector2 contact) =>
            new(frame, CombatEventType.GuardBroken, source, target, null, null, moveId, window, posture: postureMax, contact: contact);

        public static CombatEvent KnockedOut(int frame, Fighter source, Fighter target, string moveId, Vector2 contact, Vector2 impulse) =>
            new(frame, CombatEventType.KnockedOut, source, target, null, null, moveId, contact: contact, knockback: impulse);

        public static CombatEvent RageArt(int frame, Fighter source, string moveId) =>
            new(frame, CombatEventType.RageArt, source, null, null, null, moveId);

        /// <summary>
        /// The canonical line: the baseline replay format and every "identical event log" check.
        /// <c>{Frame} {Type} {SourceId}&gt;{TargetId} {MoveId}#{Window} dmg={Damage:0.###} pos={Posture:0.###} crit={0|1} lvl={Level} zone={Zone} i={Index} n={Name}</c>
        /// Missing ids and names are written as "-". Numbers use the invariant culture.
        /// </summary>
        public string ToLine()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, "{0} {1} {2}>{3} {4}#{5} dmg={6} pos={7} crit={8} lvl={9} zone={10} i={11} n={12}",
                Frame, Type, SourceId ?? "-", TargetId ?? "-", MoveId ?? "-", Window,
                Damage.ToString("0.###", ci), Posture.ToString("0.###", ci), Crit ? 1 : 0, Level, Zone, Index, Name ?? "-");
        }

        public override string ToString() => ToLine();
    }

    /// <summary>
    /// The fight's event stream (ctx.Events). Dispatch is synchronous, in publish order, on the fight
    /// loop. Listeners only read: they never change fight state inside the callback.
    /// </summary>
    public sealed class CombatEvents
    {
        public event Action<CombatEvent> Published;
        public void Publish(in CombatEvent e) => Published?.Invoke(e);
    }

    /// <summary>Records a fight's events, for tests and debugging.</summary>
    public sealed class CombatEventLog : IDisposable
    {
        readonly CombatEvents _source;
        readonly List<CombatEvent> _all = new();

        public CombatEventLog(CombatEvents source)
        {
            _source = source;
            _source.Published += Add;
        }

        public IReadOnlyList<CombatEvent> All => _all;

        public int Count(CombatEventType t)
        {
            int n = 0;
            foreach (var e in _all) if (e.Type == t) n++;
            return n;
        }

        /// <summary>Canonical text, one event per line (<see cref="CombatEvent.ToLine"/>).</summary>
        public IEnumerable<string> Lines()
        {
            foreach (var e in _all) yield return e.ToLine();
        }

        public void Dispose() => _source.Published -= Add;

        void Add(CombatEvent e) => _all.Add(e);
    }
}
