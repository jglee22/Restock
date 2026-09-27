using UnityEngine;

// Open 동안만 고객을 만들고, 퇴장하면 수를 줄인다.
// 영업 상태가 아니면 생성 타이머를 쌓지 않는다.
public class CustomerSpawner : MonoBehaviour
{
    [SerializeField] CustomerMover customerPrefab;
    [SerializeField] StoreSession session;
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform insidePoint;
    [SerializeField] Transform exitPoint;
    [SerializeField] Shelf[] shoppingShelves;
    [SerializeField] CheckoutCounter checkout;
    [SerializeField] float spawnInterval = 4f;
    [SerializeField] int maxActiveCustomers = 5;
    [SerializeField] float browseDuration = 1.2f;

    int activeCustomerCount;
    float spawnTimer;
    bool hasWarned;

    public int ActiveCustomerCount => activeCustomerCount;

    void Update()
    {
        if (session == null || session.Phase != StorePhase.Open)
        {
            spawnTimer = 0f;
            return;
        }

        if (activeCustomerCount >= maxActiveCustomers)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer < spawnInterval)
        {
            return;
        }

        spawnTimer = 0f;
        SpawnCustomer();
    }

    public void NotifyDeparted()
    {
        activeCustomerCount = Mathf.Max(0, activeCustomerCount - 1);
    }

    void SpawnCustomer()
    {
        if (customerPrefab == null || spawnPoint == null || insidePoint == null || exitPoint == null)
        {
            WarnOnce("CustomerSpawner: 고객 Prefab 또는 이동 지점이 연결되지 않았습니다.");
            return;
        }

        UnityEngine.AI.NavMeshAgent prefabAgent = customerPrefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        float baseOffset = prefabAgent != null ? prefabAgent.baseOffset : 0f;
        Vector3 pivot = spawnPoint.position + Vector3.up * baseOffset;
        CustomerMover customer = Instantiate(customerPrefab, pivot, spawnPoint.rotation);
        UnityEngine.AI.NavMeshAgent agent = customer.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null || !agent.Warp(pivot))
        {
            WarnOnce("CustomerSpawner: Spawn Point가 NavMesh 위에 없어 고객을 배치하지 못했습니다.");
            Destroy(customer.gameObject);
            return;
        }

        activeCustomerCount += 1;
        customer.Begin(this, insidePoint, exitPoint, shoppingShelves, browseDuration, checkout);
    }

    void WarnOnce(string message)
    {
        if (hasWarned)
        {
            return;
        }

        hasWarned = true;
        Debug.LogWarning(message, this);
    }
}
