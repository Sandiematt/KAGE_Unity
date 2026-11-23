using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kage.Tests
{
    /// <summary>
    /// The review images of Raikon cut from the owner's SVG on the SVG rig pipeline (SAN-71 redesign; a copy of HuskSvgScreenshots),
    /// in Screenshots/review/SAN-71/svg/: the trace beside the Unity rig at the same scale, posed as painted (every limb turned to its
    /// painted angle), in the bind pose and in the fight idle (tiles/side_*.png); close-ups of the head and hat, the torso and mantle,
    /// the arms and sword, the legs and cape at twice that scale (closeups/); Raikon beside Kage in idle, walk, attack and hit
    /// (kage_vs_raikon_sheet.png) and alone in a sheet of fight poses (raikon_poses.png); the soft parts in motion and in the wind
    /// (soft_motion.png); and every other character alone (lineup/), so they can be compared pixel for pixel before and after.
    /// The framing reads only the fit of the painting (constants below, the same as the rig's), so the same fixture renders the
    /// region-cut Raikon it replaced (the "before" images). Needs a GPU: it skips itself under -nographics.
    /// Run with <c>-testFilter RaikonSvgScreenshots</c> and no -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class RaikonSvgScreenshots
    {
        const int W = 800, H = 600;
        const string Id = "raikon";

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-71", "svg"));
        /// <summary>The trace rendered as is (written by the cut's preview step, cut/svg.png); the painting when it is missing.</summary>
        static string Art => File.Exists(Path.Combine(Dir, "cut", "svg.png"))
            ? Path.Combine(Dir, "cut", "svg.png")
            : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "raikon_art", "original", "Raikon.png"));
        const float SvgWidth = 976f;

        // ---- the fit of the painting onto Kage's reference frame (Tools/raikon_art/rig.json "fit"; SAN-71's first cut used the same) ----
        const float FitK = 0.688f;
        static readonly Vector2 FitSvg = new(501f, 1195f), FitRef = new(878f, 815f);

        static Vector2 Fit(Vector2 s) => KageModel.P(FitRef.x + (s.x - FitSvg.x) * FitK, FitRef.y + (s.y - FitSvg.y) * FitK);

        /// <summary>The painted joints (SVG px) the pose as painted turns each limb to: the rig's own seg maps when its cut exists.</summary>
        static Vector2 shoulder = new(545, 410), elbow = new(548, 528), wrist = new(542, 628);
        static Vector2 hip = new(505, 800), knee = new(518, 1000), ankle = new(520, 1118), farAnkle = new(462, 1118);

        static void ReadJoints()
        {
            var ta = Resources.Load<TextAsset>("Art/SvgRig/Raikon");
            if (ta == null) return;
            var data = SvgRigData.Parse(ta.text);
            string[] Map(string part) => data.parts.FirstOrDefault(p => p.name == part)?.map;
            Vector2 V(string[] m, int i) => new(F(m[i]), F(m[i + 1]));
            var m1 = Map("ArmN_Upper"); if (m1 != null && m1[0] == "seg") { shoulder = V(m1, 1); elbow = V(m1, 3); }
            var m2 = Map("ArmN_Fore"); if (m2 != null && m2[0] == "seg") wrist = V(m2, 3);
            var m3 = Map("ThighN"); if (m3 != null && m3[0] == "seg") { hip = V(m3, 1); knee = V(m3, 3); }
            var m4 = Map("ShinN"); if (m4 != null && m4[0] == "seg") ankle = V(m4, 3);
        }

        [Test]
        public void RenderTheRaikonReviewImages()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            ReadJoints();
            string tiles = Path.Combine(Dir, "tiles");
            Directory.CreateDirectory(tiles);
            SideBySide(tiles);

            var shots = new List<Color32[]>
            {
                Pair("idle", 30, -0.85f, 0.85f, (k, r) => { k.Add(60); r.Add(60); }),
                Pair("walk", 22, -1.6f, 1.6f, (k, r) => { k.Add(40, fwd: 1); r.Add(40, fwd: 1); }),
                Pair("attack", 15, -1.3f, 1.3f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(1, buttons: "S").Add(60); }),
                Pair("hit", 17, -0.6f, 0.6f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(60); }),
            };
            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels32((i % 2) * W, (1 - i / 2) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kage_vs_raikon_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Poses(tiles);

            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(20);
                Save(Path.Combine(Dir, "raikon_closeup.png"), Render(f.Ronin.Pos + new Vector2(0f, 1.3f), 1.45f), W, H);
                Save(Path.Combine(Dir, "raikon_head.png"), Render(f.Ronin.Pos + new Vector2(0.05f, 2.2f), 0.5f), W, H);
            }
            SoftMotion(tiles);
            Assert.IsTrue(File.Exists(Path.Combine(tiles, "side_unity_idle.png")));
        }

        static Color32[] Pair(string name, int frames, float kageX, float foeX, System.Action<ScriptedInput, ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: CharacterCatalog.Get(Id));
            script(f.KageInput, f.RoninInput);
            f.Run(frames);
            if (name == "hit") f.Ronin.rig.Secondary(0.2f, Vector2.zero);
            var px = Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.15f), 1.55f);
            Save(Path.Combine(Dir, "tiles", "kage_vs_raikon_" + name + ".png"), px, W, H);
            return px;
        }

        // ---- fight poses: Raikon alone, facing left ----

        const int PT = 460;

        static void Poses(string tiles)
        {
            var poses = new (string name, System.Func<Color32[]> make)[]
            {
                ("idle", () => Pose(s => s.Add(60), 40)),
                ("walk_f10", () => Pose(s => s.Add(40, fwd: 1), 10)),
                ("walk_f20", () => Pose(s => s.Add(40, fwd: 1), 20)),
                ("crouch", () => Pose(s => s.Add(60, dy: -1), 30)),
                ("guard", () => Pose(s => s.Add(60), 30, PoseLib.Block())),
                ("slash_f6", () => Pose(s => s.Add(1, buttons: "S").Add(60), 6)),
                ("slash_f10", () => Pose(s => s.Add(1, buttons: "S").Add(60), 10)),
                ("slash_f16", () => Pose(s => s.Add(1, buttons: "S").Add(60), 16)),
                ("special_f14", () => Pose(s => s.Add(1, buttons: "X").Add(60), 14)),
                ("kick_f9", () => Pose(s => s.Add(1, buttons: "K").Add(60), 9)),
                ("jump", () => Pose(s => s.Add(1, dy: 1).Add(60), 14)),
                ("hurt", () => Pose(s => s.Add(60), 20, hurt: true)),
            };
            const int cols = 6;
            int rows = (poses.Length + cols - 1) / cols;
            var sheet = new Texture2D(PT * cols, PT * rows, TextureFormat.RGBA32, false);
            for (int i = 0; i < poses.Length; i++)
            {
                var px = poses[i].make();
                Save(Path.Combine(tiles, "pose_" + poses[i].name + ".png"), px, PT, PT);
                sheet.SetPixels32((i % cols) * PT, (rows - 1 - i / cols) * PT, PT, PT, px);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "raikon_poses.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        static Color32[] Pose(System.Action<ScriptedInput> script, int frames, Pose pose = null, bool hurt = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3.5f, 0.6f, foeDef: CharacterCatalog.Get(Id));
            f.KageInput.Add(300);
            script(f.RoninInput);
            f.Run(frames);
            if (pose != null) f.Ronin.rig.Apply(pose);
            if (hurt) f.Ronin.rig.Apply(PoseLib.HurtHigh());
            return Render(f.Ronin.Pos + new Vector2(-0.15f, 1.35f), 1.6f, PT, PT);
        }

        // ---- the trace, the painted pose, the bind pose and the fight idle, all at the trace's scale ----

        /// <summary>The trace's canvas colour: the side-by-side tiles render the rig on it, so art and rig read alike.</summary>
        static readonly Color Canvas = new Color32(0x01, 0x00, 0x00, 0xFF);

        /// <summary>The side-by-side: the whole figure (SVG px) at 0.75 tile px per SVG px.</summary>
        static readonly Rect Figure = new(80, 6, 820, 1210);
        const float FigureScale = 0.75f;

        /// <summary>The close-ups: a rectangle of the art (SVG px) and the bone that carries what is in it, at 1.6 tile px per SVG px.</summary>
        static readonly (string name, Rect crop, B bone)[] CloseUps =
        {
            ("head_hat", new Rect(180, 0, 520, 380), B.Head),
            ("torso_mantle", new Rect(300, 230, 420, 380), B.Chest),
            ("arms_sword", new Rect(80, 380, 560, 660), B.ArmNL),
            ("legs_cape", new Rect(240, 560, 640, 650), B.Hips),
        };
        const float CloseUpScale = 1.6f;

        static void SideBySide(string tiles)
        {
            int w = Mathf.RoundToInt(Figure.width * FigureScale), h = Mathf.RoundToInt(Figure.height * FigureScale);
            var art = new Texture2D(2, 2);
            art.LoadImage(File.ReadAllBytes(Art));
            float artPx = art.width / SvgWidth;
            var artTile = FitCrop(art, Scaled(Figure, artPx), w, h);

            var def = CharacterCatalog.PartSet(Id).Build(CharacterCatalog.Get(Id));
            var go = new GameObject("RaikonBind");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(def, 1000);
            rig.SetFacing(-1f, true);
            Vector2 World(Vector2 svg) => rig.transform.TransformPoint(Fit(svg));
            float worldPerSvgPx = (World(new Vector2(0f, 0f)) - World(new Vector2(100f, 0f))).magnitude / 100f;

            Color32[] Same(float tileScale, int tw, int th, Vector2 centre) =>
                Render(centre, th * 0.5f / tileScale * worldPerSvgPx, tw, th, Canvas);

            rig.Apply(PaintedPose(def));
            var painted = Same(FigureScale, w, h, World(Figure.center));
            rig.Apply(PoseLib.Ref());
            var bind = Same(FigureScale, w, h, World(Figure.center));

            Directory.CreateDirectory(Path.Combine(Dir, "closeups"));
            foreach (var (name, crop, boneId) in CloseUps)
            {
                int cw = Mathf.RoundToInt(crop.width * CloseUpScale), ch = Mathf.RoundToInt(crop.height * CloseUpScale);
                rig.Apply(PoseLib.Ref());
                var bone = rig.Bone(boneId);
                Vector3 local = bone.InverseTransformPoint(rig.transform.TransformPoint(Fit(crop.center)));
                rig.Apply(PaintedPose(def));
                Vector2 centre = bone.TransformPoint(local);
                var unity = Same(CloseUpScale, cw, ch, centre);
                rig.Apply(PoseLib.Ref());
                var unityBind = Same(CloseUpScale, cw, ch, World(crop.center));
                Save(Path.Combine(Dir, "closeups", name + "_svg.png"), FitCrop(art, Scaled(crop, artPx), cw, ch), cw, ch);
                Save(Path.Combine(Dir, "closeups", name + "_unity.png"), unity, cw, ch);
                Save(Path.Combine(Dir, "closeups", name + "_unity_bind.png"), unityBind, cw, ch);
            }
            float bindScale = Mathf.Abs(go.transform.lossyScale.y);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(art);

            // the fight idle, facing left, at the same scale: the fighter carries the 1.24 body scale, which the bare rig above does
            // not, so the camera widens by the same factor; the ground sits where the painted sole does in the art's tile
            Color32[] idle;
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(40);
                float wpp = worldPerSvgPx * Mathf.Abs(f.Ronin.rig.transform.lossyScale.y) / bindScale;
                float ortho = h * 0.5f / FigureScale * wpp;
                idle = Render(f.Ronin.Pos + new Vector2(0.25f, ortho - (Figure.yMax - FitSvg.y) * wpp), ortho, w, h, Canvas);
            }
            Save(Path.Combine(tiles, "side_svg.png"), artTile, w, h);
            Save(Path.Combine(tiles, "side_unity_painted_pose.png"), painted, w, h);
            Save(Path.Combine(tiles, "side_unity_bind.png"), bind, w, h);
            Save(Path.Combine(tiles, "side_unity_idle.png"), idle, w, h);
        }

        static Rect Scaled(Rect r, float k) => new(r.x * k, r.y * k, r.width * k, r.height * k);

        static float Turn(RigDef d, Vector2 a, Vector2 b, B from, B to) => KMath.Ang(Fit(b) - Fit(a)) - KMath.Ang(d.pivot[(int)to] - d.pivot[(int)from]);

        /// <summary>
        /// The rig posed as the art is drawn: each limb segment turned so its drawn joints lie along the painted ones. Both arms take
        /// the painted sword arm's angles (the painting holds the tachi in the near hand, the skeleton in the far hand, so the far
        /// arm's tachi hangs where the painted one does); the near leg as painted; the far leg reaches to the painted far boot.
        /// </summary>
        static Pose PaintedPose(RigDef d)
        {
            float armU = Turn(d, shoulder, elbow, B.ArmNU, B.ArmNL), armL = Turn(d, elbow, wrist, B.ArmNL, B.HandN);
            float farU = Turn(d, shoulder, elbow, B.ArmFU, B.ArmFL), farL = Turn(d, elbow, wrist, B.ArmFL, B.HandF);
            float legU = Turn(d, hip, knee, B.LegNU, B.LegNL), legL = Turn(d, knee, ankle, B.LegNL, B.FootN);
            var fa = Fit(farAnkle);
            return new Pose()
                .ArmN(armU, KMath.Wrap(armL - armU))
                .ArmF(farU, KMath.Wrap(farL - farU), KMath.Wrap(-farL))
                .LegN(legU, KMath.Wrap(legL - legU), KMath.Wrap(-legL))
                .FootF(fa.x, fa.y, 0f);
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A rectangle of an image (its pixels, y down) scaled to fit a w x h tile.</summary>
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
                        : new Color32(1, 0, 0, 255);
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
                // Raikon faces left: a wind to +x blows at his face and streams the cape and ponytail back; to -x it blows from behind
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
            return Render(f.Ronin.Pos + new Vector2(0f, 1.4f), 1.6f, T, T);
        }

        // ---- every other character, for the pixel check ----

        [Test]
        public void RenderTheLineUp()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Dir, "lineup");
            Directory.CreateDirectory(tiles);
            var failed = new List<string>();
            foreach (var id in CharacterCatalog.Ids.OrderBy(i => i).ToList())
            {
                if (id == Id) continue;
                try
                {
                    Save(Path.Combine(tiles, id + ".png"), Solo(id, 20), W, H);
                    Save(Path.Combine(tiles, id + "_motion.png"), Moving(id), W, H);
                }
                catch (System.Exception e) { failed.Add(id + ": " + e.Message); }
            }
            Save(Path.Combine(tiles, "kage_as_kage_motion.png"), Moving(null), W, H);
            Save(Path.Combine(tiles, Id + ".png"), Solo(Id, 20), W, H);
            Save(Path.Combine(tiles, Id + "_motion.png"), Moving(Id), W, H);
            CollectionAssert.IsEmpty(failed);
        }

        static Color32[] Solo(string id, int frames)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(300);
            f.Run(frames);
            return Render(f.Ronin.Pos + new Vector2(0f, 1.2f), 1.3f);
        }

        /// <summary>After a walk and a stop: chains, cloth and coats in motion. With no id, Kage himself (the player's side).</summary>
        static Color32[] Moving(string id)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: id == null ? null : CharacterCatalog.Get(id));
            f.KageInput.Add(24, fwd: 1).Add(300);
            f.RoninInput.Add(24, fwd: 1).Add(300);
            f.Run(34);
            var who = id == null ? f.Kage : f.Ronin;
            return Render(who.Pos + new Vector2(0f, 1.2f), 1.4f);
        }

        static void Save(string path, Color32[] px, int w, int h) => HuskSvgScreenshots.Save(path, px, w, h);

        static Color32[] Render(Vector2 centre, float orthoSize, int w = W, int h = H, Color? background = null) =>
            HuskSvgScreenshots.Render(centre, orthoSize, w, h, background);
    }
}
