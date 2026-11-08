using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;

namespace RacingCarsControllerAndroid
{
    public partial class MainActivity : Activity
    {
        // Deadzone constants to prevent stick drift
        private const float STICK_DEADZONE = 0.20f;
        private const float TRIGGER_DEADZONE = 0.15f;

        // Current input states
        private bool _isSteeringLeft = false;
        private bool _isSteeringRight = false;
        private bool _isThrottleForward = false;
        private bool _isThrottleReverse = false;

        /// <summary>
        /// Captures analog inputs: Left Stick (Steering) and Triggers (Throttle/Brake)
        /// </summary>
        public override bool DispatchGenericMotionEvent(MotionEvent e)
        {
            if (e != null && (e.Source.HasFlag(InputSourceType.Gamepad) || e.Source.HasFlag(InputSourceType.Joystick)))
            {
                // 1. Steering with Left Stick (LS) Horizontal Axis
                float lsX = e.GetAxisValue(Axis.X);
                HandleSteeringStick(lsX);

                // 2. Analog Triggers: RT (Throttle) and LT (Reverse)
                float rightTrigger = Math.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
                float leftTrigger = Math.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake));
                HandleAnalogTriggers(rightTrigger, leftTrigger);

                return true; // Prevents Android UI focus shifts
            }

            return base.DispatchGenericMotionEvent(e);
        }

        /// <summary>
        /// Captures discrete buttons: A, B, X, Y, Bumpers, and D-Pad
        /// </summary>
        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if (e != null && (e.Source.HasFlag(InputSourceType.Gamepad) || e.Source.HasFlag(InputSourceType.Joystick)))
            {
                bool isDown = e.Action == KeyEventActions.Down;

                switch (e.KeyCode)
                {
                    // Throttle Forward: Button A or Right Bumper (RB)
                    case Keycode.ButtonA:
                    case Keycode.ButtonR1:
                    case Keycode.DpadUp:
                        SetForwardState(isDown);
                        return true;

                    // Reverse / Brake: Button X or Left Bumper (LB)
                    case Keycode.ButtonX:
                    case Keycode.ButtonL1:
                    case Keycode.DpadDown:
                        SetReverseState(isDown);
                        return true;

                    // Steering with D-Pad
                    case Keycode.DpadLeft:
                        SetLeftState(isDown);
                        return true;

                    case Keycode.DpadRight:
                        SetRightState(isDown);
                        return true;

                    // Lights Toggle: Button B
                    case Keycode.ButtonB:
                        if (isDown && e.RepeatCount == 0)
                        {
                            ToggleLights();
                        }
                        return true;

                    // Turbo / Speed Mode: Button Y
                    case Keycode.ButtonY:
                        SetTurboState(isDown);
                        return true;

                    // Block Android from treating D-pad center as Enter
                    case Keycode.DpadCenter:
                        return true;
                }
            }

            return base.DispatchKeyEvent(e);
        }

        #region Control Handlers

        private void HandleSteeringStick(float x)
        {
            if (x < -STICK_DEADZONE)
            {
                SetLeftState(true);
                SetRightState(false);
            }
            else if (x > STICK_DEADZONE)
            {
                SetLeftState(false);
                SetRightState(true);
            }
            else
            {
                SetLeftState(false);
                SetRightState(false);
            }
        }

        private void HandleAnalogTriggers(float rt, float lt)
        {
            if (rt > TRIGGER_DEADZONE)
            {
                SetForwardState(true);
                SetReverseState(false);
            }
            else if (lt > TRIGGER_DEADZONE)
            {
                SetForwardState(false);
                SetReverseState(true);
            }
            else
            {
                // Only reset if buttons aren't actively holding throttle
                if (!_isThrottleForward) SetForwardState(false);
                if (!_isThrottleReverse) SetReverseState(false);
            }
        }

        private void SetForwardState(bool active)
        {
            if (_isThrottleForward == active) return;
            _isThrottleForward = active;

            // Trigger the car's forward motion
            // If calling the car backend instance directly:
            // carService?.SetThrottle(active ? 100 : 0);
            
            // Or trigger the existing forward UI button:
            SimulateButtonState(Resource.Id.button_forward, active);
        }

        private void SetReverseState(bool active)
        {
            if (_isThrottleReverse == active) return;
            _isThrottleReverse = active;

            SimulateButtonState(Resource.Id.button_backward, active);
        }

        private void SetLeftState(bool active)
        {
            if (_isSteeringLeft == active) return;
            _isSteeringLeft = active;

            SimulateButtonState(Resource.Id.button_left, active);
        }

        private void SetRightState(bool active)
        {
            if (_isSteeringRight == active) return;
            _isSteeringRight = active;

            SimulateButtonState(Resource.Id.button_right, active);
        }

        private void SetTurboState(bool active)
        {
            SimulateButtonState(Resource.Id.button_turbo, active);
        }

        private void ToggleLights()
        {
            var btnLight = FindViewById<Button>(Resource.Id.button_light);
            btnLight?.PerformClick();
        }

        private void SimulateButtonState(int buttonId, bool isPressed)
        {
            RunOnUiThread(() =>
            {
                var view = FindViewById<View>(buttonId);
                if (view == null) return;

                long now = SystemClock.UptimeMillis();
                var action = isPressed ? MotionEventActions.Down : MotionEventActions.Up;
                var motionEvent = MotionEvent.Obtain(now, now, action, 0, 0, 0);
                view.DispatchTouchEvent(motionEvent);
                motionEvent.Recycle();
            });
        }

        #endregion
    }
}
