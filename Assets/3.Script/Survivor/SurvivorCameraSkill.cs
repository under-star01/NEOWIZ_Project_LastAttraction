using Mirror;
using Unity.Cinemachine;
using UnityEngine;

public class SurvivorCameraSkill : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private SurvivorInput input;
    [SerializeField] private SurvivorMove move;
    [SerializeField] private SurvivorActionState act;

    [Header("스킬 화면")]
    [SerializeField] private Camera skillCamera;
    [SerializeField] private CameraSkillUI skillUI;

    [Header("카메라 모델")]
    [SerializeField] private GameObject localCameraModel;
    [SerializeField] private GameObject worldCameraModel;

    [Header("카메라 위치")]
    [SerializeField] private CinemachineCamera normalCinemachine;
    [SerializeField] private CinemachineCamera skillCinemachine;

    [SyncVar(hook = nameof(OnSkillChanged))]
    private bool isUse;

    public bool IsUse => isUse;

    private bool isLocalReady;
    private Renderer[] worldModelRenderers;

    private void Awake()
    {
        if (input == null)
            input = GetComponent<SurvivorInput>();

        if (move == null)
            move = GetComponent<SurvivorMove>();

        if (act == null)
            act = GetComponent<SurvivorActionState>();

        if (skillCamera != null)
            skillCamera.enabled = false;

        if (localCameraModel != null)
            localCameraModel.SetActive(false);

        if (worldCameraModel != null)
        {
            worldCameraModel.SetActive(false);
            worldModelRenderers = worldCameraModel.GetComponentsInChildren<Renderer>(true);
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        BindUI();
        isLocalReady = true;
        ApplyLocalView(isUse);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer && skillCamera != null)
            skillCamera.enabled = false;

        if (!isLocalPlayer && localCameraModel != null)
            localCameraModel.SetActive(false);
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

        if (worldCameraModel != null)
            worldCameraModel.SetActive(newValue);

        if (isLocalPlayer)
        {
            SetOwnWorldModelVisible(!newValue);
            ApplyLocalView(newValue);
        }
    }

    private void BindUI()
    {
        if (skillUI == null && LobbySceneBinder.Instance != null)
            skillUI = LobbySceneBinder.Instance.GetCameraSkillUI();

        if (skillUI == null)
            skillUI = FindFirstObjectByType<CameraSkillUI>(FindObjectsInactive.Include);
    }

    private void SetOwnWorldModelVisible(bool value)
    {
        if (!isLocalPlayer)
            return;

        if (worldModelRenderers == null)
            return;

        for (int i = 0; i < worldModelRenderers.Length; i++)
        {
            if (worldModelRenderers[i] != null)
                worldModelRenderers[i].enabled = value;
        }
    }

    private void ApplyLocalView(bool value)
    {
        if (!isLocalPlayer)
            return;

        if (!isLocalReady)
        {
            if (skillCamera != null)
                skillCamera.enabled = false;

            if (localCameraModel != null)
                localCameraModel.SetActive(false);

            return;
        }

        BindUI();

        if (skillCamera != null)
            skillCamera.enabled = value;

        if (normalCinemachine != null && skillCinemachine != null)
        {
            if (value)
            {
                normalCinemachine.Priority = 0;
                skillCinemachine.Priority = 30;
            }
            else
            {
                normalCinemachine.Priority = 30;
                skillCinemachine.Priority = 0;
            }
        }

        if (localCameraModel != null)
            localCameraModel.SetActive(value);

        if (skillUI != null)
        {
            if (value)
                skillUI.Show();
            else
                skillUI.Hide();
        }
    }
}