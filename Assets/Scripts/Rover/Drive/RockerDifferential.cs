using UnityEngine;

namespace Rover
{
    public class RockerDifferential : MonoBehaviour
    {
        [SerializeField] private ArticulationBody leftRocker;
        [SerializeField] private ArticulationBody rightRocker;
        [SerializeField] private bool axesMirrored;

        private void FixedUpdate()
        {
            float coupling = axesMirrored ? -1f : 1f;

            float left = leftRocker.jointPosition[0] * Mathf.Rad2Deg;
            float right = rightRocker.jointPosition[0] * Mathf.Rad2Deg;
            float error = (left + coupling * right) * 0.5f;

            leftRocker.SetDriveTarget(ArticulationDriveAxis.X, left - error);
            rightRocker.SetDriveTarget(ArticulationDriveAxis.X, right - coupling * error);
        }
    }
}
