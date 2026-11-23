using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The 3D side of the Armory (SAN-90): the camera and Kage, live and breathing, wearing the gear the screen has selected. On the
    /// Weapons and Helmets tabs one Kage stands in front of the sun (the helmets tab frames his head); on the Armor tab a row of
    /// Kages, one per armour card, each in his guard (JS startArmorCards). Each is built through <see cref="FightBuilder"/> from
    /// <see cref="KageCharacter.Def(SaveData)"/> of a preview save, so he wears exactly what the fights would put on him (the SAN-72
    /// helmet and SAN-85 armour layers, the weapon family's blade).
    /// The screen's backdrop is a camera-space canvas (<see cref="BackRoot"/>) that sorts behind the fighters; the foreground
    /// panels are the flow's overlay canvas, so the fighters stand between the two.
    /// </summary>
    public sealed class ArmoryWorld : MonoBehaviour
    {
        /// <summary>The preview of a selection waits this long after the last change before it is built (arrow keys repeat fast).</summary>
        public const float Debounce = 0.12f;

        /// <summary>Where a fighter stands on the stage, in 1600 x 900 stage px (feet), for the framing of each tab.</summary>
        public struct Frame
        {
            public float size, cx, cy;
        }

        public RectTransform BackRoot { get; private set; }

        /// <summary>The fighters on show (one, or one per armour card), in card order. Null entries are never kept.</summary>
        public IReadOnlyList<Fighter> Fighters { get { return fighters; } }

        /// <summary>What the fighters were built from; changes when a rebuild is due.</summary>
        public string Signature { get; private set; }

        /// <summary>Times fighters were built, for tests.</summary>
        public int Builds { get; private set; }

        Camera cam;
        CameraRig rig;
        bool rigWas;
        GameObject studio;
        readonly List<Fighter> fighters = new List<Fighter>();
        readonly List<Transform> shadows = new List<Transform>();
        string pendingSig;
        System.Action pendingBuild;
        float pendingAt;
        Frame frame, target;
        float aspectOverride;
        bool snap = true;
        // which of the row's fighters stand inside the cards' window (null: all of them); the others are hidden
        List<bool> rowShown;

        public static ArmoryWorld Create(Transform parent)
        {
            var go = new GameObject("ArmoryWorld");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<ArmoryWorld>();
            w.Build();
            return w;
        }

        void Build()
        {
            Time.timeScale = 1f;
            transform.position = Vector3.zero;
            BuildCamera();

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            vfxGo.AddComponent<Vfx>();

            // the backdrop: a canvas the camera draws behind every fighter and shadow
            var cgo = new GameObject("Backdrop", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 20f;
            canvas.sortingOrder = -5000;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var stage = new GameObject("Stage", typeof(RectTransform));
            BackRoot = (RectTransform)stage.transform;
            BackRoot.SetParent(cgo.transform, false);
            BackRoot.anchorMin = BackRoot.anchorMax = new Vector2(0.5f, 0.5f);
            BackRoot.pivot = new Vector2(0.5f, 0.5f);
            BackRoot.sizeDelta = new Vector2(1600, 900);
            BackRoot.anchoredPosition = Vector2.zero;
            foreach (var t in cgo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        }

        void BuildCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 2.3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Sky3;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.cullingMask = ~(1 << 5);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            // a stage world left its follow camera on the camera: it must not move it while this screen frames the fighters
            rig = cam.GetComponent<CameraRig>();
            if (rig != null) { rigWas = rig.enabled; rig.enabled = false; }
            cam.transform.rotation = Quaternion.identity;
        }

        // ---------------- what stands on the stage ----------------

        /// <summary>
        /// Puts one Kage on the stage, built from <paramref name="save"/>'s loadout. A change of loadout builds him again after a short
        /// pause; the same loadout keeps the fighter he is.
        /// </summary>
        public void ShowSingle(SaveData save, bool head)
        {
            rowShown = null;
            string sig = (head ? "head|" : "body|") + KageCharacter.LoadoutKey(save);
            SetFrame(head ? HeadFrame : BodyFrame);
            if (sig == Signature || sig == pendingSig) return;
            pendingSig = sig;
            pendingBuild = () => BuildFighters(new List<SaveData> { save }, new List<float> { SingleX(head) }, sig);
            pendingAt = Signature == null ? 0f : Time.unscaledTime + Debounce;
            if (Signature == null) Flush();
        }

        /// <summary>Nothing stands on the stage: the Weapons tab shows the weapon's illustration, not a fighter.</summary>
        public void ShowNone()
        {
            rowShown = null;
            SetFrame(BodyFrame);
            pendingBuild = null;
            pendingSig = null;
            if (Signature != null || fighters.Count > 0) Clear();
        }

        /// <summary>
        /// Puts a row of Kages on the stage, one per save, centred at the given stage px (their feet on the card baseline).
        /// <paramref name="shown"/> says which of them stand in a card inside the row's window: the fighters are in the world, not
        /// in the clipped card row, so one whose card is scrolled out of the window is hidden rather than left standing past its edge.
        /// </summary>
        public void ShowRow(IList<SaveData> saves, IList<float> stageX, string key, float panPx = 0f, IList<bool> shown = null)
        {
            rowShown = shown != null ? new List<bool>(shown) : null;
            string sig = "row|" + key;
            // a scrolled row: the camera pans along it by as many stage px as the cards moved
            var frameRow = RowFrame;
            frameRow.cx += panPx * K(RowFrame);
            SetFrame(frameRow);
            if (sig == Signature || sig == pendingSig) return;
            pendingSig = sig;
            var list = new List<SaveData>(saves);
            var xs = new List<float>();
            foreach (float px in stageX) xs.Add(StageToWorldX(px));
            pendingBuild = () => BuildFighters(list, xs, sig);
            pendingAt = 0f;
            Flush();
        }

        void Flush()
        {
            if (pendingBuild == null) return;
            var b = pendingBuild;
            pendingBuild = null;
            pendingSig = null;
            b();
        }

        void BuildFighters(List<SaveData> saves, List<float> xs, string sig)
        {
            Clear();
            studio = new GameObject("Studio");
            studio.transform.SetParent(transform, false);
            var fight = new FightBuilder(studio.transform, 7, manualClock: false);
            fight.AddGround();
            for (int i = 0; i < saves.Count; i++)
            {
                var f = fight.Spawn(KageCharacter.Def(saves[i]), 0, new ScriptedInput());
                fighters.Add(f);
            }
            var ctx = fight.Build();
            ctx.Begin();
            for (int i = 0; i < fighters.Count; i++)
            {
                fighters[i].ResetForRound(xs[i], 1f);
                fighters[i].BeginFight();
                shadows.Add(MakeShadow(studio.transform));
            }
            Signature = sig;
            Builds++;
        }

        /// <summary>True when the fighter at this index is on show (a row fighter whose card is outside the window is not).</summary>
        public bool IsShown(int i) { return rowShown == null || i >= rowShown.Count || rowShown[i]; }

        Transform MakeShadow(Transform parent)
        {
            var t = new GameObject("KageShadow").transform;
            t.SetParent(parent, false);
            MeshKit.MakeRenderer("Contact", t, MeshKit.Disc(Vector2.zero, 0.55f, new Color(0f, 0f, 0f, 0.75f), new Color(0f, 0f, 0f, 0f), 32, 1f, 0.12f), MeshKit.Alpha, -900);
            return t;
        }

        void Clear()
        {
            fighters.Clear();
            shadows.Clear();
            Signature = null;
            // immediately: the next studio's physics must never see this one's bodies
            if (studio != null) DestroyImmediate(studio);
            studio = null;
        }

        // ---------------- framing ----------------
        // The camera maps the 1600 x 900 stage to the world: k metres per stage px, the stage point (px, py) is the world point
        // ((px - 800) k + cx, (450 - py) k + cy) with k = 2 size / 900.

        // weapons: Kage whole, his feet low on the sun's left (JS: the ghost warrior at 0.42 of the 1000 px preview)
        static readonly Frame BodyFrame = new Frame { size = 1.55f, cx = 1.343f, cy = 0.964f };
        // helmets: his head and shoulders, big, the sun behind (JS: the figure at 1.5 x the preview's height)
        static readonly Frame HeadFrame = new Frame { size = 0.87f, cx = 0.578f, cy = 1.52f };
        // armour cards: a row of whole figures, feet on the card baseline
        static readonly Frame RowFrame = new Frame { size = 2.94f, cx = 0f, cy = 0.235f };

        static float SingleX(bool head) { return 0f; }

        float K(Frame f) { return 2f * f.size / 900f; }

        float StageToWorldX(float px)
        {
            return (px - 800f) * K(RowFrame) + RowFrame.cx;
        }

        void SetFrame(Frame f)
        {
            if (target.size == f.size && target.cx == f.cx && target.cy == f.cy) return;
            // only a pan (a scrolled row) follows at once, so the fighters stay in their cards while the cards move
            bool onlyPan = target.size > 0f && Mathf.Approximately(target.size, f.size) && Mathf.Approximately(target.cy, f.cy);
            target = f;
            if (Signature == null || onlyPan) snap = true;
        }

        void Apply(Frame f)
        {
            // the stage is 1600 x 900 inside whatever the screen is: a narrower screen shrinks it to the width, so the world follows
            float aspect = aspectOverride > 0f ? aspectOverride : cam.aspect;
            float widen = Mathf.Max(1f, (16f / 9f) / Mathf.Max(0.3f, aspect));
            cam.orthographicSize = f.size * widen;
            // a taller screen shows more above and below: keep the stage's centre where it is
            cam.transform.position = new Vector3(f.cx, f.cy, -10f);
        }

        /// <summary>Frames the stage as if the screen had this aspect ratio (review screenshots render 16:9 whatever the window is).</summary>
        public void Reframe(float aspect)
        {
            aspectOverride = aspect;
            if (cam != null) Apply(frame.size > 0f ? frame : target);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (pendingBuild != null && Time.unscaledTime >= pendingAt) Flush();
            float dt = Time.unscaledDeltaTime;
            if (snap || frame.size <= 0f) { frame = target; snap = false; }
            else
            {
                frame.size = KMath.Damp(frame.size, target.size, 10f, dt);
                frame.cx = KMath.Damp(frame.cx, target.cx, 10f, dt);
                frame.cy = KMath.Damp(frame.cy, target.cy, 10f, dt);
            }
            Apply(frame);
            for (int i = 0; i < fighters.Count && i < shadows.Count; i++)
            {
                if (fighters[i] == null || shadows[i] == null) continue;
                bool shown = IsShown(i);
                if (shadows[i].gameObject.activeSelf != shown)
                {
                    shadows[i].gameObject.SetActive(shown);
                    fighters[i].rig.SetVisible(shown);
                }
                Vector2 hips = fighters[i].rig.Bone(B.Hips).position;
                shadows[i].position = new Vector3(hips.x, 0.01f, 0f);
            }
        }

        void OnDestroy()
        {
            Clear();
            if (rig != null) rig.enabled = rigWas;
        }
    }
}
