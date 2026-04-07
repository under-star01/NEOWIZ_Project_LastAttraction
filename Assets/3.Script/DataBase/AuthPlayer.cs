using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AuthPlayer : NetworkBehaviour
{
    public static AuthPlayer Local;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;

        Debug.Log("[AuthPlayer] Local 플레이어 등록 완료");
    }

    // 회원가입 요청 메소드
    public void RequestRegister(string loginId, string password, string nickname)
    {
        if (!isLocalPlayer)
            return;

        Debug.Log($"[AuthPlayer] 회원가입 요청: {loginId} / {nickname}");
        CmdRegister(loginId, password, nickname);
    }

    // 로그인 요청 메소드
    public void RequestLogin(string loginId, string password)
    {
        if (!isLocalPlayer)
            return;

        Debug.Log($"[AuthPlayer] 로그인 요청: {loginId}");
        CmdLogin(loginId, password);
    }

    // 회원가입 실행 메소드
    [Command]
    private void CmdRegister(string loginId, string password, string nickname)
    {
        Debug.Log($"[AuthPlayer] CmdRegister 도착: {loginId} / {nickname}");

        if (SQLManager.Instance == null)
        {
            TargetRegisterResult(connectionToClient, RegisterResult.Failed);
            return;
        }

        // DB에 플레이어 회원가입 정보 추가
        RegisterResult result = SQLManager.Instance.Register(loginId, password, nickname);
        TargetRegisterResult(connectionToClient, result);
    }

    // 로그인 실행 메소드
    [Command]
    private void CmdLogin(string loginId, string password)
    {
        Debug.Log($"[AuthPlayer] CmdLogin 도착: {loginId}");

        if (SQLManager.Instance == null)
        {
            TargetLoginResult(connectionToClient, LoginResult.Failed, 0, "", "", 0, 0);
            return;
        }

        LoginResult result = SQLManager.Instance.Login(loginId, password, out LoginUserData userData);

        if (result == LoginResult.Success && userData != null)
        {
            TargetLoginResult(
                connectionToClient,
                result,
                userData.accountId,
                userData.loginId,
                userData.nickname,
                userData.exp,
                userData.level);
        }
        else
        {
            TargetLoginResult(connectionToClient, result, 0, "", "", 0, 0);
        }
    }

    // 서버 -> 회원가입 결과를 클라에게 반환 메소드
    [TargetRpc]
    private void TargetRegisterResult(NetworkConnection target, RegisterResult result)
    {
        Debug.Log($"[AuthPlayer] 회원가입 결과 수신: {result}");

        TitleUIManager ui = FindFirstObjectByType<TitleUIManager>();
        if (ui == null)
            return;

        ui.OnRegisterResult(result);
    }

    // 서버 -> 로그인 결과를 클라에게 반환 메소드
    [TargetRpc]
    private void TargetLoginResult(
    NetworkConnection target,
    LoginResult result,
    int accountId,
    string loginId,
    string nickname,
    int exp,
    int level)
    {
        Debug.Log($"[AuthPlayer] 로그인 결과 수신: {result}");

        TitleUIManager ui = FindFirstObjectByType<TitleUIManager>();
        if (ui == null)
            return;

        if (result == LoginResult.Success)
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.IsLoggedIn = true;
                GameSession.Instance.AccountId = accountId;
                GameSession.Instance.LoginId = loginId;
                GameSession.Instance.Nickname = nickname;
                GameSession.Instance.Exp = exp;
                GameSession.Instance.Level = level;
            }

            LoginUserData userData = new LoginUserData
            {
                accountId = accountId,
                loginId = loginId,
                nickname = nickname,
                exp = exp,
                level = level
            };

            ui.OnLoginSuccess(userData);
        }
        else
        {
            ui.OnLoginResult(result);
        }
    }
}