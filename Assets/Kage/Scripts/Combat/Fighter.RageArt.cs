using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The rage arts (SAN-82, ADR-0004 and ADR-0006): Special + Down with full Rage starts the art of the equipped weapon's
    /// <see cref="WeaponFamilyDef.rageArt"/> (the family decides, never the weapon id): <b>Crimson Moon</b> for the katana
    /// family, Thousand Shadows (Fighter.Specials.cs) for every other. Both spend the whole meter and run in the
    /// <see cref="FState.Ultimate"/> state, where the fighter can't be hit.
    /// <para>
    /// <b>Crimson Moon</b> is the JS art (67-rage-arts.js) on the fight's own mechanisms; every number is in
    /// <see cref="CrimsonMoonTuning"/> (crimsonMoon.json), timed in the art's frames (<see cref="RageArtFrame"/>):
    /// </para>
    /// <list type="number">
    /// <item><b>Freeze</b> (frame 0): <see cref="FightClock.SlowWorld"/> at 0 with Kage exempt (ADR-0002, built by SAN-92): every other
    /// fighter and every projectile of theirs holds still, in a group fight too; Kage and what he owns run. The cut-in band
    /// (cue <c>VFX_Kage_RageCutIn</c>) slides across with the name and kanji, and the camera's override closes in on him.</item>
    /// <item><b>Cuts</b> (frames 33, 44, 56): Kage stands beside the foe, alternating sides, and a crimson crescent
    /// (cue <c>VFX_Rage_Crimson_Crescent</c>) cuts it for 7. The first binds it. Unblockable: no guard, parry or counter.</item>
    /// <item><b>Step back and sheathe</b> (frame 67), a beat of slow motion; <b>the giant crescent</b> (frame 83) for 15 with a
    /// knockdown, hit-stop, slow motion, shake and punch. The world runs again from the finisher on.</item>
    /// <item><b>End</b> (frame 111): Kage is in neutral and the camera override is given back.</item>
    /// </list>
    /// <para>
    /// <b>Who is bound:</b> the foe Kage's target was when it started (<see cref="opponent"/>, held for the art by
    /// <see cref="PickTarget"/>), and nobody else, as in the JS game (<c>tgt = nearestEnemy</c>: only it is bound and cut).
    /// In a group fight every other foe is frozen with the world, then free again from the finisher.
    /// </para>
    /// </summary>
    public partial class Fighter
    {
        public const string CrimsonMoon = "crimsonMoon";

        /// <summary>The cut-in band's cue, and the crescent's (the JS manifest id), the finisher's, the step's and the charge's.</summary>
        public const string RageArtCutInCue = "VFX_Kage_RageCutIn";
        public const string CrimsonCrescentCue = "VFX_Rage_Crimson_Crescent";
        public const string CrimsonFinishCue = "VFX_Kage_CrimsonMoonFinish";
        public const string CrimsonStepCue = "VFX_Kage_CrimsonMoonStep";
        public const string CrimsonChargeCue = "VFX_Kage_CrimsonMoonCharge";

        static CrimsonMoonTuning CmT => Tuning.Get<CrimsonMoonTuning>();

        string _artId = ThousandShadows;
        bool _cmActive, _cmFrozen, _cmCamOn;
        Fighter _cmTarget;
        float _cmSide;
        CameraRig _cmRig;
        bool _cmPrevUse;
        Vector2 _cmPrevCenter;
        float _cmPrevSize;

        /// <summary>The rage art the equipped weapon's family has: <c>crimsonMoon</c> or <c>thousandShadows</c> (ADR-0006).</summary>
        public string RageArtId =>
            Style != null && !string.IsNullOrEmpty(Style.rageArt) ? Style.rageArt
            : Family != null && !string.IsNullOrEmpty(Family.rageArt) ? Family.rageArt : ThousandShadows;

        /// <summary>The rage art being played now, or null.</summary>
        public string ActiveRageArt => state == FState.Ultimate ? _artId : null;

        /// <summary>Frames the rage art has run (Kage's ticks: hit-stop and slow motion don't count). 0 outside one.</summary>
        public int RageArtFrame => state == FState.Ultimate ? _ultFrame : 0;

        /// <summary>
        /// Hook for the AI (SAN-95): true when Special + Down would start the rage art now (the flag, a full meter, on the ground,
        /// free to act). The AI input source doesn't use it yet.
        /// </summary>
        public bool RageArtReady => Has(CharacterFlags.CanRageArt) && meter >= 100f && Grounded && state is FState.Idle or FState.Walk or FState.Run or FState.Crouch;

        /// <summary>
        /// Starts the equipped weapon's rage art (the caller checked the flag, the meter and the ground), unless the target is a sub-boss or the
        /// boss that isn't open to it: then the art is turned aside (Fighter.RageArtCounter.cs).
        /// </summary>
        void StartRageArt()
        {
            string art = RageArtId;
            if (opponent != null && opponent.WouldCounterRageArt(this)) { RageArtTurnedAside(opponent, art); return; }
            if (art == Mikazuki && StartMikazuki()) return;
            // a weapon's art needs someone to cut; with no target (the training bag) the press is spent as Thousand Shadows always was
            var cine = opponent != null && ctx != null ? MakeArt(art) : null;
            if (art == CrimsonMoon && opponent != null) StartCrimsonMoon();
            else if (cine != null) StartCinematicArt(cine);
            else StartUltimate(IsCinematicArt(art) ? art : ThousandShadows);
        }

        // =====================================================================================
        //  CRIMSON MOON
        // =====================================================================================
        void StartCrimsonMoon()
        {
            var t = CmT;
            meter = 0f;
            SetState(FState.Ultimate);
            rig.SetSwordFront(true);
            _ultFrame = 0;
            _artId = CrimsonMoon;
            _cmActive = true;
            _cmTarget = opponent;
            rb.linearVelocity = Vector2.zero;
            float dx = _cmTarget.Pos.x - Pos.x;
            if (dx != 0f) SetFacing(dx, true);
            _cmSide = Mathf.Sign(Pos.x - _cmTarget.Pos.x);
            if (_cmSide == 0f) _cmSide = -facing;

            // the world holds still, Kage exempt; replaces a perfect dodge's slow that may be running (ResumeWorld at the finisher clears it)
            ctx.Clock.SlowWorld(0f, -1, this);
            _cmFrozen = true;
            ctx.Events.Publish(CombatEvent.RageArt(ctx.Clock.Frame, this, CrimsonMoon));
            var look = RageArtLook.Crimson;
            ArtHud?.CutIn(look.name.ToUpperInvariant(), look.kanji, RageArtCues.FormatCutInCaption(look, WeaponId), look.col, look.hot, t.cutInSeconds, look.legendary, look.deep);
            EffectCues.Play(RageArtCutInCue, Pos, new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = 1f, seconds = t.cutInSeconds });
            GameFeel.Punch(0.1f);
            KageGame.I?.Audio?.Play(SfxKind.Ultimate, 1f);
        }

        static bool CmLive(Fighter f) => f != null && f.isActiveAndEnabled && f.state != FState.KO && f.health > 0f;

        void CrimsonMoonUpdate()
        {
            var t = CmT;
            _ultFrame++;
            int f = _ultFrame;
            var tgt = _cmTarget;

            // the step back's speed dies away (JS damp)
            Vector2 v = rb.linearVelocity;
            v.x *= Mathf.Exp(-t.stepBackDamp * Time.fixedDeltaTime);
            if (Mathf.Abs(v.x) < 0.01f) v.x = 0f;
            rb.linearVelocity = v;

            if (f < t.anticipationFrames && f % 2 == 0)
                EffectCues.Play(CrimsonChargeCue, rig.World(B.Sword, def.bladeTipLocal * 0.5f), new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = f / (float)Mathf.Max(1, t.anticipationFrames) });

            for (int i = 0; i < t.cutFrames.Length; i++)
                if (f == t.cutFrames[i]) CrimsonCut(i, tgt);
            if (f == t.stepBackFrame)
            {
                rb.linearVelocity = new Vector2(-facing * t.stepBackSpeed, rb.linearVelocity.y);
                GameFeel.SlowMo(t.stepBackSlowScale, t.stepBackSlowSeconds);
                KageGame.I?.Audio?.Play(SfxKind.Block, 0.5f);
            }
            if (f == t.finisherFrame) CrimsonFinisher(tgt);

            AimCrimsonCamera(tgt, f);
            ArtHud?.ArtGrade(RageArtLook.Crimson.deep, f < t.finisherFrame ? 1f : 0.45f);
            Vfx.I?.SkyGrade(RageArtLook.Crimson.deep, f < t.finisherFrame ? 1f : 0.5f, ctx != null && ctx.Camera != null ? ctx.Camera.cam : null);
            if (f >= t.totalFrames) EndCrimsonMoon();
        }

        /// <summary>Teleports Kage beside <paramref name="tgt"/> on <paramref name="side"/> (the other side if that is off the stage), facing it. Returns the side used.</summary>
        float PlaceBeside(Fighter tgt, float side)
        {
            float d = CmT.standDistance;
            float x = tgt.Pos.x + side * d;
            if (x < StageMin || x > StageMax) { side = -side; x = tgt.Pos.x + side * d; }
            x = Mathf.Clamp(x, StageMin, StageMax);
            rb.position = new Vector2(x, 0.02f);
            transform.position = new Vector3(x, 0.02f, 0f);
            rb.linearVelocity = Vector2.zero;
            SetFacing(Mathf.Sign(tgt.Pos.x - x), true);
            return side;
        }

        void CrimsonCut(int i, Fighter tgt)
        {
            var t = CmT;
            bool live = CmLive(tgt);
            if (live)
            {
                if (i > 0) EffectCues.Play(CrimsonStepCue, Pos, new CueArgs { owner = this, dir = new Vector2(-facing, 0f) });
                _cmSide = PlaceBeside(tgt, i == 0 ? _cmSide : -_cmSide);
            }
            Vector2 at = live ? tgt.Pos + Vector2.up : Pos + new Vector2(facing * 1.05f, 1f);
            float ang = i < t.cutAngles.Length ? t.cutAngles[i] : 0f;
            EffectCues.Play(CrimsonCrescentCue, at, new CueArgs { owner = this, dir = new Vector2(Mathf.Cos(ang) * facing, Mathf.Sin(ang)), scale = t.cutScale });
            if (live)
            {
                var w = new HitWindow
                {
                    dmg = t.cutDamage, level = HitLevel.Unblockable, kb = t.cutKnockback, stun = t.bindFrames, sword = true,
                    hitstop = t.cutHitstop, shake = t.cutShake, radius = 0.09f,
                };
                DeliverArtHit(tgt, w, t.cutKnockback);
                if (i == 0 && tgt.state != FState.Launched) tgt.Stagger(t.bindFrames);     // bound: held where it stands
                if (_cmFrozen) tgt.rig?.SetFlash(RageArtLook.Crimson.col, 0.28f, 12f);    // held, it can't fade the hit's white: a faint crimson instead
            }
            GameFeel.Hitstop(t.cutHitstop);
            GameFeel.Shake(t.cutShake);
            KageGame.I?.Audio?.Play(SfxKind.Slash, 0.8f);
        }

        void CrimsonFinisher(Fighter tgt)
        {
            var t = CmT;
            bool live = CmLive(tgt);
            Vector2 at = live ? tgt.Pos + Vector2.up : Pos + new Vector2(facing * 1.6f, 1f);
            EffectCues.Play(CrimsonCrescentCue, at, new CueArgs { owner = this, dir = new Vector2(Mathf.Cos(t.finisherAngle) * facing, Mathf.Sin(t.finisherAngle)), scale = t.finisherScale });
            if (live)
            {
                if (tgt.state == FState.Down) tgt.Stagger(1);       // a foe lying down stands to be struck, so the blow throws it
                var w = new HitWindow
                {
                    dmg = t.finisherDamage, level = HitLevel.Unblockable, kb = t.finisherKnockback, stun = t.finisherStun, knockdown = true, heavy = true,
                    sword = true, hitstop = t.finisherHitstop, shake = t.finisherShake, radius = 0.12f,
                };
                DeliverArtHit(tgt, w, t.finisherKnockback);
            }
            EffectCues.Play(CrimsonFinishCue, at, new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = t.finisherScale });
            var look = RageArtLook.Crimson;
            ArtHud?.ArtStamp(look.stamp, look.kanji, RageArtCues.FormatStampCaption(look, WeaponId), look.col, look.hot, 1.25f, look.legendary, look.deep);       // MOONFALL (docs/weapon-styles.md)
            GameFeel.Hitstop(t.finisherHitstop);
            GameFeel.SlowMo(t.finisherSlowScale, t.finisherSlowSeconds);
            GameFeel.Shake(t.finisherShake);
            GameFeel.Punch(t.finisherPunch);
            KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 1f);
            ReleaseRageArtWorld();          // the freeze ends with the blow: the foe is thrown into a world that runs again
        }

        /// <summary>
        /// One unblockable blow of the art straight to <see cref="Fighter.ReceiveHit"/>: no guard, parry, counter or perfect dodge
        /// (the level is Unblockable, and the check before the hit is skipped), no crit roll. Publishes the <c>Hit</c> event
        /// (MoveId <c>crimsonMoon</c>, window -1) and the hit callback. Kage gains no Rage from it: the art spent it.
        /// </summary>
        void DeliverArtHit(Fighter tgt, HitWindow w, Vector2 kb)
        {
            var info = new HitInfo
            {
                attacker = this, w = w, dmg = DamageCalc.For(this, tgt, w.dmg, false, true), kb = new Vector2(kb.x * facing, kb.y),
                zone = HitZone.Body, contact = tgt.Pos + Vector2.up, moveId = CrimsonMoon, window = -1,
            };
            var res = tgt.ReceiveHit(info);
            comboCount++;
            PublishHit(tgt, info, res);
            tgt.FlushGuardBreak();
            FlushGuardBreak();
            onHit?.Invoke(this, tgt, info, res);
        }

        void EndCrimsonMoon()
        {
            _cmActive = false;
            ReleaseRageArtWorld();
            ReleaseRageArtCamera();
            ArtHud?.ArtGrade(Color.black, 0f);
            Vfx.I?.SkyGrade(Color.black, 0f);
            SetState(FState.Idle);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        /// <summary>The art ends early (the round resets, the fight ends, Kage is replaced): the world and the camera are given back.</summary>
        void AbortRageArt()
        {
            if (_art != null) AbortCinematicArt();
            if (state == FState.Ultimate) ArtHud?.ArtGrade(Color.black, 0f);
            if (!_cmActive && !_cmFrozen && !_cmCamOn) return;
            _cmActive = false;
            ReleaseRageArtWorld();
            ReleaseRageArtCamera();
            ArtHud?.ArtGrade(Color.black, 0f);
            Vfx.I?.SkyGrade(Color.black, 0f);
        }

        /// <summary>The world runs again, if the art holds it (a later SlowWorld by someone else is left alone).</summary>
        void ReleaseRageArtWorld()
        {
            if (!_cmFrozen) return;
            _cmFrozen = false;
            if (ctx != null && ReferenceEquals(ctx.Clock.WorldExempt, this) && ctx.Clock.WorldScale < 1f) ctx.Clock.ResumeWorld();
        }

        // ---- the camera: the rig's own override (CameraRig.useOverride), which wins over every framing ----
        void AimCrimsonCamera(Fighter tgt, int f)
        {
            var camRig = ctx != null ? ctx.Camera : null;
            if (camRig == null || camRig.cam == null) return;
            var t = CmT;
            if (!_cmCamOn)
            {
                _cmRig = camRig;
                _cmPrevUse = camRig.useOverride;
                _cmPrevCenter = camRig.overrideCenter;
                _cmPrevSize = camRig.overrideSize;
                camRig.useOverride = true;
                _cmCamOn = true;
            }
            float size, cx;
            if (f < t.anticipationFrames) { size = t.closeInSize; cx = Pos.x + facing * t.closeInAhead; }
            else
            {
                size = t.exchangeSize;
                cx = tgt != null && tgt.isActiveAndEnabled ? (Pos.x + tgt.Pos.x) * 0.5f : Pos.x;
            }
            float half = size * Mathf.Max(0.5f, camRig.cam.aspect);
            cx = camRig.ClampX(cx, half);
            camRig.overrideSize = size;
            camRig.overrideCenter = new Vector2(cx, size * 0.64f);
        }

        void ReleaseRageArtCamera()
        {
            if (!_cmCamOn) return;
            _cmCamOn = false;
            if (_cmRig == null) return;
            _cmRig.useOverride = _cmPrevUse;
            _cmRig.overrideCenter = _cmPrevCenter;
            _cmRig.overrideSize = _cmPrevSize;
            _cmRig = null;
        }

        // ---- Kage's poses through the art: the katana's own key poses, in the JS art's order ----
        static Pose Pz() => new Pose();

        /// <summary>
        /// The art's pose track (frames are the art's): the draw back, then the cut poses of the JS art's order (Iai, Rising Moon,
        /// Kage Cleave strike poses for the three cuts), the sheathing (the draw back again, as Mirror Guard's ready) and the
        /// Counter's strike for the giant crescent. Evaluated from the pose he was in when it started.
        /// </summary>
        static readonly MoveDef CrimsonMovePose = BuildCrimsonMovePose();

        static MoveDef BuildCrimsonMovePose()
        {
            Pose draw() => Pz().Hip(-0.03f, -0.11f, -4f).Chest(4f).Head(4f).Feet(0.22f, -0.26f).Grip().HandF(-0.04f, 0.84f).Blade(-158f);
            return new MoveDef(CrimsonMoon, "Crimson Moon", 111)
                .Key(14, draw())
                .Key(25, draw())
                // cut one: Iai
                .Key(35, Pz().Hip(0.08f, -0.11f, -10f).Chest(-16f).Head(10f).Feet(0.32f, -0.22f).Grip().HandF(0.42f, 1.08f).Blade(8f), Ease.OutExpo)
                .Key(40, Pz().Hip(0.1f, -0.1f, -12f).Chest(-20f).Head(12f).Feet(0.32f, -0.22f).Grip().HandF(0.36f, 1.26f).Blade(62f))
                // cut two: Rising Moon, from the other side
                .Key(46, Pz().Hip(0.12f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.36f, 0.13f).FootF(-0.15f, 0.15f, -20f).Grip().HandF(0.36f, 1.42f).Blade(96f), Ease.OutQuad)
                .Key(51, Pz().Hip(0.1f, -0.04f, 2f).Chest(10f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -20f).Grip().HandF(0.26f, 1.52f).Blade(110f))
                // cut three: Kage Cleave's strike
                .Key(58, Pz().Hip(0f, 0f, -10f).Chest(-28f).Head(14f).LegN(60f, -70f).LegF(30f, -60f).Grip().HandF(0.52f, 1.02f).Blade(-48f), Ease.InCubic)
                .Key(63, Pz().Hip(0.04f, -0.32f, -6f).Chest(-34f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.24f, 0.15f, -25f).Grip().HandF(0.52f, 0.5f).Blade(-28f))
                // the step back and the sheathing
                .Key(72, draw(), Ease.InOutQuad)
                .Key(81, draw())
                // the giant crescent: the Counter's strike
                .Key(86, Pz().Hip(0.08f, -0.2f, -14f).Chest(-34f).Head(20f).Feet(0.38f, -0.28f).Grip().HandF(0.55f, 1.0f).Blade(2f), Ease.OutExpo)
                .Key(95, Pz().Hip(0.08f, -0.2f, -14f).Chest(-34f).Head(20f).Feet(0.38f, -0.28f).Grip().HandF(0.55f, 1.0f).Blade(6f))
                .Key(111, Stance(), Ease.InOutQuad)
                .Sword();
        }

        void CrimsonMovePoseOutput(float alpha) => CrimsonMovePose.Evaluate(def, _ultFrame + alpha, _start, _display);
    }
}
