using System;
using Rover;
using UnityEngine;

namespace Training
{
    public class TrainingInstance : MonoBehaviour
    {
        private InstanceSetup setup;
        private EpisodeLayoutPicker layoutPicker;
        private EpisodeMonitor monitor;
        private EpisodeRewards rewards;
        private readonly EpisodeTimer timer = new EpisodeTimer();

        private RoverAIController rover;
        private Transform roverRoot;
        private Transform chassis;
        private float groundClearance;
        private EpisodeLayout layout;
        private bool hasPendingLayout;
        private bool episodeRunning;

        public event Action<TrainingInstance, EpisodeOutcome> EpisodeEnded;

        public int Index => setup != null ? setup.Index : -1;
        public RoverAIController Rover => rover;
        public Rect Arena => setup.Epoch.ArenaFor(Index);
        public EpisodeLayout Layout => layout;
        public EpisodeTimer Timer => timer;
        public bool EpisodeRunning => episodeRunning;

        public bool Initialize(InstanceSetup instanceSetup, out string error)
        {
            setup = instanceSetup;
            layoutPicker = new EpisodeLayoutPicker(setup.Arena, new System.Random(setup.Seed));
            monitor = new EpisodeMonitor(setup.Rules);
            rewards = new EpisodeRewards(setup.Rules);

            if (!PickLayout())
            {
                error = $"Could not find a clear spawn point and goal on '{setup.Epoch.Tile.name}'. Try lowering obstacle density or goal distance, or raising the max slope.";
                return false;
            }

            hasPendingLayout = true;
            return SpawnRover(out error);
        }

        public void RestartEpisode()
        {
            if (rover == null)
                return;

            episodeRunning = false;
            hasPendingLayout = false;
            rover.EpisodeInterrupted();
        }

        public void Teardown()
        {
            episodeRunning = false;

            if (rover != null)
                rover.EpisodeBeginning -= HandleEpisodeBeginning;
        }

        private bool SpawnRover(out string error)
        {
            var holder = new GameObject("Rover");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            GameObject roverObject = Instantiate(setup.RoverPrefab, layout.Spawn.position, Quaternion.identity, holder.transform);
            rover = roverObject.GetComponentInChildren<RoverAIController>(true);
            if (rover == null)
            {
                error = $"'{setup.RoverPrefab.name}' has no {nameof(RoverAIController)}.";
                return false;
            }

            roverRoot = roverObject.transform;
            chassis = rover.Chassis.transform;
            rover.EpisodeBeginning += HandleEpisodeBeginning;

            rover.enabled = false;
            holder.SetActive(true);
            groundClearance = RoverClearance.Measure(roverRoot, chassis);
            rover.enabled = true;

            error = null;
            return true;
        }

        private void HandleEpisodeBeginning(RoverAIController agent)
        {
            if (!hasPendingLayout && !PickLayout())
                Debug.LogWarning($"[Training] Instance {Index}: no clear spawn and goal found, reusing the previous ones.", this);

            hasPendingLayout = false;

            Pose spawn = ChassisPose(layout.Spawn);
            agent.ResetRover(spawn.position, spawn.rotation);
            agent.SetTarget(layout.Goal);

            timer.Restart(setup.Request.EpisodeSeconds);
            rewards.Begin(DistanceToGoal());
            episodeRunning = true;
        }

        private bool PickLayout()
        {
            Epoch epoch = setup.Epoch;
            if (!layoutPicker.TryPick(epoch.Surface, epoch.ArenaFor(Index), roverRoot, out EpisodeLayout picked))
                return false;

            layout = picked;
            return true;
        }

        private void FixedUpdate()
        {
            if (!episodeRunning || rover == null)
                return;

            float deltaTime = Time.fixedDeltaTime;
            timer.Advance(deltaTime);
            rover.AddReward(rewards.Step(DistanceToGoal(), ForwardSpeed(), deltaTime));

            EpisodeOutcome outcome = monitor.Evaluate(chassis, layout.Goal, Arena, timer);
            if (outcome != EpisodeOutcome.Running)
                EndEpisode(outcome);
        }

        private void EndEpisode(EpisodeOutcome outcome)
        {
            episodeRunning = false;
            rover.AddReward(rewards.Final(outcome));

            if (EpisodeEnded != null)
                EpisodeEnded.Invoke(this, outcome);

            if (outcome == EpisodeOutcome.TimedOut)
                rover.EpisodeInterrupted();
            else
                rover.EndEpisode();
        }

        private Pose ChassisPose(Pose ground)
        {
            Vector3 up = ground.rotation * Vector3.up;
            return new Pose(ground.position + up * (groundClearance + setup.Arena.SpawnGap), ground.rotation);
        }

        private float ForwardSpeed()
        {
            return Vector3.Dot(rover.Chassis.linearVelocity, chassis.forward);
        }

        private float DistanceToGoal()
        {
            return EpisodeMonitor.HorizontalDistance(chassis.position, layout.Goal);
        }
    }
}
