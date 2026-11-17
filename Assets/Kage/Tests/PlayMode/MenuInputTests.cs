using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>Keyboard and gamepad both drive the menus (SAN-67): the same commands from either device.</summary>
    public class MenuInputTests : InputTestFixture
    {
        Keyboard kb;
        Gamepad pad;
        MenuInput input;
        readonly List<MenuCommand> got = new List<MenuCommand>();

        public override void Setup()
        {
            base.Setup();
            input = new MenuInput();
            kb = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
        }

        List<MenuCommand> Poll(bool inFight = false)
        {
            got.Clear();
            input.Poll(got, inFight);
            return got;
        }

        [Test]
        public void Enter_and_the_gamepad_south_button_both_choose()
        {
            Press(kb.enterKey);
            CollectionAssert.Contains(Poll(), MenuCommand.Submit);
            Release(kb.enterKey);
            InputSystem.Update();
            Poll();
            Press(pad.buttonSouth);
            CollectionAssert.Contains(Poll(), MenuCommand.Submit);
        }

        [Test]
        public void Escape_and_the_gamepad_east_button_both_go_back()
        {
            Press(kb.escapeKey);
            CollectionAssert.Contains(Poll(), MenuCommand.Back);
            Release(kb.escapeKey);
            InputSystem.Update();
            Poll();
            Press(pad.buttonEast);
            CollectionAssert.Contains(Poll(), MenuCommand.Back);
        }

        [Test]
        public void Arrows_wasd_and_the_dpad_all_move()
        {
            Press(kb.downArrowKey);
            CollectionAssert.AreEqual(new[] { MenuCommand.Down }, Poll());
            Release(kb.downArrowKey);
            InputSystem.Update();
            Poll();

            Press(kb.dKey);
            CollectionAssert.AreEqual(new[] { MenuCommand.Right }, Poll());
            Release(kb.dKey);
            InputSystem.Update();
            Poll();

            Press(pad.dpad.up);
            CollectionAssert.AreEqual(new[] { MenuCommand.Up }, Poll());
        }

        [Test]
        public void The_stick_moves_the_menu_too()
        {
            Set(pad.leftStick, new UnityEngine.Vector2(-1f, 0f));
            CollectionAssert.AreEqual(new[] { MenuCommand.Left }, Poll());
        }

        [Test]
        public void Q_and_E_and_the_shoulders_step_through_the_chapters()
        {
            Press(kb.qKey);
            CollectionAssert.Contains(Poll(), MenuCommand.Prev);
            Press(pad.rightShoulder);
            CollectionAssert.Contains(Poll(), MenuCommand.Next);
        }

        [Test]
        public void A_held_direction_repeats_only_after_a_delay()
        {
            Press(kb.sKey);
            Assert.AreEqual(1, Poll().Count);
            Assert.AreEqual(0, Poll().Count, "no repeat on the very next frame");
        }

        [Test]
        public void In_a_fight_escape_p_and_start_pause_and_nothing_else_is_taken()
        {
            Press(kb.escapeKey);
            CollectionAssert.AreEqual(new[] { MenuCommand.Pause }, Poll(inFight: true));
            Release(kb.escapeKey);
            InputSystem.Update();
            Press(kb.pKey);
            CollectionAssert.AreEqual(new[] { MenuCommand.Pause }, Poll(inFight: true));
            Release(kb.pKey);
            InputSystem.Update();
            Press(pad.startButton);
            CollectionAssert.AreEqual(new[] { MenuCommand.Pause }, Poll(inFight: true));
            Release(pad.startButton);
            InputSystem.Update();
            // fight keys are the fighters' own: the menu leaves them alone
            Press(kb.jKey);
            Press(pad.buttonSouth);
            Assert.AreEqual(0, Poll(inFight: true).Count);
        }
    }
}
