using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using kcp2k;

public enum JoinRole
{
    None,
    Killer,
    Survivor
}

public struct JoinRequestMessage : NetworkMessage
{
    public int role;
}

public struct JoinDeniedMessage : NetworkMessage
{
    public string reason;
}

public struct JoinAcceptedMessage : NetworkMessage
{
    public int role;
    public ushort port;
}

public struct RoomProbeRequestMessage : NetworkMessage { }

public struct RoomProbeResponseMessage : NetworkMessage
{
    public ushort port;
    public int survivorCount;
    public bool hasKiller;
    public bool isFull;
}

public class CustomNetworkManager : NetworkManager
{
    public static CustomNetworkManager Instance { get; private set; }

    [Header("Port Settings")]
    [SerializeField] private List<ushort> serverPorts = new() { 7777, 7778, 7779, 7780, 7781 };

    [Header("Role Prefabs")]
    [SerializeField] private GameObject killerPrefab;
    [SerializeField] private GameObject survivorPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform killerSpawnPoint;
    [SerializeField] private List<Transform> survivorSpawnPoints = new();

    [Header("Match Settings")]
    [SerializeField] private int maxRoomPlayers = 5;

    private KcpTransport kcpTransport;

    private JoinRole localJoinRole = JoinRole.None;
    private readonly Dictionary<int, JoinRole> joinedRoles = new();
    private readonly List<RoomProbeResponseMessage> probedRooms = new();

    private int currentPortIndex = -1;
    private bool isSearchingServer;
    private bool joinApproved;
    private bool isLeavingManually;
    private bool isJoiningFinalRoom;
    private ushort selectedPort;

    private Coroutine connectRoutine;

    public bool HasKiller
    {
        get
        {
            foreach (var pair in joinedRoles)
            {
                if (pair.Value == JoinRole.Killer)
                    return true;
            }

            return false;
        }
    }

    public bool IsRoomFull => numPlayers >= maxRoomPlayers;
    public bool CanJoinAsKiller => !HasKiller && !IsRoomFull;
    public bool CanJoinAsSurvivor => HasKiller && !IsRoomFull;

    public bool IsSearchingServer => isSearchingServer;
    public bool IsConnectedToServer => NetworkClient.isConnected;
    public JoinRole CurrentLocalJoinRole => localJoinRole;

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        base.Awake();

        kcpTransport = transport as KcpTransport;
        if (kcpTransport == null)
        {
            Debug.LogError("[CustomNetworkManager] KcpTransport를 찾지 못했습니다.");
            return;
        }

