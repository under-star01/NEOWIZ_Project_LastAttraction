using Mirror;
using UnityEngine;

public class EvidencePoint : NetworkBehaviour, IInteractable
{
    // 이 오브젝트는 Hold 타입 상호작용
    public InteractType InteractType => InteractType.Hold;

    [Header("조사 설정")]
    [SerializeField] private float interactTime = 10f;
    [SerializeField] private ProgressUI progressUI; // 현재 상호작용 중인 로컬 플레이어의 UI를 연결받음

    private EvidenceZone zone;

    [SyncVar]
    private bool isRealEvidence; // 진짜 증거인지

    [SyncVar(hook = nameof(OnCompletedChanged))]
    private bool isCompleted; // 이미 완료되었는지

    [SyncVar]
    private bool isInteracting; // 현재 누군가 조사 중인지

    [SyncVar]
    private float progress; // 현재 조사 진행도 시간

    [SyncVar]
    private uint currentInteractorNetId; // 현재 조사 중인 플레이어 netId

    private SurvivorInteractor localInteractor; // 이 클라이언트 기준 로컬 플레이어
    private SurvivorMove localMove;             // 로컬 플레이어 이동 제어용

    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

    [Server]
    public void SetIsRealEvidenceServer(bool value)
    {
        isRealEvidence = value;
    }

    private void Update()
    {
        // 실제 진행도 증가는 서버에서만 처리
        if (isServer)
        {
            ServerUpdateInteract();
        }

        // UI 표시는 각 클라이언트 로컬에서 처리
        UpdateLocalUI();
    }

    // 로컬 플레이어가 조사 시작 시 호출
    public void BeginInteract()
    {
        if (isCompleted)
            return;

        if (localInteractor == null)
            return;

        // 이미 다른 사람이 조사 중이면 시작 불가
        if (IsBusyByOtherLocal())
            return;

        // 로컬 체감용 처리
        // 바로 증거 쪽을 바라보고, 움직임을 잠그고, 조사 애니메이션 시작
        FaceToEvidenceLocal();
        LockMovementLocal(true);
        SetSearchingLocal(true);

        // 실제 시작 판정은 서버에 요청
        CmdBeginInteract();
    }

    // 로컬 플레이어가 조사 취소 시 호출
    public void EndInteract()
    {
        if (localInteractor == null)
            return;

        // 로컬 효과 즉시 정리
        LockMovementLocal(false);
        SetSearchingLocal(false);

        if (progressUI != null)
        {
            progressUI.SetProgress(0f);
            progressUI.Hide();
        }

        // 실제 취소는 서버에 요청
        CmdEndInteract();
    }

    [Command(requiresAuthority = false)]
    private void CmdBeginInteract(NetworkConnectionToClient sender = null)
    {
        if (isCompleted)
            return;

        if (sender == null || sender.identity == null)
            return;

        SurvivorInteractor interactor = sender.identity.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            return;

        // SurvivorMove가 없으면 상호작용 불가
        if (sender.identity.GetComponent<SurvivorMove>() == null)
            return;

        // 이미 다른 사람이 하고 있으면 시작 막기
        if (isInteracting && currentInteractorNetId != sender.identity.netId)
            return;

        // 서버 기준 범위 체크
        if (!CanInteractorUseThis(interactor.transform))
            return;

        isInteracting = true;
        currentInteractorNetId = sender.identity.netId;
        progress = 0f;
    }

    [Command(requiresAuthority = false)]
    private void CmdEndInteract(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        if (!isInteracting)
            return;

        // 현재 조사 중인 본인만 취소 가능
        if (currentInteractorNetId != sender.identity.netId)
            return;

        isInteracting = false;
        currentInteractorNetId = 0;
        progress = 0f;

        // 각 클라이언트의 로컬 효과 정리
        RpcForceStopLocalEffects();
    }

    // 서버에서 매 프레임 조사 진행
    [Server]
    private void ServerUpdateInteract()
    {
        if (!isInteracting || isCompleted)
            return;

        // 현재 조사 중인 플레이어 찾기
        if (!NetworkServer.spawned.TryGetValue(currentInteractorNetId, out NetworkIdentity identity))
        {
            StopServerInteract();
            return;
        }

        SurvivorInteractor interactor = identity.GetComponent<SurvivorInteractor>();
        if (interactor == null)
        {
            StopServerInteract();
            return;
        }

        // 범위를 벗어나면 자동 취소
        if (!CanInteractorUseThis(interactor.transform))
        {
            StopServerInteract();
            return;
        }

        progress += Time.deltaTime;

        if (progress >= interactTime)
        {
            CompleteServer();
        }
    }

    // 서버에서 조사 중단 처리
    [Server]
    private void StopServerInteract()
    {
        isInteracting = false;
        currentInteractorNetId = 0;
        progress = 0f;

        RpcForceStopLocalEffects();
    }

