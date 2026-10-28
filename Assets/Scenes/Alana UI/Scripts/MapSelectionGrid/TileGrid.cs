using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TileGrid : MonoBehaviour,
    IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
{
    // Tracking tiles
    private string currMap = "";
    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public string selectedTile = "";     // example "C6", or "" if nothing is selected, all game object tiles must be renamed to this format, arduous unfortunately
    int selected = -1;

    // Grid
    public RectTransform area;           // the map image's RectTransform, used to determine the sizing of the squares
    public Image highlight;             
    public Image selection;              
    public int cols, rows;
    bool[] available;                   
    
    

    // Message under the map
    readonly string[] message =
    {
        "Hover over a cell to check whether its terrain data is available.",
        "Data available, click to select.",
        "Unavailable, no data for this tile.",
        " Selected"
    };
    public TMPro.TMP_Text infoMessage;
    public TMPro.TMP_Text toBeLoaded;

    static readonly Color Ok = new Color(0.47f, 0.80f, 0.51f, 0.42f);
    static readonly Color No = new Color(0.93f, 0.43f, 0.39f, 0.42f);

    void Awake()
    {
        if (available == null) available = new bool[cols * rows];

        foreach (var img in new[] { highlight, selection })
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            img.enabled = false;
        }
        infoMessage.text = message[0];
        
        //currMap = just jumping all over man shit 
    }

    public string LabelFor(int i) => $"{alphabet[i % cols]}{i / cols + 1}";   // 0 -> "A1"

    int IndexFor(string label)                                               // "C6" -> index, or -1
    {
        if (string.IsNullOrEmpty(label) || label.Length < 2) return -1;
        int col = alphabet.IndexOf(label[0]);
        if (col < 0 || col >= cols) return -1;
        if (!int.TryParse(label.Substring(1), out int rowNum)) return -1;
        int row = rowNum - 1;
        if (row < 0 || row >= rows) return -1;
        return row * cols + col;
    }

    // LoadMap calls this with the labels that have terrain, e.g. "A1", "C6"
    public void SetAvailable(IEnumerable<string> labels)
    {
        available = new bool[cols * rows];
        foreach (var l in labels)
        {
            int i = IndexFor(l);
            if (i >= 0) available[i] = true;
        }
        Deselect();
    }

    int TileAt(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            area, e.position, e.enterEventCamera, out var p);
        var r = area.rect;
        float u = (p.x - r.xMin) / r.width;
        float v = (r.yMax - p.y) / r.height;
        if (u < 0 || u >= 1 || v < 0 || v >= 1) return -1;
        return (int)(v * rows) * cols + (int)(u * cols);
    }

    void Place(Image img, int i)
    {
        var r = area.rect;
        float w = r.width / cols, h = r.height / rows;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2((i % cols) * w, -(i / cols) * h);
    }

    public void OnPointerMove(PointerEventData e)
    {
        int i = TileAt(e);
        highlight.enabled = i >= 0;
        if (i < 0) return;
        highlight.color = available[i] ? Ok : No;
        string label = LabelFor(i);
        infoMessage.text = available[i] ? message[1] : message[2];
        Place(highlight, i);
    }

    public void OnPointerExit(PointerEventData e)
    {
        highlight.enabled = false;
        infoMessage.text = selected >= 0 ? $"{selectedTile}{message[3]}" : message[0];
    }

    public void OnPointerClick(PointerEventData e)
    {
        int i = TileAt(e);
        SelectionRuntime(i);
    }

    public void Deselect() //reset function pretty much
    {
        selected = -1;
        selectedTile = "";
        selection.enabled = false;
        infoMessage.text = message[0];
        toBeLoaded.text = $"No tile selected.";
    }

    public void setCurrentMap(string m)
    {
        currMap = m;
    }

    public void SelectionRuntime(int i)
    {
        if (i < 0 || !available[i]) return;
        selected = i;
        selectedTile = LabelFor(i);
        selection.enabled = true;
        infoMessage.text = $"{selectedTile}{message[3]}";
        toBeLoaded.text = $"{currMap} {selectedTile} selected.";
        Place(selection, i);
    }

    public void selectFromLabel(string label)
    {
        int i = IndexFor(label);
        SelectionRuntime(i);
    }
}