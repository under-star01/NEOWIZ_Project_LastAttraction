using Mirror;
using UnityEngine;

public class SurvivorHeal : NetworkBehaviour, IInteractable
{
    // 힐은 Hold 타입
    public InteractType InteractType => InteractType.Hold;

    [Header("힐 설정")]
    [SerializeField] private float healTime = 16f;

    [Header("참조")]
    [SerializeField] private SurvivorState targetState;                 // 힐 대상 몸 상태
    [SerializeField] private SurvivorActionState targetActionState;     // 힐 대상 행동 상태
    [SerializeField] private SurvivorMove targetMove;                   // 힐 대상 이동 제어
    [SerializeField] private SurvivorInteractor targetInteractor;       // 힐 대상 UI 표시용

    [SyncVar] private uint healer;      // 현재 힐 중인 플레이어 netId
    [SyncVar] private bool isHealing;   // 현재 힐 중인지
    [SyncVar] private float progress;   // 현재 진행도

    // 힐하는 쪽 로컬 참조
    private SurvivorInteractor localHealerInteractor;
    private SurvivorState localHealerState;
    private SurvivorMove localHealerMove;

    // 힐받는 대상 쪽 로컬 참조
    private SurvivorInteractor localTargetInteractor;

    // 이 로컬 플레이어가 범위 안에 있는지
    private bool isLocalInside;

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponentInParent<SurvivorState>();

        if (targetActionState == null)
            targetActionState = GetComponentInParent<SurvivorActionState>();

        if (targetMove == null)
            targetMove = GetComponentInParent<SurvivorMove>();

