using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class EpisodeRules
    {
        [Tooltip("Horizontal distance from the goal that counts as reaching it, in meters.")]
        [Min(0.1f)] public float GoalRadius = 1.5f;
        [Tooltip("The episode fails once the chassis tilts further than this from upright.")]
        [Range(10f, 180f)] public float MaxTiltAngle = 70f;

        [Header("Rewards")]
        public float GoalReward = 1f;
        public float FailurePenalty = -1f;
        public float TimeoutReward;
        [Tooltip("Reward per meter of progress toward the goal; moving away costs the same.")]
        public float ProgressRewardPerMeter = 0.05f;
        [Min(0f)] public float TimePenaltyPerSecond = 0.01f;
        [Tooltip("Cost per meter driven backwards. At the same value as Progress Reward Per Meter, reversing toward the goal earns nothing. 0 lets the rover reverse freely.")]
        [Min(0f)] public float ReversePenaltyPerMeter = 0.05f;
    }
}
