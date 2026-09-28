using System.Collections.Generic;
using UnityEngine;

// 하루 동안의 방문 고객과 판매 수량만 기록한다.
// 보유 자금과 매출 금액은 StoreEconomy가 가진다.
public class StoreStatistics : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] ProductDefinition[] trackedProducts;

    [SerializeField] int visitorCount;
    [SerializeField] int purchasingCustomerCount;
    [SerializeField] int itemsSold;
    [SerializeField] List<ProductDailyStatistic> productStatistics = new List<ProductDailyStatistic>();

    readonly Dictionary<ProductDefinition, ProductDailyStatistic> statisticsByProduct =
        new Dictionary<ProductDefinition, ProductDailyStatistic>();

    readonly HashSet<ProductDefinition> warnedUntrackedProducts = new HashSet<ProductDefinition>();

    public int VisitorCount => visitorCount;
    public int PurchasingCustomerCount => purchasingCustomerCount;
    public int ItemsSold => itemsSold;
    public int TrackedProductCount => trackedProducts == null ? 0 : trackedProducts.Length;

    void Awake()
    {
        ResetDailyStatistics();
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

    public void RecordCustomerVisit()
    {
        if (visitorCount == int.MaxValue)
        {
            Debug.LogWarning("StoreStatistics: 방문 고객 수가 표현 범위를 넘어 기록하지 않습니다.", this);
            return;
        }

        visitorCount += 1;
    }

    public void RecordCompletedPurchase(IReadOnlyList<CustomerBasketItem> items)
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("StoreStatistics: 구매 완료로 기록할 장바구니가 없습니다.", this);
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            CustomerBasketItem item = items[index];
            if (item.Product == null || item.UnitPrice < 0)
            {
                Debug.LogWarning("StoreStatistics: 장바구니에 기록할 수 없는 상품이 있어 구매 통계를 남기지 않습니다.", this);
                return;
            }
        }

        if (purchasingCustomerCount == int.MaxValue || itemsSold > int.MaxValue - items.Count)
        {
            Debug.LogWarning("StoreStatistics: 구매 고객 수 또는 판매 상품 수가 표현 범위를 넘어 기록하지 않습니다.", this);
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            CustomerBasketItem item = items[index];
            ProductDailyStatistic statistic = GetOrCreateStatistic(item.Product);
            if (statistic.SoldQuantity == int.MaxValue || statistic.Revenue > int.MaxValue - item.UnitPrice)
            {
                Debug.LogWarning(
                    $"StoreStatistics: {item.Product.DisplayName} 통계가 표현 범위를 넘어 구매 통계를 남기지 않습니다.",
                    this);
                return;
            }
        }

        purchasingCustomerCount += 1;
        itemsSold += items.Count;
        for (int index = 0; index < items.Count; index++)
        {
            CustomerBasketItem item = items[index];
            GetOrCreateStatistic(item.Product).AddSale(item.UnitPrice);
        }
    }

    public bool TryGetTrackedProductSales(
        int index,
        out ProductDefinition product,
        out int soldQuantity,
        out int revenue)
    {
        product = null;
        soldQuantity = 0;
        revenue = 0;
        if (trackedProducts == null || index < 0 || index >= trackedProducts.Length)
        {
            return false;
        }

        product = trackedProducts[index];
        if (product == null)
        {
            return false;
        }

        if (statisticsByProduct.TryGetValue(product, out ProductDailyStatistic statistic))
        {
            soldQuantity = statistic.SoldQuantity;
            revenue = statistic.Revenue;
        }

        return true;
    }

    void HandleDayStarted(int day)
    {
        ResetDailyStatistics();
    }

    void ResetDailyStatistics()
    {
        visitorCount = 0;
        purchasingCustomerCount = 0;
        itemsSold = 0;
        warnedUntrackedProducts.Clear();
        productStatistics.Clear();
        statisticsByProduct.Clear();
        if (trackedProducts == null)
        {
            return;
        }

        for (int index = 0; index < trackedProducts.Length; index++)
        {
            ProductDefinition product = trackedProducts[index];
            if (product == null || statisticsByProduct.ContainsKey(product))
            {
                continue;
            }

            ProductDailyStatistic statistic = new ProductDailyStatistic(product);
            productStatistics.Add(statistic);
            statisticsByProduct.Add(product, statistic);
        }
    }

    ProductDailyStatistic GetOrCreateStatistic(ProductDefinition product)
    {
        if (statisticsByProduct.TryGetValue(product, out ProductDailyStatistic statistic))
        {
            return statistic;
        }

        if (warnedUntrackedProducts.Add(product))
        {
            Debug.LogWarning(
                $"StoreStatistics: {product.DisplayName}이 추적 상품 목록에 없습니다. 판매 수량은 기록하지만 Result 순서는 추적 목록을 따릅니다.",
                this);
        }

        statistic = new ProductDailyStatistic(product);
        productStatistics.Add(statistic);
        statisticsByProduct.Add(product, statistic);
        return statistic;
    }

    void OnValidate()
    {
        if (session == null)
        {
            Debug.LogWarning("StoreStatistics: StoreSession이 연결되지 않았습니다.", this);
        }

        if (trackedProducts == null || trackedProducts.Length == 0)
        {
            Debug.LogWarning("StoreStatistics: 추적할 상품이 없습니다.", this);
            return;
        }

        for (int index = 0; index < trackedProducts.Length; index++)
        {
            ProductDefinition product = trackedProducts[index];
            if (product == null)
            {
                Debug.LogWarning($"StoreStatistics: 추적 상품 {index}가 비어 있습니다.", this);
                continue;
            }

            for (int earlier = 0; earlier < index; earlier++)
            {
                if (trackedProducts[earlier] == product)
                {
                    Debug.LogWarning($"StoreStatistics: {product.DisplayName}이 추적 상품 목록에 중복되어 있습니다.", this);
                    break;
                }
            }
        }
    }

    [System.Serializable]
    sealed class ProductDailyStatistic
    {
        [SerializeField] ProductDefinition product;
        [SerializeField] int soldQuantity;
        [SerializeField] int revenue;

        public ProductDailyStatistic(ProductDefinition source)
        {
            product = source;
        }

        public int SoldQuantity => soldQuantity;
        public int Revenue => revenue;

        public void AddSale(int unitPrice)
        {
            soldQuantity += 1;
            revenue += unitPrice;
        }
    }
}
