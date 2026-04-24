using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LobbySceneBinder : MonoBehaviour
{
    public static LobbySceneBinder Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private ProgressUI progressUI;
    [SerializeField] private QTEUI qteUI;
    [SerializeField] private CameraSkillUI cameraSkillUI;
    [SerializeField] private Image[] frameUI;

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

    public Image[] GetFrameUI()
    {
        return frameUI;
    }
}