using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Owns the warehouse room, purchasable warehouse shelf placement, and physical boxed stock.
/// </summary>
public class WarehouseManager : MonoBehaviour
{
    [Header("Backroom Zone")]
    [SerializeField] private Vector3 backroomDropOffPosition = new Vector3(-4f, 0.4f, 8f);
    [SerializeField] private Vector3 backroomZoneSize = new Vector3(4f, 0.12f, 2.6f);
    [SerializeField] private Vector3 backroomZoneLabelOffset = new Vector3(0f, 1.6f, -1.1f);
    [SerializeField] private Color backroomZoneColor = new Color(0.32f, 0.57f, 0.85f, 0.95f);

    [Header("Warehouse Layout")]
    [SerializeField] private Vector3 warehouseZoneCenter = new Vector3(-7f, 0f, 8f);
    [SerializeField] private Vector3 warehouseZoneSize = new Vector3(10f, 3.2f, 6f);
    [SerializeField] private Color warehouseWallColor = new Color(0.78f, 0.8f, 0.84f, 1f);

    [Header("Warehouse Shelves")]
    [SerializeField] private GameObject warehouseShelfVisualPrefab;
    [SerializeField] private float warehouseShelfBuyCost = 100f;
    [SerializeField] private Color warehouseShelfPreviewColor = new Color(0.35f, 1f, 0.85f, 0.75f);
    [SerializeField] private Color invalidWarehouseShelfPreviewColor = new Color(1f, 0.2f, 0.16f, 0.75f);
    [SerializeField] private bool snapWarehouseShelvesToGrid = true;
    [SerializeField] private float warehouseShelfGridSize = 1f;
    [SerializeField] private Vector3 warehouseShelfGridOrigin = Vector3.zero;
    [SerializeField] private Vector3 firstMigrationShelfPosition = new Vector3(-9.4f, 0f, 8.6f);
    [SerializeField] private Vector3 migrationShelfSpacing = new Vector3(1.6f, 0f, 0f);

    private readonly List<WarehouseShelf> warehouseShelves = new List<WarehouseShelf>();

    private GameManager gameManager;
    private Camera mainCamera;
    private LayerMask floorLayer;
    private Transform backroomZoneRoot;
    private bool isPlacingWarehouseShelf;
    private bool hasUnplacedWarehouseShelfPurchase;
    private bool waitingForPlacementClickRelease;
    private WarehouseShelf warehouseShelfPreview;
    private MeshRenderer[] previewRenderers;

    public Vector3 BackroomDropOffPosition => backroomDropOffPosition;
    public bool IsPlacingWarehouseShelf => isPlacingWarehouseShelf;

    public void Initialize(GameManager owner, Camera camera, LayerMask floorLayerValue)
    {
        gameManager = owner;
        mainCamera = camera;
        floorLayer = floorLayerValue;
        ResolveDefaultVisualPrefab();
        EnsureVisual();
    }

    public void EnsureVisual()
    {
        if (backroomZoneRoot != null)
        {
            UpdateBackroomZoneTransform();
            return;
        }

        GameObject rootObject = new GameObject("BackroomStorageZone");
        rootObject.transform.SetParent(transform, false);
        backroomZoneRoot = rootObject.transform;

        CreateWarehouseFloorPad();
        CreateWarehouseWalls();
        CreateBackroomDropPad();
        UpdateBackroomZoneTransform();
    }

    public void HandleUpdate()
    {
        if (!isPlacingWarehouseShelf)
        {
            return;
        }

        if (waitingForPlacementClickRelease)
        {
            UpdateWarehouseShelfPreviewPosition();

            if (!gameManager.IsLeftMouseButtonHeld())
            {
                waitingForPlacementClickRelease = false;
            }

            return;
        }

        UpdateWarehouseShelfPreviewPosition();

        if (gameManager.GetLeftMouseButtonDown())
        {
            TryPlaceWarehouseShelfFromMouse();
        }

        if (gameManager.GetRightMouseButtonDown())
        {
            CancelWarehouseShelfPlacement();
        }
    }

