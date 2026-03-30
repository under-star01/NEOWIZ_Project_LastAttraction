using UnityEngine;


public class SurvivorMove : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraRoot; // 카메라루트
    [SerializeField] private Camera playerCamera; // 카메라
    [SerializeField] private Transform modelRoot; // 모델루트

    [Header("속도")]
    [SerializeField] private float walkSpeed = 2f; //걷는 속도
    [SerializeField] private float runSpeed = 4f; // 달리는 속도
    [SerializeField] private float crouchSpeed = 1f; // 앉은 속도

    [Header("카메라")]
    [SerializeField] private float mouseSensitivity = 0.1f; // 마우스 감도
    [SerializeField] private float minPitch = -60f; // 마우스 최소 각
    [SerializeField] private float maxPitch = 60f; // 마우스 최대 각

    [Header("앉기")]
    [SerializeField] private float standHeight = 1.8f; // 일어선 높이
    [SerializeField] private float crouchHeight = 1.1f; // 앉은 높이

    private CharacterController controller;
    private SurvivorInput input;

    private float yaw;
    private float pitch;
    private float yVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<SurvivorInput>();

        yaw = transform.eulerAngles.y;
        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
    }

    private void Update()
    {
        Look();
        Move();
        Crouch();
    }

    private void Look()
    {
        Vector2 look = input.Look;

        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 moveInput = input.Move;

        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * moveInput.y + right * moveInput.x;
        if (move.magnitude > 1f)
            move.Normalize();

        float speed = walkSpeed;

        if (input.IsCrouching)
            speed = crouchSpeed;
        else if (input.IsRunning && moveInput.y > 0.1f)
            speed = runSpeed;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        move.y = yVelocity;
        controller.Move(move * speed * Time.deltaTime);

        if (moveInput.sqrMagnitude > 0.01f && modelRoot != null)
        {
            Vector3 lookDir = new Vector3(move.x, 0f, move.z);
            if (lookDir != Vector3.zero)
                modelRoot.forward = lookDir;
        }
    }

    private void Crouch()
    {
        if (input.IsCrouching)
            controller.height = crouchHeight;
        else
            controller.height = standHeight;

        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
    }
}