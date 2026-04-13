using Mirror;
using UnityEngine;

public class TerrorRadius : MonoBehaviour
{
    [Header("AudioSource")]
    [SerializeField] private AudioSource range1Source;
    [SerializeField] private AudioSource range2Source;
    [SerializeField] private AudioSource range3Source;

    [Header("심장소리")]
    [SerializeField] private AudioSource heartbeatSource; // 두근 소리 재생용
    [SerializeField] private AudioClip heartbeatClip;     // 두근 1번짜리 클립
    [SerializeField] private float heartbeatVolume = 1f;  // 심장소리 볼륨

    [Header("거리 단계")]
    [SerializeField] private float range1 = 32f;         // 바깥 단계
    [SerializeField] private float range2 = 16f;         // 중간 단계
    [SerializeField] private float range3 = 8f;           // 가까운 단계

    [Header("음악 전환")]
    [SerializeField] private float musicFadeSpeed = 3f;   // 음악 볼륨 변화 속도

    [Header("심장소리 간격")]
    [SerializeField] private float heartbeatInterval1 = 1.2f;   // 32m 이내
    [SerializeField] private float heartbeatInterval2 = 0.85f;  // 16m 이내
    [SerializeField] private float heartbeatInterval3 = 0.55f;   // 8m 이내

    [Header("탐색")]
    [SerializeField] private float findInterval = 1f;     // 킬러 다시 찾는 주기

    private Transform localPlayer;
    private Transform killer;

    private float nextFindTime;
    private float heartbeatTimer;

    // 단계 거리 제곱값
    private float range1Sqr;
    private float range2Sqr;
    private float range3Sqr;

    // 각 음악의 목표 볼륨
    private float range1Target;
    private float range2Target;
    private float range3Target;

    private void Awake()
    {
        // Update에서 sqrt를 쓰지 않도록 제곱값 미리 저장
        range1Sqr = range1 * range1;
        range2Sqr = range2 * range2;
        range3Sqr = range3 * range3;
    }

    private void Start()
    {
        FindLocalPlayer();
        FindKiller();

        StartMusicLoop(range1Source);
        StartMusicLoop(range2Source);
        StartMusicLoop(range3Source);

        SetupHeartbeatSource();
    }

    private void Update()
    {
        // 내 로컬 플레이어 다시 찾기
        if (localPlayer == null)
            FindLocalPlayer();

        // 킬러가 없으면 일정 주기마다만 다시 탐색
        if (killer == null && Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + findInterval;
            FindKiller();
        }

        // 아직 참조를 못 찾았으면 음악만 줄이고 종료
        if (localPlayer == null || killer == null)
        {
            SetMusicTargets(0f, 0f, 0f);
            UpdateMusicVolumes();

            heartbeatTimer = 0f;
            return;
        }

        // 생존자 로컬 플레이어만 이 오디오를 들음
        if (!localPlayer.CompareTag("Survivor"))
        {
            SetMusicTargets(0f, 0f, 0f);
            UpdateMusicVolumes();

            heartbeatTimer = 0f;
            return;
        }

        // sqrt 없는 거리 계산
        float sqrDistance = (localPlayer.position - killer.position).sqrMagnitude;

        // 음악 목표 볼륨 갱신 + 부드럽게 적용
        UpdateMusic(sqrDistance);
        UpdateMusicVolumes();

        // 심장소리 간격 재생
        UpdateHeartbeat(sqrDistance);
    }

    // 내 로컬 플레이어 찾기
    private void FindLocalPlayer()
    {
        if (NetworkClient.localPlayer != null)
            localPlayer = NetworkClient.localPlayer.transform;
    }

    // 씬에서 킬러 찾기
    private void FindKiller()
    {
        KillerState[] killers = FindObjectsByType<KillerState>(FindObjectsSortMode.None);

        for (int i = 0; i < killers.Length; i++)
        {
            if (killers[i] == null)
                continue;

            killer = killers[i].transform;
            return;
        }
    }

