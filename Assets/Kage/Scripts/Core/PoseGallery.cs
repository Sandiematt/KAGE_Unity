using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Debug contact sheet: lays out rigs frozen at every key pose of the moveset so poses can be
    /// reviewed in one screenshot. F9 toggles it, PageUp/PageDown flips pages (play mode).
    /// </summary>
    public class PoseGallery : MonoBehaviour
    {
        readonly List<GameObject> _spawned = new();
        readonly List<(string, Pose)> _entries = new();
        public int page = -1;
        public const int PerPage = 12;
        RigDef _def;

        void BuildEntries()
        {
            if (_entries.Count > 0) return;
            _def = KageModel.Build(KageModel.Look.Kage);
            _entries.Add(("Reference", PoseLib.Ref()));
            _entries.Add(("Stance", PoseLib.Stance()));
            _entries.Add(("Walk", PoseLib.Walk(1.2f, 1f, new Pose())));
            _entries.Add(("Crouch", PoseLib.Crouch()));
            _entries.Add(("Block", PoseLib.Block()));
            _entries.Add(("CrouchBlock", PoseLib.CrouchBlock()));
            _entries.Add(("JumpRise", PoseLib.JumpRise()));
            _entries.Add(("JumpFall", PoseLib.JumpFall()));
            _entries.Add(("Land", PoseLib.Land()));
            _entries.Add(("HurtHigh", PoseLib.HurtHigh()));
            _entries.Add(("HurtBody", PoseLib.HurtBody()));
            _entries.Add(("HurtLow", PoseLib.HurtLow()));
            _entries.Add(("Tumble", PoseLib.Tumble()));
            _entries.Add(("Lie", PoseLib.Lie()));
            _entries.Add(("Thrown 120", PoseLib.Thrown(120f)));
            var m = KatanaMoves.Build();
            var all = new[]
            {
                m.jab, m.cross, m.uppercut, m.crouchPunch, m.lunge, m.kick1, m.kick2, m.axeKick, m.sweep, m.pushKick,
                m.throwGrab, m.airKick, m.airPunch, m.airSlash, m.roll, m.backflip, m.groundPound, m.getUp,
                m.iai, m.risingMoon, m.cleave, m.airCleave, m.shadowStep, m.lowReap, m.counterStance, m.counterSlash, m.crescent,
                m.crownWheel, m.tameCut,
            };
            var start = PoseLib.Stance();
            foreach (var mv in all)
            {
                foreach (var k in mv.keys)
                {
                    if (k.frame == mv.keys[^1].frame && mv.keys.Count > 2) continue;
                    var p = new Pose();
                    mv.Evaluate(_def, k.frame, start, p);
                    _entries.Add(($"{mv.display} f{k.frame}", p));
                }
            }
        }

        public int Pages { get { BuildEntries(); return (_entries.Count + PerPage - 1) / PerPage; } }

        public void Show(int p)
        {
            BuildEntries();
            page = Mathf.Clamp(p, 0, Pages - 1);
            var items = new List<(string, Pose)>();
            for (int i = 0; i < PerPage; i++)
            {
                int idx = page * PerPage + i;
                if (idx < _entries.Count) items.Add(_entries[idx]);
            }
            Layout(items, $"POSE GALLERY  {page + 1}/{Pages}");
        }

        /// <summary>Strobe view: one technique sampled at 12 evenly spaced frames, to judge how
        /// smoothly it moves. id is a KatanaMoves field name ("roll", "backflip", …) or "tumble" for a
        /// launched body turning towards lying.</summary>
        public void ShowStrobe(string id)
        {
            BuildEntries();
            if (page < 0) page = 0;
            var items = new List<(string, Pose)>();
            var stance = PoseLib.Stance();
            if (id == "tumble")
            {
                Pose tumble = PoseLib.Tumble();
                for (int i = 0; i < PerPage; i++)
                {
                    float u = i / (float)(PerPage - 1);
                    var p = new Pose();
                    p.CopyFrom(tumble);
                    p.a[(int)B.Hips] = u * Tuning.Get<CombatFlowTuning>().tumbleRestAngle;     // towards lying, never past it
                    p.MarkDirty();
                    items.Add(($"{id} {u:0.00}", p));
                }
            }
            else
            {
                var field = typeof(KatanaMoves).GetField(id);
                if (field == null) return;
                var mv = (MoveDef)field.GetValue(KatanaMoves.Build());
                for (int i = 0; i < PerPage; i++)
                {
                    float f = mv.total * i / (float)(PerPage - 1);
                    var p = new Pose();
                    mv.Evaluate(_def, f, stance, p);
                    items.Add(($"{mv.display} f{f:0}", p));
                }
            }
            Layout(items, "STROBE  " + id.ToUpperInvariant());
        }

        void Layout(List<(string, Pose)> items, string title)
        {
            Clear();
            var game = KageGame.I;
            var back = MeshKit.MakeRenderer("GalleryBackdrop", transform, MeshKit.Quad(30f, 14f, KMath.Hex("#7A5A5C")), MeshKit.Alpha, 2900);
            back.transform.position = new Vector3(0f, -0.3f, 0f);
            _spawned.Add(back.gameObject);
            const int cols = 6;
            const float dx = 1.55f, dy = 2.45f;
            for (int i = 0; i < items.Count; i++)
            {
                var (label, pose) = items[i];
                int c = i % cols, r = i / cols;
                Vector2 at = new Vector2(-dx * (cols - 1) * 0.5f + c * dx, -r * dy);
                var go = new GameObject("Gallery_" + label);
                go.transform.SetParent(transform, false);
                go.transform.position = at;
                var rig = go.AddComponent<FighterRig>();
                rig.Build(_def, 3000);
                pose.MarkDirty();
                rig.Apply(pose);
                rig.groundY = at.y;
                rig.autoSecondary = true;
                rig.ResetSoft();
                if (rig.scarf != null) { rig.scarf.groundY = at.y; rig.scarf.ResetChain(); }
                _spawned.Add(go);
                if (game != null) game.Hud.Label(at + new Vector2(0f, -0.12f), label);
            }
            if (game != null)
            {
                game.Cam.useOverride = true;
                game.Cam.overrideCenter = new Vector2(0f, -0.25f);
                game.Cam.overrideSize = 2.75f;
                game.Hud.Banner(title, Color.white, 2f);
            }
        }

        public void Clear()
        {
            foreach (var g in _spawned) if (g != null) Destroy(g);
            _spawned.Clear();
            if (KageGame.I != null) KageGame.I.Hud.ClearLabels();
        }

        public void Hide()
        {
            Clear();
            page = -1;
            if (KageGame.I != null) KageGame.I.Cam.useOverride = false;
        }
    }
}
