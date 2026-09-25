using System;
using UnityEngine;

namespace Rover
{
    public class RoverDriveController : MonoBehaviour
    {
        [Serializable]
        private class WheelModule
        {
            public ArticulationBody wheel;
            public ArticulationBody steering;
            public bool invertDrive;
            public bool invertSteering;

            [NonSerialized] public Vector2 offset;
            [NonSerialized] public float radius;
        }

        [SerializeField] private MonoBehaviour commandSource;
        [SerializeField] private ArticulationBody chassis;
        [SerializeField] private WheelModule[] wheels = Array.Empty<WheelModule>();

        [Header("Limits")]
        [SerializeField, Min(0f)] private float maxSpeed = 0.5f;
        [SerializeField, Min(0.01f)] private float minTurnRadius = 1.5f;
        [SerializeField, Min(0f)] private float steerRate = 1f;

        private IDriveCommandSource source;
        private float currentSteer;

        public float CurrentSteer => currentSteer;

        public void SetCommandSource(IDriveCommandSource newSource) => source = newSource;

        private void Awake()
        {
            source = commandSource as IDriveCommandSource;
            CacheWheelOffsets();
            CacheWheelRadii();
        }

        private void OnValidate()
        {
            if (commandSource == null || commandSource is IDriveCommandSource)
                return;

            var candidate = commandSource.GetComponent<IDriveCommandSource>() as MonoBehaviour;
            if (candidate == null)
                Debug.LogWarning($"{commandSource.name} has no {nameof(IDriveCommandSource)} component.", this);

            commandSource = candidate;
        }

        private Vector3 WheelPositionInChassis(ArticulationBody wheel)
        {
            Transform frame = chassis.transform;
            Vector3 hub = wheel.transform.TransformPoint(wheel.anchorPosition);
            return Quaternion.Inverse(frame.rotation) * (hub - frame.position);
        }

        private void CacheWheelOffsets()
        {
            Vector3 pivot = Vector3.zero;
            int fixedCount = 0;
            float centerline = 0f;

            foreach (var module in wheels)
            {
                Vector3 local = WheelPositionInChassis(module.wheel);
                centerline += local.x;
                if (module.steering == null)
                {
                    pivot += local;
                    fixedCount++;
                }
            }

            pivot.z = fixedCount > 0 ? pivot.z / fixedCount : 0f;
            pivot.x = centerline / Mathf.Max(1, wheels.Length);

            float maxLateral = 0f;
            foreach (var module in wheels)
            {
                Vector3 local = WheelPositionInChassis(module.wheel) - pivot;
                module.offset = new Vector2(local.x, local.z);
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(local.x));
            }

            if (minTurnRadius <= maxLateral)
                Debug.LogWarning($"Min turn radius {minTurnRadius} is inside the wheel track ({maxLateral}); inner wheels will reverse.", this);
        }

        private void CacheWheelRadii()
        {
            Physics.SyncTransforms();

            foreach (var module in wheels)
            {
                module.radius = MeasureWheelRadius(module.wheel);
                if (module.radius <= 0f)
                    Debug.LogError($"{module.wheel.name} has no collider to measure a radius from.", module.wheel);
            }
        }

        private static float MeasureWheelRadius(ArticulationBody wheel)
        {
            Transform body = wheel.transform;
            Vector3 axle = body.rotation * wheel.anchorRotation * Vector3.right;
            Vector3 hub = body.TransformPoint(wheel.anchorPosition);
            Vector3 reference = Mathf.Abs(Vector3.Dot(axle, Vector3.up)) < 0.9f ? Vector3.up : Vector3.forward;
            Vector3 spoke = Vector3.Cross(axle, reference).normalized;

            float radius = 0f;
            foreach (var collider in wheel.GetComponentsInChildren<Collider>())
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(i * 45f, axle) * spoke;
                    Vector3 surface = collider.ClosestPoint(hub + direction * 100f);
                    radius = Mathf.Max(radius, Vector3.ProjectOnPlane(surface - hub, axle).magnitude);
                }
            }

            return radius;
        }

        private void FixedUpdate()
        {
            var command = source != null ? source.Command : DriveCommand.Idle;

            currentSteer = Mathf.MoveTowards(currentSteer, command.Steer, steerRate * Time.fixedDeltaTime);

            float speed = command.Throttle * maxSpeed;
            float curvature = currentSteer / minTurnRadius;

            foreach (var module in wheels)
            {
                SolveWheel(module.offset, speed, curvature, out float steerAngle, out float wheelSpeed);

                if (module.steering != null)
                    module.steering.SetDriveTarget(ArticulationDriveAxis.X, module.invertSteering ? -steerAngle : steerAngle);

                if (module.radius <= 0f)
                    continue;

                float angularVelocity = wheelSpeed / module.radius * Mathf.Rad2Deg;
                module.wheel.SetDriveTargetVelocity(ArticulationDriveAxis.X, module.invertDrive ? -angularVelocity : angularVelocity);
            }
        }

        private static void SolveWheel(Vector2 offset, float speed, float curvature, out float steerAngle, out float wheelSpeed)
        {
            if (Mathf.Abs(curvature) < 1e-4f)
            {
                steerAngle = 0f;
                wheelSpeed = speed;
                return;
            }

            float lateral = 1f / curvature - offset.x;
            steerAngle = Mathf.Atan(offset.y / lateral) * Mathf.Rad2Deg;

            float distance = Mathf.Sqrt(lateral * lateral + offset.y * offset.y);
            wheelSpeed = speed * curvature * distance * Mathf.Sign(lateral);
        }
    }
}