using Mirror;
using UnityEngine;

public class EvidencePoint : NetworkBehaviour, IInteractable
{
    public InteractType InteractType => InteractType.Hold;

    [Header("조사 설정")]
    [SerializeField] private float interactTime = 10f;
    [SerializeField] private ProgressUI progressUI;

    private EvidenceZone zone;

    [SyncVar]
    private bool isRealEvidence;

    [SyncVar(hook = nameof(OnCompletedChanged))]
    private bool isCompleted;

    [SyncVar]
    private bool isInteracting;

    [SyncVar]
    private float progress;

    // 현재 조사 중인 플레이어
    [SyncVar]
    private uint currentInteractorNetId;

    private SurvivorInteractor localInteractor;
    private SurvivorMove localMove;

    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

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
        if (isServer)
        {
            ServerUpdateInteract();
        }

        UpdateLocalUI();
    }

    // 로컬 플레이어가 조사 시작 요청
    public void BeginInteract()
    {
        if (isCompleted)
            return;

        if (localInteractor == null)
            return;

        FaceToEvidenceLocal();
        LockMovementLocal(true);
        SetSearchingLocal(true);

        CmdBeginInteract();
    }

    // 로컬 플레이어가 조사 중단 요청
    public void EndInteract()
    {
        if (localInteractor == null)
            return;

        LockMovementLocal(false);
        SetSearchingLocal(false);

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

        SurvivorMove move = sender.identity.GetComponent<SurvivorMove>();
        if (move == null)
            return;

        // 이미 다른 사람이 조사 중이면 막기
        if (isInteracting && currentInteractorNetId != sender.identity.netId)
            return;

        // 범위 체크
        if (!CanInteractorUseThis(interactor.transform))
            return;

        isInteracting = true;
        currentInteractorNetId = sender.identity.netId;
    }

    [Command(requiresAuthority = false)]
    private void CmdEndInteract(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        if (!isInteracting)
            return;

        if (currentInteractorNetId != sender.identity.netId)
            return;

        isInteracting = false;
        currentInteractorNetId = 0;
    }

    [Server]
    private void ServerUpdateInteract()
    {
        if (!isInteracting || isCompleted)
            return;

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

    [Server]
    private void StopServerInteract()
    {
        isInteracting = false;
        currentInteractorNetId = 0;
    }

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
        gameObject.SetActive(false);
    }

    [ClientRpc]
    private void RpcForceStopLocalEffects()
    {
        if (localMove != null)
        {
            localMove.SetMoveLock(false);
            localMove.SetSearching(false);
        }

        if (progressUI != null)
            progressUI.Hide();
    }

    private void OnCompletedChanged(bool oldValue, bool newValue)
    {
        if (!newValue)
            return;

        if (progressUI != null)
            progressUI.Hide();
    }

    private void UpdateLocalUI()
    {
        if (progressUI == null)
            return;

        if (localInteractor == null)
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
            progressUI.Hide();
        }
    }

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

    private void LockMovementLocal(bool value)
    {
        if (localMove != null)
            localMove.SetMoveLock(value);
    }

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

        if (!interactor.isLocalPlayer)
            return;

        localInteractor = interactor;

        localMove = interactor.GetComponent<SurvivorMove>();
        if (localMove == null)
            localMove = interactor.GetComponentInParent<SurvivorMove>();

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

            CmdEndInteract();

            localInteractor = null;
            localMove = null;
        }

        Debug.Log($"{name} 범위 이탈");
    }
}