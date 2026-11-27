using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The weapon rage arts after Crimson Moon (docs/weapon-styles.md; the JS 67-rage-arts.js): one per player weapon, each a
    /// <see cref="CinematicArt"/> nested in the fighter (Fighter.Art.*.cs) so it drives Kage the way Crimson Moon does, on the fight's
    /// own mechanisms:
    /// <list type="bullet">
    /// <item><b>The freeze</b>: <see cref="FightClock.SlowWorld"/> at 0 with Kage exempt from the first frame to the finisher
    /// (ADR-0002's rule for Crimson Moon); every other fighter and projectile holds still. The bound target is held where it stands
    /// (its velocity is zeroed after each blow), unless the art moves it on purpose (the water dragon lifts it).</item>
    /// <item><b>The cut-in band</b> with the art's name and kanji (cue <c>VFX_Kage_RageCutIn</c>), and <b>the grade</b>: the arena
    /// takes the art's deep colour (<see cref="Hud.ArtGrade"/>).</item>
    /// <item><b>The blows</b>: unblockable, straight to <see cref="ReceiveHit"/> (<see cref="ArtBlow"/>), each publishing a Hit with the art's id.</item>
    /// <item><b>The finisher</b>: the art's word on the screen (<see cref="Hud.ArtStamp"/>), an impact flash in the art's colour, hit-stop,
    /// slow motion, a shake and a zoom punch; the world runs again.</item>
    /// <item><b>The camera</b>: the rig's own override (<see cref="CameraRig.useOverride"/>), close on Kage through the anticipation, then
    /// framing the exchange; given back at the end, or when the art is cut short (<see cref="AbortRageArt"/>).</item>
    /// </list>
    /// Kage stays in the <see cref="FState.Ultimate"/> state (untouchable) throughout, and his pose comes from the art's track: real key
    /// frames of his weapon family's own moves, played at the art's beats.
    /// </summary>
    public partial class Fighter
    {
        public const string SilentMoon = "silentMoon", SwallowStorm = "swallowStorm", HellSplitter = "hellSplitter",
            WaterDragon = "waterDragon", ReaperRequiem = "reaperRequiem", Earthsplitter = "earthsplitter";

        CinematicArt _art;

        /// <summary>The cinematic art of <paramref name="id"/>; null for Crimson Moon and Thousand Shadows (their own code) and unknown ids.</summary>
        static CinematicArt MakeArt(string id) => id switch
        {
            SilentMoon => new SilentMoonArt(),
            SwallowStorm => new SwallowStormArt(),
            HellSplitter => new HellSplitterArt(),
            WaterDragon => new WaterDragonArt(),
            ReaperRequiem => new ReaperRequiemArt(),
            Earthsplitter => new EarthsplitterArt(),
            _ => null,
        };

        /// <summary>True when <paramref name="id"/> is one of the cinematic arts (every rage art but Thousand Shadows has a cut-in and a finisher word).</summary>
        public static bool IsCinematicArt(string id) => id == CrimsonMoon || MakeArt(id) != null;

        void StartCinematicArt(CinematicArt art)
        {
            meter = 0f;
            SetState(FState.Ultimate);
            rig.SetSwordFront(true);
            _ultFrame = 0;
            _artId = art.Id;
            _cmActive = false;
            _art = art;
            art.k = this;
            art.tgt = opponent;
            rb.linearVelocity = Vector2.zero;
            if (opponent != null)
            {
                float dx = opponent.Pos.x - Pos.x;
                if (dx != 0f) SetFacing(dx, true);
            }
            art.Init();
            ctx.Clock.SlowWorld(0f, -1, this);
            _cmFrozen = true;
            ctx.Events.Publish(CombatEvent.RageArt(ctx.Clock.Frame, this, art.Id));
            var look = art.Look;
            if (look != null)
                ArtHud?.CutIn(look.name.ToUpperInvariant(), look.kanji, RageArtCues.FormatCutInCaption(look, WeaponId), look.col, look.hot, art.cutIn / 60f, look.legendary, look.deep);
            EffectCues.Play(RageArtCutInCue, Pos, new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = 1f, seconds = art.cutIn / 60f });
            GameFeel.Punch(0.1f);
            KageGame.I?.Audio?.Play(SfxKind.Ultimate, 1f);
            if (art.Look != null && art.Look.legendary)
            {
                Vfx.I?.PrismRing(Pos + Vector2.up * 0.9f, 0.3f, 2.2f, 0.12f, 0.7f);
                Vfx.I?.PrismSparks(Pos + Vector2.up * 1f, 22, 1.2f);
            }
            art.Start();
        }

        void CinematicUpdate()
        {
            var art = _art;
            _ultFrame++;
            int f = _ultFrame;
            art.Tick(f);
            if (_art != art) return;                                    // the art ended itself
            AimArtCamera(art.CamSize(f), art.CamX(f), art.CamY(f));
            var look = art.Look;
            ArtHud?.ArtGrade(look.deep, art.finished ? 0.45f : 1f);
            Vfx.I?.SkyGrade(look.deep, art.finished ? 0.5f : 1f, ctx.Camera != null ? ctx.Camera.cam : null);
            if (f >= art.total) EndCinematicArt();
        }

        Hud ArtHud => ctx != null && ctx.Hud != null ? ctx.Hud : KageGame.I?.Hud;

        void EndCinematicArt()
        {
            var art = _art;
            _art = null;
            art?.Stop();
            _artSheathed = false;
            ReleaseRageArtWorld();
            ReleaseRageArtCamera();
            ArtHud?.ArtGrade(Color.black, 0f);
            Vfx.I?.SkyGrade(Color.black, 0f);
            SetState(Grounded ? FState.Idle : FState.Air);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        /// <summary>A cinematic art cut short (a round reset, the fight's end, Kage replaced): the world, the camera and the grade come back.</summary>
        void AbortCinematicArt()
        {
            var art = _art;
            _art = null;
            art?.Stop();
            _artSheathed = false;
            ArtHud?.ArtGrade(Color.black, 0f);
            Vfx.I?.SkyGrade(Color.black, 0f);
        }

        /// <summary>The art camera: the rig's override at <paramref name="size"/>, centred on <paramref name="cx"/> (clamped to the stage) at <paramref name="cy"/>.</summary>
        void AimArtCamera(float size, float cx, float cy)
        {
            var camRig = ctx != null ? ctx.Camera : null;
            if (camRig == null || camRig.cam == null) return;
            if (!_cmCamOn)
            {
                _cmRig = camRig;
                _cmPrevUse = camRig.useOverride;
                _cmPrevCenter = camRig.overrideCenter;
                _cmPrevSize = camRig.overrideSize;
                camRig.useOverride = true;
                _cmCamOn = true;
            }
            float half = size * Mathf.Max(0.5f, camRig.cam.aspect);
            camRig.overrideSize = size;
            camRig.overrideCenter = new Vector2(camRig.ClampX(cx, half), cy);
        }

        /// <summary>
        /// One unblockable blow of a rage art straight to <see cref="ReceiveHit"/> (no guard, parry, counter or perfect dodge, no crit roll),
        /// pushing <paramref name="worldKb"/> (world metres a second, mass 1). Publishes the Hit with the art's id; Kage gains no Rage from it.
        /// </summary>
        void ArtBlow(Fighter tgt, HitWindow w, Vector2 worldKb, string artId, Vector2 contact)
        {
            var info = new HitInfo
            {
                attacker = this, w = w, dmg = DamageCalc.For(this, tgt, w.dmg, false, true), kb = worldKb,
                zone = HitZone.Body, contact = contact, moveId = artId, window = -1,
            };
            var res = tgt.ReceiveHit(info);
            comboCount++;
            PublishHit(tgt, info, res);
            tgt.FlushGuardBreak();
            FlushGuardBreak();
            onHit?.Invoke(this, tgt, info, res);
        }

        /// <summary>Kage stands at (<paramref name="x"/>, <paramref name="y"/>), clamped to the stage, still; facing <paramref name="face"/> when it isn't 0.</summary>
        void ArtPlace(float x, float y, float face = 0f)
        {
            x = Mathf.Clamp(x, StageMin, StageMax);
            rb.position = new Vector2(x, Mathf.Max(0.02f, y));
            transform.position = new Vector3(x, Mathf.Max(0.02f, y), 0f);
            rb.linearVelocity = Vector2.zero;
            if (face != 0f) SetFacing(face, true);
        }

        // =====================================================================================
        //  THE ART BASE
        // =====================================================================================
        /// <summary>
        /// A cinematic rage art (nested in the fighter, so it drives Kage's body, rig and blows like his own code). Frames are the art's
        /// own, counted on Kage's ticks (hit-stop and slow motion don't count them).
        /// </summary>
        abstract class CinematicArt
        {
            public Fighter k, tgt;
            public abstract string Id { get; }
            public RageArtLook Look => RageArtLook.Of(Id);
            /// <summary>Frames in all; frames of the close-in camera (the anticipation); frames of the cut-in band.</summary>
            public int total = 120, closeIn = 30, cutIn = 42;
            /// <summary>The camera's orthographic size close in and through the exchange (Crimson Moon's are 1.7 and 2.2).</summary>
            public float closeSize = 1.7f, exchangeSize = 2.3f;
            /// <summary>The finisher has landed: the world runs again.</summary>
            public bool finished;
            /// <summary>The blade (and the twin's second blade) leaves its trail on this frame.</summary>
            public bool trail;

            public virtual void Init() { }
            public abstract void Start();
            public abstract void Tick(int f);
            public virtual void Stop() { }

            public virtual float CamSize(int f) => f < closeIn ? closeSize : exchangeSize;
            public virtual float CamX(int f) => f < closeIn ? k.Pos.x + k.facing * 0.35f : Live ? (k.Pos.x + tgt.Pos.x) * 0.5f : k.Pos.x;
            public virtual float CamY(int f) => CamSize(f) * 0.64f;

            protected bool Live => CmLive(tgt);
            /// <summary>The target's chest, or a point in front of Kage when there is no target left.</summary>
            protected Vector2 Chest => Live ? tgt.Pos + Vector2.up * 1.05f : k.Pos + new Vector2(k.facing * 1.2f, 1.05f);
            protected float AnchorX => Live ? tgt.Pos.x : k.Pos.x + k.facing * 1.4f;
            /// <summary>+1 when the target stands to Kage's right.</summary>
            protected float SideOfTarget => Live && tgt.Pos.x != k.Pos.x ? Mathf.Sign(tgt.Pos.x - k.Pos.x) : k.facing;

            /// <summary>Holds the target where it stands (bound): hit-stun, no matter what knocked it before.</summary>
            protected void Bind(int frames)
            {
                if (Live && tgt.state != FState.Launched && tgt.state != FState.Down) tgt.Stagger(frames);
            }

            /// <summary>
            /// An unblockable blow of the art on the target: <paramref name="kbAway"/> pushes it away from Kage (x) and up (y). While the world
            /// is held the target stays where it stands unless <paramref name="keepMotion"/>.
            /// </summary>
            protected void Blow(int dmg, Vector2 kbAway, int stun = 60, bool knockdown = false, bool heavy = false, int hitstop = 3, float shake = 0.15f, bool keepMotion = false)
            {
                if (!Live) return;
                if (knockdown && tgt.state == FState.Down) tgt.Stagger(1);       // a foe lying down stands to be struck, so the blow throws it
                var w = new HitWindow
                {
                    dmg = dmg, level = HitLevel.Unblockable, kb = kbAway, stun = stun, knockdown = knockdown, heavy = heavy, sword = true,
                    hitstop = hitstop, shake = shake, radius = 0.1f,
                };
                float away = Mathf.Sign(tgt.Pos.x - k.Pos.x);
                if (away == 0f) away = k.facing;
                k.ArtBlow(tgt, w, new Vector2(kbAway.x * away, kbAway.y), Id, Chest);
                if (k._cmFrozen)
                {
                    if (!keepMotion && tgt.rb != null) tgt.rb.linearVelocity = Vector2.zero;
                    // a held foe doesn't tick, so the hit's white flash would never fade: it keeps a faint tint of the art instead
                    tgt.rig?.SetFlash(Look.col, 0.28f, 12f);
                }
            }

            /// <summary>An unblockable blow on every other live foe within <paramref name="range"/> metres of <paramref name="x"/> (the floor arts).</summary>
            protected void BlowAround(float x, float range, int dmg, Vector2 kbAway, bool knockdown, bool includeTarget, int stun = 40)
            {
                if (k.ctx == null) return;
                var list = k.ctx.Fighters;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null || f == k || f.team == k.team || !CmLive(f) || (!includeTarget && f == tgt)) continue;
                    if (Mathf.Abs(f.Pos.x - x) > range) continue;
                    if (knockdown && f.state == FState.Down) f.Stagger(1);
                    var w = new HitWindow { dmg = dmg, level = HitLevel.Unblockable, kb = kbAway, stun = stun, knockdown = knockdown, heavy = true, sword = true, hitstop = 4, shake = 0.2f, radius = 0.12f };
                    float away = Mathf.Sign(f.Pos.x - x);
                    if (away == 0f) away = k.facing;
                    k.ArtBlow(f, w, new Vector2(kbAway.x * away, kbAway.y), Id, f.Pos + Vector2.up * 0.9f);
                }
            }

            /// <summary>
            /// The finisher's screen work at <paramref name="at"/>: the art's word, an impact flash in its colour, hit-stop, slow motion, shake and
            /// punch; then the world runs again.
            /// </summary>
            protected void Finish(Vector2 at, int hitstop = 7, float slow = 0.3f, float slowSeconds = 0.45f, float shake = 0.9f, float punch = 0.12f)
            {
                finished = true;
                var look = Look;
                k.ArtHud?.ArtStamp(look.stamp, look.kanji, RageArtCues.FormatStampCaption(look, k.WeaponId), look.col, look.hot, 1.25f, look.legendary, look.deep);
                GameFeel.Flash(look.hot.WithA(0.22f), 0.14f);
                if (look.legendary) { Vfx.I?.PrismRing(at, 0.2f, 1.9f, 0.1f, 0.55f); Vfx.I?.PrismSparks(at, 18, 1.4f); }
                Vfx.I?.Ring(at, 0.2f, 1.5f, 0.12f, look.col, 0.42f, true, 1f, 2.2f);
                Vfx.I?.Ring(at, 0.1f, 0.9f, 0.07f, look.hot, 0.3f, true, 1f, 1.8f);
                GameFeel.Hitstop(hitstop);
                GameFeel.SlowMo(slow, slowSeconds);
                GameFeel.Shake(shake);
                GameFeel.Punch(punch);
                KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 1f);
                KageGame.I?.Audio?.Play(SfxKind.Gong, 0.6f);
                k.ReleaseRageArtWorld();
            }

            // ---- the pose track: real key frames of the family's moves, played at the art's beats ----
            struct Seg { public int at, len; public MoveDef move; public float from, to; public Pose pose; public Ease ease; }
            readonly List<Seg> _track = new();
            int _seg = -1;
            readonly Pose _segStart = new(), _tmp = new();

            /// <summary>From art frame <paramref name="at"/>, play <paramref name="m"/>'s frames <paramref name="from"/>..<paramref name="to"/> over <paramref name="len"/> art frames.</summary>
            protected void Play(int at, MoveDef m, float from, float to, int len)
            {
                if (m == null) { Hold(at, Stance(), 6); return; }
                _track.Add(new Seg { at = at, len = Mathf.Max(1, len), move = m, from = from, to = to });
            }

            /// <summary>From art frame <paramref name="at"/>, blend to <paramref name="p"/> over <paramref name="blend"/> frames and hold it.</summary>
            protected void Hold(int at, Pose p, int blend, Ease e = Ease.OutQuad) =>
                _track.Add(new Seg { at = at, len = Mathf.Max(1, blend), pose = p, ease = e });

            /// <summary>The pose at (fractional) art frame <paramref name="f"/> into Kage's display pose.</summary>
            public void PoseOutput(float f)
            {
                var d = k._display;
                int idx = -1;
                for (int i = 0; i < _track.Count; i++) if (_track[i].at <= f) idx = i;
                if (idx < 0) { Pose.Blend(k.def, k._start, k._start, 0f, d); return; }
                if (idx != _seg) { _segStart.CopyFrom(d); _seg = idx; }
                var s = _track[idx];
                float u = Mathf.Clamp01((f - s.at) / s.len);
                if (s.move != null)
                {
                    s.move.Evaluate(k.def, Mathf.Lerp(s.from, s.to, u), _segStart, _tmp);
                    float inK = Mathf.Clamp01((f - s.at) / Mathf.Min(3f, s.len));         // a short blend from the last segment
                    Pose.Blend(k.def, _segStart, _tmp, KMath.Apply(Ease.OutQuad, inK), d);
                }
                else Pose.Blend(k.def, _segStart, s.pose, KMath.Apply(s.ease, u), d);
            }
        }
    }
}
