using UnityEngine;

public class SurvivorInteractor : MonoBehaviour
{
    private SurvivorInput input;
    private SurvivorState state;

    // 현재 상호작용 가능한 대상 1개
    private IInteractable currentInteractable;

    // 현재 Hold 상호작용 진행 중인지
    private bool isInteracting;

    public bool IsInteracting => isInteracting;

    private void Awake()
    {
        input = GetComponent<SurvivorInput>();
        state = GetComponent<SurvivorState>();
    }

    private void Update()
    {
        // 다운 상태에서는 상호작용 차단
        if (state != null && state.IsDowned)
        {
            ForceClear();
            return;
        }

        // 현재 상호작용 중이 아니고, 앉아 있는 상태라면
        // 상호작용 시작 막음
        if (!isInteracting && input.IsCrouching)
        {
            return;
        }

        HandleInteraction();
    }

    // 현재 대상의 타입에 따라 처리 분기
    private void HandleInteraction()
    {
        if (currentInteractable == null)
        {
            isInteracting = false;
            return;
        }

        if (currentInteractable.InteractType == InteractType.Hold)
        {
            HandleHoldInteraction();
        }
        else
        {
            HandlePressInteraction();
        }
    }

    // Hold 타입: 버튼 누르고 있는 동안만 상호작용
    private void HandleHoldInteraction()
    {
        // 좌클릭을 누르고 있는 동안
        if (input.IsInteracting1)
        {
            // 아직 시작 안 했고, 앉은 상태도 아닐 때만 시작
            if (!isInteracting && !input.IsCrouching)
            {
                isInteracting = true;
                currentInteractable.BeginInteract();
            }
        }
        else
        {
            // 좌클릭을 떼면 정상적으로 종료
            if (isInteracting)
            {
                isInteracting = false;
                currentInteractable.EndInteract();
            }
        }
    }

    // Press 타입: 버튼 누른 프레임에 즉시 실행
    private void HandlePressInteraction()
    {
        // 앉은 상태에서는 Press 상호작용도 막음
        if (input.IsCrouching)
            return;

        if (input.IsInteracting2)
        {
            currentInteractable.BeginInteract();
        }
    }

    // 상호작용 가능 대상 등록
    public void SetInteractable(IInteractable interactable)
    {
        // 다운 상태면 아예 등록 안 함
        if (!enabled)
            return;

        if (state != null && state.IsDowned)
            return;

        currentInteractable = interactable;
    }

    // 현재 등록된 대상이 맞을 때만 해제
    public void ClearInteractable(IInteractable interactable)
    {
        if (currentInteractable != interactable)
            return;

        // Hold 중이었다면 정상 종료 처리
        if (isInteracting)
        {
            isInteracting = false;
            currentInteractable.EndInteract();
        }

        currentInteractable = null;
    }

    private void OnDisable()
    {
        ForceClear();
    }

    // 강제 종료 + 대상 비우기
    private void ForceClear()
    {
        if (isInteracting && currentInteractable != null)
        {
            isInteracting = false;
            currentInteractable.EndInteract();
        }

        currentInteractable = null;
    }
}