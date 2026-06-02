using UnityEngine;
using UnityEngine.InputSystem;  

public class InputManager : MonoBehaviour
{
    public bool HasInput() => hasInput;
    bool hasInput = false;
    public Vector3i currentInput;
    Vector3 pos => transform.position;
    int groundLayerMask = 1 << 3;

    public bool ReadInput(out Vector3i inputPos)
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
                currentInput = clickPosition.WithY(transform.position.y).ToVector3i();
                hasInput = true;
            }
        }
    }
}