using System.Collections;
using UnityEngine;

public class Pallet : MonoBehaviour, IInteractable
{
    public InteractType InteractType => InteractType.Press;

    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider standingCollider;   // 드롭 전 충돌
    [SerializeField] private Collider droppedCollider;    // 드롭 후 충돌
    [SerializeField] private Collider interactTrigger;    // 상호작용 범위 트리거
    [SerializeField] private Transform leftDropStandPoint;    // 왼쪽에서 내릴 때 서바이버 위치
    [SerializeField] private Transform rightDropStandPoint;   // 오른쪽에서 내릴 때 서바이버 위치

    [Header("시간")]
    [SerializeField] private float dropActionTime = 1f;

    private bool isDropped;
    private bool isDropping;
    private SurvivorInteractor currentInteractor;
    private bool isLeftSide;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (standingCollider != null)
            standingCollider.enabled = true;

        if (droppedCollider != null)
            droppedCollider.enabled = false;
    }

    public void BeginInteract()
    {
        if (isDropped || isDropping)
            return;

        StartCoroutine(DropRoutine());
    }

    public void EndInteract()
    {
        // SPACE 즉발형이라 비워둠
    }

    private IEnumerator DropRoutine()
    {
        isDropping = true;

        SnapSurvivorToDropPoint();
        FaceSurvivorToDropDirection();
        LockSurvivorMovement(true);

        if (animator != null)
            animator.SetTrigger("Drop");

        yield return new WaitForSeconds(dropActionTime);

        Drop();

        LockSurvivorMovement(false);
        isDropping = false;
    }

    private void Drop()
    {
        isDropped = true;

        if (standingCollider != null)
            standingCollider.enabled = false;

        if (droppedCollider != null)
            droppedCollider.enabled = true;

        if (interactTrigger != null)
            interactTrigger.enabled = false;

        Debug.Log($"{name} 판자 드롭");
    }

    private void SnapSurvivorToDropPoint()
    {
        if (currentInteractor == null)
            return;

        Transform targetPoint = GetSideDropPoint();
        if (targetPoint == null)
            return;

        CharacterController controller = currentInteractor.GetComponent<CharacterController>();
        if (controller == null)
            controller = currentInteractor.GetComponentInParent<CharacterController>();

        Transform survivorTransform = currentInteractor.transform;

        if (controller != null)
            controller.enabled = false;

        survivorTransform.position = targetPoint.position;

        if (controller != null)
            controller.enabled = true;
    }

    private Transform GetSideDropPoint()
    {
        if (currentInteractor == null)
            return null;

        Vector3 localPos = transform.InverseTransformPoint(currentInteractor.transform.position);
        isLeftSide = localPos.x < 0f;

        if (isLeftSide)
            return leftDropStandPoint != null ? leftDropStandPoint : rightDropStandPoint;

        return rightDropStandPoint != null ? rightDropStandPoint : leftDropStandPoint;
    }

    private void FaceSurvivorToDropDirection()
    {
        if (currentInteractor == null)
            return;

        Vector3 lookDir = isLeftSide ? transform.right : -transform.right;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude <= 0.001f)
            return;

        SurvivorMove move = currentInteractor.GetComponent<SurvivorMove>();
        if (move == null)
            move = currentInteractor.GetComponentInParent<SurvivorMove>();

        if (move != null)
            move.FaceDirection(lookDir);
        else
            currentInteractor.transform.rotation = Quaternion.LookRotation(lookDir.normalized);
    }

    private void LockSurvivorMovement(bool value)
    {
        if (currentInteractor == null)
            return;

        SurvivorMove move = currentInteractor.GetComponent<SurvivorMove>();
        if (move == null)
            move = currentInteractor.GetComponentInParent<SurvivorMove>();

        if (move != null)
            move.SetMoveLock(value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDropped || isDropping) return;
        if (!other.CompareTag("Survivor")) return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor != null)
        {
            currentInteractor = interactor;
            interactor.SetInteractable(this);
            Debug.Log($"{name} 범위 진입");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Survivor")) return;

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();
        if (interactor == null)
            interactor = other.GetComponentInParent<SurvivorInteractor>();

        if (interactor != null)
        {
            interactor.ClearInteractable(this);

            if (currentInteractor == interactor)
                currentInteractor = null;

            Debug.Log($"{name} 범위 이탈");
        }
    }
}