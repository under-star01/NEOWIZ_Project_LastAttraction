using UnityEngine;

public class SurvivorInteractor : MonoBehaviour
{
    private SurvivorInput input;
    private IInteractable currentOB;
    private bool isInteracting;

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

        if (currentOB.InteractType == InteractType.Hold)
        {
            HandleHoldInteraction();
        }
        else if (currentOB.InteractType == InteractType.Press)
        {
            HandlePressInteraction();
        }
    }

    private void HandleHoldInteraction()
    {
        if (input.IsInteracting1)
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

    private void HandlePressInteraction()
    {
        if (input.IsInteracting2)
        {
            currentOB.BeginInteract();
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