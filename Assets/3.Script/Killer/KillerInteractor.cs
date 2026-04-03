using UnityEngine;
using System.Collections;

public class KillerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactRange = 2.0f;
    public LayerMask interactLayer;

    private KillerInput input;
    private KillerState state;
    private Animator animator;

    void Awake()
    {
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // 공격 중이 아닐 때만 상호작용 체크
        if (state.CurrentCondition == KillerCondition.Idle)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, interactRange, interactLayer))
        {
            string tag = hit.collider.tag;

            switch (tag)
            {
                case "Window": StartVault(hit.collider); break;
                case "Pallet": StartBreakPallet(hit.collider); break;
                case "DownedSurvivor": StartPickUp(hit.collider.gameObject); break;
            }
        }
    }

    // ① 창틀 넘기
    private void StartVault(Collider window)
    {
        state.ChangeState(KillerCondition.Vaulting);
        animator.SetTrigger("Vault");
        // 애니메이션 길이에 맞춰 상태 복구 (예시: 1.2초 후 Idle)
        StartCoroutine(ResetStateRoutine(1.2f));
    }

    // ② 판자에 맞음 (외부에서 호출됨)
    public void GetStunned(float duration)
    {
        state.ChangeState(KillerCondition.Stunned);
        animator.SetTrigger("Stunned");
        StartCoroutine(ResetStateRoutine(duration));
    }

    // ③ 내려진 판자 파괴
    private void StartBreakPallet(Collider pallet)
    {
        state.ChangeState(KillerCondition.Breaking);
        animator.SetTrigger("BreakPallet");
        // 파괴 완료 시점 근처에서 오브젝트 제거 로직
        StartCoroutine(ResetStateRoutine(2.0f));
    }

    // ④ 생존자 들기
    private void StartPickUp(GameObject survivor)
    {
        state.ChangeState(KillerCondition.Carrying);
        animator.SetTrigger("PickUp");
        // survivor.transform.SetParent(shoulderSocket); 로직 추가
    }

    private IEnumerator ResetStateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        state.ChangeState(KillerCondition.Idle);
    }
}