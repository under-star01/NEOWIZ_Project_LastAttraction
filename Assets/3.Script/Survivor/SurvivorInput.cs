using UnityEngine;
using UnityEngine.InputSystem;

public class SurvivorInput : MonoBehaviour
{
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction runAction;
    private InputAction crouchAction;
    private InputAction interactAction;

    public Vector2 Move => moveAction.ReadValue<Vector2>(); //WASD
    public Vector2 Look => lookAction.ReadValue<Vector2>(); //마우스
    public bool IsRunning => runAction.IsPressed(); // Shift
    public bool IsCrouching => crouchAction.IsPressed(); // Left Ctrl
    public bool IsInteracting => interactAction.IsPressed(); // 좌클릭

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        runAction = playerInput.actions["Run"];
        crouchAction = playerInput.actions["Crouch"];
        interactAction = playerInput.actions["Interact"];
    }
}