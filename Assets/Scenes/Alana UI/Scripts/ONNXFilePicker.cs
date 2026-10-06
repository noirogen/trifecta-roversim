using UnityEngine;
using SFB; // StandaloneFileBrowser

public class ONNXFilePicker : MonoBehaviour
{
    public TMPro.TMP_Text selectedPathLabel;
    public void OpenONNXFilePicker()
    {
        // provide a list of acceptable extensions
        var extensions = new[] { new ExtensionFilter("ONNX Model", "onnx") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select ONNX Model", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string modelPath = paths[0];
            Debug.Log($"Selected ONNX file: {modelPath}");

            if (selectedPathLabel != null)
                selectedPathLabel.text = modelPath;

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
    }
}
