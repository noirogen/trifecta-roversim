using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapImageSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string id;        // "Jezero"
        public Sprite image;

        public TileGrid map;
        public GridBuilder grid;
    }

    public Image mapImage;       // the Image the grid sits on
    public List<Entry> entries;

    public void Show(string id)
    {
        foreach (var e in entries)
        {
            if (e.id != id) continue;

            mapImage.sprite = e.image;
            var fitter = mapImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.aspectRatio = e.image.rect.width / e.image.rect.height;
            return;
        }
        Debug.LogWarning($"No image for map '{id}'");
    }
}