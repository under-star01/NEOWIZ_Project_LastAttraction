using UnityEngine;
using Unity.Cinemachine;

public class LobbySceneBinder : MonoBehaviour
{
    public static LobbySceneBinder Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private ProgressUI progressUI;
    [SerializeField] private QTEUI qteUI;
    [SerializeField] private CameraSkillUI cameraSkillUI;

    private void Awake()
    {
        Instance = this;
    }

    public ProgressUI GetProgressUI()
    {
        return progressUI;
    }

    public QTEUI GetQTEUI()
    {
        return qteUI;
    }

    public CameraSkillUI GetCameraSkillUI()
    {
        return cameraSkillUI;
    }
}