using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns the warehouse/backroom area visuals and stock-box interaction rules.
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
    [SerializeField] private Color warehouseRackColor = new Color(0.45f, 0.49f, 0.55f, 1f);

    private readonly List<WarehouseStockBox> activeWarehouseStockBoxes = new List<WarehouseStockBox>();
    private readonly Dictionary<string, int> reservedSlotByProductName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Vector3[] warehouseStockLocalSlots =
    {
        new Vector3(-2.3f, 0.82f, 1.02f),
        new Vector3(-2.3f, 1.52f, 1.02f),
        new Vector3(-2.3f, 2.22f, 1.02f),
        new Vector3(0f, 0.82f, 1.02f),
        new Vector3(0f, 1.52f, 1.02f),
        new Vector3(0f, 2.22f, 1.02f),
        new Vector3(2.3f, 0.82f, 1.02f),
        new Vector3(2.3f, 1.52f, 1.02f),
        new Vector3(2.3f, 2.22f, 1.02f)
    };

    private GameManager gameManager;
    private Transform backroomZoneRoot;
    private Collider backroomZoneCollider;

    public Vector3 BackroomDropOffPosition => backroomDropOffPosition;

    public void Initialize(GameManager owner)
    {
        gameManager = owner;
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
        CreateWarehouseRacks();
        CreateWarehouseStockBoxes();

        GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneObject.name = "BackroomStoragePad";
        zoneObject.transform.SetParent(backroomZoneRoot, false);
        zoneObject.transform.localPosition = new Vector3(backroomDropOffPosition.x - warehouseZoneCenter.x,
                                                         -backroomDropOffPosition.y + (backroomZoneSize.y * 0.5f),
                                                         backroomDropOffPosition.z - warehouseZoneCenter.z);
        zoneObject.transform.localScale = backroomZoneSize;
        backroomZoneCollider = zoneObject.GetComponent<Collider>();

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
        label.text = "BACKROOM STORAGE\nBulk Stock Drop";
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color(0f, 0f, 0f, 0.7f);
        label.outlineWidth = 0.15f;
        labelObject.AddComponent<BillboardToCamera>();

        UpdateBackroomZoneTransform();
    }

    public void RefreshStockBoxes()
    {
        if (backroomZoneRoot == null)
        {
            return;
        }

        SyncWarehouseStockBoxes();
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

    public bool IsPlayerInsideWarehouse(Camera mainCamera)
    {
        if (mainCamera == null)
        {
            return false;
        }

        Vector3 position = mainCamera.transform.position;
        Vector3 relative = position - warehouseZoneCenter;
        float halfWidth = warehouseZoneSize.x * 0.5f;
        float halfDepth = warehouseZoneSize.z * 0.5f;

        return relative.x >= -halfWidth &&
               relative.x <= halfWidth &&
               relative.z >= -halfDepth &&
               relative.z <= halfDepth;
    }

    public string GetPrompt(WarehouseStockBox stockBox, Shelf targetShelf, Camera mainCamera)
    {
        if (stockBox == null)
        {
            return string.Empty;
        }

        if (!IsPlayerInsideWarehouse(mainCamera))
        {
            return "Go into the warehouse to grab stock";
        }

        if (targetShelf != null && targetShelf.CanAcceptProduct(stockBox.Product))
        {
            return "[E] Grab stock box for " + stockBox.Product.productName;
        }

        return "[E] Grab " + stockBox.Product.productName + " stock box";
    }

    public bool TryPrepareRestockForShelf(Shelf shelf, Camera mainCamera)
    {
        if (shelf == null || shelf.AssignedProduct == null || !IsPlayerInsideWarehouse(mainCamera))
        {
            return false;
        }

        WarehouseStockBox stockBox = FindStockBox(shelf.AssignedProduct);
        return gameManager.TryPickUpWarehouseStockForShelf(stockBox, shelf);
    }

    public void HandleStockBoxClicked(WarehouseStockBox stockBox)
    {
        if (stockBox == null)
        {
            return;
        }

        Shelf targetShelf = gameManager.GetSelectedOrFocusedShelf();
        if (targetShelf != null && !targetShelf.CanAcceptProduct(stockBox.Product))
        {
            targetShelf = null;
        }

        gameManager.TryPickUpWarehouseStockForShelf(stockBox, targetShelf);
    }

    public WarehouseStockBox FindStockBox(ProductData product)
    {
        if (product == null)
        {
            return null;
        }

        foreach (WarehouseStockBox stockBox in activeWarehouseStockBoxes)
        {
            if (stockBox != null && stockBox.Product == product)
            {
                return stockBox;
            }
        }

        return null;
    }

    private void CreateWarehouseStockBoxes()
    {
        SyncWarehouseStockBoxes();
    }

    private void ClearWarehouseStockBoxes()
    {
        foreach (WarehouseStockBox stockBox in activeWarehouseStockBoxes)
        {
            if (stockBox != null)
            {
                Destroy(stockBox.gameObject);
            }
        }

        activeWarehouseStockBoxes.Clear();
    }

    private void SyncWarehouseStockBoxes()
    {
        activeWarehouseStockBoxes.RemoveAll(stockBox => stockBox == null);

        List<ProductData> productsToDisplay = GetProductsForReservedSlots();
        AssignReservedSlots(productsToDisplay);
        int targetCount = Mathf.Min(reservedSlotByProductName.Count, warehouseStockLocalSlots.Length);

        while (activeWarehouseStockBoxes.Count < targetCount)
        {
            activeWarehouseStockBoxes.Add(CreateWarehouseStockBoxObject());
        }

        while (activeWarehouseStockBoxes.Count > targetCount)
        {
            int lastIndex = activeWarehouseStockBoxes.Count - 1;
            WarehouseStockBox extraBox = activeWarehouseStockBoxes[lastIndex];
            activeWarehouseStockBoxes.RemoveAt(lastIndex);

            if (extraBox != null)
            {
                Destroy(extraBox.gameObject);
            }
        }

        foreach (ProductData product in productsToDisplay)
        {
            if (product == null ||
                !reservedSlotByProductName.TryGetValue(product.productName, out int slotIndex) ||
                slotIndex < 0 ||
                slotIndex >= activeWarehouseStockBoxes.Count ||
                slotIndex >= warehouseStockLocalSlots.Length)
            {
                continue;
            }

            WarehouseStockBox stockBox = activeWarehouseStockBoxes[slotIndex];
            if (stockBox == null || product == null)
            {
                continue;
            }

            stockBox.gameObject.name = product.productName + "_WarehouseStock";
            stockBox.transform.localPosition = warehouseStockLocalSlots[slotIndex];
            stockBox.Setup(product, gameManager.GetBackroomStock(product));
        }
    }

    private WarehouseStockBox CreateWarehouseStockBoxObject()
    {
        GameObject boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.name = "WarehouseStock";
        boxObject.transform.SetParent(backroomZoneRoot, false);
        return boxObject.AddComponent<WarehouseStockBox>();
    }

    private List<ProductData> GetProductsForReservedSlots()
    {
        List<ProductData> products = new List<ProductData>();
        foreach (ProductData product in gameManager.GetKnownProducts())
        {
            if (product == null)
            {
                continue;
            }

            products.Add(product);
        }

        products.Sort((left, right) => string.Compare(left.productName, right.productName, StringComparison.OrdinalIgnoreCase));
        return products;
    }

    private void AssignReservedSlots(List<ProductData> products)
    {
        foreach (ProductData product in products)
        {
            if (product == null ||
                string.IsNullOrWhiteSpace(product.productName) ||
                reservedSlotByProductName.ContainsKey(product.productName) ||
                reservedSlotByProductName.Count >= warehouseStockLocalSlots.Length)
            {
                continue;
            }

            reservedSlotByProductName[product.productName] = reservedSlotByProductName.Count;
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
        sign.text = "BACKROOM WAREHOUSE\nBulk Crates Here";
        sign.fontSize = 5f;
        sign.alignment = TextAlignmentOptions.Center;
        sign.color = Color.white;
        sign.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        sign.outlineWidth = 0.2f;
        signObject.AddComponent<BillboardToCamera>();
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

    private void CreateWarehouseRacks()
    {
        CreateWarehouseRack(new Vector3(-2.3f, 0f, 0.7f));
        CreateWarehouseRack(new Vector3(0f, 0f, 0.7f));
        CreateWarehouseRack(new Vector3(2.3f, 0f, 0.7f));
    }

    private void CreateWarehouseRack(Vector3 localCenter)
    {
        CreateWarehouseRackPart("Rack_LeftPost", localCenter + new Vector3(-0.55f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), true);
        CreateWarehouseRackPart("Rack_RightPost", localCenter + new Vector3(0.55f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), true);
        CreateWarehouseRackPart("Rack_BackPostLeft", localCenter + new Vector3(-0.55f, 1.1f, -0.65f), new Vector3(0.12f, 2.2f, 0.12f), true);
        CreateWarehouseRackPart("Rack_BackPostRight", localCenter + new Vector3(0.55f, 1.1f, -0.65f), new Vector3(0.12f, 2.2f, 0.12f), true);
        CreateWarehouseRackPart("RackShelf", localCenter + new Vector3(0f, 0.45f, -0.32f), new Vector3(1.2f, 0.08f, 0.72f), false);
        CreateWarehouseRackPart("RackShelf", localCenter + new Vector3(0f, 1.15f, -0.32f), new Vector3(1.2f, 0.08f, 0.72f), false);
        CreateWarehouseRackPart("RackShelf", localCenter + new Vector3(0f, 1.85f, -0.32f), new Vector3(1.2f, 0.08f, 0.72f), false);
    }

    private void CreateWarehouseRackPart(string objectName, Vector3 localPosition, Vector3 localScale, bool keepCollider)
    {
        GameObject rackPart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rackPart.name = objectName;
        rackPart.transform.SetParent(backroomZoneRoot, false);
        rackPart.transform.localPosition = localPosition;
        rackPart.transform.localScale = localScale;

        if (!keepCollider)
        {
            Collider rackCollider = rackPart.GetComponent<Collider>();
            if (rackCollider != null)
            {
                Destroy(rackCollider);
            }
        }

        MeshRenderer rackRenderer = rackPart.GetComponent<MeshRenderer>();
        if (rackRenderer != null)
        {
            Material rackMaterial = gameManager.CreateRuntimeMaterial(warehouseRackColor);
            rackRenderer.material = rackMaterial;
            rackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rackRenderer.receiveShadows = false;
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
}
