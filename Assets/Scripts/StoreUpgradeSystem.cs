using UnityEngine;

public enum StoreUpgradeType
{
    FastCheckout,
    Advertising,
    WordOfMouth
}

// 준비 시간에 사는 영구 업그레이드 3종.
// 효과는 기존 값에 곱하는 배율만 제공하고, 날짜가 바뀌어도 레벨을 유지한다.
public class StoreUpgradeSystem : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] UpgradeConfig fastCheckout = new UpgradeConfig
    {
        displayName = "빠른 계산",
        levelCosts = new[] { 30000, 60000, 100000 },
        perLevelMultiplier = 0.9f
    };
    [SerializeField] UpgradeConfig advertising = new UpgradeConfig
    {
        displayName = "광고",
        levelCosts = new[] { 25000, 50000, 80000 },
        perLevelMultiplier = 1.1f
    };
    [SerializeField] UpgradeConfig wordOfMouth = new UpgradeConfig
    {
        displayName = "입소문",
        levelCosts = new[] { 20000, 40000, 70000 },
        perLevelMultiplier = 0.9f
    };

    int fastCheckoutLevel;
    int advertisingLevel;
    int wordOfMouthLevel;
    float cachedCheckoutMultiplier = 1f;
    float cachedPurchaseMultiplier = 1f;
    float cachedSpawnMultiplier = 1f;
    bool hasWarned;

    public float CheckoutDurationMultiplier => cachedCheckoutMultiplier;
    public float PurchaseChanceMultiplier => cachedPurchaseMultiplier;
    public float SpawnIntervalMultiplier => cachedSpawnMultiplier;

    void Awake()
    {
        Recalculate();
    }

    public string GetDisplayName(StoreUpgradeType type)
    {
        UpgradeConfig config = ConfigFor(type);
        if (config == null || string.IsNullOrEmpty(config.displayName))
        {
            return string.Empty;
        }

        return config.displayName;
    }

    public int GetLevel(StoreUpgradeType type)
    {
        switch (type)
        {
            case StoreUpgradeType.FastCheckout:
                return fastCheckoutLevel;
            case StoreUpgradeType.Advertising:
                return advertisingLevel;
            default:
                return wordOfMouthLevel;
        }
    }

    public int GetMaxLevel(StoreUpgradeType type)
    {
        UpgradeConfig config = ConfigFor(type);
        if (config == null || config.levelCosts == null)
        {
            return 0;
        }

        return config.levelCosts.Length;
    }

    public float GetLevelMultiplier(StoreUpgradeType type)
    {
        return MultiplierFor(ConfigFor(type), GetLevel(type));
    }

    public bool TryGetNextMultiplier(StoreUpgradeType type, out float multiplier)
    {
        multiplier = 1f;
        int level = GetLevel(type);
        if (level >= GetMaxLevel(type))
        {
            return false;
        }

        multiplier = MultiplierFor(ConfigFor(type), level + 1);
        return multiplier > 0f;
    }

    public string GetSummary(StoreUpgradeType type)
    {
        switch (type)
        {
            case StoreUpgradeType.FastCheckout:
                return "계산 시간을 줄입니다.";
            case StoreUpgradeType.Advertising:
                return "상품 구매 확률을 높입니다.";
            default:
                return "고객 방문 간격을 줄입니다.";
        }
    }

    public bool TryGetNextCost(StoreUpgradeType type, out int cost)
    {
        cost = 0;
        UpgradeConfig config = ConfigFor(type);
        int level = GetLevel(type);
        if (config == null || config.levelCosts == null || level < 0 || level >= config.levelCosts.Length)
        {
            return false;
        }

        cost = config.levelCosts[level];
        return true;
    }

    public bool CanPurchase(StoreUpgradeType type)
    {
        if (!CanChangeLevel(type, out int cost))
        {
            return false;
        }

        return economy != null && economy.CanAffordUpgrade(cost);
    }

    public bool TryPurchase(StoreUpgradeType type)
    {
        if (!CanChangeLevel(type, out int cost))
        {
            return false;
        }

        if (economy == null || !economy.TrySpendUpgrade(cost))
        {
            return false;
        }

        SetLevel(type, GetLevel(type) + 1);
        Recalculate();
        return true;
    }

    public bool TryRestoreLevels(int checkoutLevel, int advertisingLevelValue, int trafficLevel)
    {
        if (!IsRestorable(fastCheckout, checkoutLevel)
            || !IsRestorable(advertising, advertisingLevelValue)
            || !IsRestorable(wordOfMouth, trafficLevel))
        {
            return false;
        }

        fastCheckoutLevel = checkoutLevel;
        advertisingLevel = advertisingLevelValue;
        wordOfMouthLevel = trafficLevel;
        Recalculate();
        return true;
    }

    bool CanChangeLevel(StoreUpgradeType type, out int cost)
    {
        cost = 0;
        if (session == null || session.Phase != StorePhase.Preparation)
        {
            return false;
        }

        if (!TryGetNextCost(type, out cost) || cost < 0)
        {
            return false;
        }

        return true;
    }

    static bool IsRestorable(UpgradeConfig config, int level)
    {
        int maxLevel = config == null || config.levelCosts == null ? 0 : config.levelCosts.Length;
        return level >= 0 && level <= maxLevel;
    }

    void SetLevel(StoreUpgradeType type, int level)
    {
        switch (type)
        {
            case StoreUpgradeType.FastCheckout:
                fastCheckoutLevel = level;
                break;
            case StoreUpgradeType.Advertising:
                advertisingLevel = level;
                break;
            default:
                wordOfMouthLevel = level;
                break;
        }
    }

    UpgradeConfig ConfigFor(StoreUpgradeType type)
    {
        switch (type)
        {
            case StoreUpgradeType.FastCheckout:
                return fastCheckout;
            case StoreUpgradeType.Advertising:
                return advertising;
            default:
                return wordOfMouth;
        }
    }

    void Recalculate()
    {
        cachedCheckoutMultiplier = MultiplierFor(fastCheckout, fastCheckoutLevel);
        cachedPurchaseMultiplier = MultiplierFor(advertising, advertisingLevel);
        cachedSpawnMultiplier = MultiplierFor(wordOfMouth, wordOfMouthLevel);
    }

    float MultiplierFor(UpgradeConfig config, int level)
    {
        if (level <= 0)
        {
            return 1f;
        }

        if (config == null || config.perLevelMultiplier <= 0f)
        {
            WarnOnce("StoreUpgradeSystem: 레벨 배율은 0보다 커야 합니다.");
            return 1f;
        }

        return Mathf.Pow(config.perLevelMultiplier, level);
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
        ValidateConfig(fastCheckout, "빠른 계산");
        ValidateConfig(advertising, "광고");
        ValidateConfig(wordOfMouth, "입소문");
    }

    void ValidateConfig(UpgradeConfig config, string name)
    {
        if (config == null || config.levelCosts == null || config.levelCosts.Length == 0)
        {
            Debug.LogWarning($"StoreUpgradeSystem: {name} 비용 목록이 비어 있습니다.", this);
            return;
        }

        if (config.perLevelMultiplier <= 0f)
        {
            Debug.LogWarning($"StoreUpgradeSystem: {name} 배율은 0보다 커야 합니다. 현재 값: {config.perLevelMultiplier}", this);
        }

        for (int index = 0; index < config.levelCosts.Length; index++)
        {
            if (config.levelCosts[index] < 0)
            {
                Debug.LogWarning($"StoreUpgradeSystem: {name} 비용은 0 이상이어야 합니다. 현재 값: {config.levelCosts[index]}", this);
            }
        }
    }

    [System.Serializable]
    public class UpgradeConfig
    {
        public string displayName;
        public int[] levelCosts;
        public float perLevelMultiplier = 1f;
    }
}
