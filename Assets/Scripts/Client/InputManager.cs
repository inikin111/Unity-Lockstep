using UnityEngine;
using UnityEngine.InputSystem;  
using Lockstep.Packets;

public class InputManager : MonoBehaviour
{
    public bool HasInput() => hasInput;
    bool hasInput = false;
    public InputPosition currentInput;
    Vector3 pos => transform.position;
    int groundLayerMask = 1 << 3;

    // Make sure to call this method only when HasInput() returns true to avoid reading stale input
    public InputPosition ReadInput()
    {
        if (hasInput)
        {
            hasInput = false; // Reset input flag after reading
            return currentInput; // Return the current input position
        }
        else
        {
            return new InputPosition
            {
                x = Mathf.RoundToInt(pos.x * 1000),
                y = Mathf.RoundToInt(pos.y * 1000),
                z = Mathf.RoundToInt(pos.z * 1000)
            };
        }
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
            {
                Vector3 clickPosition = hit.point;
                currentInput = new InputPosition
                {
                    x = Mathf.RoundToInt(clickPosition.x * 1000),
                    y = Mathf.RoundToInt(clickPosition.y * 1000),
                    z = Mathf.RoundToInt(clickPosition.z * 1000)
                };
                hasInput = true;
            }
        }
    }
}