using UnityEngine;

public class EvidenceZone : MonoBehaviour
{
    [SerializeField] private EvidencePoint[] points;

    private EvidencePoint realEvidencePoint;

    private void Awake()
    {
        if (points == null || points.Length == 0)
            points = GetComponentsInChildren<EvidencePoint>();

        SelectRandomEvidence();
    }

    private void SelectRandomEvidence()
    {
        int randomIndex = Random.Range(0, points.Length);
        realEvidencePoint = points[randomIndex];

        for (int i = 0; i < points.Length; i++)
        {
            bool isReal = points[i] == realEvidencePoint;
            points[i].SetIsRealEvidence(isReal);
            points[i].SetZone(this);
        }

        Debug.Log($"{name} : 진짜 증거 포인트는 {realEvidencePoint.name}");
    }

    public void OnRealEvidenceFound(EvidencePoint point)
    {
        Debug.Log($"{name} : 진짜 증거 발견 완료 - {point.name}");
    }
}