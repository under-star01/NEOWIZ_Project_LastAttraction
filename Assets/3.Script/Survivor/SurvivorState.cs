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
    [SerializeField] private SurvivorInteractor interactor;

    [Header("디버그")]
    [SerializeField] private SurvivorCondition debugCondition = SurvivorCondition.Healthy;

    [Header("다운 연출")]
    [SerializeField] private float downHitDuration = 1.2f; // 다운 피격 애니메이션 시간

    private SurvivorMove move;

    private int normalLayer; // 기본 레이어 번호
    private int downedLayer; // 다운 레이어 번호

    private bool isToDowned; // 다운 피격 연출 중인지
    private SurvivorCondition lastDebugCondition; // 마지막으로 적용한 디버그 상태

    public SurvivorCondition CurrentCondition { get; private set; } = SurvivorCondition.Healthy;

    public bool IsHealthy => CurrentCondition == SurvivorCondition.Healthy;
    public bool IsInjured => CurrentCondition == SurvivorCondition.Injured;
    public bool IsDowned => CurrentCondition == SurvivorCondition.Downed;
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

        // 시작 상태는 디버그 값 기준으로 맞춤
        SetCondition(debugCondition);
        lastDebugCondition = debugCondition;
    }

    private void Update()
    {
        // 다운 전환 중에는 디버그 상태 변경 막기
        if (isToDowned)
            return;

        // 인스펙터에서 debugCondition 값을 바꿨을 때만 자동 적용
        if (debugCondition != lastDebugCondition)
        {
            SetCondition(debugCondition);
            lastDebugCondition = debugCondition;
        }
    }

    // 공격받았을 때 호출
    public void TakeHit()
    {
        // 다운 전환 연출 중이면 무시
        if (isToDowned)
            return;

        // 정상 상태에서 맞으면 부상
        if (CurrentCondition == SurvivorCondition.Healthy)
        {
            SetCondition(SurvivorCondition.Injured);
        }
        // 부상 상태에서 한대 더 맞으면 다운
        else if (CurrentCondition == SurvivorCondition.Injured)
        {
            StartCoroutine(DownedRoutine());
            return;
        }

        // 현재 실제 상태를 디버그 값에도 맞춰줌
        SyncDebugCondition();
    }

    // 완전 회복
    public void HealToHealthy()
    {
        if (isToDowned)
            return;

        SetCondition(SurvivorCondition.Healthy);
        SyncDebugCondition();
    }

    // 다운 상태에서 다시 부상 상태로 복귀
    public void RecoverToInjured()
    {
        if (isToDowned)
            return;

        SetCondition(SurvivorCondition.Injured);
        SyncDebugCondition();
    }

    // 다운 피격 애니메이션 후 Downed 상태 진입
    private IEnumerator DownedRoutine()
    {
        isToDowned = true;

        if (move != null)
        {
            // 다운 피격 모션 동안만 잠깐 이동 막기
            move.SetMoveLock(true);
            move.StopAnimation();
        }

        if (animator != null)
        {
            animator.SetTrigger("DownHit");
        }

        yield return new WaitForSeconds(downHitDuration);

        // 실제 다운 상태로 변경
        SetCondition(SurvivorCondition.Downed);
        SyncDebugCondition();

        if (move != null)
        {
            // 다운 상태에서는 crawl 이동을 해야 하므로 잠금 해제
            move.SetMoveLock(false);
        }

        isToDowned = false;
    }

    // 상태 적용
    private void SetCondition(SurvivorCondition newCondition)
    {
        CurrentCondition = newCondition;

        ApplyInteractionState();
        ApplyLayer();
        UpdateAnimator();
    }

    // 다운 상태면 상호작용 막기
    private void ApplyInteractionState()
    {
        if (interactor != null)
        {
            interactor.enabled = !IsDowned;
        }
    }

    // 상태에 따라 레이어 변경
    private void ApplyLayer()
    {
        int targetLayer = normalLayer;

        if (IsDowned)
        {
            targetLayer = downedLayer;
        }

        // 레이어가 없으면 그냥 넘어감
        if (targetLayer == -1)
        {
            return;
        }

        SetLayerRecursive(transform, targetLayer);
    }

    // 자기 자신 + 자식들 레이어까지 전부 변경
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

    // 현재 실제 상태를 debugCondition에도 반영
    private void SyncDebugCondition()
    {
        debugCondition = CurrentCondition;
        lastDebugCondition = debugCondition;
    }

    // 애니메이터 파라미터 반영
    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetInteger("Condition", (int)CurrentCondition);
    }
}