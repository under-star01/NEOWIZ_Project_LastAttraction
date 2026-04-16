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
    public float baseAttackAnimationLength = 2.666f;

    private KillerInput input;
    private KillerState state;
    private Animator animator;

    private float currentLungeTime;
    private float currentPenaltyTime;
    private bool hasHitTarget;
    private uint hitSurvivorNetId;
    private bool isEndingAttack;

    void Awake()
    {
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (animator != null)
        {
            // 모든 클라이언트에서 현재 상태가 Lunging이면 Run 애니메이션을 틉니다.
            animator.SetBool("isLunging", state.CurrentCondition == KillerCondition.Lunging);
        }

        if (!isLocalPlayer) return;

        if (state.CurrentCondition == KillerCondition.Recovering)
        {
            currentPenaltyTime -= Time.deltaTime;
            if (currentPenaltyTime <= 0f)
            {
                isEndingAttack = false;
                CmdResetToIdle();
            }
            return;
        }

        if (state.CanAttack || state.CurrentCondition == KillerCondition.Lunging)
        {
            HandleAttackInput();
        }
    }

    private void HandleAttackInput()
    {
        if (input.IsAttackPressed)
        {
            if (state.CurrentCondition != KillerCondition.Lunging)
            {
                // [연속 공격 방지] 현재 공격 가능한 상태(Idle)가 아니면 입력을 무시합니다.
                if (!state.CanAttack) return;

                // [중요] 여기서 PlayTrigger를 호출하지 않습니다. (휘두르기 스킵 방지)
                hasHitTarget = false;
                currentLungeTime = 0f;
                hitSurvivorNetId = 0;
                isEndingAttack = false;

                CmdStartLunge();
            }

            if (isEndingAttack) return;

            currentLungeTime += Time.deltaTime;
            currentLungeTime = Mathf.Clamp(currentLungeTime, 0.1f, maxLungeDuration);
            CheckHitDetection();

            if (currentLungeTime >= maxLungeDuration || hasHitTarget)
            {
                isEndingAttack = true;
                CmdEndLunge(currentLungeTime, hasHitTarget, hitSurvivorNetId);
            }
        }
        else if (state.CurrentCondition == KillerCondition.Lunging)
        {
            if (isEndingAttack) return;
            isEndingAttack = true;
            CmdEndLunge(currentLungeTime, hasHitTarget, hitSurvivorNetId);
        }
    }

    private void CheckHitDetection()
    {
        if (hasHitTarget || attackPoint == null) return;
        if (Physics.CheckSphere(attackPoint.position, attackRadius * 0.5f, obstacleLayer))
        {
            hasHitTarget = true;
            currentPenaltyTime = wallHitPenalty;
            return;
        }

        Collider[] hitSurvivors = Physics.OverlapSphere(attackPoint.position, attackRadius, survivorLayer);
        foreach (var hit in hitSurvivors)
        {
            SurvivorState sState = hit.GetComponentInParent<SurvivorState>();
            if (sState != null)
            {
                NetworkIdentity id = sState.GetComponent<NetworkIdentity>();
                if (id != null)
                {
                    hasHitTarget = true;
                    currentPenaltyTime = hitSuccessPenalty;
                    hitSurvivorNetId = id.netId;
                    return;
                }
            }
        }
    }

    [Command]
    private void CmdStartLunge()
    {
        if (state.CurrentCondition != KillerCondition.Idle) return;
        state.ChangeState(KillerCondition.Lunging);
    }

    [Command]
    private void CmdEndLunge(float lungeTime, bool isHit, uint survivorNetId)
    {
        if (state.CurrentCondition != KillerCondition.Lunging) return;

        state.ChangeState(KillerCondition.Recovering);
        float finalPenalty;

        if (isHit)
        {
            // survivorNetId가 있으면 생존자 타격(2.5s), 없으면 벽 타격(3.0s)
            finalPenalty = (survivorNetId != 0) ? hitSuccessPenalty : wallHitPenalty;
        }
        else
        {
            // 헛스윙 패널티 계산
            finalPenalty = Mathf.Max(1.2f, lungeTime * hitFailPenalty);
        }

        if (isHit && survivorNetId != 0)
        {
            if (NetworkServer.spawned.TryGetValue(survivorNetId, out NetworkIdentity identity))
            {
                SurvivorState sState = identity.GetComponentInParent<SurvivorState>();
                if (sState != null) sState.TakeHit();
            }
        }

        if (isHit && survivorNetId != 0)
        {
            Debug.Log("킬러 공격 명중");
            // ... TakeHit() 호출 ...
        }
        else
        {
            Debug.Log("헛스윙 또는 장해물에 막힘");
        }

        float animSpeed = baseAttackAnimationLength / finalPenalty;
        RpcSyncAttackResult(animSpeed, finalPenalty);
    }

    [Command]
    private void CmdResetToIdle()
    {
        if (state.CurrentCondition == KillerCondition.Recovering)
            state.ChangeState(KillerCondition.Idle);
    }

    [ClientRpc]
    private void RpcSyncAttackResult(float speed, float penalty)
    {
        if (animator != null) animator.SetFloat("AttackSpeed", Mathf.Clamp(speed, 0.8f, 3.0f));
        if (isLocalPlayer) currentPenaltyTime = penalty;
    }
}