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
    [SerializeField] private Camera skillCamera;
    [SerializeField] private CameraSkillUI skillUI;

    [SyncVar(hook = nameof(OnSkillChanged))]
    private bool isUse;

    public bool IsUse => isUse;

    private void Awake()
    {
        if (input == null)
            input = GetComponent<SurvivorInput>();

        if (move == null)
            move = GetComponent<SurvivorMove>();

        if (act == null)
            act = GetComponent<SurvivorActionState>();

        SetLocalView(false);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (skillUI == null && LobbySceneBinder.Instance != null)
            skillUI = LobbySceneBinder.Instance.GetCameraSkillUI();

        if (skillUI == null)
            skillUI = FindFirstObjectByType<CameraSkillUI>(FindObjectsInactive.Include);

        SetLocalView(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer)
            SetLocalView(false);
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

        SetLocalView(newValue);
    }

    private void SetLocalView(bool value)
    {
        if (!isLocalPlayer)
        {
            if (skillCamera != null)
                skillCamera.enabled = false;

            return;
        }

        if (skillCamera != null)
            skillCamera.enabled = value;

        if (skillUI != null)
        {
            if (value)
                skillUI.Show();
            else
                skillUI.Hide();
        }
    }
}