using UnityEngine;

namespace Rover
{
    public readonly struct DriveCommand
    {
        public readonly float Throttle;
        public readonly float Steer;

        public DriveCommand(float throttle, float steer)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
        }

        public static DriveCommand Idle => default;
    }

    public interface IDriveCommandSource
    {
        DriveCommand Command { get; }
    }
}
