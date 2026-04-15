using Mirror;
using UnityEngine;

public class SurvivorMove : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraYawRoot;
    [SerializeField] private Transform cameraPitchRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerListener;
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
    private SurvivorActionState actionState;
    private SurvivorMoveState moveState;

    private float localYaw;
    private float localPitch;
    private float yVelocity;

    private bool isMoveLocked;

    private Vector2 serverMoveInput;
    private bool serverWantsRun;
    private bool serverWantsCrouch;
    private float serverYaw;
    private float serverPitch;

    [SyncVar] private float syncedYaw;
    [SyncVar] private float syncedPitch;
    [SyncVar] private float syncedModelYaw;

    public void SetMoveLock(bool value)
    {
        isMoveLocked = value;

        if (isLocalPlayer && !isServer)
            CmdSetMoveLock(value);
    }

    [Command]
    private void CmdSetMoveLock(bool value)
    {
        isMoveLocked = value;
    }

    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        if (isServer)
        {
            ApplyFaceDirection(dir.normalized);
            RpcFaceDirection(dir.normalized);
        }
        else if (isLocalPlayer)
        {
            CmdFaceDirection(dir.normalized);
        }
    }

    [Command]
    private void CmdFaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude <= 0.001f)
            return;

        ApplyFaceDirection(dir.normalized);
        RpcFaceDirection(dir.normalized);
    }

    [ClientRpc]
    private void RpcFaceDirection(Vector3 dir)
    {
        if (isServer)
            return;

        ApplyFaceDirection(dir.normalized);
    }

    private void ApplyFaceDirection(Vector3 dir)
    {
        if (modelRoot != null)
        {
            modelRoot.rotation = Quaternion.LookRotation(dir);
            syncedModelYaw = modelRoot.eulerAngles.y;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<SurvivorInput>();
        interactor = GetComponent<SurvivorInteractor>();
        state = GetComponent<SurvivorState>();
        actionState = GetComponent<SurvivorActionState>();
        moveState = GetComponent<SurvivorMoveState>();

        if (animator == null && modelRoot != null)
            animator = modelRoot.GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (playerCamera != null)
            playerCamera.enabled = false;

        if (playerListener != null)
            playerListener.enabled = false;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerCamera != null)
            playerCamera.enabled = true;

        if (playerListener != null)
            playerListener.enabled = true;

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

            if (playerListener != null)
                playerListener.enabled = false;
        }
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            UpdateLocalLook();
            SendInputToServer();
            ApplyLocalCamera();
            ApplyModelRotation();
        }
        else
        {
            ApplyRemoteLook();
            ApplyModelRotation();
        }
    }

    private void FixedUpdate()
    {
        if (!isServer)
            return;

        ServerTickMovement();
    }

    private void UpdateLocalLook()
    {
        if (input == null)
            return;

        Vector2 look = input.Look;

        localYaw += look.x * mouseSensitivity;
        localPitch -= look.y * mouseSensitivity;
        localPitch = Mathf.Clamp(localPitch, minPitch, maxPitch);
    }

    private void ApplyLocalCamera()
    {
        if (cameraYawRoot != null)
            cameraYawRoot.localRotation = Quaternion.Euler(0f, localYaw, 0f);

        if (cameraPitchRoot != null)
            cameraPitchRoot.localRotation = Quaternion.Euler(localPitch, 0f, 0f);
    }

    private void ApplyRemoteLook()
    {
        if (cameraYawRoot != null)
            cameraYawRoot.localRotation = Quaternion.Euler(0f, syncedYaw, 0f);

        if (cameraPitchRoot != null)
            cameraPitchRoot.localRotation = Quaternion.Euler(syncedPitch, 0f, 0f);
    }

    private void ApplyModelRotation()
    {
        if (modelRoot == null)
            return;

        Vector3 euler = modelRoot.eulerAngles;
        euler.y = syncedModelYaw;
        modelRoot.eulerAngles = euler;
    }

    private void SendInputToServer()
    {
        if (input == null)
            return;

        CmdSetMoveInput(
            input.Move,
            input.IsRunning,
            input.IsCrouching,
            localYaw,
            localPitch
        );
    }

    [Command]
    private void CmdSetMoveInput(Vector2 moveInput, bool wantsRun, bool wantsCrouch, float yaw, float pitch)
    {
        serverMoveInput = moveInput;
        serverWantsRun = wantsRun;
        serverWantsCrouch = wantsCrouch;
        serverYaw = yaw;
        serverPitch = pitch;

        syncedYaw = yaw;
        syncedPitch = pitch;
    }

    // 서버에서 실제 이동 처리
    [Server]
    private void ServerTickMovement()
    {
        if (controller == null || !controller.enabled)
            return;

        bool isDowned = state != null && state.IsDowned;
        bool isDead = state != null && state.IsDead;
        bool isBusy = actionState != null && actionState.IsBusy;

        // 이동 잠김 / 행동 제한 / 사망이면 이동 정지
        if (isMoveLocked || isBusy || isDead)
        {
            ApplyGravityOnlyServer();

            if (moveState != null)
                moveState.SetMoveState(SurvivorLocomotionState.Idle);

            return;
        }

        // 다운 상태면 기어가기 이동
        if (isDowned)
        {
            CrawlMoveServer(serverMoveInput, serverYaw);
            return;
        }

        // Hold 상호작용 중이면 앉기 시작 금지
        bool canCrouch = interactor == null || !interactor.IsInteracting;
        bool isCrouching = canCrouch && serverWantsCrouch;

        if (isCrouching)
            SetSizeServer(crouchHeight, crouchCenter);
        else
            SetSizeServer(standHeight, standCenter);

        MoveServer(serverMoveInput, serverWantsRun, isCrouching, serverYaw);
    }

    // 일반 이동
    [Server]
    private void MoveServer(Vector2 moveInput, bool wantsRun, bool isCrouching, float yaw)
    {
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;

        Vector3 move = forward * moveInput.y + right * moveInput.x;

        if (move.magnitude > 1f)
            move.Normalize();

        bool isMoving = move.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && !isCrouching && wantsRun;

        float speed = walkSpeed;

        if (isCrouching)
            speed = crouchSpeed;
        else if (isRunning)
            speed = runSpeed;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.fixedDeltaTime;

        Vector3 finalMove = move * speed;
        finalMove.y = yVelocity;

        controller.Move(finalMove * Time.fixedDeltaTime);

        RotateModelServer(move, isMoving);

        if (moveState != null)
        {
            if (!isMoving)
                moveState.SetMoveState(SurvivorLocomotionState.Idle);
            else if (isCrouching)
                moveState.SetMoveState(SurvivorLocomotionState.Crouch);
            else if (isRunning)
                moveState.SetMoveState(SurvivorLocomotionState.Run);
            else
                moveState.SetMoveState(SurvivorLocomotionState.Walk);
        }
    }

    // 다운 상태 이동
    [Server]
    private void CrawlMoveServer(Vector2 moveInput, float yaw)
    {
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;

        Vector3 move = forward * moveInput.y + right * moveInput.x;

        if (move.magnitude > 1f)
            move.Normalize();

        bool isMoving = move.sqrMagnitude > 0.001f;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.fixedDeltaTime;

        Vector3 finalMove = move * crawlSpeed;
        finalMove.y = yVelocity;

        controller.Move(finalMove * Time.fixedDeltaTime);

        if (isMoving && modelRoot != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(move);
            modelRoot.rotation = Quaternion.Slerp(
                modelRoot.rotation,
                targetRot,
                turnSpeed * Time.fixedDeltaTime
            );

            syncedModelYaw = modelRoot.eulerAngles.y;
        }

        if (moveState != null)
        {
            if (isMoving)
                moveState.SetMoveState(SurvivorLocomotionState.Crawl);
            else
                moveState.SetMoveState(SurvivorLocomotionState.Idle);
        }
    }

    [Server]
    private void RotateModelServer(Vector3 move, bool isMoving)
    {
        if (!isMoving || modelRoot == null)
            return;

        Quaternion targetRot = Quaternion.LookRotation(move);
        modelRoot.rotation = Quaternion.Slerp(
            modelRoot.rotation,
            targetRot,
            turnSpeed * Time.fixedDeltaTime
        );

        syncedModelYaw = modelRoot.eulerAngles.y;
    }

    [Server]
    private void ApplyGravityOnlyServer()
    {
        if (controller == null || !controller.enabled)
            return;

        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.fixedDeltaTime;

        Vector3 gravityMove = new Vector3(0f, yVelocity, 0f);
        controller.Move(gravityMove * Time.fixedDeltaTime);
    }

    [Server]
    private void SetSizeServer(float height, Vector3 center)
    {
        controller.height = height;
        controller.center = center;
    }

    // 애니메이션 트리거 실행
    public void PlayAnimation(string triggerName)
    {
        if (isServer)
        {
            ApplyPlayAnimation(triggerName);
            RpcPlayAnimation(triggerName);
        }
        else if (isLocalPlayer)
        {
            CmdPlayAnimation(triggerName);
        }
    }

    [Command]
    private void CmdPlayAnimation(string triggerName)
    {
        ApplyPlayAnimation(triggerName);
        RpcPlayAnimation(triggerName);
    }

    [ClientRpc]
    private void RpcPlayAnimation(string triggerName)
    {
        if (isServer)
            return;

        ApplyPlayAnimation(triggerName);
    }

    private void ApplyPlayAnimation(string triggerName)
    {
        if (animator == null)
            return;

        animator.SetTrigger(triggerName);
    }

    // 볼트 상태 애니메이션
    public void SetVaulting(bool value)
    {
        if (isServer)
        {
            ApplySetVaulting(value);
            RpcSetVaulting(value);
        }
        else if (isLocalPlayer)
        {
            CmdSetVaulting(value);
        }
    }

    [Command]
    private void CmdSetVaulting(bool value)
    {
        ApplySetVaulting(value);
        RpcSetVaulting(value);
    }

    [ClientRpc]
    private void RpcSetVaulting(bool value)
    {
        if (isServer)
            return;

        ApplySetVaulting(value);
    }

    private void ApplySetVaulting(bool value)
    {
        if (animator != null)
            animator.SetBool("IsVaulting", value);
    }

    // 조사/힐 같은 searching 상태
    public void SetSearching(bool value)
    {
        if (isServer)
        {
            ApplySetSearching(value);
            RpcSetSearching(value);
        }
        else if (isLocalPlayer)
        {
            CmdSetSearching(value);
        }
    }

    [Command]
    private void CmdSetSearching(bool value)
    {
        ApplySetSearching(value);
        RpcSetSearching(value);
    }

    [ClientRpc]
    private void RpcSetSearching(bool value)
    {
        if (isServer)
            return;

        ApplySetSearching(value);
    }

    private void ApplySetSearching(bool value)
    {
        if (animator != null)
            animator.SetBool("IsSearching", value);
    }

    // 이동 애니메이션 정지
    public void StopAnimation()
    {
        if (isServer)
        {
            if (moveState != null)
                moveState.SetMoveState(SurvivorLocomotionState.Idle);
        }
        else if (isLocalPlayer)
        {
            CmdStopAnimation();
        }
    }

    [Command]
    private void CmdStopAnimation()
    {
        if (moveState != null)
            moveState.SetMoveState(SurvivorLocomotionState.Idle);
    }
}