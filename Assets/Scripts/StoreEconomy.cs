using UnityEngine;

// 편의점 하나의 보유 자금, 당일 매출, 당일 매입 비용을 관리한다.
// 재고와 줄, 시간은 바꾸지 않는다.
public class StoreEconomy : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] int startingMoney = 500000;

    int currentMoney;
    int dailyRevenue;
    int dailyExpense;

    public int CurrentMoney => currentMoney;
    public int DailyRevenue => dailyRevenue;
    public int DailyExpense => dailyExpense;
    public int NetProfit => dailyRevenue - dailyExpense;

    void Awake()
    {
        currentMoney = startingMoney;
        dailyRevenue = 0;
        dailyExpense = 0;
    }

    void OnEnable()
    {
        if (session == null)
        {
            return;
        }

        session.DayStarted += HandleDayStarted;
    }

    void OnDisable()
    {
        if (session == null)
        {
            return;
        }

        session.DayStarted -= HandleDayStarted;
    }

    public void RecordSale(ProductDefinition product, int salePrice)
    {
        if (product == null)
        {
            Debug.LogWarning("StoreEconomy: 판매할 상품이 없습니다.", this);
            return;
        }

        if (salePrice < 0)
        {
            Debug.LogWarning(
                $"StoreEconomy: 판매 가격이 음수라 판매하지 않습니다. 상품: {product.DisplayName}, 가격: {salePrice}",
                this);
            return;
        }

        if (salePrice > 0 && (currentMoney > int.MaxValue - salePrice || dailyRevenue > int.MaxValue - salePrice))
        {
            Debug.LogWarning(
                $"StoreEconomy: 판매 금액을 더하면 보유 자금 또는 매출이 표현 범위를 넘습니다. 상품: {product.DisplayName}, 가격: {salePrice}",
                this);
            return;
        }

        currentMoney += salePrice;
        dailyRevenue += salePrice;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"StoreEconomy: 지출 금액은 1 이상이어야 합니다. 요청 금액: {amount}", this);
            return false;
        }

        if (currentMoney < amount)
        {
            return false;
        }

        currentMoney -= amount;
        dailyExpense += amount;
        return true;
    }

    public bool TryRestoreState(int money, int revenue, int expense)
    {
        if (money < 0 || revenue < 0 || expense < 0)
        {
            Debug.LogWarning(
                $"StoreEconomy: 복원 값은 0 이상이어야 합니다. 자금: {money}, 매출: {revenue}, 매입: {expense}",
                this);
            return false;
        }

        currentMoney = money;
        dailyRevenue = revenue;
        dailyExpense = expense;
        return true;
    }

    void HandleDayStarted(int day)
    {
        dailyRevenue = 0;
        dailyExpense = 0;
    }

    void OnValidate()
    {
        if (startingMoney < 0)
        {
            Debug.LogWarning($"StoreEconomy: Starting Money는 0 이상이어야 합니다. 현재 값: {startingMoney}", this);
        }

        if (session == null)
        {
            Debug.LogWarning("StoreEconomy: StoreSession이 연결되지 않았습니다.", this);
        }
    }
}
