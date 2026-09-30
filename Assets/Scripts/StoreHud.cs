using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 기존 Day/Time TMP와 Phase 1 버튼을 StoreSession 상태에 맞춘다.
// MoneyText는 StoreEconomy의 보유 자금을 표시한다.
public class StoreHud : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] StoreStatistics statistics;
    [SerializeField] StorePersistence persistence;
    [SerializeField] CustomerSpawner customerSpawner;
    [SerializeField] StoreEventSystem eventSystem;
    [SerializeField] StoreUpgradeSystem upgradeSystem;
    [SerializeField] Button upgradeButton;
    [SerializeField] GameObject upgradePanel;
    [SerializeField] Button upgradeCloseButton;
    [SerializeField] UpgradeRowWidgets[] upgradeRows;
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text moneyText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] TMP_Text eventText;

    [SerializeField] Button startBusinessButton;
    [SerializeField] Button showResultButton;
    [SerializeField] Button nextDayButton;
    [SerializeField] Button pauseButton;
    [SerializeField] Button speed1Button;
    [SerializeField] Button speed2Button;
    [SerializeField] Button speed3Button;
    [SerializeField] Button skipTimeButton;
    [SerializeField] Button restockShelvesButton;
    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;
    [SerializeField] Shelf[] restockShelves;

    [SerializeField] GameObject speedControls;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject orderPanel;
    [SerializeField] GameObject pricePanel;
    [SerializeField] GameObject saveLoadPanel;

    StorePhase displayedPhase;
    TMP_Text skipTimeLabel;
    int displayedMoney = int.MinValue;
    int displayedRevenue = int.MinValue;
    int displayedExpense = int.MinValue;
    int displayedResultDay = int.MinValue;
    int displayedVisitors = int.MinValue;
    int displayedPurchasingCustomers = int.MinValue;
    int displayedItemsSold = int.MinValue;
    int displayedCheckoutWaitSamples = int.MinValue;
    int displayedStockoutCount = int.MinValue;
    bool hasWarned;
    bool upgradeRowsDirty = true;
    int displayedUpgradeMoney = int.MinValue;
    int[] shownUpgradeLevels = new int[3];
    UnityEngine.Events.UnityAction[] upgradeBuyActions;

    void Awake()
    {
        if (session == null)
        {
            Debug.LogError("StoreHud: StoreSession이 연결되지 않았습니다.", this);
        }

        if (skipTimeButton != null)
        {
            skipTimeLabel = skipTimeButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void OnEnable()
    {
        Bind(startBusinessButton, OnStartBusiness);
        Bind(showResultButton, OnShowResult);
        Bind(nextDayButton, OnNextDay);
        Bind(pauseButton, OnTogglePause);
        Bind(speed1Button, OnSpeed1);
        Bind(speed2Button, OnSpeed2);
        Bind(speed3Button, OnSpeed3);
        Bind(skipTimeButton, OnSkipTime);
        Bind(restockShelvesButton, OnRestockShelves);
        Bind(saveButton, OnSave);
        Bind(loadButton, OnLoad);
        Bind(upgradeButton, OnOpenUpgradePanel);
        Bind(upgradeCloseButton, OnCloseUpgradePanel);
        BindUpgradeRows();
    }

    void Start()
    {
        Refresh(forceVisibility: true);
    }

    void OnDisable()
    {
        Unbind(startBusinessButton, OnStartBusiness);
        Unbind(showResultButton, OnShowResult);
        Unbind(nextDayButton, OnNextDay);
        Unbind(pauseButton, OnTogglePause);
        Unbind(speed1Button, OnSpeed1);
        Unbind(speed2Button, OnSpeed2);
        Unbind(speed3Button, OnSpeed3);
        Unbind(skipTimeButton, OnSkipTime);
        Unbind(restockShelvesButton, OnRestockShelves);
        Unbind(saveButton, OnSave);
        Unbind(loadButton, OnLoad);
        Unbind(upgradeButton, OnOpenUpgradePanel);
        Unbind(upgradeCloseButton, OnCloseUpgradePanel);
        UnbindUpgradeRows();
    }

    void Update()
    {
        if (upgradePanel != null && upgradePanel.activeSelf && WasEscapePressed())
        {
            OnCloseUpgradePanel();
        }

        Refresh(forceVisibility: false);
    }

    void Refresh(bool forceVisibility)
    {
        if (session == null)
        {
            return;
        }

        SetText(dayText, session.DayLabel);
        SetText(timeText, session.TimeLabel);
        SetText(phaseText, session.Phase.ToString());
        SetText(skipTimeLabel, session.SkipTimeLabel);
        RefreshEvent();
        RefreshMoney();
        RefreshLoadButton();
        if (session.Phase != StorePhase.Preparation)
        {
            SetObjectVisible(upgradePanel, false);
        }

        RefreshUpgradePanel(false);

        if (session.Phase == StorePhase.Result)
        {
            RefreshResult();
        }

        if (!forceVisibility && session.Phase == displayedPhase)
        {
            if (session.Phase == StorePhase.Closing)
            {
                SetButtonInteractable(showResultButton, CanShowResult());
            }

            return;
        }

        displayedPhase = session.Phase;
        SetButtonVisible(startBusinessButton, session.Phase == StorePhase.Preparation);
        SetButtonVisible(showResultButton, session.Phase == StorePhase.Closing);
        SetButtonInteractable(showResultButton, CanShowResult());
        SetObjectVisible(speedControls, session.Phase == StorePhase.Open);
        SetObjectVisible(resultPanel, session.Phase == StorePhase.Result);
        SetObjectVisible(orderPanel, session.Phase == StorePhase.Preparation);
        SetObjectVisible(pricePanel, session.Phase == StorePhase.Preparation);
        SetButtonVisible(restockShelvesButton, session.Phase == StorePhase.Preparation || session.Phase == StorePhase.Open);
        SetObjectVisible(saveLoadPanel, session.Phase == StorePhase.Preparation);
        SetButtonVisible(upgradeButton, session.Phase == StorePhase.Preparation);
        if (session.Phase != StorePhase.Preparation)
        {
            SetObjectVisible(upgradePanel, false);
        }
    }

    void RefreshLoadButton()
    {
        bool canLoad = session != null
            && session.Phase == StorePhase.Preparation
            && persistence != null
            && persistence.SaveFileExists;
        SetButtonInteractable(loadButton, canLoad);
    }

    bool CanShowResult()
    {
        if (session == null || session.Phase != StorePhase.Closing)
        {
            return false;
        }

        if (customerSpawner == null)
        {
            WarnOnce("StoreHud: CustomerSpawner가 연결되지 않아 결산 보기를 열 수 없습니다.");
            return false;
        }

        return customerSpawner.ActiveCustomerCount <= 0;
    }

    void RefreshEvent()
    {
        if (eventSystem == null)
        {
            WarnOnce("StoreHud: StoreEventSystem이 연결되지 않아 이벤트를 표시할 수 없습니다.");
            return;
        }

        if (eventText == null)
        {
            WarnOnce("StoreHud: 이벤트 텍스트가 연결되지 않았습니다.");
            return;
        }

        SetText(eventText, eventSystem.StatusLabel);
    }

    void RefreshMoney()
    {
        if (economy == null)
        {
            WarnOnce("StoreHud: StoreEconomy가 연결되지 않아 보유 자금을 표시할 수 없습니다.");
            return;
        }

        if (displayedMoney == economy.CurrentMoney)
        {
            return;
        }

        displayedMoney = economy.CurrentMoney;
        SetText(moneyText, FormatWon(displayedMoney));
    }

    void RefreshResult()
    {
        if (economy == null)
        {
            WarnOnce("StoreHud: StoreEconomy가 연결되지 않아 오늘 매출을 표시할 수 없습니다.");
            return;
        }

        int visitors = statistics != null ? statistics.VisitorCount : 0;
        int purchasingCustomers = statistics != null ? statistics.PurchasingCustomerCount : 0;
        int itemsSold = statistics != null ? statistics.ItemsSold : 0;
        int checkoutWaitSamples = statistics != null ? statistics.CheckoutWaitSampleCount : 0;
        int stockoutCount = statistics != null ? statistics.StockoutCount : 0;
        if (statistics == null)
        {
            WarnOnce("StoreHud: StoreStatistics가 연결되지 않아 판매 통계를 표시할 수 없습니다.");
        }

        if (displayedResultDay == session.Day
            && displayedRevenue == economy.DailyRevenue
            && displayedExpense == economy.DailyExpense
            && displayedVisitors == visitors
            && displayedPurchasingCustomers == purchasingCustomers
            && displayedItemsSold == itemsSold
            && displayedCheckoutWaitSamples == checkoutWaitSamples
            && displayedStockoutCount == stockoutCount)
        {
            return;
        }

        displayedResultDay = session.Day;
        displayedRevenue = economy.DailyRevenue;
        displayedExpense = economy.DailyExpense;
        displayedVisitors = visitors;
        displayedPurchasingCustomers = purchasingCustomers;
        displayedItemsSold = itemsSold;
        displayedCheckoutWaitSamples = checkoutWaitSamples;
        displayedStockoutCount = stockoutCount;
        int averageTransaction = AverageTransactionValue(displayedRevenue, purchasingCustomers);
        float averageCheckoutWait = statistics != null ? statistics.AverageCheckoutWaitSeconds : 0f;
        var builder = new StringBuilder();
        builder.Append(session.DayLabel).Append(" 종료\n");
        if (eventSystem != null)
        {
            builder.Append(eventSystem.ResultLabel).Append('\n');
        }
        builder.Append("오늘 매출 ").Append(FormatWon(displayedRevenue)).Append('\n');
        builder.Append("매입 비용 ").Append(FormatWon(displayedExpense)).Append('\n');
        builder.Append("순이익 ").Append(FormatSignedWon(economy.NetProfit)).Append("\n\n");
        builder.Append("방문 고객 ").Append(visitors.ToString(CultureInfo.InvariantCulture)).Append("명\n");
        builder.Append("구매 고객 ").Append(purchasingCustomers.ToString(CultureInfo.InvariantCulture)).Append("명\n");
        builder.Append("판매 상품 ").Append(itemsSold.ToString(CultureInfo.InvariantCulture)).Append("개\n");
        builder.Append("품절 횟수 ").Append(stockoutCount.ToString(CultureInfo.InvariantCulture)).Append("회\n");
        builder.Append("평균 객단가 ").Append(FormatWon(averageTransaction)).Append('\n');
        builder.Append("평균 계산 대기 ").Append(FormatCheckoutWait(averageCheckoutWait)).Append('\n');
        builder.Append("구매 전환율 ").Append(FormatConversionRate(purchasingCustomers, visitors)).Append("\n\n");
        builder.Append("상품별 판매");
        if (statistics != null)
        {
            for (int index = 0; index < statistics.TrackedProductCount; index++)
            {
                if (!statistics.TryGetTrackedProductSales(index, out ProductDefinition product, out int soldQuantity, out int revenue))
                {
                    continue;
                }

                string productName = string.IsNullOrEmpty(product.DisplayName) ? product.name : product.DisplayName;
                builder.Append('\n');
                builder.Append(productName).Append(' ');
                builder.Append(soldQuantity.ToString(CultureInfo.InvariantCulture)).Append("개 / ");
                builder.Append(FormatWon(revenue));
            }
        }

        SetText(resultText, builder.ToString());
    }

    static int AverageTransactionValue(int dailyRevenue, int purchasingCustomers)
    {
        if (purchasingCustomers <= 0)
        {
            return 0;
        }

        return (int)System.Math.Round(
            dailyRevenue / (double)purchasingCustomers,
            System.MidpointRounding.AwayFromZero);
    }

    static string FormatConversionRate(int purchasingCustomers, int visitors)
    {
        if (visitors <= 0)
        {
            return "0%";
        }

        long purchasing = purchasingCustomers;
        long visitorCount = visitors;
        if (purchasing * 100 % visitorCount == 0)
        {
            return (purchasing * 100 / visitorCount).ToString(CultureInfo.InvariantCulture) + "%";
        }

        double percent = System.Math.Round(
            purchasing * 100d / visitorCount,
            1,
            System.MidpointRounding.AwayFromZero);
        return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    static string FormatCheckoutWait(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
        {
            return "0초";
        }

        long tenths = (long)System.Math.Round(seconds * 10d, System.MidpointRounding.AwayFromZero);
        if (tenths % 10 == 0)
        {
            return (tenths / 10).ToString(CultureInfo.InvariantCulture) + "초";
        }

        return (tenths / 10d).ToString("0.0", CultureInfo.InvariantCulture) + "초";
    }

    static string FormatWon(int amount)
    {
        return "₩" + amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    static string FormatSignedWon(int amount)
    {
        if (amount < 0)
        {
            return "-" + FormatWon(-amount);
        }

        return FormatWon(amount);
    }

    void OnStartBusiness()
    {
        session.StartBusiness();
        Refresh(forceVisibility: true);
    }

    void OnShowResult()
    {
        if (!CanShowResult())
        {
            return;
        }

        session.ShowResult();
        Refresh(forceVisibility: true);
    }

    void OnNextDay()
    {
        session.AdvanceToNextDay();
        Refresh(forceVisibility: true);
    }

    void OnTogglePause()
    {
        session.TogglePause();
    }

    void OnSpeed1()
    {
        session.SetSpeed(StoreSession.NormalTimeScale);
    }

    void OnSpeed2()
    {
        session.SetSpeed(StoreSession.DoubleTimeScale);
    }

    void OnSpeed3()
    {
        session.SetSpeed(StoreSession.TripleTimeScale);
    }

    void OnSkipTime()
    {
        session.JumpToSkipTime();
        Refresh(forceVisibility: true);
    }

    void OnSave()
    {
        if (persistence == null)
        {
            WarnOnce("StoreHud: StorePersistence가 연결되지 않아 저장할 수 없습니다.");
            return;
        }

        if (persistence.TrySave())
        {
            RefreshLoadButton();
        }
    }

    void OnLoad()
    {
        if (persistence == null)
        {
            WarnOnce("StoreHud: StorePersistence가 연결되지 않아 불러올 수 없습니다.");
            return;
        }

        if (!persistence.TryLoad())
        {
            return;
        }

        displayedMoney = int.MinValue;
        displayedResultDay = int.MinValue;
        Refresh(forceVisibility: true);
    }

    void OnRestockShelves()
    {
        if (restockShelves == null)
        {
            WarnOnce("StoreHud: 진열할 Shelf가 연결되지 않았습니다.");
            return;
        }

        for (int index = 0; index < restockShelves.Length; index++)
        {
            Shelf shelf = restockShelves[index];
            if (shelf == null || shelf.AssignedProduct == null)
            {
                continue;
            }

            int transferred = shelf.RestockToFull();
            Debug.Log($"Shelf: 창고에서 {transferred}개를 진열했습니다. {shelf.CurrentQuantity}/{shelf.Capacity}", shelf);
        }
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    void OnValidate()
    {
        if (customerSpawner == null)
        {
            Debug.LogWarning("StoreHud: CustomerSpawner가 연결되지 않았습니다.", this);
        }

        if (economy == null)
        {
            Debug.LogWarning("StoreHud: StoreEconomy가 연결되지 않았습니다.", this);
        }

        if (statistics == null)
        {
            Debug.LogWarning("StoreHud: StoreStatistics가 연결되지 않았습니다.", this);
        }

        if (orderPanel == null)
        {
            Debug.LogWarning("StoreHud: OrderPanel이 연결되지 않았습니다.", this);
        }

        if (pricePanel == null)
        {
            Debug.LogWarning("StoreHud: PricePanel이 연결되지 않았습니다.", this);
        }

        if (restockShelvesButton == null)
        {
            Debug.LogWarning("StoreHud: 진열 채우기 버튼이 연결되지 않았습니다.", this);
        }

        if (persistence == null)
        {
            Debug.LogWarning("StoreHud: StorePersistence가 연결되지 않았습니다.", this);
        }

        if (saveLoadPanel == null)
        {
            Debug.LogWarning("StoreHud: SaveLoadPanel이 연결되지 않았습니다.", this);
        }

        if (saveButton == null || loadButton == null)
        {
            Debug.LogWarning("StoreHud: 저장 또는 불러오기 버튼이 연결되지 않았습니다.", this);
        }
    }

    void OnOpenUpgradePanel()
    {
        if (session == null || session.Phase != StorePhase.Preparation || upgradePanel == null)
        {
            return;
        }

        upgradeRowsDirty = true;
        upgradePanel.SetActive(true);
        RefreshUpgradePanel(true);
    }

    void OnCloseUpgradePanel()
    {
        SetObjectVisible(upgradePanel, false);
    }

    void OnBuyUpgrade(int rowIndex)
    {
        if (upgradeSystem == null || upgradeRows == null || rowIndex < 0 || rowIndex >= upgradeRows.Length)
        {
            return;
        }

        upgradeSystem.TryPurchase(upgradeRows[rowIndex].type);
        upgradeRowsDirty = true;
        RefreshUpgradePanel(true);
    }

    void BindUpgradeRows()
    {
        if (upgradeRows == null)
        {
            return;
        }

        upgradeBuyActions = new UnityEngine.Events.UnityAction[upgradeRows.Length];
        for (int index = 0; index < upgradeRows.Length; index++)
        {
            int captured = index;
            upgradeBuyActions[index] = () => OnBuyUpgrade(captured);
            Bind(upgradeRows[index].buyButton, upgradeBuyActions[index]);
        }
    }

    void UnbindUpgradeRows()
    {
        if (upgradeRows == null || upgradeBuyActions == null)
        {
            return;
        }

        int count = upgradeRows.Length < upgradeBuyActions.Length ? upgradeRows.Length : upgradeBuyActions.Length;
        for (int index = 0; index < count; index++)
        {
            Unbind(upgradeRows[index].buyButton, upgradeBuyActions[index]);
        }
    }

    void RefreshUpgradePanel(bool force)
    {
        if (upgradePanel == null || !upgradePanel.activeSelf || upgradeSystem == null || upgradeRows == null)
        {
            return;
        }

        int money = economy != null ? economy.CurrentMoney : int.MinValue;
        if (!force && !upgradeRowsDirty && money == displayedUpgradeMoney)
        {
            bool sameLevels = true;
            for (int index = 0; index < upgradeRows.Length; index++)
            {
                if (upgradeSystem.GetLevel(upgradeRows[index].type) != LastShownLevel(index))
                {
                    sameLevels = false;
                    break;
                }
            }

            if (sameLevels)
            {
                return;
            }
        }

        displayedUpgradeMoney = money;
        upgradeRowsDirty = false;
        if (shownUpgradeLevels == null || shownUpgradeLevels.Length != upgradeRows.Length)
        {
            shownUpgradeLevels = new int[upgradeRows.Length];
        }

        for (int index = 0; index < upgradeRows.Length; index++)
        {
            UpgradeRowWidgets row = upgradeRows[index];
            StoreUpgradeType type = row.type;
            int level = upgradeSystem.GetLevel(type);
            int maxLevel = upgradeSystem.GetMaxLevel(type);
            shownUpgradeLevels[index] = level;
            SetText(row.titleText, upgradeSystem.GetDisplayName(type));
            SetText(row.levelText, "Lv " + level.ToString(CultureInfo.InvariantCulture) + " / " + maxLevel.ToString(CultureInfo.InvariantCulture));
            SetText(row.effectText, UpgradeEffectLabel(type));
            if (upgradeSystem.TryGetNextCost(type, out int cost))
            {
                SetText(row.costText, "다음 비용: " + FormatWon(cost));
            }
            else
            {
                SetText(row.costText, "다음 비용: MAX");
            }

            SetButtonInteractable(row.buyButton, upgradeSystem.CanPurchase(type));
        }
    }

    int LastShownLevel(int index)
    {
        if (shownUpgradeLevels == null || index < 0 || index >= shownUpgradeLevels.Length)
        {
            return int.MinValue;
        }

        return shownUpgradeLevels[index];
    }

    static string UpgradeEffectLabel(StoreUpgradeType type)
    {
        switch (type)
        {
            case StoreUpgradeType.FastCheckout:
                return "계산 시간 -10% / Lv";
            case StoreUpgradeType.Advertising:
                return "구매 확률 +10% / Lv";
            default:
                return "고객 방문 간격 -10% / Lv";
        }
    }

    static bool WasEscapePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
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

    static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null && button.interactable != interactable)
        {
            button.interactable = interactable;
        }
    }

    static void SetObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    [System.Serializable]
    public class UpgradeRowWidgets
    {
        public StoreUpgradeType type;
        public TMP_Text titleText;
        public TMP_Text levelText;
        public TMP_Text effectText;
        public TMP_Text costText;
        public Button buyButton;
    }
}
