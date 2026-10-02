using UnityEngine;

public enum FacilityType
{
    Shelf,
    Refrigerator,
    Checkout
}

// 시설의 고정 정의만 담는다. 위치와 회전, 진열 수량은 배치된 인스턴스가 가진다.
// 상품 보관 타입은 Prefab의 Shelf가 계속 판단한다.
[CreateAssetMenu(fileName = "FacilityDefinition", menuName = "Restock/Facility Definition")]
public class FacilityDefinition : ScriptableObject
{
    [SerializeField] string facilityId;
    [SerializeField] string displayName;
    [SerializeField] FacilityType facilityType;
    [SerializeField] int cost;
    [SerializeField] Vector2Int gridSize = new Vector2Int(1, 1);
    [SerializeField] GameObject prefab;
    [SerializeField] bool requiresWall;

    public string FacilityId => facilityId;
    public string DisplayName => displayName;
    public FacilityType FacilityType => facilityType;
    public int Cost => cost;
    public Vector2Int GridSize => gridSize;
    public GameObject Prefab => prefab;
    public bool RequiresWall => requiresWall;

    public bool TryGetAcceptedStorageType(out ProductStorageType storageType)
    {
        storageType = default;
        Shelf shelf = prefab != null ? prefab.GetComponent<Shelf>() : null;
        if (shelf == null)
        {
            return false;
        }

        storageType = shelf.AcceptedStorageType;
        return true;
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            Debug.LogWarning("FacilityDefinition: Facility Id가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogWarning("FacilityDefinition: Display Name이 비어 있습니다.", this);
        }

        if (cost < 0)
        {
            Debug.LogWarning($"FacilityDefinition: Cost는 0 이상이어야 합니다. 현재 값: {cost}", this);
        }

        if (gridSize.x <= 0 || gridSize.y <= 0)
        {
            Debug.LogWarning($"FacilityDefinition: Grid Size는 각 축이 1 이상이어야 합니다. 현재 값: {gridSize}", this);
        }

        if (prefab == null)
        {
            Debug.LogWarning("FacilityDefinition: Prefab이 연결되지 않았습니다.", this);
            return;
        }

        if (facilityType == FacilityType.Checkout)
        {
            if (prefab.GetComponent<CheckoutCounter>() == null)
            {
                Debug.LogWarning("FacilityDefinition: 계산대 Prefab에 CheckoutCounter가 없습니다.", this);
            }

            return;
        }

        Shelf shelf = prefab.GetComponent<Shelf>();
        if (shelf == null)
        {
            Debug.LogWarning("FacilityDefinition: Prefab에 Shelf가 없습니다.", this);
            return;
        }

        ProductStorageType expected = facilityType == FacilityType.Refrigerator
            ? ProductStorageType.Refrigerated
            : ProductStorageType.Shelf;
        if (shelf.AcceptedStorageType != expected)
        {
            Debug.LogWarning(
                $"FacilityDefinition: Prefab의 Accepted Storage Type이 {expected}가 아닙니다. 현재 값: {shelf.AcceptedStorageType}",
                this);
        }
    }
}
