using System;
using System.Security.Cryptography;
using Mirror;
using MySql.Data.MySqlClient;
using UnityEngine;

// 회원가입 결과
public enum RegisterResult
{
    Success,
    InvalidInput,
    DuplicateLoginId,
    DuplicateNickname,
    Failed
}

// 로그인 결과
public enum LoginResult
{
    Success,
    InvalidInput,
    UserNotFound,
    WrongPassword,
    Failed
}

// 로그인 정보
[Serializable]
public class LoginUserData
{
    public int accountId;
    public string loginId;
    public string nickname;
    public int exp;
    public int level;
}

public class SQLManager : MonoBehaviour
{
    public static SQLManager Instance;

    [Header("DB Settings")]
    [SerializeField] private string server = "127.0.0.1";
    [SerializeField] private int port = 3306;
    [SerializeField] private string database = "last_attraction_db";
    [SerializeField] private string user = "understar";
    [SerializeField] private string password = "";

    [Header("Debug")]
    [SerializeField] private bool testConnectionOnStart = true;

    private string connectionString;
    private bool isInitialized = false;

    private void Awake()
    {
        Debug.Log("[SQLManager] Awake 호출");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Debug.Log("[SQLManager] Start 호출");
    }

    public void ServerInitialize()
    {
        if (isInitialized)
        {
            Debug.Log("[SQLManager] 이미 초기화되어 있습니다.");
            return;
        }

        connectionString =
            $"Server={server};Port={port};Database={database};User ID={user};Password={password};";

        isInitialized = true;

        Debug.Log("[SQLManager] DB 초기화 완료");

        if (testConnectionOnStart)
        {
            TestConnection();
        }
    }

