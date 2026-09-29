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
        Moving
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
    [SerializeField] Button exitButton;

    readonly Collider[] overlapHits = new Collider[32];
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    MaterialPropertyBlock tintBlock;

    Transform placedRoot;
    GameObject preview;
    UnityEngine.Events.UnityAction selectShelf;
    UnityEngine.Events.UnityAction selectRefrigerator;
    UnityEngine.Events.UnityAction selectCheckout;
    UnityEngine.Events.UnityAction selectMove;
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
        if (checkoutButton != null)
            checkoutButton.onClick.AddListener(selectCheckout);
        if (moveButton != null)
            moveButton.onClick.AddListener(selectMove);
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
        Vector2Int footprint = CurrentFootprintSize;
        GameObject placed = Instantiate(selected.Prefab, placedRoot);
        placed.name = "Placed_" + selected.FacilityId + "_" + currentOrigin.x + "_" + currentOrigin.y;
        placed.transform.rotation = PlacementRotation();
        PlaceOnFloor(placed, FootprintCenter(currentOrigin, footprint));

        PlacedFacility metadata = placed.AddComponent<PlacedFacility>();
        metadata.Initialize(selected, currentOrigin, rotationQuarterTurns);
        Occupy(currentOrigin, footprint);
    }

    void TryBeginMoveFromPointer()
    {
        if (viewCamera == null || Mouse.current == null)
            return;

        Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return;

        PlacedFacility placed = hit.collider.GetComponentInParent<PlacedFacility>();
        if (placed == null || placed.Definition == null || placed.Definition.Prefab == null)
            return;

        BeginMoving(placed);
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
