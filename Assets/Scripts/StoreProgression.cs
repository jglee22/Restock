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

    const string StarterRamenId = "ramen";
    const int StarterRamenCount = 16;
    const string StarterShelfName = "ShelfPlaceholder";
    const string WaterRefrigeratorName = "Refrigerator_Water";
    const string ColaRefrigeratorName = "Refrigerator_Cola";
    const string FirstAisleShelfName = "AisleShelf_02";
    const string SecondAisleShelfName = "AisleShelf_02 (1)";
    const string NorthWestWallShelfName = "WallShelf_NorthWest";
    const string NorthWallShelfName = "WallShelfPlaceholder";
    const string WestWallShelfName = "WallShelf_WestNorth";
    const string EastWallShelfName = "WallShelf_East";
    const string WestCenterWallShelfName = "WallShelfPlaceholder (1)";

    int completedStage;
    bool evaluated;
    bool succeeded;
    bool hadGoal;
    int evaluatedGoal;
    int evaluatedRevenue;

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
        ApplyFacilityVisibility();
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
        ApplyFacilityVisibility();
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
                return "라면 상품군, 통로 진열 공간";
            case 1:
                return "과자 상품군, 북쪽 벽 진열 공간, 진열대";
            case 2:
                return "나머지 벽 진열 공간, 벽 진열대, 냉장고, 업그레이드";
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

        ApplyFacilityVisibility();
        Shelf[] shelves = FindShelves();
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

        KeepStarterRamen(shelves);
    }

    void ApplyFacilityVisibility()
    {
        bool visibilityChanged = false;
        Shelf[] shelves = FindShelves();
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null)
            {
                continue;
            }

            int requiredStage = RequiredStageForShelf(shelf.name);
            if (requiredStage < 0)
            {
                continue;
            }

            bool visible = completedStage >= requiredStage;
            if (shelf.gameObject.activeSelf != visible)
            {
                shelf.gameObject.SetActive(visible);
                visibilityChanged = true;
            }
        }

        if (visibilityChanged)
            RequestLayoutNavMeshRefresh();
    }

    void RequestLayoutNavMeshRefresh()
    {
        BuildModeController[] builders = Object.FindObjectsByType<BuildModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (builders == null || builders.Length == 0 || builders[0] == null)
        {
            Debug.LogWarning("StoreProgression: BuildModeController가 없어 NavMesh를 갱신하지 못했습니다.", this);
            return;
        }

        builders[0].RequestLayoutNavMeshRefresh();
    }

    static int RequiredStageForShelf(string shelfName)
    {
        switch (shelfName)
        {
            case WaterRefrigeratorName:
            case ColaRefrigeratorName:
            case StarterShelfName:
                return 0;
            case FirstAisleShelfName:
            case SecondAisleShelfName:
                return 1;
            case NorthWestWallShelfName:
            case NorthWallShelfName:
                return 2;
            case WestWallShelfName:
            case EastWallShelfName:
            case WestCenterWallShelfName:
                return 3;
            default:
                return -1;
        }
    }

    void KeepStarterRamen(Shelf[] shelves)
    {
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null || shelf.name != StarterShelfName)
            {
                continue;
            }

            ProductDefinition product = shelf.AssignedProduct;
            if (product == null || product.ProductId != StarterRamenId)
            {
                Debug.LogWarning("StoreProgression: 시작 진열대의 기본 라면을 유지하지 못했습니다.", shelf);
                return;
            }

            if (!shelf.TryRestoreState(product, StarterRamenCount))
            {
                Debug.LogWarning("StoreProgression: 시작 진열대의 기본 라면 수량을 맞추지 못했습니다.", shelf);
            }

            return;
        }

        Debug.LogWarning("StoreProgression: 시작 진열대를 찾지 못했습니다.", this);
    }

    static Shelf[] FindShelves()
    {
        return Object.FindObjectsByType<Shelf>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