    public void StartWarehouseShelfPlacement(MoneyManager moneyManager)
    {
        if (isPlacingWarehouseShelf)
        {
            return;
        }

        if (moneyManager == null)
        {
            Debug.LogWarning("MoneyManager not found in the scene.");
            return;
        }

        if (!moneyManager.SpendMoney(warehouseShelfBuyCost))
        {
            Debug.Log("Not enough money to buy a warehouse shelf.");
            return;
        }

        isPlacingWarehouseShelf = true;
        hasUnplacedWarehouseShelfPurchase = true;
        waitingForPlacementClickRelease = gameManager.IsLeftMouseButtonHeld();
        CreateWarehouseShelfPreview();
        Debug.Log("Warehouse shelf purchased. Left click on the floor to place it. Right click to cancel.");
    }

    public void CancelWarehouseShelfPlacement()
    {
        MoneyManager moneyManager = gameManager != null ? gameManager.MoneyManager : null;
        if (isPlacingWarehouseShelf && hasUnplacedWarehouseShelfPurchase && moneyManager != null)
        {
            moneyManager.AddMoney(warehouseShelfBuyCost);
        }

        isPlacingWarehouseShelf = false;
        hasUnplacedWarehouseShelfPurchase = false;
        waitingForPlacementClickRelease = false;
        DestroyWarehouseShelfPreview();
    }

    public void TryPlaceWarehouseShelfFromCurrentPointer()
    {
        TryPlaceWarehouseShelfFromMouse();
    }

    public int GetStoredAmount(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int total = 0;
        foreach (WarehouseShelf shelf in GetLiveWarehouseShelves())
        {
            total += shelf.GetStoredAmount(product);
        }

        return total;
    }

    public bool TryPlaceBoxOnShelf(WarehouseShelf shelf, ProductData product, int amount)
    {
        if (shelf == null || product == null || amount <= 0)
        {
            return false;
        }

        bool placed = shelf.TryAddBox(product, amount);
        if (placed)
        {
            gameManager.RegisterKnownProduct(product);
            gameManager.NotifyStateChanged();
        }

        return placed;
    }

    public bool TryPickUpBoxFromShelf(WarehouseShelf shelf, Shelf targetShelf)
    {
        if (shelf == null)
        {
            return false;
        }

        ProductData preferredProduct = targetShelf != null ? targetShelf.AssignedProduct : null;
        if (!shelf.TryRemoveBox(preferredProduct, out ProductData product, out int amount))
        {
            if (shelf.TryClearProductLock())
            {
                gameManager.NotifyStateChanged();
                return true;
            }

            return false;
        }

        gameManager.SpawnCarriedWarehouseBox(product, amount, shelf.transform.position + Vector3.up * 0.75f);
        gameManager.NotifyStateChanged();
        return true;
    }

    public bool TryReturnWarehouseBox(ProductData product, int amount)
    {
        if (product == null || amount <= 0)
        {
            return false;
        }

        foreach (WarehouseShelf shelf in GetLiveWarehouseShelves())
        {
            if (shelf.TryAddBox(product, amount))
            {
                gameManager.NotifyStateChanged();
                return true;
            }
        }

        return false;
    }

    public bool TryReturnWarehouseBoxToShelf(WarehouseShelf shelf, ProductData product, int amount)
    {
        return TryPlaceBoxOnShelf(shelf, product, amount);
    }

    public string GetPrompt(WarehouseShelf shelf, ProductData carriedProduct, bool isCarryingWarehouseBox, Shelf targetShelf, Camera camera)
    {
        if (shelf == null)
        {
            return string.Empty;
        }

        if (!IsPlayerInsideWarehouse(camera))
        {
            return "Go into the warehouse to use storage shelves";
        }

        return shelf.GetPrompt(carriedProduct, isCarryingWarehouseBox, targetShelf);
    }

