using TMPro;
using Training;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Holds the training panel's settings.
// TrainingRequestBuilder and TrainingOrganizer read these fields by name,
// so keep the field names in sync with them.

public class TrainingSettings : MonoBehaviour
{
    [Header("Default Training Values")]
    // public TrainingDefaults defaults;
    /*
        return new TrainingRequest
    {
        NewTileEachEpoch = defaults.NewTileEachEpoch,
        RocksEnabled = ui.rocks,
        RockDensity = Mathf.Clamp01(ui.obstacleDensity / defaults.ObstacleSliderMax) * defaults.MaxRockDensity,
        EpochSeconds = defaults.EpochMinutes * 60f,
        EpisodeSeconds = defaults.EpisodeSeconds,
        IterationUnit = defaults.IterationUnit,
        MaxIterations = iterations,
        NetworkPreset = trainerConfig.PresetAt(ui.networkArchitecture).Name,
        EarlyStopping = ui.earlyStopping,
        ResultsDirectory = resultsDirectory,
        ModelOutputPath = Path.Combine(modelFolder, modelName + ".onnx"),
        RunName = modelName,
        RunId = PythonPaths.MakeRunId(modelName),
        UseFixedSeed = defaults.UseFixedSeed,
        Seed = defaults.Seed
    };
    */

    public TrainingDefaults defaults;

    public TMP_Dropdown NADropdown;
    public TMP_InputField IterInput;
    public Slider EarlyStopSlider;
    public Slider SuccessSlider;
    public Slider FixedSeedToggle;
    public Slider EpochToggle;
    public Slider ObstacleDensitySlider;
    public TMP_Text Folder;
    public TMP_Text OutputFileText;


    public void Start()
    {
        Refresh();
    }
    private string[] architectureNames = {"Small","Medium","Large"};
    public string NetworkArchitecture = "Small";
    public int Iterations = 5000;

    public bool EarlyStopping = true;
    public float SuccessTarget = 0.9f;
    
    public bool FixedSeed = true;

    public bool NewTileEachEpoch = true;
    public float EpochMinutes = 10f;
    public float EpisodeSeconds = 60f;
    public float ObstacleDensity = 5;

    public bool RocksOn; //set to 0 if obstacle density is 0

    public bool RandomPosition;

    public Vector3 StartPos;

    public Vector3 GoalPos;

    public string ModelOutputPath;
    
    public string ModelName;

    public void SetNetworkArchitecture(string name) => NetworkArchitecture = name;
 
    // For a dropdown, which sends the selected option's index.
    public void SetNetworkArchitectureIndex(int index)
    {
        if (architectureNames != null && index >= 0 && index < architectureNames.Length)
            NetworkArchitecture = architectureNames[index];
    }
 
    // For an input field, which sends text.
    public void SetIterations(string text)
    {
        if (int.TryParse(text, out int n))
            Iterations = n;
    }

    public void SetEarlyStopping(float a)    
    {   
        NewTileEachEpoch = a != 0.0f;  
    }
 
    // For a 0 to 1 slider.
    public void SetSuccessTarget(float value) => SuccessTarget = value;
 
    // For a 0 to 100 slider.
    public void SetSuccessTargetPercent(float percent) => SuccessTarget = percent / 100f;// a decimal?
 
    public void SetFixedSeed(float a)
    {
        FixedSeed = a != 0.0f;  
    }
 
    public void SetNewTileEachEpoch(float a)
    {
        NewTileEachEpoch = a != 0.0f;  
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
 
    public void SetModelOutputPath(string path) => ModelOutputPath = path;
 
    // public void SetModelName(string name) => ModelName = name;

    public void Refresh()
    {   
        //ideally replace with the default values set in trainingDefaults rather than reassigning them to their current values
        int i = NADropdown.options.FindIndex(o => o.text == NetworkArchitecture);
        if (i >= 0) NADropdown.SetValueWithoutNotify(i);

        IterInput.SetTextWithoutNotify(Iterations.ToString());
        EarlyStopSlider.SetValueWithoutNotify(EarlyStopping ? 1f : 0f);
        SuccessSlider.SetValueWithoutNotify(SuccessTarget);
        FixedSeedToggle.SetValueWithoutNotify(FixedSeed?1:0);
        EpochToggle.SetValueWithoutNotify(NewTileEachEpoch?1:0);
        // FixedSeedToggle.SetIsOnWithoutNotify(FixedSeed);
        // EpochToggle.SetIsOnWithoutNotify(NewTileEachEpoch);
        ObstacleDensitySlider.SetValueWithoutNotify(ObstacleDensity);
        Folder.text = ModelOutputPath;
        OutputFileText.text = ModelName;
    }


}