using UnityEngine;
using UnityEngine.InputSystem;

public class SurvivorInput : MonoBehaviour
{
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction runAction;
    private InputAction crouchAction;
    private InputAction interactAction1;
    private InputAction interactAction2;

    public Vector2 Move => moveAction.ReadValue<Vector2>(); //WASD
    public Vector2 Look => lookAction.ReadValue<Vector2>(); //마우스
    public bool IsRunning => runAction.IsPressed(); // Shift
    public bool IsCrouching => crouchAction.IsPressed(); // Left Ctrl
    public bool IsInteracting1 => interactAction1.IsPressed(); // 좌클릭
    public bool IsInteracting2 => interactAction2.WasPressedThisFrame(); // SPACE

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        runAction = playerInput.actions["Run"];
        crouchAction = playerInput.actions["Crouch"];
        interactAction1 = playerInput.actions["Interact1"];
        interactAction2 = playerInput.actions["Interact2"];
    }
}