    // 음악 소스는 전부 루프로 미리 재생시켜 두고
    // 볼륨만 바꿔서 섞는다
    private void StartMusicLoop(AudioSource source)
    {
        if (source == null)
            return;

        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        if (!source.isPlaying)
            source.Play();
    }

    // 심장소리용 소스 설정
    private void SetupHeartbeatSource()
    {
        if (heartbeatSource == null)
            return;

        heartbeatSource.loop = false;
        heartbeatSource.playOnAwake = false;
        heartbeatSource.spatialBlend = 0f;
    }

    // 거리 단계 사이를 딱 끊지 않고 부드럽게 섞기 위한 목표 볼륨 계산
    private void UpdateMusic(float sqrDistance)
    {
        SetMusicTargets(0f, 0f, 0f);

        // 32m 밖이면 음악 없음
        if (sqrDistance > range2Sqr)
            return;

        // 8m 이내면 가장 가까운 음악만
        if (sqrDistance <= range3Sqr)
        {
            range3Target = 1f;
            return;
        }

        // 16m ~ 8m 사이는 2단계 음악에서 3단계 음악으로 천천히 섞음
        if (sqrDistance <= range2Sqr)
        {
            float distance = Mathf.Sqrt(sqrDistance);
            float t = Mathf.InverseLerp(range2, range1, distance);

            // distance가 16에 가까우면 range2이 큼
            // distance가 8에 가까우면 range3이 큼
            range2Target = t;
            range3Target = 1f - t;
            return;
        }

        // 32m ~ 16m 사이는 1단계 음악에서 2단계 음악으로 천천히 섞음
        {
            float distance = Mathf.Sqrt(sqrDistance);
            float t = Mathf.InverseLerp(range3, range2, distance);

            // distance가 32에 가까우면 range1가 큼
            // distance가 16에 가까우면 range2이 큼
            range1Target = t;
            range2Target = 1f - t;
        }
    }

    // 목표 볼륨 저장
    private void SetMusicTargets(float value1, float value2, float value3)
    {
        range1Target = value1;
        range2Target = value2;
        range3Target = value3;
    }

    // 현재 볼륨을 목표 볼륨으로 부드럽게 이동
    private void UpdateMusicVolumes()
    {
        FadeMusic(range1Source, range1Target);
        FadeMusic(range2Source, range2Target);
        FadeMusic(range3Source, range3Target);
    }

    private void FadeMusic(AudioSource source, float targetVolume)
    {
        if (source == null)
            return;

        source.volume = Mathf.Lerp(source.volume, targetVolume, Time.deltaTime * musicFadeSpeed);
    }

    // 거리 단계에 따라 심장소리 간격을 정해서
    // 두근 1회 클립을 반복 재생
    private void UpdateHeartbeat(float sqrDistance)
    {
        float interval = GetHeartbeatInterval(sqrDistance);

        // 범위 밖이면 타이머 초기화
        if (interval <= 0f)
        {
            heartbeatTimer = 0f;
            return;
        }

        heartbeatTimer -= Time.deltaTime;

        if (heartbeatTimer <= 0f)
        {
            PlayHeartbeat();
            heartbeatTimer = interval;
        }
    }

    // 현재 거리 단계에 맞는 심장소리 간격 반환
    private float GetHeartbeatInterval(float sqrDistance)
    {
        // 32m 밖이면 심장소리 없음
        if (sqrDistance > range1Sqr)
            return 0f;

        if (sqrDistance <= range3Sqr)
            return heartbeatInterval3;

        if (sqrDistance <= range2Sqr)
            return heartbeatInterval2;

        return heartbeatInterval1;
    }

    // 두근 1회 재생
    private void PlayHeartbeat()
    {
        if (heartbeatSource == null)
            return;

        if (heartbeatClip == null)
            return;

        heartbeatSource.PlayOneShot(heartbeatClip, heartbeatVolume);
    }
}