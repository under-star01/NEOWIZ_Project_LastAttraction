using UnityEngine;

public class SurvivorInteractor : MonoBehaviour
{
    private SurvivorInput input;
    private IInteractable currentOB; // 최근에 상호작용한 오브젝트
    private bool isInteracting; // 상호작용중인지

    public bool IsInteracting => isInteracting;

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
        if (currentOB == null)
        {
            isInteracting = false;
            return;
        }

        if (input.IsInteracting)
        {
            if (!isInteracting)
            {
                isInteracting = true;
                currentOB.BeginInteract();
            }
        }
        else
        {
            if (isInteracting)
            {
                isInteracting = false;
                currentOB.EndInteract();
            }
        }
    }

    public void SetInteractable(IInteractable interactable)
    {
        currentOB = interactable;
    }

    public void ClearInteractable(IInteractable interactable)
    {
        if (currentOB != interactable) return;

        if (isInteracting)
        {
            isInteracting = false;
            currentOB.EndInteract();
        }

        currentOB = null;
    }
}