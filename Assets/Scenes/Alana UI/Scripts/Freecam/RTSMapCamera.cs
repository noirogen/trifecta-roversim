using UnityEngine;
using UnityEngine.InputSystem;
// Make sure to include this namespace if using the new Input System classes directly:
// using UnityEngine.InputSystem; 

public class RTSMapCamera : MonoBehaviour
{
    public float moveSpeed = 20f;

    void Update()
    {
        // Replace Input.GetAxis with new Input System checks:
        float moveX = 0f;
        float moveZ = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;
        }

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ).normalized;
        transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
    }
}