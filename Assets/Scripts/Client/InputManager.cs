using UnityEngine;
using UnityEngine.InputSystem;  
using Lockstep.Packets;

public class InputManager : MonoBehaviour
{
    public bool HasInput() => hasInput;
    bool hasInput = false;
    public Position currentInput;
    Vector3 pos => transform.position;
    int groundLayerMask = 1 << 3;

    public bool ReadInput(out Position inputPos)
    {
        if (hasInput)
        {
            hasInput = false; // Reset input flag after reading
            inputPos = currentInput;
            return true; // Return the current input position
        }
        else
        {
            inputPos = default; // Return default value if no input is available
            return false;
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
                currentInput = new Position
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