using System.Collections;
using UnityEngine;

public class Pallet : MonoBehaviour, IInteractable
{
    // Space 1번 눌러서 실행되는 상호작용
    public InteractType InteractType => InteractType.Press;

    [Header("참조")]
    [SerializeField] private Animator animator;
    [SerializeField] private Collider standingCollider; // 세워진 판자 콜라이더
    [SerializeField] private Collider droppedCollider;  // 넘어진 판자 콜라이더
    [SerializeField] private Transform leftPoint;       // 판자 왼쪽 포인트
    [SerializeField] private Transform rightPoint;      // 판자 오른쪽 포인트
    [SerializeField] private Vector3 upPoint = new Vector3(0, 0.2f, 0);

    [Header("이동/연출")]
    [SerializeField] private float moveToPointSpeed = 5f; // 포인트까지 걸어가는 속도
    [SerializeField] private float dropActionTime = 0.5f; // 판자 내리는 연출 시간
    [SerializeField] private float vaultSpeed = 4f;      // 반대편으로 넘어가는 시간

    [Header("밀어내기")]
    [SerializeField] private float pushDistance = 1.2f;   // 겹친 대상 밀어내는 거리

    private bool isDropped;   // 판자가 이미 내려갔는지
    private bool isDropping;  // 현재 판자 내리는 중인지
    private bool isVaulting;  // 현재 판자 넘는 중인지
    private bool isLeftSide;  // 플레이어가 현재 왼쪽에 있는지

    private SurvivorInteractor currentInteractor; // 현재 범위 안 플레이어

    private void Awake()
    {
        // Animator 자동 찾기
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // 시작할 때는 세워진 상태
        if (standingCollider != null)
        {
            standingCollider.enabled = true;
        }

        if (droppedCollider != null)
        {
            droppedCollider.enabled = false;
        }
    }

    // Space 눌렀을 때 실행
    public void BeginInteract()
    {
        if (isDropping)
        {
            return;
        }

        if (isVaulting)
        {
            return;
        }

        // 아직 안 내려간 판자는 드롭
        if (isDropped == false)
        {
            StartCoroutine(DropRoutine());
        }
        // 이미 내려간 판자는 넘기
        else
        {
            StartCoroutine(VaultRoutine());
        }
    }

    public void EndInteract()
    {
        // Press 타입은 비움
    }

    // 판자 내리기
    private IEnumerator DropRoutine()
    {
        // 현재 플레이어가 서있는 쪽 포인트 구하기
        Transform sidePoint = GetSidePoint();

        isDropping = true;

        // 이동 막음
        LockMovement(true);

        // 판자 내리는 방향 보게 만들기
        FaceToPallet();

        // CharacterController 켜진 상태에서 위치를 직접 바꾸면 충돌 문제가 날 수 있어서 잠깐 끔
        CharacterController controller = currentInteractor.GetComponent<CharacterController>();
        controller.enabled = false;

        // 걷는 모션 끄고
        StopAnim();

        // 먼저 자기 쪽 포인트로 이동
        yield return MoveToPoint(sidePoint.position, moveToPointSpeed);

        // 드롭 애니메이션 실행
        PlayAnim("Drop");

        // 판자 애니메이션 실행
        animator.SetTrigger("Drop");

        // 연출 시간만큼 대기
        yield return new WaitForSeconds(dropActionTime);

        // 실제 상태 변경
        Drop();

        controller.enabled = true;

        LockMovement(false);
        isDropping = false;
    }

    // 실제 판자 상태를 내려진 상태로 변경
    private void Drop()
    {
        isDropped = true;

        // 세워진 콜라이더 끄기
        standingCollider.enabled = false;

        // 넘어진 콜라이더 켜기
        droppedCollider.enabled = true;

        // 내려진 뒤 겹친 대상 밀어내기
        PushOut();
    }

    // 판자 넘기
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

        // 이동 막음
        LockMovement(true);

        // 넘는 방향 보게 만들기
        FaceToPallet();


        // CharacterController 켜진 상태에서 위치를 직접 바꾸면 충돌 문제가 날 수 있어서 잠깐 끔
        CharacterController controller = currentInteractor.GetComponent<CharacterController>();
        controller.enabled = false;

        Vector3 start = sidePoint.position + upPoint;
        Vector3 arrive = oppositePoint.position + upPoint;

        // 모션 끄고
        StopAnim();

        // 먼저 현재 쪽 포인트로 이동
        yield return MoveToPoint(start, moveToPointSpeed);

        // Vault 트리거 실행
        if (isLeftSide)
        {
            PlayAnim("LeftVault");
        }
        else
        {
            PlayAnim("RightVault");
        }

        // 반대편 포인트로 부드럽게 이동
        yield return MoveToPoint(arrive, vaultSpeed);

        controller.enabled = true;

        LockMovement(false);
        isVaulting = false;
    }

    // 포인트/방향
    // 현재 플레이어가 서 있는 쪽 포인트 구하기
    private Transform GetSidePoint()
    {
        // 플레이어 위치를 판자 기준 로컬좌표로 바꿈
        Vector3 localPos = transform.InverseTransformPoint(currentInteractor.transform.position);

        // x가 0보다 작으면 왼쪽
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
    private void FaceToPallet()
    {
        if (currentInteractor == null)
        {
            return;
        }

        Vector3 lookDir = Vector3.zero;

        // 왼쪽에 있으면 오른쪽 방향 보고
        // 오른쪽에 있으면 왼쪽 방향 봄
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

    // 이동
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

    // 생존자 제어
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

    // 판자 내려간 뒤 겹친 대상 밀어내기
    private void PushOut()
    {
        if (droppedCollider == null)
        {
            return;
        }

        Collider[] hits = Physics.OverlapBox(
            droppedCollider.bounds.center,
            droppedCollider.bounds.extents,
            droppedCollider.transform.rotation
        );

        foreach (Collider hit in hits)
        {
            // 자기 자신 무시
            if (hit == droppedCollider)
            {
                continue;
            }

            // 생존자/살인마만 처리
            if (hit.CompareTag("Survivor") == false && hit.CompareTag("Killer") == false)
            {
                continue;
            }

            PushSingleActor(hit.transform);
        }
    }

    // 대상 1명을 판자 밖으로 밀어냄
    private void PushSingleActor(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        float dot = Vector3.Dot(transform.forward, toTarget);

        Vector3 pushDir = Vector3.zero;

        if (dot >= 0f)
        {
            pushDir = transform.forward;
        }
        else
        {
            pushDir = -transform.forward;
        }

        pushDir.y = 0f;
        pushDir.Normalize();

        CharacterController controller = target.GetComponent<CharacterController>();

        if (controller == null)
        {
            controller = target.GetComponentInParent<CharacterController>();
        }

        if (controller != null)
        {
            controller.enabled = false;
        }

        target.position += pushDir * pushDistance;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    // 트리거
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