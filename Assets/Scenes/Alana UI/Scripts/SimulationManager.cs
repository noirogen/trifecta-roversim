using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.AI;

/*
Simulation manager stores variables and initializes each new run, calls functions from the required scripts and manages states
in progress
variables keep changing unforch
*/

// this is the data container, contains the parameter settings
[Serializable]
public class SimulationSettings
{

    [Header("Neural Network")]
    public int networkArchitecture;
    public int maxIterations;
    public bool earlyStopping;

    [Header("Map")]
    public string mapName;
    public string chunkFileName;     // e.g. "tile_0_1.raw" or maybe the name of the larger map?
    public float terrainHeightY;     // from _tile_summary.csv, for setting the height of the terrain
    public float obstacleDensity;
    public bool rocks = true, pits = true, craters = true, items = true;

    [Header("Mode")]
    public State simMode; // true = train, false = run a saved model
    public bool simulationActive;
    public string loadedModelPath;   // .onnx picked with "Load Model" (run mode)

    [Header("Training output")]
    public string checkpointOutputFolder;
    public string onnxOutputFolder;
    public string onnxFileName = "trained_model";
    public string behaviorName = "RoverAgent";

    [Header("Starting Rover Position")]
    public Vector3 roverStartPosition;
    public GameObject position;

    
    public bool Validate(out string error)
    {
        if (string.IsNullOrEmpty(chunkFileName))
        { error = "No map chunk selected."; return false; }

        if ((int)simMode  == 1 && string.IsNullOrEmpty(checkpointOutputFolder))
        { error = "Pick a checkpoint output folder before training."; return false; }

        if (!((int)simMode == 1) && string.IsNullOrEmpty(loadedModelPath))
        { error = "Load a trained model before running."; return false; }

        if (string.IsNullOrEmpty(loadedModelPath) || string.IsNullOrEmpty(loadedModelPath))
        { error = "Please choose a map and a tile before running"; return false;}

        error = null;
        return true;
    }

}

public enum State {TRAIN, EVALUATE};

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    public SimulationSettings settings = new SimulationSettings();

    [Header("Optional UI")]
    public TMPro.TMP_Text statusLabel;
    public void StartSimulation()
    {
        // if (!settings.Validate(out string error))
        // {
        //     SetStatus(error);
        //     return;
        // }

        Debug.Log("StartSimulation called");

        // pass settings to items that do the work, have public game objects, call functions with parameters likely
        //   terrainLoader.LoadChunk(settings.chunkFileName, settings.terrainHeightY);
        //   rover.transform.position = settings.roverStartPosition;
        //   if (settings.trainingMode) { /* launch / connect trainer */ }
        //   else { /* load settings.loadedModelPath into the agent */ }

        //SetStatus($"Starting on {settings.mapName} / {settings.chunkFileName}");
    }

    void SetStatus(string message)
    {
        Debug.Log(message);
        if (statusLabel != null) statusLabel.text = message;
    }

    public void SetMode(int mode) // 0 or 1
    {
        if (System.Enum.IsDefined(typeof(State), mode)){
            settings.simMode = (State)mode;
            Debug.Log($"Currently in {settings.simMode} mode");
        }
    }

    public void SetArchitecture(int index)   
    { 
        settings.networkArchitecture = index; 
        Debug.Log("Selection: "+ index);
        Debug.Log("Settings.NetworkArch set to: " + settings.networkArchitecture);
    }
    public void SetEarlyStopping(float on)    
    {   
        bool res = false;

        if (on == 0.0f) {
            res = false;   
        }

        if (on == 1.0f)
            res = true;

        settings.earlyStopping = res;
        string message = res ? "on" : "off";
        Debug.Log($"Early Stopping toggled {message}");

        string setting = settings.earlyStopping?"on" : "off";
        Debug.Log($"Early Stopping setting set to: {setting}");
    }
    public void SetDensity(float value)      
    { 
        Debug.Log($"Obstacle Density set to {value}");
        settings.obstacleDensity = value; 

        Debug.Log($"Obstacle Density set to: {settings.obstacleDensity}");
    }
    public void SetRocks(bool on)            
    { 
        settings.rocks = on; 
        string message = on ? "on" : "off";
        string status = on ? "on" : "off";
        
        Debug.Log($"Rocks toggled {message}");
        Debug.Log($"Settings rocks saved to: {status}");
    }
    public void SetPits(bool on)             
    { 
        settings.pits = on; 
        string message = on ? "on" : "off";
        string status = on ? "on" : "off";
        
        Debug.Log($"Pits toggled {message}");
        Debug.Log($"Settings Pits saved to: {status}");
    }
    public void SetCraters(bool on)          
    { 
        settings.craters = on; 
        string message = on ? "on" : "off";
        string status = on ? "on" : "off";
        
        Debug.Log($"Craters toggled {message}");
        Debug.Log($"Settings Craters saved to: {status}");
    }
    public void SetItems(bool on)            
    { 
        settings.items = on; 
        string message = on ? "on" : "off";
        string status = on ? "on" : "off";
        
        Debug.Log($"Items toggled {message}");
        Debug.Log($"Settings Items saved to: {status}");
    }

    public void SetIterations(string text)
    {
        Debug.Log("SetIterations input: " + text);
        if (int.TryParse(text, out int n)){
            settings.maxIterations = Mathf.Clamp(n, 1, 5000);
            Debug.Log("settings.maxIterations set to: " + settings.maxIterations);
        }
        else
            Debug.Log("This value cannot be converted to an integer.");
        
    }
}

