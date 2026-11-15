using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastHuman
{
    /// <summary>One place that reads the keyboard, mouse and gamepad, so gameplay code never touches devices.</summary>
    public static class GameInput
    {
        // ---- scripted play-tests only: an automated run can hold these instead of real keys (which the
        //      editor drops whenever its window is not in front). Nothing in the game itself sets them.
        public static Vector2? AutoMove;
        public static bool AutoSlide, AutoGrapple, AutoSprint, AutoInteractHeld;
        static int autoJumpFrame = -1, autoInteractFrame = -1;
        /// <summary>Press E on the next frame and keep it held until AutoInteractHeld is cleared.</summary>
        public static void AutoHoldInteract()
        {
            autoInteractFrame = Time.frameCount + 1;
            AutoInteractHeld = true;
        }
        /// <summary>Press jump / dodge on the next frame.</summary>
        public static void AutoTapJump() => autoJumpFrame = Time.frameCount + 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAuto()
        {
            AutoMove = null;
            AutoSlide = AutoGrapple = AutoSprint = AutoInteractHeld = false;
            autoJumpFrame = autoInteractFrame = -1;
        }

        public static Vector2 Move
        {
            get
            {
                if (AutoMove.HasValue) return AutoMove.Value;
                var v = Vector2.zero;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                }
                var g = Gamepad.current;
                if (g != null) v += g.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Look delta for this frame in degrees.</summary>
        public static Vector2 Look(float mouseDegreesPerPixel, float stickDegreesPerSecond)
        {
            var v = Vector2.zero;
            var m = Mouse.current;
            if (m != null) v += m.delta.ReadValue() * mouseDegreesPerPixel;
            var g = Gamepad.current;
            if (g != null) v += g.rightStick.ReadValue() * stickDegreesPerSecond * Time.deltaTime;
            return v;
        }

        public static bool SprintHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return AutoSprint || (k != null && k.leftShiftKey.isPressed) || (g != null && g.leftStickButton.isPressed);
            }
        }

        /// <summary>Shift while driving: the car's boost.</summary>
        public static bool BoostHeld => SprintHeld;

        public static bool InteractPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                // (the arcade game keeps E for the disguise alone: everything else is used with F)
                return Time.frameCount == autoInteractFrame
                       || (k != null && k.fKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame);
            }
        }

        /// <summary>E (or the pad button) is being held down.</summary>
        public static bool InteractHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return AutoInteractHeld || (k != null && k.fKey.isPressed) || (g != null && g.buttonWest.isPressed);
            }
        }

        /// <summary>Crouch: hold Left Ctrl, or tap C to toggle.</summary>
        public static bool CrouchHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && k.leftCtrlKey.isPressed) || (g != null && g.buttonEast.isPressed);
            }
        }

        public static bool CrouchTogglePressed
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.cKey.wasPressedThisFrame;
            }
        }

        static int autoMaskFrame = -1;
        /// <summary>Scripted play-tests: press E (the disguise) on the next frame.</summary>
        public static void AutoTapMask() => autoMaskFrame = Time.frameCount + 1;

        /// <summary>E: the robot disguise.</summary>
        public static bool MaskPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return Time.frameCount == autoMaskFrame
                       || (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
            }
        }

        /// <summary>R: try again at once after a loss.</summary>
        public static bool RetryPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && (k.rKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                       || (g != null && g.buttonSouth.wasPressedThisFrame);
            }
        }

        /// <summary>Space: dodge in the boss fight, jump in the parkour escape.</summary>
        public static bool JumpPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return Time.frameCount == autoJumpFrame
                       || (k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
            }
        }

        public static bool DodgePressed => JumpPressed;

        /// <summary>
        /// Hold to slide. Left Shift is the key shown to the player: in a browser Ctrl+W closes the
        /// tab, so Ctrl must not be the one held while running forward. Ctrl and C still work.
        /// </summary>
        public static bool SlideHeld
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return AutoSlide || (k != null && (k.leftShiftKey.isPressed || k.leftCtrlKey.isPressed || k.cKey.isPressed))
                       || (g != null && g.buttonEast.isPressed);
            }
        }

        /// <summary>Left mouse button (or F): hold to fire and hang on to the grapple.</summary>
        public static bool GrappleHeld
        {
            get
            {
                var m = Mouse.current;
                var k = Keyboard.current;
                var g = Gamepad.current;
                return AutoGrapple || (m != null && m.leftButton.isPressed) || (k != null && k.fKey.isPressed) || (g != null && g.rightTrigger.isPressed);
            }
        }

        public static bool ConfirmPressed
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                       || (g != null && g.buttonSouth.wasPressedThisFrame);
            }
        }

        /// <summary>Any "go on" input: used to continue story screens and skip the intro flight.</summary>
        public static bool ContinuePressed
        {
            get
            {
                var k = Keyboard.current;
                return InteractPressed || ConfirmPressed || ClickPressed || (k != null && k.spaceKey.wasPressedThisFrame);
            }
        }

        public static bool ClickPressed
        {
            get
            {
                var m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
            }
        }
    }
}
