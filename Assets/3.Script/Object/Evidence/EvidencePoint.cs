using Mirror;
using UnityEngine;

public class EvidencePoint : NetworkBehaviour, IInteractable
{
    // 증거는 Hold 타입
    public InteractType InteractType => InteractType.Hold;

    [Header("조사 설정")]
    [SerializeField] private float interactTime = 10f;   // 조사 완료까지 걸리는 시간
    [SerializeField] private ProgressUI progressUI;      // 진행도 UI

    private EvidenceZone zone;                           // 이 포인트가 속한 구역

    [SyncVar]
    private bool isRealEvidence;                         // 진짜 증거인지 여부

    [SyncVar]
    private bool isCompleted;                            // 조사 완료 여부

    [SyncVar]
    private bool isInteracting;                          // 현재 누가 조사 중인지

    [SyncVar]
    private float progress;                              // 현재 조사 진행도

    [SyncVar]
    private uint currentInteractorNetId;                 // 현재 조사 중인 플레이어 netId

    // 로컬 플레이어 관련 참조
    private SurvivorInteractor localInteractor;
    private SurvivorMove localMove;

    // 내 로컬 플레이어가 이 증거 범위 안에 있는지
    private bool isLocalInside;

    // 어느 EvidenceZone 소속인지 저장
    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

    // 서버만 진짜/가짜 증거 여부 설정
    [Server]
    public void SetIsRealEvidenceServer(bool value)
    {
        isRealEvidence = value;
    }

    private void Update()
    {
        // 실제 조사 진행은 서버에서만 처리
        if (isServer)
            ServerUpdateInteract();

        // UI 갱신은 각 클라이언트 로컬에서 처리
        UpdateLocalUI();

        // 현재 로컬 플레이어가 이 증거를 상호작용 후보로 유지할지 갱신
        RefreshLocalAvailability();
    }

    // 상호작용 시작
    public void BeginInteract(GameObject actor)
    {
        // 이미 완료된 증거면 시작 불가
        if (isCompleted)
            return;

        if (localInteractor == null)
            return;

        // 다른 사람이 조사 중이면 시작 불가
        if (IsBusyByOtherLocal())
            return;

        // 로컬 연출 시작
        FaceToEvidenceLocal();
        LockMovementLocal(true);
        SetSearchingLocal(true);

        // 실제 시작 판정은 서버에 요청
        CmdBeginInteract();
    }

    // 상호작용 종료
    public void EndInteract()
    {
        if (localInteractor == null)
            return;

        // 로컬 연출 종료
        LockMovementLocal(false);
        SetSearchingLocal(false);

        if (progressUI != null)
        {
            progressUI.SetProgress(0f);
            progressUI.Hide();
        }

        // 실제 종료는 서버에 요청
        CmdEndInteract();
    }

    // 클라이언트 -> 서버 : 조사 시작 요청
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

        // 이미 다른 사람이 조사 중이면 시작 불가
        if (isInteracting && currentInteractorNetId != sender.identity.netId)
            return;

        // 범위 밖이면 시작 불가
        if (!CanInteractorUseThis(sender.identity.transform))
            return;

        isInteracting = true;
        currentInteractorNetId = sender.identity.netId;
        progress = 0f;
    }

    // 클라이언트 -> 서버 : 조사 종료 요청
    [Command(requiresAuthority = false)]
    private void CmdEndInteract(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        if (!isInteracting)
            return;

        // 현재 조사 중인 플레이어만 종료 가능
        if (currentInteractorNetId != sender.identity.netId)
            return;

        StopServerInteract();
    }

    // 서버에서 조사 진행
    [Server]
    private void ServerUpdateInteract()
    {
        if (!isInteracting || isCompleted)
            return;

        // 조사 중인 플레이어를 찾을 수 없으면 중단
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

        // 범위를 벗어나면 중단
        if (!CanInteractorUseThis(identity.transform))
        {
            StopServerInteract();
            return;
        }

        // 진행도 증가
        progress += Time.deltaTime;

        // 완료되면 서버 처리
        if (progress >= interactTime)
            CompleteServer();
    }

    // 서버에서 조사 중단
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

        // 진짜 증거면 구역에 알림
        if (isRealEvidence)
        {
            Debug.Log($"{name} : 진짜 증거 발견!");
            zone?.OnRealEvidenceFound(this);
        }
        else
        {
            Debug.Log($"{name} : 가짜 포인트");
        }

        // 모든 클라이언트에서 조사 연출 종료
        RpcForceStopLocalEffects();

        // 모든 클라이언트에서 이 포인트를 직접 숨김
        RpcHideEvidence();
    }

    // 모든 클라이언트에서 로컬 연출 종료
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

        if (localInteractor != null)
            localInteractor.ClearInteractable(this);
    }

    // 모든 클라이언트에서 이 포인트를 직접 숨김
    [ClientRpc]
    private void RpcHideEvidence()
    {
        HideEvidenceLocal();
    }

    // 실제 로컬 오브젝트 숨기기
    private void HideEvidenceLocal()
    {
        // 상호작용 후보에서 제거
        if (localInteractor != null)
            localInteractor.ClearInteractable(this);

        // 이 오브젝트와 자식들의 충돌 제거
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        // 이 오브젝트와 자식들의 렌더러 제거
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;

        // UI 숨김
        if (progressUI != null)
        {
            progressUI.SetProgress(0f);
            progressUI.Hide();
        }

        // 마지막에 오브젝트 자체 비활성화
        gameObject.SetActive(false);
    }

    // 내 로컬 플레이어가 조사 중일 때만 진행도 UI 표시
    private void UpdateLocalUI()
    {
        if (progressUI == null)
            return;

        if (localInteractor == null)
            return;

        bool isMyInteract = false;

        if (isInteracting && !isCompleted && localInteractor.netId == currentInteractorNetId)
            isMyInteract = true;

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

    // 현재 로컬 플레이어가 이 증거를 상호작용 후보로 유지할지 판단
    private void RefreshLocalAvailability()
    {
        if (!isLocalInside)
            return;

        if (localInteractor == null)
            return;

        // 완료된 증거면 후보 제거
        if (isCompleted)
        {
            localInteractor.ClearInteractable(this);
            return;
        }

        // 다른 사람이 사용 중이면 후보 제거
        if (IsBusyByOtherLocal())
        {
            localInteractor.ClearInteractable(this);
            return;
        }

        localInteractor.SetInteractable(this);
    }

    // 다른 사람이 조사 중인지 판정
    private bool IsBusyByOtherLocal()
    {
        if (!isInteracting)
            return false;

        if (localInteractor == null)
            return true;

        return currentInteractorNetId != localInteractor.netId;
    }

    // 서버 기준 사용 가능 거리 검사
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

    // 로컬에서 증거를 바라보게 함
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

    // 로컬 조사 애니메이션 on/off
    private void SetSearchingLocal(bool value)
    {
        if (localMove != null)
            localMove.SetSearching(value);
    }

    // 로컬 플레이어가 범위 안에 들어오면 상호작용 후보 등록
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

        progressUI = interactor.ProgressUI;
        isLocalInside = true;

        RefreshLocalAvailability();
    }

    // 로컬 플레이어가 범위 밖으로 나가면 상호작용 후보 제거
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
        isLocalInside = false;

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
    }
}