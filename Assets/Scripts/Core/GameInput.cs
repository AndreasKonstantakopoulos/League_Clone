using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One place for input so the game works with either the new Input System or the legacy one.
public static class GameInput
{
    public static Vector2 MousePos
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    public static bool LeftClickDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    public static bool RightClickDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }
    }

    // c must be an upper-case letter A-Z (letter keys are contiguous in both enums).
    public static bool KeyDown(char c)
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[(Key)((int)Key.A + (c - 'A'))].wasPressedThisFrame;
#else
        return Input.GetKeyDown((KeyCode)((int)KeyCode.A + (c - 'A')));
#endif
    }

    // Mouse position projected onto the ground plane (y = 0).
    public static Vector3 MouseGround()
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        var ray = cam.ScreenPointToRay(MousePos);
        return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
    }
}
