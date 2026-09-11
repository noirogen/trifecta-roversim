using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class EvaluateMenu : MonoBehaviour
{

    public void StartEvaluating()
    {
        SceneManager.LoadSceneAsync(5); 
    }

    public void BackToMain()
    {
        SceneManager.LoadSceneAsync(0);
    }
}
