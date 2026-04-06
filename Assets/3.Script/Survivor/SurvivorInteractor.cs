using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SurvivorInteractor : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private ProgressUI progressUI; // 이 로컬 플레이어가 사용할 진행도 UI

    private SurvivorInput input;
    private SurvivorState state;

    // 현재 선택된 상호작용 대상
    // 범위 안 후보들 중에서 우선순위로 고른 대상
    private IInteractable currentInteractable;

    // 현재 실제로 진행 중인 상호작용 대상
    private IInteractable activeInteractable;

    // 현재 Hold 타입 상호작용 중인지
    private bool isInteracting;

    // 현재 ProgressUI를 사용 중인 오브젝트
    private object progressOwner;

    // 범위 안에 들어온 상호작용 대상 목록
    private readonly List<IInteractable> nearbyInteractables = new List<IInteractable>();

    public bool IsInteracting => isInteracting;

    public ProgressUI ProgressUI
    {
        get
        {
            if (progressUI == null)
                BindUI();

            return progressUI;
        }
    }

    // 현재 내가 선택 중인 대상인지 확인할 때 사용
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

        // 씬 바뀌면 UI 다시 찾기
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnStopClient()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnStopClient();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        BindUI();
        ForceHideProgress();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isLocalPlayer)
            return;

        BindUI();
        ForceHideProgress();
    }

    private void Update()
    {
        // 로컬 플레이어만 상호작용 입력 처리
        if (!isLocalPlayer)
            return;

        if (progressUI == null)
            BindUI();

        // 다운 상태 / 다운 피격 연출 중이면 강제 종료
        if (state != null && (state.IsDowned || state.IsBusy))
        {
            ClearForce();
            return;
        }

        // 상호작용 중이 아닐 때 앉아 있으면 시작 막기
        if (!isInteracting && input != null && input.IsCrouching)
            return;

        // 범위 안 대상들 중 현재 선택할 대상 갱신
        RefreshCurrentInteractable();

        // 현재 대상 타입에 맞게 입력 처리
        HandleInteract();
    }

    // ProgressUI 연결
    private void BindUI()
    {
        // Binder에서 먼저 찾기
        if (LobbySceneBinder.Instance != null)
            progressUI = LobbySceneBinder.Instance.GetProgressUI();

        // 없으면 씬에서 직접 찾기
        if (progressUI == null)
            progressUI = FindFirstObjectByType<ProgressUI>(FindObjectsInactive.Include);
    }

    // 진행도 UI 표시
    public void ShowProgress(object owner, float value)
    {
        if (!isLocalPlayer)
            return;

        if (progressUI == null)
            BindUI();

        if (progressUI == null)
            return;

        // 이미 다른 owner가 UI를 쓰고 있으면 무시
        if (progressOwner != null && progressOwner != owner)
            return;

        progressOwner = owner;

        progressUI.Show();
        progressUI.SetProgress(value);
    }

    // 진행도 UI 숨김
    public void HideProgress(object owner, bool reset)
    {
        if (!isLocalPlayer)
            return;

        if (progressUI == null)
            return;

        // 지금 UI를 쓰고 있는 owner만 숨길 수 있음
        if (progressOwner != owner)
            return;

        progressOwner = null;

        progressUI.Hide();
    }

    // UI 강제 정리
    public void ForceHideProgress()
    {
        progressOwner = null;

        if (progressUI != null)
            progressUI.Hide();
    }

    // 범위 안 목록에서 현재 선택할 상호작용 대상 결정
    private void RefreshCurrentInteractable()
    {
        // 이미 Hold 상호작용 진행 중이면
        // 중간에 더 높은 우선순위 대상이 들어와도 바꾸지 않음
        if (isInteracting && activeInteractable != null)
        {
            currentInteractable = activeInteractable;
            return;
        }

        IInteractable best = null;
        int bestPriority = int.MinValue;
        float bestDistance = float.MaxValue;

        for (int i = nearbyInteractables.Count - 1; i >= 0; i--)
        {
            IInteractable interactable = nearbyInteractables[i];

            // 이미 사라진 참조 정리
            if (interactable == null)
            {
                nearbyInteractables.RemoveAt(i);
                continue;
            }

            MonoBehaviour behaviour = interactable as MonoBehaviour;

            // MonoBehaviour가 아니거나 비활성화되었으면 목록에서 제거
            if (behaviour == null || !behaviour.isActiveAndEnabled)
            {
                nearbyInteractables.RemoveAt(i);
                continue;
            }

            int priority = GetPriority(interactable);
            float sqrDistance = (behaviour.transform.position - transform.position).sqrMagnitude;

            // 첫 후보면 바로 채택
            if (best == null)
            {
                best = interactable;
                bestPriority = priority;
                bestDistance = sqrDistance;
                continue;
            }

            // 우선순위가 더 높으면 교체
            if (priority > bestPriority)
            {
                best = interactable;
                bestPriority = priority;
                bestDistance = sqrDistance;
                continue;
            }

            // 우선순위 같으면 더 가까운 쪽 선택
            if (priority == bestPriority && sqrDistance < bestDistance)
            {
                best = interactable;
                bestPriority = priority;
                bestDistance = sqrDistance;
            }
        }

        currentInteractable = best;
    }

    // 상호작용 우선순위
    // 숫자가 클수록 먼저 선택됨
    private int GetPriority(IInteractable interactable)
    {
        // 힐이 가장 우선
        if (interactable is SurvivorHeal)
            return 300;

        // 증거 조사
        if (interactable is EvidencePoint)
            return 200;

        // 판자 / 창틀
        if (interactable is Pallet)
            return 110;

        if (interactable is Window)
            return 100;

        return 0;
    }

    // 현재 대상 타입에 따라 처리
    private void HandleInteract()
    {
        if (currentInteractable == null)
        {
            // 대상이 없는데 상호작용 중이라면 정리
            if (isInteracting)
            {
                isInteracting = false;
                SetInteractionState(false);

                if (activeInteractable != null)
                    activeInteractable.EndInteract();

                activeInteractable = null;
            }

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

        // 다운 상태 / 다운 연출 중이면 Hold 시작 금지
        if (state != null && (state.IsDowned || state.IsBusy))
            return;

        // 누르고 있는 동안
        if (input.IsInteracting1)
        {
            // 아직 시작 안 했으면 시작
            if (!isInteracting && !input.IsCrouching)
            {
                if (currentInteractable == null)
                    return;

                isInteracting = true;

                // 시작 시점의 대상을 고정
                // 이후 다른 대상이 들어와도 이 대상 기준으로 끝날 때까지 유지
                activeInteractable = currentInteractable;

                SetInteractionState(true);
                activeInteractable.BeginInteract(this.gameObject);
            }
        }
        else
        {
            // 버튼을 떼면 진행 중이던 activeInteractable 종료
            if (isInteracting)
            {
                isInteracting = false;
                SetInteractionState(false);

                if (activeInteractable != null)
                    activeInteractable.EndInteract();

                activeInteractable = null;
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

        if (state != null && (state.IsDowned || state.IsBusy))
            return;

        if (input.IsInteracting2)
            currentInteractable.BeginInteract(this.gameObject);
    }

    // 상호작용 가능 대상 등록
    public void SetInteractable(IInteractable interactable)
    {
        if (!isLocalPlayer)
            return;

        if (!enabled)
            return;

        if (state != null && (state.IsDowned || state.IsBusy))
            return;

        if (interactable == null)
            return;

        // 중복 등록 방지
        if (!nearbyInteractables.Contains(interactable))
            nearbyInteractables.Add(interactable);
    }

    // 상호작용 대상 해제
    public void ClearInteractable(IInteractable interactable)
    {
        if (!isLocalPlayer)
            return;

        if (interactable == null)
            return;

        nearbyInteractables.Remove(interactable);

        // 현재 진행 중인 대상이 사라진 경우만 강제 종료
        if (activeInteractable == interactable)
        {
            if (isInteracting)
            {
                isInteracting = false;
                SetInteractionState(false);
                activeInteractable.EndInteract();
            }

            activeInteractable = null;
        }

        if (currentInteractable == interactable)
            currentInteractable = null;
    }

    private void OnDisable()
    {
        ClearForce();
    }

    // 강제 정리
    private void ClearForce()
    {
        if (isInteracting && activeInteractable != null)
        {
            isInteracting = false;
            SetInteractionState(false);
            activeInteractable.EndInteract();
        }

        activeInteractable = null;
        currentInteractable = null;
        nearbyInteractables.Clear();
        ForceHideProgress();
    }

    // 현재 플레이어가 상호작용 중인지 서버 SurvivorState에 전달
    private void SetInteractionState(bool value)
    {
        if (state == null)
            return;

        if (isServer)
        {
            state.SetDoingInteractionServer(value);
        }
        else if (isLocalPlayer)
        {
            CmdSetInteractionState(value);
        }
    }

    [Command]
    private void CmdSetInteractionState(bool value)
    {
        if (state == null)
            return;

        state.SetDoingInteractionServer(value);
    }
}