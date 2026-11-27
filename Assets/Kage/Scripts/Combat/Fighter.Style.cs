using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The equipped weapon's attack pattern (<see cref="WeaponStyle"/>, docs/weapon-styles.md) on the fighter: every rule a style
    /// adds is read here, and only here. A fighter whose weapon has no style (every foe) runs none of it, and a style field at its
    /// default changes nothing, so Kagetsu at level 1 plays its old frames exactly until it guards or parries.
    /// <list type="bullet">
    /// <item><b>Footwork</b>: walk, run and dash speed (<see cref="GroundSpeedMul"/>, <see cref="DashSpeedMul"/>), the dash trail, heavy steps.</item>
    /// <item><b>Tempo</b>: the twin blades' momentum (<see cref="StyleRate"/>) and their dash cancel on a landed cut.</item>
    /// <item><b>Blows</b>: before a blow lands, <see cref="StyleShapeHit"/> (weight, the spear's sweet spot, the chain's hook);
    /// after it, <see cref="StyleAfterHit"/> (momentum, hook marks, the cleaver's guard crush).</item>
    /// <item><b>Defence</b>: a guard or a parry opens the riposte (<see cref="NoteDefended"/>, <see cref="TryRiposte"/>); the cleaver's
    /// swings are armoured (<see cref="StyleArmoured"/>, read by the armour check).</item>
    /// <item><b>The scabbard</b>: Shizuka's sheathed frames (<see cref="MoveDef.sheathFrom"/>, a rage art's) and the silent cuts they leave.</item>
    /// </list>
    /// </summary>
    public partial class Fighter
    {
        /// <summary>The equipped weapon's style; null for a weapon without one (foes, bare hands).</summary>
        public WeaponStyle Style { get; private set; }

        float GroundSpeedMul => (Style != null ? SpeedMul * Style.moveMul : SpeedMul) * GroundMul;

        /// <summary>Walk and run speed only (<see cref="CharacterDef.groundSpeedMul"/>): dash and jump distances leave it out.</summary>
        public float GroundMul = 1f;
        float DashSpeedMul => Style != null ? SpeedMul * Style.moveMul * Style.dashMul : SpeedMul;

        // ---- momentum (the twin blades) ----
        float _tempo;
        int _tempoIdle;
        /// <summary>The playback rate the twin blades' momentum adds now (0 to <see cref="WeaponStyle.tempoMax"/>).</summary>
        public float Momentum => _tempo;

        // ---- the riposte (the katanas) ----
        int _riposteLeft;
        /// <summary>True while a Weapon press would play the style's Iai Counter (after a guard or a parry).</summary>
        public bool RiposteReady => _riposteLeft > 0 && Style != null && Style.HasRiposte;

        // ---- the hook mark (the chain sickle): on the fighter that carries it ----
        int _hookLeft;
        Fighter _hookBy;
        /// <summary>True while a chain-sickle wielder's hook mark is on this fighter.</summary>
        public bool Hooked => _hookLeft > 0 && _hookBy != null;

        // ---- silent cuts (Shizuka) ----
        struct PendingEcho { public Fighter target; public int frames, dmg; public string moveId; }
        readonly List<PendingEcho> _echoes = new();
        readonly List<IHittable> _echoTmp = new();
        /// <summary>Silent cuts waiting to open (their owner's frames).</summary>
        public int PendingSilentCuts => _echoes.Count;

        // ---- the scabbard and the heavy steps ----
        /// <summary>A rage art holds the blade in its scabbard (Silent Moon).</summary>
        bool _artSheathed;
        float _stepPhase = -1f;

        void ResetStyleState()
        {
            _tempo = 0f; _tempoIdle = 0;
            _riposteLeft = 0;
            _hookLeft = 0; _hookBy = null;
            _echoes.Clear();
            _artSheathed = false;
            _stepPhase = -1f;
            if (rig != null && rig.Sheathed) rig.SetSheathed(false);
        }

        /// <summary>The style's playback rate on <paramref name="m"/>: momentum speeds up striking moves (those with hit windows), nothing else.</summary>
        float StyleRate(MoveDef m) => Style != null && Style.HasMomentum && m != null && m.hits.Count > 0 ? 1f + _tempo : 1f;

        /// <summary>A damage number × a style factor, rounded, never below 1; exactly the number when the factor is 1.</summary>
        static int StyleScale(int dmg, float f) => f == 1f || dmg <= 0 ? dmg : Mathf.Max(1, Mathf.RoundToInt(dmg * f));

        /// <summary>True when the current move is a Weapon-button swing (it has a blade window) and its last blade window hasn't ended.</summary>
        bool InWeaponSwing()
        {
            if (state != FState.Attack || move == null) return false;
            int last = -1;
            foreach (var w in move.hits) if (w.sword && w.to > last) last = w.to;
            return last >= 0 && moveFrame <= last;
        }

        /// <summary>The cleaver's armoured swings: light blows don't flinch a Weapon swing before its last blow (read by the armour check).</summary>
        bool StyleArmoured => Style != null && Style.armouredSwings && InWeaponSwing();

        // =====================================================================================
        //  ONE FRAME
        // =====================================================================================
        void StyleTick()
        {
            if (Style == null) { if (_hookLeft > 0 && --_hookLeft == 0) _hookBy = null; return; }
            if (_riposteLeft > 0)
            {
                _riposteLeft--;
                if (_riposteLeft % 6 == 5) Vfx.I?.Glint(rig.World(B.Sword, BladeTipLocal * 0.7f), fxCore);     // the tell: the edge catches the light
            }
            if (_hookLeft > 0 && --_hookLeft == 0) _hookBy = null;
            if (_tempo > 0f && ++_tempoIdle > Style.tempoHoldFrames) _tempo = Mathf.Max(0f, _tempo - 0.02f);   // momentum drains a step a frame
            EchoStep();
            SheathStep();
            QuakeStep();
            HeavyStepStep();
        }

        // =====================================================================================
        //  THE TREMOR (the hammer)
        // =====================================================================================
        /// <summary>On a quake move's impact frame the floor shakes: every grounded foe near the impact is stunned (the JS Earthshatter rule).</summary>
        void QuakeStep()
        {
            if (!Style.HasQuake || state != FState.Attack || move == null) return;
            foreach (var q in Style.quakes)
            {
                if (q.moveId != move.id || !Passed(q.frame)) continue;
                Vector2 at = new(Pos.x + facing * q.ahead, 0f);
                Vfx.I?.QuakeStomp(at, Style.quakeRadius);
                GameFeel.Shake(0.28f);
                KageGame.I?.Audio?.Play(SfxKind.Thud, 0.9f);
                if (ctx == null) continue;
                var list = ctx.Fighters;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null || f == this || f.team == team || !f.isActiveAndEnabled || f.health <= 0f) continue;
                    if (!f.Grounded || f.state is FState.KO or FState.Down or FState.Launched or FState.Thrown or FState.Ultimate) continue;
                    if (Mathf.Abs(f.Pos.x - at.x) > Style.quakeRadius) continue;
                    if (f.state == FState.Attack && f.move != null && f.MoveInvulnerable()) continue;      // a roll or a dodge's own frames still clear it
                    f.Stagger(f.ScaleStun(Style.quakeStun, HardStunResist));
                    f.rig?.Flash(new Color(1f, 0.85f, 0.4f), 0.4f, 12f);
                    KageGame.I?.Hud?.Float(f.Pos + Vector2.up * 2.1f, "QUAKE", new Color(1f, 0.78f, 0.3f), 0.8f);
                }
            }
        }

        // =====================================================================================
        //  BLOWS
        // =====================================================================================
        /// <summary>
        /// Before a blow of this fighter's move reaches <paramref name="t"/>: the style's damage factor (returned) and its push
        /// (<see cref="HitInfo.kb"/>) and posture (<see cref="HitInfo.posture"/>). The spear's sweet spot reads where on the reach the
        /// blow landed; a chain sickle blow on a foe it hooked spends the mark and drags the foe in.
        /// </summary>
        float StyleShapeHit(IHittable t, HitWindow w, ref HitInfo info)
        {
            _styleCall = StyleCall.None;
            var s = Style;
            if (s == null || w == null) return 1f;
            float dmg = s.damageMul;
            if (s.postureMul != 1f) info.posture = s.postureMul;
            if (!w.sword) return dmg;
            if (s.HasSweetSpot)
            {
                float u = ReachAlongBlade(info.contact);
                if (s.tipZone > 0f && u >= 1f - s.tipZone)
                {
                    dmg *= s.tipDamage;
                    info.kb = new Vector2(info.kb.x * s.tipKnockback, info.kb.y);
                    _styleCall = StyleCall.Tip;
                }
                else if (s.innerZone > 0f && u <= s.innerZone) dmg *= s.innerDamage;
            }
            if (s.HasHook && t is Fighter tf && tf.Hooked && ReferenceEquals(tf._hookBy, this))
            {
                dmg *= s.hookDamage;
                tf._hookLeft = 0; tf._hookBy = null;                        // the mark is spent
                info.kb = new Vector2(-facing * s.hookPull * 0.42f, Mathf.Max(0.4f, info.kb.y * 0.4f));   // dragged in, not knocked away
                _styleCall = StyleCall.Reap;
            }
            return dmg;
        }

        enum StyleCall { None, Tip, Reap }
        StyleCall _styleCall;

        /// <summary>Where on the weapon's reach <paramref name="contact"/> lies: 0 at the guard (a polearm's line starts behind its lead hand), 1 at the tip.</summary>
        float ReachAlongBlade(Vector2 contact)
        {
            if (rig == null) return 0.5f;
            Vector2 a = rig.World(B.Sword, BladeBaseLocal), b = rig.World(B.Sword, BladeTipLocal);
            Vector2 d = b - a;
            float len2 = d.sqrMagnitude;
            return len2 < 1e-6f ? 0.5f : Mathf.Clamp01(Vector2.Dot(contact - a, d) / len2);
        }

        /// <summary>
        /// After a blow of this fighter's move resolved: momentum, the hook mark, the guard crush. Only a melee window counts (a rage art's
        /// blow, a projectile's and a hazard's don't).
        /// </summary>
        void StyleAfterHit(IHittable t, in HitInfo info, HitResult res)
        {
            var call = _styleCall;
            _styleCall = StyleCall.None;
            var s = Style;
            if (s == null || info.window < 0 || info.origin != HitOrigin.Melee || info.w == null) return;
            var tf = t as Fighter;
            if (res == HitResult.Hit)
            {
                if (s.legendary) Vfx.I?.PrismSparks(info.contact, 7, 0.8f);
                if (s.HasMomentum)
                {
                    _tempo = Mathf.Min(s.tempoMax, _tempo + s.tempoPerHit);
                    _tempoIdle = 0;
                    if (_tempo >= s.tempoMax - 1e-4f && comboCount % 4 == 0) KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.2f, "STORM", fxMain, 0.8f);
                }
                if (call == StyleCall.Tip)
                {
                    Vfx.I?.TipSplash(info.contact, new Vector2(facing, 0f));
                    KageGame.I?.Hud?.Float(info.contact + Vector2.up * 0.5f, "TIP", new Color(0.55f, 0.82f, 1f), 0.75f);
                }
                if (call == StyleCall.Reap)
                {
                    Vfx.I?.ReapBurst(info.contact, new Vector2(-facing, 0f));
                    KageGame.I?.Hud?.Float(info.contact + Vector2.up * 0.55f, "REAP", new Color(0.78f, 0.55f, 1f), 0.85f);
                }
                else if (s.HasHook && info.w.sword && tf != null && tf.state != FState.KO)
                {
                    tf._hookLeft = s.hookFrames;
                    tf._hookBy = this;
                    Vfx.I?.HookMark(info.contact);
                    KageGame.I?.Hud?.Float(info.contact + Vector2.up * 0.5f, "HOOKED", new Color(0.62f, 0.4f, 0.9f), 0.7f);
                }
            }
            else if (res == HitResult.Blocked && tf != null && info.w.sword && s.guardCrushChip > 0f)
            {
                // the cleaver crushes through the guard: more chip, a harder shove, sparks of the guard giving way
                tf.health = Mathf.Max(1f, tf.health - Mathf.Ceil(info.dmg * s.guardCrushChip));
                tf.Knock(new Vector2(tf.KnockVelocity(new Vector2(info.kb.x * 0.55f * s.guardCrushPush + Mathf.Sign(info.kb.x) * 0.8f, 0f)).x, tf.rb.linearVelocity.y));
                Vfx.I?.GuardCrush(info.contact, new Vector2(facing, 0f));
                GameFeel.Shake(0.12f);
            }
        }

        /// <summary>This fighter guarded or parried a blow: the weapon's riposte is open for its window.</summary>
        void NoteDefended()
        {
            if (Style == null || !Style.HasRiposte) return;
            _riposteLeft = Style.riposteFrames;
        }

        /// <summary>
        /// The riposte: a Weapon press (not Down, which stays the low cut) in its window plays the style's Iai Counter at once, out of a
        /// guard's blockstun too. True when it started.
        /// </summary>
        bool TryRiposte()
        {
            if (!RiposteReady || _buffer != Btn.S || _bufferAge > 8 || !Grounded || _bufferDy < 0 || SwordBroken) return false;
            var m = Armed(Style.riposte);
            if (m == null) return false;
            ConsumeBuffer();
            _riposteLeft = 0;
            if (opponent != null && opponent.Pos.x != Pos.x) SetFacing(opponent.Pos.x - Pos.x, true);
            StartMove(m);
            Vector2 glintAt = rig.World(B.Sword, def.bladeTipLocal * 0.5f);
            if (PresentsCombat)
            {
                Vfx.I?.Glint(glintAt, Color.white);
                GameFeel.ImpactFrame(new Color(0.1f, 0.035f, 0.14f, 0.77f), 2f / 60f);
                GameFeel.Flash(new Color(1f, 1f, 1f, 0.22f), 0.08f);
                CombatAudio?.Play(SfxKind.Counter, 0.85f);
            }
            if (isPlayer) (ctx?.Hud ?? KageGame.I?.Hud)?.ShowCallout(CombatCalloutKind.IaiCounter, glintAt);
            return true;
        }

        /// <summary>The twin blades' flow: after a landed blade blow, a dash press cancels the rest of the move into a dash.</summary>
        bool DashCancelStep(MoveDef m, int fwd)
        {
            if (Style == null || !Style.dashCancelOnHit || !moveHit || !Grounded || m.airborne || _dashBuf > 4 || _dashCool > 0) return false;
            bool blade = false;
            foreach (var w in m.hits) if (w.sword) { blade = true; break; }
            if (!blade) return false;
            _dashBuf = 99;
            int dir = fwd < 0 ? -1 : 1;                       // the way the stick is held: back flows out, anything else on through
            PlayCancel(dir * facing);
            StartDash(dir, false);
            return true;
        }

        // =====================================================================================
        //  THE SCABBARD AND THE SILENT CUT (Shizuka)
        // =====================================================================================
        /// <summary>Shows the blade in its scabbard while the move or a rage art holds it there; queues the silent cuts as the blade clicks home.</summary>
        void SheathStep()
        {
            bool want = _artSheathed || (state == FState.Attack && move != null && move.Sheathed(moveFrame));
            if (rig != null && rig.Sheathed != want) rig.SetSheathed(want);
            if (state != FState.Attack || move == null || Style.echoes == null) return;
            foreach (var e in Style.echoes)
            {
                if (e.moveId != move.id || !Passed(e.sheathFrame) || !moveHit) continue;
                // every fighter the move's blows landed on gets the silent cut, once
                _echoTmp.Clear();
                foreach (var (_, ht) in _hitLog) if (ht is Fighter f && !_echoTmp.Contains(f)) _echoTmp.Add(f);
                foreach (var h in _echoTmp)
                {
                    var f = (Fighter)h;
                    _echoes.Add(new PendingEcho { target = f, frames = Style.echoDelay, dmg = e.damage, moveId = move.id });
                    Vfx.I?.Glint(f.Pos + Vector2.up * 1.1f, new Color(0.85f, 0.77f, 1f));
                }
                KageGame.I?.Audio?.Play(SfxKind.Block, 0.35f);           // the blade clicks home
            }
        }

        void EchoStep()
        {
            for (int i = _echoes.Count - 1; i >= 0; i--)
            {
                var e = _echoes[i];
                if (--e.frames > 0) { _echoes[i] = e; continue; }
                _echoes.RemoveAt(i);
                var tgt = e.target;
                if (tgt == null || !tgt.isActiveAndEnabled || tgt.state == FState.KO || tgt.health <= 0f || state == FState.KO) continue;
                OpenSilentCut(tgt, e.dmg, e.moveId);
            }
        }

        /// <summary>A silent cut opens on <paramref name="tgt"/>: an unblockable blow for <paramref name="dmg"/>, the big one knocks down.</summary>
        void OpenSilentCut(Fighter tgt, int dmg, string moveId)
        {
            bool big = dmg >= 10;
            var w = new HitWindow
            {
                dmg = dmg, level = HitLevel.Unblockable, kb = big ? new Vector2(2.0f, 2.6f) : new Vector2(1.0f, 0.6f), stun = big ? 30 : 18,
                knockdown = big, heavy = big, sword = true, hitstop = big ? 8 : 4, shake = big ? 0.3f : 0.14f, radius = 0.1f,
            };
            Vector2 at = tgt.Pos + Vector2.up * 1.05f;
            float dir = Mathf.Sign(tgt.Pos.x - Pos.x);
            if (dir == 0f) dir = facing;
            var info = new HitInfo
            {
                attacker = this, w = w, dmg = DamageCalc.For(this, tgt, dmg, false, true), kb = new Vector2(w.kb.x * dir, w.kb.y),
                zone = HitZone.Body, contact = at, moveId = moveId + ".silentCut", window = -1, origin = HitOrigin.Projectile,
            };
            if (tgt.state == FState.Down && big) tgt.Stagger(1);
            var res = tgt.ReceiveHit(info);
            Vfx.I?.SilentCut(at, dir, big ? 1.3f : 0.8f);
            if (big) KageGame.I?.Hud?.Float(at + Vector2.up * 0.9f, "SILENCE", new Color(0.72f, 0.55f, 1f), 1.1f);
            GameFeel.Hitstop(w.hitstop);
            GameFeel.Shake(w.shake);
            KageGame.I?.Audio?.Play(SfxKind.Slash, big ? 1f : 0.7f);
            if (res == HitResult.Hit) { comboCount++; meter = Mathf.Min(100f, meter + (2f + info.dmg * 0.4f) * RageMul); }
            PublishHit(tgt, info, res);
            tgt.FlushGuardBreak();
            onHit?.Invoke(this, tgt, info, res);
        }

        // =====================================================================================
        //  FOOTWORK
        // =====================================================================================
        /// <summary>One frame of a dash: the weapon's own trail (the JS <c>WeaponFootwork.dash</c>), every other frame.</summary>
        void StyleFootwork(int f)
        {
            if (Style == null || Style.footwork == Footwork.None || Vfx.I == null || f % 2 != 0) return;
            Vfx.I.FootworkTrail(Style.footwork, rig, Pos, facing * _dashDir, Grounded);
            if (Style.legendary && f % 4 == 0) Vfx.I.PrismSparks(Pos + new Vector2(-facing * _dashDir * 0.2f, Random.Range(0.3f, 1.4f)), 2, 0.5f);
        }

        /// <summary>A heavy weapon thuds on every running footfall: dust and a tremor (the JS <c>WeaponFootwork.stomp</c>).</summary>
        void HeavyStepStep()
        {
            if (!Style.heavyStep || state != FState.Run || !Grounded) { _stepPhase = -1f; return; }
            float ph = _gaitPhase;
            if (_stepPhase >= 0f && (Crossed(_stepPhase, ph, 0f) || Crossed(_stepPhase, ph, 0.5f)))
            {
                Vfx.I?.Dust(Pos + new Vector2(facing * 0.1f, 0f), 0.6f, -facing);
                GameFeel.Shake(0.025f);
            }
            _stepPhase = ph;
        }

        static bool Crossed(float a, float b, float at) => a <= b ? a < at && b >= at : a < at || b >= at;     // phase wraps at 1

        void StyleLanded(float hard)
        {
            if (Style == null || !Style.heavyStep) return;
            Vfx.I?.Dust(Pos, 1.1f + hard, 0f);
            GameFeel.Shake(0.06f + 0.08f * hard);
        }
    }
}
