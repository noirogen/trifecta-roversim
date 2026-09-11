using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{

    public void TrainMenu()
    {
        SceneManager.LoadSceneAsync(2); 
    }

    public void EvaluateMenu()
    {
        SceneManager.LoadSceneAsync(3);
    }

    public void OptionsMenu()
    {
        SceneManager.LoadSceneAsync(1);
    }

    public void Quit()
    {
    #if UNITY_EDITOR
        bool confirm = UnityEditor.EditorUtility.DisplayDialog("Confirm Quit", "Are you sure you want to quit?", "Yes", "No");
        if (confirm)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
    #else
        Application.Quit();
    #endif
    }
}
