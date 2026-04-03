using Mirror;
using UnityEngine;

public class EvidenceZone : MonoBehaviour
{
    [SerializeField] private EvidencePoint[] points;

    // 이 구역에서 진짜 증거 1개
    private EvidencePoint realEvidencePoint;

    private void Start()
    {
        // 서버에서만 진짜 증거 선택
        if (!NetworkServer.active)
            return;

        if (points == null || points.Length == 0)
            points = GetComponentsInChildren<EvidencePoint>(true);

        SelectRandomEvidenceServer();
    }

    // 서버만 여러 포인트 중 하나를 랜덤으로 진짜 증거로 선택
    private void SelectRandomEvidenceServer()
    {
        if (points == null || points.Length == 0)
            return;

        int randomIndex = Random.Range(0, points.Length);
        realEvidencePoint = points[randomIndex];

        for (int i = 0; i < points.Length; i++)
        {
            bool isReal = points[i] == realEvidencePoint;

            points[i].SetZone(this);
            points[i].SetIsRealEvidenceServer(isReal);
        }

        Debug.Log($"{name} : 진짜 증거는 {realEvidencePoint.name}");
    }

    // 진짜 증거가 발견됐을 때 호출됨
    public void OnRealEvidenceFound(EvidencePoint point)
    {
        // 서버에서만 처리
        if (!NetworkServer.active)
            return;

        Debug.Log($"{name} : 진짜 증거 발견 완료 - {point.name}");

        // 나중에 여기서:
        // 총 증거 개수 증가
        // 문 열기
        // 목표 진행도 갱신
        // 같은 처리 추가 
    }
}