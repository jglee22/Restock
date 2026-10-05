using System.Globalization;
using System.Text;
using UnityEngine;

// 달력 날짜와 분리된 5단계 진행.
// 완료한 단계 정수 하나로 상품, 건설, 업그레이드, 이벤트를 다시 계산한다.
[DefaultExecutionOrder(200)]
public class StoreProgression : MonoBehaviour
{
    public const int StageCount = 5;
    public const int CampaignCompletedStage = 5;

    public static StoreProgression Instance { get; private set; }

    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] int[] stageRevenueGoals = { 38000, 79000, 81000, 85000, 90000 };

    const string StageOneRamenId = "ramen";
    const string StageOneAisleShelfName = "AisleShelf_02";
    const string StageOneWallShelfName = "WallShelf_NorthWest";
    const int StageOneRamenNearCount = 6;
    const int StageOneRamenAisleCount = 5;
    const int StageOneRamenWallCount = 5;

    struct MerchandiseSnapshot
    {
        public Shelf shelf;
        public ProductDefinition product;
        public int quantity;
    }

    int completedStage;
    bool evaluated;
    bool succeeded;
    bool hadGoal;
    int evaluatedGoal;
    int evaluatedRevenue;
    MerchandiseSnapshot[] newGameMerchandise;

    public int CompletedStage => completedStage;
    public bool IsCampaignComplete => completedStage >= CampaignCompletedStage;
    public bool HasEvaluation => evaluated;
    public bool LastSucceeded => succeeded;
    public int LastGoal => evaluatedGoal;
    public int LastRevenue => evaluatedRevenue;

    public bool ShowsCampaignComplete =>
        IsCampaignComplete || (evaluated && succeeded && completedStage + 1 >= CampaignCompletedStage);

    public int UpgradeLevelCap
    {
        get
        {
            if (completedStage >= 4)
            {
                return 3;
            }

            if (completedStage >= 3)
            {
                return 1;
            }

            return 0;
        }
    }

    void Awake()
    {
        Instance = this;
        completedStage = 0;
        evaluated = false;
        succeeded = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        ApplyNewGameShelves();
    }

    public bool IsProductUnlocked(ProductDefinition product)
    {
        if (product == null)
        {
            return false;
        }

        return IsCampaignComplete || product.UnlockStage <= completedStage;
    }

    public bool IsFacilityUnlocked(FacilityDefinition facility)
    {
        if (facility == null)
        {
            return false;
        }

        return IsCampaignComplete || facility.UnlockStage <= completedStage;
    }

    public bool HasUnlockedFacility(FacilityDefinition[] facilities)
    {
        if (facilities == null)
        {
            return false;
        }

        for (int index = 0; index < facilities.Length; index++)
        {
            if (IsFacilityUnlocked(facilities[index]))
            {
                return true;
            }
        }

        return false;
    }

    public bool AllowsEvent(StoreEventType eventType)
    {
        if (eventType == StoreEventType.None || IsCampaignComplete || completedStage >= 3)
        {
            return true;
        }

        if (completedStage <= 0)
        {
            return false;
        }

        if (completedStage == 1)
        {
            return eventType == StoreEventType.ProductTrend;
        }

        return eventType != StoreEventType.CheckoutDelay;
    }

    public bool TryGetDailyGoal(out int goal)
    {
        goal = 0;
        if (IsCampaignComplete || stageRevenueGoals == null || stageRevenueGoals.Length == 0)
        {
            return false;
        }

        int index = Mathf.Clamp(completedStage, 0, stageRevenueGoals.Length - 1);
        goal = stageRevenueGoals[index];
        return goal > 0;
    }

    public void EvaluateDay()
    {
        if (evaluated)
        {
            return;
        }

        evaluated = true;
        evaluatedRevenue = economy != null ? economy.DailyRevenue : 0;
        succeeded = false;
        hadGoal = false;
        evaluatedGoal = 0;
        if (IsCampaignComplete)
        {
            return;
        }

        if (!TryGetDailyGoal(out evaluatedGoal))
        {
            return;
        }

        hadGoal = true;
        succeeded = evaluatedRevenue >= evaluatedGoal;
    }

    public void CommitDay()
    {
        if (!evaluated)
        {
            return;
        }

        if (succeeded && completedStage < CampaignCompletedStage)
        {
            completedStage += 1;
        }

        evaluated = false;
        succeeded = false;
        hadGoal = false;
    }

    public bool TryRestoreCompletedStage(int stage)
    {
        if (stage < 0 || stage > CampaignCompletedStage)
        {
            Debug.LogWarning($"StoreProgression: 진행 단계가 올바르지 않습니다. 현재 값: {stage}", this);
            return false;
        }

        completedStage = stage;
        evaluated = false;
        succeeded = false;
        hadGoal = false;
        return true;
    }

    public string HudLabel(int revenue)
    {
        if (ShowsCampaignComplete)
        {
            return "캠페인 완료";
        }

        if (!TryGetDailyGoal(out int goal))
        {
            return string.Empty;
        }

        return "목표 " + FormatWon(goal) + " / 현재 " + FormatWon(revenue);
    }

    public void AppendResult(StringBuilder builder)
    {
        if (!evaluated)
        {
            EvaluateDay();
        }

        builder.Append("\n\n[목표]\n");
        if (IsCampaignComplete)
        {
            builder.Append("캠페인 완료\n");
            builder.Append("모든 콘텐츠가 열려 있습니다.");
            return;
        }

        if (!hadGoal)
        {
            builder.Append("오늘 매출 ").Append(FormatWon(evaluatedRevenue));
            return;
        }

        builder.Append("오늘 목표 ").Append(FormatWon(evaluatedGoal)).Append('\n');
        builder.Append("오늘 매출 ").Append(FormatWon(evaluatedRevenue)).Append('\n');
        if (!succeeded)
        {
            builder.Append("목표 미달\n");
            builder.Append("다음 날 다시 도전");
            return;
        }

        if (completedStage + 1 >= CampaignCompletedStage)
        {
            builder.Append("캠페인 완료\n");
            builder.Append("모든 콘텐츠 해금 완료");
            return;
        }

        builder.Append("목표 달성\n");
        builder.Append("신규 해금 ").Append(RewardText(completedStage)).Append('\n');
        int nextIndex = completedStage + 1;
        if (stageRevenueGoals != null && nextIndex < stageRevenueGoals.Length && stageRevenueGoals[nextIndex] > 0)
        {
            builder.Append("다음 목표 ").Append(FormatWon(stageRevenueGoals[nextIndex]));
        }
    }

    static string RewardText(int stage)
    {
        switch (stage)
        {
            case 0:
                return "라면 상품군";
            case 1:
                return "과자 상품군, 진열대";
            case 2:
                return "벽 진열대, 냉장고, 업그레이드";
            case 3:
                return "계산대, 업그레이드 상한";
            default:
                return "모든 콘텐츠";
        }
    }

    static string FormatWon(int amount)
    {
        return "₩" + amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    void ApplyNewGameShelves()
    {
        if (completedStage != 0)
        {
            return;
        }

        Shelf[] shelves = Object.FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null)
            {
                continue;
            }

            ProductDefinition product = shelf.AssignedProduct;
            if (product == null || IsProductUnlocked(product))
            {
                continue;
            }

            shelf.ClearDisplay();
        }

        DistributeStageOneRamen(shelves);
    }

    void DistributeStageOneRamen(Shelf[] shelves)
    {
        Shelf source = null;
        Shelf aisle = null;
        Shelf wall = null;
        int ramenShelfCount = 0;
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null)
            {
                continue;
            }

            ProductDefinition product = shelf.AssignedProduct;
            if (product != null && product.ProductId == StageOneRamenId && shelf.CurrentQuantity > 0)
            {
                ramenShelfCount += 1;
                source = shelf;
            }

            if (product != null || shelf.CurrentQuantity != 0 || shelf.AcceptedStorageType != ProductStorageType.Shelf)
            {
                continue;
            }

            if (shelf.name == StageOneAisleShelfName)
            {
                aisle = shelf;
            }
            else if (shelf.name == StageOneWallShelfName)
            {
                wall = shelf;
            }
        }

        int distributedTotal = StageOneRamenNearCount + StageOneRamenAisleCount + StageOneRamenWallCount;
        if (ramenShelfCount != 1 || source == null || aisle == null || wall == null || source.CurrentQuantity != distributedTotal)
        {
            Debug.LogWarning("StoreProgression: Stage 1 라면 분산 진열을 적용하지 못했습니다.", this);
            return;
        }

        ProductDefinition ramen = source.AssignedProduct;
        newGameMerchandise = new MerchandiseSnapshot[]
        {
            CaptureShelf(source),
            CaptureShelf(aisle),
            CaptureShelf(wall)
        };

        if (source.TryRestoreState(ramen, StageOneRamenNearCount)
            && aisle.TryRestoreState(ramen, StageOneRamenAisleCount)
            && wall.TryRestoreState(ramen, StageOneRamenWallCount))
        {
            return;
        }

        RevertNewGameMerchandise();
        Debug.LogWarning("StoreProgression: Stage 1 라면 분산 진열 적용에 실패해 원래 진열로 되돌렸습니다.", this);
    }

    static MerchandiseSnapshot CaptureShelf(Shelf shelf)
    {
        return new MerchandiseSnapshot
        {
            shelf = shelf,
            product = shelf.AssignedProduct,
            quantity = shelf.CurrentQuantity
        };
    }

    public void RevertNewGameMerchandise()
    {
        if (newGameMerchandise == null)
        {
            return;
        }

        for (int index = 0; index < newGameMerchandise.Length; index++)
        {
            MerchandiseSnapshot snapshot = newGameMerchandise[index];
            if (snapshot.shelf == null)
            {
                continue;
            }

            if (!snapshot.shelf.TryRestoreState(snapshot.product, snapshot.quantity))
            {
                Debug.LogWarning("StoreProgression: 새 게임 진열 분산을 되돌리지 못했습니다.", snapshot.shelf);
            }
        }

        newGameMerchandise = null;
    }
}
