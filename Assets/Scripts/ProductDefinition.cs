using UnityEngine;

public enum ProductCategory
{
    Drink,
    Food,
    Snack
}

public enum ProductStorageType
{
    Shelf,
    Refrigerated
}

// 상품의 고정 정의만 담는다. 재고, 현재 판매가, 판매량은 포함하지 않는다.
[CreateAssetMenu(fileName = "ProductDefinition", menuName = "Restock/Product Definition")]
public class ProductDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] string productId;
    [SerializeField] string displayName;

    [Header("Classification")]
    [SerializeField] ProductCategory category;
    [SerializeField] ProductStorageType storageType;

    [Header("Economy")]
    [SerializeField] int purchasePrice;
    [SerializeField] int baseSellPrice;

    [Header("Store")]
    [SerializeField] int maxShelfCount = 1;
    [SerializeField, Range(0f, 1f)] float popularity;
    [SerializeField] int unlockStage;

    [Header("Presentation")]
    [SerializeField] Sprite icon;
    [SerializeField] GameObject productPrefab;

    public string ProductId => productId;
    public string DisplayName => displayName;
    public ProductCategory Category => category;
    public ProductStorageType StorageType => storageType;
    public int PurchasePrice => purchasePrice;
    public int BaseSellPrice => baseSellPrice;
    public int MaxShelfCount => maxShelfCount;
    public float Popularity => popularity;
    public int UnlockStage => unlockStage;
    public Sprite Icon => icon;
    public GameObject ProductPrefab => productPrefab;

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            Debug.LogWarning("ProductDefinition: Product Id가 비어 있습니다.", this);
        }

        if (purchasePrice < 0)
        {
            Debug.LogWarning($"ProductDefinition: Purchase Price는 0 이상이어야 합니다. 현재 값: {purchasePrice}", this);
        }

        if (baseSellPrice < 0)
        {
            Debug.LogWarning($"ProductDefinition: Base Sell Price는 0 이상이어야 합니다. 현재 값: {baseSellPrice}", this);
        }

        if (maxShelfCount < 1)
        {
            Debug.LogWarning($"ProductDefinition: Max Shelf Count는 1 이상이어야 합니다. 현재 값: {maxShelfCount}", this);
        }

        if (popularity < 0f || popularity > 1f)
        {
            Debug.LogWarning($"ProductDefinition: Popularity는 0 이상 1 이하여야 합니다. 현재 값: {popularity}", this);
        }

        if (unlockStage < 0)
        {
            unlockStage = 0;
        }
    }
}
