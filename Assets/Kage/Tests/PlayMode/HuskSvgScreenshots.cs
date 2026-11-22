using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review images of the Hollow Husk cut from the owner's SVG (SAN-112 SVG redesign and its definition pass; a copy of
    /// ShadeSvgScreenshots), in Screenshots/review/SAN-112/define/: the owner's art beside the Unity rig at the same scale, posed as
    /// painted (every limb turned to its painted angle), in the bind pose and in the fight idle (side_by_side.png); close-ups of the
    /// head, torso, arms and fists and legs, the art beside the painted pose at twice that scale (closeups/); the Husk beside Kage in
    /// idle, walk, attack and hit (kage_vs_hollow_husk_sheet.png) with a close-up and its head; the soft parts in motion and in the
    /// wind (soft_motion.png); and the line-up of every Order and Hollow foe (each tile also saved alone in lineup/, so the other
    /// characters can be compared pixel for pixel before and after). Needs a GPU: it skips itself under -nographics.
    /// Run with <c>-testFilter HuskSvgScreenshots</c> and no -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class HuskSvgScreenshots
    {
        const int W = 800, H = 600;
        static readonly string[] LineUp =
        {
            "order_footman", "order_runner", "order_shieldbearer", "order_chainthrower", "order_ironclad",
            "hollow_shade", "hollow_tutorial", "hollow_flicker", "hollow_husk",
        };
        /// <summary>Characters off the sheet whose soft parts share the code this ticket touches: rendered alone for the pixel check.</summary>
        static readonly string[] Others = { "raikon", "kage" };

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-112", "define"));
        static string Painting => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "hollow_art", "husk", "original", "husk_ref.png"));
        const string Id = "hollow_husk";

        [Test]
        public void RenderTheHuskReviewImages()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Dir, "tiles");
            Directory.CreateDirectory(tiles);
            SideBySide(tiles);

            var shots = new List<Color32[]>
            {
                Pair("idle", 30, -0.75f, 0.75f, (k, r) => { k.Add(60); r.Add(60); }),
                Pair("walk", 22, -1.5f, 1.5f, (k, r) => { k.Add(40, fwd: 1); r.Add(40, fwd: 1); }),
                Pair("attack", 15, -1.15f, 1.15f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(1, buttons: "S").Add(60); }),
                Pair("hit", 17, -0.5f, 0.5f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(60); }),
            };
            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels32((i % 2) * W, (1 - i / 2) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kage_vs_hollow_husk_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(20);
                Save(Path.Combine(Dir, "hollow_husk_closeup.png"), Render(f.Ronin.Pos + new Vector2(0f, 1.15f), 1.2f), W, H);     // the Husk stands 1.16 times Kage's height
                Save(Path.Combine(Dir, "hollow_husk_head.png"), Render(f.Ronin.Pos + new Vector2(-0.05f, 1.8f), 0.42f), W, H);
            }
            SoftMotion(tiles);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "side_by_side.png")));
        }

        static Color32[] Pair(string name, int frames, float kageX, float foeX, System.Action<ScriptedInput, ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: CharacterCatalog.Get(Id));
            script(f.KageInput, f.RoninInput);
            f.Run(frames);
            if (name == "hit") f.Ronin.rig.Secondary(0.2f, Vector2.zero);
            var px = Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 0.95f), 1.3f);
            Save(Path.Combine(Dir, "tiles", "kage_vs_hollow_husk_" + name + ".png"), px, W, H);
            return px;
        }

        // ---- the painting, the bind pose and the fight idle ----

        /// <summary>The art's canvas colour (#0E0814): the side-by-side tiles render the rig on it, so art and rig read alike.</summary>
        static readonly Color Canvas = new Color32(0x0E, 0x08, 0x14, 0xFF);

        /// <summary>The side-by-side: the whole figure (SVG px) at 0.9 tile px per SVG px.</summary>
        static readonly Rect Figure = new(512, 6, 560, 1000);
        const float FigureScale = 0.9f;

        /// <summary>The close-ups: a rectangle of the art (SVG px), the part whose bone carries it, at 2 tile px per SVG px.</summary>
        static readonly (string name, Rect crop, string part)[] CloseUps =
        {
            ("head", new Rect(560, 10, 260, 290), "Hood"),
            ("torso", new Rect(640, 150, 320, 400), "Torso"),
            ("arms_fists", new Rect(560, 200, 300, 300), "HandN"),
            ("legs", new Rect(520, 520, 520, 460), "Pelvis"),
        };
        const float CloseUpScale = 2f;

        static void SideBySide(string tiles)
        {
            int w = Mathf.RoundToInt(Figure.width * FigureScale), h = Mathf.RoundToInt(Figure.height * FigureScale);
            var painting = new Texture2D(2, 2);
            painting.LoadImage(File.ReadAllBytes(Painting));
            var art = FitCrop(painting, Figure, w, h);

            var def = CharacterCatalog.PartSet(Id).Build(CharacterCatalog.Get(Id));
            var data = SvgRigData.Load(HollowHuskPartSet.Resource);
            var go = new GameObject("HuskBind");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(def, 1000);
            rig.SetFacing(-1f, true);
            var fit = SvgRig.Map(def, data, new[] { "trunk" }, "fit");
            Vector2 World(Vector2 svg) => rig.transform.TransformPoint(fit(svg));
            float worldPerSvgPx = (World(new Vector2(0f, 0f)) - World(new Vector2(100f, 0f))).magnitude / 100f;

            // the same scale as the art, centred on the same SVG point by the fit (the painted sole is on Kage's ground)
            Color32[] Same(Rect crop, float tileScale, int tw, int th, Vector2 centre) =>
                Render(centre, th * 0.5f / tileScale * worldPerSvgPx, tw, th, Canvas);

            // the rig posed as painted: every limb turned so its drawn joints lie along the painted ones
            rig.Apply(PaintedPose(def, data));
            var painted = Same(Figure, FigureScale, w, h, World(Figure.center));
            rig.Apply(PoseLib.Ref());
            var bind = Same(Figure, FigureScale, w, h, World(Figure.center));

            // the close-ups: the art and the painted pose at twice the scale, centred by the part that carries the piece
            rig.Apply(PaintedPose(def, data));
            Directory.CreateDirectory(Path.Combine(Dir, "closeups"));
            foreach (var (name, crop, part) in CloseUps)
            {
                int cw = Mathf.RoundToInt(crop.width * CloseUpScale), ch = Mathf.RoundToInt(crop.height * CloseUpScale);
                var spec = data.parts.Find(p => p.name == part);
                var map = SvgRig.Map(def, data, spec.map, part);
                // the crop's centre on the part's bone: bind space through the bone's bind transform, then where the bone is now
                rig.Apply(PoseLib.Ref());
                var bone = rig.Bone(spec.bone);
                Vector3 local = bone.InverseTransformPoint(rig.transform.TransformPoint(map(crop.center)));
                rig.Apply(PaintedPose(def, data));
                Vector2 centre = bone.TransformPoint(local);
                var unity = Render(centre, ch * 0.5f / CloseUpScale * worldPerSvgPx, cw, ch, Canvas);
                var svg = FitCrop(painting, crop, cw, ch);
                Save(Path.Combine(Dir, "closeups", name + "_svg.png"), svg, cw, ch);
                Save(Path.Combine(Dir, "closeups", name + "_unity.png"), unity, cw, ch);
                var pair = new Texture2D(cw * 2, ch, TextureFormat.RGBA32, false);
                pair.SetPixels32(0, 0, cw, ch, svg);
                pair.SetPixels32(cw, 0, cw, ch, unity);
                pair.Apply();
                File.WriteAllBytes(Path.Combine(Dir, "closeup_" + name + ".png"), pair.EncodeToPNG());
                Object.DestroyImmediate(pair);
            }
            float bindScale = Mathf.Abs(go.transform.lossyScale.y);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(painting);

            // the fight idle, facing left (the foe faces Kage, who stands to its left), at the same scale: the fighter carries the
            // Husk's build scale, which the bare rig above does not, so the camera widens by the same factor; the ground sits where
            // the painted sole does in the art's tile
            Color32[] idle;
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(40);
                float wpp = worldPerSvgPx * Mathf.Abs(f.Ronin.rig.transform.lossyScale.y) / bindScale;
                float ortho = h * 0.5f / FigureScale * wpp;
                idle = Render(f.Ronin.Pos + new Vector2(-0.1f, ortho - (Figure.yMax - 965f) * wpp), ortho, w, h, Canvas);
            }
            Save(Path.Combine(tiles, "side_svg.png"), art, w, h);
            Save(Path.Combine(tiles, "side_unity_painted_pose.png"), painted, w, h);
            Save(Path.Combine(tiles, "side_unity_bind.png"), bind, w, h);
            Save(Path.Combine(tiles, "side_unity_idle.png"), idle, w, h);
            var sheet = new Texture2D(w * 4, h, TextureFormat.RGBA32, false);
            sheet.SetPixels32(0, 0, w, h, art);
            sheet.SetPixels32(w, 0, w, h, painted);
            sheet.SetPixels32(w * 2, 0, w, h, bind);
            sheet.SetPixels32(w * 3, 0, w, h, idle);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "side_by_side.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        /// <summary>
        /// The rig posed as the art is drawn: each limb segment turned so its drawn joints (the part's <c>seg</c> map) lie along the
        /// painted ones, the hands and feet kept as painted, the trunk and head as bound (the trunk's map already puts the painted neck
        /// and hips on Kage's). The far leg reaches back to the painted far boot; the far arm, hidden in the art, hangs behind the trunk.
        /// </summary>
        static Pose PaintedPose(RigDef d, SvgRigData data)
        {
            var fit = SvgRig.Map(d, data, new[] { "trunk" }, "fit");
            float Turn(string part)
            {
                var m = data.parts.Find(p => p.name == part).map;
                Vector2 a = new(F(m[1]), F(m[2])), b = new(F(m[3]), F(m[4]));
                B from = (B)System.Enum.Parse(typeof(B), m[5]), to = (B)System.Enum.Parse(typeof(B), m[6]);
                return KMath.Ang(fit(b) - fit(a)) - KMath.Ang(d.pivot[(int)to] - d.pivot[(int)from]);
            }
            float armU = Turn("ArmN_Upper"), armL = Turn("ArmN_Fore");
            float legU = Turn("LegN_Thigh"), legL = Turn("LegN_Shin");
            var farAnkle = fit(new Vector2(872f, 930f));
            return new Pose()
                .ArmN(armU, KMath.Wrap(armL - armU))
                .ArmF(-35f, -10f)
                .LegN(legU, KMath.Wrap(legL - legU), KMath.Wrap(-legL))
                .FootF(farAnkle.x, farAnkle.y, 0f);
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A rectangle of the painting (its pixels, y down) scaled to fit a w x h tile.</summary>
        static Color32[] FitCrop(Texture2D src, Rect crop, int w, int h)
        {
            var px = new Color32[w * h];
            float scale = Mathf.Min(w / crop.width, h / crop.height);
            float ox = (w - crop.width * scale) * 0.5f, oy = (h - crop.height * scale) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = crop.x + (x - ox) / scale, syDown = crop.y + (h - 1 - y - oy) / scale;
                    float sy = src.height - 1 - syDown;
                    px[y * w + x] = sx >= 0 && sy >= 0 && sx < src.width && sy < src.height
                        ? (Color32)src.GetPixelBilinear(sx / src.width, sy / src.height)
                        : new Color32(0, 0, 0, 255);
                }
            return px;
        }

        // ---- the soft parts: through a move, and in the wind ----

        const int T = 400;

        static void SoftMotion(string tiles)
        {
            var rows = new (string name, System.Func<Color32[]> make)[3][];
            for (int r = 0; r < 3; r++) rows[r] = new (string, System.Func<Color32[]>)[6];
            for (int c = 0; c < 6; c++)
            {
                int frame = 6 + c * 8;
                rows[0][c] = ($"move_f{frame:00}", () => Soft(frame, Wind.Calm, s => s.Add(20, fwd: 1).Add(6).Add(1, buttons: "S").Add(60)));
                int wf = 20 + c * 12;
                // the Husk faces left: a wind to +x blows at its face and streams the cape back; to -x it blows from behind
                rows[1][c] = ($"headwind_f{wf:00}", () => Soft(wf, Wind.Blowing(new Vector2(3f, 0f), 6f, seed: 76), s => s.Add(200)));
                rows[2][c] = ($"tailwind_f{wf:00}", () => Soft(wf, Wind.Blowing(new Vector2(-3f, 0.5f), 6f, seed: 77), s => s.Add(200)));
            }
            var sheet = new Texture2D(T * 6, T * 3, TextureFormat.RGBA32, false);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 6; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    Save(Path.Combine(tiles, "soft_" + name + ".png"), px, T, T);
                    sheet.SetPixels32(c * T, (2 - r) * T, T, T, px);
                }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "soft_motion.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        static Color32[] Soft(int frames, Wind wind, System.Action<ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3.5f, 0.6f, foeDef: CharacterCatalog.Get(Id));
            f.KageInput.Add(300);
            f.Ronin.rig.wind = wind;
            script(f.RoninInput);
            f.Run(frames);
            return Render(f.Ronin.Pos + new Vector2(-0.1f, 1.15f), 1.35f, T, T);
        }

        [Test]
        public void RenderTheLineUp()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Dir, "lineup");
            Directory.CreateDirectory(tiles);
            var line = new Texture2D(W * LineUp.Length, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < LineUp.Length; i++)
            {
                var px = Solo(LineUp[i], 20);
                Save(Path.Combine(tiles, LineUp[i] + ".png"), px, W, H);
                line.SetPixels32(i * W, 0, W, H, px);
            }
            line.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "lineup_sheet.png"), line.EncodeToPNG());
            Object.DestroyImmediate(line);
            // the soft parts after a walk and a stop: chains and cloth in motion, for the pixel check of the characters off the sheet
            foreach (var id in Others)
                Save(Path.Combine(tiles, id + "_motion.png"), Moving(id), W, H);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "lineup_sheet.png")));
        }

        static Color32[] Solo(string id, int frames)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(300);
            f.Run(frames);
            return Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f);
        }

        static Color32[] Moving(string id)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: id == "kage" ? null : CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(24, fwd: 1).Add(300);
            f.Run(34);
            var who = id == "kage" ? f.Kage : f.Ronin;
            return Render(who.Pos + new Vector2(0f, 1.1f), 1.3f);
        }

        internal static void Save(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        internal static Color32[] Render(Vector2 centre, float orthoSize, int w = W, int h = H, Color? background = null)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background ?? new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return px;
        }
    }
}