    public bool IsPointInsideDropZone(Vector3 worldPoint)
    {
        Vector3 zoneCenter = new Vector3(backroomDropOffPosition.x, 0f, backroomDropOffPosition.z);
        float halfWidth = backroomZoneSize.x * 0.5f;
        float halfDepth = backroomZoneSize.z * 0.5f;

        return worldPoint.x >= zoneCenter.x - halfWidth &&
               worldPoint.x <= zoneCenter.x + halfWidth &&
               worldPoint.z >= zoneCenter.z - halfDepth &&
               worldPoint.z <= zoneCenter.z + halfDepth;
    }

    public bool IsPlayerInsideWarehouse(Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        Vector3 position = camera.transform.position;
        Vector3 relative = position - warehouseZoneCenter;
        float halfWidth = warehouseZoneSize.x * 0.5f;
        float halfDepth = warehouseZoneSize.z * 0.5f;

        return relative.x >= -halfWidth &&
               relative.x <= halfWidth &&
               relative.z >= -halfDepth &&
               relative.z <= halfDepth;
    }

    public void ClearWarehouseShelves()
    {
        foreach (WarehouseShelf shelf in GetLiveWarehouseShelves())
        {
            if (shelf != null)
            {
                Destroy(shelf.gameObject);
            }
        }

        warehouseShelves.Clear();
        DestroyWarehouseShelfPreview();
        isPlacingWarehouseShelf = false;
        hasUnplacedWarehouseShelfPurchase = false;
        waitingForPlacementClickRelease = false;
    }

    public WarehouseShelf SpawnLoadedWarehouseShelf(Vector3 worldPosition, ProductData productLock, List<WarehouseShelfSlotRuntimeState> loadedSlots)
    {
        WarehouseShelf shelf = CreateWarehouseShelf(worldPosition);
        shelf.LoadState(productLock, loadedSlots);
        return shelf;
    }

    public List<WarehouseShelfSaveEntry> BuildWarehouseShelfStates()
    {
        List<WarehouseShelfSaveEntry> states = new List<WarehouseShelfSaveEntry>();
        foreach (WarehouseShelf shelf in GetLiveWarehouseShelves())
        {
            states.Add(shelf.BuildSaveEntry());
        }

        return states;
    }

    public void MigrateLegacyBackroomInventory(Dictionary<ProductData, int> inventory)
    {
        if (inventory == null || inventory.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<ProductData, int> entry in inventory)
        {
            if (entry.Key == null || entry.Value <= 0)
            {
                continue;
            }

            WarehouseShelf shelf = FindCompatibleShelfWithSpace(entry.Key);
            if (shelf == null)
            {
                shelf = CreateWarehouseShelf(GetNextMigrationShelfPosition());
            }

            shelf.TryAddBox(entry.Key, entry.Value);
        }

        inventory.Clear();
    }

    private WarehouseShelf FindCompatibleShelfWithSpace(ProductData product)
    {
        foreach (WarehouseShelf shelf in GetLiveWarehouseShelves())
        {
            if (shelf.CanAcceptProduct(product))
            {
                return shelf;
            }
        }

        return null;
    }

    private List<WarehouseShelf> GetLiveWarehouseShelves()
    {
        warehouseShelves.RemoveAll(shelf => shelf == null);

        WarehouseShelf[] sceneShelves = FindObjectsByType<WarehouseShelf>();
        foreach (WarehouseShelf shelf in sceneShelves)
        {
            if (shelf != null && !warehouseShelves.Contains(shelf))
            {
                warehouseShelves.Add(shelf);
            }
        }

        return warehouseShelves;
    }

    private Vector3 GetNextMigrationShelfPosition()
    {
        int index = GetLiveWarehouseShelves().Count;
        int column = index % 5;
        int row = index / 5;
        return firstMigrationShelfPosition +
               new Vector3(migrationShelfSpacing.x * column, migrationShelfSpacing.y * row, migrationShelfSpacing.z * row);
    }

