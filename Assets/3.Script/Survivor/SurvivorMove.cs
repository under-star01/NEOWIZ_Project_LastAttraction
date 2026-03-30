using UnityEngine;

public class SurvivorMove : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraYawRoot; // 카메라 좌우 회전 루트
    [SerializeField] private Transform cameraPitchRoot; // 카메라 상하 회전 루트
    [SerializeField] private Camera playerCamera; // 카메라
    [SerializeField] private Transform modelRoot; // 모델루트

    [Header("속도")]
    [SerializeField] private float walkSpeed = 2f; //걷는 속도
    [SerializeField] private float runSpeed = 4f; // 달리는 속도
    [SerializeField] private float crouchSpeed = 1f; // 앉은 속도
    [SerializeField] private float turnSpeed = 15f; // 몸 회전 속도

    [Header("카메라")]
    [SerializeField] private float mouseSensitivity = 0.1f; // 마우스 감도
    [SerializeField] private float minPitch = -60f; // 마우스 최소 각
    [SerializeField] private float maxPitch = 60f; // 마우스 최대 각

    [Header("앉기")]
    [SerializeField] private float standHeight = 1.8f; // 일어선 높이
    [SerializeField] private float crouchHeight = 1.1f; // 앉은 높이

    private CharacterController controller;
    private SurvivorInput input;

    private float cameraYaw;
    private float pitch;
    private float yVelocity;
    private bool isMoveLocked;

    public void SetMoveLock(bool value)
    {
        isMoveLocked = value;
    }

    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) return;

        if (modelRoot != null)
            modelRoot.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<SurvivorInput>();

        controller.height = standHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Look();

        if (isMoveLocked)
        {
            ApplyGravityOnly();
            return;
        }

        Move();
        Crouch();
    }

    private void Look()
    {
        Vector2 look = input.Look;

        cameraYaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraYawRoot != null)
            cameraYawRoot.localRotation = Quaternion.Euler(0f, cameraYaw, 0f);

        if (cameraPitchRoot != null)
            cameraPitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
        else if (input.IsRunning)
            speed = runSpeed;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 finalMove = move;
        finalMove.y = yVelocity;
        controller.Move(finalMove * speed * Time.deltaTime);

        if (move.sqrMagnitude > 0.001f && modelRoot != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(move);

            modelRoot.rotation = Quaternion.Slerp(
                modelRoot.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }
    }

    private void ApplyGravityOnly()
    {
        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 gravityMove = new Vector3(0f, yVelocity, 0f);
        controller.Move(gravityMove * Time.deltaTime);
    }

    private void Crouch()
    {
        if (input.IsCrouching)
            controller.height = crouchHeight;
        else
            controller.height = standHeight;
    }
}