using System.Collections.Generic;
using UnityEngine;

// 플레이 중 판매 가격만 보관한다.
// ProductDefinition의 Base Sell Price는 바꾸지 않고, 매출과 재고도 다루지 않는다.
public class StorePricing : MonoBehaviour
{
    [SerializeField] StoreSession session;
    [SerializeField] ProductDefinition[] products;

    readonly Dictionary<ProductDefinition, int> currentPrices = new Dictionary<ProductDefinition, int>();
    bool hasWarned;

    void Awake()
    {
        currentPrices.Clear();
        if (products == null)
        {
            return;
        }

        for (int index = 0; index < products.Length; index++)
        {
            ProductDefinition product = products[index];
            if (product == null)
            {
                Debug.LogWarning("StorePricing: 가격 목록에 비어 있는 상품이 있습니다.", this);
                continue;
            }

            if (currentPrices.ContainsKey(product))
            {
                Debug.LogWarning($"StorePricing: {product.DisplayName}이 중복 등록되어 첫 가격만 사용합니다.", this);
                continue;
            }

            if (product.BaseSellPrice < 0)
            {
                Debug.LogWarning(
                    $"StorePricing: {product.DisplayName}의 Base Sell Price가 음수라 현재 가격을 만들지 않습니다.",
                    this);
                continue;
            }

            currentPrices.Add(product, product.BaseSellPrice);
        }
    }

    public bool TryGetCurrentPrice(ProductDefinition product, out int price)
    {
        if (product != null && currentPrices.TryGetValue(product, out price))
        {
            return true;
        }

        price = 0;
        return false;
    }

    public bool TrySetPrice(ProductDefinition product, int price)
    {
        if (!CanChangePrice(product))
        {
            return false;
        }

        if (price < 0)
        {
            return false;
        }

        currentPrices[product] = price;
        return true;
    }

    public bool TryAdjustPrice(ProductDefinition product, int delta)
    {
        if (!TryGetCurrentPrice(product, out int current))
        {
            WarnOnce("StorePricing: 등록되지 않은 상품의 가격은 바꿀 수 없습니다.");
            return false;
        }

        long next = (long)current + delta;
        if (next < 0 || next > int.MaxValue)
        {
            return false;
        }

        return TrySetPrice(product, (int)next);
    }

    bool CanChangePrice(ProductDefinition product)
    {
        if (session == null)
        {
            WarnOnce("StorePricing: StoreSession이 연결되지 않아 가격을 바꿀 수 없습니다.");
            return false;
        }

        if (session.Phase != StorePhase.Preparation)
        {
            return false;
        }

        if (product == null || !currentPrices.ContainsKey(product))
        {
            WarnOnce("StorePricing: 등록되지 않은 상품의 가격은 바꿀 수 없습니다.");
            return false;
        }

        return true;
    }

    void OnValidate()
    {
        if (session == null)
        {
            Debug.LogWarning("StorePricing: StoreSession이 연결되지 않았습니다.", this);
        }

        if (products == null || products.Length == 0)
        {
            Debug.LogWarning("StorePricing: 가격을 관리할 상품이 없습니다.", this);
            return;
        }

        for (int index = 0; index < products.Length; index++)
        {
            if (products[index] == null)
            {
                Debug.LogWarning($"StorePricing: 상품 {index}가 비어 있습니다.", this);
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
