using UnityEngine;

public enum StoreEventType
{
    None,
    RushHour,
    CheckoutDelay,
    ProductTrend
}

// 하루 영업 이벤트를 Open 진입 때 한 번만 정한다.
// Preparation에서는 미결정이고, 다음 Preparation에서 다시 비운다.
// 효과는 기존 값에 곱하는 배율만 제공한다.
public class StoreEventSystem : MonoBehaviour
{
    const string UndecidedStatus = "이벤트: 영업 시작 시 결정";
    const string NoneStatus = "이벤트: 없음";
    const string RushHourStatus = "이벤트: 붐비는 시간대";
    const string CheckoutDelayStatus = "이벤트: 결제 지연";
    const string ProductTrendStatusPrefix = "이벤트: 인기 상품 - ";
    const string ResultPrefix = "오늘의 이벤트: ";

    [SerializeField] StoreInventory inventory;
    [SerializeField] int noneWeight = 1;
    [SerializeField] int rushHourWeight = 1;
    [SerializeField] int checkoutDelayWeight = 1;
    [SerializeField] int productTrendWeight = 1;
    [SerializeField] float spawnIntervalMultiplier = 0.75f;
    [SerializeField] float checkoutDurationMultiplier = 1.5f;
    [SerializeField] float purchaseChanceMultiplier = 1.35f;

    StoreEventType currentEvent = StoreEventType.None;
    ProductDefinition trendProduct;
    bool eventRolled;
    int rolledDay;
    string statusLabel = UndecidedStatus;
    string resultLabel = ResultPrefix + "없음";
    bool hasWarned;

    public StoreEventType CurrentEvent => currentEvent;
    public ProductDefinition CurrentTrendProduct => trendProduct;
    public bool EventRolled => eventRolled;
    public string StatusLabel => statusLabel;
    public string ResultLabel => resultLabel;

    public float SpawnIntervalMultiplier
    {
        get
        {
            if (currentEvent != StoreEventType.RushHour)
            {
                return 1f;
            }

            if (spawnIntervalMultiplier <= 0f)
            {
                WarnOnce($"StoreEventSystem: Spawn Interval Multiplier는 0보다 커야 합니다. 현재 값: {spawnIntervalMultiplier}");
                return 1f;
            }

            return spawnIntervalMultiplier;
        }
    }

    public float CheckoutDurationMultiplier
    {
        get
        {
            if (currentEvent != StoreEventType.CheckoutDelay)
            {
                return 1f;
            }

            if (checkoutDurationMultiplier <= 0f)
            {
                WarnOnce($"StoreEventSystem: Checkout Duration Multiplier는 0보다 커야 합니다. 현재 값: {checkoutDurationMultiplier}");
                return 1f;
            }

            return checkoutDurationMultiplier;
        }
    }

    public float GetPurchaseChanceMultiplier(ProductDefinition product)
    {
        if (currentEvent != StoreEventType.ProductTrend || trendProduct == null || product != trendProduct)
        {
            return 1f;
        }

        if (purchaseChanceMultiplier < 0f)
        {
            WarnOnce($"StoreEventSystem: Purchase Chance Multiplier는 0 이상이어야 합니다. 현재 값: {purchaseChanceMultiplier}");
            return 1f;
        }

        return purchaseChanceMultiplier;
    }

    public void NotifyPreparation()
    {
        currentEvent = StoreEventType.None;
        trendProduct = null;
        eventRolled = false;
        rolledDay = 0;
        statusLabel = UndecidedStatus;
        resultLabel = ResultPrefix + "없음";
    }

    public void NotifyOpen(int day)
    {
        if (eventRolled && rolledDay == day)
        {
            return;
        }

        Roll(day);
    }

