using UnityEngine;

public class SurvivorHeal : MonoBehaviour, IInteractable
{
    // 이 오브젝트는 Hold 타입 상호작용
    public InteractType InteractType => InteractType.Hold;

    [Header("힐 설정")]
    [SerializeField] private float healTime = 30f;       // 힐 완료까지 걸리는 시간
    [SerializeField] private ProgressUI progressUI;      // 진행도 UI

    [Header("참조")]
    [SerializeField] private SurvivorState targetState;  // 힐 받을 대상의 상태
    [SerializeField] private SurvivorMove targetMove;    // 힐 받을 대상 이동 제어
    [SerializeField] private SurvivorMove healerMove;    // 힐하는 생존자 애니메이션

    private SurvivorInteractor healerInteractor; // 현재 힐 중인 생존자
    private SurvivorState healerState;           // 힐하는 쪽 상태

    private bool isHealing;       // 현재 힐 진행 중인지
    private float progress;       // 현재 힐 진행 시간
    private bool isCompleted;     // 현재 단계 힐 완료 여부

    private void Awake()
    {
        if (targetState == null)
            targetState = GetComponentInParent<SurvivorState>();

        if (targetMove == null && targetState != null)
            targetMove = targetState.GetComponent<SurvivorMove>();

        if (progressUI == null)
            progressUI = FindFirstObjectByType<ProgressUI>();
    }

    // 힐 시작
    public void BeginInteract(GameObject actor)
    {
        if (CanHeal() == false)
            return;

        isHealing = true;

        // 힐 시작할 때 힐하는 사람이 대상 방향을 보게 함
        FaceToTarget();

        // 힐하는 사람 / 힐받는 사람 둘 다 이동 막기
        LockMovement(true);

        // 힐 시작 시 진행도 UI 표시
        progressUI?.Show();
        progressUI?.SetProgress(progress / healTime);

        // 힐 애니메이션 시작
        SetHealAnimation(true);

        Debug.Log($"{name} 힐 시작");
    }

    // 힐 중단
    public void EndInteract()
    {
        if (isHealing == false)
            return;

        isHealing = false;

        // 힐하는 사람 / 힐받는 사람 둘 다 이동 해제
        LockMovement(false);

        // 진행도는 유지하고 UI만 숨김
        progressUI?.Hide();
        progressUI?.SetProgress(progress / healTime);

        // 힐 애니메이션 종료
        SetHealAnimation(false);

        Debug.Log($"{name} 힐 중단");
    }

    private void Update()
    {
        if (isHealing == false)
            return;

        if (CanHeal() == false)
        {
            EndInteract();
            return;
        }

        progress += Time.deltaTime;

        float normalized = progress / healTime;
        progressUI?.SetProgress(normalized);

        if (progress >= healTime)
        {
            CompleteHeal();
        }
    }

    // 힐 완료
    private void CompleteHeal()
    {
        isHealing = false;
        isCompleted = true;
        progress = 0f;

        LockMovement(false);

        progressUI?.SetProgress(1f);
        progressUI?.Hide();

        SetHealAnimation(false);

        if (targetState == null)
            return;

        // 다운 상태면 부상 상태까지 회복
        if (targetState.IsDowned)
        {
            targetState.RecoverToInjured();
            Debug.Log($"{name} : 다운 상태 힐 완료 -> 부상 상태로 회복");
        }
        // 부상 상태면 정상 상태까지 회복
        else if (targetState.IsInjured)
        {
            targetState.HealToHealthy();
            Debug.Log($"{name} : 부상 상태 힐 완료 -> 정상 상태로 회복");
        }

        isCompleted = false;
    }

    // 현재 힐 가능한 대상인지 확인
    private bool CanHeal()
    {
        if (targetState == null)
            return false;

        // Healthy면 힐할 필요 없음
        if (targetState.IsHealthy)
            return false;

        // 힐하는 생존자가 없으면 불가
        if (healerInteractor == null)
            return false;

        // 힐하는 쪽 상태가 없으면 불가
        if (healerState == null)
            return false;

        // 다운된 생존자는 다른 사람을 힐 못 함
        if (healerState.IsDowned)
            return false;

        return true;
    }

    // 힐 애니메이션 on/off
    private void SetHealAnimation(bool value)
    {
        if (healerMove != null)
            healerMove.SetSearching(value);
    }

    // 힐하는 사람이 힐 대상을 바라보게 함
    private void FaceToTarget()
    {
        if (healerMove == null || targetState == null)
            return;

        Vector3 lookDir = targetState.transform.position - healerMove.transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        healerMove.FaceDirection(lookDir.normalized);
    }

    // 힐하는 사람 / 힐받는 사람 둘 다 이동 잠금/해제
    private void LockMovement(bool value)
    {
        if (healerMove != null)
            healerMove.SetMoveLock(value);

        if (targetMove != null)
            targetMove.SetMoveLock(value);
    }

    // 범위 안에 들어오면 상호작용 가능 대상으로 등록
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Survivor") == false)
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor == null)
            return;

        SurvivorState state = interactor.GetComponent<SurvivorState>();
        if (state == null)
            state = interactor.GetComponentInParent<SurvivorState>();

        // 자기 자신 힐 방지
        if (targetState != null && state == targetState)
            return;

        // 이미 다른 생존자가 힐 중이면 새로 등록 안 함
        if (healerInteractor != null && healerInteractor != interactor)
            return;

        healerInteractor = interactor;
        healerState = state;

        healerMove = interactor.GetComponent<SurvivorMove>();
        if (healerMove == null)
            healerMove = interactor.GetComponentInParent<SurvivorMove>();

        if (CanHeal())
        {
            interactor.SetInteractable(this);
            Debug.Log($"{name} 힐 범위 진입");
        }
    }

    // 범위를 벗어나면 상호작용 해제
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Survivor") == false)
            return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor != null)
        {
            interactor.ClearInteractable(this);
        }

        if (healerInteractor == interactor)
        {
            EndInteract();

            healerInteractor = null;
            healerState = null;
            healerMove = null;

            Debug.Log($"{name} 힐 범위 이탈");
        }
    }
}