using UnityEngine;
using Mirror;

public class KillerCombat : NetworkBehaviour
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

    private NetworkAnimator networkAnimator;

    // 이번 공격에서 실제로 맞은 생존자 netId 저장
    private uint hitSurvivorNetId;

    void Awake()
    {
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();
        animator = GetComponentInChildren<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
    }

    void Update()
    {
        if (!isLocalPlayer) return; // 내 화면의 킬러만 입력을 처리합니다.

        // 1. 후딜레이 타이머 처리 (로컬)
        if (state.CurrentCondition == KillerCondition.Recovering)
        {
            currentPenaltyTime -= Time.deltaTime;
            if (currentPenaltyTime <= 0)
            {
                // 타이머 종료 후 서버에 상태 복구 요청
                ResetToIdle();
            }
            return;
        }

        // 2. 공격 처리
        if (state.CanAttack || state.CurrentCondition == KillerCondition.Lunging)
        {
            HandleAttackInput();
        }

        if (animator != null)
        {
            animator.SetBool("isLunging", state.CurrentCondition == KillerCondition.Lunging);
        }
    }

    private void HandleAttackInput()
    {
        if (input.IsAttackPressed)
        {
            if (state.CurrentCondition != KillerCondition.Lunging)
            {
                hasHitTarget = false;
                currentLungeTime = 0f;
                hitSurvivorNetId = 0;
                StartLunge();
            }

            // 런지 진행 중 로직
            currentLungeTime += Time.deltaTime;
            currentLungeTime = Mathf.Clamp(currentLungeTime, 0.1f, maxLungeDuration);

            CheckHitDetection();

            // 최대 도달 혹은 타격 성공 시 종료
            if (currentLungeTime >= maxLungeDuration || hasHitTarget)
            {
                // 맞은 생존자 netId까지 같이 서버에 넘김
                EndLunge(currentLungeTime, hasHitTarget, currentPenaltyTime, hitSurvivorNetId);
            }
        }
        else if (state.CurrentCondition == KillerCondition.Lunging)
        {
            EndLunge(currentLungeTime, hasHitTarget, currentPenaltyTime, hitSurvivorNetId);
        }
    }

    private void CheckHitDetection()
    {
        if (hasHitTarget) return;
        if (attackPoint == null) return;

        // 벽 충돌 체크
        if (Physics.CheckSphere(attackPoint.position, attackRadius * 0.5f, obstacleLayer))
        {
            hasHitTarget = true;
            currentPenaltyTime = wallHitPenalty;
            hitSurvivorNetId = 0;
            return;
        }

        // 생존자 타격 체크
        Collider[] hitSurvivors = Physics.OverlapSphere(attackPoint.position, attackRadius, survivorLayer);
        if (hitSurvivors.Length > 0)
        {
            for (int i = 0; i < hitSurvivors.Length; i++)
            {
                Collider hit = hitSurvivors[i];
                if (hit == null) continue;

                SurvivorState survivorState = hit.GetComponent<SurvivorState>();
                if (survivorState == null)
                    survivorState = hit.GetComponentInParent<SurvivorState>();

                if (survivorState == null)
                    continue;

                NetworkIdentity identity = survivorState.GetComponent<NetworkIdentity>();
                if (identity == null)
                    identity = survivorState.GetComponentInParent<NetworkIdentity>();

                if (identity == null)
                    continue;

                hasHitTarget = true;
                currentPenaltyTime = hitSuccessPenalty;
                hitSurvivorNetId = identity.netId;
                return;
            }
        }
    }

    [Command]
    private void StartLunge()
    {
        state.ChangeState(KillerCondition.Lunging);
        hasHitTarget = false;
        currentLungeTime = 0f;
        hitSurvivorNetId = 0;

        if (networkAnimator != null)
            networkAnimator.SetTrigger("Attack");

        Debug.Log("런지 시작!");
    }

    [Command]
    private void EndLunge(float lungeTime, bool isHit, float penalty, uint survivorNetId)
    {
        state.ChangeState(KillerCondition.Recovering);

        // 서버에서 최종 페널티 시간 재계산
        float finalPenalty = isHit ? penalty : Mathf.Max(1.2f, lungeTime * hitFailPenalty);

        // 실제 생존자 피격 적용
        if (isHit && survivorNetId != 0)
        {
            if (NetworkServer.spawned.TryGetValue(survivorNetId, out NetworkIdentity identity))
            {
                SurvivorState survivorState = identity.GetComponent<SurvivorState>();
                if (survivorState == null)
                    survivorState = identity.GetComponentInChildren<SurvivorState>();
                if (survivorState == null)
                    survivorState = identity.GetComponentInParent<SurvivorState>();

                if (survivorState != null)
                {
                    survivorState.TakeHit();
                }
            }
        }

        // 애니메이션 속도 계산 및 모든 클라이언트 적용
        float animSpeed = baseAttackAnimationLength / finalPenalty;
        SyncAttackEffect(animSpeed);
    }

    [Command]
    private void ResetToIdle()
    {
        state.ChangeState(KillerCondition.Idle);
    }

    [ClientRpc]
    private void SyncAttackEffect(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat("AttackSpeed", Mathf.Clamp(speed, 1.0f, 3.0f));
            // 트리거는 이미 CmdStartLunge에서 실행되었거나, 여기서 한 번 더 보정 가능
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}