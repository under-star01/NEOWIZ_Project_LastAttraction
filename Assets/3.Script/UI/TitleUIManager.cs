using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleUIManager : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private TMP_InputField inputId;
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private TMP_Text logText;
    [SerializeField] private GameObject logObject;

    [Header("Register UI")]
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private TMP_InputField inputNickname;

    [Header("Login UI")]
    [SerializeField] private GameObject loginPanel;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Start()
    {
        ShowLoginUI();
        SetLog(string.Empty, false);

        if (Application.isBatchMode)
        {
            Debug.Log("[TitleUIManager] 배치모드 서버이므로 인증 서버 접속을 시도하지 않습니다.");
            return;
        }

        if (AuthNetworkManager.Instance != null)
        {
            AuthNetworkManager.Instance.ConnectToAuthServer();
        }
        else
        {
            SetLog("인증 네트워크 매니저가 없습니다.", true);
        }
    }

    // UI 전환

    public void ShowLoginUI()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        SetLog(string.Empty, false);
    }

    public void ShowRegisterUI()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        SetLog(string.Empty, false);
    }

    // 버튼 이벤트

    public void OnClickLogin()
    {
        string loginId = inputId.text.Trim();
        string password = inputPassword.text;

        if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(password))
        {
            SetLog("아이디와 비밀번호를 입력해주세요.", true);
            return;
        }

        if (AuthPlayer.Local == null)
        {
            SetLog("인증 서버와 아직 연결되지 않았습니다.", true);
            return;
        }

        SetLog("로그인 요청 중...", true);
        AuthPlayer.Local.RequestLogin(loginId, password);
    }

    public void OnClickOpenRegister()
    {
        ShowRegisterUI();
    }

    public void OnClickBackFromRegister()
    {
        ShowLoginUI();
    }

    public void OnClickCreateAccount()
    {
        string loginId = inputId.text.Trim();
        string password = inputPassword.text;
        string nickname = inputNickname.text.Trim();

        if (string.IsNullOrWhiteSpace(loginId) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(nickname))
        {
            SetLog("아이디, 비밀번호, 닉네임을 모두 입력해주세요.", true);
            return;
        }

        if (AuthPlayer.Local == null)
        {
            SetLog("인증 서버와 아직 연결되지 않았습니다.", true);
            return;
        }

        SetLog("회원가입 요청 중...", true);
        AuthPlayer.Local.RequestRegister(loginId, password, nickname);
    }

    // AuthPlayer 에서 호출

    public void OnRegisterResult(RegisterResult result)
    {
        switch (result)
        {
            case RegisterResult.Success:
                SetLog("회원가입이 완료되었습니다. 로그인해주세요.", true);
                inputNickname.text = string.Empty;
                ShowLoginUI();
                break;

            case RegisterResult.InvalidInput:
                SetLog("입력값을 다시 확인해주세요.", true);
                break;

            case RegisterResult.DuplicateLoginId:
                SetLog("이미 사용 중인 아이디입니다.", true);
                break;

            case RegisterResult.DuplicateNickname:
                SetLog("이미 사용 중인 닉네임입니다.", true);
                break;

            default:
                SetLog("회원가입에 실패했습니다.", true);
                break;
        }
    }

    public void OnLoginResult(LoginResult result)
    {
        switch (result)
        {
            case LoginResult.InvalidInput:
                SetLog("아이디와 비밀번호를 확인해주세요.", true);
                break;

            case LoginResult.UserNotFound:
                SetLog("존재하지 않는 아이디입니다.", true);
                break;

            case LoginResult.WrongPassword:
                SetLog("비밀번호가 올바르지 않습니다.", true);
                break;

            default:
                SetLog("로그인에 실패했습니다.", true);
                break;
        }
    }

    public void OnLoginSuccess(LoginUserData userData)
    {
        SetLog("로그인 성공", true);

        if (GameSession.Instance != null)
        {
            GameSession.Instance.SetLoginData(userData);
        }

        if (AuthNetworkManager.Instance != null)
        {
            AuthNetworkManager.Instance.DisconnectFromAuthServer();
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    // 공통

    private void SetLog(string message, bool show)
    {
        if (logText != null)
            logText.text = message;

        if (logObject != null)
            logObject.SetActive(show);
    }
}