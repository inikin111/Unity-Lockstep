using UnityEngine;
using UnityEngine.InputSystem;  
using Lockstep.Network;

public class InputManager : MonoBehaviour
{
    public InputType ReadInput()
    {
        InputType result = InputType.None; // Default input
        if (Keyboard.current.wKey.isPressed)
            result = InputType.Up;
        if (Keyboard.current.sKey.isPressed)
            result = InputType.Down;
        if (Keyboard.current.aKey.isPressed)
            result = InputType.Left;
        if (Keyboard.current.dKey.isPressed)
            result = InputType.Right;

        return result; // Default input
    }
}