    // 서버에서 조사 완료 처리
    [Server]
    private void CompleteServer()
    {
        isCompleted = true;
        isInteracting = false;
        currentInteractorNetId = 0;
        progress = interactTime;

        if (isRealEvidence)
        {
            Debug.Log($"{name} : 진짜 증거 발견!");
            zone?.OnRealEvidenceFound(this);
        }
        else
        {
            Debug.Log($"{name} : 가짜 포인트");
        }

        RpcForceStopLocalEffects();

        // 완료된 증거는 비활성화
        gameObject.SetActive(false);
    }

    // 모든 클라이언트에서 로컬 효과 정리
    [ClientRpc]
    private void RpcForceStopLocalEffects()
    {
        if (localMove != null)
        {
            localMove.SetMoveLock(false);
            localMove.SetSearching(false);
        }

        if (progressUI != null)
        {
            progressUI.SetProgress(0f);
            progressUI.Hide();
        }
    }

    private void OnCompletedChanged(bool oldValue, bool newValue)
    {
        if (!newValue)
            return;

        if (progressUI != null)
            progressUI.Hide();
    }

    // 현재 로컬 플레이어가 조사 중일 때만 UI 표시
    private void UpdateLocalUI()
    {
        if (progressUI == null && localInteractor != null)
        {
            progressUI = localInteractor.ProgressUI;
        }

        if (progressUI == null)
            return;

        if (localInteractor == null)
            return;

        // 현재 로컬 플레이어가 실제로 잡고 있는 상호작용 대상이
        // 이 EvidencePoint가 아니면 UI를 건드리지 않는다.
        // 다른 상호작용 오브젝트가 같은 ProgressUI를 쓰고 있을 수 있기 때문.
        if (!localInteractor.IsCurrentInteractable(this) && !isInteracting)
            return;

        bool isMyInteract =
            isInteracting &&
            localInteractor.netId == currentInteractorNetId &&
            !isCompleted;

        if (isMyInteract)
        {
            progressUI.Show();
            progressUI.SetProgress(progress / interactTime);
        }
        else
        {
            // 이 오브젝트가 실제 상호작용 대상일 때만 Hide
            // 그래야 다른 오브젝트 UI를 덮어쓰지 않음
            if (localInteractor.IsCurrentInteractable(this))
            {
                progressUI.Hide();
            }
        }
    }

    // 이미 다른 사람이 조사 중인지 로컬 기준으로 확인
    private bool IsBusyByOtherLocal()
    {
        if (!isInteracting)
            return false;

        if (localInteractor == null)
            return true;

        return currentInteractorNetId != localInteractor.netId;
    }

    // 서버 기준 범위 체크
    private bool CanInteractorUseThis(Transform interactorTransform)
    {
        if (interactorTransform == null)
            return false;

        Collider myCol = GetComponent<Collider>();
        if (myCol == null)
            myCol = GetComponentInChildren<Collider>();

        if (myCol == null)
            return false;

        Vector3 closest = myCol.ClosestPoint(interactorTransform.position);
        float sqrDist = (closest - interactorTransform.position).sqrMagnitude;

        return sqrDist <= 4f;
    }

    // 로컬에서 증거 방향 바라보기
    private void FaceToEvidenceLocal()
    {
        if (localMove == null)
            return;

        Vector3 lookDir = transform.position - localMove.transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        localMove.FaceDirection(lookDir.normalized);
    }

    // 로컬에서 이동 잠금
    private void LockMovementLocal(bool value)
    {
        if (localMove != null)
            localMove.SetMoveLock(value);
    }

    // 로컬에서 조사 애니메이션 on/off
    private void SetSearchingLocal(bool value)
    {
        if (localMove != null)
            localMove.SetSearching(value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor == null)
            return;

        // 로컬 플레이어만 자기 UI를 연결해야 하므로
        // 내 플레이어가 아니면 무시
        if (!interactor.isLocalPlayer)
            return;

        localInteractor = interactor;

        localMove = interactor.GetComponent<SurvivorMove>();
        if (localMove == null)
            localMove = interactor.GetComponentInParent<SurvivorMove>();

        // 이 로컬 플레이어가 들고 있는 ProgressUI를 연결
        progressUI = interactor.ProgressUI;

        // 이미 다른 플레이어가 조사 중이면 등록하지 않음
        if (IsBusyByOtherLocal())
        {
            Debug.Log($"{name} : 다른 플레이어가 조사 중이라 상호작용 불가");
            return;
        }

        interactor.SetInteractable(this);
        Debug.Log($"{name} 범위 진입");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor == null)
            return;

        if (!interactor.isLocalPlayer)
            return;

        interactor.ClearInteractable(this);

        if (localInteractor == interactor)
        {
            LockMovementLocal(false);
            SetSearchingLocal(false);

            if (progressUI != null)
            {
                progressUI.SetProgress(0f);
                progressUI.Hide();
            }

            CmdEndInteract();

            localInteractor = null;
            localMove = null;
        }

        Debug.Log($"{name} 범위 이탈");
    }
}