    // 서버 준비 상태 반환 메소드
    private bool IsServerReady()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[SQLManager] 서버 상태가 아니므로 DB 작업을 수행할 수 없습니다.");
            return false;
        }

        if (!isInitialized)
        {
            Debug.LogWarning("[SQLManager] DB가 아직 초기화되지 않았습니다.");
            return false;
        }

        return true;
    }

    // 서버 연결 상태 디버그 표시 메소드
    public void TestConnection()
    {
        if (!IsServerReady())
            return;

        try
        {
            // using : 블록처리된 객체를 잠시 만들고, 블록이 끝나면 정리함.
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                Debug.Log("[SQLManager] DB 연결 성공");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLManager] DB 연결 실패: {e}");
        }
    }

    // 회원가입 메소드
    public RegisterResult Register(string loginId, string rawPassword, string nickname)
    {
        // 서버 준비 상태x -> 실패
        if (!IsServerReady())
            return RegisterResult.Failed;

        // 입력값 문제o -> 실패
        if (string.IsNullOrWhiteSpace(loginId) ||
            string.IsNullOrWhiteSpace(rawPassword) ||
            string.IsNullOrWhiteSpace(nickname))
        {
            return RegisterResult.InvalidInput;
        }

        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                // DB 연결
                connection.Open();

                // 아이디 중복 검사
                if (IsLoginIdExists(connection, loginId))
                    return RegisterResult.DuplicateLoginId;
                
                // 닉네임 중복 검사
                if (IsNicknameExists(connection, nickname))
                    return RegisterResult.DuplicateNickname;

                // 비밀번호 해시 형태로 변경 (암호화)
                string passwordHash = HashPassword(rawPassword);

                // 새 유저 추가 Query문
                const string query = @"
                    INSERT INTO users (login_id, password_hash, nickname)
                    VALUES (@loginId, @passwordHash, @nickname);
                ";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    // 파라미터 값 추가
                    cmd.Parameters.AddWithValue("@loginId", loginId);
                    cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@nickname", nickname);

                    // Query 문 실행
                    int result = cmd.ExecuteNonQuery(); // 1이상 -> 성공, 0 -> 실패
                    return result > 0 ? RegisterResult.Success : RegisterResult.Failed;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLManager] 회원가입 실패: {e}");
            return RegisterResult.Failed;
        }
    }

    // 로그인 메소드
    public LoginResult Login(string loginId, string rawPassword, out LoginUserData userData)
    {
        userData = null;

        // 서버 준비 및 입력 검사
        if (!IsServerReady())
            return LoginResult.Failed;

        if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(rawPassword))
            return LoginResult.InvalidInput;

        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                // DB 연결
                connection.Open();

                // 입력 내용 검색 Query문 작성
                const string query = @"
                    SELECT id, login_id, nickname, exp, level, password_hash
                    FROM users
                    WHERE login_id = @loginId
                    LIMIT 1;
                ";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    // 파라미터 값 추가
                    cmd.Parameters.AddWithValue("@loginId", loginId);

                    // Query문 실행 후 Select 결과 읽기
                    using (var reader = cmd.ExecuteReader())
                    {
                        // 결과x -> 실패
                        if (!reader.Read())
                            return LoginResult.UserNotFound;

                        // DB 저장값 저장
                        string savedHash = reader.GetString("password_hash");

                        bool isValid = VerifyPassword(rawPassword, savedHash);
                        if (!isValid)
                            return LoginResult.WrongPassword;

                        userData = new LoginUserData
                        {
                            accountId = reader.GetInt32("id"),
                            loginId = reader.GetString("login_id"),
                            nickname = reader.GetString("nickname"),
                            exp = reader.GetInt32("exp"),
                            level = reader.GetInt32("level")
                        };

                        return LoginResult.Success;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLManager] 로그인 실패: {e}");
            return LoginResult.Failed;
        }
    }

    private bool IsLoginIdExists(MySqlConnection connection, string loginId)
    {
        const string query = @"
            SELECT COUNT(*)
            FROM users
            WHERE login_id = @loginId;
        ";

        using (var cmd = new MySqlCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@loginId", loginId);

            object result = cmd.ExecuteScalar();
            long count = Convert.ToInt64(result);

            return count > 0;
        }
    }

    private bool IsNicknameExists(MySqlConnection connection, string nickname)
    {
        const string query = @"
            SELECT COUNT(*)
            FROM users
            WHERE nickname = @nickname;
        ";

        using (var cmd = new MySqlCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@nickname", nickname);

            object result = cmd.ExecuteScalar();
            long count = Convert.ToInt64(result);

            return count > 0;
        }
    }

    // 비밀번호 암호화 메소드
    private string HashPassword(string rawPassword)
    {
        // 비밀번호에 섞을 랜덤 값
        byte[] salt = new byte[16];

        // 랜덤값 설정
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 비밀번호 해시 알고리즘 적용 (이건 더 공부해야할 것 같아.)
        using (var pbkdf2 = new Rfc2898DeriveBytes(
            rawPassword,
            salt,
            100_000,
            HashAlgorithmName.SHA256))
        {
            byte[] hash = pbkdf2.GetBytes(32);

            string saltBase64 = Convert.ToBase64String(salt);
            string hashBase64 = Convert.ToBase64String(hash);

            // 최종 저장 형태로 반환
            return $"{saltBase64}:{hashBase64}";
        }
    }

    // 비밀번호 검증 메소드
    private bool VerifyPassword(string rawPassword, string storedValue)
    {
        // salt, hash 분리
        string[] parts = storedValue.Split(':');
        if (parts.Length != 2)
            return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] savedHash = Convert.FromBase64String(parts[1]);

        // 비밀번호 비교 후 결과 반환
        using (var pbkdf2 = new Rfc2898DeriveBytes(
            rawPassword,
            salt,
            100_000,
            HashAlgorithmName.SHA256))
        {
            byte[] computedHash = pbkdf2.GetBytes(32);

            if (savedHash.Length != computedHash.Length)
                return false;

            for (int i = 0; i < savedHash.Length; i++)
            {
                if (savedHash[i] != computedHash[i])
                    return false;
            }

            return true;
        }
    }
}