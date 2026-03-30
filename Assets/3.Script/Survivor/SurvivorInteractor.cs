using UnityEngine;

[RequireComponent(typeof(SurvivorInput))]
public class SurvivorInteractor : MonoBehaviour
{
    [SerializeField] private Transform interactOrigin;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactLayer;

    private SurvivorInput input;
    private IInteractable currentOB;

    private void Awake()
    {
        input = GetComponent<SurvivorInput>();
    }

    private void Update()
    {
        HandleInteraction();
    }

    private void HandleInteraction()
    {
        IInteractable target = FindInteractable();

        // 누르고 있는 중
        if (input.IsInteracting)
        {
            // 새 대상에 처음 진입
            if (target != null && currentInteractable == null)
            {
                currentInteractable = target;
                currentInteractable.BeginInteract();
            }
            // 대상이 바뀐 경우
            else if (target != currentInteractable)
            {
                if (currentInteractable != null)
                    currentInteractable.EndInteract();

                currentInteractable = target;

                if (currentInteractable != null)
                    currentInteractable.BeginInteract();
            }
        }
        // 버튼을 뗀 경우
        else
        {
            if (currentInteractable != null)
            {
                currentInteractable.EndInteract();
                currentInteractable = null;
            }
        }

        // 누르고 있는데 대상이 사라진 경우도 정리
        if (input.IsInteracting && target == null && currentInteractable != null)
        {
            currentInteractable.EndInteract();
            currentInteractable = null;
        }
    }

    private IInteractable FindInteractable()
    {
        if (interactOrigin == null)
            interactOrigin = transform;

        if (Physics.Raycast(interactOrigin.position, interactOrigin.forward, out RaycastHit hit, interactDistance, interactLayer))
        {
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        return null;
    }
}