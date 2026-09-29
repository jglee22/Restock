using UnityEngine;

// 고객 한 종류의 고정 성향만 담는다. 남은 예산과 바구니는 방문마다 CustomerMover가 가진다.
[CreateAssetMenu(fileName = "CustomerDefinition", menuName = "Restock/Customer Definition")]
public class CustomerDefinition : ScriptableObject
{
    [SerializeField] string customerId;
    [SerializeField] string displayName;
    [SerializeField] int budget;
    [SerializeField] float priceSensitivity = 1f;
    [SerializeField] int minTargetItems = 1;
    [SerializeField] int maxTargetItems = 1;
    [SerializeField] float browseTimeModifier = 1f;

    public string CustomerId => customerId;
    public string DisplayName => displayName;
    public int Budget => budget;
    public float PriceSensitivity => priceSensitivity;
    public int MinTargetItems => minTargetItems;
    public int MaxTargetItems => maxTargetItems;
    public float BrowseTimeModifier => browseTimeModifier;

    public bool CanSpawn()
    {
        return !string.IsNullOrWhiteSpace(customerId)
            && budget >= 0
            && priceSensitivity > 0f
            && minTargetItems >= 1
            && maxTargetItems >= minTargetItems
            && browseTimeModifier > 0f;
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            Debug.LogWarning("CustomerDefinition: Customer Id가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogWarning("CustomerDefinition: Display Name이 비어 있습니다.", this);
        }

        if (budget < 0)
        {
            Debug.LogWarning($"CustomerDefinition: Budget은 0 이상이어야 합니다. 현재 값: {budget}", this);
        }

        if (priceSensitivity <= 0f)
        {
            Debug.LogWarning($"CustomerDefinition: Price Sensitivity는 0보다 커야 합니다. 현재 값: {priceSensitivity}", this);
        }

        if (minTargetItems < 1)
        {
            Debug.LogWarning($"CustomerDefinition: Min Target Items는 1 이상이어야 합니다. 현재 값: {minTargetItems}", this);
        }

        if (maxTargetItems < minTargetItems)
        {
            Debug.LogWarning($"CustomerDefinition: Max Target Items는 Min Target Items 이상이어야 합니다. 현재 값: {maxTargetItems}", this);
        }

        if (browseTimeModifier <= 0f)
        {
            Debug.LogWarning($"CustomerDefinition: Browse Time Modifier는 0보다 커야 합니다. 현재 값: {browseTimeModifier}", this);
        }
    }
}
