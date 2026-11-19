using System.Globalization;
using Evaluation;
using TMPro;
using Unity.MLAgents;
using UnityEngine;

namespace Training
{
    // Fills the Training panel's monitor from the TrainingOrganizer's report, live: when a run
    // starts, as episodes finish, and when it ends. Adapted from EvaluationMonitorView.
    //
    // Every field is optional. Leave any you don't have empty and that part is skipped.
    public class TrainingMonitorView : MonoBehaviour
    {
        public enum Window { Recent, Overall }

        [SerializeField] private TrainingOrganizer organizer;

        [Tooltip("Recent: the stats over the latest episodes, so you can see whether it is improving. " +
                 "Overall: the stats over the whole run.")]
        [SerializeField] private Window window = Window.Recent;

        [Tooltip("Training finishes episodes far faster than an evaluation, so the numbers are refreshed at most this often " +
                 "(seconds) while episodes arrive. The start and end of a run always refresh straight away. " +
                 "The episode tracker isn't affected: it takes every episode as it finishes.")]
        [SerializeField, Min(0f)] private float refreshInterval = 0.2f;

        [Header("Header")]
        [Tooltip("\"Run 4\"")]
        [SerializeField] private TMP_Text runText;
        [Tooltip("\"checkpointName - map startTile - # episodes - seed #####\"")]
        [SerializeField] private TMP_Text detailsText;
        [Tooltip("Which episodes the numbers cover, e.g. \"Recent: last 10% of the run (at least 100)\"")]
        [SerializeField] private TMP_Text windowText;

        [Header("Progress")]
        [SerializeField] private OutcomeBar progressBar;
        [SerializeField] private TMP_Text progressLabelText;    // "100 / 200 episodes"
        [SerializeField] private TMP_Text progressPercentText;  // "50%"

        [Header("Status")]
        [Tooltip("The organizer's status line, e.g. \"Starting the Python trainer...\" or \"Training on Jezero_B4: episode 120/1000, success 38%\". " +
                 "Useful on a training panel because starting Python takes a while.")]
        [SerializeField] private TMP_Text statusText;
        [Tooltip("How far along the run is: a percentage, \"epoch 3 of 10\", then Done or Stopped once it ends. (The \"Completion\" label.)")]
        [SerializeField] private TMP_Text completionText;

        [Header("Success tile")]
        [SerializeField] private TMP_Text successText;
        [Tooltip("\"95% CI 42%-69%\"")]
        [SerializeField] private TMP_Text successDetailText;

        [Header("Collisions tile")]
        [SerializeField] private TMP_Text collisionsText;
        [Tooltip("\"14 of 50\"")]
        [SerializeField] private TMP_Text collisionsDetailText;

        [Header("Timeouts tile")]
        [SerializeField] private TMP_Text timeoutsText;
        [Tooltip("\"9 of 50\"")]
        [SerializeField] private TMP_Text timeoutsDetailText;

        [Header("Time to goal tile")]
        [SerializeField] private TMP_Text timeToGoalText;

        [Header("Avg reward tile")]
        [Tooltip("The average total reward per episode, e.g. \"2.4\"")]
        [SerializeField] private TMP_Text avgRewardText;

        [Header("Why it failed")]
        [SerializeField] private TMP_Text hitRockCountText;
        [SerializeField] private TMP_Text timedOutCountText;

        [Header("Compared with the previous run")]
        [Tooltip("\"First Run\" or \"vs run 3\"")]
        [SerializeField] private TMP_Text changeLabelText;
        [Tooltip("\"+8 pts\" (change in the recent success rate)")]
        [SerializeField] private TMP_Text changeText;

        [Header("Episode tracker")]
        [SerializeField] private EpisodeTrackingBar tracker;
        [Tooltip("Shows the hovered column's details, and this text when nothing is hovered.")]
        [SerializeField] private TMP_Text trackerLabel;
        [SerializeField] private string trackerLabelDefault = "Episode Tracker";

        private static readonly string EpisodesUnit = IterationUnit.Episodes.ToString();
        private static readonly string EpochsUnit = IterationUnit.Epochs.ToString();

        [Header("Learning curve")]
        [SerializeField] private LearningCurve curve;
        [SerializeField] private LearningCurve rewardCurve;
        [SerializeField] private TMP_Text curveLabel;

        private TrainingReport latest;
        private bool dirty;
        private float nextRefresh;

        private void Awake()
        {
            if (organizer == null)
            {
                organizer = GetComponent<TrainingOrganizer>();
                if (organizer == null)
                    organizer = FindAnyObjectByType<TrainingOrganizer>();
            }
        }

