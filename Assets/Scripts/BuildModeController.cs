using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BuildModeController : MonoBehaviour
{
    public enum BuildToolMode
    {
        None,
        Placement,
        MoveSelect,
        Moving,
        DeleteSelect,
        ProductSelect
    }

    const float DefaultCellSize = 1f;
    const float OverlapPadding = 0.02f;
    const float OverlapHalfHeight = 1.1f;
    const float OverlapCenterHeight = 1.2f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    static readonly Color ValidTint = new Color(0.25f, 0.85f, 0.3f, 1f);
    static readonly Color InvalidTint = new Color(0.9f, 0.22f, 0.18f, 1f);

    [SerializeField] float cellSize = DefaultCellSize;
    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] StoreInventory storeInventory;
    [SerializeField] StoreStatistics storeStatistics;
    [SerializeField] CustomerSpawner customerSpawner;
    [SerializeField] NavMeshSurface navMeshSurface;
    [SerializeField] Camera viewCamera;
    [SerializeField] Collider buildSurface;
    [SerializeField] FacilityDefinition[] facilities;
    [SerializeField] Button buildButton;
    [SerializeField] GameObject buildPanel;
    [SerializeField] Button shelfButton;
    [SerializeField] Button refrigeratorButton;
    [SerializeField] Button checkoutButton;
    [SerializeField] Button moveButton;
    [SerializeField] Button deleteButton;
    [SerializeField] Button productButton;
    [SerializeField] GameObject productAssignmentPanel;
    [SerializeField] TMP_Text productStatusText;
    [SerializeField] TMP_Dropdown productDropdown;
    [SerializeField] Button applyProductButton;
    [SerializeField] Button clearProductButton;
    [SerializeField] Button closeProductButton;
    [SerializeField] Button exitButton;

    readonly Collider[] overlapHits = new Collider[32];
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    MaterialPropertyBlock tintBlock;

    Transform placedRoot;
    readonly List<PlacedFacility> placedFacilities = new List<PlacedFacility>();
    readonly List<Vector3> accessScratch = new List<Vector3>(8);
    readonly List<ProductDefinition> assignmentProducts = new List<ProductDefinition>();
    Shelf assignmentShelf;
    float cachedAgentRadius = -1f;
    int cachedAgentTypeId = -1;
    NavMeshQueryFilter walkableQuery;
    bool walkableQueryReady;
    Bounds originalBodyBounds;
    bool hasOriginalBodyBounds;
    readonly List<Bounds> dynamicBodyScratch = new List<Bounds>(8);
    bool warnedAccessSetup;
    GameObject preview;
    UnityEngine.Events.UnityAction selectShelf;
    UnityEngine.Events.UnityAction selectRefrigerator;
    UnityEngine.Events.UnityAction selectCheckout;
    UnityEngine.Events.UnityAction selectMove;
    UnityEngine.Events.UnityAction selectDelete;
    UnityEngine.Events.UnityAction selectProduct;
    UnityEngine.Events.UnityAction applyProduct;
    UnityEngine.Events.UnityAction clearProduct;
    UnityEngine.Events.UnityAction closeProduct;
    Renderer[] previewRenderers;
    FacilityDefinition selected;
    Vector2Int currentOrigin;
    Quaternion previewBaseRotation = Quaternion.identity;
    int rotationQuarterTurns;
    BuildToolMode toolMode;
    PlacedFacility movingFacility;
    Vector2Int originalOrigin;
    int originalQuarterTurns;
    Vector3 originalPosition;
    Quaternion originalRotation;
    bool buildModeActive;
    bool previewValid;
    bool hasGrid;
    bool navMeshRefreshPending;
    float originX;
    float originZ;
    float floorTop;
    int gridWidth;
    int gridDepth;

    public bool IsActive => buildModeActive;
    public FacilityDefinition SelectedFacility => selected;
    public bool HasPreview => preview != null;
    public bool IsCurrentPlacementValid => preview != null && previewValid;
    public Vector2Int CurrentGridOrigin => currentOrigin;
    public int RotationQuarterTurns => rotationQuarterTurns;
    public Vector2Int CurrentFootprintSize => selected == null
        ? Vector2Int.zero
        : RotatedGridSize(selected.GridSize, rotationQuarterTurns);
    public float CellSize => cellSize;
    public int GridWidth => gridWidth;
    public int GridDepth => gridDepth;
    public BuildToolMode ToolMode => toolMode;

    void Awake()
    {
        tintBlock = new MaterialPropertyBlock();
        placedRoot = new GameObject("PlacedFacilities").transform;
        CacheGrid();
        EnsureWalkableQuery();
        if (buildPanel != null)
            buildPanel.SetActive(false);
        if (productAssignmentPanel != null)
            productAssignmentPanel.SetActive(false);
    }

    void OnEnable()
    {
        selectShelf = () => SelectFacility(0);
        selectRefrigerator = () => SelectFacility(1);
        selectCheckout = () => SelectFacility(2);

        if (buildButton != null)
            buildButton.onClick.AddListener(EnterBuildMode);
        if (shelfButton != null)
            shelfButton.onClick.AddListener(selectShelf);
        if (refrigeratorButton != null)
            refrigeratorButton.onClick.AddListener(selectRefrigerator);
        selectMove = BeginMoveSelect;
        selectDelete = BeginDeleteSelect;
        selectProduct = BeginProductSelect;
        applyProduct = ApplySelectedProduct;
        clearProduct = ClearSelectedProduct;
        closeProduct = CloseProductSelection;
        if (checkoutButton != null)
            checkoutButton.onClick.AddListener(selectCheckout);
        if (moveButton != null)
            moveButton.onClick.AddListener(selectMove);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(selectDelete);
        if (productButton != null)
            productButton.onClick.AddListener(selectProduct);
        if (applyProductButton != null)
            applyProductButton.onClick.AddListener(applyProduct);
        if (clearProductButton != null)
            clearProductButton.onClick.AddListener(clearProduct);
        if (closeProductButton != null)
            closeProductButton.onClick.AddListener(closeProduct);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitBuildMode);
    }

    void OnDisable()
    {
        if (buildButton != null)
            buildButton.onClick.RemoveListener(EnterBuildMode);
        if (shelfButton != null)
            shelfButton.onClick.RemoveListener(selectShelf);
        if (refrigeratorButton != null)
            refrigeratorButton.onClick.RemoveListener(selectRefrigerator);
        if (checkoutButton != null)
            checkoutButton.onClick.RemoveListener(selectCheckout);
        if (moveButton != null)
            moveButton.onClick.RemoveListener(selectMove);
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(selectDelete);
        if (productButton != null)
            productButton.onClick.RemoveListener(selectProduct);
        if (applyProductButton != null)
            applyProductButton.onClick.RemoveListener(applyProduct);
        if (clearProductButton != null)
            clearProductButton.onClick.RemoveListener(clearProduct);
        if (closeProductButton != null)
            closeProductButton.onClick.RemoveListener(closeProduct);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitBuildMode);
    }

    void OnValidate()
    {
        if (economy == null)
            Debug.LogWarning("BuildModeController: StoreEconomy가 연결되지 않았습니다.", this);
        if (storeInventory == null)
            Debug.LogWarning("BuildModeController: StoreInventory가 연결되지 않았습니다.", this);
        if (customerSpawner == null)
            Debug.LogWarning("BuildModeController: CustomerSpawner가 연결되지 않았습니다.", this);
        if (storeStatistics == null)
            Debug.LogWarning("BuildModeController: StoreStatistics가 연결되지 않았습니다.", this);
        if (navMeshSurface == null)
            Debug.LogWarning("BuildModeController: NavMeshSurface가 연결되지 않았습니다.", this);
        if (productButton == null || productAssignmentPanel == null || productStatusText == null || productDropdown == null)
            Debug.LogWarning("BuildModeController: 상품 지정 UI가 연결되지 않았습니다.", this);
        if (applyProductButton == null || clearProductButton == null || closeProductButton == null)
            Debug.LogWarning("BuildModeController: 상품 지정 버튼이 연결되지 않았습니다.", this);
    }

    void Update()
    {
        bool preparation = IsPreparation();
        if (buildButton != null)
            buildButton.gameObject.SetActive(preparation);

        if (buildModeActive && !preparation)
        {
            ExitBuildMode();
            return;
        }

        if (!buildModeActive || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (toolMode == BuildToolMode.Moving)
                CancelMove();
            else if (toolMode == BuildToolMode.ProductSelect && assignmentShelf != null)
                CloseProductSelection();
            else
                ExitBuildMode();
            return;
        }

        if (toolMode != BuildToolMode.MoveSelect
            && Keyboard.current.rKey.wasPressedThisFrame
            && selected != null
            && preview != null)
        {
            rotationQuarterTurns = PlacedFacility.NormalizeQuarterTurns(rotationQuarterTurns + 1);
        }

        if (toolMode == BuildToolMode.Placement || toolMode == BuildToolMode.Moving)
            UpdatePreview();

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || IsPointerOverUI())
            return;

        if (toolMode == BuildToolMode.MoveSelect)
            TryBeginMoveFromPointer();
        else if (toolMode == BuildToolMode.DeleteSelect)
            TryDeleteFromPointer();
        else if (toolMode == BuildToolMode.ProductSelect)
            TrySelectProductTarget();
        else if (toolMode == BuildToolMode.Moving && preview != null && previewValid)
            ConfirmMove();
        else if (toolMode == BuildToolMode.Placement && preview != null && previewValid)
            PlaceSelected();
    }

    public void EnterBuildMode()
    {
        if (!IsPreparation())
            return;

        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        buildModeActive = true;
        rotationQuarterTurns = 0;
        if (buildPanel != null)
            buildPanel.SetActive(true);
    }

    public void ExitBuildMode()
    {
        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        buildModeActive = false;
        toolMode = BuildToolMode.None;
        rotationQuarterTurns = 0;
        selected = null;
        movingFacility = null;
        DestroyPreview();
        CloseProductSelection();
        if (buildPanel != null)
            buildPanel.SetActive(false);
    }

    public void BeginMoveSelect()
    {
        if (!buildModeActive || !IsPreparation())
            return;

        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        CloseProductSelection();
        toolMode = BuildToolMode.MoveSelect;
    }

    public void BeginDeleteSelect()
    {
        if (!buildModeActive || !IsPreparation())
            return;

        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        CloseProductSelection();
        toolMode = BuildToolMode.DeleteSelect;
    }

    public void SelectFacility(int index)
    {
        if (!buildModeActive || !IsPreparation())
            return;
        if (facilities == null || index < 0 || index >= facilities.Length)
            return;

        FacilityDefinition definition = facilities[index];
        if (definition == null || definition.Prefab == null)
            return;

        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        toolMode = BuildToolMode.Placement;
        selected = definition;
        rotationQuarterTurns = 0;
        CloseProductSelection();
        DestroyPreview();
        preview = Instantiate(definition.Prefab);
        preview.name = "FacilityPreview";
        previewBaseRotation = preview.transform.rotation;
        DisablePreviewGameplay(preview);
        previewRenderers = preview.GetComponentsInChildren<Renderer>(true);
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (preview == null || selected == null || !TryGetFloorPoint(out Vector3 worldPoint))
        {
            if (preview != null)
                preview.SetActive(false);
            previewValid = false;
            return;
        }

        preview.SetActive(true);
        Vector2Int footprint = CurrentFootprintSize;
        preview.transform.rotation = PlacementRotation();
        currentOrigin = OriginFor(worldPoint, footprint);
        Vector3 center = FootprintCenter(currentOrigin, footprint);
        PlaceOnFloor(preview, center);
        bool spatialValid = IsSpatiallyValid(currentOrigin, footprint, center);
        bool layoutValid = spatialValid && IsAccessClear(center, footprint);
        previewValid = toolMode == BuildToolMode.Moving
            ? layoutValid
            : layoutValid && CanAffordPlacement();
        ApplyTint(previewValid ? ValidTint : InvalidTint);
    }

    void PlaceSelected()
    {
        if (selected == null || selected.Prefab == null)
            return;

        Vector2Int footprint = CurrentFootprintSize;
        Vector3 center = FootprintCenter(currentOrigin, footprint);
        if (!IsSpatiallyValid(currentOrigin, footprint, center) || !IsAccessClear(center, footprint))
            return;

        if (economy == null || !economy.TrySpendFacility(selected.Cost))
            return;

        SpawnPlacedFacility(selected, currentOrigin, rotationQuarterTurns);
        RequestNavMeshRefresh();
    }

    bool CanAffordPlacement()
    {
        return selected != null && economy != null && economy.CanAffordFacility(selected.Cost);
    }

    bool IsSpatiallyValid(Vector2Int origin, Vector2Int footprint, Vector3 center)
    {
        return IsInsideFloor(origin, footprint)
            && !OverlapsBlockedCell(origin, footprint)
            && !OverlapsExisting(center, footprint);
    }

    public bool TryGetFacilityDefinition(string facilityId, out FacilityDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(facilityId) || facilities == null)
            return false;

        for (int index = 0; index < facilities.Length; index++)
        {
            FacilityDefinition candidate = facilities[index];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.FacilityId))
                continue;
            if (!string.Equals(candidate.FacilityId, facilityId, System.StringComparison.Ordinal))
                continue;

            definition = candidate;
            return true;
        }

        return false;
    }

    public bool TryCaptureDynamicFacilities(List<StorePersistence.DynamicFacilitySaveData> results)
    {
        if (results == null)
            return false;

        results.Clear();
        for (int index = 0; index < placedFacilities.Count; index++)
        {
            PlacedFacility placed = placedFacilities[index];
            if (placed == null || placed.Definition == null || string.IsNullOrWhiteSpace(placed.Definition.FacilityId))
                return false;

            if (!TryCaptureProductState(placed, out string productId, out int quantity))
                return false;

            results.Add(new StorePersistence.DynamicFacilitySaveData
            {
                facilityId = placed.Definition.FacilityId,
                gridX = placed.GridOrigin.x,
                gridY = placed.GridOrigin.y,
                rotationQuarterTurns = placed.RotationQuarterTurns,
                productId = productId,
                quantity = quantity
            });
        }

        results.Sort(CompareDynamicFacilities);
        return true;
    }

    public bool TryValidateDynamicLayouts(List<StorePersistence.DynamicFacilitySaveData> records, out string error)
    {
        error = string.Empty;
        if (!hasGrid)
        {
            error = "건설 격자를 계산할 수 없습니다.";
            return false;
        }

        if (records == null)
        {
            error = "동적 시설 목록이 없습니다.";
            return false;
        }

        var occupied = new HashSet<Vector2Int>();
        for (int index = 0; index < records.Count; index++)
        {
            StorePersistence.DynamicFacilitySaveData record = records[index];
            if (!TryDescribeDynamicRecord(record, out FacilityDefinition definition, out Vector2Int origin, out Vector2Int footprint, out error))
                return false;

            if (!TryReserveFootprint(occupied, origin, footprint, definition.FacilityId, out error))
                return false;

            Vector3 center = FootprintCenter(origin, footprint);
            if (OverlapsStaticCollider(center, footprint))
            {
                error = $"{definition.FacilityId} ({origin.x}, {origin.y})가 기존 시설이나 벽과 겹칩니다.";
                return false;
            }
        }

        if (!SavedAccessClear(records, out error))
            return false;

        return true;
    }

    public bool TryReplaceDynamicLayouts(List<StorePersistence.DynamicFacilitySaveData> records, ProductDefinition[] catalog)
    {
        if (records == null || !hasGrid)
            return false;

        ExitBuildMode();
        ClearDynamicFacilities();
        for (int index = 0; index < records.Count; index++)
        {
            StorePersistence.DynamicFacilitySaveData record = records[index];
            if (!TryGetFacilityDefinition(record.facilityId, out FacilityDefinition definition) || definition.Prefab == null)
                return false;

            PlacedFacility placed = SpawnPlacedFacility(definition, new Vector2Int(record.gridX, record.gridY), record.rotationQuarterTurns);
            if (definition.FacilityType == FacilityType.Checkout)
                continue;

            if (!TryGetShoppingShelf(placed, out Shelf shelf))
                return false;

            ProductDefinition product = null;
            if (!string.IsNullOrEmpty(record.productId) && !TryFindCatalogProduct(catalog, record.productId, out product))
                return false;

            if (!shelf.TryRestoreState(product, record.quantity))
                return false;
        }

        RequestNavMeshRefresh();
        return true;
    }

    PlacedFacility SpawnPlacedFacility(FacilityDefinition definition, Vector2Int origin, int quarterTurns)
    {
        int quarter = PlacedFacility.NormalizeQuarterTurns(quarterTurns);
        Vector2Int footprint = RotatedGridSize(definition.GridSize, quarter);
        GameObject placed = Instantiate(definition.Prefab, placedRoot);
        placed.name = "Placed_" + definition.FacilityId + "_" + origin.x + "_" + origin.y;
        placed.transform.rotation = placed.transform.rotation * Quaternion.Euler(0f, quarter * 90f, 0f);
        PlaceOnFloor(placed, FootprintCenter(origin, footprint));

        PlacedFacility metadata = placed.AddComponent<PlacedFacility>();
        metadata.Initialize(definition, origin, quarter);
        Occupy(origin, footprint);
        placedFacilities.Add(metadata);
        RegisterFacilityRuntimeDependencies(metadata);
        return metadata;
    }

    void RegisterFacilityRuntimeDependencies(PlacedFacility placed)
    {
        if (placed == null || placed.Definition == null)
            return;

        if (placed.Definition.FacilityType == FacilityType.Checkout)
        {
            RegisterCheckoutFacility(placed);
            return;
        }

        if (!TryGetShoppingShelf(placed, out Shelf shelf))
            return;

        if (storeInventory == null)
        {
            Debug.LogWarning("BuildModeController: StoreInventory가 없어 진열대를 연결하지 못했습니다.", this);
            return;
        }

        shelf.BindInventory(storeInventory);
        if (customerSpawner == null)
        {
            Debug.LogWarning("BuildModeController: CustomerSpawner가 없어 진열대를 등록하지 못했습니다.", this);
            return;
        }

        customerSpawner.RegisterShoppingShelf(shelf);
    }

    void UnregisterFacilityRuntimeDependencies(PlacedFacility placed)
    {
        if (placed == null || placed.Definition == null)
            return;

        if (placed.Definition.FacilityType == FacilityType.Checkout)
        {
            UnregisterCheckoutFacility(placed);
            return;
        }

        if (!TryGetShoppingShelf(placed, out Shelf shelf) || customerSpawner == null)
            return;

        customerSpawner.UnregisterShoppingShelf(shelf);
    }

    void RegisterCheckoutFacility(PlacedFacility placed)
    {
        CheckoutCounter counter = placed.GetComponent<CheckoutCounter>();
        if (counter == null)
        {
            Debug.LogWarning($"BuildModeController: {placed.Definition.FacilityId}에 CheckoutCounter가 없습니다.", placed);
            return;
        }

        if (economy == null || storeStatistics == null)
        {
            Debug.LogWarning("BuildModeController: StoreEconomy 또는 StoreStatistics가 없어 계산대를 연결하지 못했습니다.", this);
            return;
        }

        if (!counter.BindStoreServices(economy, storeStatistics))
            return;

        if (customerSpawner == null)
        {
            Debug.LogWarning("BuildModeController: CustomerSpawner가 없어 계산대를 등록하지 못했습니다.", this);
            return;
        }

        customerSpawner.RegisterCheckout(counter);
    }

    void UnregisterCheckoutFacility(PlacedFacility placed)
    {
        CheckoutCounter counter = placed.GetComponent<CheckoutCounter>();
        if (counter == null)
        {
            Debug.LogWarning($"BuildModeController: {placed.Definition.FacilityId}에 CheckoutCounter가 없어 등록을 해제하지 못했습니다.", placed);
            return;
        }

        if (customerSpawner == null)
        {
            Debug.LogWarning("BuildModeController: CustomerSpawner가 없어 계산대 등록을 해제하지 못했습니다.", this);
            return;
        }

        customerSpawner.UnregisterCheckout(counter);
    }

    void RequestNavMeshRefresh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning("BuildModeController: NavMeshSurface가 없어 NavMesh를 갱신하지 못했습니다.", this);
            return;
        }

        if (navMeshRefreshPending)
            return;

        navMeshRefreshPending = true;
        StartCoroutine(RefreshNavMeshNextFrame());
    }

    IEnumerator RefreshNavMeshNextFrame()
    {
        yield return null;
        navMeshRefreshPending = false;
        if (navMeshSurface == null)
        {
            Debug.LogWarning("BuildModeController: NavMeshSurface가 없어 NavMesh를 갱신하지 못했습니다.", this);
            yield break;
        }

        navMeshSurface.BuildNavMesh();
    }

    static bool TryGetShoppingShelf(PlacedFacility placed, out Shelf shelf)
    {
        shelf = null;
        if (placed == null || placed.Definition == null)
            return false;

        FacilityType facilityType = placed.Definition.FacilityType;
        if (facilityType != FacilityType.Shelf && facilityType != FacilityType.Refrigerator)
            return false;

        shelf = placed.GetComponent<Shelf>();
        if (shelf != null)
            return true;

        Debug.LogWarning($"BuildModeController: {placed.Definition.FacilityId}에 Shelf가 없어 쇼핑 목록에 넣지 못했습니다.", placed);
        return false;
    }

    // Load는 저장 상태로 통째로 바꾸므로 현재 진열 수량을 창고로 되돌리지 않는다.
    void ClearDynamicFacilities()
    {
        for (int index = placedFacilities.Count - 1; index >= 0; index--)
        {
            PlacedFacility placed = placedFacilities[index];
            if (placed == null)
                continue;

            UnregisterFacilityRuntimeDependencies(placed);
            Destroy(placed.gameObject);
        }

        placedFacilities.Clear();
        occupiedCells.Clear();
    }

    bool TryDescribeDynamicRecord(
        StorePersistence.DynamicFacilitySaveData record,
        out FacilityDefinition definition,
        out Vector2Int origin,
        out Vector2Int footprint,
        out string error)
    {
        definition = null;
        origin = default;
        footprint = default;
        error = string.Empty;
        if (record == null || string.IsNullOrWhiteSpace(record.facilityId))
        {
            error = "동적 시설 Id가 비어 있습니다.";
            return false;
        }

        if (!TryGetFacilityDefinition(record.facilityId, out definition))
        {
            error = $"알 수 없는 시설 Id입니다. Id: {record.facilityId}";
            return false;
        }

        if (definition.Prefab == null)
        {
            error = $"{record.facilityId}의 Prefab이 없습니다.";
            return false;
        }

        if (record.rotationQuarterTurns < 0 || record.rotationQuarterTurns > 3)
        {
            error = $"{record.facilityId}의 회전 값은 0에서 3이어야 합니다. 현재 값: {record.rotationQuarterTurns}";
            return false;
        }

        origin = new Vector2Int(record.gridX, record.gridY);
        footprint = RotatedGridSize(definition.GridSize, record.rotationQuarterTurns);
        if (!IsInsideFloor(origin, footprint))
        {
            error = $"{record.facilityId} ({origin.x}, {origin.y})가 바닥 밖에 있습니다.";
            return false;
        }

        return true;
    }

    static bool TryReserveFootprint(HashSet<Vector2Int> occupied, Vector2Int origin, Vector2Int size, string facilityId, out string error)
    {
        error = string.Empty;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!occupied.Add(cell))
                {
                    error = $"{facilityId} ({origin.x}, {origin.y})의 점유 칸이 다른 동적 시설과 겹칩니다.";
                    return false;
                }
            }
        }

        return true;
    }

    static int CompareDynamicFacilities(StorePersistence.DynamicFacilitySaveData left, StorePersistence.DynamicFacilitySaveData right)
    {
        int gridY = left.gridY.CompareTo(right.gridY);
        if (gridY != 0)
            return gridY;

        int gridX = left.gridX.CompareTo(right.gridX);
        if (gridX != 0)
            return gridX;

        int facilityId = string.Compare(left.facilityId, right.facilityId, System.StringComparison.Ordinal);
        if (facilityId != 0)
            return facilityId;

        return left.rotationQuarterTurns.CompareTo(right.rotationQuarterTurns);
    }

    void TryBeginMoveFromPointer()
    {
        if (!TryGetPlacedFacilityUnderPointer(out PlacedFacility placed))
            return;
        if (placed.Definition == null || placed.Definition.Prefab == null)
            return;

        BeginMoving(placed);
    }

    void TryDeleteFromPointer()
    {
        if (!TryGetPlacedFacilityUnderPointer(out PlacedFacility placed))
            return;
        if (placed.Definition == null)
            return;

        Vector2Int origin = placed.GridOrigin;
        int quarterTurns = placed.RotationQuarterTurns;
        Vector2Int footprint = RotatedGridSize(placed.Definition.GridSize, quarterTurns);
        if (!FootprintIsOccupied(origin, footprint))
            Debug.LogWarning("삭제할 시설의 점유 칸이 회전된 발자국과 일치하지 않습니다.");

        if (TryGetShoppingShelf(placed, out Shelf shelf) && !ReturnShelfStock(shelf))
            return;

        if (assignmentShelf != null && placed.GetComponent<Shelf>() == assignmentShelf)
            CloseProductSelection();

        int cost = placed.Definition.Cost;
        UnregisterFacilityRuntimeDependencies(placed);
        placedFacilities.Remove(placed);
        Release(origin, footprint);
        Destroy(placed.gameObject);
        RequestNavMeshRefresh();
        if (cost < 0)
            return;

        if (economy == null)
        {
            Debug.LogWarning("시설을 삭제했지만 StoreEconomy가 없어 환불하지 못했습니다.", this);
            return;
        }

        economy.AddFacilityRefund(cost / 2);
    }

    bool TryGetPlacedFacilityUnderPointer(out PlacedFacility placed)
    {
        placed = null;
        if (viewCamera == null || Mouse.current == null)
            return false;

        Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        placed = hit.collider.GetComponentInParent<PlacedFacility>();
        return placed != null;
    }

    bool FootprintIsOccupied(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (!occupiedCells.Contains(new Vector2Int(origin.x + x, origin.y + y)))
                    return false;
            }
        }

        return true;
    }

    void BeginMoving(PlacedFacility placed)
    {
        movingFacility = placed;
        originalOrigin = placed.GridOrigin;
        originalQuarterTurns = placed.RotationQuarterTurns;
        originalPosition = placed.transform.position;
        originalRotation = placed.transform.rotation;
        CaptureOriginalBody(placed);
        selected = placed.Definition;
        rotationQuarterTurns = originalQuarterTurns;
        Release(originalOrigin, RotatedGridSize(selected.GridSize, originalQuarterTurns));
        placed.gameObject.SetActive(false);

        DestroyPreview();
        preview = Instantiate(selected.Prefab);
        preview.name = "FacilityPreview";
        previewBaseRotation = preview.transform.rotation;
        DisablePreviewGameplay(preview);
        previewRenderers = preview.GetComponentsInChildren<Renderer>(true);
        toolMode = BuildToolMode.Moving;
        UpdatePreview();
    }

    void ConfirmMove()
    {
        if (movingFacility == null)
            return;

        Vector2Int footprint = CurrentFootprintSize;
        Vector3 center = FootprintCenter(currentOrigin, footprint);
        if (!IsSpatiallyValid(currentOrigin, footprint, center) || !IsAccessClear(center, footprint))
            return;

        Vector3 position = preview.transform.position;
        Quaternion rotation = preview.transform.rotation;
        Vector2Int origin = currentOrigin;
        int quarterTurns = rotationQuarterTurns;
        FacilityDefinition definition = selected;

        GameObject target = movingFacility.gameObject;
        target.SetActive(true);
        target.transform.SetPositionAndRotation(position, rotation);
        target.name = "Placed_" + definition.FacilityId + "_" + origin.x + "_" + origin.y;
        movingFacility.UpdatePlacement(origin, quarterTurns);
        Occupy(origin, footprint);

        movingFacility = null;
        hasOriginalBodyBounds = false;
        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        toolMode = BuildToolMode.MoveSelect;
        RequestNavMeshRefresh();
    }

    void CancelMove()
    {
        if (movingFacility != null)
        {
            GameObject target = movingFacility.gameObject;
            target.SetActive(true);
            target.transform.SetPositionAndRotation(originalPosition, originalRotation);
            Occupy(originalOrigin, RotatedGridSize(movingFacility.Definition.GridSize, originalQuarterTurns));
            movingFacility = null;
            hasOriginalBodyBounds = false;
        }

        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        if (buildModeActive)
            toolMode = BuildToolMode.MoveSelect;
    }

    void CacheGrid()
    {
        hasGrid = false;
        if (buildSurface == null || cellSize <= 0f)
            return;

        Bounds bounds = buildSurface.bounds;
        originX = bounds.min.x;
        originZ = bounds.min.z;
        floorTop = bounds.max.y;
        gridWidth = Mathf.RoundToInt(bounds.size.x / cellSize);
        gridDepth = Mathf.RoundToInt(bounds.size.z / cellSize);
        hasGrid = gridWidth > 0 && gridDepth > 0;
    }

    bool TryGetFloorPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (!hasGrid || viewCamera == null || Mouse.current == null)
            return false;

        Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, floorTop, 0f));
        if (!plane.Raycast(ray, out float distance))
            return false;

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    static Vector2Int RotatedGridSize(Vector2Int baseSize, int quarterTurns)
    {
        int turns = PlacedFacility.NormalizeQuarterTurns(quarterTurns);
        if (turns == 1 || turns == 3)
            return new Vector2Int(baseSize.y, baseSize.x);
        return baseSize;
    }

    Quaternion PlacementRotation()
    {
        Quaternion yaw = Quaternion.Euler(0f, rotationQuarterTurns * 90f, 0f);
        return previewBaseRotation * yaw;
    }

    Vector2Int OriginFor(Vector3 worldPoint, Vector2Int size)
    {
        int x = Mathf.RoundToInt((worldPoint.x - originX) / cellSize - size.x * 0.5f);
        int y = Mathf.RoundToInt((worldPoint.z - originZ) / cellSize - size.y * 0.5f);
        return new Vector2Int(x, y);
    }

    Vector3 FootprintCenter(Vector2Int origin, Vector2Int size)
    {
        float x = originX + (origin.x + size.x * 0.5f) * cellSize;
        float z = originZ + (origin.y + size.y * 0.5f) * cellSize;
        return new Vector3(x, floorTop, z);
    }

    bool IsInsideFloor(Vector2Int origin, Vector2Int size)
    {
        return origin.x >= 0
            && origin.y >= 0
            && origin.x + size.x <= gridWidth
            && origin.y + size.y <= gridDepth;
    }

    bool OverlapsBlockedCell(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (occupiedCells.Contains(new Vector2Int(origin.x + x, origin.y + y)))
                    return true;
            }
        }

        return false;
    }

    bool IsAccessClear(Vector3 center, Vector2Int footprint)
    {
        float radius = AccessClearance();
        if (radius < 0f)
            return false;

        return ExistingPointsClearOfFootprint(center, footprint, radius, false)
            && PreviewPointsClear(radius);
    }

    bool SavedAccessClear(List<StorePersistence.DynamicFacilitySaveData> records, out string error)
    {
        error = string.Empty;
        float radius = AccessClearance();
        if (radius < 0f)
        {
            error = "NavMesh Agent Radius를 확인하지 못했습니다.";
            return false;
        }

        CollectDynamicBodyBounds();

        for (int index = 0; index < records.Count; index++)
        {
            StorePersistence.DynamicFacilitySaveData record = records[index];
            if (!TryDescribeDynamicRecord(record, out FacilityDefinition definition, out Vector2Int origin, out Vector2Int footprint, out error))
                return false;

            Vector3 center = FootprintCenter(origin, footprint);
            if (!ExistingPointsClearOfFootprint(center, footprint, radius, true))
            {
                error = $"{definition.FacilityId} ({origin.x}, {origin.y})가 기존 고객 접근 지점을 막습니다.";
                return false;
            }

            if (!TryFillSavedAccess(definition, origin, footprint, record.rotationQuarterTurns))
            {
                error = $"{definition.FacilityId} ({origin.x}, {origin.y})의 고객 접근 지점을 확인하지 못했습니다.";
                return false;
            }

            for (int pointIndex = 0; pointIndex < accessScratch.Count; pointIndex++)
            {
                if (AccessPointBlocked(accessScratch[pointIndex], radius, true))
                {
                    error = $"{definition.FacilityId} ({origin.x}, {origin.y})의 고객 접근 지점이 기존 시설이나 벽에 막힙니다.";
                    return false;
                }

                if (!AccessOnWalkableMesh(accessScratch[pointIndex], false, true))
                {
                    error = $"{definition.FacilityId} ({origin.x}, {origin.y})의 고객 접근 지점이 매장 바닥 또는 NavMesh 밖에 있습니다.";
                    return false;
                }
            }
        }

        for (int left = 0; left < records.Count; left++)
        {
            if (!TryDescribeDynamicRecord(records[left], out FacilityDefinition leftDefinition, out Vector2Int leftOrigin, out Vector2Int leftFootprint, out error))
                return false;

            Vector3 leftCenter = FootprintCenter(leftOrigin, leftFootprint);
            for (int right = 0; right < records.Count; right++)
            {
                if (left == right)
                    continue;

                if (!TryDescribeDynamicRecord(records[right], out FacilityDefinition rightDefinition, out Vector2Int rightOrigin, out Vector2Int rightFootprint, out error))
                    return false;

                if (!TryFillSavedAccess(rightDefinition, rightOrigin, rightFootprint, records[right].rotationQuarterTurns))
                {
                    error = $"{rightDefinition.FacilityId} ({rightOrigin.x}, {rightOrigin.y})의 고객 접근 지점을 확인하지 못했습니다.";
                    return false;
                }

                for (int pointIndex = 0; pointIndex < accessScratch.Count; pointIndex++)
                {
                    if (!ClearsFootprint(accessScratch[pointIndex], leftCenter, leftFootprint, radius))
                    {
                        error = $"{leftDefinition.FacilityId} ({leftOrigin.x}, {leftOrigin.y})가 {rightDefinition.FacilityId}의 고객 접근 지점을 막습니다.";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    bool ExistingPointsClearOfFootprint(Vector3 center, Vector2Int footprint, float radius, bool ignoreDynamic)
    {
        if (customerSpawner == null)
        {
            WarnAccessSetup("BuildModeController: CustomerSpawner가 없어 고객 접근 지점을 확인하지 못했습니다.");
            return false;
        }

        IReadOnlyList<Shelf> shelves = customerSpawner.ShoppingShelves;
        if (shelves != null)
        {
            for (int index = 0; index < shelves.Count; index++)
            {
                Shelf shelf = shelves[index];
                if (shelf == null || SkipAccessOwner(shelf, ignoreDynamic))
                    continue;

                Transform standPoint = shelf.CustomerStandPoint;
                if (standPoint == null)
                {
                    WarnAccessSetup("BuildModeController: CustomerStandPoint가 없는 진열대가 있습니다.");
                    return false;
                }

                if (!ClearsFootprint(standPoint.position, center, footprint, radius))
                    return false;
            }
        }

        IReadOnlyList<CheckoutCounter> checkouts = customerSpawner.AvailableCheckouts;
        if (checkouts == null)
            return true;

        for (int index = 0; index < checkouts.Count; index++)
        {
            CheckoutCounter counter = checkouts[index];
            if (counter == null || SkipAccessOwner(counter, ignoreDynamic))
                continue;

            IReadOnlyList<Transform> points = counter.QueuePoints;
            if (points == null || points.Count == 0)
            {
                WarnAccessSetup("BuildModeController: Queue Point가 없는 계산대가 있습니다.");
                return false;
            }

            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                Transform point = points[pointIndex];
                if (point == null)
                {
                    WarnAccessSetup("BuildModeController: 비어 있는 Queue Point가 있습니다.");
                    return false;
                }

                if (!ClearsFootprint(point.position, center, footprint, radius))
                    return false;
            }
        }

        return true;
    }

    bool PreviewPointsClear(float radius)
    {
        if (preview == null || selected == null)
            return false;

        if (selected.FacilityType == FacilityType.Checkout)
        {
            CheckoutCounter counter = preview.GetComponent<CheckoutCounter>();
            IReadOnlyList<Transform> points = counter == null ? null : counter.QueuePoints;
            if (counter == null || points == null || points.Count == 0)
            {
                WarnAccessSetup("BuildModeController: 배치할 계산대에 Queue Point가 없습니다.");
                return false;
            }

            for (int index = 0; index < points.Count; index++)
            {
                if (points[index] == null)
                {
                    WarnAccessSetup("BuildModeController: 배치할 계산대의 Queue Point가 비어 있습니다.");
                    return false;
                }

                if (AccessPointBlocked(points[index].position, radius, false))
                    return false;
                if (!AccessOnWalkableMesh(points[index].position, movingFacility != null, false))
                    return false;
            }

            return true;
        }

        Shelf shelf = preview.GetComponent<Shelf>();
        if (shelf == null || shelf.CustomerStandPoint == null)
        {
            WarnAccessSetup("BuildModeController: 배치할 진열대에 CustomerStandPoint가 없습니다.");
            return false;
        }

        Vector3 stand = shelf.CustomerStandPoint.position;
        return !AccessPointBlocked(stand, radius, false)
            && AccessOnWalkableMesh(stand, movingFacility != null, false);
    }

    bool TryFillSavedAccess(FacilityDefinition definition, Vector2Int origin, Vector2Int footprint, int quarterTurns)
    {
        accessScratch.Clear();
        if (definition == null || definition.Prefab == null)
            return false;

        GameObject prefab = definition.Prefab;
        Quaternion rotation = prefab.transform.rotation * Quaternion.Euler(0f, PlacedFacility.NormalizeQuarterTurns(quarterTurns) * 90f, 0f);
        Vector3 center = FootprintCenter(origin, footprint);
        if (definition.FacilityType == FacilityType.Checkout)
        {
            CheckoutCounter counter = prefab.GetComponent<CheckoutCounter>();
            IReadOnlyList<Transform> points = counter == null ? null : counter.QueuePoints;
            if (counter == null || points == null || points.Count == 0)
            {
                WarnAccessSetup("BuildModeController: 저장할 계산대에 Queue Point가 없습니다.");
                return false;
            }

            for (int index = 0; index < points.Count; index++)
            {
                if (points[index] == null)
                {
                    WarnAccessSetup("BuildModeController: 저장할 계산대의 Queue Point가 비어 있습니다.");
                    return false;
                }

                accessScratch.Add(SavedAccessWorld(prefab, rotation, center, points[index]));
            }

            return true;
        }

        Shelf shelf = prefab.GetComponent<Shelf>();
        if (shelf == null || shelf.CustomerStandPoint == null)
        {
            WarnAccessSetup("BuildModeController: 저장할 진열대에 CustomerStandPoint가 없습니다.");
            return false;
        }

        accessScratch.Add(SavedAccessWorld(prefab, rotation, center, shelf.CustomerStandPoint));
        return true;
    }

    static Vector3 SavedAccessWorld(GameObject prefab, Quaternion rotation, Vector3 center, Transform point)
    {
        Vector3 local = prefab.transform.InverseTransformPoint(point.position);
        Vector3 offset = rotation * Vector3.Scale(local, prefab.transform.localScale);
        return new Vector3(center.x + offset.x, center.y, center.z + offset.z);
    }

    bool AccessPointBlocked(Vector3 point, float radius, bool ignoreDynamic)
    {
        float horizontal = Mathf.Max(0f, radius - OverlapPadding);
        Vector3 boxCenter = new Vector3(point.x, floorTop + OverlapCenterHeight, point.z);
        Vector3 halfExtents = new Vector3(horizontal, OverlapHalfHeight, horizontal);
        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            overlapHits,
            Quaternion.identity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = overlapHits[index];
            if (hit == null || hit == buildSurface)
                continue;
            if (preview != null && hit.transform.IsChildOf(preview.transform))
                continue;
            if (movingFacility != null && hit.transform.IsChildOf(movingFacility.transform))
                continue;
            if (ignoreDynamic && hit.GetComponentInParent<PlacedFacility>() != null)
                continue;
            return true;
        }

        return false;
    }

    bool ClearsFootprint(Vector3 point, Vector3 center, Vector2Int footprint, float radius)
    {
        Vector3 halfExtents = FootprintHalfExtents(footprint);
        float left = center.x - halfExtents.x;
        float right = center.x + halfExtents.x;
        float bottom = center.z - halfExtents.z;
        float top = center.z + halfExtents.z;
        float dx = Mathf.Max(left - point.x, Mathf.Max(0f, point.x - right));
        float dz = Mathf.Max(bottom - point.z, Mathf.Max(0f, point.z - top));
        return dx * dx + dz * dz >= radius * radius;
    }

    bool SkipAccessOwner(Component owner, bool ignoreDynamic)
    {
        if (movingFacility != null && owner.transform.IsChildOf(movingFacility.transform))
            return true;

        return ignoreDynamic && owner.GetComponent<PlacedFacility>() != null;
    }

    float AccessClearance()
    {
        if (!EnsureWalkableQuery())
            return -1f;

        return cachedAgentRadius;
    }

    bool EnsureWalkableQuery()
    {
        if (walkableQueryReady)
            return true;

        if (navMeshSurface == null)
        {
            WarnAccessSetup("BuildModeController: NavMeshSurface가 없어 고객 접근 간격을 확인하지 못했습니다.");
            return false;
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(navMeshSurface.agentTypeID);
        if (settings.agentTypeID == -1)
        {
            WarnAccessSetup("BuildModeController: NavMesh Agent 설정을 찾지 못했습니다.");
            return false;
        }

        cachedAgentRadius = settings.agentRadius;
        cachedAgentTypeId = settings.agentTypeID;
        walkableQuery = new NavMeshQueryFilter
        {
            agentTypeID = cachedAgentTypeId,
            areaMask = NavMesh.AllAreas
        };
        walkableQueryReady = true;
        return true;
    }

    bool InsideFloorBounds(Vector3 point)
    {
        if (!hasGrid || buildSurface == null)
            return false;

        Bounds floor = buildSurface.bounds;
        return point.x >= floor.min.x && point.x <= floor.max.x
            && point.z >= floor.min.z && point.z <= floor.max.z;
    }

    bool AccessOnWalkableMesh(Vector3 point, bool forgiveMovingHole, bool forgiveDynamicHoles)
    {
        if (!InsideFloorBounds(point))
            return false;

        if (!EnsureWalkableQuery())
            return false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, cachedAgentRadius, walkableQuery))
            return true;

        if (forgiveMovingHole && hasOriginalBodyBounds && CoversExpanded(originalBodyBounds, point, cachedAgentRadius))
            return true;

        if (forgiveDynamicHoles && CoveredByDynamicBody(point, cachedAgentRadius))
            return true;

        return false;
    }

    void CaptureOriginalBody(PlacedFacility placed)
    {
        hasOriginalBodyBounds = false;
        if (placed == null)
            return;

        Collider[] colliders = placed.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider collider = colliders[index];
            if (collider == null)
                continue;

            if (!hasOriginalBodyBounds)
            {
                originalBodyBounds = collider.bounds;
                hasOriginalBodyBounds = true;
                continue;
            }

            originalBodyBounds.Encapsulate(collider.bounds);
        }
    }

    void CollectDynamicBodyBounds()
    {
        dynamicBodyScratch.Clear();
        for (int index = 0; index < placedFacilities.Count; index++)
        {
            PlacedFacility placed = placedFacilities[index];
            if (placed == null)
                continue;

            Collider[] colliders = placed.GetComponentsInChildren<Collider>(true);
            bool any = false;
            Bounds body = default;
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider collider = colliders[colliderIndex];
                if (collider == null)
                    continue;

                if (!any)
                {
                    body = collider.bounds;
                    any = true;
                    continue;
                }

                body.Encapsulate(collider.bounds);
            }

            if (any)
                dynamicBodyScratch.Add(body);
        }
    }

    bool CoveredByDynamicBody(Vector3 point, float padding)
    {
        for (int index = 0; index < dynamicBodyScratch.Count; index++)
        {
            if (CoversExpanded(dynamicBodyScratch[index], point, padding))
                return true;
        }

        return false;
    }

    static bool CoversExpanded(Bounds bounds, Vector3 point, float padding)
    {
        return point.x >= bounds.min.x - padding
            && point.x <= bounds.max.x + padding
            && point.z >= bounds.min.z - padding
            && point.z <= bounds.max.z + padding;
    }

    void WarnAccessSetup(string message)
    {
        if (warnedAccessSetup)
            return;

        warnedAccessSetup = true;
        Debug.LogWarning(message, this);
    }

    Vector3 FootprintHalfExtents(Vector2Int size)
    {
        return new Vector3(
            size.x * cellSize * 0.5f - OverlapPadding,
            OverlapHalfHeight,
            size.y * cellSize * 0.5f - OverlapPadding);
    }

    bool OverlapsExisting(Vector3 center, Vector2Int size)
    {
        Vector3 boxCenter = center + Vector3.up * OverlapCenterHeight;
        Vector3 halfExtents = FootprintHalfExtents(size);
        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            overlapHits,
            Quaternion.identity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || hit == buildSurface)
                continue;
            if (preview != null && hit.transform.IsChildOf(preview.transform))
                continue;
            return true;
        }

        return false;
    }

    bool OverlapsStaticCollider(Vector3 center, Vector2Int size)
    {
        Vector3 boxCenter = center + Vector3.up * OverlapCenterHeight;
        Vector3 halfExtents = FootprintHalfExtents(size);
        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            overlapHits,
            Quaternion.identity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || hit == buildSurface)
                continue;
            if (preview != null && hit.transform.IsChildOf(preview.transform))
                continue;
            if (hit.GetComponentInParent<PlacedFacility>() != null)
                continue;
            return true;
        }

        return false;
    }

    void Occupy(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
                occupiedCells.Add(new Vector2Int(origin.x + x, origin.y + y));
        }
    }

    void Release(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
                occupiedCells.Remove(new Vector2Int(origin.x + x, origin.y + y));
        }
    }

    static void PlaceOnFloor(GameObject target, Vector3 footprintCenter)
    {
        target.transform.position = footprintCenter;
        float lowest = float.PositiveInfinity;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            lowest = Mathf.Min(lowest, renderers[i].bounds.min.y);

        if (lowest < float.PositiveInfinity)
            target.transform.position += Vector3.up * (footprintCenter.y - lowest);
    }

    static void DisablePreviewGameplay(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Behaviour[] behaviours = target.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            behaviours[i].enabled = false;
    }

    void ApplyTint(Color color)
    {
        if (previewRenderers == null || tintBlock == null)
            return;

        tintBlock.SetColor(BaseColorId, color);
        tintBlock.SetColor(ColorId, color);
        for (int i = 0; i < previewRenderers.Length; i++)
        {
            if (previewRenderers[i] != null)
                previewRenderers[i].SetPropertyBlock(tintBlock);
        }
    }

    void DestroyPreview()
    {
        if (preview != null)
            Destroy(preview);
        preview = null;
        previewRenderers = null;
        previewBaseRotation = Quaternion.identity;
        previewValid = false;
    }

    bool IsPreparation()
    {
        return session != null && session.Phase == StorePhase.Preparation;
    }

    public void BeginProductSelect()
    {
        if (!buildModeActive || !IsPreparation())
            return;

        if (toolMode == BuildToolMode.Moving)
            CancelMove();

        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        CloseProductSelection();
        toolMode = BuildToolMode.ProductSelect;
    }

    void TrySelectProductTarget()
    {
        if (!TryGetPlacedFacilityUnderPointer(out PlacedFacility placed))
            return;
        if (!TryGetShoppingShelf(placed, out Shelf shelf))
            return;

        assignmentShelf = shelf;
        RefreshProductOptions();
        RefreshProductStatus();
        if (productAssignmentPanel != null)
            productAssignmentPanel.SetActive(true);
    }

    void ApplySelectedProduct()
    {
        if (assignmentShelf == null || productDropdown == null)
            return;

        int index = productDropdown.value;
        if (index < 0 || index >= assignmentProducts.Count)
            return;

        ProductDefinition product = assignmentProducts[index];
        if (product == null || product == assignmentShelf.AssignedProduct)
            return;
        if (product.StorageType != assignmentShelf.AcceptedStorageType)
            return;
        if (!ReturnShelfStock(assignmentShelf))
            return;
        if (!assignmentShelf.TryAssignProduct(product))
            return;

        RefreshProductStatus();
    }

    void ClearSelectedProduct()
    {
        if (assignmentShelf == null)
            return;
        if (assignmentShelf.AssignedProduct == null && assignmentShelf.CurrentQuantity == 0)
            return;
        if (!ReturnShelfStock(assignmentShelf))
            return;

        assignmentShelf.TryAssignProduct(null);
        RefreshProductStatus();
    }

    void CloseProductSelection()
    {
        assignmentShelf = null;
        assignmentProducts.Clear();
        if (productDropdown != null)
            productDropdown.ClearOptions();
        if (productAssignmentPanel != null)
            productAssignmentPanel.SetActive(false);
    }

    void RefreshProductOptions()
    {
        assignmentProducts.Clear();
        if (productDropdown == null)
            return;

        productDropdown.ClearOptions();
        if (assignmentShelf == null || storeInventory == null)
            return;

        var options = new List<TMP_Dropdown.OptionData>();
        int selectedIndex = 0;
        for (int index = 0; index < storeInventory.ProductDefinitionCount; index++)
        {
            ProductDefinition product = storeInventory.GetProductDefinition(index);
            if (product == null || product.StorageType != assignmentShelf.AcceptedStorageType)
                continue;

            if (product == assignmentShelf.AssignedProduct)
                selectedIndex = assignmentProducts.Count;

            assignmentProducts.Add(product);
            options.Add(new TMP_Dropdown.OptionData(product.DisplayName));
        }

        if (options.Count == 0)
            return;

        productDropdown.AddOptions(options);
        productDropdown.SetValueWithoutNotify(selectedIndex);
        productDropdown.RefreshShownValue();
    }

    void RefreshProductStatus()
    {
        if (productStatusText == null || assignmentShelf == null)
            return;

        PlacedFacility placed = assignmentShelf.GetComponent<PlacedFacility>();
        string facilityName = placed != null && placed.Definition != null
            ? placed.Definition.DisplayName
            : "진열대";
        string productName = assignmentShelf.AssignedProduct == null
            ? "없음"
            : assignmentShelf.AssignedProduct.DisplayName;
        productStatusText.text = facilityName + "\n현재 상품: " + productName
            + "\n수량: " + assignmentShelf.CurrentQuantity + " / " + assignmentShelf.Capacity;
    }

    bool TryCaptureProductState(PlacedFacility placed, out string productId, out int quantity)
    {
        productId = string.Empty;
        quantity = 0;
        if (placed == null || placed.Definition == null)
            return false;
        if (placed.Definition.FacilityType == FacilityType.Checkout)
            return true;
        if (!TryGetShoppingShelf(placed, out Shelf shelf))
            return false;

        ProductDefinition assigned = shelf.AssignedProduct;
        if (assigned == null)
        {
            if (shelf.CurrentQuantity != 0)
            {
                Debug.LogWarning("BuildModeController: 상품이 없는 진열대의 수량이 0이 아니어서 저장하지 않습니다.", placed);
                return false;
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(assigned.ProductId)
            || assigned.StorageType != shelf.AcceptedStorageType
            || shelf.CurrentQuantity < 0
            || shelf.CurrentQuantity > shelf.Capacity)
        {
            Debug.LogWarning("BuildModeController: 진열 상태가 올바르지 않아 저장하지 않습니다.", placed);
            return false;
        }

        productId = assigned.ProductId;
        quantity = shelf.CurrentQuantity;
        return true;
    }

    bool ReturnShelfStock(Shelf shelf)
    {
        if (shelf == null)
            return false;

        int quantity = shelf.CurrentQuantity;
        if (quantity <= 0)
            return true;

        return shelf.ReturnToInventory(quantity) == quantity;
    }

    static bool TryFindCatalogProduct(ProductDefinition[] catalog, string productId, out ProductDefinition product)
    {
        product = null;
        if (catalog == null || string.IsNullOrEmpty(productId))
            return false;

        for (int index = 0; index < catalog.Length; index++)
        {
            ProductDefinition candidate = catalog[index];
            if (candidate == null || !string.Equals(candidate.ProductId, productId, System.StringComparison.Ordinal))
                continue;

            product = candidate;
            return true;
        }

        return false;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData data = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }
}
