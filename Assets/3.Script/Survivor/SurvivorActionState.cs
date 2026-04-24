using System.Collections;
using Mirror;
using UnityEngine;

// 생존자의 현재 행동 상태
public enum SurvivorAction
{
    None,
    Hit,
    DownHit,
    Healing,
    Interacting,
    Stunned,
    Vault
}

public class SurvivorActionState : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private SurvivorMove move;
    [SerializeField] private SurvivorInteractor interactor;
    [SerializeField] private Animator animator;

    // 현재 대표 행동 상태
    // 서버에서 변경되면 클라이언트에도 동기화된다.
    [SyncVar(hook = nameof(OnActChanged))]
    private SurvivorAction currentAction = SurvivorAction.None;

    // 힐을 받고 있는 중인지
    [SyncVar(hook = nameof(OnHealChanged))]
    private bool isBeingHealed;

    // Hold 상호작용을 진행 중인지
    [SyncVar]
    private bool isDoingInteraction;

    // 우클릭 카메라 스킬을 사용 중인지
    [SyncVar]
    private bool isCamSkill;

    public SurvivorAction CurrentAction => currentAction;
    public bool IsBeingHealed => isBeingHealed;
    public bool IsDoingInteraction => isDoingInteraction;
    public bool IsCamSkill => isCamSkill;
    public bool IsVault => currentAction == SurvivorAction.Vault;

    public bool IsBusy =>
        currentAction == SurvivorAction.DownHit ||
        currentAction == SurvivorAction.Stunned ||
        currentAction == SurvivorAction.Vault;

    private void Awake()
    {
        if (move == null)
            move = GetComponent<SurvivorMove>();

        if (interactor == null)
            interactor = GetComponent<SurvivorInteractor>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // 서버에서 현재 행동 상태 설정
    [Server]
    public void SetAct(SurvivorAction act)
    {
        currentAction = act;
        ApplyState();
    }

    // 특정 행동 상태일 때만 해제
    [Server]
    public void ClearAct(SurvivorAction act)
    {
        if (currentAction != act)
            return;

        currentAction = SurvivorAction.None;
        ApplyState();
    }

    // 힐 받는 상태 설정
    [Server]
    public void SetHeal(bool value)
    {
        isBeingHealed = value;
        ApplyUse();
    }

    // Hold 상호작용 중인지 저장
    [Server]
    public void SetInteract(bool value)
    {
        isDoingInteraction = value;
    }

    // 카메라 스킬 사용 상태 저장
    [Server]
    public void SetCam(bool value)
    {
        isCamSkill = value;
    }

    // 카메라 스킬 사용 가능 여부
    public bool CanCam()
    {
        SurvivorState state = GetComponent<SurvivorState>();
        if (state == null)
            return false;

        if (state.IsDead)
            return false;

        if (state.IsDowned)
            return false;

        // 감옥 안에서는 카메라 스킬 금지
        if (state.IsImprisoned)
            return false;

        if (isBeingHealed)
            return false;

        if (isDoingInteraction)
            return false;

        if (currentAction == SurvivorAction.DownHit)
            return false;

        if (currentAction == SurvivorAction.Stunned)
            return false;

        if (currentAction == SurvivorAction.Vault)
            return false;

        return true;
    }

    // 행동 상태가 동기화되면 클라이언트에서도 이동/상호작용 제한을 반영한다.
    private void OnActChanged(SurvivorAction oldValue, SurvivorAction newValue)
    {
        ApplyState();
    }

    // 힐 받는 상태가 바뀌면 상호작용 가능 여부를 갱신한다.
    private void OnHealChanged(bool oldValue, bool newValue)
    {
        ApplyUse();
    }

    private void ApplyState()
    {
        ApplyLock();
        ApplyUse();
    }

    // DownHit, Stunned 상태에서는 이동을 막는다
    private void ApplyLock()
    {
        if (move == null)
            return;

        bool lockMove = false;

        if (currentAction == SurvivorAction.DownHit)
            lockMove = true;

        if (currentAction == SurvivorAction.Stunned)
            lockMove = true;

        move.SetMoveLock(lockMove);

        // 이동 잠금 상태에서는 이동 애니메이션을 Idle 쪽으로 정리한다.
        if (lockMove)
            move.StopAnimation();
    }

    // 상태에 따라 SurvivorInteractor 자체를 켜고 끈다.
    public void ApplyUse()
    {
        if (interactor == null)
            return;

        SurvivorState state = GetComponent<SurvivorState>();
        if (state == null)
            return;

        bool canUse = true;

        if (state.IsDowned)
            canUse = false;

        if (state.IsDead)
            canUse = false;

        if (isBeingHealed)
            canUse = false;

        if (currentAction == SurvivorAction.DownHit)
            canUse = false;

        if (currentAction == SurvivorAction.Stunned)
            canUse = false;

        interactor.enabled = canUse;
    }

    // 일반 피격 연출
    // Healthy -> Injured가 될 때 사용한다.
    // DownHit과 다르게 이동 잠금 없이 Hit 애니메이션만 재생한다.
    [Server]
    public IEnumerator HitRoutine(float time)
    {
        if (time <= 0f)
            yield break;

        // 더 강한 행동 상태일 때는 일반 Hit으로 덮어쓰지 않는다.
        if (currentAction == SurvivorAction.DownHit)
            yield break;

        if (currentAction == SurvivorAction.Stunned)
            yield break;

        if (currentAction == SurvivorAction.Vault)
            yield break;

        currentAction = SurvivorAction.Hit;

        if (move != null)
        {
            move.SetCamAnim(false);
            move.PlayAnimation("Hit");
        }
        else if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        // ApplyState를 호출해도 Hit은 Busy / Lock / Use 차단에 포함되지 않는다.
        ApplyState();

        yield return new WaitForSeconds(time);

        if (currentAction == SurvivorAction.Hit)
        {
            currentAction = SurvivorAction.None;
            ApplyState();
        }
    }

    // 다운 피격 연출
    [Server]
    public IEnumerator DownHitRoutine(float time)
    {
        currentAction = SurvivorAction.DownHit;
        isCamSkill = false;
        isDoingInteraction = false;

        // DownHit이 스턴보다 우선이므로 스턴 Bool이 남아있으면 제거
        if (move != null)
            move.SetStunned(false);

        if (interactor != null)
            interactor.ForceStopInteract();

        ApplyState();

        RpcDownHit();

        yield return new WaitForSeconds(time);

        if (currentAction == SurvivorAction.DownHit)
        {
            currentAction = SurvivorAction.None;
            ApplyState();
        }
    }

    [ClientRpc]
    private void RpcDownHit()
    {
        if (interactor != null)
            interactor.ForceStopInteract();

        if (move != null)
        {
            move.SetMoveLock(true);
            move.StopAnimation();

            // 다른 행동 애니메이션 정리
            move.SetCamAnim(false);
            move.SetSearching(false);
            move.SetVaulting(false);
            move.SetStunned(false);
        }

        if (animator != null)
            animator.SetTrigger("DownHit");
    }

    // 트랩 / QTE 실패 등에서 공통으로 사용하는 스턴 루틴
    [Server]
    public IEnumerator StunRoutine(float time)
    {
        if (time <= 0f)
            yield break;

        // 다운 피격 중이면 스턴으로 덮어쓰지 않는다.
        if (currentAction == SurvivorAction.DownHit)
            yield break;

        // 이미 스턴 중이면 중복 스턴 방지
        if (currentAction == SurvivorAction.Stunned)
            yield break;

        currentAction = SurvivorAction.Stunned;
        isCamSkill = false;
        isDoingInteraction = false;

        // 진행 중인 상호작용 강제 종료
        if (interactor != null)
            interactor.ForceStopInteract();

        if (move != null)
        {
            // 스턴 시작 전 다른 행동 Bool 정리
            move.SetCamAnim(false);
            move.SetSearching(false);
            move.SetVaulting(false);

            // 이동 애니메이션 정리
            move.StopAnimation();

            // 스턴 중 다른 애니메이션을 막기 위한 Bool
            move.SetStunned(true);

            // 실제 스턴 애니메이션 Trigger
            move.PlayAnimation("Stun");
        }
        else if (animator != null)
        {
            animator.SetBool("IsStunned", true);
            animator.SetTrigger("Stun");
        }

        ApplyState();

        yield return new WaitForSeconds(time);

        if (currentAction == SurvivorAction.Stunned)
        {
            currentAction = SurvivorAction.None;

            // 스턴 종료 후 Bool 해제
            if (move != null)
                move.SetStunned(false);
            else if (animator != null)
                animator.SetBool("IsStunned", false);

            ApplyState();
        }
    }

    // 다른 상태에서 강제로 행동 상태를 초기화할 때 사용
    [Server]
    public void ForceResetActionServer()
    {
        currentAction = SurvivorAction.None;
        isDoingInteraction = false;
        isCamSkill = false;

        // 스턴 Bool이 남아있으면 이후 애니메이션 전환이 막힐 수 있으므로 해제
        if (move != null)
            move.SetStunned(false);
        else if (animator != null)
            animator.SetBool("IsStunned", false);

        ApplyState();
    }
}