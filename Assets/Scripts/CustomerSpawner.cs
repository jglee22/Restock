using System.Collections.Generic;
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
    [SerializeField] StorePricing storePricing;
    [SerializeField] StoreStatistics storeStatistics;
    [SerializeField] CustomerDefinition[] customerDefinitions;
    [SerializeField] float spawnInterval = 4f;
    [SerializeField] int maxActiveCustomers = 5;
    [SerializeField] float browseDuration = 1.2f;

    int activeCustomerCount;
    float spawnTimer;
    bool hasWarned;
    readonly List<CheckoutCounter> availableCheckouts = new List<CheckoutCounter>();
    int nextCheckoutIndex;

    public int ActiveCustomerCount => activeCustomerCount;

    void Awake()
    {
        RegisterCheckout(checkout);
    }

    public void RegisterCheckout(CheckoutCounter checkoutCounter)
    {
        if (checkoutCounter == null || availableCheckouts.Contains(checkoutCounter))
        {
            return;
        }

        availableCheckouts.Add(checkoutCounter);
    }

    public void UnregisterCheckout(CheckoutCounter checkoutCounter)
    {
        if (checkoutCounter == null)
        {
            return;
        }

        int index = availableCheckouts.IndexOf(checkoutCounter);
        if (index < 0)
        {
            return;
        }

        availableCheckouts.RemoveAt(index);
        if (availableCheckouts.Count == 0)
        {
            nextCheckoutIndex = 0;
            return;
        }

        if (nextCheckoutIndex > index)
        {
            nextCheckoutIndex -= 1;
        }

        if (nextCheckoutIndex >= availableCheckouts.Count)
        {
            nextCheckoutIndex = 0;
        }
    }

    CheckoutCounter SelectCheckout()
    {
        int count = availableCheckouts.Count;
        if (count == 0)
        {
            WarnOnce("CustomerSpawner: 사용할 수 있는 계산대가 없습니다.");
            return null;
        }

        for (int attempt = 0; attempt < count; attempt++)
        {
            if (nextCheckoutIndex >= availableCheckouts.Count)
            {
                nextCheckoutIndex = 0;
            }

            CheckoutCounter candidate = availableCheckouts[nextCheckoutIndex];
            nextCheckoutIndex += 1;
            if (nextCheckoutIndex >= availableCheckouts.Count)
            {
                nextCheckoutIndex = 0;
            }

            if (candidate != null && candidate.isActiveAndEnabled)
            {
                return candidate;
            }
        }

        WarnOnce("CustomerSpawner: 사용할 수 있는 계산대가 없습니다.");
        return null;
    }

    public void RegisterShoppingShelf(Shelf shelf)
    {
        if (shelf == null)
        {
            return;
        }

        if (ContainsShoppingShelf(shelf))
        {
            return;
        }

        int oldLength = shoppingShelves == null ? 0 : shoppingShelves.Length;
        Shelf[] next = new Shelf[oldLength + 1];
        for (int index = 0; index < oldLength; index++)
        {
            next[index] = shoppingShelves[index];
        }

        next[oldLength] = shelf;
        shoppingShelves = next;
    }

    public void UnregisterShoppingShelf(Shelf shelf)
    {
        if (shelf == null || shoppingShelves == null)
        {
            return;
        }

        int found = -1;
        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            if (shoppingShelves[index] == shelf)
            {
                found = index;
                break;
            }
        }

        if (found < 0)
        {
            return;
        }

        Shelf[] next = new Shelf[shoppingShelves.Length - 1];
        int write = 0;
        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            if (index == found)
            {
                continue;
            }

            next[write] = shoppingShelves[index];
            write += 1;
        }

        shoppingShelves = next;
    }

    bool ContainsShoppingShelf(Shelf shelf)
    {
        if (shoppingShelves == null)
        {
            return false;
        }

        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            if (shoppingShelves[index] == shelf)
            {
                return true;
            }
        }

        return false;
    }

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

        if (!TryChooseCustomerDefinition(out CustomerDefinition definition))
        {
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

        CheckoutCounter selectedCheckout = SelectCheckout();
        if (selectedCheckout == null)
        {
            Destroy(customer.gameObject);
            return;
        }

        if (storePricing == null)
        {
            WarnOnce("CustomerSpawner: StorePricing이 연결되지 않았습니다.");
        }

        activeCustomerCount += 1;
        customer.Begin(this, insidePoint, exitPoint, shoppingShelves, browseDuration, selectedCheckout, storePricing, definition);
        if (storeStatistics == null)
        {
            WarnOnce("CustomerSpawner: StoreStatistics가 연결되지 않아 방문 고객을 기록하지 못했습니다.");
            return;
        }

        storeStatistics.RecordCustomerVisit();
    }

    bool TryChooseCustomerDefinition(out CustomerDefinition definition)
    {
        definition = null;
        if (customerDefinitions == null || customerDefinitions.Length == 0)
        {
            WarnOnce("CustomerSpawner: 고객 유형이 연결되지 않아 고객을 만들지 않습니다.");
            return false;
        }

        int index = Random.Range(0, customerDefinitions.Length);
        CustomerDefinition candidate = customerDefinitions[index];
        if (candidate == null || !candidate.CanSpawn())
        {
            WarnOnce("CustomerSpawner: 고객 유형이 비어 있거나 값이 올바르지 않아 고객을 만들지 않습니다.");
            return false;
        }

        definition = candidate;
        return true;
    }

    void OnValidate()
    {
        if (customerDefinitions == null || customerDefinitions.Length == 0)
        {
            Debug.LogWarning("CustomerSpawner: 고객 유형이 연결되지 않았습니다.", this);
            return;
        }

        for (int index = 0; index < customerDefinitions.Length; index++)
        {
            CustomerDefinition definition = customerDefinitions[index];
            if (definition == null)
            {
                Debug.LogWarning($"CustomerSpawner: 고객 유형 {index}가 비어 있습니다.", this);
                continue;
            }

            if (!definition.CanSpawn())
            {
                Debug.LogWarning($"CustomerSpawner: {definition.name} 고객 유형의 값이 올바르지 않아 스폰에 사용하지 않습니다.", this);
            }
        }
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
