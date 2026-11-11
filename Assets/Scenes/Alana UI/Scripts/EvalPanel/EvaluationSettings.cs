using TMPro;
//using Evaluation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Holds the training panel's settings.
// TrainingRequestBuilder and TrainingOrganizer read these fields by name,
// so keep the field names in sync with them.

public class EvaluationSettings : MonoBehaviour
{  
    //public EvaluationDefaults defaults;
    [Header("UI elements")]
    public TMP_Text ModelText;
    public Slider ObstacleDensitySlider; 
    public TMP_InputField NumberEpisodes;
    public TMP_Text StartingSeedText;

    [Header("Variables")]
    //Default values for now 
    public string SelectedModel = "None";
    public int EpisodeCount = 5000;
    public bool RandomPosition = true;
    public float ObstacleDensity = 5;
    public bool RocksOn; //set to 0 if obstacle density is 0
    public Vector3 StartPos;
    public Vector3 GoalPos;
    public int StartingSeed;
    public string ModelName;
    private int RunID; //?

    // not sure how to handle runspeed, so it's just there
    //note, the selected map is not kept as a setting because it's handled by load map
    // though it might be a good idea to put it in here... I'm not sure because my brain is EMPTY!


    public void Start()
    {
        Refresh();
    }
    public void SetEpisodes(string text)
    {
        if (int.TryParse(text, out int n))
            EpisodeCount = n;
    } 
    public void SetSeed()
    {
        //random
        StartingSeed = Random.Range(0, int.MaxValue);
        StartingSeedText.text = StartingSeed.ToString();
    }
    public void SetObstacleDensity(float value)
    {
        ObstacleDensity = value;
        SetRocks();
    } 
 
    private void SetRocks()
    {
        RocksOn = ObstacleDensity == 0.0f? false : true;
    }
 
    // Vector3 can't be passed from an Inspector UI event, so call these from code
    // (for example when the user clicks the terrain).
    public void SetStartPosition(Vector3 position) => StartPos = position;
 
    public void SetGoalPosition(Vector3 position) => GoalPos = position;
 
    // public void SetModelName(string name) => ModelName = name;

    public void Refresh()
    {
        //ideally replace with the default values set in evaluationDefaults rather than reassigning them to their current values
        SetRocks();
        ModelText.text = "None";
        ObstacleDensitySlider.SetValueWithoutNotify(ObstacleDensity);
        NumberEpisodes.SetTextWithoutNotify(EpisodeCount.ToString());
        SetSeed();
        // StartingSeedText.text = StartingSeed.ToString();
    }


}