using Mirror;
using UnityEngine;

public class SurvivorInteractor : NetworkBehaviour
{
    private SurvivorInput input;
    private SurvivorState state;

    private IInteractable currentInteractable;
    private bool isInteracting;

    public bool IsInteracting => isInteracting;

    private void Awake()
    {
        input = GetComponent<SurvivorInput>();
        state = GetComponent<SurvivorState>();
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        if (state != null && state.IsDowned)
        {
            ForceClear();
            return;
        }

        if (!isInteracting && input != null && input.IsCrouching)
            return;

        HandleInteraction();
    }

    private void HandleInteraction()
    {
        if (currentInteractable == null)
        {
            isInteracting = false;
            return;
        }

        if (currentInteractable.InteractType == InteractType.Hold)
            HandleHoldInteraction();
        else
            HandlePressInteraction();
    }

    private void HandleHoldInteraction()
    {
        if (input == null)
            return;

        if (input.IsInteracting1)
        {
            if (!isInteracting && !input.IsCrouching)
            {
                isInteracting = true;
                currentInteractable.BeginInteract(this.gameObject);
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

    private void HandlePressInteraction()
    {
        if (input == null)
            return;

        if (input.IsCrouching)
            return;

        if (input.IsInteracting2)
        {
            currentInteractable.BeginInteract(this.gameObject);
        }
    }

    public void SetInteractable(IInteractable interactable)
    {
        if (!isLocalPlayer)
            return;

        if (!enabled)
            return;

        if (state != null && state.IsDowned)
            return;

        currentInteractable = interactable;
    }

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
        ForceClear();
    }

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