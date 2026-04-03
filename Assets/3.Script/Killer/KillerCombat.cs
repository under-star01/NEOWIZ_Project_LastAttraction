using UnityEngine;

public class KillerCombat : MonoBehaviour
{
    [Header("Lunge Settings")]
    public float maxLungeDuration = 1.2f;
    public float hitFailPenalty = 2.0f;
    public float hitSuccessPenalty = 2.5f;
    public float wallHitPenalty = 3.0f;

    [Header("Hit Detection")]
    public Transform attackPoint;
    public float attackRadius = 1.0f;
    public LayerMask survivorLayer;
    public LayerMask obstacleLayer;

    [Header("Animation Settings")]
    public float baseAttackAnimationLength = 3.333f; // 실제 공격 애니메이션 파일의 재생 시간(초)

    private KillerInput input;
    private KillerState state;
    private Animator animator;

    private float currentLungeTime;
    private float currentPenaltyTime;
    private bool hasHitTarget;

    void Awake()
    {
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // 1. 후딜레이(Recovery) 타이머 처리
        if (state.CurrentCondition == KillerCondition.Recovering)
        {
            currentPenaltyTime -= Time.deltaTime;
            if (currentPenaltyTime <= 0)
            {
                state.ChangeState(KillerCondition.Idle);
            }
            return;
        }

        // 2. 공격 가능 상태일 때 입력 처리
        if (state.CanAttack || state.CurrentCondition == KillerCondition.Lunging)
        {
            HandleAttackInput();
        }

        // 애니메이터 동기화 (기존의 isLunging 파라미터)
        if (animator != null)
        {
            animator.SetBool("isLunging", state.CurrentCondition == KillerCondition.Lunging);
        }
    }

    private void HandleAttackInput()
    {
        if (input.IsAttackPressed)
        {
            if (state.CurrentCondition != KillerCondition.Lunging) StartLunge();

            // 런지 진행 중 로직
            currentLungeTime += Time.deltaTime;
            currentLungeTime = Mathf.Clamp(currentLungeTime, 0.1f, maxLungeDuration);

            CheckHitDetection();

            // 최대 시간에 도달하거나 타격 시 종료
            if (currentLungeTime >= maxLungeDuration || hasHitTarget) EndLunge();
        }
        else if (state.CurrentCondition == KillerCondition.Lunging)
        {
            EndLunge();
        }
    }

    private void StartLunge()
    {
        state.ChangeState(KillerCondition.Lunging);
        hasHitTarget = false;
        currentLungeTime = 0f;
        Debug.Log("런지 시작!");
    }

    private void CheckHitDetection()
    {
        if (hasHitTarget) return;

        // 1. 벽 충돌 체크
        if (Physics.CheckSphere(attackPoint.position, attackRadius * 0.5f, obstacleLayer))
        {
            hasHitTarget = true;
            currentPenaltyTime = wallHitPenalty;
            Debug.Log("벽 충돌!");
            return;
        }

        // 2. 생존자 타격 체크
        Collider[] hitSurvivors = Physics.OverlapSphere(attackPoint.position, attackRadius, survivorLayer);
        if (hitSurvivors.Length > 0)
        {
            hasHitTarget = true;
            currentPenaltyTime = hitSuccessPenalty;
            Debug.Log("생존자 타격 성공!");
        }
    }

    private void EndLunge()
    {
        state.ChangeState(KillerCondition.Recovering);

        // 1. 페널티 시간 계산 (기존 로직)
        if (!hasHitTarget)
        {
            currentPenaltyTime = Mathf.Max(1.2f, currentLungeTime * hitFailPenalty);
        }
        // 타격 성공 시에는 hitSuccessPenalty가 이미 할당되어 있음

        // 2. 애니메이션 재생 속도 계산
        // 페널티 시간이 길수록 속도는 느려집니다.
        float animSpeed = baseAttackAnimationLength / currentPenaltyTime;

        // 3. 파라미터 전달 및 트리거 실행
        if (animator != null)
        {
            // 속도가 너무 빠르거나 느려져서 기괴해지는 것을 방지 (1배 ~ 2.5배 사이로 고정)
            animator.SetFloat("AttackSpeed", Mathf.Clamp(animSpeed, 1.0f, 3.0f));
            animator.SetTrigger("Attack");
        }
    }
}