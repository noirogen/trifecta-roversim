using UnityEngine;
using SFB; // StandaloneFileBrowser
using System.IO;

public class ONNXFilePicker : MonoBehaviour
{
    public TMPro.TMP_Text[] outputPathLabel;
    public TMPro.TMP_Text[] outputPathFileName;
    public TMPro.TMP_Text[] selectedFileLabel;

    public int mode; //0 = training, 1 = evaluation 
    
    public void OpenONNXFolderPicker() //picking folder destination
    {
        // allow user to select a save location for the onnx file
        var folders = StandaloneFileBrowser.OpenFolderPanel("Select a save location", "", false);

        // empty result = user cancelled
        if (folders.Length == 0 || string.IsNullOrEmpty(folders[0]))
            return;

        string onnxOutputFolder = folders[0];

        if (outputPathLabel[mode] != null)
            outputPathLabel[mode].text = onnxOutputFolder;
    }

    public void OpenONNXFilePicker() //selecting a preexisting onnx file for training/evaluation
    {
        // provide a list of acceptable extensions
        var extensions = new[] { new ExtensionFilter("ONNX Model", "onnx") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select an ONNX Model", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string modelPath = paths[0];
            Debug.Log($"Selected ONNX file: {modelPath}");

            if (selectedFileLabel[mode] != null)
                selectedFileLabel[mode].text = modelPath;

            OnModelSelected(modelPath);
        }
        else
        {
            Debug.Log("File selection cancelled.");
        }
    }


    void OnModelSelected(string path)
    {
        //Not sure where to load it yet 
        // but the model must be loaded from its file into the sim
    }

    public string GetOutputPath()
    {
        return outputPathLabel[mode].text;
    }
    public string GetFileName()
    {
        return outputPathFileName[mode].text;
    }

    public string GetFullPath()
    {
        return outputPathLabel[mode].text + outputPathFileName[mode].text;
    }
    
}
