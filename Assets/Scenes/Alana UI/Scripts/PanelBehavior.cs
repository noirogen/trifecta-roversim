using System.Resources;
using UnityEngine;

public class PanelBehavior : MonoBehaviour
{
    //toggle between 2 panels
    public State simMode;
    public GameObject Panel1;
    public GameObject Panel2;
    public GameObject panel;
    public void TogglePanel1()
    {
        Panel1.SetActive(true);
        Panel2.SetActive(false);
    }

    public void TogglePanel2()
    {
        Panel2.SetActive(true);
        Panel1.SetActive(false);
    }

    // closing and opening 1 panel
    
    public void ShowPanel()
    {
        panel.SetActive(true);
    }
    public void HidePanel()
    {
        panel.SetActive(false);
    }

    public void setMode(int mode) // 0 or 1
    {
        if (System.Enum.IsDefined(typeof(State), mode)){
            simMode = (State)mode;
            Debug.Log($"Currently in {simMode} mode");
        }
        else
        {
            Debug.LogWarning($"[PanelBehavior] Invalid mode index: {mode}. Must be 0 (TRAIN) or 1 (EVALUATE).");
        }
    }




}
public enum State {TRAIN, EVALUATE}; //{0, 1} I think?
