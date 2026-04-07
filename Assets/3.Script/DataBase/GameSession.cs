using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;

    public bool IsLoggedIn;
    public int AccountId;
    public string LoginId;
    public string Nickname;
    public int Exp;
    public int Level;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 로그인시 기억할 데이터 저장 메소드
    public void SetLoginData(LoginUserData data)
    {
        if (data == null)
            return;

        IsLoggedIn = true;
        AccountId = data.accountId;
        LoginId = data.loginId;
        Nickname = data.nickname;
        Exp = data.exp;
        Level = data.level;
    }

    // 저장 내용 초기화 메소드
    public void Clear()
    {
        IsLoggedIn = false;
        AccountId = 0;
        LoginId = string.Empty;
        Nickname = string.Empty;
        Exp = 0;
        Level = 0;
    }
}