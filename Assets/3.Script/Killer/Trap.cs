using UnityEngine;
using Mirror;

public class Trap : NetworkBehaviour
{
    [Header("설정")]
    [SerializeField] private float stunDuration = 3.0f; // 생존자 구속 시간
    [SerializeField] private Animator animator;

    [SyncVar]
    private bool isTriggered = false; // 중복 발동 방지

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // 서버에서만 물리 충돌을 감지합니다.
    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        // 1. 생존자 레이어 또는 태그 확인
        if (other.CompareTag("Survivor"))
        {
            SurvivorState survivor = other.GetComponentInParent<SurvivorState>();

            // 2. 유효한 생존자이고, 이미 다운된 상태가 아닐 때만 발동
            if (survivor != null && !survivor.IsDowned && !survivor.IsDead)
            {
                TriggerTrap(survivor);
            }
        }
    }

    [Server]
    private void TriggerTrap(SurvivorState survivor)
    {
        isTriggered = true;

        // 생존자에게 스턴 및 피격 적용
        survivor.ApplyTrapStun(stunDuration);

        // 애니메이션 동기화
        RpcPlayTriggerEffects();

        // 3초 뒤 오브젝트 제거 (NetworkServer.Destroy 사용)
        StartCoroutine(DestroyAfterDelay(3.0f));
    }

    [ClientRpc]
    private void RpcPlayTriggerEffects()
    {
        if (animator != null)
            animator.SetTrigger("Snap"); // 삐에로 가면 튀어나오는 트리거

        // 여기에 발동 사운드 추가 가능
    }

    private System.Collections.IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        NetworkServer.Destroy(gameObject);
    }
}