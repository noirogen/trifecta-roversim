using UnityEngine;

//I think this should be attached to the runTraining Run Simulation button
public class RunSimulation : MonoBehaviour
{
    public State activeMode;
    public void StartSim()
    {
        switch (activeMode)
        {
            case State.TRAIN:
                break;

            case State.EVALUATE:
                break;
        }
    }
}
