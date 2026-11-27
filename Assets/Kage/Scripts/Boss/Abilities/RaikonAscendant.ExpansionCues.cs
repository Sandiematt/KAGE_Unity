using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The expansion's effect cues (ADR-0007; docs/raikon-ascendant-vfx.md, "Effect briefs" and "VFX implementation notes"): each one is a brief drawn
    /// with <see cref="Vfx"/>'s primitives (Vfx.Ascendant.cs, Vfx.Lightning.cs, Vfx.Thunder.cs). Sounds are <see cref="KageAudio"/>'s own kinds.
    /// </summary>
    public static partial class RaikonAscendant
    {
        // ---- tells (the same three colours: a glint at the scabbard's mouth is steel; an eye in the storm is the eye) ----
        public const string SteelSayaCue = "VFX_Raikon_Tell_Steel_Saya", EyeCloudCue = "VFX_Raikon_Tell_Eye_Cloud";
        // ---- phase 1: steel, air shear, dust, embers ----
        public const string BackstepDustCue = "VFX_Raikon_Backstep_Dust", DrawArcCue = "VFX_Raikon_Draw_Arc", DrawArcReverseCue = "VFX_Raikon_Draw_Arc_Reverse";
        public const string AirConeCue = "VFX_Raikon_Air_Cone", RingSparkCue = "VFX_Raikon_Ring_Spark", WindSplitCue = "VFX_Raikon_Wind_Split";
        public const string FeintSmearCue = "VFX_Raikon_Feint_Smear", SkidCue = "VFX_Raikon_Skid", NeedleCue = "VFX_Raikon_Needle_Flash";
        // ---- phase 2: static, ground lightning, tethers, wind blades ----
        public const string StaticCrossMarkCue = "VFX_Raikon_Static_Cross_Mark", StaticCrossSnapCue = "VFX_Raikon_Static_Cross";
        public const string VeeMarkCue = "VFX_Raikon_Cleave_Vee_Mark", VeeStrikeCue = "VFX_Raikon_Cleave_Vee";
        public const string RoosterCue = "VFX_Raikon_Rooster_Tail", NeedleFanCue = "VFX_Raikon_Needle_Fan";
        public const string GougeMarkCue = "VFX_Raikon_Gouge_Pillar_Mark", GougePillarCue = "VFX_Raikon_Gouge_Pillar";
        public const string AnchorFlightCue = "VFX_Raikon_Anchor_Blade", AnchorLandCue = "VFX_Raikon_Anchor_Land", AnchorArriveCue = "VFX_Raikon_Anchor_Arrive";
        public const string AnchorRecallCue = "VFX_Raikon_Anchor_Recall", RadialSlashCue = "VFX_Raikon_Radial_Slash";
        public const string VacuumBladeCue = "VFX_Raikon_Vacuum_Blade", LowCrescentCue = "VFX_Raikon_Low_Crescent", XCrescentCue = "VFX_Raikon_X_Crescent";
        public const string CrescentEndCue = "VFX_Raikon_Crescent_End", ResidueCue = "VFX_Raikon_Static_Residue";
        // ---- phase 3: the storm called down ----
        public const string HiltSlamCue = "VFX_Raikon_Hilt_Slam", CageCloudsCue = "VFX_Raikon_Cage_Clouds", CageCue = "VFX_Raikon_Cage";
        public const string CageSliceCue = "VFX_Raikon_Cage_Slice", CageShatterCue = "VFX_Raikon_Cage_Shatter";
        public const string ZenithColumnCue = "VFX_Raikon_Zenith_Column", ZenithRingCue = "VFX_Raikon_Zenith_Ring", MeteorCue = "VFX_Raikon_Zenith_Meteor";
        public const string DischargeCue = "VFX_Raikon_Zenith_Discharge";
        public const string MirageSplitCue = "VFX_Raikon_Mirage_Split", MirageMarkCue = "VFX_Raikon_Mirage_Mark", MirageBurstCue = "VFX_Raikon_Mirage_Burst";

        /// <summary>Every cue id the expansion registers (a property: <see cref="CueIds"/> joins it in its own initializer, in another part of the class).</summary>
        public static string[] ExpansionCueIds => new[]
        {
            SteelSayaCue, EyeCloudCue, BackstepDustCue, DrawArcCue, DrawArcReverseCue, AirConeCue, RingSparkCue, WindSplitCue, FeintSmearCue, SkidCue, NeedleCue,
            StaticCrossMarkCue, StaticCrossSnapCue, VeeMarkCue, VeeStrikeCue, RoosterCue, NeedleFanCue, GougeMarkCue, GougePillarCue, AnchorFlightCue,
            AnchorLandCue, AnchorArriveCue, AnchorRecallCue, RadialSlashCue, VacuumBladeCue, LowCrescentCue, XCrescentCue, CrescentEndCue, ResidueCue,
            HiltSlamCue, CageCloudsCue, CageCue, CageSliceCue, CageShatterCue, ZenithColumnCue, ZenithRingCue, MeteorCue, DischargeCue, MirageSplitCue,
            MirageMarkCue, MirageBurstCue,
        };

        static void RegisterExpansionCues()
        {
            // tells
            EffectCues.Register(SteelSayaCue, (Vector2 p, in CueArgs a) => V?.SteelGlint(SayaMouth(a, p), 0.34f));
            EffectCues.Register(EyeCloudCue, PlayEyeCloud);
            // phase 1
            EffectCues.Register(BackstepDustCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.BackstepDust(a.owner.Pos, a.owner.facing); });
            EffectCues.Register(DrawArcCue, (Vector2 p, in CueArgs a) => PlayDrawArc(a, false));
            EffectCues.Register(DrawArcReverseCue, (Vector2 p, in CueArgs a) => PlayDrawArc(a, true));
            EffectCues.Register(AirConeCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.AirCone(Bone(a, B.HandF, p) + Vector2.right * a.owner.facing * 0.1f, a.owner.facing); });
            EffectCues.Register(RingSparkCue, (Vector2 p, in CueArgs a) => { if (a.owner == null) return; V?.RingSpark(Bone(a, B.HandF, p), a.owner.facing); Sfx(SfxKind.Clang, 0.45f); });
            EffectCues.Register(WindSplitCue, PlayWindSplit);
            EffectCues.Register(FeintSmearCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.FeintSmear(Chest(a, p) + Vector2.up * 0.15f, a.owner.facing); });
            EffectCues.Register(SkidCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.Skid(a.owner.Pos, a.owner.facing); });
            EffectCues.Register(NeedleCue, (Vector2 p, in CueArgs a) => { if (a.owner == null) return; V?.NeedleFlash(Tip(a, p, 0.9f), a.owner.facing); Sfx(SfxKind.Slash, 0.4f); });
            // phase 2
            EffectCues.Register(StaticCrossMarkCue, (Vector2 p, in CueArgs a) => V?.StaticCrossMark(p + Vector2.up * StaticCrossHeight, a.seconds));
            EffectCues.Register(StaticCrossSnapCue, (Vector2 p, in CueArgs a) => { V?.StaticCrossSnap(p + Vector2.up * StaticCrossHeight); Sfx(SfxKind.Slash, 0.35f); });
            EffectCues.Register(VeeMarkCue, (Vector2 p, in CueArgs a) => { float f = Face(a); V?.VeeMark(p - Vector2.right * f * a.size.x * 0.5f, f, a.size.x, a.seconds); });
            EffectCues.Register(VeeStrikeCue, (Vector2 p, in CueArgs a) => { float f = Face(a); V?.VeeStrike(p - Vector2.right * f * a.size.x * 0.5f, f, a.size.x); });
            EffectCues.Register(RoosterCue, PlayRooster);
            EffectCues.Register(NeedleFanCue, (Vector2 p, in CueArgs a) => V?.NeedleFan(Tip(a, p, 1f), Face(a)));
            EffectCues.Register(GougeMarkCue, (Vector2 p, in CueArgs a) => V?.SparkLineMark(p - Vector2.right * 0.45f, p + Vector2.right * 0.45f, a.seconds));
            EffectCues.Register(GougePillarCue, (Vector2 p, in CueArgs a) => { V?.GougePillar(p); Sfx(SfxKind.HitHeavy, 0.3f); });
            EffectCues.Register(AnchorFlightCue, PlayAnchorFlight);
            EffectCues.Register(AnchorLandCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.AnchorLand(p, a.owner.Pos.y, Face(a)); });
            EffectCues.Register(AnchorArriveCue, PlayAnchorArrive);
            EffectCues.Register(AnchorRecallCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.AnchorRecall(StateOf(a.owner).anchorAt, Bone(a, B.HandF, p)); });
            EffectCues.Register(RadialSlashCue, (Vector2 p, in CueArgs a) => { V?.RadialSlash(p); Sfx(SfxKind.Slash, 0.5f); });
            EffectCues.Register(VacuumBladeCue, (Vector2 p, in CueArgs a) => PlayCrescent(p, a, Vfx.CrescentKind.Vacuum, true));
            EffectCues.Register(LowCrescentCue, (Vector2 p, in CueArgs a) => PlayCrescent(p, a, Vfx.CrescentKind.Low, false));
            EffectCues.Register(XCrescentCue, (Vector2 p, in CueArgs a) => PlayCrescent(p, a, Vfx.CrescentKind.X, true));
            EffectCues.Register(CrescentEndCue, (Vector2 p, in CueArgs a) => V?.CrescentEnd(p));
            EffectCues.Register(ResidueCue, PlayResidue);
            // phase 3
            EffectCues.Register(HiltSlamCue, (Vector2 p, in CueArgs a) => { if (a.owner == null) return; V?.HiltSlam(new Vector2(Tip(a, p, 1f).x, a.owner.Pos.y)); Sfx(SfxKind.Thud, 0.5f); });
            EffectCues.Register(CageCloudsCue, PlayCageClouds);
            EffectCues.Register(CageCue, PlayCage);
            EffectCues.Register(CageSliceCue, PlayCageSlice);
            EffectCues.Register(CageShatterCue, (Vector2 p, in CueArgs a) => { if (a.owner == null) return; V?.CageShatter(CageFeet(a.owner)); Sfx(SfxKind.Clang, 0.4f); });
            EffectCues.Register(ZenithColumnCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) V?.ZenithColumn(a.owner.rig, a.owner.Pos, a.owner); Sfx(SfxKind.Whoosh, 0.45f); });
            EffectCues.Register(ZenithRingCue, (Vector2 p, in CueArgs a) => RaikonZenithRing.Play(a.owner, ZenithRingSeconds));
            EffectCues.Register(MeteorCue, PlayMeteor);
            EffectCues.Register(DischargeCue, (Vector2 p, in CueArgs a) => { if (a.owner == null) return; V?.Discharge(a.owner.Pos); Sfx(SfxKind.HitHeavy, 0.45f); });
            EffectCues.Register(MirageSplitCue, PlayMirageSplit);
            EffectCues.Register(MirageMarkCue, (Vector2 p, in CueArgs a) => { float f = Face(a); V?.MirageMark(p + new Vector2(-f * 0.6f, 0.35f), p + new Vector2(f * 0.6f, 1.5f), a.seconds); });
            EffectCues.Register(MirageBurstCue, (Vector2 p, in CueArgs a) => { V?.MirageBurst(p); Sfx(SfxKind.Slash, 0.35f); });
        }

        static void Sfx(SfxKind k, float vol) => KageGame.I?.Audio?.Play(k, vol);

        static float Face(in CueArgs a) => a.owner != null ? a.owner.facing : a.dir.x < 0f ? -1f : 1f;

        /// <summary>The scabbard's mouth: just in front of his hips, where the sheathed blade's hilt rests.</summary>
        static Vector2 SayaMouth(in CueArgs a, Vector2 fallback) =>
            a.owner != null ? a.owner.Pos + new Vector2(a.owner.facing * 0.18f, 0.92f) : fallback;

        // ---------------------------------------------------------------------------------------------- phase 1
        /// <summary>Sheathe Cross's draws: two pale arcs whose middles meet at the X (where the phase 2 Static Cross stands): a rising draw, then the reverse.</summary>
        static void PlayDrawArc(in CueArgs a, bool reverse)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            float f = a.owner.facing;
            Vector2 feet = a.owner.Pos, x = feet + new Vector2(f * StaticCrossAhead, StaticCrossHeight);
            Vector2 from = reverse ? feet + new Vector2(f * 2.0f, 0.6f) : feet + new Vector2(f * -0.15f, 0.55f);
            Vector2 to = reverse ? feet + new Vector2(f * -0.1f, 1.6f) : feet + new Vector2(f * 2.0f, 1.45f);
            v.DrawArc(from, 2f * x - (from + to) * 0.5f, to);        // a quadratic through x at its middle
            Sfx(SfxKind.SwordWhoosh, 0.4f);
        }

        /// <summary>The cleave's wind-split, on the line the blade comes down, a metre in front of him.</summary>
        static void PlayWindSplit(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            float x = a.owner.Pos.x + a.owner.facing * 1.0f, y = a.owner.Pos.y;
            v.WindSplit(new Vector2(x, y + 2.5f), new Vector2(x, y));
        }

        // ---------------------------------------------------------------------------------------------- phase 2
        static readonly Dictionary<Fighter, (float x, float time, int n)> Rooster = new();

        /// <summary>One step of the drag: the gouge runs on from where the last step left it (a new drag starts just behind the tip).</summary>
        static void PlayRooster(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            float face = a.owner.facing;
            Vector2 tip = Tip(a, p, 1f);
            if (!Rooster.TryGetValue(a.owner, out var r) || Time.time - r.time > 0.12f || Mathf.Abs(tip.x - r.x) > 1.5f) r = (tip.x - face * 0.35f, Time.time, 0);
            if (Rooster.Count > 8) Rooster.Clear();
            v.RoosterTail(tip, a.owner.Pos.y, face, r.x, r.n % 3 == 2);
            Rooster[a.owner] = (tip.x, Time.time, r.n + 1);
            if (r.n == 0) Sfx(SfxKind.SwordWhoosh, 0.35f);
        }

        /// <summary>The thrown tachi and its tether: they last while he is still in the throw (to the blink) or the recall's first frames.</summary>
        static void PlayAnchorFlight(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            var owner = a.owner;
            v.AnchorBlade(owner, a.follow, p, Face(a), owner.Pos.y, () =>
                owner.state == FState.Attack && ((owner.move == ArcAnchor && owner.moveFrame < AnchorBlinkFrame) || (owner.move == ArcAnchorRecall && owner.moveFrame < 10)));
            Sfx(SfxKind.Crescent, 0.45f);
        }

        /// <summary>He arrived at the hilt: ghosts strung back along the tether to where he stood.</summary>
        static void PlayAnchorArrive(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null || a.owner.move != ArcAnchor) return;
            v.TetherGhosts(a.owner.rig, StateOf(a.owner).blinkFrom, a.owner.Pos);
            Sfx(SfxKind.Whoosh, 0.45f);
        }

        /// <summary>A wind crescent in flight; the wide swings also blow faint streaks across the screen's edges.</summary>
        static void PlayCrescent(Vector2 p, in CueArgs a, Vfx.CrescentKind kind, bool screenWind)
        {
            var v = V;
            if (v == null) return;
            float face = Face(a), floor = a.owner != null ? a.owner.Pos.y : p.y - 1f;
            v.WindCrescent(a.follow, p, face, floor, kind);
            if (screenWind) v.ScreenWind(face, a.owner);
            Sfx(SfxKind.Crescent, 0.4f);
        }

        /// <summary>The X-crescent's residue along the floor, igniting from the near end as the crescent passes, for the residue's whole life.</summary>
        static void PlayResidue(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            float face = Face(a), half = a.size.x * 0.5f;
            v.StaticResidue(p.x - face * half, p.x + face * half, p.y, ResidueFrames / 60f, XCrescent != null ? XCrescent.speed : 14f);
        }

        // ---------------------------------------------------------------------------------------------- phase 3
        /// <summary>Tempest Cage's four spear feet on the ring's floor ellipse: two near (a little wider, a little lower), two far, round the ring.</summary>
        public static Vector2[] CageFeet(Fighter owner)
        {
            var s = StateOf(owner);
            float c = s.cageCentre, y = owner != null ? owner.Pos.y : 0f;
            return new[]
            {
                new Vector2(c + 2.05f, y - 0.06f), new Vector2(c + 1.45f, y + 0.12f), new Vector2(c - 1.45f, y + 0.12f), new Vector2(c - 2.05f, y - 0.06f),
            };
        }

        /// <summary>Four small clouds gather over the spear points while the hilt slam calls them (f16 → the spears at f30).</summary>
        static void PlayCageClouds(Vector2 p, in CueArgs a)
        {
            if (V == null || a.owner == null) return;
            int power = RaikonStorm.PowerOf(a.owner);
            foreach (var foot in CageFeet(a.owner)) ThunderCloud.Gather(foot.x, 0.55f, 14 / 60f, power, null, a.owner, 1.2f);       // four small pinpoints, one over each spear
        }

        /// <summary>The spears fall out of their clouds onto the ring and stand until the final cross shatters them.</summary>
        static void PlayCage(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            int power = RaikonStorm.PowerOf(a.owner);
            var feet = CageFeet(a.owner);
            foreach (var foot in feet)
            {
                Vector2 top = ThunderCloud.StrikeOver(foot.x, foot.y, power, a.owner, 0.8f);
                v.Bolt(top, foot, 0.05f, 0.28f, Vfx.Crimson, 16, -1f, 1);
            }
            v.CageSpears(feet, new Vector2(StateOf(a.owner).cageCentre, a.owner.Pos.y), (64 - 30) / 60f);
            v.BackdropFlash(0.12f, 0.35f);
            GameFeel.Shake(0.4f);
            Sfx(SfxKind.HitHeavy, 0.5f);
        }

        /// <summary>A chord's slice across the ring: the first rises, the second falls, so the two cross.</summary>
        static void PlayCageSlice(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            var c = new Vector2(StateOf(a.owner).cageCentre, a.owner.Pos.y + 1.0f);
            v.CageSlice(c, CageChordAt + 0.25f, a.owner.moveFrame <= 52 ? 0.18f : -0.18f);
            Sfx(SfxKind.Slash, 0.45f);
        }

        /// <summary>Seconds the zenith's ring lives: from the vanish (f14) to the crash (f38).</summary>
        public const float ZenithRingSeconds = 24 / 60f;

        /// <summary>The eye in the storm: the red eye flash in the cloud over the zenith's ring (the crash at f38 is unguardable).</summary>
        static void PlayEyeCloud(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            var s = StateOf(a.owner);
            float x = s.zenithLocked || a.owner.opponent == null ? s.zenithX : a.owner.opponent.Pos.x;
            var cam = Vfx.ViewCam(a.owner);
            float y = cam != null ? Vfx.CloudBaseY(cam) - 0.35f : s.zenithGround + 4f;
            v.EyeFlash(new Vector2(x, y));
        }

        /// <summary>The meteor: out of the cloud over the lock, down onto it (he is back in sight on its line).</summary>
        static void PlayMeteor(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            var s = StateOf(a.owner);
            var ground = new Vector2(s.zenithX, s.zenithGround);
            Vector2 top = ThunderCloud.StrikeOver(s.zenithX, s.zenithGround, RaikonStorm.PowerOf(a.owner), a.owner, 2.2f);
            v.Meteor(top, ground);
            Sfx(SfxKind.Whoosh, 0.5f);
        }

        /// <summary>The split and the decoy: the decoy holds to f26, dashes in to Kage's near side in 10 frames (no damage), and breaks into ash.</summary>
        static void PlayMirageSplit(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null || a.owner.rig == null) return;
            float face = a.owner.facing;
            v.MirageSplit(a.owner.rig, a.owner.Pos, face);
            float to = a.owner.opponent != null ? a.owner.opponent.Pos.x - face * 0.9f : a.owner.Pos.x + face * 2f;
            float by = to - a.owner.Pos.x;
            if (Mathf.Sign(by) != face || Mathf.Abs(by) < 1f) by = face * 1f;
            v.MirageDecoy(a.owner.rig, 10 / 60f, new Vector2(by, 0f), 10 / 60f);
            Sfx(SfxKind.Whoosh, 0.4f);
        }
    }

    /// <summary>
    /// Zenith Guillotine's ring under Kage: it follows him along the floor until the lock (f30), then stays; it widens and pulses every 4 frames, with
    /// arcs crawling in it from halfway and sparks in the last fifth. A storm cloud gathers over it, riding it, out of which the meteor falls.
    /// </summary>
    public sealed class RaikonZenithRing : MonoBehaviour
    {
        Fighter _owner;
        float _t, _life, _pulse;

        public static void Play(Fighter owner, float seconds)
        {
            if (Vfx.I == null || owner == null) return;
            var go = new GameObject("RaikonZenithRing");
            go.transform.SetParent(Vfx.I.transform, false);
            var r = go.AddComponent<RaikonZenithRing>();
            r._owner = owner;
            r._life = Mathf.Max(0.1f, seconds);
            go.transform.position = r.Target();
            ThunderCloud.Gather(go.transform.position.x, 2.2f, r._life * 0.9f, RaikonStorm.PowerOf(owner), go.transform, owner);
        }

        Vector2 Target()
        {
            var s = RaikonAscendant.StateOf(_owner);
            if (s.zenithLocked || _owner == null || _owner.opponent == null) return new Vector2(s.zenithX, s.zenithGround);
            return new Vector2(_owner.opponent.Pos.x, s.zenithGround);
        }

        void Update()
        {
            var v = Vfx.I;
            _t += Time.deltaTime;
            if (v == null || _owner == null || _t >= _life) { Destroy(gameObject); return; }
            transform.position = Target();
            float u = _t / _life;
            _pulse -= Time.deltaTime;
            if (_pulse > 0f) return;
            _pulse = 0.067f;
            Vector2 p = transform.position;
            float r = Mathf.Lerp(0.5f, 1.25f, u);
            v.Ring(p + Vector2.up * 0.03f, r * 0.85f, r, 0.025f + 0.03f * u, Vfx.Crimson.WithA(0.3f + 0.55f * u), 0.12f, false, 0.22f, 1.3f + u);
            if (u > 0.5f) v.Crackle(p + Vector2.up * 0.04f, r, r * 0.2f, 1);
            if (u > 0.8f) v.Spark(p + new Vector2(Random.Range(-r, r), 0.04f), new Vector2(0f, Random.Range(1.5f, 3f)), Color.white, 0.022f, 0.2f);
        }
    }
}
