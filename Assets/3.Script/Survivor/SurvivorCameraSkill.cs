using Mirror;
using UnityEngine;

// 우클릭 홀드 카메라 스킬
// - 입력 체크
// - 상태 체크
// - 스킬 on/off 동기화
// - 로컬 UI / 전용 카메라 표시 제어
public class SurvivorCameraSkill : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private SurvivorInput input;
    [SerializeField] private SurvivorMove move;
    [SerializeField] private SurvivorActionState act;

    [Header("스킬 화면")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera skillCamera;
    [SerializeField] private CameraSkillUI skillUI;
    [SerializeField] private GameObject cameraModel;

    [Header("카메라 위치")]
    [SerializeField] private Vector3 normalLocalPosition = new Vector3(0f, 1f, -3f);
    [SerializeField] private Vector3 skillLocalPosition = new Vector3(1f, -1.5f, -2f);
    [SerializeField] private Vector3 normalLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 skillLocalEuler = Vector3.zero;

    [SyncVar(hook = nameof(OnSkillChanged))]
    private bool isUse;

    public bool IsUse => isUse;

    // 로컬 UI 준비 완료 여부
    private bool isLocalReady;
    
    private void Awake()
    {
        if (input == null)
            input = GetComponent<SurvivorInput>();

        if (move == null)
            move = GetComponent<SurvivorMove>();

        if (act == null)
            act = GetComponent<SurvivorActionState>();

        // 카메라 초기 설정
        if (skillCamera != null)
            skillCamera.enabled = false;

        if (cameraModel != null)
            cameraModel.SetActive(false);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        BindUI();
        isLocalReady = true;

        // 로컬 준비가 끝난 뒤 현재 상태 다시 반영
        ApplyLocalView(isUse);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 내 플레이어가 아니면 전용 카메라는 항상 꺼둔다
        if (!isLocalPlayer && skillCamera != null)
            skillCamera.enabled = false;
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        bool want = false;

        if (input != null)
            want = input.IsCameraSkillPressed;

        if (!CanUse())
            want = false;

        if (want != isUse)
            CmdSetSkill(want);
    }

    private bool CanUse()
    {
        if (act == null)
            return false;

        return act.CanCam();
    }

    [Command]
    private void CmdSetSkill(bool value)
    {
        if (act == null)
        {
            value = false;
        }
        else if (value && !act.CanCam())
        {
            value = false;
        }

        isUse = value;
        act.SetCam(value);
    }

    private void OnSkillChanged(bool oldValue, bool newValue)
    {
        if (move != null)
            move.SetCamAnim(newValue);

        // SyncVar hook은 UI 준비 전에도 들어올 수 있으니
        // 로컬 준비가 끝난 뒤에만 안전하게 반영
        if (isLocalPlayer)
            ApplyLocalView(newValue);
    }

    private void BindUI()
    {
        if (skillUI == null && LobbySceneBinder.Instance != null)
            skillUI = LobbySceneBinder.Instance.GetCameraSkillUI();

        if (skillUI == null)
            skillUI = FindFirstObjectByType<CameraSkillUI>(FindObjectsInactive.Include);
    }

    private void ApplyLocalView(bool value)
    {
        if (!isLocalPlayer)
            return;

        // 아직 로컬 준비 전이면 전용 카메라/모델만 꺼두고 종료
        if (!isLocalReady)
        {
            if (skillCamera != null)
                skillCamera.enabled = false;

            if (cameraModel != null)
                cameraModel.SetActive(false);

            return;
        }

        BindUI();

        // 촬영 상태 카메라 위치 조정
        if (skillCamera != null)
            skillCamera.enabled = value;
        
        if (mainCamera != null)
        {
            if (value)
            {
                mainCamera.transform.localPosition = skillLocalPosition;
                mainCamera.transform.localRotation = Quaternion.Euler(skillLocalEuler);
            }
            else
            {
                mainCamera.transform.localPosition = normalLocalPosition;
                mainCamera.transform.localRotation = Quaternion.Euler(normalLocalEuler);
            }
        }

        if (cameraModel != null)
            cameraModel.SetActive(value);

        if (skillUI != null)
        {
            if (value)
                skillUI.Show();
            else
                skillUI.Hide();
        }
    }
}