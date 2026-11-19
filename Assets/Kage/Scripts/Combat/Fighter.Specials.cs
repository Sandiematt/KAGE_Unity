using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Mirror Guard counter, Crescent spawn and the Thousand Shadows ultimate.</summary>
    public partial class Fighter
    {
        // =====================================================================================
        //  COUNTER + PROJECTILE + ULTIMATE
        // =====================================================================================
        void TriggerCounter(Fighter attacker)
        {
            if (attacker == null) return;
            Vfx.I?.Glint(rig.World(B.Sword, def.bladeTipLocal * 0.5f), Color.white);
            Vfx.I?.Ghost(rig, fxMain, 0.35f);
            GameFeel.Hitstop(10);
            GameFeel.Flash(new Color(1f, 1f, 1f, 0.5f), 0.12f);
            float side = -attacker.facing;
            float x = Mathf.Clamp(attacker.Pos.x + side * 0.75f, -StageHalf, StageHalf);
            rb.position = new Vector2(x, 0.02f);
            transform.position = new Vector3(x, 0.02f, 0f);
            SetFacing(Mathf.Sign(attacker.Pos.x - x), true);
            attacker.Stagger(24);
            StartMove(moves.counterSlash);
            KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.2f, "COUNTER!", fxMain, 1.1f);
            KageGame.I?.Audio?.Play(SfxKind.Counter, 1f);
        }

        /// <summary>The Crimson Crescent as data for the generic projectile (SAN-75): the numbers it always had.</summary>
        public static readonly ProjectileDef CrescentProjectile = new()
        {
            id = "crescent",
            hit = new HitWindow { dmg = 10, level = HitLevel.Mid, kb = new Vector2(2.6f, 1.2f), stun = 22, hitstop = 7, shake = 0.22f, heavy = true, radius = 0.32f, sword = true },
            speed = 8.2f,
            radius = 0.32f,
            lifeFrames = 69,                 // 1.15 s
            maxHits = 1,
            samples = new[] { new Vector2(0.3f, -0.32f), new Vector2(0.3f, 0f), new Vector2(0.3f, 0.32f) },    // its leading arc
            flightCue = CrescentCues.Flight, hitCue = CrescentCues.Hit, endCue = CrescentCues.Fade,
        };

        void SpawnCrescent()
        {
            if (ctx == null) return;
            Vector2 p = rig.World(B.Sword, def.bladeTipLocal * 0.55f);
            p.y = Mathf.Max(p.y, 1.0f);
            Projectile.Spawn(ctx, this, CrescentProjectile, new Vector2(p.x, 1.05f), facing);
            GameFeel.Shake(0.1f);
            KageGame.I?.Audio?.Play(SfxKind.Crescent, 0.9f);
        }

        /// <summary>
        /// Special + Down (ADR-0004): the rage art when Rage is full, else a kunai if any are left, else nothing.
        /// Returns true when the press was used up (including the "nothing" case); false when it is not for this
        /// method (in the air), so the caller falls through to the move table.
        /// </summary>
        bool TrySpecialDown()
        {
            if (!Grounded) return false;
            ConsumeBuffer();
            if (Has(CharacterFlags.CanRageArt) && meter >= 100f) { StartRageArt(); return true; }
            if (StartKunaiThrow()) return true;
            if (Has(CharacterFlags.CanThrowKunai)) KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.1f, "NO KUNAI", new Color(0.7f, 0.7f, 0.7f), 0.7f);
            return true;
        }

        public const string ThousandShadows = "thousandShadows";
        static readonly HitWindow UltCut = new() { dmg = 4, level = HitLevel.Unblockable, sword = true };
        int _ultFrame;
        Vector2 _ultAnchor;
        static readonly float[] UltSides = { 1f, -1f, 1f, -1f, 1f };
        static readonly float[] UltHeights = { 0f, 0.55f, 0f, 0.8f, 0.2f };

        /// <summary>
        /// Starts Thousand Shadows (every family but the katana's, SAN-82), or, for a katana-family fighter with nobody to cut, spends the
        /// press the same way and announces <paramref name="art"/>: the generic start of the rage art as it always was.
        /// </summary>
        void StartUltimate(string art = ThousandShadows)
        {
            meter = 0f;
            SetState(FState.Ultimate);
            _artId = art;
            _cmActive = false;
            _ultFrame = 0;
            rb.linearVelocity = Vector2.zero;
            GameFeel.SlowMo(0.5f, 0.35f);
            GameFeel.Punch(0.18f);
            ctx?.Events.Publish(CombatEvent.RageArt(ctx.Clock.Frame, this, art));
            KageGame.I?.Hud?.Banner(art == CrimsonMoon ? "CRIMSON MOON" : "THOUSAND SHADOWS", fxMain, 1.4f);
            KageGame.I?.Audio?.Play(SfxKind.Ultimate, 1f);
        }

        void UltimateUpdate()
        {
            if (_cmActive) { CrimsonMoonUpdate(); return; }
            _ultFrame++;
            int f = _ultFrame;
            if (opponent == null) { SetState(FState.Idle); return; }
            if (f <= 12)
            {
                Vfx.I?.ChargeDark(rig.World(B.Chest, Vector2.zero), fxMain, fxDark);
                if (f == 12)
                {
                    Vfx.I?.InkBurst(rig.World(B.Chest, Vector2.zero), fxDark, 1.4f);
                    _hidden = true;
                    rig.SetVisible(false);
                    rb.simulated = false;
                    opponent.Stagger(90);
                    _ultAnchor = opponent.Pos;
                }
                return;
            }
            int slash = (f - 16) / 9;
            int sf = (f - 16) % 9;
            if (f >= 16 && slash < 5 && sf == 0)
            {
                float side = UltSides[slash] * (opponent.facing);
                Vector2 p = _ultAnchor + new Vector2(side * 0.95f, UltHeights[slash]);
                Vector2 q = _ultAnchor + new Vector2(-side * 0.95f, 1.6f - UltHeights[slash]);
                Vfx.I?.SlashLine(p + Vector2.up * 0.6f, q - Vector2.up * 0.3f, fxMain, fxCore);
                Vfx.I?.HitBurst(opponent.Pos + Vector2.up * 1.0f, (q - p).normalized, 1.0f, true, fxMain, fxCore, fxDark);
                float before = opponent.health;
                int cut = DamageCalc.For(this, opponent, UltCut.dmg, false, true);
                opponent.health = Mathf.Max(1f, opponent.health - cut);
                opponent.LastDamageTaken = before - opponent.health;
                // a cut publishes a Hit like any window; the onHit callback never saw cuts, so it still doesn't
                PublishHit(opponent, new HitInfo
                {
                    attacker = this, w = UltCut, dmg = cut, zone = HitZone.Body, contact = opponent.Pos + Vector2.up * 1.0f,
                    moveId = ThousandShadows, window = -1,
                }, HitResult.Hit);
                opponent.rig.Flash(Color.white, 0.9f);
                opponent.Stagger(60);
                GameFeel.Shake(0.18f);
                GameFeel.Hitstop(3);
                KageGame.I?.Audio?.Play(SfxKind.Slash, 0.8f);
                comboCount++;
            }
            if (f == 66)
            {
                float x = Mathf.Clamp(_ultAnchor.x - opponent.facing * 1.1f, -StageHalf, StageHalf);
                rb.simulated = true;
                rb.position = new Vector2(x, 0.02f);
                transform.position = new Vector3(x, 0.02f, 0f);
                SetFacing(-Mathf.Sign(_ultAnchor.x - x), true);          // back turned to the opponent: the reference pose
                _hidden = false;
                rig.SetVisible(true);
                _display.CopyFrom(_ref);
                _start.CopyFrom(_ref);
                Vfx.I?.InkBurst(rig.World(B.Chest, Vector2.zero), fxDark, 1.0f);
            }
            if (f == 84)
            {
                Vfx.I?.SlashLine(_ultAnchor + new Vector2(-1.3f, 0.3f), _ultAnchor + new Vector2(1.3f, 1.8f), fxMain, Color.white);
                Vfx.I?.SlashLine(_ultAnchor + new Vector2(-1.3f, 1.8f), _ultAnchor + new Vector2(1.3f, 0.3f), fxMain, Color.white);
                GameFeel.Flash(new Color(1f, 0.25f, 0.25f, 0.45f), 0.25f);
                var info = new HitInfo
                {
                    attacker = this,
                    w = new HitWindow { dmg = 14, level = HitLevel.Unblockable, knockdown = true, heavy = true, hitstop = 14, shake = 0.6f, sword = true, stun = 30 },
                    dmg = DamageCalc.For(this, opponent, 14, false, true), kb = new Vector2(-facing * 2.0f, 6f), zone = HitZone.Body, contact = opponent.Pos + Vector2.up * 1.0f,
                    moveId = ThousandShadows, window = -1,
                };
                opponent.state = FState.Idle;
                var res = opponent.ReceiveHit(info);
                OnHitResolved(opponent, info, res);
                comboCount++;
            }
            if (f >= 110) SetState(FState.Idle);
        }
    }
}
