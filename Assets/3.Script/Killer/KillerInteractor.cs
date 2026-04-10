using UnityEngine;
using Mirror;

public class KillerInteractor : NetworkBehaviour
{
    public float interactRange = 2.0f;
    public LayerMask interactLayer;

    private KillerInput input;
    private KillerState state;
    private IInteractable currentTarget;

    void Awake()
    {
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        SearchTarget();

        if (state.CurrentCondition == KillerCondition.Idle && input.IsInteractPressed)
        {
            if (currentTarget != null)
            {
                GameObject targetObj = ((MonoBehaviour)currentTarget).gameObject;

                // [로컬 반응 추가] 상태에 따른 트리거를 미리 당겨 동기화 속도를 맞춥니다.
                if (targetObj.CompareTag("Pallet")) state.PlayTrigger(KillerCondition.Breaking);
                else if (targetObj.CompareTag("Window")) state.PlayTrigger(KillerCondition.Vaulting);

                CmdInteract(targetObj);
            }
        }
    }

    private void SearchTarget()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, interactRange, interactLayer))
        {
            currentTarget = hit.collider.GetComponent<IInteractable>();
        }
        else currentTarget = null;
    }

    [Command]
    private void CmdInteract(GameObject target)
    {
        if (state.CurrentCondition != KillerCondition.Idle) return;

        IInteractable interactable = target.GetComponent<IInteractable>();
        if (interactable == null) return;

        if (target.CompareTag("Pallet")) state.ChangeState(KillerCondition.Breaking);
        else if (target.CompareTag("Window")) state.ChangeState(KillerCondition.Vaulting);

        interactable.BeginInteract(this.gameObject);
    }

    // Pallet에서 호출하는 스턴 함수
    public void ApplyHitStun(float duration)
    {
        if (!isServer) return;
        if (state.CurrentCondition == KillerCondition.Hit) return;

        state.ChangeState(KillerCondition.Hit);
        StartCoroutine(ResetHitStunRoutine(duration));
    }

    private System.Collections.IEnumerator ResetHitStunRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (state.CurrentCondition == KillerCondition.Hit)
            state.ChangeState(KillerCondition.Idle);
    }
}