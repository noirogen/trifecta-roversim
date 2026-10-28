using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridBuilder : MonoBehaviour
{
    public RectTransform area;          // the map image
    public int cols = 12, rows = 8;
    public float lineThickness = 1f;
    public Color lineColor  = new Color(0.93f, 0.90f, 0.83f, 0.22f);
    public Color labelColor = new Color(0.55f, 0.53f, 0.45f, 1f);
    public float labelSize = 18f;       // height of column labels / width basis for row labels
    public float labelGap  = 6f;        // space between the image and the labels
    public TMP_FontAsset font;          // optional

    RectTransform container;

    void Start()
    {
        Canvas.ForceUpdateCanvases();   // make sure area.rect has its final size
        Build();
    }

    public void Build()
    {
        if (container != null) Destroy(container.gameObject);

        container = new GameObject("GridOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(area, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = container.offsetMax = Vector2.zero;

        float totalW = area.rect.width, totalH = area.rect.height;
        float w = totalW / cols, h = totalH / rows;

        // lines (cols + 1 vertical, rows + 1 horizontal)
        for (int i = 0; i <= cols; i++)
            MakeLine("V" + i, new Vector2(i * w - lineThickness * 0.5f, 0),
                              new Vector2(lineThickness, totalH));
        for (int j = 0; j <= rows; j++)
            MakeLine("H" + j, new Vector2(0, -(j * h) + lineThickness * 0.5f),
                              new Vector2(totalW, lineThickness));

        // column letters above the image
        for (int i = 0; i < cols; i++)
            MakeLabel(((char)('A' + i)).ToString(),
                      new Vector2(i * w + w * 0.5f, labelGap), new Vector2(0.5f, 0f),
                      new Vector2(w, labelSize), TextAlignmentOptions.Center);

        // row numbers left of the image
        for (int j = 0; j < rows; j++)
            MakeLabel((j + 1).ToString(),
                      new Vector2(-labelGap, -(j * h + h * 0.5f)), new Vector2(1f, 0.5f),
                      new Vector2(labelSize * 1.5f, h), TextAlignmentOptions.MidlineRight);
    }

    void MakeLine(string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(container, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = lineColor;
        img.raycastTarget = false;
    }

    void MakeLabel(string text, Vector2 pos, Vector2 pivot, Vector2 size, TextAlignmentOptions align)
    {
        var go = new GameObject("Label " + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(container, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 12;
        t.color = labelColor;
        t.alignment = align;
        t.raycastTarget = false;
        if (font != null) t.font = font;
    }
}