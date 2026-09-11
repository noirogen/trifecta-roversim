using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class OptionsMenu : MonoBehaviour
{

    public void BackToMain()
    {
        SceneManager.LoadSceneAsync(0); 
    }

}
