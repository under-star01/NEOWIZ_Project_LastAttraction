using UnityEngine;

public class EvidencePoint : MonoBehaviour, IInteractable
{
    // 이 오브젝트는 Hold 타입 상호작용
    public InteractType InteractType => InteractType.Hold;

    [Header("조사 설정")]
    [SerializeField] private float interactTime = 10f; // 조사 완료까지 걸리는 시간
    [SerializeField] private ProgressUI progressUI;    // 진행도 UI

    private EvidenceZone zone;         // 어떤 구역에 속하는지
    private bool isRealEvidence;       // 진짜 증거인지
    private bool isCompleted;          // 이미 조사 끝났는지
    private bool isInteracting;        // 현재 조사 중인지
    private float progress;            // 현재 조사 진행 시간

    private SurvivorInteractor playerInteractor; // 현재 상호작용 중인 플레이어
    private SurvivorMove playerMove;             // 이동 잠금용 참조

    // EvidenceZone이 자기 자신을 등록
    public void SetZone(EvidenceZone evidenceZone)
    {
        zone = evidenceZone;
    }

    // EvidenceZone이 진짜/가짜 여부 지정
    public void SetIsRealEvidence(bool value)
    {
        isRealEvidence = value;
    }

    private void Awake()
    {
        if (progressUI == null)
            progressUI = FindFirstObjectByType<ProgressUI>();
    }

    // 조사 시작
    public void BeginInteract()
    {
        if (isCompleted)
            return;

        isInteracting = true;

        // 조사 시작할 때 플레이어를 증거 쪽으로 돌림
        FaceToEvidence();

        // 이동만 막음
        // SurvivorMove에서 Look()는 계속 돌기 때문에 마우스 회전은 가능
        LockMovement(true);

        // 써칭 애니메이션 시작
        SetSearching(true);

        progressUI?.Show();
        progressUI?.SetProgress(progress / interactTime);

        Debug.Log($"{name} 조사 시작");
    }

    // 조사 중단
    public void EndInteract()
    {
        if (isCompleted)
            return;

        isInteracting = false;
        progress = 0f; // 중간 취소 시 처음부터 다시

        LockMovement(false);

        // 써칭 애니메이션 종료
        SetSearching(false);

        progressUI?.Hide();

        Debug.Log($"{name} 조사 중단");
    }

    private void Update()
    {
        if (!isInteracting || isCompleted)
            return;

        progress += Time.deltaTime;

        float normalized = progress / interactTime;
        progressUI?.SetProgress(normalized);

        if (progress >= interactTime)
        {
            Complete();
        }
    }

    // 조사 완료 처리
    private void Complete()
    {
        isCompleted = true;
        isInteracting = false;
        progress = interactTime;

        LockMovement(false);

        // 써칭 애니메이션 종료
        SetSearching(false);

        progressUI?.SetProgress(1f);
        progressUI?.Hide();

        if (isRealEvidence)
        {
            Debug.Log($"{name} : 진짜 증거 발견!");
            zone?.OnRealEvidenceFound(this);
        }
        else
        {
            Debug.Log($"{name} : 가짜 포인트");
        }

        // 한 번 조사 끝난 포인트는 비활성화
        gameObject.SetActive(false);
    }

    // 플레이어가 증거 방향을 보도록 맞춤
    private void FaceToEvidence()
    {
        if (playerMove == null)
            return;

        Vector3 lookDir = transform.position - playerMove.transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        playerMove.FaceDirection(lookDir.normalized);
    }

    // 이동 잠금/해제
    private void LockMovement(bool value)
    {
        if (playerMove != null)
            playerMove.SetMoveLock(value);
    }

    // 써칭 애니메이션 on/off
    private void SetSearching(bool value)
    {
        if (playerMove != null)
            playerMove.SetSearching(value);
    }

    // 플레이어가 범위 안에 들어오면 상호작용 가능 대상으로 등록
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

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

    // 범위를 벗어나면 상호작용 해제
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Survivor"))
            return;

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
            LockMovement(false);
            SetSearching(false);

            playerInteractor = null;
            playerMove = null;
        }
    }
}