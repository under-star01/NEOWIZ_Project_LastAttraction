using UnityEngine;

public class SurvivorMove : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraYawRoot;   // 좌우 회전용 루트
    [SerializeField] private Transform cameraPitchRoot; // 상하 회전용 루트
    [SerializeField] private Camera playerCamera;       // 이동 방향 계산 기준 카메라
    [SerializeField] private Transform modelRoot;       // 캐릭터 모델 회전용
    [SerializeField] private Animator animator;         // 애니메이터

    [Header("속도")]
    [SerializeField] private float walkSpeed = 2f;      // 걷기 속도
    [SerializeField] private float runSpeed = 4f;       // 달리기 속도
    [SerializeField] private float crouchSpeed = 1f;    // 앉기 이동 속도
    [SerializeField] private float turnSpeed = 15f;     // 모델 회전 보간 속도

    [Header("카메라")]
    [SerializeField] private float mouseSensitivity = 0.1f; // 마우스 감도
    [SerializeField] private float minPitch = -60f;         // 아래로 보는 최대 각도
    [SerializeField] private float maxPitch = 60f;          // 위로 보는 최대 각도

    private CharacterController controller;
    private SurvivorInput input;
    private SurvivorInteractor interactor; // 상호작용 중인지 확인하기 위한 참조

    private float cameraYaw;   // 카메라 좌우 회전값
    private float pitch;       // 카메라 상하 회전값
    private float yVelocity;   // 중력용 Y축 속도
    private bool isMoveLocked; // 이동 잠금 여부 (증거 조사, 다운 상태 등)

    // 외부에서 이동 잠금/해제를 하기 위한 함수
    public void SetMoveLock(bool value)
    {
        isMoveLocked = value;
    }

    // 외부에서 캐릭터를 특정 방향으로 바라보게 할 때 사용
    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;

        // 방향이 너무 작으면 회전 안 함
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

        // animator를 직접 안 넣었으면 modelRoot 하위에서 자동 탐색
        if (animator == null && modelRoot != null)
            animator = modelRoot.GetComponentInChildren<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // CharacterController가 없거나 꺼져 있으면 처리 중단
        if (controller == null || !controller.enabled)
            return;

        // 이동 잠금과 상관없이 시점 회전은 계속 가능하게 함
        Look();

        // =========================
        // 핵심 부분
        // 상호작용 중이면 crouch를 못 하게 막음
        // =========================
        // interactor가 없으면 그냥 crouch 허용
        // interactor가 있고 현재 상호작용 중이면 crouch 불가
        bool canCrouch = interactor == null || !interactor.IsInteracting;

        // 실제로 이번 프레임에 적용할 crouch 상태
        // Ctrl을 눌렀더라도 상호작용 중이면 false가 됨
        bool isCrouching = canCrouch && input.IsCrouching;

        // 이동 잠금 상태면 움직이지 않고 중력만 적용
        if (isMoveLocked)
        {
            ApplyGravityOnly();

            // 이동 잠금 중이어도 현재 crouch 가능 여부는 반영
            // 하지만 상호작용 중이면 위에서 false가 되기 때문에 서 있는 상태 유지
            Crouch(isCrouching);
            UpdateAnimator(0f, isCrouching);
            return;
        }

        // 일반 이동 처리
        Move(isCrouching);

        // 컨트롤러 높이/중심 조정
        Crouch(isCrouching);
    }

    // 마우스로 카메라 회전
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

    // 이동 처리
    private void Move(bool isCrouching)
    {
        Vector2 moveInput = input.Move;

        // 카메라 기준 앞/오른쪽 방향 가져오기
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        // y 제거해서 평면 이동만 하게 함
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // 입력 방향을 월드 방향으로 변환
        Vector3 move = forward * moveInput.y + right * moveInput.x;

        // 대각선 이동 속도 보정
        if (move.magnitude > 1f)
            move.Normalize();

        bool isMoving = move.sqrMagnitude > 0.001f;

        // 앉은 상태에서는 달리기 불가
        bool isRunning = isMoving && !isCrouching && input.IsRunning;

        float speed = walkSpeed;
        float animSpeed = 0f;

        // 상태별 이동 속도와 애니메이션 값 결정
        if (isCrouching)
        {
            speed = crouchSpeed;
            animSpeed = isMoving ? 0.5f : 0f;
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

        // 중력 처리
        if (controller.isGrounded)
            yVelocity = -1f;
        else
            yVelocity += Physics.gravity.y * Time.deltaTime;

        // 이동 벡터에 y축 속도 추가
        Vector3 finalMove = move;
        finalMove.y = yVelocity / speed;

        controller.Move(finalMove * speed * Time.deltaTime);

        // 이동 중이면 모델이 이동 방향을 자연스럽게 바라보게 함
        if (isMoving && modelRoot != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(move);

            modelRoot.rotation = Quaternion.Slerp(
                modelRoot.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }

        // 애니메이터 갱신
        UpdateAnimator(animSpeed, isCrouching);
    }

    // 이동 잠금 중에는 중력만 적용
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

    // 앉기/서기 상태에 따라 CharacterController 크기 변경
    private void Crouch(bool isCrouching)
    {
        if (isCrouching)
        {
            controller.height = 0.9f;
            controller.center = new Vector3(0f, 0.45f, 0f);
        }
        else
        {
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    // 애니메이터 파라미터 갱신
    private void UpdateAnimator(float targetMoveSpeed, bool isCrouching)
    {
        if (animator == null)
            return;

        animator.SetFloat("MoveSpeed", targetMoveSpeed, 0.1f, Time.deltaTime);
        animator.SetBool("IsCrouching", isCrouching);
    }

    // 외부에서 트리거 애니메이션 실행할 때 사용
    public void PlayAnimation(string triggerName)
    {
        if (animator == null)
            return;

        animator.SetTrigger(triggerName);
    }

    public void SetVaulting(bool value)
    {
        animator.SetBool("IsVaulting", value);
    }

    public void SetSearching(bool value)
    {
        if (animator != null)
            animator.SetBool("IsSearching", value);
    }

    // 이동 애니메이션을 멈추고 현재 상태만 반영
    public void StopAnimation()
    {
        // 여기서도 똑같이 상호작용 중 crouch 불가 규칙 적용
        bool canCrouch = interactor == null || !interactor.IsInteracting;
        bool isCrouching = canCrouch && input != null && input.IsCrouching;

        UpdateAnimator(0f, isCrouching);
    }
}