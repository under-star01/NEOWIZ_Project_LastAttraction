using UnityEngine;

public class EvidencePoint : MonoBehaviour, IInteractable
{
    public InteractType InteractType => InteractType.Hold;

    [SerializeField] private float interactTime = 10f;
    [SerializeField] private ProgressUI progressUI;

    private EvidenceZone zone;
    private bool isRealEvidence;
    private bool isCompleted;
    private bool isInteracting;
    private float progress;

    private SurvivorInteractor playerInteractor;
    private SurvivorMove playerMove;

    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

    public void SetIsRealEvidence(bool value)
    {
        isRealEvidence = value;
    }

    private void Awake()
    {
        if (progressUI == null)
            progressUI = FindFirstObjectByType<ProgressUI>();
    }

    public void BeginInteract()
    {
        if (isCompleted) return;

        isInteracting = true;

        FacePlayerToEvidence();
        LockPlayerMovement(true);

        progressUI?.Show();
        progressUI?.SetProgress(progress / interactTime);

        Debug.Log($"{name} 조사 시작");
    }

    public void EndInteract()
    {
        if (isCompleted) return;

        isInteracting = false;
        progress = 0f;

        LockPlayerMovement(false);

        progressUI?.Hide();

        Debug.Log($"{name} 조사 중단");
    }

    private void Update()
    {
        if (!isInteracting || isCompleted) return;

        progress += Time.deltaTime;

        float normalized = progress / interactTime;
        progressUI?.SetProgress(normalized);

        if (progress >= interactTime)
        {
            CompleteInvestigation();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Survivor")) return;

        playerInteractor = other.GetComponent<SurvivorInteractor>();
        if (playerInteractor == null)
            playerInteractor = other.GetComponentInParent<SurvivorInteractor>();

        if (playerInteractor != null)
        {
            playerMove = playerInteractor.GetComponent<SurvivorMove>();
            if (playerMove == null)
                playerMove = playerInteractor.GetComponentInParent<SurvivorMove>();

            playerInteractor.SetInteractable(this);
            Debug.Log($"{name} 범위 진입");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Survivor")) return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor != null)
        {
            interactor.ClearInteractable(this);
            Debug.Log($"{name} 범위 이탈");
        }

        if (playerInteractor == interactor)
        {
            LockPlayerMovement(false);
            playerInteractor = null;
            playerMove = null;
        }
    }

    private void CompleteInvestigation()
    {
        isCompleted = true;
        isInteracting = false;
        progress = interactTime;

        LockPlayerMovement(false);

        progressUI?.SetProgress(1f);
        progressUI?.Hide();

        if (isRealEvidence)
        {
            Debug.Log($"{name} : 진짜 증거 발견!");
            zone?.OnRealEvidenceFound(this);
        }
        else
        {
            Debug.Log($"{name} : 아무 증거도 없음");
        }

        gameObject.SetActive(false);
    }

    private void FacePlayerToEvidence()
    {
        if (playerMove == null)
            return;

        Vector3 lookDir = transform.position - playerMove.transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        playerMove.FaceDirection(lookDir.normalized);
    }

    private void LockPlayerMovement(bool value)
    {
        if (playerMove != null)
            playerMove.SetMoveLock(value);
    }
}