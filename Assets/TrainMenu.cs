using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class TrainMenu : MonoBehaviour
{

    public void StartTraining()
    {
        SceneManager.LoadSceneAsync(4); 
    }
    public void BackToMain()
    {
        SceneManager.LoadSceneAsync(0);
    }
}