        if (targetInteractor == null)
            targetInteractor = GetComponentInParent<SurvivorInteractor>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        CacheTarget();
    }

    private void Update()
    {
        if (isServer)
            HealUpdate();

        CacheTarget();
        UpdateUI();
        RefreshLocalAvailability();
    }

    public void BeginInteract(GameObject actor)
    {
        if (!CanHeal())
            return;

        if (IsBusy())
            return;

        FaceTarget();
        SetHealerLock(true);
        SetHealAnim(true);

        CmdBeginHeal();
    }

    public void EndInteract()
    {
        if (localHealerInteractor == null)
            return;

        // 실제 힐러 본인일 때만 종료
        bool isMyHeal = isHealing && healer == localHealerInteractor.netId;

        if (!isMyHeal)
            return;

        SetHealerLock(false);
        SetHealAnim(false);

        // 진행도는 유지하고 UI만 숨김
        localHealerInteractor.HideProgress(this, false);

        CmdEndHeal();
    }

    [Command(requiresAuthority = false)]
    private void CmdBeginHeal(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        SurvivorInteractor healerInteractor = sender.identity.GetComponent<SurvivorInteractor>();
        SurvivorState healerState = sender.identity.GetComponent<SurvivorState>();
        SurvivorMove healerMove = sender.identity.GetComponent<SurvivorMove>();

        if (healerInteractor == null || healerState == null || healerMove == null)
            return;

        if (targetState == null || targetMove == null)
            return;

        // 대상이 정상 상태면 힐 불가
        if (targetState.IsHealthy)
            return;

        // 감옥 상태면 힐 불가
        if (targetState.IsImprisoned)
            return;

        // 힐러가 다운 상태면 불가
        if (healerState.IsDowned)
            return;

        // 자기 자신 힐 방지
        if (healerState == targetState)
            return;

        // 이미 다른 플레이어가 힐 중이면 시작 불가
        if (isHealing && healer != sender.identity.netId)
            return;

        // 범위 밖이면 불가
        if (!CanUse(healerInteractor.transform))
            return;

        // 대상이 현재 다른 Hold 상호작용 중이면 힐 시작 금지
        if (targetActionState != null && targetActionState.IsDoingInteraction)
            return;

        isHealing = true;
        healer = sender.identity.netId;

        // 진행도는 유지
        // 끊겼다가 다시 잡으면 이어서 진행

        // 힐받는 대상은 다른 상호작용 못 하게
        if (targetActionState != null)
            targetActionState.SetBeingHealedServer(true);

        // 대상 본인 로컬 이동 잠금
        if (targetState.connectionToClient != null)
            TargetLockTarget(targetState.connectionToClient, true);
    }

    [Command(requiresAuthority = false)]
    private void CmdEndHeal(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        if (!isHealing)
            return;

        // 현재 힐 중인 본인만 종료 가능
        if (healer != sender.identity.netId)
            return;

        StopHeal();
    }

    [Server]
    private void HealUpdate()
    {
        if (!isHealing)
            return;

        if (!NetworkServer.spawned.TryGetValue(healer, out NetworkIdentity identity))
        {
            StopHeal();
            return;
        }

        SurvivorInteractor healerInteractor = identity.GetComponent<SurvivorInteractor>();
        SurvivorState healerState = identity.GetComponent<SurvivorState>();

        if (healerInteractor == null || healerState == null)
        {
            StopHeal();
            return;
        }

        // 대상이 정상 상태가 되었거나 힐러가 다운되면 종료
        if (targetState.IsHealthy || healerState.IsDowned)
        {
            StopHeal();
            return;
        }

        // 감옥 상태가 되면 힐 중단
        if (targetState.IsImprisoned)
        {
            StopHeal();
            return;
        }

        // 범위 밖이면 종료
        if (!CanUse(healerInteractor.transform))
        {
            StopHeal();
            return;
        }

        progress += Time.deltaTime;

        if (progress >= healTime)
            CompleteHeal();
    }

    [Server]
    private void StopHeal()
    {
        uint stoppedHealer = healer;

        isHealing = false;
        healer = 0;

        // 진행도는 유지
        if (targetActionState != null)
            targetActionState.SetBeingHealedServer(false);

        if (targetState.connectionToClient != null)
            TargetLockTarget(targetState.connectionToClient, false);

        RpcStopHeal(stoppedHealer);
    }

    [Server]
    private void CompleteHeal()
    {
        uint stoppedHealer = healer;

        isHealing = false;
        healer = 0;

        // 완전히 회복됐을 때만 진행도 초기화
        progress = 0f;

        if (targetActionState != null)
            targetActionState.SetBeingHealedServer(false);

        if (targetState.connectionToClient != null)
            TargetLockTarget(targetState.connectionToClient, false);

        if (targetState.IsDowned)
            targetState.RecoverToInjured();
        else if (targetState.IsInjured)
            targetState.HealToHealthy();

        RpcStopHeal(stoppedHealer);
    }

    [ClientRpc]
    private void RpcStopHeal(uint stoppedHealer)
    {
        // 실제 힐러 본인만 힐 애니메이션 종료
        if (localHealerMove != null && localHealerInteractor != null)
        {
            if (localHealerInteractor.netId == stoppedHealer)
            {
                localHealerMove.SetMoveLock(false);
                localHealerMove.SetSearching(false);
            }
        }

        // 힐받는 대상 쪽 이동 애니메이션 정지
        if (targetMove != null)
            targetMove.StopAnimation();

        if (localHealerInteractor != null)
            localHealerInteractor.HideProgress(this, false);

        if (localTargetInteractor != null)
            localTargetInteractor.HideProgress(this, false);
    }

    // 힐하는 쪽 / 힐받는 쪽 UI 갱신
    private void UpdateUI()
    {
        if (localHealerInteractor != null)
        {
            bool isMyHeal = false;

            if (isHealing && healer == localHealerInteractor.netId && !targetState.IsHealthy)
                isMyHeal = true;

            if (isMyHeal)
                localHealerInteractor.ShowProgress(this, progress / healTime);
            else
                localHealerInteractor.HideProgress(this, false);
        }

        if (localTargetInteractor != null && targetState.isLocalPlayer)
        {
            bool isMyTargetHeal = false;

            if (isHealing && !targetState.IsHealthy)
                isMyTargetHeal = true;

            if (isMyTargetHeal)
                localTargetInteractor.ShowProgress(this, progress / healTime);
            else
                localTargetInteractor.HideProgress(this, false);
        }
    }

    // 로컬 플레이어가 힐 후보 유지 가능한지 갱신
    private void RefreshLocalAvailability()
    {
        if (!isLocalInside)
            return;

        if (localHealerInteractor == null)
            return;

        if (CanHeal() && !IsBusy())
            localHealerInteractor.SetInteractable(this);
        else
            localHealerInteractor.ClearInteractable(this);
    }

    // 힐 가능 조건 검사
    private bool CanHeal()
    {
        if (targetState == null)
            return false;

        // 정상 상태는 힐 대상 아님
        if (targetState.IsHealthy)
            return false;

        // 감옥 상태 힐 불가
        if (targetState.IsImprisoned)
            return false;

        if (localHealerInteractor == null || localHealerState == null)
            return false;

        // 다운된 생존자는 힐러가 될 수 없음
        if (localHealerState.IsDowned)
            return false;

        // 자기 자신 힐 방지
        if (localHealerState == targetState)
            return false;

        // 대상이 다른 Hold 상호작용 중이면 힐 시작 불가
        if (targetActionState != null && targetActionState.IsDoingInteraction)
            return false;

        return true;
    }

    // 다른 사람이 이미 힐 중인지 검사
    private bool IsBusy()
    {
        if (!isHealing)
            return false;

        if (localHealerInteractor == null)
            return true;

        return healer != localHealerInteractor.netId;
    }

    // 힐 범위 검사
    private bool CanUse(Transform healerTransform)
    {
        Collider col = GetComponent<Collider>();

        if (col == null)
            col = GetComponentInChildren<Collider>();

        if (col == null)
            return false;

        Vector3 closest = col.ClosestPoint(healerTransform.position);
        float sqrDist = (closest - healerTransform.position).sqrMagnitude;

        return sqrDist <= 4f;
    }

    private void SetHealAnim(bool value)
    {
        if (localHealerMove != null)
            localHealerMove.SetSearching(value);
    }

    private void FaceTarget()
    {
        if (localHealerMove == null)
            return;

        Vector3 lookDir = targetState.transform.position - localHealerMove.transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        localHealerMove.FaceDirection(lookDir.normalized);
    }

    private void SetHealerLock(bool value)
    {
        if (localHealerMove != null)
            localHealerMove.SetMoveLock(value);
    }

    private void CacheTarget()
    {
        if (targetState == null)
            return;

        if (!targetState.isLocalPlayer)
            return;

        if (localTargetInteractor == null)
            localTargetInteractor = targetInteractor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor == null || !interactor.isLocalPlayer)
            return;

        localHealerInteractor = interactor;
        localHealerState = interactor.GetComponent<SurvivorState>();
        localHealerMove = interactor.GetComponent<SurvivorMove>();

        // 자기 자신 힐 방지
        if (localHealerState == targetState)
            return;

        isLocalInside = true;
        RefreshLocalAvailability();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor == null || !interactor.isLocalPlayer)
            return;

        interactor.ClearInteractable(this);
        isLocalInside = false;

        if (localHealerInteractor != interactor)
            return;

        // 실제 힐러 본인일 때만 종료
        bool isMyHeal = isHealing && healer == interactor.netId;

        if (isMyHeal)
        {
            SetHealerLock(false);
            SetHealAnim(false);
            localHealerInteractor.HideProgress(this, false);
            CmdEndHeal();
        }

        localHealerInteractor = null;
        localHealerState = null;
        localHealerMove = null;
    }

    // 힐받는 대상 본인 클라이언트에만 이동 잠금 전달
    [TargetRpc]
    private void TargetLockTarget(NetworkConnection target, bool value)
    {
        if (targetMove == null)
            return;

        targetMove.SetMoveLock(value);

        if (value)
            targetMove.StopAnimation();
    }
}