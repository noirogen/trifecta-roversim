using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem; // Required for the new Input System
using Training; // Make sure this matches your namespace

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public CinemachineCamera freeCam; // assign in inspector

    private TrainingInstance currentTrainingInstance;
    public CinemachineCamera roverCam;
    private bool isFreeCamActive = true;

    void Update()
    {
        // 1. If we haven't grabbed the rover camera yet, look for it in the scene
        if (roverCam == null)
        {
            currentTrainingInstance = FindFirstObjectByType<TrainingInstance>();
            if (currentTrainingInstance != null && currentTrainingInstance.roverCamera != null)
            {
                roverCam = currentTrainingInstance.roverCamera;
                
                // Set initial priorities: Rover cam standby (0), Freecam active (10)
                roverCam.Priority = 0;
                freeCam.Priority = 10;
            }
        }

        // 2. Handle the toggle when pressing 'V' using the new Input System
        if (roverCam != null && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFreeCamActive = !isFreeCamActive;

            if (isFreeCamActive)
            {
                // Switch to Freecam
                roverCam.Priority = 0;
                freeCam.Priority = 10;
                Debug.Log("Switched to Freecam");
            }
            else
            {
                // Switch back to Rover Cam
                roverCam.Priority = 10;
                freeCam.Priority = 0;
                Debug.Log("Switched to Rover Camera");
            }
        }
    }
}