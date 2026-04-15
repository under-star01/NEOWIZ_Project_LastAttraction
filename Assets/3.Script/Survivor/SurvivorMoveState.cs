using Mirror;
using UnityEngine;

// 생존자의 "이동 상태" 전용
public enum SurvivorLocomotionState
{
    Idle,
    Walk,
    Run,
    Crouch,
    Crawl
}

public class SurvivorMoveState : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private Animator animator;

    [SyncVar(hook = nameof(OnMoveStateChanged))]
    private SurvivorLocomotionState currentMoveState = SurvivorLocomotionState.Idle;

    public SurvivorLocomotionState CurrentMoveState => currentMoveState;

    public bool IsIdle => currentMoveState == SurvivorLocomotionState.Idle;
    public bool IsWalking => currentMoveState == SurvivorLocomotionState.Walk;
    public bool IsRunning => currentMoveState == SurvivorLocomotionState.Run;
    public bool IsCrouching => currentMoveState == SurvivorLocomotionState.Crouch;
    public bool IsCrawling => currentMoveState == SurvivorLocomotionState.Crawl;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    [Server]
    public void SetMoveState(SurvivorLocomotionState newState)
    {
        currentMoveState = newState;
        ApplyAnimator(newState);
    }

    private void OnMoveStateChanged(SurvivorLocomotionState oldValue, SurvivorLocomotionState newValue)
    {
        ApplyAnimator(newValue);
    }

    // 이동 상태를 애니메이터 값으로 변환
    private void ApplyAnimator(SurvivorLocomotionState state)
    {
        if (animator == null)
            return;

        animator.SetInteger("Locomotion", (int)state);
    }
}