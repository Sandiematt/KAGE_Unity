using Kage.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// The Kage in the Profile card (JS openLoadout draws him in a canvas there): the same Kage the pre-fight screen shows, built the
    /// same way (<see cref="FightBuilder"/> from <see cref="KageCharacter.Def(SaveData)"/>, so he wears exactly what the fights put on
    /// him) and drawn the same way: a camera like the stage world's (SMAA, post-processing with the stage's bloom, vignette, contrast
    /// and grain), at the card's own size in screen pixels, so his edges are as clean as on the pre-fight screen. He stands in a studio
    /// far off the stage (nothing else is ever there) in front of the red sun, which is drawn in the studio too: the post-processing
    /// writes no alpha, so the picture is opaque. It lives while the card's root is on show and puts itself away after.
    /// The campaign map's sub-boss card draws its foe the same way (<see cref="EnsureFoe"/>), in a studio of its own.
    /// </summary>
    public sealed class ProfileKage : MonoBehaviour
    {
        /// <summary>Where the studio stands: far off every stage and the Armory, so no other camera sees him and no other body touches him.</summary>
        public const float StudioX = 5000f;
        /// <summary>The foe studio (<see cref="EnsureFoe"/>): further along, so a foe and Kage never share a frame or a body.</summary>
        public const float FoeStudioX = 5400f;
        /// <summary>Half the framed height, metres, and the framed centre's height: Kage whole, feet near the bottom, blade in frame.</summary>
        public const float HalfHeight = 1.45f, CentreY = 1.1f;
        /// <summary>The layer of the studio's post-processing volume: only this camera reads it, the main camera never does.</summary>
        public const int VolumeLayer = 30;

        public RenderTexture Texture { get; private set; }
        public Fighter Fighter { get; private set; }
        public string Signature { get; private set; }

        Camera cam;
        GameObject studio, backdrop;
        Transform shadow;
        RectTransform owner;
        float stageW, stageH;
        // where this studio stands and what its camera frames (a bigger foe is framed wider)
        float studioX = StudioX, halfHeight = HalfHeight, centreY = CentreY;
        bool framed;

        /// <summary>
        /// The Kage for <paramref name="save"/>, drawn for a box of <paramref name="w"/> x <paramref name="h"/> stage px, kept while
        /// <paramref name="ownerRoot"/> is on show; the same loadout keeps the fighter he is.
        /// </summary>
        public static ProfileKage Ensure(ref ProfileKage existing, Transform parent, RectTransform ownerRoot, SaveData save, float w, float h)
        {
            if (existing == null) existing = Create(parent, "ProfileKage", StudioX);
            existing.owner = ownerRoot;
            existing.stageW = w; existing.stageH = h;
            existing.Frame(HalfHeight, CentreY);
            existing.Resize();
            existing.Show(save);
            return existing;
        }

        /// <summary>
        /// The foe of a stage (<paramref name="enemyId"/>, a <see cref="CharacterCatalog"/> id) standing at ease and facing left, drawn
        /// for a box of <paramref name="w"/> x <paramref name="h"/> stage px while <paramref name="ownerRoot"/> is on show; null when the
        /// id is not a character. A bigger foe is framed wider, so he stands whole in the box.
        /// </summary>
        public static ProfileKage EnsureFoe(ref ProfileKage existing, Transform parent, RectTransform ownerRoot, string enemyId, float w, float h)
        {
            var def = CharacterCatalog.Get(enemyId);
            if (def == null) return null;
            if (existing == null) existing = Create(parent, "FoePortrait", FoeStudioX);
            existing.owner = ownerRoot;
            existing.stageW = w; existing.stageH = h;
            float s = Mathf.Max(1f, def.bodyScale);
            existing.Frame(HalfHeight * s, CentreY * s);
            existing.Resize();
            existing.Spawn("foe:" + enemyId, def, -1f);
            return existing;
        }

        static ProfileKage Create(Transform parent, string name, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var k = go.AddComponent<ProfileKage>();
            k.studioX = x;
            k.Init();
            return k;
        }

        /// <summary>Points the camera at the studio; a new framing redraws the backdrop to fit it.</summary>
        void Frame(float half, float centre)
        {
            if (framed && Mathf.Approximately(half, halfHeight) && Mathf.Approximately(centre, centreY)) return;
            framed = true;
            halfHeight = half; centreY = centre;
            cam.orthographicSize = halfHeight;
            cam.transform.position = new Vector3(studioX, centreY, -10f);
            if (Texture != null) BuildBackdrop();
        }

        void Init()
        {
            var cgo = new GameObject("ProfileKageCamera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Sky3;
            cam.cullingMask = ~(1 << 5);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.transform.position = new Vector3(studioX, centreY, -10f);
            // the stage world's camera: post-processing and SMAA (StageWorld.Begin)
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderShadows = false;
                data.volumeLayerMask = 1 << VolumeLayer;
                data.volumeTrigger = cam.transform;
            }
            BuildVolume();
        }

        /// <summary>The stage world's look (StageWorld.BuildVolume), on a layer only this camera reads.</summary>
        void BuildVolume()
        {
            var go = new GameObject("ProfilePostFX") { layer = VolumeLayer };
            go.transform.SetParent(transform, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.82f));
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);
            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(14f);
            ca.saturation.Override(6f);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            vol.sharedProfile = p;
        }

        /// <summary>The picture is as many pixels as the box covers on screen (the canvas scales the 1600 x 900 stage to fit): no resampling, no jagged edges.</summary>
        void Resize()
        {
            float scale = Mathf.Max(0.25f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            int w = Mathf.Clamp(Mathf.RoundToInt(stageW * scale), 16, 2048), h = Mathf.Clamp(Mathf.RoundToInt(stageH * scale), 16, 2048);
            if (Texture != null && Texture.width == w && Texture.height == h) return;
            if (Texture != null) { cam.targetTexture = null; Texture.Release(); Destroy(Texture); }
            Texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "ProfileKage", antiAliasing = 4, filterMode = FilterMode.Bilinear };
            Texture.Create();
            cam.targetTexture = Texture;
            cam.aspect = w / (float)h;
            BuildBackdrop();
        }

        /// <summary>The red sun behind him and the dark ground under him, in the studio, sized to the frame.</summary>
        void BuildBackdrop()
        {
            if (backdrop != null) Destroy(backdrop);
            backdrop = new GameObject("ProfileBackdrop");
            backdrop.transform.SetParent(transform, false);
            backdrop.transform.position = new Vector3(studioX, 0f, 0f);
            float hw = halfHeight * cam.aspect + 0.2f, y0 = centreY - halfHeight - 0.2f, y1 = centreY + halfHeight + 0.2f;
            var sky = new[] { new Vector2(-hw, 0f), new Vector2(hw, 0f), new Vector2(hw, y1), new Vector2(-hw, y1) };
            MeshKit.MakeRenderer("Sky", backdrop.transform, MeshKit.PolyGradient(sky, Ui.Mix(Ui.Sun2, Ui.Sky3, 0.35f), Ui.Mix(Ui.Sun2, Ui.Sky3, 0.55f)), MeshKit.Alpha, -3000);
            var sun = MeshKit.MakeRenderer("Sun", backdrop.transform, MeshKit.Disc(Vector2.zero, 1.35f * halfHeight / HalfHeight, Ui.A(Ui.Acc, 0.62f), Ui.A(Ui.Acc, 0f), 48), MeshKit.Alpha, -2990);
            sun.transform.localPosition = new Vector3(0f, centreY + 0.12f, 0f);
            var ground = new[] { new Vector2(-hw, y0), new Vector2(hw, y0), new Vector2(hw, 0f), new Vector2(-hw, 0f) };
            MeshKit.MakeRenderer("Ground", backdrop.transform, MeshKit.PolyGradient(ground, Ui.Void, Ui.Ground), MeshKit.Alpha, -2980);
            var line = new[] { new Vector2(-hw, -0.006f), new Vector2(hw, -0.006f), new Vector2(hw, 0.006f), new Vector2(-hw, 0.006f) };
            MeshKit.MakeRenderer("Horizon", backdrop.transform, MeshKit.Poly(line, Ui.A(Ui.Gold, 0.22f)), MeshKit.Alpha, -2970);
        }

        void Show(SaveData save)
        {
            string sig = KageCharacter.LoadoutKey(save);
            if (sig == Signature && Fighter != null) return;
            Spawn(sig, KageCharacter.Def(save), 1f);
        }

        /// <summary>Puts <paramref name="def"/> in the studio facing <paramref name="face"/> (1 right, -1 left); the same signature keeps him.</summary>
        void Spawn(string sig, CharacterDef def, float face)
        {
            if (sig == Signature && Fighter != null) return;
            Clear();
            studio = new GameObject("ProfileStudio");
            studio.transform.SetParent(transform, false);
            studio.transform.position = new Vector3(studioX, 0f, 0f);
            var fight = new FightBuilder(studio.transform, 7, manualClock: false);
            fight.AddGround();
            // the fight's bounds stand around the studio, not the stage's origin: otherwise he is clamped back to x = 7.6, off his ground
            fight.Context.Bounds.Set(studioX - StageBounds.DefaultHalf, studioX + StageBounds.DefaultHalf);
            Fighter = fight.Spawn(def, 0, new ScriptedInput());
            fight.Build().Begin();
            // a touch behind centre, so his scarf and cape fit behind him and his blade in front; a long blade (the cleaver, the
            // spear) needs more room ahead of him
            bool longBlade = def.family == WeaponFamilies.Cleaver || def.family == WeaponFamilies.Spear;
            Fighter.ResetForRound(studioX - face * (longBlade ? 0.3f : 0.12f), face);
            Fighter.BeginFight();
            shadow = new GameObject("KageShadow").transform;
            shadow.SetParent(studio.transform, false);
            MeshKit.MakeRenderer("Contact", shadow, MeshKit.Disc(Vector2.zero, 0.55f, new Color(0f, 0f, 0f, 0.75f), new Color(0f, 0f, 0f, 0f), 32, 1f, 0.12f), MeshKit.Alpha, -900);
            Signature = sig;
        }

        void LateUpdate()
        {
            // the card was closed (or rebuilt without him): put the studio away
            if (owner == null || !owner.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            if (Fighter != null && shadow != null)
            {
                var hips = Fighter.rig != null ? Fighter.rig.Bone(B.Hips) : null;
                if (hips != null) shadow.position = new Vector3(hips.position.x, 0.01f, 0f);
            }
        }

        void Clear()
        {
            Fighter = null;
            Signature = null;
            shadow = null;
            // immediately: the next studio's physics must never see this one's bodies
            if (studio != null) DestroyImmediate(studio);
            studio = null;
        }

        void OnDestroy()
        {
            Clear();
            if (cam != null) cam.targetTexture = null;
            if (Texture != null) { Texture.Release(); Destroy(Texture); Texture = null; }
        }
    }
}
