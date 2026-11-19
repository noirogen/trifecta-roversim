using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem; // Required for the new Input System
using Rover;
using Training; // Make sure this matches your namespace

// Switches between the free camera and the rover camera (V key).
//  - When a rover appears (a training or evaluation rover, or the drivable rover from HumanRoverSpawner),
//    the view switches to its camera straight away. When that rover is removed, the view goes back to the
//    free camera until the next one appears.
//  - The keys follow the view: WASD/arrows pan the free camera only while it's active, and drive the
//    drivable rover only while you're looking through its camera.
public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public CinemachineCamera freeCam; // assign in inspector

    [Tooltip("Found automatically: the training or evaluation rover's camera, else the drivable rover's.")]
    public CinemachineCamera roverCam;

    [Tooltip("Switch to a rover's camera as soon as it appears.")]
    public bool switchToNewRovers = true;

    [Tooltip("WASD/arrows control only what you're looking at: the free camera, or the drivable rover.")]
    public bool keysFollowView = true;

    [Tooltip("Optional. The free camera's mover; found automatically.")]
    public RTSMapCamera freeCamMover;

    private const float SearchInterval = 0.2f;
    private bool isFreeCamActive = true;
    private float nextSearch;
    private HumanRoverSpawner humanRovers;

    [Tooltip("The first time a rover appears, move the free camera over it (once).")]
    public bool centerFreeCamOnFirstRover = true;

    [Tooltip("Offset from the rover when the free camera is centered on it. The camera's height is kept; " +
            "use Z to shift it back if your free camera looks down at an angle.")]
    public Vector3 firstFocusOffset = Vector3.zero;

    private bool hasCenteredFreeCam;

    void Start()
    {
        if (freeCamMover == null && freeCam != null)
            freeCamMover = freeCam.GetComponentInParent<RTSMapCamera>();
        if (freeCamMover == null)
            freeCamMover = FindAnyObjectByType<RTSMapCamera>();
        humanRovers = FindAnyObjectByType<HumanRoverSpawner>();

        // A camera assigned in the scene that isn't usable (e.g. on a disabled rover) is looked up again.
        if (roverCam != null && !roverCam.isActiveAndEnabled)
            roverCam = null;
        CenterFreeCamOnRover();
        ApplyView();
    }

    void Update()
    {
        // The rover was removed (Unity reports destroyed objects as null): look for the next one.
        if (roverCam == null && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + SearchInterval;
            CinemachineCamera found = FindRoverCamera();

            if (found != null)
            {
                roverCam = found;
                CenterFreeCamOnRover(); 
                if (switchToNewRovers)
                    isFreeCamActive = false;
                ApplyView();
            }
            else if (!isFreeCamActive)
            {
                isFreeCamActive = true;
                ApplyView();
            }
        }

        // 2. Handle the toggle when pressing 'V' using the new Input System
        if (roverCam != null && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFreeCamActive = !isFreeCamActive;
            ApplyView();
            Debug.Log(isFreeCamActive ? "Switched to Freecam" : "Switched to Rover Camera");
        }
    }

    // Priorities: the live camera gets 10, the other 0. Then hand the keys to whatever is in view.
    void ApplyView()
    {
        bool roverView = !isFreeCamActive && roverCam != null;

        if (roverCam != null)
            roverCam.Priority = roverView ? 10 : 0;
        if (freeCam != null)
            freeCam.Priority = roverView ? 0 : 10;

        if (!keysFollowView)
            return;

        if (freeCamMover != null)
            freeCamMover.enabled = !roverView;

        KeyboardDriveInput drive = DrivableInput(roverCam);
        if (drive != null)
            drive.enabled = roverView;
    }

    // The training or evaluation rover's camera while one exists, else the drivable rover's.
    CinemachineCamera FindRoverCamera()
    {
        var training = FindAnyObjectByType<TrainingInstance>();
        if (training != null && training.roverCamera != null)
            return training.roverCamera;

        var evaluation = FindAnyObjectByType<Evaluation.EvaluationInstance>();
        if (evaluation != null && evaluation.RoverCamera != null)
            return evaluation.RoverCamera;

        if (humanRovers == null)
            humanRovers = FindAnyObjectByType<HumanRoverSpawner>();
        return humanRovers != null ? humanRovers.CurrentCamera : null;
    }

    // The keyboard input of the rover a camera belongs to, if that's a drivable (non-AI) rover.
    // AI rovers keep theirs untouched (it's only used for heuristic testing).
    static KeyboardDriveInput DrivableInput(CinemachineCamera camera)
    {
        if (camera == null)
            return null;

        Transform root = camera.transform.root;
        if (root.GetComponentInChildren<RoverAIController>(true) != null)
            return null;

        return root.GetComponentInChildren<KeyboardDriveInput>(true);
    }

    // Puts the free camera over the rover, once. Keeps the camera's current height.
    void CenterFreeCamOnRover()
    {
        if (!centerFreeCamOnFirstRover || hasCenteredFreeCam || roverCam == null)
            return;

        Transform rig = freeCamMover != null ? freeCamMover.transform
                    : freeCam != null ? freeCam.transform
                    : null;
        if (rig == null)
            return;

        Vector3 rover = roverCam.transform.position; // the rover camera sits on the rover
        Vector3 p = rig.position;
        p.x = rover.x + firstFocusOffset.x;
        p.z = rover.z + firstFocusOffset.z;
        rig.position = p;

        hasCenteredFreeCam = true;
    }
}
