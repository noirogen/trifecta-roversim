using UnityEngine;

public class RoverPositionSelector : MonoBehaviour
{
    public Camera cam;
    public LayerMask terrainLayer;
    
    bool positionSet = false;
    private Vector3? startPosition;
    private Vector3? endPosition;
    public float radius;

    void Update()
    {
        // Check for Shift + Left Click
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer))
            {
                HandlePointSelection(hit.point);
            }
        }
    }

    void HandlePointSelection(Vector3 clickedPoint)
    {

        if (startPosition == null)
        {
            startPosition = clickedPoint;
            Debug.Log($"Start Position Set: {startPosition}");
        }
        else if (endPosition == null)
        {
            endPosition = clickedPoint;
            Debug.Log($"End Position Set: {endPosition}");
            // Trigger popup UI window here
            positionSet = true;
        }
    }


}