        private void OnEnable()
        {
            if (organizer != null)
            {
                organizer.RunStarted += OnRunStarted;
                organizer.EpisodeFinished += OnEpisodeFinished;
                organizer.Finished += OnFinished;
                organizer.StatusChanged += OnStatusChanged;

                // Opened mid-run or after a run: show what is there.
                Set(statusText, organizer.Status);
                if (organizer.Report != null)
                {
                    Show(organizer.Report);
                    SyncTracker(organizer.Report);
                }
            }

            if (tracker != null)
                tracker.BinHovered += OnBinHovered;
            if (curve != null) curve.PointHovered += OnCurveHovered;
            if (organizer == null || organizer.Report == null)
                ShowIdle();
        }

        private void OnDisable()
        {
            if (organizer != null)
            {
                organizer.RunStarted -= OnRunStarted;
                organizer.EpisodeFinished -= OnEpisodeFinished;
                organizer.Finished -= OnFinished;
                organizer.StatusChanged -= OnStatusChanged;
            }

            if (tracker != null)
                tracker.BinHovered -= OnBinHovered;
            if (curve != null) curve.PointHovered -= OnCurveHovered;
        }

        private void Update()
        {
            // Episodes only mark the numbers as out of date; this redraws them at most every refreshInterval.
            if (dirty && Time.unscaledTime >= nextRefresh)
                Show(latest);
        }

        /// <summary>For a Recent / Overall toggle button.</summary>
        public void ShowRecent()
        {
            window = Window.Recent;
            if (latest != null)
                Show(latest);
        }

        /// <summary>For a Recent / Overall toggle button.</summary>
        public void ShowOverall()
        {
            window = Window.Overall;
            if (latest != null)
                Show(latest);
        }

        private void OnRunStarted(TrainingReport report)
        {
            Show(report);
            SyncTracker(report);
        }

        // The report already includes this episode when the event fires.
        private void OnEpisodeFinished(TrainingAttempt attempt, TrainingReport report)
        {
            latest = report;
            dirty = true;

            // The tracker is cheap to extend, so it takes every episode.
            if (tracker == null)
                return;

            if (tracker.EpisodeCount == report.AttemptLog.Count - 1)
                tracker.AddAttempt(attempt);
            else
                tracker.SetFromReport(report); // out of step with the report: start again from it
        }

        private void OnFinished(TrainingReport report, string savedPath)
        {
            Show(report);
            SyncTracker(report);
        }

        private void OnStatusChanged(string status) => Set(statusText, status);

        private void OnBinHovered(EpisodeBin? bin)
        {
            Set(trackerLabel, bin.HasValue ? bin.Value.ToString() : trackerLabelDefault);
        }

        private void SyncTracker(TrainingReport report)
        {
            if (tracker != null)
                tracker.SetFromReport(report);
        }

        private void Show(TrainingReport r)
        {
            latest = r;
            dirty = false;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh(r);
        }

        private void Refresh(TrainingReport r)
        {
            if (r == null)
                return;

            if (curve != null) curve.SetFromReport(r);
            ShowProgress(r);
            if (rewardCurve != null) rewardCurve.SetFromReport(r);

            // Every stat lives under Recent or Overall.
            var s = window == Window.Recent ? r.Recent : r.Overall;
            bool any = s.Episodes > 0;

            Set(runText, $"Run {r.RunNumber}");
            Set(detailsText, $"{r.Model} - {r.MapName} {r.StartTile} - {Iterations(r)} - seed {r.Seed}");
            Set(windowText, window == Window.Recent ? $"Recent: {r.RecentWindow}" : $"Overall: all {r.Episodes:N0} episodes");
            Set(completionText, Completion(r));

            Set(successText, EvaluationReport.Percent(s.SuccessRate));
            Set(successDetailText, any
                ? $"95% CI {EvaluationReport.Percent(s.SuccessRateLow)}-{EvaluationReport.Percent(s.SuccessRateHigh)}"
                : "95% CI -");

            Set(collisionsText, EvaluationReport.Percent(s.CollisionRate));
            Set(collisionsDetailText, $"{s.Collisions} of {s.Episodes}");

            Set(timeoutsText, EvaluationReport.Percent(s.TimeoutRate));
            Set(timeoutsDetailText, $"{s.TimedOut} of {s.Episodes}");

            Set(timeToGoalText, EvaluationReport.Seconds(s.AverageSecondsToGoal));
            Set(avgRewardText, any ? s.AverageReward.ToString("0.0", CultureInfo.InvariantCulture) : "-");

            Set(hitRockCountText, s.FailedHitRock.ToString("00"));
            Set(timedOutCountText, s.FailedTimedOut.ToString("00"));

            Set(changeLabelText, r.HasPrevious ? $"vs run {r.PreviousRunNumber}" : "First Run");
            Set(changeText, r.HasPrevious ? EvaluationReport.Points(r.SuccessChangePoints) : "-");
        }