    private void TryPlaceWarehouseShelfFromMouse()
    {
        if (TryGetPlacementPosition(out Vector3 placementPosition))
        {
            PlaceWarehouseShelfAtPosition(placementPosition);
        }
    }

    private bool TryGetPlacementPosition(out Vector3 placementPosition)
    {
        placementPosition = Vector3.zero;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return false;
        }

        Vector3 mousePosition = gameManager.GetMouseScreenPosition();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        bool hitFloor = Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer);
        if (!hitFloor)
        {
            hitFloor = Physics.Raycast(ray, out hit, 100f);
        }

        if (hitFloor)
        {
            placementPosition = hit.point;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            placementPosition = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private void PlaceWarehouseShelfAtPosition(Vector3 worldPosition)
    {
        Vector3 spawnPosition = GetWarehouseShelfPlacementGroundPosition(worldPosition);

        if (!IsPlacementPositionValid(spawnPosition))
        {
            Debug.Log("Cannot place warehouse shelf here. Move it away from shelves, walls, customers, checkout, or warehouse racks.");
            return;
        }

        CreateWarehouseShelf(spawnPosition);
        isPlacingWarehouseShelf = false;
        hasUnplacedWarehouseShelfPurchase = false;
        waitingForPlacementClickRelease = false;
        DestroyWarehouseShelfPreview();
        gameManager.QueueStoreNavigationRebuild();
        gameManager.NotifyStateChanged();
    }

    private WarehouseShelf CreateWarehouseShelf(Vector3 worldPosition)
    {
        GameObject shelfObject = new GameObject("WarehouseShelf");
        shelfObject.transform.position = worldPosition;

        WarehouseShelf shelf = shelfObject.AddComponent<WarehouseShelf>();
        shelf.Initialize(gameManager, warehouseShelfVisualPrefab);
        warehouseShelves.Add(shelf);
        return shelf;
    }

    private void UpdateWarehouseShelfPreviewPosition()
    {
        if (!isPlacingWarehouseShelf || warehouseShelfPreview == null)
        {
            return;
        }

        if (TryGetPlacementPosition(out Vector3 placementPosition))
        {
            Vector3 previewPosition = GetWarehouseShelfPlacementGroundPosition(placementPosition);
            warehouseShelfPreview.transform.position = previewPosition;
            bool isValid = IsPlacementPositionValid(previewPosition);
            ApplyPreviewColor(isValid);

            if (!warehouseShelfPreview.gameObject.activeSelf)
            {
                warehouseShelfPreview.gameObject.SetActive(true);
            }
        }
        else if (warehouseShelfPreview.gameObject.activeSelf)
        {
            warehouseShelfPreview.gameObject.SetActive(false);
        }
    }

    private Vector3 GetWarehouseShelfPlacementGroundPosition(Vector3 worldPosition)
    {
        if (!snapWarehouseShelvesToGrid || warehouseShelfGridSize <= 0.01f)
        {
            return worldPosition;
        }

        Vector3 snappedPosition = worldPosition;
        snappedPosition.x = SnapCoordinate(worldPosition.x, warehouseShelfGridOrigin.x, warehouseShelfGridSize);
        snappedPosition.z = SnapCoordinate(worldPosition.z, warehouseShelfGridOrigin.z, warehouseShelfGridSize);
        snappedPosition.y = 0f;
        return snappedPosition;
    }

    private float SnapCoordinate(float value, float origin, float gridSize)
    {
        return origin + (Mathf.Round((value - origin) / gridSize) * gridSize);
    }

    private void CreateWarehouseShelfPreview()
    {
        if (warehouseShelfPreview != null)
        {
            return;
        }

        warehouseShelfPreview = CreateWarehouseShelf(Vector3.zero);
        warehouseShelfPreview.gameObject.name = "WarehouseShelf_Preview";
        warehouseShelves.Remove(warehouseShelfPreview);
        warehouseShelfPreview.enabled = false;

        Collider[] colliders = warehouseShelfPreview.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        previewRenderers = warehouseShelfPreview.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in previewRenderers)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material previewMaterial = new Material(renderer.material);
            previewMaterial.color = warehouseShelfPreviewColor;
            renderer.material = previewMaterial;
        }

        warehouseShelfPreview.gameObject.SetActive(false);
    }

    private void DestroyWarehouseShelfPreview()
    {
        if (warehouseShelfPreview == null)
        {
            return;
        }

        Destroy(warehouseShelfPreview.gameObject);
        warehouseShelfPreview = null;
        previewRenderers = null;
    }

    private bool IsPlacementPositionValid(Vector3 shelfWorldPosition)
    {
        Bounds placementBounds = new Bounds(shelfWorldPosition + new Vector3(0f, 1f, 0f), new Vector3(1.35f, 2.05f, 0.95f));
        Vector3 overlapHalfExtents = placementBounds.extents;
        overlapHalfExtents.x = Mathf.Max(0.05f, overlapHalfExtents.x - 0.05f);
        overlapHalfExtents.y = Mathf.Max(0.05f, overlapHalfExtents.y - 0.04f);
        overlapHalfExtents.z = Mathf.Max(0.05f, overlapHalfExtents.z - 0.05f);

        Collider[] overlaps = Physics.OverlapBox(
            placementBounds.center,
            overlapHalfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
        {
            if (IsIgnoredPlacementOverlap(overlap, placementBounds))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsIgnoredPlacementOverlap(Collider overlap, Bounds placementBounds)
    {
        if (overlap == null)
        {
            return true;
        }

        if (warehouseShelfPreview != null && overlap.transform.IsChildOf(warehouseShelfPreview.transform))
        {
            return true;
        }

        return IsPlacementSurface(overlap, placementBounds);
    }

    private bool IsPlacementSurface(Collider overlap, Bounds placementBounds)
    {
        Bounds overlapBounds = overlap.bounds;
        bool isBelowShelf = overlapBounds.max.y <= placementBounds.min.y + 0.12f;
        bool isThinSurface = overlapBounds.size.y <= 0.2f;
        string overlapName = overlap.gameObject.name;
        bool looksLikeGround = overlapName.Contains("Floor") ||
                               overlapName.Contains("Ground") ||
                               overlapName.Contains("Pad") ||
                               overlapName.Contains("Zone");

        return isBelowShelf && (isThinSurface || looksLikeGround);
    }

    private void ApplyPreviewColor(bool isValidPlacement)
    {
        if (previewRenderers == null)
        {
            return;
        }

        Color targetColor = isValidPlacement ? warehouseShelfPreviewColor : invalidWarehouseShelfPreviewColor;
        foreach (MeshRenderer renderer in previewRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = targetColor;
            }
        }
    }

    private void CreateWarehouseFloorPad()
    {
        GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObject.name = "WarehouseFloorPad";
        floorObject.transform.SetParent(backroomZoneRoot, false);
        floorObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        floorObject.transform.localScale = new Vector3(warehouseZoneSize.x, 0.06f, warehouseZoneSize.z);

        MeshRenderer floorRenderer = floorObject.GetComponent<MeshRenderer>();
        if (floorRenderer != null)
        {
            Material floorMaterial = gameManager.CreateRuntimeMaterial(new Color(0.55f, 0.56f, 0.58f, 1f));
            floorRenderer.material = floorMaterial;
            floorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floorRenderer.receiveShadows = false;
        }
    }

    private void CreateWarehouseWalls()
    {
        CreateWarehousePanel("BackroomRearWall", new Vector3(0f, warehouseZoneSize.y * 0.5f, warehouseZoneSize.z * 0.5f), new Vector3(warehouseZoneSize.x, warehouseZoneSize.y, 0.18f));
        CreateWarehousePanel("BackroomLeftWall", new Vector3(-warehouseZoneSize.x * 0.5f, warehouseZoneSize.y * 0.5f, 0f), new Vector3(0.18f, warehouseZoneSize.y, warehouseZoneSize.z));
        CreateWarehousePanel("BackroomRightWall", new Vector3(warehouseZoneSize.x * 0.5f, warehouseZoneSize.y * 0.5f, 0f), new Vector3(0.18f, warehouseZoneSize.y, warehouseZoneSize.z));

        GameObject signObject = new GameObject("BackroomWarehouseLabel");
        signObject.transform.SetParent(backroomZoneRoot, false);
        signObject.transform.localPosition = new Vector3(0f, 2.45f, warehouseZoneSize.z * 0.5f - 0.35f);

        TextMeshPro sign = signObject.AddComponent<TextMeshPro>();
        sign.text = "BACKROOM WAREHOUSE\nPlace Crates on Shelves";
        sign.fontSize = 5f;
        sign.alignment = TextAlignmentOptions.Center;
        sign.color = Color.white;
        sign.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        sign.outlineWidth = 0.2f;
        signObject.AddComponent<BillboardToCamera>();
    }

    private void CreateBackroomDropPad()
    {
        GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneObject.name = "BackroomStoragePad";
        zoneObject.transform.SetParent(backroomZoneRoot, false);
        zoneObject.transform.localPosition = new Vector3(backroomDropOffPosition.x - warehouseZoneCenter.x,
                                                         -backroomDropOffPosition.y + (backroomZoneSize.y * 0.5f),
                                                         backroomDropOffPosition.z - warehouseZoneCenter.z);
        zoneObject.transform.localScale = backroomZoneSize;

        Collider zoneCollider = zoneObject.GetComponent<Collider>();
        if (zoneCollider != null)
        {
            Destroy(zoneCollider);
        }

        MeshRenderer zoneRenderer = zoneObject.GetComponent<MeshRenderer>();
        if (zoneRenderer != null)
        {
            Material zoneMaterial = gameManager.CreateRuntimeMaterial(backroomZoneColor);
            zoneRenderer.material = zoneMaterial;
            zoneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zoneRenderer.receiveShadows = false;
        }

        GameObject labelObject = new GameObject("BackroomStorageLabel");
        labelObject.transform.SetParent(backroomZoneRoot, false);
        labelObject.transform.localPosition = new Vector3(backroomDropOffPosition.x - warehouseZoneCenter.x,
                                                          backroomZoneLabelOffset.y,
                                                          backroomDropOffPosition.z - warehouseZoneCenter.z + backroomZoneLabelOffset.z);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "WAREHOUSE STORAGE\nUse Shelf Slots";
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.7f);
        label.outlineWidth = 0.15f;
        labelObject.AddComponent<BillboardToCamera>();
    }

    private void CreateWarehousePanel(string objectName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject panelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panelObject.name = objectName;
        panelObject.transform.SetParent(backroomZoneRoot, false);
        panelObject.transform.localPosition = localPosition;
        panelObject.transform.localScale = localScale;

        MeshRenderer panelRenderer = panelObject.GetComponent<MeshRenderer>();
        if (panelRenderer != null)
        {
            Material panelMaterial = gameManager.CreateRuntimeMaterial(warehouseWallColor);
            panelRenderer.material = panelMaterial;
            panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            panelRenderer.receiveShadows = false;
        }
    }

    private void UpdateBackroomZoneTransform()
    {
        if (backroomZoneRoot == null)
        {
            return;
        }

        backroomZoneRoot.position = warehouseZoneCenter;
    }

    private void ResolveDefaultVisualPrefab()
    {
#if UNITY_EDITOR
        if (warehouseShelfVisualPrefab == null)
        {
            warehouseShelfVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowPolyMetalRack/Prefabs/WireShelf C.prefab");
        }
#endif
    }
}
