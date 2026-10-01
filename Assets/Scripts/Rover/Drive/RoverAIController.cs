using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Rover
{
    // Vector observations (chassis frame, x = right, y = up, z = forward):
    //   0-1    direction to target (x, z)
    //   2      distance to target
    //   3-5    linear velocity
    //   6-8    angular velocity
    //   9-11   world up
    //   12     applied steer
    //   13     last throttle
    //   14-15  left and right rocker angles
    //   16-17  left and right bogie angles
    //
    // Actions: 0 throttle, 1 steer.

    [RequireComponent(typeof(RoverDriveController))]
    public class RoverAIController : Agent, IDriveCommandSource
    {
        public const int ObservationSize = 18;
        public const int ContinuousActionSize = 2;

        [Header("Rover")]
        [SerializeField] private ArticulationBody chassis;
        [SerializeField] private ArticulationBody leftRocker;
        [SerializeField] private ArticulationBody rightRocker;
        [SerializeField] private ArticulationBody leftBogie;
        [SerializeField] private ArticulationBody rightBogie;
        [SerializeField] private KeyboardDriveInput heuristicInput;

        [Header("Observation scales")]
        [SerializeField, Min(0.01f)] private float targetDistanceScale = 30f; // >= 30 meters = 1
        [SerializeField, Min(0.01f)] private float speedScale = 1.5f; // >= 1.5 m/s = 1
        [SerializeField, Min(0.01f)] private float angularSpeedScale = 1f;
        [SerializeField, Min(0.01f)] private float rockerAngleScale = 30f; // degrees
        [SerializeField, Min(0.01f)] private float bogieAngleScale = 30f;

        private RoverDriveController controller;
        private ArticulationBody[] bodies;
        private ArticulationDrive[] initialDrives;
        private Pose startPose;
        private DriveCommand command;
        private Vector3? target;

        public DriveCommand Command => command;
        public ArticulationBody Chassis => chassis;
        public Pose StartPose => startPose;
        public bool HasTarget => target.HasValue;
        public Vector3 TargetWorld => target ?? chassis.transform.position;

        public Vector2 TargetInRoverFrame
        {
            get
            {
                if (!target.HasValue)
                    return Vector2.zero;

                Vector3 local = Quaternion.Inverse(chassis.transform.rotation) * (target.Value - chassis.transform.position);
                return new Vector2(local.x, local.z);
            }
        }

        public void SetTarget(Vector3 worldPosition) => target = worldPosition;
        public void ClearTarget() => target = null;

        public override void Initialize()
        {
            controller = GetComponent<RoverDriveController>();
            if (heuristicInput == null)
                heuristicInput = GetComponent<KeyboardDriveInput>();

            startPose = new Pose(chassis.transform.position, chassis.transform.rotation);

            bodies = chassis.GetComponentsInChildren<ArticulationBody>();
            initialDrives = new ArticulationDrive[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
                initialDrives[i] = bodies[i].xDrive;

            ValidateSetup();
        }

        public override void OnEpisodeBegin()
        {
            ResetRover(startPose.position, startPose.rotation);
        }

        // Resets rover to its defaults, teleports it to the provided position (and rotation)
        public void ResetRover(Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body == chassis)
                    continue;

                if (TryZeroSpace(body.dofCount, out var zero))
                {
                    body.jointPosition = zero;
                    body.jointVelocity = zero;
                }

                body.xDrive = initialDrives[i];
            }

            chassis.TeleportRoot(position, rotation);
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;

            command = DriveCommand.Idle;
            controller.ResetSteer();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Quaternion toLocal = Quaternion.Inverse(chassis.transform.rotation);

            Vector2 toTarget = TargetInRoverFrame;
            float distance = toTarget.magnitude;
            sensor.AddObservation(distance > 1e-3f ? toTarget / distance : Vector2.zero);
            sensor.AddObservation(Mathf.Clamp01(distance / targetDistanceScale));

            sensor.AddObservation(toLocal * chassis.linearVelocity / speedScale);
            sensor.AddObservation(toLocal * chassis.angularVelocity / angularSpeedScale);
            sensor.AddObservation(toLocal * Vector3.up);

            sensor.AddObservation(controller.CurrentSteer);
            sensor.AddObservation(command.Throttle);

            sensor.AddObservation(JointAngle(leftRocker, rockerAngleScale));
            sensor.AddObservation(JointAngle(rightRocker, rockerAngleScale));
            sensor.AddObservation(JointAngle(leftBogie, bogieAngleScale));
            sensor.AddObservation(JointAngle(rightBogie, bogieAngleScale));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuous = actions.ContinuousActions;
            command = new DriveCommand(continuous[0], continuous[1]);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var input = heuristicInput != null ? heuristicInput.Command : DriveCommand.Idle;
            var continuous = actionsOut.ContinuousActions;
            continuous[0] = input.Throttle;
            continuous[1] = input.Steer;
        }

        private static float JointAngle(ArticulationBody joint, float scale)
        {
            return joint != null ? joint.jointPosition[0] * Mathf.Rad2Deg / scale : 0f;
        }

        private void ValidateSetup()
        {
            if (!chassis.isRoot)
                Debug.LogError($"{chassis.name} is not the articulation root; TeleportRoot will not work.", this);

            if (leftRocker == null || rightRocker == null || leftBogie == null || rightBogie == null)
                Debug.LogWarning("A rocker or bogie is not assigned; its observation will always be 0.", this);

            var parameters = GetComponent<BehaviorParameters>();
            var brain = parameters.BrainParameters;

            if (brain.VectorObservationSize != ObservationSize)
                Debug.LogError($"Vector Observation Space Size is {brain.VectorObservationSize}, expected {ObservationSize}.", this);

            if (brain.ActionSpec.NumContinuousActions != ContinuousActionSize || brain.ActionSpec.NumDiscreteActions != 0)
                Debug.LogError($"Actions should be {ContinuousActionSize} continuous and 0 discrete.", this);

            if (!parameters.UseChildSensors)
                Debug.LogWarning("Use Child Sensors is off; camera sensors on child objects will be ignored.", this);

            var names = new HashSet<string>();
            foreach (var camera in GetComponentsInChildren<CameraSensorComponent>(true))
            {
                if (camera.Camera == null)
                    Debug.LogError($"{camera.name} has no Camera assigned.", camera);
                if (!names.Add(camera.SensorName))
                    Debug.LogError($"Duplicate camera sensor name '{camera.SensorName}'.", camera);
            }
        }

        private static bool TryZeroSpace(int dofCount, out ArticulationReducedSpace zero)
        {
            switch (dofCount)
            {
                case 1: zero = new ArticulationReducedSpace(0f); return true;
                case 2: zero = new ArticulationReducedSpace(0f, 0f); return true;
                case 3: zero = new ArticulationReducedSpace(0f, 0f, 0f); return true;
                default: zero = default; return false;
            }
        }
    }
}