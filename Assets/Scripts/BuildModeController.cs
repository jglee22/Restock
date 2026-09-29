using System.Collections.Generic;
using UnityEngine;
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
        DeleteSelect
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
    [SerializeField] Button exitButton;

    readonly Collider[] overlapHits = new Collider[32];
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    MaterialPropertyBlock tintBlock;

    Transform placedRoot;
    readonly List<PlacedFacility> placedFacilities = new List<PlacedFacility>();
    GameObject preview;
    UnityEngine.Events.UnityAction selectShelf;
    UnityEngine.Events.UnityAction selectRefrigerator;
    UnityEngine.Events.UnityAction selectCheckout;
    UnityEngine.Events.UnityAction selectMove;
    UnityEngine.Events.UnityAction selectDelete;
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
        if (buildPanel != null)
            buildPanel.SetActive(false);
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
        if (checkoutButton != null)
            checkoutButton.onClick.AddListener(selectCheckout);
        if (moveButton != null)
            moveButton.onClick.AddListener(selectMove);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(selectDelete);
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
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitBuildMode);
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
        previewValid = IsInsideFloor(currentOrigin, footprint)
            && !OverlapsBlockedCell(currentOrigin, footprint)
            && !OverlapsExisting(center, footprint);
        ApplyTint(previewValid ? ValidTint : InvalidTint);
    }

    void PlaceSelected()
    {
        SpawnPlacedFacility(selected, currentOrigin, rotationQuarterTurns);
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

            results.Add(new StorePersistence.DynamicFacilitySaveData
            {
                facilityId = placed.Definition.FacilityId,
                gridX = placed.GridOrigin.x,
                gridY = placed.GridOrigin.y,
                rotationQuarterTurns = placed.RotationQuarterTurns
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

        return true;
    }

    public bool TryReplaceDynamicLayouts(List<StorePersistence.DynamicFacilitySaveData> records)
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

            SpawnPlacedFacility(definition, new Vector2Int(record.gridX, record.gridY), record.rotationQuarterTurns);
        }

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
        return metadata;
    }

    void ClearDynamicFacilities()
    {
        for (int index = placedFacilities.Count - 1; index >= 0; index--)
        {
            PlacedFacility placed = placedFacilities[index];
            if (placed != null)
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

        placedFacilities.Remove(placed);
        Release(origin, footprint);
        Destroy(placed.gameObject);
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
        selected = null;
        rotationQuarterTurns = 0;
        DestroyPreview();
        toolMode = BuildToolMode.MoveSelect;
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

    bool OverlapsExisting(Vector3 center, Vector2Int size)
    {
        Vector3 boxCenter = center + Vector3.up * OverlapCenterHeight;
        Vector3 halfExtents = new Vector3(
            size.x * cellSize * 0.5f - OverlapPadding,
            OverlapHalfHeight,
            size.y * cellSize * 0.5f - OverlapPadding);
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
        Vector3 halfExtents = new Vector3(
            size.x * cellSize * 0.5f - OverlapPadding,
            OverlapHalfHeight,
            size.y * cellSize * 0.5f - OverlapPadding);
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