        // "1,000 episodes", using whatever unit the run counts in.
        private static string Iterations(TrainingReport r)
        {
            string unit = string.IsNullOrEmpty(r.IterationUnit) ? "iterations" : r.IterationUnit.ToLowerInvariant();
            return $"{r.IterationsPlanned:N0} {unit}";
        }

        // How far along the run is, depending on what the run counts in.
        private static string Completion(TrainingReport r)
        {
            // Finish() fills FinishedAt; autosaves during the run don't.
            if (!string.IsNullOrEmpty(r.FinishedAt))
                return r.Completed ? "Done" : "Stopped";

            if (r.IterationUnit == EpisodesUnit && r.IterationsPlanned > 0)
                return EvaluationReport.Percent(Mathf.Clamp01(r.Episodes / (float)r.IterationsPlanned));

            if (r.IterationUnit == EpochsUnit && r.IterationsPlanned > 0)
            {
                int epoch = r.Episodes > 0 ? r.AttemptLog[r.Episodes - 1].Epoch : 0;
                return $"epoch {epoch} of {r.IterationsPlanned}";
            }

            // Counting in trainer steps: the run stops at an estimated step budget.
            if (r.MaxTrainerSteps > 0 && Academy.IsInitialized)
                return EvaluationReport.Percent(Mathf.Clamp01(Academy.Instance.TotalStepCount / (float)r.MaxTrainerSteps));

            return "-";
        }

        private static void Set(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private void OnCurveHovered(TrainingReport.Stats s){
            Set(curveLabel, s != null ? curve.Label(s) : "Learning curve");
        }
        private void ShowProgress(TrainingReport r)
        {
            int done = 0, total = 0;
            string label = "-";

            if (r.IterationUnit == EpisodesUnit && r.IterationsPlanned > 0)
            {
                done = r.Episodes;
                total = r.IterationsPlanned;
                label = $"{done:N0} / {total:N0} episodes";
            }
            else if (r.IterationUnit == EpochsUnit && r.IterationsPlanned > 0)
            {
                done = r.Episodes > 0 ? r.AttemptLog[r.Episodes - 1].Epoch : 0;
                total = r.IterationsPlanned;
                label = $"Epoch {done:N0} / {total:N0}";
            }
            else if (r.MaxTrainerSteps > 0 && Academy.IsInitialized)
            {
                long steps = Academy.Instance.TotalStepCount;
                long max = r.MaxTrainerSteps;
                done = (int)System.Math.Min(steps, int.MaxValue);
                total = (int)System.Math.Min(max, int.MaxValue);
                label = $"{steps:N0} / {max:N0} steps";
            }

            bool complete = !string.IsNullOrEmpty(r.FinishedAt) && r.Completed;
            float fraction = complete ? 1f : (total > 0 ? Mathf.Clamp01(done / (float)total) : 0f);

            if (progressBar != null)
                progressBar.Set(complete ? 1 : done, complete ? 1 : total);

            Set(progressLabelText, label);
            Set(progressPercentText, (total > 0 || complete) ? EvaluationReport.Percent(fraction) : "-");
        }

        private void ShowIdle()
        {
            const string dash = "---";

            if (progressBar != null)
                progressBar.Set(0, 1);
            Set(progressLabelText, "Not started");
            Set(progressPercentText, "0%");

            Set(runText, "N/A");
            Set(detailsText, "Start a run to view metrics");
            Set(windowText, dash);
            Set(completionText, dash);

            Set(successText, dash);
            Set(successDetailText, dash);
            Set(collisionsText, dash);
            Set(collisionsDetailText, dash);
            Set(timeoutsText, dash);
            Set(timeoutsDetailText, dash);
            Set(timeToGoalText, dash);
            Set(avgRewardText, dash);

            Set(hitRockCountText, dash);
            Set(timedOutCountText, dash);

            Set(changeLabelText, "Since Previous Run");
            Set(changeText, dash);

            if (tracker != null)
                tracker.ClearEpisodes();
            Set(trackerLabel, dash);
            if (curve != null)
                curve.Clear();
            if (rewardCurve != null)
                rewardCurve.Clear();
        }
    }
}