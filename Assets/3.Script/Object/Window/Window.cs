using System.Collections;
using UnityEngine;

public class Window : MonoBehaviour, IInteractable
{
    // Space 1번 눌러서 실행되는 상호작용
    public InteractType InteractType => InteractType.Press;

    [Header("참조")]
    [SerializeField] private Transform leftPoint;   // 창틀 왼쪽 포인트
    [SerializeField] private Transform rightPoint;  // 창틀 오른쪽 포인트

    [Header("이동/연출")]
    [SerializeField] private float moveToPointSpeed = 50f; // 자기 쪽 포인트까지 이동 속도
    [SerializeField] private float vaultTime = 4f;         // 반대편으로 넘어가는 속도

    private SurvivorInteractor currentInteractor; // 현재 범위 안 플레이어
    private bool isVaulting;                      // 현재 넘는 중인지
    private bool isLeftSide;                      // 플레이어가 현재 왼쪽에 있는지

    public void BeginInteract()
    {
        if (isVaulting)
        {
            return;
        }

        StartCoroutine(VaultRoutine());
    }

    public void EndInteract()
    {
        // Press 타입은 비움
    }

    // 창틀 넘기
    private IEnumerator VaultRoutine()
    {
        // 현재 플레이어가 있는 쪽 포인트
        Transform sidePoint = GetSidePoint();

        // 반대편 포인트
        Transform oppositePoint = null;

        if (isLeftSide)
        {
            oppositePoint = rightPoint;
        }
        else
        {
            oppositePoint = leftPoint;
        }

        isVaulting = true;

        // 이동만 막음
        LockMovement(true);

        // 넘는 방향 보게 만들기
        FaceToWindow();

        // 걷기/뛰기 모션 끄고 Vault 트리거 실행
        StopAnim();
        PlayAnim("Vault");

        // CharacterController 잠깐 끄기
        CharacterController controller = currentInteractor.GetComponent<CharacterController>();
        controller.enabled = false;

        // 먼저 현재 쪽 포인트로 이동
        yield return MoveToPoint(sidePoint.position, moveToPointSpeed);

        // 반대편 포인트로 이동
        yield return MoveToPoint(oppositePoint.position, vaultTime);

        controller.enabled = true;

        LockMovement(false);
        isVaulting = false;
    }

    // 현재 플레이어가 서 있는 쪽 포인트 구하기
    private Transform GetSidePoint()
    {
        Vector3 localPos = transform.InverseTransformPoint(currentInteractor.transform.position);

        if (localPos.x < 0f)
        {
            isLeftSide = true;
            return leftPoint;
        }
        else
        {
            isLeftSide = false;
            return rightPoint;
        }
    }

    // 현재 액션 방향을 바라보게 함
    private void FaceToWindow()
    {
        if (currentInteractor == null)
        {
            return;
        }

        Vector3 lookDir = Vector3.zero;

        if (isLeftSide)
        {
            lookDir = transform.right;
        }
        else
        {
            lookDir = -transform.right;
        }

        lookDir.y = 0f;

        SurvivorMove move = GetCurrentMove();
        if (move == null)
        {
            return;
        }

        if (lookDir.sqrMagnitude <= 0.001f)
        {
            return;
        }

        move.FaceDirection(lookDir.normalized);
    }

    // 일정한 속도로 특정 위치까지 이동
    private IEnumerator MoveToPoint(Vector3 targetPos, float speed)
    {
        if (currentInteractor == null)
        {
            yield break;
        }

        Transform t = currentInteractor.transform;

        while ((t.position - targetPos).sqrMagnitude > 0.0001f)
        {
            t.position = Vector3.MoveTowards(t.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }

        t.position = targetPos;
    }

    // 이동 잠금/해제
    private void LockMovement(bool value)
    {
        SurvivorMove move = GetCurrentMove();

        if (move != null)
        {
            move.SetMoveLock(value);
        }
    }

    // 생존자 애니메이션 Trigger 실행
    private void PlayAnim(string triggerName)
    {
        SurvivorMove move = GetCurrentMove();

        if (move != null)
        {
            move.PlayAnimation(triggerName);
        }
    }

    // 걷기/뛰기 애니메이션 멈춤
    private void StopAnim()
    {
        SurvivorMove move = GetCurrentMove();

        if (move != null)
        {
            move.StopAnimation();
        }
    }

    // 현재 SurvivorMove 찾기
    private SurvivorMove GetCurrentMove()
    {
        SurvivorMove move = currentInteractor.GetComponent<SurvivorMove>();

        if (move == null)
        {
            move = currentInteractor.GetComponentInParent<SurvivorMove>();
        }

        return move;
    }

    // 범위 안에 들어오면 상호작용 등록
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Survivor") == false)
        {
            return;
        }

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();

        currentInteractor = interactor;
        interactor.SetInteractable(this);
    }

    // 범위를 나가면 상호작용 해제
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Survivor") == false)
        {
            return;
        }

        SurvivorInteractor interactor = other.GetComponent<SurvivorInteractor>();

        interactor.ClearInteractable(this);

        if (currentInteractor == interactor)
        {
            currentInteractor = null;
        }
    }
}