using Mirror;
using UnityEngine;

public class SurvivorMove : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraYawRoot;
    [SerializeField] private Transform cameraPitchRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Animator animator;

    [Header("속도")]
    [SerializeField] private float walkSpeed = 2.3f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float crouchSpeed = 1.2f;
    [SerializeField] private float crawlSpeed = 0.45f;
    [SerializeField] private float turnSpeed = 15f;

    [Header("카메라")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    [Header("컨트롤러 높이")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private Vector3 standCenter = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private float crouchHeight = 0.9f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0f, 0.45f, 0f);

    private CharacterController controller;
    private SurvivorInput input;
    private SurvivorInteractor interactor;
    private SurvivorState state;

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

        if (dir.sqrMagnitude <= 0.001f)
            return;

        if (modelRoot != null)
            modelRoot.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<SurvivorInput>();
        interactor = GetComponent<SurvivorInteractor>();
        state = GetComponent<SurvivorState>();

        if (animator == null && modelRoot != null)
            animator = modelRoot.GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public override void OnStartLocalPlayer()
    {
        if (playerCamera != null)
            playerCamera.enabled = true;

        AudioListener listener = GetComponentInChildren<AudioListener>();
        if (listener != null)
            listener.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer)
        {
            if (playerCamera != null)
                playerCamera.enabled = false;

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        if (controller == null || !controller.enabled)
            return;

        Look();

        bool isDowned = state != null && state.IsDowned;
        bool isBusy = state != null && state.IsBusy;

        if (isMoveLocked || isBusy)
        {
            ApplyGravityOnly();
            UpdateAnimator(0f, false, isDowned);
            return;
        }

        if (isDowned)
        {
            CrawlMove();
            return;
        }

        bool canCrouch = interactor == null || !interactor.IsInteracting;
        bool isCrouching = canCrouch && input != null && input.IsCrouching;

        if (isCrouching)
            SetSize(crouchHeight, crouchCenter);
        else
            SetSize(standHeight, standCenter);

        Move(isCrouching);
    }

    private void Look()
    {
        if (input == null)
            return;

        Vector2 look = input.Look;

        cameraYaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraYawRoot != null)
            cameraYawRoot.localRotation = Quaternion.Euler(0f, cameraYaw, 0f);

        if (cameraPitchRoot != null)
            cameraPitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move(bool isCrouching)
    {
        if (input == null || playerCamera == null)
            return;

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

        bool isMoving = move.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && !isCrouching && input.IsRunning;

        float speed = walkSpeed;
        float animSpeed = 0f;

        if (isCrouching)
        {
            speed = crouchSpeed;
            if (isMoving) animSpeed = 0.25f;
        }
        else if (isRunning)
        {
            speed = runSpeed;
            animSpeed = 1f;
        }
        else if (isMoving)
        {
            speed = walkSpeed;
            animSpeed = 0.5f;
        }

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 finalMove = move * speed;
        finalMove.y = yVelocity;

        controller.Move(finalMove * Time.deltaTime);

        RotateModel(move, isMoving);
        UpdateAnimator(animSpeed, isCrouching, false);
    }

    private void CrawlMove()
    {
        if (input == null || playerCamera == null)
            return;

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

        bool isMoving = move.sqrMagnitude > 0.001f;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 finalMove = move * crawlSpeed;
        finalMove.y = yVelocity;

        controller.Move(finalMove * Time.deltaTime);

        if (isMoving && modelRoot != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(move);
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        float animSpeed = 0f;
        if (isMoving)
            animSpeed = 0.2f;

        UpdateAnimator(animSpeed, false, true);
    }

    private void RotateModel(Vector3 move, bool isMoving)
    {
        if (!isMoving || modelRoot == null)
            return;

        Quaternion targetRot = Quaternion.LookRotation(move);
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (controller == null || !controller.enabled)
            return;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 gravityMove = new Vector3(0f, yVelocity, 0f);
        controller.Move(gravityMove * Time.deltaTime);
    }

    private void SetSize(float height, Vector3 center)
    {
        controller.height = height;
        controller.center = center;
    }

    private void UpdateAnimator(float targetMoveSpeed, bool isCrouching, bool isDowned)
    {
        if (animator == null)
            return;

        animator.SetFloat("MoveSpeed", targetMoveSpeed, 0.1f, Time.deltaTime);
        animator.SetBool("IsCrouching", isCrouching);
        animator.SetBool("IsDowned", isDowned);
    }

    public void PlayAnimation(string triggerName)
    {
        if (animator == null)
            return;

        animator.SetTrigger(triggerName);
    }

    public void SetVaulting(bool value)
    {
        if (animator != null)
            animator.SetBool("IsVaulting", value);
    }

    public void SetSearching(bool value)
    {
        if (animator != null)
            animator.SetBool("IsSearching", value);
    }

    public void StopAnimation()
    {
        bool isDowned = state != null && state.IsDowned;
        UpdateAnimator(0f, false, isDowned);
    }
}