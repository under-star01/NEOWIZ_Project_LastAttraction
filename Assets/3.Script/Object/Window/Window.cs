using System.Collections;
using UnityEngine;

public class Window : MonoBehaviour, IInteractable
{
    public InteractType InteractType => InteractType.Press;

    [Header("참조")]
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform rightPoint;
    [SerializeField] private Vector3 upPoint = new Vector3(0, 0.5f, 0);

    [Header("이동/연출 설정")]
    [SerializeField] private float moveToPointSpeed = 5f;
    [SerializeField] private float survivorVaultSpeed = 4f;
    [SerializeField] private float killerVaultSpeed = 2.5f;
    [SerializeField] private float occupationRadius = 1.0f;

    private SurvivorInteractor currentInteractor; // MonoBehaviour에서 구체적인 타입으로 변경 추천
    private bool isVaulting;
    private bool isLeftSide;

    public void BeginInteract(GameObject actor)
    {
        if (isVaulting) return;

        Transform myPoint = GetSidePointForActor(actor.transform);
        string opponentTag = actor.CompareTag("Survivor") ? "Killer" : "Survivor";

        if (IsOpponentAtPoint(myPoint, opponentTag))
        {
            Debug.Log("상대방이 반대편에 있어 넘을 수 없습니다.");
            return;
        }

        if (actor.CompareTag("Survivor"))
        {
            StartCoroutine(SurvivorVaultRoutine());
        }
        else if (actor.CompareTag("Killer"))
        {
            StartCoroutine(KillerVaultRoutine(actor));
        }
    }

    public void EndInteract() { }

    // --- [루틴: 생존자 넘기] ---
    private IEnumerator SurvivorVaultRoutine()
    {
        if (currentInteractor == null) yield break;

        Transform sidePoint = GetSidePoint();
        Transform oppositePoint = isLeftSide ? rightPoint : leftPoint;

        LockMovement(true);
        FaceToWindow();

        CharacterController controller = currentInteractor.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Vector3 start = sidePoint.position + upPoint;
        Vector3 arrive = oppositePoint.position + upPoint;

        StopAnim();
        yield return MoveToPoint(start, moveToPointSpeed);

        SurvivorMove move = GetCurrentMove();
        if (move != null) move.SetVaulting(true);

        isVaulting = true;
        PlayAnim("LeftVault"); // 애니메이션 이름 확인 필요

        yield return MoveToPoint(arrive, survivorVaultSpeed);

        if (controller != null) controller.enabled = true;
        LockMovement(false);
        isVaulting = false;
        if (move != null) move.SetVaulting(false);
    }

    // --- [루틴: 살인마 넘기] ---
    private IEnumerator KillerVaultRoutine(GameObject killer)
    {
        isVaulting = true;

        KillerState kState = killer.GetComponent<KillerState>();
        CharacterController kController = killer.GetComponent<CharacterController>();
        Animator kAnimator = killer.GetComponentInChildren<Animator>();

        kState.ChangeState(KillerCondition.Vaulting);
        if (kController != null) kController.enabled = false;
        yield return null;

        // [중요] 여기서 sidePoint가 null이 아니어야 에러가 안 납니다!
        Transform sidePoint = GetSidePointForActor(killer.transform);
        Transform oppositePoint = (sidePoint == leftPoint) ? rightPoint : leftPoint;

        yield return MoveActorToPoint(killer.transform, sidePoint.position, moveToPointSpeed);
        FaceActorToPallet(killer.transform, sidePoint == leftPoint);

        if (kAnimator != null) kAnimator.SetTrigger("Vault");

        yield return MoveActorToPoint(killer.transform, oppositePoint.position, killerVaultSpeed);

        if (kController != null) kController.enabled = true;
        kState.ChangeState(KillerCondition.Idle);
        isVaulting = false;
    }

    // --- [도움 함수들: 실제 구현부] ---

    private Transform GetSidePointForActor(Transform actor)
    {
        Vector3 localPos = transform.InverseTransformPoint(actor.position);
        isLeftSide = localPos.x < 0f;
        return isLeftSide ? leftPoint : rightPoint;
    }

    private void FaceActorToPallet(Transform actor, bool isLeft)
    {
        Vector3 lookDir = isLeft ? transform.right : -transform.right;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            actor.rotation = Quaternion.LookRotation(lookDir.normalized);
        }
    }

    private IEnumerator MoveActorToPoint(Transform actor, Vector3 target, float speed)
    {
        while (Vector3.Distance(actor.position, target) > 0.001f)
        {
            actor.position = Vector3.MoveTowards(actor.position, target, speed * Time.deltaTime);
            yield return null;
        }
        actor.position = target;
    }

    private bool IsOpponentAtPoint(Transform targetPoint, string opponentTag)
    {
        if (targetPoint == null) return false;
        Collider[] hits = Physics.OverlapSphere(targetPoint.position, occupationRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(opponentTag)) return true;
        }
        return false;
    }

    // --- [나머지 유틸리티 함수들] ---

    private Transform GetSidePoint()
    {
        Vector3 localPos = transform.InverseTransformPoint(currentInteractor.transform.position);
        isLeftSide = localPos.x < 0f;
        return isLeftSide ? leftPoint : rightPoint;
    }

    private void FaceToWindow()
    {
        if (currentInteractor == null) return;
        Vector3 lookDir = isLeftSide ? transform.right : -transform.right;
        lookDir.y = 0f;
        SurvivorMove move = GetCurrentMove();
        if (move != null) move.FaceDirection(lookDir.normalized);
    }

    private IEnumerator MoveToPoint(Vector3 targetPos, float speed)
    {
        if (currentInteractor == null) yield break;
        Transform t = currentInteractor.transform;
        while ((t.position - targetPos).sqrMagnitude > 0.0001f)
        {
            t.position = Vector3.MoveTowards(t.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        t.position = targetPos;
    }

    private void LockMovement(bool value)
    {
        SurvivorMove move = GetCurrentMove();
        if (move != null) move.SetMoveLock(value);
    }

    private void PlayAnim(string triggerName)
    {
        SurvivorMove move = GetCurrentMove();
        if (move != null) move.PlayAnimation(triggerName);
    }

    private void StopAnim()
    {
        SurvivorMove move = GetCurrentMove();
        if (move != null) move.StopAnimation();
    }

    private SurvivorMove GetCurrentMove()
    {
        if (currentInteractor == null) return null;
        return currentInteractor.GetComponent<SurvivorMove>() ?? currentInteractor.GetComponentInParent<SurvivorMove>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Survivor"))
        {
            SurvivorInteractor interactor = other.GetComponentInParent<SurvivorInteractor>();
            if (interactor != null)
            {
                currentInteractor = interactor;
                interactor.SetInteractable(this);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Survivor"))
        {
            SurvivorInteractor interactor = other.GetComponentInParent<SurvivorInteractor>();
            if (interactor != null && currentInteractor == interactor)
            {
                interactor.ClearInteractable(this);
                currentInteractor = null;
            }
        }
    }
}