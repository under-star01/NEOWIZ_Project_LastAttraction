using System.Collections;
using UnityEngine;

public enum SurvivorCondition
{
    Healthy,   // 정상
    Injured,   // 부상
    Downed     // 쓰러짐
}

public class SurvivorState : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Animator animator;

    [Header("디버그")]
    [SerializeField] private SurvivorCondition debugCondition = SurvivorCondition.Healthy;

    [Header("다운 연출")]
    [SerializeField] private float downHitDuration = 1.2f; // 다운 피격 애니메이션 시간

    private SurvivorMove move;

    private bool isTransitioningToDowned;

    public SurvivorCondition CurrentCondition { get; private set; } = SurvivorCondition.Healthy;

    public bool IsHealthy => CurrentCondition == SurvivorCondition.Healthy;
    public bool IsInjured => CurrentCondition == SurvivorCondition.Injured;
    public bool IsDowned => CurrentCondition == SurvivorCondition.Downed;
    public bool IsBusy => isTransitioningToDowned;

    private void Awake()
    {
        move = GetComponent<SurvivorMove>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        SetCondition(debugCondition);
    }

    private void Update()
    {
        if (CurrentCondition != debugCondition && !isTransitioningToDowned)
        {
            SetCondition(debugCondition);
        }
    }

    public void TakeHit()
    {
        if (isTransitioningToDowned)
            return;

        if (CurrentCondition == SurvivorCondition.Healthy)
        {
            SetCondition(SurvivorCondition.Injured);
            animator.SetTrigger("Hit");
        }
        else if (CurrentCondition == SurvivorCondition.Injured)
        {
            StartCoroutine(DownedRoutine());
        }

        debugCondition = CurrentCondition;
    }

    public void HealToHealthy()
    {
        if (isTransitioningToDowned)
            return;

        SetCondition(SurvivorCondition.Healthy);
        debugCondition = CurrentCondition;
    }

    public void RecoverToInjured()
    {
        if (isTransitioningToDowned)
            return;

        SetCondition(SurvivorCondition.Injured);
        debugCondition = CurrentCondition;
    }

    private IEnumerator DownedRoutine()
    {
        isTransitioningToDowned = true;

        if (move != null)
        {
            move.SetMoveLock(true);
            move.StopAnimation();
        }

        animator.SetTrigger("DownHit");

        yield return new WaitForSeconds(downHitDuration);

        SetCondition(SurvivorCondition.Downed);

        if (move != null)
        {
            move.SetMoveLock(false);
        }

        isTransitioningToDowned = false;
        debugCondition = CurrentCondition;
    }

    private void SetCondition(SurvivorCondition newCondition)
    {
        CurrentCondition = newCondition;
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetInteger("Condition", (int)CurrentCondition);
    }
}