        kcpTransport.Port = GetPortFromArgs();
        maxConnections = maxRoomPlayers;
    }

    private void Start()
    {
        if (!Application.isBatchMode)
            return;

        StartServer();
    }

    #region Client API

    public void ConnectAsKiller()
    {
        BeginRoleSearch(JoinRole.Killer);
    }

    public void ConnectAsSurvivor()
    {
        BeginRoleSearch(JoinRole.Survivor);
    }

    public void BackToRoleSelect()
    {
        isLeavingManually = true;
        isSearchingServer = false;
        joinApproved = false;
        isJoiningFinalRoom = false;
        selectedPort = 0;

        LobbySceneBinder.Instance?.ApplyCameraForRole(JoinRole.None);

        if (connectRoutine != null)
        {
            StopCoroutine(connectRoutine);
            connectRoutine = null;
        }

        if (NetworkClient.active || NetworkClient.isConnected)
        {
            StopClient();
            return;
        }

        ResetClientSearchState();
    }

    private void BeginRoleSearch(JoinRole role)
    {
        if (role != JoinRole.Killer && role != JoinRole.Survivor)
        {
            Debug.LogWarning("[CustomNetworkManager] 유효하지 않은 역할입니다.");
            return;
        }

        if (NetworkClient.active || isSearchingServer)
        {
            Debug.LogWarning("[CustomNetworkManager] 이미 접속 중이거나 서버 탐색 중입니다.");
            return;
        }

        if (serverPorts == null || serverPorts.Count == 0)
        {
            Debug.LogError("[CustomNetworkManager] serverPorts가 비어 있습니다.");
            return;
        }

        localJoinRole = role;
        currentPortIndex = -1;
        isSearchingServer = true;
        joinApproved = false;
        isLeavingManually = false;
        isJoiningFinalRoom = false;
        selectedPort = 0;

        probedRooms.Clear();

        ProbeNextPort();
    }

    private void ProbeNextPort()
    {
        currentPortIndex++;

        if (currentPortIndex >= serverPorts.Count)
        {
            SelectBestRoomAndJoin();
            return;
        }

        StartClientDelayed(serverPorts[currentPortIndex]);
    }

    private void SelectBestRoomAndJoin()
    {
        selectedPort = FindBestPort();

        if (selectedPort == 0)
        {
            Debug.LogWarning($"[CustomNetworkManager] {localJoinRole} 입장 가능한 방이 없습니다.");
            ResetClientSearchState();
            return;
        }

        isJoiningFinalRoom = true;
        StartClientDelayed(selectedPort);
    }

    private ushort FindBestPort()
    {
        if (localJoinRole == JoinRole.Killer)
        {
            foreach (var room in probedRooms)
            {
                if (!room.isFull && !room.hasKiller && room.survivorCount > 0)
                    return room.port;
            }

            foreach (var room in probedRooms)
            {
                if (!room.isFull && !room.hasKiller && room.survivorCount == 0)
                    return room.port;
            }

            return 0;
        }

        if (localJoinRole == JoinRole.Survivor)
        {
            foreach (var room in probedRooms)
            {
                if (!room.isFull && room.hasKiller)
                    return room.port;
            }

            return 0;
        }

        return 0;
    }

    private void StartClientDelayed(ushort targetPort)
    {
        if (connectRoutine != null)
        {
            StopCoroutine(connectRoutine);
            connectRoutine = null;
        }

        connectRoutine = StartCoroutine(StartClientNextFrame(targetPort));
    }

    private IEnumerator StartClientNextFrame(ushort targetPort)
    {
        yield return null;
        yield return new WaitForSeconds(0.1f);

        if (isLeavingManually)
        {
            connectRoutine = null;
            yield break;
        }

        if (kcpTransport == null)
        {
            Debug.LogError("[CustomNetworkManager] KcpTransport를 찾지 못했습니다.");
            connectRoutine = null;
            yield break;
        }

        if (NetworkClient.active || NetworkClient.isConnected)
        {
            connectRoutine = null;
            yield break;
        }

        kcpTransport.Port = targetPort;
        StartClient();

        connectRoutine = null;
    }

    private void ResetClientSearchState()
    {
        localJoinRole = JoinRole.None;
        currentPortIndex = -1;
        isSearchingServer = false;
        joinApproved = false;
        isLeavingManually = false;
        isJoiningFinalRoom = false;
        selectedPort = 0;
        probedRooms.Clear();

        if (connectRoutine != null)
        {
            StopCoroutine(connectRoutine);
            connectRoutine = null;
        }
    }

    #endregion

    #region Server Lifecycle

    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<JoinRequestMessage>(OnReceiveJoinRequest, false);
        NetworkServer.RegisterHandler<RoomProbeRequestMessage>(OnReceiveRoomProbeRequest, false);
    }

    public override void OnStopServer()
    {
        joinedRoles.Clear();
        base.OnStopServer();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        if (IsRoomFull)
        {
            conn.Disconnect();
            return;
        }

        base.OnServerConnect(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        joinedRoles.Remove(conn.connectionId);
        base.OnServerDisconnect(conn);
    }

    #endregion

    #region Client Lifecycle

    public override void OnStartClient()
    {
        base.OnStartClient();

        NetworkClient.RegisterHandler<JoinDeniedMessage>(OnJoinDenied, false);
        NetworkClient.RegisterHandler<JoinAcceptedMessage>(OnJoinAccepted, false);
        NetworkClient.RegisterHandler<RoomProbeResponseMessage>(OnRoomProbeResponse, false);
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        if (localJoinRole == JoinRole.None)
        {
            StopClient();
            return;
        }

        if (isJoiningFinalRoom)
        {
            NetworkClient.Send(new JoinRequestMessage
            {
                role = (int)localJoinRole
            });
        }
        else
        {
            NetworkClient.Send(new RoomProbeRequestMessage());
        }
    }

    public override void OnClientDisconnect()
    {
        bool wasProbing = isSearchingServer && !joinApproved && !isLeavingManually && !isJoiningFinalRoom;
        bool finalJoinFailed = isSearchingServer && !joinApproved && !isLeavingManually && isJoiningFinalRoom;

        base.OnClientDisconnect();

        if (wasProbing)
        {
            ProbeNextPort();
            return;
        }

        if (finalJoinFailed)
        {
            Debug.LogWarning("[CustomNetworkManager] 최종 방 입장에 실패했습니다.");
        }

        if (!joinApproved)
            ResetClientSearchState();
    }

    private void OnJoinDenied(JoinDeniedMessage msg)
    {
        Debug.LogWarning($"[CustomNetworkManager] 입장 거부: {msg.reason}");

        if (NetworkClient.active || NetworkClient.isConnected)
            StopClient();
    }

    private void OnJoinAccepted(JoinAcceptedMessage msg)
    {
        joinApproved = true;
        isSearchingServer = false;
        isJoiningFinalRoom = false;
        localJoinRole = (JoinRole)msg.role;

        Debug.Log($"[CustomNetworkManager] 입장 완료 - Role: {localJoinRole}, Port: {msg.port}");
    }

    private void OnRoomProbeResponse(RoomProbeResponseMessage msg)
    {
        probedRooms.Add(msg);

        if (NetworkClient.active || NetworkClient.isConnected)
            StopClient();
    }

    #endregion

    #region Server Request Handlers

    private void OnReceiveRoomProbeRequest(NetworkConnectionToClient conn, RoomProbeRequestMessage msg)
    {
        conn.Send(new RoomProbeResponseMessage
        {
            port = kcpTransport.Port,
            survivorCount = GetCurrentSurvivorCount(),
            hasKiller = HasKiller,
            isFull = IsRoomFull
        });

        StartCoroutine(DisconnectNextFrame(conn));
    }

    private void OnReceiveJoinRequest(NetworkConnectionToClient conn, JoinRequestMessage msg)
    {
        JoinRole requestedRole = (JoinRole)msg.role;

        if (conn.identity != null)
        {
            conn.Send(new JoinDeniedMessage { reason = "이미 플레이어가 생성된 연결입니다." });
            StartCoroutine(DisconnectNextFrame(conn));
            return;
        }

        if (!CanAcceptRole(requestedRole, out string denyReason))
        {
            conn.Send(new JoinDeniedMessage { reason = denyReason });
            StartCoroutine(DisconnectNextFrame(conn));
            return;
        }

        if (!TryCreatePlayer(conn, requestedRole, out string createFailReason))
        {
            conn.Send(new JoinDeniedMessage { reason = createFailReason });
            StartCoroutine(DisconnectNextFrame(conn));
            return;
        }

        joinedRoles[conn.connectionId] = requestedRole;

        conn.Send(new JoinAcceptedMessage
        {
            role = (int)requestedRole,
            port = kcpTransport.Port
        });
    }

    private bool CanAcceptRole(JoinRole role, out string reason)
    {
        reason = string.Empty;

        if (role != JoinRole.Killer && role != JoinRole.Survivor)
        {
            reason = "유효하지 않은 역할 요청입니다.";
            return false;
        }

        if (IsRoomFull)
        {
            reason = "방이 가득 찼습니다.";
            return false;
        }

        if (role == JoinRole.Killer && !CanJoinAsKiller)
        {
            reason = "이미 Killer가 존재하는 방입니다.";
            return false;
        }

        if (role == JoinRole.Survivor && !CanJoinAsSurvivor)
        {
            reason = "아직 Killer가 없는 방에는 Survivor가 입장할 수 없습니다.";
            return false;
        }

        return true;
    }

    private bool TryCreatePlayer(NetworkConnectionToClient conn, JoinRole role, out string reason)
    {
        reason = string.Empty;

        GameObject prefabToSpawn = null;
        Transform spawnPoint = null;

        switch (role)
        {
            case JoinRole.Killer:
                prefabToSpawn = killerPrefab;
                spawnPoint = killerSpawnPoint;
                break;

            case JoinRole.Survivor:
                prefabToSpawn = survivorPrefab;
                spawnPoint = GetSurvivorSpawnPoint(GetCurrentSurvivorCount());
                break;
        }

        if (prefabToSpawn == null)
        {
            reason = $"{role} 프리팹이 설정되지 않았습니다.";
            return false;
        }

        if (spawnPoint == null)
        {
            reason = $"{role} 스폰 포인트가 설정되지 않았습니다.";
            return false;
        }

        GameObject playerObj = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        NetworkServer.AddPlayerForConnection(conn, playerObj);
        return true;
    }

    private IEnumerator DisconnectNextFrame(NetworkConnectionToClient conn)
    {
        yield return null;

        if (conn != null)
            conn.Disconnect();
    }

    #endregion

    #region Utils

    private ushort GetPortFromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-port")
                continue;

            if (ushort.TryParse(args[i + 1], out ushort parsedPort))
                return parsedPort;
        }

        if (serverPorts == null || serverPorts.Count == 0)
            return 7777;

        return serverPorts[0];
    }

    private int GetCurrentSurvivorCount()
    {
        int count = 0;

        foreach (var pair in joinedRoles)
        {
            if (pair.Value == JoinRole.Survivor)
                count++;
        }

        return count;
    }

    private Transform GetSurvivorSpawnPoint(int survivorIndex)
    {
        if (survivorSpawnPoints == null || survivorSpawnPoints.Count == 0)
            return null;

        int index = Mathf.Clamp(survivorIndex, 0, survivorSpawnPoints.Count - 1);
        return survivorSpawnPoints[index];
    }

    #endregion
}