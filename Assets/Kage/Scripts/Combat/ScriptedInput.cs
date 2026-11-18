using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// Plays a fixed list of inputs (frames held + buttons on the first frame). Used to drive
    /// fighters from the Unity CLI (`unity command script eval`) for automated play tests.
    /// </summary>
    public class ScriptedInput : IInputSource
    {
        struct Step { public int frames; public FrameInput input; }
        readonly Queue<Step> _q = new();
        Step _cur;
        int _left;
        public bool Done => _left <= 0 && _q.Count == 0;
        public IInputSource fallback;

        /// <summary>dx/dy are relative to the fighter's facing when relative = true.</summary>
        public ScriptedInput Add(int frames, int fwd = 0, int dy = 0, string buttons = "", bool tapFwd = false, bool tapBack = false, bool dash = false)
        {
            var f = new FrameInput { dx = fwd, dy = dy, p = buttons.Contains("P"), k = buttons.Contains("K"), s = buttons.Contains("S"), x = buttons.Contains("X"), dash = dash };
            f.tapRight2 = tapFwd; f.tapLeft2 = tapBack;
            _q.Enqueue(new Step { frames = frames, input = f });
            return this;
        }

        public void Sample() { fallback?.Sample(); }

        public FrameInput Poll(Fighter self)
        {
            if (_left <= 0)
            {
                if (_q.Count == 0) return fallback != null ? fallback.Poll(self) : default;
                _cur = _q.Dequeue();
                _left = _cur.frames;
            }
            bool first = _left == _cur.frames;
            _left--;
            var o = _cur.input;
            float face = self.facing;
            o.dx = (int)(o.dx * face);
            bool tf = o.tapRight2, tb = o.tapLeft2;
            o.tapRight2 = o.tapLeft2 = false;
            if (first)
            {
                if (tf) { if (face > 0) o.tapRight2 = true; else o.tapLeft2 = true; }
                if (tb) { if (face > 0) o.tapLeft2 = true; else o.tapRight2 = true; }
            }
            else { o.p = o.k = o.s = o.x = o.dash = false; }
            return o;
        }
    }
}
