using UnityEngine;

public class KillerMove : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 5f;
    public float lookSensitivity = 0.2f;
    public float lungeMultiplier = 1.6f;
    public float penaltyMultiplier = 0.4f;

    [Header("Camera")]
    public Transform killerCamera;
    public Transform cameraTarget;
    public float followSpeed = 25f;

    private CharacterController controller;
    private KillerInput input;
    private KillerState state;
    private Animator animator; // 애니메이터 참조 추가
    private float cameraPitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<KillerInput>();
        state = GetComponent<KillerState>();

        // 자식 오브젝트에서 애니메이터를 찾아옵니다.
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (state.CanMove) HandleMovement();
        if (state.CanLook) HandleLook();

        // 매 프레임 애니메이션 파라미터를 업데이트합니다.
        UpdateAnimation();
    }

    private void HandleMovement()
    {
        float speed = moveSpeed;
        if (state.CurrentCondition == KillerCondition.Lunging) speed *= lungeMultiplier;
        else if (state.CurrentCondition == KillerCondition.Recovering) speed *= penaltyMultiplier;

        Vector3 move = transform.right * input.Move.x + transform.forward * input.Move.y;
        controller.Move(move * speed * Time.deltaTime);
    }

    private void HandleLook()
    {
        transform.Rotate(Vector3.up * input.Look.x * lookSensitivity);
        cameraPitch = Mathf.Clamp(cameraPitch - input.Look.y * lookSensitivity, -80f, 80f);
        killerCamera.localRotation = Quaternion.Euler(cameraPitch, 0, 0);

        if (cameraTarget != null)
            killerCamera.position = Vector3.Lerp(killerCamera.position, cameraTarget.position, Time.deltaTime * followSpeed);
    }

    // --- [추가된 부분] ---
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // 입력 값의 크기(0~1)를 Speed 파라미터에 전달합니다.
        // SurvivorMove처럼 부드러운 전환을 위해 dampTime(0.1f)을 줄 수 있습니다.
        float targetSpeed = input.Move.magnitude;
        animator.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
    }
}