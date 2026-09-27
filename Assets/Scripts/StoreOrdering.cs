using UnityEngine;

// Preparation에서 상품을 창고로 주문한다.
// 진열과 매출은 바꾸지 않는다.
public class StoreOrdering : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] StoreInventory inventory;

    bool hasWarned;

    public bool CanOrder(ProductDefinition product, int quantity)
    {
        return TryValidate(product, quantity, false, out _);
    }

    public bool TryOrder(ProductDefinition product, int quantity)
    {
        if (!TryValidate(product, quantity, true, out int cost))
        {
            return false;
        }

        if (cost > 0 && !economy.TrySpend(cost))
        {
            return false;
        }

        inventory.AddStock(product, quantity);
        return true;
    }

    bool TryValidate(ProductDefinition product, int quantity, bool report, out int cost)
    {
        cost = 0;
        if (session == null || economy == null || inventory == null)
        {
            if (report)
            {
                WarnOnce("StoreOrdering: 주문에 필요한 참조가 연결되지 않았습니다.");
            }

            return false;
        }

        if (session.Phase != StorePhase.Preparation)
        {
            if (report)
            {
                WarnOnce("StoreOrdering: Preparation에서만 주문할 수 있습니다.");
            }

            return false;
        }

        if (product == null)
        {
            if (report)
            {
                Debug.LogWarning("StoreOrdering: 주문할 상품이 없습니다.", this);
            }

            return false;
        }

        if (quantity <= 0)
        {
            if (report)
            {
                Debug.LogWarning($"StoreOrdering: 주문 수량은 1 이상이어야 합니다. 요청 수량: {quantity}", this);
            }

            return false;
        }

        int price = product.PurchasePrice;
        if (price < 0)
        {
            if (report)
            {
                Debug.LogWarning(
                    $"StoreOrdering: Purchase Price가 음수라 주문하지 않습니다. 상품: {product.DisplayName}, 가격: {price}",
                    this);
            }

            return false;
        }

        if (price > 0 && quantity > int.MaxValue / price)
        {
            if (report)
            {
                Debug.LogWarning(
                    $"StoreOrdering: 주문 금액이 너무 커서 처리하지 않습니다. 상품: {product.DisplayName}, 수량: {quantity}",
                    this);
            }

            return false;
        }

        cost = price * quantity;
        return cost <= economy.CurrentMoney;
    }

    void OnValidate()
    {
        if (session == null)
        {
            Debug.LogWarning("StoreOrdering: StoreSession이 연결되지 않았습니다.", this);
        }

        if (economy == null)
        {
            Debug.LogWarning("StoreOrdering: StoreEconomy가 연결되지 않았습니다.", this);
        }

        if (inventory == null)
        {
            Debug.LogWarning("StoreOrdering: StoreInventory가 연결되지 않았습니다.", this);
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
