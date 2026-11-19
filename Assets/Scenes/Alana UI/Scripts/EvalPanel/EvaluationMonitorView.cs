using TMPro;
using UnityEngine;
using Training;

namespace Evaluation
{
    // Fills the Evaluation Monitor panel from the organizer's report, live: the report is refreshed
    // when a run starts, after every episode, and when it finishes.
    //
    // Every field is optional. Leave any you don't have empty and that part is skipped.
    // Doesn't touch EvaluationReportPanel: both can listen to the organizer at the same time.
    public class EvaluationMonitorView : MonoBehaviour
    {
        [SerializeField] public EvaluationOrganizer organizer;

        [Header("Header")]
        [Tooltip("\"Run 4\"")]
        [SerializeField] private TMP_Text runText;
        [Tooltip("\"checkpointName - selection - # episodes - seed #####\"")]
        [SerializeField] private TMP_Text detailsText;

        [Header("Progress")]
        [SerializeField] private OutcomeBar progressBar;
        [SerializeField] private TMP_Text progressLabelText;    // "100 / 200 episodes"
        [SerializeField] private TMP_Text progressPercentText;  // "50%"

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

        [Header("Why it failed")]
        [SerializeField] private TMP_Text hitRockCountText;
        [SerializeField] private TMP_Text timedOutCountText;

        [Header("Compared with the previous run")]
        [Tooltip("\"First Run\" or \"vs run 3\"")]
        [SerializeField] private TMP_Text changeLabelText;
        [Tooltip("\"+8 pts\"")]
        [SerializeField] private TMP_Text changeText;

        [Header("Episode tracker")]
        [SerializeField] private EpisodeTrackingBar tracker;
        [Tooltip("Shows the hovered column's details, and this text when nothing is hovered.")]
        [SerializeField] private TMP_Text trackerLabel;
        [SerializeField] private string trackerLabelDefault = "Episode Tracker";

        private void Awake()
        {
            if (organizer == null)
            {
                organizer = GetComponent<EvaluationOrganizer>();
                if (organizer == null)
                    organizer = FindAnyObjectByType<EvaluationOrganizer>();
            }
        }

        private void OnEnable()
        {
            if (organizer != null)
            {
                organizer.RunStarted += OnRunStarted;
                organizer.EpisodeFinished += OnEpisodeFinished;
                organizer.Finished += OnFinished;

                // Opened mid-run or after a run: show what is there.
                if (organizer.Report != null)
                    Refresh(organizer.Report);
            }

            if (tracker != null)
                tracker.BinHovered += OnBinHovered;

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
            }

            if (tracker != null)
                tracker.BinHovered -= OnBinHovered;
        }

        private void OnRunStarted(EvaluationReport report) => Refresh(report);

        // The report already includes this episode when the event fires.
        private void OnEpisodeFinished(AttemptResult attempt, EvaluationReport report) => Refresh(report);

        private void OnFinished(EvaluationReport report, string savedPath) => Refresh(report);

        private void OnBinHovered(EpisodeBin? bin)
        {
            Set(trackerLabel, bin.HasValue ? bin.Value.ToString() : trackerLabelDefault);
        }

        private void Refresh(EvaluationReport r)
        {
            if (r == null)
                return;

            int done = r.Episodes + r.Skipped;
            if (progressBar != null)
                progressBar.Set(done, r.EpisodesPlanned);
            Set(progressLabelText, $"{done:N0} / {r.EpisodesPlanned:N0} episodes");
            Set(progressPercentText, r.EpisodesPlanned > 0
                ? EvaluationReport.Percent(Mathf.Clamp01(done / (float)r.EpisodesPlanned))
                : "-");

            bool any = r.Episodes > 0;

            Set(runText, $"Run {r.RunNumber}");
            Set(detailsText, $"{r.Model} - {r.Selection} - {r.EpisodesPlanned} episodes - seed {r.Seed}");

            Set(successText, EvaluationReport.Percent(r.SuccessRate));
            Set(successDetailText, any
                ? $"95% CI {EvaluationReport.Percent(r.SuccessRateLow)}-{EvaluationReport.Percent(r.SuccessRateHigh)}"
                : "95% CI -");

            Set(collisionsText, EvaluationReport.Percent(r.CollisionRate));
            Set(collisionsDetailText, $"{r.Collisions} of {r.Episodes}");

            Set(timeoutsText, EvaluationReport.Percent(r.TimeoutRate));
            Set(timeoutsDetailText, $"{r.TimedOut} of {r.Episodes}");

            Set(timeToGoalText, EvaluationReport.Seconds(r.AverageSecondsToGoal));

            Set(hitRockCountText, r.FailedHitRock.ToString("00"));
            Set(timedOutCountText, r.FailedTimedOut.ToString("00"));

            Set(changeLabelText, r.HasPrevious ? $"vs run {r.PreviousRunNumber}" : "First Run");
            Set(changeText, r.HasPrevious ? EvaluationReport.Points(r.SuccessChangePoints) : "-");

            if (tracker != null)
                tracker.SetFromReport(r);
        }

        private static void Set(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
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

            Set(successText, dash);
            Set(successDetailText, dash);
            Set(collisionsText, dash);
            Set(collisionsDetailText, dash);
            Set(timeoutsText, dash);
            Set(timeoutsDetailText, dash);
            Set(timeToGoalText, dash);

            Set(hitRockCountText, dash);
            Set(timedOutCountText, dash);

            Set(changeLabelText, "Since Previous Run");
            Set(changeText, dash);

            if (tracker != null)
                tracker.ClearEpisodes();
            Set(trackerLabel, dash);
        }
    }
}