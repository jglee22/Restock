using UnityEngine;

// 진열대 하나의 진열 수량만 관리한다.
// 창고 수량은 StoreInventory API로만 바꾸고, 상품 정의 Asset은 수정하지 않는다.
public class Shelf : MonoBehaviour
{
    [SerializeField] string saveId;
    [SerializeField] StoreInventory storeInventory;
    [SerializeField] ProductStorageType acceptedStorageType = ProductStorageType.Shelf;
    [SerializeField] ProductDefinition assignedProduct;
    [SerializeField] Transform customerStandPoint;
    [SerializeField] int currentQuantity;

    public string SaveId => saveId;

    public ProductStorageType AcceptedStorageType => acceptedStorageType;

    public ProductDefinition AssignedProduct => assignedProduct;

    public Transform CustomerStandPoint => customerStandPoint;

    public int CurrentQuantity => currentQuantity;

    public int Capacity => assignedProduct == null ? 0 : assignedProduct.MaxShelfCount;

    public bool IsEmpty => currentQuantity <= 0;

    public bool IsFull => assignedProduct != null && Capacity > 0 && currentQuantity >= Capacity;

    public bool TryAssignProduct(ProductDefinition product)
    {
        if (product == assignedProduct)
        {
            return product == null || CanDisplayProduct(product);
        }

        if (currentQuantity > 0)
        {
            return false;
        }

        if (product != null && !CanDisplayProduct(product))
        {
            return false;
        }

        assignedProduct = product;
        return true;
    }

    public bool BindInventory(StoreInventory inventory)
    {
        if (inventory == null)
        {
            Debug.LogWarning("Shelf: StoreInventory가 연결되지 않았습니다.", this);
            return false;
        }

        storeInventory = inventory;
        return true;
    }

    public bool TryRestoreState(ProductDefinition product, int quantity)
    {
        if (quantity < 0)
        {
            Debug.LogWarning($"Shelf: 복원 수량은 0 이상이어야 합니다. 현재 값: {quantity}", this);
            return false;
        }

        if (product == null)
        {
            if (quantity != 0)
            {
                Debug.LogWarning("Shelf: 진열 상품이 없으면 수량은 0이어야 합니다.", this);
                return false;
            }

            assignedProduct = null;
            currentQuantity = 0;
            return true;
        }

        if (!CanDisplayProduct(product))
        {
            Debug.LogWarning(
                $"Shelf: {product.DisplayName}의 보관 타입이 {acceptedStorageType} 진열대와 맞지 않습니다.",
                this);
            return false;
        }

        if (quantity > product.MaxShelfCount)
        {
            Debug.LogWarning(
                $"Shelf: 복원 수량 {quantity}이 최대 수량 {product.MaxShelfCount}을 넘습니다.",
                this);
            return false;
        }

        assignedProduct = product;
        currentQuantity = quantity;
        return true;
    }

    public int RestockFromInventory(int requestedAmount)
    {
        if (!CanMoveStock(requestedAmount))
        {
            return 0;
        }

        int transfer = TransferableToShelf(requestedAmount);
        if (transfer <= 0)
        {
            return 0;
        }

        if (!storeInventory.TryRemoveStock(assignedProduct, transfer))
        {
            return 0;
        }

        currentQuantity += transfer;
        return transfer;
    }

    public int ReturnToInventory(int requestedAmount)
    {
        if (!CanMoveStock(requestedAmount))
        {
            return 0;
        }

        int transfer = Mathf.Min(requestedAmount, currentQuantity);
        if (transfer <= 0)
        {
            return 0;
        }

        storeInventory.AddStock(assignedProduct, transfer);
        currentQuantity -= transfer;
        return transfer;
    }

    public bool TryTakeOne()
    {
        if (assignedProduct == null)
        {
            Debug.LogWarning("Shelf: 진열 상품이 지정되지 않았습니다.", this);
            return false;
        }

        if (currentQuantity <= 0)
        {
            return false;
        }

        currentQuantity -= 1;
        return true;
    }

    public int RestockToFull()
    {
        if (assignedProduct == null || storeInventory == null)
        {
            return RestockFromInventory(1);
        }

        int space = Mathf.Max(0, Capacity - currentQuantity);
        if (space <= 0)
        {
            return 0;
        }

        return RestockFromInventory(space);
    }

