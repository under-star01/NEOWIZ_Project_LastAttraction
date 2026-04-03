using Mirror;
using UnityEngine;

public class EvidencePoint : NetworkBehaviour, IInteractable
{
    // 이 오브젝트는 Hold 타입 상호작용
    public InteractType InteractType => InteractType.Hold;

    [Header("조사 설정")]
    [SerializeField] private float interactTime = 10f;
    [SerializeField] private ProgressUI progressUI;

    private EvidenceZone zone;

    // 진짜 증거 여부
    [SyncVar]
    private bool isRealEvidence;

    // 완료 여부
    // 완료되면 hook이 호출되어 UI 등을 정리할 수 있다.
    [SyncVar(hook = nameof(OnCompletedChanged))]
    private bool isCompleted;

    // 현재 조사 중인지
    [SyncVar]
    private bool isInteracting;

    // 현재 진행도 시간
    [SyncVar]
    private float progress;

    // 현재 조사 중인 플레이어의 netId
    [SyncVar]
    private uint currentInteractorNetId;

    // 로컬 플레이어 쪽 캐시 참조
    private SurvivorInteractor localInteractor;
    private SurvivorMove localMove;

    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

    // 서버에서만 진짜 증거 여부를 설정
    [Server]
    public void SetIsRealEvidenceServer(bool value)
    {
        isRealEvidence = value;
    }

    private void Awake()
    {
        if (progressUI == null)
            progressUI = FindFirstObjectByType<ProgressUI>();
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

    // 로컬 플레이어가 상호작용 시작
    public void BeginInteract()
    {
        if (isCompleted)
            return;

        if (localInteractor == null)
            return;

        // 시작할 때 증거 방향을 보게 하고
        // 로컬에서 즉시 이동 잠금 / searching 애니메이션 적용
        FaceToEvidenceLocal();
        LockMovementLocal(true);
        SetSearchingLocal(true);

        // 서버에 조사 시작 요청
        CmdBeginInteract();
    }

    // 로컬 플레이어가 상호작용 취소
    public void EndInteract()
    {
        if (localInteractor == null)
            return;

        // 로컬에서 즉시 이동 잠금 해제 / searching 종료
        LockMovementLocal(false);
        SetSearchingLocal(false);

        // UI도 바로 꺼주고 0으로 초기화
        if (progressUI != null)
        {
            progressUI.SetProgress(0f);
            progressUI.Hide();
        }

        // 서버에 조사 중단 요청
        CmdEndInteract();
    }

    // 서버에 조사 시작 요청
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

        SurvivorMove move = sender.identity.GetComponent<SurvivorMove>();
        if (move == null)
            return;

        // 이미 다른 사람이 조사 중이면 막음
        if (isInteracting && currentInteractorNetId != sender.identity.netId)
            return;

        // 범위 안에 있는지 서버가 최종 확인
        if (!CanInteractorUseThis(interactor.transform))
            return;

        isInteracting = true;
        currentInteractorNetId = sender.identity.netId;

        // 새로 시작했으니 progress도 0부터 시작
        progress = 0f;
    }

    // 서버에 조사 중단 요청
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

        // 여기서 progress를 0으로 초기화하는 것이 중요
        // 이게 없으면 다시 잡았을 때 이전 진행도가 남는다.
        isInteracting = false;
        currentInteractorNetId = 0;
        progress = 0f;

        // 취소 시점에 로컬 효과도 확실히 꺼줌
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

        // 진행도 증가
        progress += Time.deltaTime;

        // 완료
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

        // 클라 로컬 효과도 정리
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

        // 완료 시 로컬 이동 잠금 / 애니메이션 / UI 정리
        RpcForceStopLocalEffects();

        // 완전히 끝난 뒤 비활성화
        gameObject.SetActive(false);
    }

    // 모든 클라이언트에서 로컬 효과 강제 종료
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

    // 완료 상태가 바뀌었을 때 UI 정리
    private void OnCompletedChanged(bool oldValue, bool newValue)
    {
        if (!newValue)
            return;

        if (progressUI != null)
            progressUI.Hide();
    }

    // 로컬 플레이어에게만 자기 조사 UI 표시
    private void UpdateLocalUI()
    {
        if (progressUI == null)
            return;

        if (localInteractor == null)
            return;

        // 내가 현재 조사 중인 플레이어인지 확인
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
            progressUI.Hide();
        }
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

    // 로컬에서 증거 방향 보게 하기
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

    // 로컬 이동 잠금
    private void LockMovementLocal(bool value)
    {
        if (localMove != null)
            localMove.SetMoveLock(value);
    }

    // 로컬 searching 애니메이션 on/off
    private void SetSearchingLocal(bool value)
    {
        if (localMove != null)
            localMove.SetSearching(value);
    }

    // 범위 진입 시 로컬 플레이어만 상호작용 등록
    private void OnTriggerEnter(Collider other)
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

        localInteractor = interactor;

        localMove = interactor.GetComponent<SurvivorMove>();
        if (localMove == null)
            localMove = interactor.GetComponentInParent<SurvivorMove>();

        interactor.SetInteractable(this);
        Debug.Log($"{name} 범위 진입");
    }

    // 범위 이탈 시 상호작용 해제 + 취소 처리
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
            // 로컬 효과 즉시 해제
            LockMovementLocal(false);
            SetSearchingLocal(false);

            if (progressUI != null)
            {
                progressUI.SetProgress(0f);
                progressUI.Hide();
            }

            // 서버에도 취소 요청
            CmdEndInteract();

            localInteractor = null;
            localMove = null;
        }

        Debug.Log($"{name} 범위 이탈");
    }
}