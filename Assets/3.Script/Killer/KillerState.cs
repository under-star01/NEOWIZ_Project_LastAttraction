using UnityEngine;
using Mirror;

public enum KillerCondition { Idle, Lunging, Recovering, Hit, Vaulting, Breaking, Carrying }

public class KillerState : NetworkBehaviour
{
    private NetworkAnimator networkAnimator;
    private Animator animator;
    private KillerMove move;

    [Header("Sync Variables")]
    // 데이터 동기화와 시각적 반응을 하나로 묶기 위해 훅(Hook)을 다시 사용합니다.
    [SyncVar(hook = nameof(OnConditionChanged))]
    private KillerCondition currentCondition = KillerCondition.Idle;

    public KillerCondition CurrentCondition => currentCondition;

    // --- [외부 참조용 프로퍼티] ---
    public bool CanMove =>
        currentCondition == KillerCondition.Idle ||
        currentCondition == KillerCondition.Lunging ||
        currentCondition == KillerCondition.Recovering;

    public bool CanLook =>
        currentCondition != KillerCondition.Hit &&
        currentCondition != KillerCondition.Vaulting &&
        currentCondition != KillerCondition.Breaking;

    public bool CanAttack => currentCondition == KillerCondition.Idle;
    public bool IsInAttackAnimation => currentCondition == KillerCondition.Recovering;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        move = GetComponent<KillerMove>();
    }

    // --- [서버 전용 상태 변경 함수] ---
    [Server]
    public void ChangeState(KillerCondition newState)
    {
        if (currentCondition == newState) return;

        // 서버가 값을 바꾸면 모든 클라이언트의 OnConditionChanged가 호출됩니다.
        currentCondition = newState;
    }

    // [핵심 로직] 사용자님이 제안하신 "이중 트리거"를 여기서 처리합니다.
    private void OnConditionChanged(KillerCondition oldState, KillerCondition newState)
    {
        if (animator == null) return;

        // 1. 내 화면 (Local Player)
        // 네트워크 딜레이 없이 즉시 내 애니메이터를 조작합니다.
        if (isLocalPlayer)
        {
            ApplyLocalTrigger(newState);
        }

        // 2. 서버 (Host/Server)
        // 서버에서 트리거를 당겨서 '다른 생존자들'에게 애니메이션을 전파합니다.
        if (isServer)
        {
            ApplyNetworkTrigger(newState);
        }
    }

    // 내 화면용 직접 제어
    private void ApplyLocalTrigger(KillerCondition condition)
    {
        switch (condition)
        {
            case KillerCondition.Lunging: animator.SetTrigger("Attack"); break;
            case KillerCondition.Hit: animator.SetTrigger("Hit"); break;
            case KillerCondition.Breaking: animator.SetTrigger("Break"); break;
        }
    }

    // 네트워크 동기화용 제어 (서버 권한 필요)
    private void ApplyNetworkTrigger(KillerCondition condition)
    {
        if (networkAnimator == null) return;
        switch (condition)
        {
            case KillerCondition.Lunging: networkAnimator.SetTrigger("Attack"); break;
            case KillerCondition.Hit: networkAnimator.SetTrigger("Hit"); break;
            case KillerCondition.Breaking: networkAnimator.SetTrigger("Break"); break;
        }
    }

    // --- [상태 관리] 매 프레임 파라미터 업데이트 ---
    private void Update()
    {
        if (animator == null) return;

        // Lunging을 Busy에 넣어야 공격 애니메이션 도중 이동 애니메이션이 간섭하지 못합니다.
        bool isBusy = currentCondition == KillerCondition.Recovering ||
                      currentCondition == KillerCondition.Hit ||
                      currentCondition == KillerCondition.Breaking ||
                      currentCondition == KillerCondition.Lunging;

        if (!isBusy)
        {
            // 평상시: 이동 애니메이션 갱신
            animator.SetFloat("Speed", move.SyncedMoveSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("isLunging", false);
        }
        else
        {
            // Busy 상태: 발 미끄러짐 방지
            animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);

            // 런지 중일 때만 런지 달리기 애니메이션 허용
            animator.SetBool("isLunging", currentCondition == KillerCondition.Lunging);
        }
    }
}