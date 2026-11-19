using Evaluation;
using Training;
using UnityEngine;
public class SettingsLock : MonoBehaviour
{
    [SerializeField] private CanvasGroup settingsGroup;
    [SerializeField] private TrainingOrganizer trainingOrganizer;
    [SerializeField] private EvaluationOrganizer evaluationOrganizer;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.5f;

    private void Awake()
    {
        if (trainingOrganizer == null) trainingOrganizer = FindAnyObjectByType<TrainingOrganizer>();
        if (evaluationOrganizer == null) evaluationOrganizer = FindAnyObjectByType<EvaluationOrganizer>();
    }

    private void OnEnable()
    {
        if (trainingOrganizer != null) trainingOrganizer.StateChanged += OnState;
        if (evaluationOrganizer != null) evaluationOrganizer.StateChanged += OnState;
        Apply(false);
    }

    private void OnDisable()
    {
        if (trainingOrganizer != null) trainingOrganizer.StateChanged -= OnState;
        if (evaluationOrganizer != null) evaluationOrganizer.StateChanged -= OnState;
    }

    private void OnState(TrainingState state) => Apply(state != TrainingState.Idle);

    private void Apply(bool locked)
    {
        if (settingsGroup == null) return;
        settingsGroup.interactable = !locked;
        settingsGroup.alpha = locked ? lockedAlpha : 1f;
    }
}