    void Roll(int day)
    {
        int none = NonNegativeWeight(noneWeight, "None");
        int rush = NonNegativeWeight(rushHourWeight, "Rush Hour");
        int delay = NonNegativeWeight(checkoutDelayWeight, "Checkout Delay");
        int trend = NonNegativeWeight(productTrendWeight, "Product Trend");
        int total = none + rush + delay + trend;
        eventRolled = true;
        rolledDay = day;
        trendProduct = null;

        if (total <= 0)
        {
            currentEvent = StoreEventType.None;
            statusLabel = NoneStatus;
            resultLabel = ResultPrefix + "없음";
            WarnOnce("StoreEventSystem: 이벤트 가중치 합이 0이라 없음으로 정합니다.");
            return;
        }

        int roll = Random.Range(0, total);
        if (roll < none)
        {
            Apply(StoreEventType.None, null);
            return;
        }

        roll -= none;
        if (roll < rush)
        {
            Apply(StoreEventType.RushHour, null);
            return;
        }

        roll -= rush;
        if (roll < delay)
        {
            Apply(StoreEventType.CheckoutDelay, null);
            return;
        }

        if (!TryPickTrendProduct(out ProductDefinition product))
        {
            currentEvent = StoreEventType.None;
            statusLabel = NoneStatus;
            resultLabel = ResultPrefix + "없음";
            WarnOnce("StoreEventSystem: 선택할 상품이 없어 인기 상품 이벤트를 없음으로 바꿉니다.");
            return;
        }

        Apply(StoreEventType.ProductTrend, product);
    }

    void Apply(StoreEventType eventType, ProductDefinition product)
    {
        currentEvent = eventType;
        trendProduct = product;
        switch (eventType)
        {
            case StoreEventType.RushHour:
                statusLabel = RushHourStatus;
                resultLabel = ResultPrefix + "붐비는 시간대";
                break;
            case StoreEventType.CheckoutDelay:
                statusLabel = CheckoutDelayStatus;
                resultLabel = ResultPrefix + "결제 지연";
                break;
            case StoreEventType.ProductTrend:
                string productName = product != null && !string.IsNullOrEmpty(product.DisplayName)
                    ? product.DisplayName
                    : "상품";
                statusLabel = ProductTrendStatusPrefix + productName;
                resultLabel = ResultPrefix + "인기 상품 - " + productName;
                break;
            default:
                statusLabel = NoneStatus;
                resultLabel = ResultPrefix + "없음";
                break;
        }
    }

    bool TryPickTrendProduct(out ProductDefinition product)
    {
        product = null;
        if (inventory == null)
        {
            WarnOnce("StoreEventSystem: StoreInventory가 연결되지 않았습니다.");
            return false;
        }

        int count = inventory.ProductDefinitionCount;
        int validCount = 0;
        for (int index = 0; index < count; index++)
        {
            if (inventory.GetProductDefinition(index) != null)
            {
                validCount += 1;
            }
        }

        if (validCount == 0)
        {
            return false;
        }

        int pick = Random.Range(0, validCount);
        for (int index = 0; index < count; index++)
        {
            ProductDefinition candidate = inventory.GetProductDefinition(index);
            if (candidate == null)
            {
                continue;
            }

            if (pick == 0)
            {
                product = candidate;
                return true;
            }

            pick -= 1;
        }

        return false;
    }

    int NonNegativeWeight(int weight, string name)
    {
        if (weight >= 0)
        {
            return weight;
        }

        WarnOnce($"StoreEventSystem: {name} Weight는 0 이상이어야 합니다. 현재 값: {weight}");
        return 0;
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

    void OnValidate()
    {
        if (noneWeight < 0)
        {
            Debug.LogWarning($"StoreEventSystem: None Weight는 0 이상이어야 합니다. 현재 값: {noneWeight}", this);
        }

        if (rushHourWeight < 0)
        {
            Debug.LogWarning($"StoreEventSystem: Rush Hour Weight는 0 이상이어야 합니다. 현재 값: {rushHourWeight}", this);
        }

        if (checkoutDelayWeight < 0)
        {
            Debug.LogWarning($"StoreEventSystem: Checkout Delay Weight는 0 이상이어야 합니다. 현재 값: {checkoutDelayWeight}", this);
        }

        if (productTrendWeight < 0)
        {
            Debug.LogWarning($"StoreEventSystem: Product Trend Weight는 0 이상이어야 합니다. 현재 값: {productTrendWeight}", this);
        }

        if (spawnIntervalMultiplier <= 0f)
        {
            Debug.LogWarning($"StoreEventSystem: Spawn Interval Multiplier는 0보다 커야 합니다. 현재 값: {spawnIntervalMultiplier}", this);
        }

        if (checkoutDurationMultiplier <= 0f)
        {
            Debug.LogWarning($"StoreEventSystem: Checkout Duration Multiplier는 0보다 커야 합니다. 현재 값: {checkoutDurationMultiplier}", this);
        }

        if (purchaseChanceMultiplier < 0f)
        {
            Debug.LogWarning($"StoreEventSystem: Purchase Chance Multiplier는 0 이상이어야 합니다. 현재 값: {purchaseChanceMultiplier}", this);
        }
    }
}
