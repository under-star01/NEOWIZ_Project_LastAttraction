using UnityEngine;

public class EvidencePoint : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactTime = 10f;
    [SerializeField] private ProgressUI progressUI;

    private EvidenceZone zone;
    private bool isRealEvidence;
    private bool isCompleted;
    private bool isInteracting;
    private float progress;

    private SurvivorInteractor playerInteractor;

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

        progressUI?.Show();
        progressUI?.SetProgress(progress / interactTime);

        Debug.Log($"{name} 조사 시작");
    }

    public void EndInteract()
    {
        if (isCompleted) return;

        isInteracting = false;
        progress = 0f;

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
            playerInteractor = null;
    }

    private void CompleteInvestigation()
    {
        isCompleted = true;
        isInteracting = false;
        progress = interactTime;

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
}