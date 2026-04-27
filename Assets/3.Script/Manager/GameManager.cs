using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("디버그")]
    [SerializeField] private bool survivorInputEnabled;

    public bool SurvivorInputEnabled => survivorInputEnabled;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 로비에서는 기본적으로 생존자 입력을 막는다.
        SetAllSurvivorInput(false);
    }

    // 서버에서 모든 생존자의 입력 가능 여부 변경
    [Server]
    public void SetAllSurvivorInput(bool value)
    {
        survivorInputEnabled = value;

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null)
                continue;

            SurvivorInput input = conn.identity.GetComponent<SurvivorInput>();

            if (input == null)
                continue;

            input.SetInputEnabledServer(value);
        }

        Debug.Log($"[GameManager] 생존자 입력 상태 변경: {value}");
    }

    // 테스트용 토글
    // F3을 누르면 true -> false / false -> true 로
    [Server]
    public void ToggleAllSurvivorInput()
    {
        SetAllSurvivorInput(!survivorInputEnabled);
    }

    // 나중에 실제 게임 시작 버튼에서 호출
    [Server]
    public void StartGame()
    {
        SetAllSurvivorInput(true);
    }

    // 나중에 로비로 돌아갈 때 호출
    [Server]
    public void EnterLobby()
    {
        SetAllSurvivorInput(false);
    }
}