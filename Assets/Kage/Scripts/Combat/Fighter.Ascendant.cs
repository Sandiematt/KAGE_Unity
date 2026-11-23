using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant's hooks (Raikon's three-phase fight, docs/raikon-ascendant-vfx.md). Each one is off unless a move, an ability or a
    /// phase asks for it, so every other fighter plays as before:
    /// <list type="bullet">
    /// <item><b>Extra hazards and backward throws</b> (<see cref="MoveDef.extraHazards"/>, <see cref="MoveDef.ThrowBack"/>): Heavens' Verdict's three bolts,
    /// Blink Flurry's delayed slashes, Final Overload's twin waves.</item>
    /// <item><b>The pull</b> (<see cref="MoveDef.pullFrom"/>): the target is drawn along the floor towards the fighter. It is a nudge of its
    /// position each frame, so walking or dashing away still beats it.</item>
    /// <item><b>A forced move</b> (<see cref="QueueForcedMove"/>): a move that starts on the fighter's next frame from any state that can act,
    /// hit-stun included (a boss phase's entry move, an escape). It waits while the fighter is in the air, launched, down or thrown.</item>
    /// <item><b>An escape</b> (<see cref="AbilityDef.escapeAfterHits"/>): an ability the fighter itself starts once it has taken that many blows in a row
    /// (<see cref="HitsTakenInARow"/>), out of the hit-stun or through its armour (Afterimage Feint).</item>
    /// <item><b>Light-attack tracking</b> (<see cref="LightAttacksWithin"/>): how many light attacks this fighter started lately, the trigger of Iron
    /// Guard Counter.</item>
    /// </list>
    /// </summary>
    public partial class Fighter
    {
        MoveDef _forced;
        readonly int[] _lightStarts = new int[8];      // clock frames of the last light attacks this fighter started (a ring)
        int _lightNext;

        /// <summary>The move <see cref="QueueForcedMove"/> is holding for the next frame it can start; null when none.</summary>
        public MoveDef ForcedMove => _forced;

        /// <summary>Starts <paramref name="m"/> on the fighter's next frame that it can act, whatever it is doing then (see the class notes).</summary>
        public void QueueForcedMove(MoveDef m) => _forced = m;

        /// <summary>Drops a queued forced move (a new round).</summary>
        public void ClearForcedMove() => _forced = null;

        /// <summary>A new round: no forced move, no run of blows taken, no light attacks remembered.</summary>
        void ResetAscendantHooks()
        {
            _forced = null;
            HitsTakenInARow = 0;
            _lastTakenFrame = -9999;
            System.Array.Clear(_lightStarts, 0, _lightStarts.Length);
            _lightNext = 0;
        }

        /// <summary>At the top of a frame: starts the queued forced move when the fighter can take it. True when it started.</summary>
        bool TryForcedMove()
        {
            if (_forced == null) return false;
            switch (state)
            {
                case FState.KO: case FState.Thrown: case FState.Launched: case FState.Down: case FState.Ultimate: case FState.Intro: case FState.Victory: case FState.Air:
                    return false;
            }
            if (_victim != null) ReleaseVictim(false);
            var m = _forced;
            _forced = null;
            _stun = 0;
            StartMove(m);
            return true;
        }

        // ---------------------------------------------------------------- hazards, throws, the pull
        void SpawnMoveHazard(HazardDef def, float ahead, bool onTarget)
        {
            if (ctx == null || def == null) return;
            float x = Mathf.Clamp(Pos.x + facing * ahead, StageMin, StageMax);
            if (onTarget && opponent != null) x = Mathf.Clamp(opponent.Pos.x, StageMin, StageMax);
            Hazard.Spawn(ctx, this, def, new Vector2(x, Pos.y));
        }

        void SpawnMoveProjectile(ProjectileDef d, Vector2 from, bool back)
        {
            if (!back) { SpawnMoveProjectile(d, from); return; }
            if (ctx == null) return;
            float speed = d.speedMax > d.speed ? ctx.Random.Stream("throw/" + FighterId).Range(d.speed, d.speedMax) : d.speed;
            var at = new Vector2(Pos.x - facing * from.x, Pos.y + from.y);
            Projectile.Spawn(ctx, this, d, at, -facing, speed);
        }

        static readonly HitWindow PullProbe = new() { level = HitLevel.Unblockable };

        /// <summary>The nearest a pulled target is drawn to the fighter, metres.</summary>
        public const float PullStopDistance = 0.9f;

        /// <summary>One frame of the pull: the target slides <paramref name="speed"/> m/s towards the fighter, inside the stage.</summary>
        void PullTarget(float speed)
        {
            var t = opponent;
            if (t == null || t.rb == null || speed <= 0f) return;
            if (t.state is FState.Down or FState.Thrown or FState.KO or FState.Ultimate or FState.Intro or FState.Victory || !t.HittableBy(PullProbe)) return;
            float dx = Pos.x - t.Pos.x;
            if (Mathf.Abs(dx) <= PullStopDistance) return;
            float step = Mathf.Min(speed * Time.fixedDeltaTime, Mathf.Abs(dx) - PullStopDistance);
            float x = Mathf.Clamp(t.rb.position.x + Mathf.Sign(dx) * step, StageMin, StageMax);
            t.rb.position = new Vector2(x, t.rb.position.y);
        }

        // ---------------------------------------------------------------- the escape
        /// <summary>
        /// Blows that landed on this fighter in a row, each within <see cref="ComboGapFrames"/> of the last (guarded, parried and countered blows don't
        /// count). Counted on the victim, not read from the attacker's combo counter: armour (Raikon's) takes light blows without a flinch, and the
        /// attacker's counter is reset whenever its victim is not in hit-stun.
        /// </summary>
        public int HitsTakenInARow { get; private set; }

        /// <summary>The longest gap between two blows of one run, in clock frames (0.75 s).</summary>
        public const int ComboGapFrames = 45;
        int _lastTakenFrame = -9999;

        void NoteHitTaken()
        {
            int now = ctx != null ? ctx.Clock.Frame : _lastTakenFrame + 1;
            HitsTakenInARow = now - _lastTakenFrame <= ComboGapFrames ? HitsTakenInARow + 1 : 1;
            _lastTakenFrame = now;
        }

        /// <summary>
        /// After a blow landed (in hit-stun, or through armour): an enabled, ready ability with <see cref="AbilityDef.escapeAfterHits"/> is queued as a
        /// forced move once <see cref="HitsTakenInARow"/> reaches that many. A player-driven fighter never escapes on its own; a launched or downed one waits.
        /// </summary>
        void TryEscapeAbility()
        {
            if (isPlayer || _forced != null || state is FState.Launched or FState.Down or FState.Thrown or FState.KO) return;
            var ids = AbilityIds;
            for (int i = 0; i < ids.Count; i++)
            {
                var a = AbilityCatalog.Get(ids[i]);
                if (a == null || a.escapeAfterHits <= 0 || HitsTakenInARow < a.escapeAfterHits || !AbilityReady(i + 1)) continue;
                var m = Armed(AbilityMove(ids[i]));
                if (m == null) continue;
                if (a.cooldownFrames > 0 && ctx != null) _abilityReady[a.id] = ctx.Clock.Frame + Mathf.RoundToInt(a.cooldownFrames * AbilityCooldownMul);
                _forced = m;
                HitsTakenInARow = 0;
                return;
            }
        }

        // ---------------------------------------------------------------- light attacks
        /// <summary>A light attack: a move with hit windows, none of them heavy or unblockable, and not a throw.</summary>
        public static bool IsLightAttack(MoveDef m)
        {
            if (m == null || m.isThrow || m.hits.Count == 0) return false;
            foreach (var w in m.hits) if (w.heavy || w.level == HitLevel.Unblockable) return false;
            return true;
        }

        void NoteMoveStart(MoveDef m)
        {
            if (ctx == null || !IsLightAttack(m)) return;
            _lightStarts[_lightNext] = ctx.Clock.Frame + 1;        // +1 so an empty slot (0) never counts
            _lightNext = (_lightNext + 1) % _lightStarts.Length;
        }

        /// <summary>How many light attacks this fighter started in the last <paramref name="frames"/> clock frames.</summary>
        public int LightAttacksWithin(int frames)
        {
            if (ctx == null) return 0;
            int now = ctx.Clock.Frame + 1, n = 0;
            foreach (var at in _lightStarts) if (at > 0 && now - at <= frames) n++;
            return n;
        }
    }
}