    [ContextMenu("Debug/Restock To Full")]
    void DebugRestockToFull()
    {
        if (!CanRunDebugAction())
        {
            return;
        }

        int transferred = RestockToFull();
        Debug.Log($"Shelf: 창고에서 {transferred}개를 진열했습니다. {CurrentQuantity}/{Capacity}", this);
    }

    [ContextMenu("Debug/Return All")]
    void DebugReturnAll()
    {
        if (!CanRunDebugAction())
        {
            return;
        }

        if (assignedProduct == null || storeInventory == null)
        {
            ReturnToInventory(1);
            return;
        }

        int transferred = currentQuantity > 0 ? ReturnToInventory(currentQuantity) : 0;
        Debug.Log($"Shelf: 창고로 {transferred}개를 반환했습니다. {CurrentQuantity}/{Capacity}", this);
    }

    [ContextMenu("Debug/Take One")]
    void DebugTakeOne()
    {
        if (!CanRunDebugAction())
        {
            return;
        }

        bool taken = TryTakeOne();
        Debug.Log($"Shelf: 상품 1개 제거 {(taken ? "성공" : "실패")}. {CurrentQuantity}/{Capacity}", this);
    }

    [ContextMenu("Debug/Log State")]
    void DebugLogState()
    {
        if (!CanRunDebugAction())
        {
            return;
        }

        string productName = assignedProduct == null ? "없음" : assignedProduct.DisplayName;
        int warehouseQuantity = assignedProduct != null && storeInventory != null
            ? storeInventory.GetQuantity(assignedProduct)
            : 0;
        Debug.Log(
            $"Shelf {name}: {productName} {CurrentQuantity}/{Capacity}, 창고 {warehouseQuantity}, Empty={IsEmpty}, Full={IsFull}",
            this);
    }

    void OnValidate()
    {
        if (assignedProduct != null && !CanDisplayProduct(assignedProduct))
        {
            Debug.LogWarning(
                $"Shelf: {assignedProduct.DisplayName}의 보관 타입이 {acceptedStorageType} 진열대와 맞지 않습니다.",
                this);
        }

        if (currentQuantity < 0)
        {
            Debug.LogWarning($"Shelf: 진열 수량은 0 이상이어야 합니다. 현재 값: {currentQuantity}", this);
        }

        if (currentQuantity > Capacity)
        {
            Debug.LogWarning(
                $"Shelf: 진열 수량이 최대 수량 {Capacity}을 넘습니다. 현재 값: {currentQuantity}",
                this);
        }

        if (customerStandPoint == null)
        {
            Debug.LogWarning("Shelf: CustomerStandPoint가 연결되지 않았습니다.", this);
        }
    }

    bool CanDisplayProduct(ProductDefinition product)
    {
        return product != null && product.StorageType == acceptedStorageType;
    }

    bool CanMoveStock(int requestedAmount)
    {
        if (storeInventory == null)
        {
            Debug.LogWarning("Shelf: StoreInventory가 연결되지 않았습니다.", this);
            return false;
        }

        if (assignedProduct == null)
        {
            Debug.LogWarning("Shelf: 진열 상품이 지정되지 않았습니다.", this);
            return false;
        }

        if (!CanDisplayProduct(assignedProduct))
        {
            Debug.LogWarning(
                $"Shelf: {assignedProduct.DisplayName}의 보관 타입이 {acceptedStorageType} 진열대와 맞지 않습니다.",
                this);
            return false;
        }

        if (requestedAmount <= 0)
        {
            Debug.LogWarning($"Shelf: 이동 수량은 1 이상이어야 합니다. 요청 수량: {requestedAmount}", this);
            return false;
        }

        return true;
    }

    int TransferableToShelf(int requestedAmount)
    {
        int available = storeInventory.GetQuantity(assignedProduct);
        int space = Mathf.Max(0, Capacity - currentQuantity);
        return Mathf.Min(requestedAmount, available, space);
    }

    bool CanRunDebugAction()
    {
        if (Application.isPlaying)
        {
            return true;
        }

        Debug.Log("Shelf: Play Mode에서만 실행할 수 있습니다.", this);
        return false;
    }
}
