using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] StoreProgression progression;
    [SerializeField] BuildModeController buildMode;
    [SerializeField] Button upgradeButton;
    [SerializeField] GameObject upgradePanel;
    [SerializeField] Button upgradeCloseButton;
    [SerializeField] UpgradeRowWidgets[] upgradeRows;
    [SerializeField] Button orderToggleButton;
    [SerializeField] Button priceToggleButton;
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text moneyText;
    [SerializeField] TMP_Text goalText;
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

    const float MoneyPulseSeconds = 0.16f;
    const float BannerFadeInSeconds = 0.12f;
    const float BannerHoldSeconds = 0.9f;
    const float BannerFadeOutSeconds = 0.22f;

    StorePhase displayedPhase;
    TMP_Text skipTimeLabel;
    int displayedMoney = int.MinValue;
    int displayedGoalRevenue = int.MinValue;
    int displayedGoalStage = int.MinValue;
    bool displayedGoalComplete;
    int appliedUnlockStage = int.MinValue;
    bool appliedUnlockComplete;
    TMP_Text nextDayLabel;
    string defaultNextDayLabel;
    bool suppressNextBanner;
    int moneyPulseVersion;
    int bannerVersion;
    Vector3 moneyBaseScale = Vector3.one;
    Color moneyBaseColor = Color.white;
    GameObject phaseBanner;
    CanvasGroup phaseBannerGroup;
    TMP_Text phaseBannerText;
    StorePresentationFeedback presentation;
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

        if (moneyText != null)
        {
            moneyBaseScale = moneyText.rectTransform.localScale;
            moneyBaseColor = moneyText.color;
        }

        EnsurePhaseBanner();
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
        Bind(upgradeButton, OnToggleUpgradePanel);
        Bind(upgradeCloseButton, OnCloseUpgradePanel);
        Bind(orderToggleButton, OnToggleOrderPanel);
        Bind(priceToggleButton, OnTogglePricePanel);
        BindUpgradeRows();
    }

    void Start()
    {
        EnsurePhaseBanner();
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
        Unbind(upgradeButton, OnToggleUpgradePanel);
        Unbind(upgradeCloseButton, OnCloseUpgradePanel);
        Unbind(orderToggleButton, OnToggleOrderPanel);
        Unbind(priceToggleButton, OnTogglePricePanel);
        UnbindUpgradeRows();
    }

    void Update()
    {
        if (WasEscapePressed())
        {
            if (IsWorkPanelOpen(upgradePanel))
            {
                OnCloseUpgradePanel();
            }
            else if (IsWorkPanelOpen(pricePanel))
            {
                SetWorkPanelVisible(pricePanel, false);
            }
            else if (IsWorkPanelOpen(orderPanel))
            {
                SetWorkPanelVisible(orderPanel, false);
            }
        }

        Refresh(forceVisibility: false);
    }

    void Refresh(bool forceVisibility)
    {
        if (session == null)
        {
            return;
        }

        SetText(dayText, session.Day.ToString(CultureInfo.InvariantCulture) + "일차");
        SetText(timeText, session.IsPaused ? session.TimeLabel + "  일시정지" : session.TimeLabel);
        SetText(phaseText, PhaseLabel(session.Phase));
        SetText(skipTimeLabel, session.SkipTimeLabel);
        RefreshSpeedIndicators();
        RefreshEvent();
        RefreshMoney();
        RefreshGoal();
        ApplyUnlockFilters();
        RefreshNextDayLabel();
        SetButtonVisible(
            upgradeButton,
            session.Phase == StorePhase.Preparation && (upgradeSystem == null || upgradeSystem.HasAvailablePurchase()));
        RefreshLoadButton();
        if (session.Phase != StorePhase.Preparation && IsWorkPanelOpen(upgradePanel))
        {
            SetWorkPanelVisible(upgradePanel, false);
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

        StorePhase previousPhase = displayedPhase;
        displayedPhase = session.Phase;
        if (previousPhase != displayedPhase)
        {
            if (suppressNextBanner)
            {
                suppressNextBanner = false;
                HidePhaseBanner();
            }
            else if (displayedPhase != StorePhase.Result)
            {
                PlayPhaseBanner(displayedPhase);
            }
        }
        else
        {
            suppressNextBanner = false;
        }

        SetButtonVisible(startBusinessButton, session.Phase == StorePhase.Preparation);
        SetButtonVisible(showResultButton, session.Phase == StorePhase.Closing);
        SetButtonInteractable(showResultButton, CanShowResult());
        SetObjectVisible(speedControls, session.Phase == StorePhase.Open);
        if (session.Phase == StorePhase.Result)
        {
            StorePresentationFeedback feedback = Presentation();
            if (feedback != null)
            {
                feedback.PresentResult(resultPanel);
            }
            else
            {
                SetObjectVisible(resultPanel, true);
            }
        }
        else
        {
            SetWorkPanelVisible(resultPanel, false);
        }

        if (session.Phase != StorePhase.Preparation)
        {
            SetWorkPanelVisible(orderPanel, false);
            SetWorkPanelVisible(pricePanel, false);
        }

        SetButtonVisible(orderToggleButton, session.Phase == StorePhase.Preparation);
        SetButtonVisible(priceToggleButton, session.Phase == StorePhase.Preparation);
        SetButtonVisible(restockShelvesButton, session.Phase == StorePhase.Preparation || session.Phase == StorePhase.Open);
        SetObjectVisible(saveLoadPanel, session.Phase == StorePhase.Preparation);
        SetButtonVisible(
            upgradeButton,
            session.Phase == StorePhase.Preparation && (upgradeSystem == null || upgradeSystem.HasAvailablePurchase()));
        if (session.Phase != StorePhase.Preparation)
        {
            SetWorkPanelVisible(upgradePanel, false);
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

        int nextMoney = economy.CurrentMoney;
        if (displayedMoney == nextMoney)
        {
            return;
        }

        int previousMoney = displayedMoney;
        displayedMoney = nextMoney;
        SetText(moneyText, FormatWon(displayedMoney));
        if (previousMoney != int.MinValue)
        {
            StartCoroutine(PulseMoney(nextMoney >= previousMoney));
        }
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
        if (resultText != null)
        {
            resultText.richText = true;
            resultText.textWrappingMode = TextWrappingModes.Normal;
            resultText.overflowMode = TextOverflowModes.Overflow;
        }

        var builder = new StringBuilder();
        builder.Append("<size=18><color=#C8C3B8>");
        builder.Append(session.Day.ToString(CultureInfo.InvariantCulture));
        builder.Append("일차 종료</color></size>\n\n");
        if (Progression != null)
        {
            Progression.AppendResult(builder);
        }

        builder.Append("\n\n<size=20><color=#C8C3B8>판매 통계</color></size>");
        builder.Append("\n<size=20>매출 ").Append(FormatWon(displayedRevenue));
        builder.Append(" · 지출 ").Append(FormatWon(displayedExpense));
        builder.Append(" · 순이익 ").Append(FormatSignedWon(economy.NetProfit)).Append("</size>");
        builder.Append("\n<size=20>판매 수량 ").Append(itemsSold.ToString(CultureInfo.InvariantCulture));
        builder.Append("개 · 품절 ").Append(stockoutCount.ToString(CultureInfo.InvariantCulture)).Append("회</size>");
        int shownProducts = 0;
        if (statistics != null)
        {
            for (int index = 0; index < statistics.TrackedProductCount; index++)
            {
                if (!statistics.TryGetTrackedProductSales(index, out ProductDefinition product, out int soldQuantity, out int revenue))
                {
                    continue;
                }

                if (soldQuantity == 0)
                {
                    continue;
                }

                string productName = string.IsNullOrEmpty(product.DisplayName) ? product.name : product.DisplayName;
                builder.Append("\n<size=20>");
                builder.Append(productName).Append(' ');
                builder.Append(soldQuantity.ToString(CultureInfo.InvariantCulture)).Append("개 / ");
                builder.Append(FormatWon(revenue));
                builder.Append("</size>");
                shownProducts++;
            }
        }

        if (shownProducts == 0)
        {
            builder.Append("\n<size=20>판매 상품 없음</size>");
        }

        builder.Append("\n\n<size=20><color=#C8C3B8>고객</color></size>");
        builder.Append("\n<size=20>방문 고객 ").Append(visitors.ToString(CultureInfo.InvariantCulture)).Append("명</size>");
        builder.Append("\n<size=20>구매 고객 ").Append(purchasingCustomers.ToString(CultureInfo.InvariantCulture)).Append("명</size>");
        builder.Append("\n<size=20>구매 전환율 ").Append(FormatConversionRate(purchasingCustomers, visitors)).Append("</size>");
        builder.Append("\n<size=20>평균 결제 금액 ").Append(FormatWon(averageTransaction)).Append("</size>");
        builder.Append("\n<size=20>평균 대기 시간 ").Append(FormatCheckoutWait(averageCheckoutWait)).Append("</size>");
        builder.Append("\n\n<size=20><color=#C8C3B8>이벤트</color></size>");
        builder.Append("\n<size=20>");
        builder.Append(eventSystem != null ? eventSystem.ResultLabel : "오늘의 이벤트: 없음");
        builder.Append("</size>");
        SetText(resultText, builder.ToString());
        Canvas.ForceUpdateCanvases();
        ScrollRect resultScroll = resultText.GetComponentInParent<ScrollRect>();
        if (resultScroll != null)
        {
            resultScroll.verticalNormalizedPosition = 1f;
        }
    }

    StoreProgression Progression => progression != null ? progression : StoreProgression.Instance;

    void RefreshGoal()
    {
        EnsureGoalText();
        if (goalText == null || economy == null || Progression == null)
        {
            return;
        }

        int revenue = economy.DailyRevenue;
        int stage = Progression.CompletedStage;
        bool complete = Progression.ShowsCampaignComplete;
        if (revenue == displayedGoalRevenue && stage == displayedGoalStage && complete == displayedGoalComplete)
        {
            return;
        }

        displayedGoalRevenue = revenue;
        displayedGoalStage = stage;
        displayedGoalComplete = complete;
        SetText(goalText, Progression.HudLabel(revenue));
    }

    void EnsureGoalText()
    {
        if (goalText != null || moneyText == null)
        {
            return;
        }

        GameObject copy = Instantiate(moneyText.gameObject, moneyText.transform.parent);
        copy.name = "GoalText";
        goalText = copy.GetComponent<TMP_Text>();
        RectTransform source = moneyText.rectTransform;
        RectTransform target = goalText.rectTransform;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition + new Vector2(0f, -(source.sizeDelta.y + 6f));
    }

    void ApplyUnlockFilters()
    {
        if (Progression == null)
        {
            return;
        }

        int stage = Progression.CompletedStage;
        bool complete = Progression.IsCampaignComplete;
        if (stage == appliedUnlockStage && complete == appliedUnlockComplete)
        {
            return;
        }

        appliedUnlockStage = stage;
        appliedUnlockComplete = complete;
        if (pricePanel == null)
        {
            return;
        }

        ProductPriceRow[] priceRows = pricePanel.GetComponentsInChildren<ProductPriceRow>(true);
        for (int index = 0; index < priceRows.Length; index++)
        {
            ProductPriceRow row = priceRows[index];
            if (row == null)
            {
                continue;
            }

            bool visible = Progression.IsProductUnlocked(row.Product);
            if (row.gameObject.activeSelf != visible)
            {
                row.gameObject.SetActive(visible);
            }
        }
    }

    void RefreshNextDayLabel()
    {
        if (nextDayButton == null || session == null || session.Phase != StorePhase.Result)
        {
            return;
        }

        if (nextDayLabel == null)
        {
            nextDayLabel = nextDayButton.GetComponentInChildren<TMP_Text>(true);
            if (nextDayLabel != null && string.IsNullOrEmpty(defaultNextDayLabel))
            {
                defaultNextDayLabel = nextDayLabel.text;
            }
        }

        if (nextDayLabel == null)
        {
            return;
        }

        bool continuePlay = Progression != null && Progression.ShowsCampaignComplete;
        SetText(nextDayLabel, continuePlay ? "계속 플레이" : defaultNextDayLabel);
    }

    static string PhaseLabel(StorePhase phase)
    {
        switch (phase)
        {
            case StorePhase.Preparation:
                return "준비";
            case StorePhase.Open:
                return "영업 중";
            case StorePhase.Closing:
                return "마감 중";
            default:
                return "결산";
        }
    }

    void RefreshSpeedIndicators()
    {
        if (session == null)
        {
            return;
        }

        TMP_Text pauseLabel = pauseButton != null ? pauseButton.GetComponentInChildren<TMP_Text>(true) : null;
        SetText(pauseLabel, "일시정지");
        float scale = session.PlayingTimeScale;
        SetButtonInteractable(speed1Button, !Mathf.Approximately(scale, StoreSession.NormalTimeScale));
        SetButtonInteractable(speed2Button, !Mathf.Approximately(scale, StoreSession.DoubleTimeScale));
        SetButtonInteractable(speed3Button, !Mathf.Approximately(scale, StoreSession.TripleTimeScale));
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

        if (amount > 0)
        {
            return "+" + FormatWon(amount);
        }

        return FormatWon(amount);
    }

    void OnStartBusiness()
    {
        CloseWorkPanels();
        if (buildMode != null && buildMode.IsActive)
        {
            buildMode.ExitBuildMode();
        }

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
        displayedUpgradeMoney = int.MinValue;
        upgradeRowsDirty = true;
        suppressNextBanner = true;
        moneyPulseVersion += 1;
        if (moneyText != null)
        {
            moneyText.rectTransform.localScale = moneyBaseScale;
            moneyText.color = moneyBaseColor;
        }

        StorePresentationFeedback feedback = Presentation();
        if (feedback != null)
        {
            feedback.ResetPanels();
        }
        HidePhaseBanner();
        CloseWorkPanels();
        if (buildMode != null && buildMode.IsActive)
        {
            buildMode.ExitBuildMode();
        }

        Refresh(forceVisibility: true);
    }

    void OnRestockShelves()
    {
        if (restockShelves == null)
        {
            WarnOnce("StoreHud: 진열할 Shelf가 연결되지 않았습니다.");
        }

        var restocked = new HashSet<Shelf>();
        RestockShelves(restockShelves, restocked);
        if (customerSpawner != null)
        {
            RestockShelves(customerSpawner.ShoppingShelves, restocked);
        }
    }

    void RestockShelves(IReadOnlyList<Shelf> shelves, HashSet<Shelf> restocked)
    {
        if (shelves == null)
        {
            return;
        }

        for (int index = 0; index < shelves.Count; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null || !shelf.gameObject.activeInHierarchy || shelf.AssignedProduct == null)
            {
                continue;
            }

            if (!restocked.Add(shelf))
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

    public void CloseWorkPanels()
    {
        SetWorkPanelVisible(orderPanel, false);
        SetWorkPanelVisible(pricePanel, false);
        SetWorkPanelVisible(upgradePanel, false);
    }

    void OnToggleOrderPanel()
    {
        ToggleWorkPanel(orderPanel);
    }

    void OnTogglePricePanel()
    {
        ToggleWorkPanel(pricePanel);
    }

    void OnToggleUpgradePanel()
    {
        bool opening = !IsWorkPanelOpen(upgradePanel);
        if (!ToggleWorkPanel(upgradePanel) || !opening)
        {
            return;
        }

        upgradeRowsDirty = true;
        RefreshUpgradePanel(true);
    }

    bool ToggleWorkPanel(GameObject panel)
    {
        if (session == null || session.Phase != StorePhase.Preparation || panel == null)
        {
            return false;
        }

        bool opening = !IsWorkPanelOpen(panel);
        CloseWorkPanels();
        if (buildMode != null && buildMode.IsActive)
        {
            buildMode.ExitBuildMode();
        }

        if (opening)
        {
            SetWorkPanelVisible(panel, true);
        }

        return true;
    }

    void OnCloseUpgradePanel()
    {
        SetWorkPanelVisible(upgradePanel, false);
    }

    StorePresentationFeedback Presentation()
    {
        if (presentation == null)
        {
            presentation = FindFirstObjectByType<StorePresentationFeedback>();
        }

        return presentation;
    }

    bool IsWorkPanelOpen(GameObject panel)
    {
        StorePresentationFeedback feedback = Presentation();
        if (feedback != null)
        {
            return feedback.IsOpen(panel);
        }

        return panel != null && panel.activeSelf;
    }

    void SetWorkPanelVisible(GameObject panel, bool visible)
    {
        StorePresentationFeedback feedback = Presentation();
        if (feedback == null)
        {
            SetObjectVisible(panel, visible);
            return;
        }

        if (visible)
        {
            feedback.Show(panel);
        }
        else
        {
            feedback.Hide(panel);
        }
    }

    IEnumerator PulseMoney(bool increased)
    {
        if (moneyText == null)
        {
            yield break;
        }

        int version = ++moneyPulseVersion;
        Color peak = increased
            ? Color.Lerp(moneyBaseColor, new Color(1f, 0.95f, 0.78f), 0.45f)
            : Color.Lerp(moneyBaseColor, new Color(0.78f, 0.74f, 0.68f), 0.45f);
        float elapsed = 0f;
        while (elapsed < MoneyPulseSeconds)
        {
            if (version != moneyPulseVersion)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / MoneyPulseSeconds);
            float scale = t < 0.4f
                ? Mathf.Lerp(1f, 1.06f, t / 0.4f)
                : Mathf.Lerp(1.06f, 1f, (t - 0.4f) / 0.6f);
            moneyText.rectTransform.localScale = moneyBaseScale * scale;
            moneyText.color = Color.Lerp(moneyBaseColor, peak, t < 0.4f ? t / 0.4f : (1f - t) / 0.6f);
            yield return null;
        }

        if (version == moneyPulseVersion)
        {
            moneyText.rectTransform.localScale = moneyBaseScale;
            moneyText.color = moneyBaseColor;
        }
    }

    void EnsurePhaseBanner()
    {
        if (phaseBanner != null || dayText == null)
        {
            return;
        }

        Canvas canvas = dayText.canvas;
        if (canvas == null)
        {
            return;
        }

        phaseBanner = new GameObject("PhaseBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        phaseBanner.transform.SetParent(canvas.transform, false);
        RectTransform rect = phaseBanner.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 180f);
        rect.sizeDelta = new Vector2(560f, 108f);
        Image image = phaseBanner.GetComponent<Image>();
        image.color = new Color(0.98f, 0.95f, 0.88f, 0.94f);
        image.raycastTarget = false;
        phaseBannerGroup = phaseBanner.GetComponent<CanvasGroup>();
        phaseBannerGroup.alpha = 0f;
        phaseBannerGroup.interactable = false;
        phaseBannerGroup.blocksRaycasts = false;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(phaseBanner.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 8f);
        labelRect.offsetMax = new Vector2(-16f, -8f);
        phaseBannerText = label.AddComponent<TextMeshProUGUI>();
        phaseBannerText.font = dayText.font;
        phaseBannerText.fontSharedMaterial = dayText.fontSharedMaterial;
        phaseBannerText.fontSize = 32f;
        phaseBannerText.alignment = TextAlignmentOptions.Center;
        phaseBannerText.color = new Color(0.24f, 0.18f, 0.12f, 1f);
        phaseBannerText.raycastTarget = false;
        phaseBannerText.text = string.Empty;
        phaseBanner.SetActive(false);
    }

    void PlayPhaseBanner(StorePhase phase)
    {
        EnsurePhaseBanner();
        if (phaseBanner == null || phaseBannerText == null)
        {
            return;
        }

        phaseBannerText.text = BannerText(phase);
        phaseBanner.SetActive(true);
        phaseBanner.transform.SetAsLastSibling();
        StartCoroutine(PlayBanner());
    }

    string BannerText(StorePhase phase)
    {
        if (phase == StorePhase.Open)
        {
            string eventLabel = eventSystem != null ? eventSystem.StatusLabel : string.Empty;
            return string.IsNullOrEmpty(eventLabel) ? "영업 시작" : "영업 시작\n" + eventLabel;
        }

        if (phase == StorePhase.Closing)
        {
            return "마감 중";
        }

        int day = session != null ? session.Day : 1;
        return day.ToString(CultureInfo.InvariantCulture) + "일차 준비";
    }

    void HidePhaseBanner()
    {
        bannerVersion += 1;
        if (phaseBannerGroup != null)
        {
            phaseBannerGroup.alpha = 0f;
        }

        if (phaseBanner != null)
        {
            phaseBanner.SetActive(false);
        }
    }

    IEnumerator PlayBanner()
    {
        int version = ++bannerVersion;
        phaseBannerGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < BannerFadeInSeconds)
        {
            if (version != bannerVersion)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            phaseBannerGroup.alpha = Mathf.Clamp01(elapsed / BannerFadeInSeconds);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < BannerHoldSeconds)
        {
            if (version != bannerVersion)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < BannerFadeOutSeconds)
        {
            if (version != bannerVersion)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            phaseBannerGroup.alpha = 1f - Mathf.Clamp01(elapsed / BannerFadeOutSeconds);
            yield return null;
        }

        if (version == bannerVersion && phaseBanner != null)
        {
            phaseBannerGroup.alpha = 0f;
            phaseBanner.SetActive(false);
        }
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
            int maxLevel = upgradeSystem.GetPurchaseLimit(type);
            shownUpgradeLevels[index] = level;
            SetText(row.titleText, upgradeSystem.GetDisplayName(type));
            SetText(row.levelText, "Lv " + level.ToString(CultureInfo.InvariantCulture) + " / " + maxLevel.ToString(CultureInfo.InvariantCulture));
            SetText(row.effectText, upgradeSystem.GetSummary(type));
            string currentMultiplier = FormatMultiplier(upgradeSystem.GetLevelMultiplier(type));
            bool hasNextCost = upgradeSystem.TryGetNextCost(type, out int cost);
            bool hasNextMultiplier = upgradeSystem.TryGetNextMultiplier(type, out float nextMultiplier);
            if (hasNextCost && hasNextMultiplier)
            {
                SetText(row.costText, "현재 " + currentMultiplier + " · 다음 " + FormatMultiplier(nextMultiplier) + " · " + FormatWon(cost));
            }
            else
            {
                SetText(row.costText, "현재 " + currentMultiplier + " · MAX");
            }

            TMP_Text buyLabel = row.buyButton != null ? row.buyButton.GetComponentInChildren<TMP_Text>(true) : null;
            SetText(buyLabel, hasNextCost ? "구매" : "MAX");
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

    static string FormatMultiplier(float value)
    {
        return "×" + value.ToString("0.###", CultureInfo.InvariantCulture);
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
