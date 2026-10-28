using UnityEngine;
using UnityEngine.EventSystems;

public class DeselectOnClick : MonoBehaviour, IPointerClickHandler
{
    public TileGrid grid;

    public void OnPointerClick(PointerEventData e) => grid.Deselect();
}