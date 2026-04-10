using UnityEngine;
using Mirror;

public enum KillerCondition { Idle, Lunging, Recovering, Hit, Vaulting, Breaking, Incage }

public class KillerState : NetworkBehaviour
{
    private Animator animator;
    private NetworkAnimator networkAnimator;

    [Header("Sync Variables")]
    [SyncVar(hook = nameof(OnConditionChanged))]
    private KillerCondition currentCondition = KillerCondition.Idle;

    public KillerCondition CurrentCondition => currentCondition;

    public bool CanMove =>
        currentCondition == KillerCondition.Idle ||
        currentCondition == KillerCondition.Lunging ||
        currentCondition == KillerCondition.Recovering;

    public bool CanLook =>
        currentCondition != KillerCondition.Hit &&
        currentCondition != KillerCondition.Vaulting &&
        currentCondition != KillerCondition.Breaking &&
        currentCondition != KillerCondition.Incage;

    public bool CanAttack => currentCondition == KillerCondition.Idle;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
    }

    [Server]
    public void ChangeState(KillerCondition newState)
    {
        if (currentCondition == newState) return;
        currentCondition = newState;

        // [중요] 데디케이트 서버는 화면이 없으므로 트리거를 당길 필요가 없지만,
        // NetworkAnimator를 통해 트리거를 전파하고 싶다면 여기서 호출할 수 있습니다.
        // 하지만 우리는 더 확실한 SyncVar Hook 방식을 사용합니다.
    }

    private void OnConditionChanged(KillerCondition oldState, KillerCondition newState)
    {
        // 서버(데디케이트)는 실행하지 않음
        if (isServer && !isClient) return;

        // 모든 클라이언트(나 포함)가 상태 변화를 감지하면 트리거를 실행합니다.
        PlayTrigger(newState);
    }

    public void PlayTrigger(KillerCondition condition)
    {
        if (animator == null) return;

        switch (condition)
        {
            case KillerCondition.Lunging: animator.SetTrigger("Attack"); break;
            case KillerCondition.Hit: animator.SetTrigger("Hit"); break;
            case KillerCondition.Breaking: animator.SetTrigger("Break"); break;
            case KillerCondition.Vaulting: animator.SetTrigger("Vault"); break;
        }
    }
}