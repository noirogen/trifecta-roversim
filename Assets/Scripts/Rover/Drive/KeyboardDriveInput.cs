using UnityEngine;
using UnityEngine.InputSystem;

namespace Rover
{
    public class KeyboardDriveInput : MonoBehaviour, IDriveCommandSource
    {
        [SerializeField, Min(0f)] private float throttleResponse = 2f;
        [SerializeField, Min(0f)] private float steerResponse = 3f;

        private float throttle;
        private float steer;

        public DriveCommand Command => new DriveCommand(throttle, steer);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            float targetThrottle = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                                        keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            float targetSteer = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                                     keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);

            throttle = Mathf.MoveTowards(throttle, targetThrottle, throttleResponse * Time.deltaTime);
            steer = Mathf.MoveTowards(steer, targetSteer, steerResponse * Time.deltaTime);
        }

        private static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
