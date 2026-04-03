using UnityEngine;
using UnityEngine.InputSystem;

public class KillerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float lookSensitivity = 0.2f;
    public float lungeSpeed = 1.6f;   // 런지 시 가속 배율
    public float penaltySpeed = 0.4f;  // 후딜레이 시 감속 배율

    [Header("Lunge & Penalty Settings")]
    public float maxLungeDuration = 1.2f;        // 최대 런지 시간
    public float hitFailPenalty = 2.0f;      // 허공 휘두름 시 배율 페널티
    public float hitSuccessPenalty = 2.5f;       // 생존자 타격 시 고정 후딜레이
    public float wallHitPenalty = 3.0f;          // 벽 충돌 시 고정 후딜레이

    [Header("Hit Detection")]
    public Transform attackPoint;       // 공격 판정 중심점
    public float attackRadius = 1.0f;   // 공격 범위 반지름
    public LayerMask survivorLayer;     // 생존자 레이어
    public LayerMask obstacleLayer;     // 벽/구조물 레이어

    [Header("Camera Stabilization")]
    public Transform killerCamera;      // 카메라 오브젝트
    public Transform cameraTarget;      // [중요] 루트의 자식인 '고정된' 눈높이 빈 오브젝트
    public float followSpeed = 25f;     // 추적 속도 (높을수록 즉각적)

    private CharacterController controller;
    private float cameraPitch = 0f;
    private Vector2 moveInput;
    private Vector2 lookInput;

    // 상태 관리 변수
    private bool isLunging = false;
    private bool isRecovering = false;
    private bool hasHitTarget = false;
    private float currentLungeTime = 0f;
    private float currentPenaltyTime = 0f;

    // 자식 오브젝트의 애니메이터 참조
    private Animator animator;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // [수정] 애니메이터가 자식 오브젝트(모델)에 있으므로 GetComponentInChildren 사용
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
            Debug.Log($"애니메이터 탐색 성공: {animator.gameObject.name}");
        else
            Debug.LogError("자식 오브젝트에서 애니메이터를 찾지 못했습니다!");
    }

    void Update()
    {
        if (TestMng.inputSys == null) return;

        moveInput = TestMng.inputSys.Killer.Move.ReadValue<Vector2>();
        lookInput = TestMng.inputSys.Killer.Look.ReadValue<Vector2>();

        HandleLungeInput();
        HandleRecoveryTimer();

        HandleMovement();
        HandleLook();

        UpdateAnimationParameters();
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null) return;

        // 이동 입력 벡터 크기 전달 (Idle <-> Walk)
        animator.SetFloat("Speed", moveInput.magnitude);
        // 런지 상태 전달 (Walk <-> Run)
        animator.SetBool("isLunging", isLunging);
    }

    private void HandleLungeInput()
    {
        if (isRecovering || AnimationPlayCheck("WeaponSwing")) return;

        if (TestMng.inputSys.Killer.Attack.IsPressed())
        {
            if (!isLunging) StartLunge();

            currentLungeTime += Time.deltaTime;
            currentLungeTime = Mathf.Clamp(currentLungeTime, 0.1f, maxLungeDuration);

            CheckHitDetection();

            if (currentLungeTime >= maxLungeDuration || hasHitTarget) EndLunge();
        }
        else if (isLunging)
        {
            EndLunge();
        }
    }

    private bool AnimationPlayCheck(string stateName)
    {
        if (animator == null) return false;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName) && stateInfo.normalizedTime < 1.0f;
    }

    private void HandleRecoveryTimer()
    {
        if (isRecovering)
        {
            currentPenaltyTime -= Time.deltaTime;
            if (currentPenaltyTime <= 0) isRecovering = false;
        }
    }

    private void StartLunge()
    {
        isLunging = true;
        hasHitTarget = false;
        currentLungeTime = 0f;
        Debug.Log("런지 시작!");
    }

    private void CheckHitDetection()
    {
        if (hasHitTarget) return;

        if (Physics.CheckSphere(attackPoint.position, attackRadius * 0.5f, obstacleLayer))
        {
            OnWallHit();
            return;
        }

        Collider[] hitSurvivors = Physics.OverlapSphere(attackPoint.position, attackRadius, survivorLayer);
        if (hitSurvivors.Length > 0)
        {
            OnSurvivorHit(hitSurvivors[0].gameObject);
        }
    }

    private void OnSurvivorHit(GameObject survivor)
    {
        hasHitTarget = true;
        currentPenaltyTime = hitSuccessPenalty;
        Debug.Log($"{survivor.name} 타격 성공!");
    }

    private void OnWallHit()
    {
        hasHitTarget = true;
        currentPenaltyTime = wallHitPenalty;
        Debug.Log("벽 충돌!");
    }

    private void EndLunge()
    {
        isLunging = false;
        isRecovering = true;

        // 런지가 끝날 때 무기를 휘두르는 애니메이션 트리거
        if (animator != null) animator.SetTrigger("Attack");

        if (!hasHitTarget)
        {
            currentPenaltyTime = currentLungeTime * hitFailPenalty;
            Debug.Log($"헛손질! 페널티: {currentPenaltyTime}초");
        }
    }

    private void HandleMovement()
    {
        if (AnimationPlayCheck("WeaponSwing")) return;

        float finalSpeed = moveSpeed;

        if (isLunging) finalSpeed *= lungeSpeed;
        else if (isRecovering) finalSpeed *= penaltySpeed;

        // 최상위 루트 이동 (자식인 카메라와 모델은 자동으로 따라옴)
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * finalSpeed * Time.deltaTime);
    }

    private void HandleLook()
    {
        // 1. 최상위 루트 좌우 회전
        transform.Rotate(Vector3.up * lookInput.x * lookSensitivity);

        // 2. 카메라 상하 회전
        cameraPitch -= lookInput.y * lookSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        killerCamera.localRotation = Quaternion.Euler(cameraPitch, 0, 0);

        // 3. 카메라 위치 고정 (애니메이션에 영향받지 않는 target 위치 추적)
        if (cameraTarget != null)
        {
            killerCamera.position = Vector3.Lerp(killerCamera.position, cameraTarget.position, Time.deltaTime * followSpeed);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}