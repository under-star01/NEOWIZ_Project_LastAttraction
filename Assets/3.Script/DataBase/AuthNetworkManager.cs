using kcp2k;
using Mirror;
using UnityEngine;

public class AuthNetworkManager : NetworkManager
{
    public static AuthNetworkManager Instance = null;

    [Header("Auth Server Settings")]
    [SerializeField] private string authAddress = "AWS Public IP";
    [SerializeField] private ushort authPort = 7770;

    [Header("Transport")]
    [SerializeField] private KcpTransport kcpTransport;

    public override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log("[AuthNetworkManager] Awake 완료");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[AuthNetworkManager] 인증 서버 시작");

        if (SQLManager.Instance != null)
        {
            SQLManager.Instance.ServerInitialize();
        }
        else
        {
            Debug.LogWarning("[AuthNetworkManager] SQLManager.Instance가 없습니다.");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("[AuthNetworkManager] 클라이언트 시작");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[AuthNetworkManager] 인증 서버 연결 성공");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[AuthNetworkManager] 인증 서버 연결 종료");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log("[AuthNetworkManager] 클라이언트 중지");
    }

    public void ConnectToAuthServer()
    {
        if (NetworkClient.isConnected || NetworkClient.active)
        {
            Debug.Log("[AuthNetworkManager] 이미 인증 서버 연결 시도 중이거나 연결 상태입니다.");
            return;
        }

        networkAddress = authAddress;

        Debug.Log($"[AuthNetworkManager] 인증 서버 접속 시도: {authAddress}:{authPort}");
        StartClient();
    }

    public void DisconnectFromAuthServer()
    {
        if (!NetworkClient.active)
            return;

        Debug.Log("[AuthNetworkManager] 인증 서버 연결 종료 요청");
        StopClient();
    }
}