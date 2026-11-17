using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage
{
    /// <summary>What the player asked of the menu: the one vocabulary keyboard, gamepad and tests share.</summary>
    public enum MenuCommand
    {
        Up,
        Down,
        Left,
        Right,
        Submit,
        Back,
        /// <summary>Q / left shoulder: the previous chapter on the map.</summary>
        Prev,
        /// <summary>E / right shoulder: the next chapter on the map.</summary>
        Next,
        /// <summary>Esc / P / Start inside a fight.</summary>
        Pause,
    }

    /// <summary>
    /// One thing on a screen the player can move to and press: a button, a map node, a weapon card, one option of a segmented
    /// control. The screen sets what happens on press and how focus looks; <see cref="NavGroup"/> moves between items.
    /// </summary>
    public sealed class NavItem : MonoBehaviour
    {
        public string key;
        public bool interactable = true;
        public Action onSubmit;
        public Action<bool> onFocus;
        /// <summary>Called when the player moved the focus here (arrows, stick, mouse), not when a rebuild restored it.</summary>
        public Action onUserFocus;
        /// <summary>Left / Right pressed on this item; return true if it was used (a slider), false to move focus instead.</summary>
        public Func<int, bool> onSide;

        public RectTransform Rt { get { return (RectTransform)transform; } }

        public bool Focused { get; internal set; }

        /// <summary>The item's centre in the stage's own px (the canvas space, y up).</summary>
        public Vector2 Centre(RectTransform stage)
        {
            var corners = new Vector3[4];
            Rt.GetWorldCorners(corners);
            Vector3 c = (corners[0] + corners[2]) * 0.5f;
            return stage.InverseTransformPoint(c);
        }

        public UnityEngine.Rect BoundsIn(RectTransform stage)
        {
            var corners = new Vector3[4];
            Rt.GetWorldCorners(corners);
            Vector2 a = stage.InverseTransformPoint(corners[0]), b = stage.InverseTransformPoint(corners[2]);
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }

    /// <summary>
    /// Focus and directional movement over a set of <see cref="NavItem"/>s: the nearest item in the pressed direction wins
    /// (spatial navigation), so it works for lists, grids, the map's nodes and segmented controls alike. Disabled items are
    /// skipped. The group of an overlay (pause, a question) is separate, so a modal keeps the focus.
    /// </summary>
    public sealed class NavGroup
    {
        readonly List<NavItem> items = new List<NavItem>();
        readonly RectTransform stage;
        NavItem focus;

        public NavGroup(RectTransform stage) { this.stage = stage; }

        public IReadOnlyList<NavItem> Items { get { return items; } }

        public NavItem Focus { get { return focus; } }

        public void Clear()
        {
            items.Clear();
            focus = null;
        }

        public void Add(NavItem item) { items.Add(item); }

        /// <summary>Moves focus to the item with this key; the first usable item when no key matches.</summary>
        public void SetFocus(string key)
        {
            NavItem target = null;
            if (key != null) foreach (var i in items) if (i != null && i.key == key && i.interactable) { target = i; break; }
            if (target == null) foreach (var i in items) if (i != null && i.interactable) { target = i; break; }
            FocusOn(target);
        }

        public void FocusOn(NavItem item, bool byPlayer = false)
        {
            if (item == focus) return;
            if (focus != null) { focus.Focused = false; if (focus.onFocus != null) focus.onFocus(false); }
            focus = item;
            if (focus != null)
            {
                focus.Focused = true;
                if (focus.onFocus != null) focus.onFocus(true);
                if (byPlayer && focus != null && focus.onUserFocus != null) focus.onUserFocus();
            }
        }

        /// <summary>Applies a command. Returns true when the group used it.</summary>
        public bool Handle(MenuCommand cmd)
        {
            switch (cmd)
            {
                case MenuCommand.Submit:
                    if (focus != null && focus.interactable && focus.onSubmit != null) { focus.onSubmit(); return true; }
                    return false;
                case MenuCommand.Left:
                    if (focus != null && focus.onSide != null && focus.onSide(-1)) return true;
                    return Move(Vector2.left);
                case MenuCommand.Right:
                    if (focus != null && focus.onSide != null && focus.onSide(1)) return true;
                    return Move(Vector2.right);
                case MenuCommand.Up: return Move(Vector2.up);
                case MenuCommand.Down: return Move(Vector2.down);
            }
            return false;
        }

        bool Move(Vector2 dir)
        {
            if (focus == null) { SetFocus(null); return focus != null; }
            Vector2 from = focus.Centre(stage);
            NavItem best = null;
            float bestScore = float.MaxValue;
            foreach (var i in items)
            {
                if (i == null || i == focus || !i.interactable || !i.gameObject.activeInHierarchy) continue;
                Vector2 d = i.Centre(stage) - from;
                float along = Vector2.Dot(d, dir);
                if (along < 4f) continue;
                float across = Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x)));
                // a direction only reaches items that are mostly that way; prefer the ones lined up with the focus
                if (across > along * 2.2f + 40f) continue;
                float score = along + across * 2.5f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best == null) return false;
            FocusOn(best, true);
            return true;
        }

        /// <summary>The topmost active item under a screen point, or null (mouse).</summary>
        public NavItem ItemAt(Vector2 screenPoint, Camera cam)
        {
            NavItem hit = null;
            foreach (var i in items)
            {
                if (i == null || !i.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(i.Rt, screenPoint, cam)) hit = i;
            }
            return hit;
        }
    }

    /// <summary>
    /// Keyboard and gamepad to <see cref="MenuCommand"/>s. Arrows / WASD / d-pad / left stick move (with key repeat), Enter /
    /// Space / J / A submit, Esc / K / B go back, Q / E / shoulders step chapters, Esc / P / Start pause a fight. Fights read
    /// their own keys; the menus only listen while a menu is up.
    /// </summary>
    public sealed class MenuInput
    {
        const float RepeatDelay = 0.38f, RepeatRate = 0.11f;
        MenuCommand? held;
        float nextRepeat;

        public void Poll(List<MenuCommand> into, bool inFight)
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (inFight)
            {
                if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (gp != null && gp.startButton.wasPressedThisFrame))
                    into.Add(MenuCommand.Pause);
                held = null;
                return;
            }
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) into.Add(MenuCommand.Submit);
                if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame) into.Add(MenuCommand.Back);
                if (kb.qKey.wasPressedThisFrame) into.Add(MenuCommand.Prev);
                if (kb.eKey.wasPressedThisFrame) into.Add(MenuCommand.Next);
            }
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame) into.Add(MenuCommand.Submit);
                if (gp.buttonEast.wasPressedThisFrame) into.Add(MenuCommand.Back);
                if (gp.leftShoulder.wasPressedThisFrame) into.Add(MenuCommand.Prev);
                if (gp.rightShoulder.wasPressedThisFrame) into.Add(MenuCommand.Next);
            }
            MenuCommand? dir = ReadDirection(kb, gp);
            float now = Time.unscaledTime;
            if (dir == null) held = null;
            else if (held != dir) { held = dir; nextRepeat = now + RepeatDelay; into.Add(dir.Value); }
            else if (now >= nextRepeat) { nextRepeat = now + RepeatRate; into.Add(dir.Value); }
        }

        static MenuCommand? ReadDirection(Keyboard kb, Gamepad gp)
        {
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) return MenuCommand.Up;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) return MenuCommand.Down;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) return MenuCommand.Left;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) return MenuCommand.Right;
            }
            if (gp != null)
            {
                Vector2 v = gp.dpad.ReadValue() + gp.leftStick.ReadValue();
                if (v.sqrMagnitude > 0.36f)
                {
                    if (Mathf.Abs(v.y) >= Mathf.Abs(v.x)) return v.y > 0 ? MenuCommand.Up : MenuCommand.Down;
                    return v.x > 0 ? MenuCommand.Right : MenuCommand.Left;
                }
            }
            return null;
        }
    }
}
