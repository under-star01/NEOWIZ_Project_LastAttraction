using System.Collections;
using Mirror;
using UnityEngine;

public enum SurvivorCondition
{
    Healthy,
    Injured,
    Downed
}

public class SurvivorState : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private SurvivorInteractor interactor;

    [Header("다운 연출")]
    [SerializeField] private float downHitDuration = 1.2f;

    private SurvivorMove move;

    private int normalLayer;
    private int downedLayer;

    [SyncVar(hook = nameof(OnConditionChanged))]
    private SurvivorCondition currentCondition = SurvivorCondition.Healthy;

    private bool isToDowned;

    public SurvivorCondition CurrentCondition => currentCondition;

    public bool IsHealthy => currentCondition == SurvivorCondition.Healthy;
    public bool IsInjured => currentCondition == SurvivorCondition.Injured;
    public bool IsDowned => currentCondition == SurvivorCondition.Downed;
    public bool IsBusy => isToDowned;

    private void Awake()
    {
        move = GetComponent<SurvivorMove>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (interactor == null)
            interactor = GetComponent<SurvivorInteractor>();

        normalLayer = LayerMask.NameToLayer("Survivor");
        downedLayer = LayerMask.NameToLayer("Downed");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyInteractionState();
        ApplyLayer();
        UpdateAnimator();
    }

    [Server]
    public void TakeHit()
    {
        if (isToDowned)
            return;

        if (currentCondition == SurvivorCondition.Healthy)
        {
            currentCondition = SurvivorCondition.Injured;
        }
        else if (currentCondition == SurvivorCondition.Injured)
        {
            StartCoroutine(DownedRoutine());
        }
    }

    [Server]
    public void HealToHealthy()
    {
        if (isToDowned)
            return;

        currentCondition = SurvivorCondition.Healthy;
    }

    [Server]
    public void RecoverToInjured()
    {
        if (isToDowned)
            return;

        currentCondition = SurvivorCondition.Injured;
    }

    [Server]
    private IEnumerator DownedRoutine()
    {
        isToDowned = true;

        RpcPlayDownHit();

        yield return new WaitForSeconds(downHitDuration);

        currentCondition = SurvivorCondition.Downed;
        isToDowned = false;
    }

    [ClientRpc]
    private void RpcPlayDownHit()
    {
        if (move != null)
        {
            move.SetMoveLock(true);
            move.StopAnimation();
        }

        if (animator != null)
        {
            animator.SetTrigger("DownHit");
        }

        StartCoroutine(LocalUnlockAfterDownHit());
    }

    private IEnumerator LocalUnlockAfterDownHit()
    {
        yield return new WaitForSeconds(downHitDuration);

        if (move != null && IsDowned)
        {
            move.SetMoveLock(false);
        }
    }

    private void OnConditionChanged(SurvivorCondition oldValue, SurvivorCondition newValue)
    {
        ApplyInteractionState();
        ApplyLayer();
        UpdateAnimator();
    }

    private void ApplyInteractionState()
    {
        if (interactor != null)
        {
            interactor.enabled = !IsDowned;
        }
    }

    private void ApplyLayer()
    {
        int targetLayer = normalLayer;

        if (IsDowned)
            targetLayer = downedLayer;

        if (targetLayer == -1)
            return;

        SetLayerRecursive(transform, targetLayer);
    }

    private void SetLayerRecursive(Transform target, int layer)
    {
        if (target.GetComponent<SurvivorHeal>() != null)
            return;

        target.gameObject.layer = layer;

        foreach (Transform child in target)
        {
            SetLayerRecursive(child, layer);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetInteger("Condition", (int)currentCondition);
    }
}