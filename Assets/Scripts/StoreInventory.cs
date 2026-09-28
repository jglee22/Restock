using System;
using System.Collections.Generic;
using UnityEngine;

// 창고에 있는 상품 수량만 관리한다.
// ProductDefinition은 고정 정의이고, 수량은 이 컴포넌트의 Runtime 상태다.
public class StoreInventory : MonoBehaviour
{
    [Serializable]
    class InventoryEntry
    {
        [SerializeField] ProductDefinition product;
        [SerializeField] int quantity;

        public ProductDefinition Product => product;
        public int Quantity => quantity;

        public InventoryEntry(ProductDefinition productDefinition, int initialQuantity)
        {
            product = productDefinition;
            quantity = initialQuantity;
        }

        public void AddQuantity(int amount)
        {
            quantity += amount;
        }

        public void RemoveQuantity(int amount)
        {
            quantity -= amount;
        }

        public void SetQuantity(int value)
        {
            quantity = value;
        }
    }

    [SerializeField] List<InventoryEntry> entries = new List<InventoryEntry>();

    public int GetQuantity(ProductDefinition product)
    {
        if (product == null)
        {
            Debug.LogWarning("StoreInventory: 조회할 ProductDefinition이 없습니다.", this);
            return 0;
        }

        InventoryEntry entry = FindEntry(product);
        return entry == null ? 0 : entry.Quantity;
    }

    public void AddStock(ProductDefinition product, int amount)
    {
        if (!IsValidAmount(product, amount))
        {
            return;
        }

        InventoryEntry entry = FindEntry(product);
        if (entry == null)
        {
            entries.Add(new InventoryEntry(product, amount));
            return;
        }

        entry.AddQuantity(amount);
    }

    public bool TryRemoveStock(ProductDefinition product, int amount)
    {
        if (!IsValidAmount(product, amount))
        {
            return false;
        }

        InventoryEntry entry = FindEntry(product);
        if (entry == null || entry.Quantity < amount)
        {
            return false;
        }

        entry.RemoveQuantity(amount);
        return true;
    }

    public bool TryRestoreQuantity(ProductDefinition product, int quantity)
    {
        if (product == null)
        {
            Debug.LogWarning("StoreInventory: 복원할 ProductDefinition이 없습니다.", this);
            return false;
        }

        if (quantity < 0)
        {
            Debug.LogWarning(
                $"StoreInventory: 복원 수량은 0 이상이어야 합니다. 상품: {product.DisplayName}, 수량: {quantity}",
                this);
            return false;
        }

        InventoryEntry entry = FindEntry(product);
        if (entry == null)
        {
            entries.Add(new InventoryEntry(product, quantity));
            return true;
        }

        entry.SetQuantity(quantity);
        return true;
    }

    void OnValidate()
    {
        if (entries == null)
        {
            return;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            InventoryEntry entry = entries[index];
            if (entry == null || entry.Product == null)
            {
                Debug.LogWarning($"StoreInventory: {index}번 초기 재고의 ProductDefinition이 비어 있습니다.", this);
                continue;
            }

            if (entry.Quantity < 0)
            {
                Debug.LogWarning(
                    $"StoreInventory: {entry.Product.DisplayName}의 초기 수량은 0 이상이어야 합니다. 현재 값: {entry.Quantity}",
                    this);
            }

            for (int earlierIndex = 0; earlierIndex < index; earlierIndex++)
            {
                InventoryEntry earlier = entries[earlierIndex];
                if (earlier != null && earlier.Product == entry.Product)
                {
                    Debug.LogWarning($"StoreInventory: {entry.Product.DisplayName} 상품이 초기 재고에 중복되어 있습니다.", this);
                    break;
                }
            }
        }
    }

    InventoryEntry FindEntry(ProductDefinition product)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            InventoryEntry entry = entries[index];
            if (entry != null && entry.Product == product)
            {
                return entry;
            }
        }

        return null;
    }

    bool IsValidAmount(ProductDefinition product, int amount)
    {
        if (product == null)
        {
            Debug.LogWarning("StoreInventory: ProductDefinition이 없습니다.", this);
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning(
                $"StoreInventory: 수량은 1 이상이어야 합니다. 상품: {product.DisplayName}, 요청 수량: {amount}",
                this);
            return false;
        }

        return true;
    }
}
