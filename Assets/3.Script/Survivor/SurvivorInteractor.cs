using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SurvivorInteractor : NetworkBehaviour
{
    private SurvivorInput input;
    private SurvivorState state;

    private IInteractable currentInteractable;
    private bool isInteracting;

    [Header("UI")]
    [SerializeField] private ProgressUI progressUI; // 이 로컬 플레이어가 사용할 진행도 UI

    public bool IsInteracting => isInteracting;

    // 다른 상호작용 스크립트가 ProgressUI를 가져갈 때
    // 혹시 아직 연결이 안 되어 있으면 다시 시도하고 반환
    public ProgressUI ProgressUI
    {
        get
        {
            if (progressUI == null)
                BindUI();

            return progressUI;
        }
    }

    // 현재 내가 잡고 있는 상호작용 대상인지 확인
    public bool IsCurrentInteractable(IInteractable interactable)
    {
        return currentInteractable == interactable;
    }

    private void Awake()
    {
        input = GetComponent<SurvivorInput>();
        state = GetComponent<SurvivorState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 씬이 바뀔 때마다 UI를 다시 연결할 수 있게 등록
        SceneManager.sceneLoaded += OnScene;
    }

    public override void OnStopClient()
    {
        SceneManager.sceneLoaded -= OnScene;
        base.OnStopClient();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 로컬 플레이어 생성 시 1차 연결
        BindUI();
    }

    private void OnScene(Scene scene, LoadSceneMode mode)
    {
        if (!isLocalPlayer)
            return;

        // 씬 전환 후 다시 UI 연결
        BindUI();
    }

    private void Update()
    {
        // 로컬 플레이어만 상호작용 입력 처리
        if (!isLocalPlayer)
            return;

        // 아직 UI를 못 잡았으면 계속 재시도
        if (progressUI == null)
            BindUI();

        // 다운 상태면 상호작용 강제 종료
        if (state != null && state.IsDowned)
        {
            ClearForce();
            return;
        }

        // 상호작용 중이 아닐 때 앉아 있으면 시작 막기
        if (!isInteracting && input != null && input.IsCrouching)
            return;

        HandleInteract();
    }

    // ProgressUI 연결
    private void BindUI()
    {
        // 1순위: Binder에서 가져오기
        if (LobbySceneBinder.Instance != null)
        {
            progressUI = LobbySceneBinder.Instance.GetProgressUI();
        }

        // 2순위: 아직 없으면 현재 씬에서 직접 찾기
        if (progressUI == null)
        {
            progressUI = FindFirstObjectByType<ProgressUI>(FindObjectsInactive.Include);
        }
    }

    // 현재 상호작용 대상 타입에 따라 처리
    private void HandleInteract()
    {
        if (currentInteractable == null)
        {
            isInteracting = false;
            return;
        }

        if (currentInteractable.InteractType == InteractType.Hold)
            HandleHold();
        else
            HandlePress();
    }

    // Hold 타입 처리
    private void HandleHold()
    {
        if (input == null)
            return;

        if (input.IsInteracting1)
        {
            if (!isInteracting && !input.IsCrouching)
            {
                isInteracting = true;
                currentInteractable.BeginInteract();
            }
        }
        else
        {
            if (isInteracting)
            {
                isInteracting = false;
                currentInteractable.EndInteract();
            }
        }
    }

    // Press 타입 처리
    private void HandlePress()
    {
        if (input == null)
            return;

        if (input.IsCrouching)
            return;

        if (input.IsInteracting2)
            currentInteractable.BeginInteract();
    }

    // 상호작용 가능 대상 등록
    public void SetInteractable(IInteractable interactable)
    {
        if (!isLocalPlayer)
            return;

        if (!enabled)
            return;

        if (state != null && state.IsDowned)
            return;

        // 상호작용 등록 전에 UI도 다시 한 번 보정
        if (progressUI == null)
            BindUI();

        currentInteractable = interactable;
    }

    // 상호작용 대상 해제
    public void ClearInteractable(IInteractable interactable)
    {
        if (!isLocalPlayer)
            return;

        if (currentInteractable != interactable)
            return;

        if (isInteracting)
        {
            isInteracting = false;
            currentInteractable.EndInteract();
        }

        currentInteractable = null;
    }

    private void OnDisable()
    {
        ClearForce();
    }

    // 강제 정리
    private void ClearForce()
    {
        if (isInteracting && currentInteractable != null)
        {
            isInteracting = false;
            currentInteractable.EndInteract();
        }

        currentInteractable = null;
    }
}