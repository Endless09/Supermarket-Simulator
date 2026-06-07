using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game coordinator for the prototype.
/// It tracks the day, manages backroom inventory, and handles simple shelf placement.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [System.Serializable]
    public class ProductInventoryEntry
    {
        public ProductData product;
        public int amount = 10;
    }

    [Header("Store Progress")]
    [SerializeField] private int startingDay = 1;

    [Header("Shelf Placement")]
    [SerializeField] private Shelf shelfPrefab;
    [SerializeField] private LayerMask floorLayer;
    [SerializeField] private float shelfBuyCost = 50f;
    [SerializeField] private float placementHeightOffset = 0f;
    [SerializeField] private bool autoPlaceShelves = false;
    [SerializeField] private Vector3 firstShelfPosition = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 shelfSpacing = new Vector3(2f, 0f, 0f);
    [SerializeField] private Color shelfPreviewColor = new Color(0.35f, 1f, 0.85f, 0.75f);

    [Header("Starting Backroom Inventory")]
    [SerializeField] private List<ProductInventoryEntry> startingInventory = new List<ProductInventoryEntry>();

    [Header("Shelf Product")]
    [SerializeField] private ProductData defaultShelfProduct;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 4f;

    [Header("Carry")]
    [SerializeField] private Vector3 carriedCrateOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private int restockBoxCarryAmount = 5;
    [SerializeField] private float heldObjectForwardDistance = 1.35f;
    [SerializeField] private float heldObjectSideOffset = 0.6f;
    [SerializeField] private float heldObjectVerticalOffset = 0.15f;

    private readonly ProductInventoryManager productInventory = new ProductInventoryManager();
    private readonly ProductPricingManager productPricing = new ProductPricingManager();
    private readonly List<RestockBox> activeRestockBoxes = new List<RestockBox>();
    private const string CustomersStillInsideWarning = "Customers are still inside. Press Next Day again to continue anyway.";

    public int CurrentDay { get; private set; }
    public bool IsPlacingShelf => (shelfPlacementManager != null && shelfPlacementManager.IsPlacingShelf) ||
                                  (warehouseManager != null && warehouseManager.IsPlacingWarehouseShelf);
    public bool IsPlacingWarehouseShelf => warehouseManager != null && warehouseManager.IsPlacingWarehouseShelf;
    public bool IsMovingShelf => shelfPlacementManager != null && shelfPlacementManager.IsMovingShelf;

    private MoneyManager moneyManager;
    private Camera mainCamera;
    private bool isApplyingSaveData;
    private bool hasLoadedSaveData;
    private ProductData initialDefaultShelfProduct;
    private RestockBox carriedRestockBox;
    private EmptyBox carriedEmptyBox;
    private DeliveryManager deliveryManager;
    private PlayerInteractionManager playerInteractionManager;
    private GameSaveSystem saveSystem;
    private ShelfPlacementManager shelfPlacementManager;
    private WarehouseManager warehouseManager;
    private TrashManager trashManager;
    private StoreComputer storeComputer;
    private DayNightCycle dayNightCycle;
    private RuntimeSceneManager runtimeSceneManager;
    private bool isStateDirty;
    private bool nextDayCustomerWarningPending;
    private int dailyItemsSold;
    private int dailyMissingItems;
    private int dailyTooExpensiveItems;
    private int dailyCustomerTrips;
    private float dailySalesRevenue;
    private float dailyWholesaleCostOfGoods;
    private float dailyProductOrderCosts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentDay = startingDay;
        moneyManager = FindAnyObjectByType<MoneyManager>();
        mainCamera = Camera.main;
        EnsurePlayerController();
        initialDefaultShelfProduct = defaultShelfProduct;
        EnsurePlayerInteractionManager();
        EnsureShelfPlacementManager();
        EnsureSaveSystem();
        EnsureDayNightCycle();
        EnsureDeliveryManager();
        EnsureWarehouseManager();
        EnsureTrashManager();
        EnsureStoreComputer();
        EnsureRuntimeSceneManager();

        BuildStartingInventory();
        MigrateBackroomInventoryToWarehouseShelves();
        RegisterKnownProductsFromInitialData();
    }

    private void Start()
    {
        StartCoroutine(LoadGameAfterSceneSetup());
    }

    private void Update()
    {
        EnsureDeliveryManager();
        if (deliveryManager != null && (dayNightCycle == null || dayNightCycle.IsClockRunning))
        {
            deliveryManager.TickPendingDeliveries(isApplyingSaveData);
        }

        if (IsPlayerViewInteractionPressed())
        {
            HandlePlayerViewInteraction();
        }

        if (carriedRestockBox != null)
        {
            UpdateCarriedRestockBoxPosition();

            if (GetRightMouseButtonDown())
            {
                ThrowCarriedRestockBox();
            }

            return;
        }

        if (carriedEmptyBox != null)
        {
            UpdateCarriedEmptyBoxPosition();

            if (GetLeftMouseButtonDown())
            {
                TryDisposeCarriedEmptyBoxFromPointer();
            }

            return;
        }

        if (deliveryManager != null && deliveryManager.IsCarryingCrate)
        {
            deliveryManager.UpdateCarriedCratePosition();

            if (GetRightMouseButtonDown())
            {
                deliveryManager.ReturnCarriedCrateToDock();
            }

            return;
        }

        EnsureShelfPlacementManager();
        shelfPlacementManager?.HandleUpdate();
        EnsureWarehouseManager();
        warehouseManager?.HandleUpdate();
    }

    public void StartShelfPlacement()
    {
        if (warehouseManager != null && warehouseManager.IsPlacingWarehouseShelf)
        {
            return;
        }

        EnsureShelfPlacementManager();
        shelfPlacementManager?.StartShelfPlacement(moneyManager);
    }

    public void StartWarehouseShelfPlacement()
    {
        if (shelfPlacementManager != null && shelfPlacementManager.IsPlacingShelf)
        {
            return;
        }

        EnsureWarehouseManager();
        warehouseManager?.StartWarehouseShelfPlacement(moneyManager);
    }

    public void CancelShelfPlacement()
    {
        EnsureShelfPlacementManager();
        shelfPlacementManager?.CancelShelfPlacement();
        EnsureWarehouseManager();
        warehouseManager?.CancelWarehouseShelfPlacement();
    }

    public bool StartMovingShelf(Shelf shelf)
    {
        EnsureDeliveryManager();
        if (shelf == null ||
            IsPlacingShelf ||
            carriedRestockBox != null ||
            carriedEmptyBox != null ||
            (deliveryManager != null && deliveryManager.IsCarryingCrate))
        {
            return false;
        }

        EnsureShelfPlacementManager();
        return shelfPlacementManager != null && shelfPlacementManager.StartMovingShelf(shelf);
    }

    public bool StartMovingSelectedOrFocusedShelf()
    {
        return StartMovingShelf(GetSelectedOrFocusedShelf());
    }

    public void NextDay()
    {
        HandleDayControl();
    }

    public void HandleDayControl()
    {
        EnsureDayNightCycle();
        if (dayNightCycle == null)
        {
            return;
        }

        if (dayNightCycle.CanStartDay)
        {
            nextDayCustomerWarningPending = false;
            dayNightCycle.StartDay();
            NotifyStateChanged();
            return;
        }

        if (!dayNightCycle.CanAdvanceToNextDay)
        {
            return;
        }

        int activeCustomerCount = GetActiveCustomerCount();
        if (activeCustomerCount > 0 && !nextDayCustomerWarningPending)
        {
            nextDayCustomerWarningPending = true;
            return;
        }

        AdvanceToNextMorning();
    }

    public void HandleDayReachedClosingTime()
    {
        nextDayCustomerWarningPending = false;
        NotifyStateChanged();
        SaveGame();
    }

    public void SkipToClosingTimeForTesting()
    {
        EnsureDayNightCycle();
        if (dayNightCycle == null)
        {
            return;
        }

        dayNightCycle.SkipToClosingTime();
    }

    public int GetBackroomStock(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int physicalStock = 0;
        EnsureWarehouseManager();
        if (warehouseManager != null)
        {
            physicalStock = warehouseManager.GetStoredAmount(product);
        }

        return physicalStock + productInventory.GetLegacyBackroomStock(product);
    }

    public bool TakeFromBackroom(ProductData product, int amount)
    {
        return false;
    }

    public bool BuyProductStock(ProductData product, int amount)
    {
        if (product == null || amount <= 0 || moneyManager == null)
        {
            return false;
        }

        float totalCost = product.wholesaleCost * amount;
        if (!moneyManager.SpendMoney(totalCost))
        {
            return false;
        }

        dailyProductOrderCosts += totalCost;
        EnsureDeliveryManager();
        if (deliveryManager != null)
        {
            deliveryManager.QueueDelivery(product, amount);
        }
        NotifyStateChanged();
        return true;
    }

    public void AddToBackroom(ProductData product, int amount)
    {
        if (product == null || amount <= 0)
        {
            return;
        }

        productInventory.AddToBackroom(product, amount);
    }

    public void SetDefaultShelfProduct(ProductData product)
    {
        defaultShelfProduct = product;
        RegisterKnownProduct(product);
        NotifyStateChanged();
    }

    public void RegisterKnownProducts(IEnumerable<ProductData> products)
    {
        productInventory.RegisterKnownProducts(products);
    }

    public void RegisterKnownProduct(ProductData product)
    {
        productInventory.RegisterKnownProduct(product);
        productPricing.GetSalePrice(product);
    }

    public float GetProductSalePrice(ProductData product)
    {
        return productPricing.GetSalePrice(product);
    }

    public void SetProductSalePrice(ProductData product, float price)
    {
        productPricing.SetSalePrice(product, price);
        NotifyStateChanged();
        RefreshShelfLabels();
    }

    public void ResetProductSalePrice(ProductData product)
    {
        productPricing.ResetSalePrice(product);
        NotifyStateChanged();
        RefreshShelfLabels();
    }

    public void RecordCustomerPurchase(ProductData product)
    {
        if (product == null)
        {
            return;
        }

        dailyItemsSold++;
        float salePrice = productPricing.GetSalePrice(product);
        dailySalesRevenue += salePrice;
        dailyWholesaleCostOfGoods += Mathf.Max(0f, product.wholesaleCost);
        NotifyStateChanged();
    }

    public void RecordCustomerShoppingFeedback(int missingItemCount, int tooExpensiveItemCount)
    {
        dailyCustomerTrips++;
        dailyMissingItems += Mathf.Max(0, missingItemCount);
        dailyTooExpensiveItems += Mathf.Max(0, tooExpensiveItemCount);
        NotifyStateChanged();
    }

    public string GetDailyCustomerFeedbackReport()
    {
        return "Today's Feedback" +
               "\nItems sold: " + dailyItemsSold +
               "\nCould not find: " + dailyMissingItems +
               "\nToo expensive: " + dailyTooExpensiveItems;
    }

    public string GetEndOfDayReportText()
    {
        float grossProfit = dailySalesRevenue - dailyWholesaleCostOfGoods;
        float netProfit = grossProfit - dailyProductOrderCosts;

        return "END OF DAY REPORT - DAY " + CurrentDay +
               "\nPerformance: " + GetDailyPerformanceSummary(netProfit) +
               "\n\nSales: $" + dailySalesRevenue.ToString("0.00") +
               "\nProduct costs: $" + dailyWholesaleCostOfGoods.ToString("0.00") +
               "\nStock ordered: $" + dailyProductOrderCosts.ToString("0.00") +
               "\nProfit: $" + netProfit.ToString("0.00") +
               "\n\nCustomers: " + dailyCustomerTrips +
               "\nItems sold: " + dailyItemsSold +
               "\nToo expensive: " + dailyTooExpensiveItems +
               "\nCould not find: " + dailyMissingItems;
    }

    private string GetDailyPerformanceSummary(float netProfit)
    {
        if (dailyCustomerTrips <= 0)
        {
            return "No customer traffic yet";
        }

        if (dailyTooExpensiveItems > dailyItemsSold)
        {
            return "Prices felt too high";
        }

        if (dailyMissingItems > dailyItemsSold)
        {
            return "Low stock hurt sales";
        }

        if (netProfit > 0f && dailyItemsSold >= dailyCustomerTrips)
        {
            return "Great day";
        }

        if (netProfit > 0f)
        {
            return "Profitable day";
        }

        return "Needs improvement";
    }

    public bool ShouldShowEndOfDayReport => dayNightCycle != null &&
                                            dayNightCycle.CurrentPhase == DayNightCycle.DayPhase.AfterClose;

    public void NotifyStateChanged()
    {
        if (isApplyingSaveData || !hasLoadedSaveData)
        {
            return;
        }

        isStateDirty = true;
    }

    public int GetIncomingDeliveryAmount(ProductData product)
    {
        EnsureDeliveryManager();
        return deliveryManager != null ? deliveryManager.GetIncomingDeliveryAmount(product) : 0;
    }

    public float GetSoonestDeliveryTime(ProductData product)
    {
        EnsureDeliveryManager();
        return deliveryManager != null ? deliveryManager.GetSoonestDeliveryTime(product) : -1f;
    }

    public int GetDockDeliveryAmount(ProductData product)
    {
        EnsureDeliveryManager();
        return deliveryManager != null ? deliveryManager.GetDockDeliveryAmount(product) : 0;
    }

    public bool HasAnyDockCrates()
    {
        EnsureDeliveryManager();
        return deliveryManager != null && deliveryManager.HasAnyDockCrates();
    }

    public bool IsCarryingDeliveryCrate()
    {
        EnsureDeliveryManager();
        return deliveryManager != null && deliveryManager.IsCarryingCrate;
    }

    public bool IsCarryingRestockBox => carriedRestockBox != null;
    public bool IsCarryingEmptyBox => carriedEmptyBox != null;
    public ProductData CarriedRestockProduct => carriedRestockBox != null ? carriedRestockBox.Product : null;
    public int CarriedRestockAmount => carriedRestockBox != null ? carriedRestockBox.Amount : 0;
    public MoneyManager MoneyManager => moneyManager;
    public DeliveryManager DeliveryManager => deliveryManager;
    public WarehouseManager WarehouseManager => warehouseManager;
    public TrashManager TrashManager => trashManager;
    public DayNightCycle DayNightCycle => dayNightCycle;
    public bool IsApplyingSaveData => isApplyingSaveData;
    public bool IsStoreOpen => dayNightCycle == null || dayNightCycle.IsStoreOpen;
    public bool IsClockRunning => dayNightCycle != null && dayNightCycle.IsClockRunning;
    public bool CanSaveGame => hasLoadedSaveData && !isApplyingSaveData && dayNightCycle != null && dayNightCycle.IsAtSaveBoundary;
    public bool HasUnsavedChanges => isStateDirty;
    public bool CanUseDayControl => dayNightCycle == null || dayNightCycle.CanStartDay || dayNightCycle.CanAdvanceToNextDay;
    public string StoreClockText => dayNightCycle != null ? dayNightCycle.GetClockText() : "8:00 AM";
    public string StoreStateText => dayNightCycle != null ? dayNightCycle.StoreStateText : "Open";
    public string DayControlButtonText
    {
        get
        {
            if (dayNightCycle == null || dayNightCycle.CanStartDay)
            {
                return "Start Day";
            }

            return dayNightCycle.CanAdvanceToNextDay ? "Next Day" : "Day In Progress";
        }
    }

    public string GetStoreTaskStatus()
    {
        if (nextDayCustomerWarningPending && GetActiveCustomerCount() > 0)
        {
            return CustomersStillInsideWarning;
        }

        if (IsMovingShelf)
        {
            return "Moving shelf\nLeft click to place it, or right click to cancel.";
        }

        if (IsPlacingShelf)
        {
            return warehouseManager != null && warehouseManager.IsPlacingWarehouseShelf
                ? "Placing warehouse shelf\nLeft click on the floor to place it, or right click to cancel."
                : "Placing new shelf\nLeft click on the floor to place it, or right click to cancel.";
        }

        if (carriedRestockBox != null && carriedRestockBox.Product != null)
        {
            return "Carrying stock: " + carriedRestockBox.Product.productName + " x" + carriedRestockBox.Amount +
                   "\nUse [E] to stock/place it, or right click to throw it down.";
        }

        if (carriedEmptyBox != null)
        {
            return "Carrying empty box\nThrow it in the Baler";
        }

        EnsureDeliveryManager();
        string deliveryTaskStatus = deliveryManager != null ? deliveryManager.GetTaskStatus() : string.Empty;
        if (!string.IsNullOrEmpty(deliveryTaskStatus))
        {
            return deliveryTaskStatus;
        }

        return "No active carry tasks";
    }

    public string GetPlayerInteractionPrompt()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null ? playerInteractionManager.GetPlayerInteractionPrompt() : string.Empty;
    }

    public bool PrepareRestockForShelf(Shelf shelf)
    {
        Debug.Log("Automatic restock preparation is disabled. Pick up a box from a warehouse shelf manually.");
        return false;
    }

    public Shelf GetFocusedShelf()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null ? playerInteractionManager.GetFocusedShelf() : null;
    }

    public int GetActiveCustomerCount()
    {
        int activeCustomerCount = 0;
        CustomerSpawner[] customerSpawners = FindObjectsByType<CustomerSpawner>();
        foreach (CustomerSpawner customerSpawner in customerSpawners)
        {
            if (customerSpawner != null)
            {
                activeCustomerCount += customerSpawner.ActiveCustomerCount;
            }
        }

        return activeCustomerCount;
    }

    private void ClearActiveCustomers()
    {
        CustomerSpawner[] customerSpawners = FindObjectsByType<CustomerSpawner>();
        foreach (CustomerSpawner customerSpawner in customerSpawners)
        {
            if (customerSpawner != null)
            {
                customerSpawner.ClearActiveCustomers();
            }
        }
    }

    private void BuildStartingInventory()
    {
        productInventory.BuildStartingInventory(startingInventory);
    }

    private void RegisterKnownProductsFromInitialData()
    {
        foreach (ProductInventoryEntry entry in startingInventory)
        {
            RegisterKnownProduct(entry.product);
        }

        RegisterKnownProduct(defaultShelfProduct);
    }

    private void ClearRestockBoxes()
    {
        carriedRestockBox = null;
        ClearCarriedEmptyBox();

        foreach (RestockBox box in activeRestockBoxes)
        {
            if (box != null)
            {
                Destroy(box.gameObject);
            }
        }

        activeRestockBoxes.Clear();
    }

    private void EnsureBackroomZoneVisual()
    {
        EnsureWarehouseManager();
        if (warehouseManager != null)
        {
            warehouseManager.EnsureVisual();
        }
    }

    private void RefreshWarehouseStockBoxes()
    {
        MigrateBackroomInventoryToWarehouseShelves();
    }

    private void MigrateBackroomInventoryToWarehouseShelves()
    {
        EnsureWarehouseManager();
        productInventory.MigrateLegacyBackroomInventory(warehouseManager);
    }

    public void HandleWarehouseStockBoxClicked(WarehouseStockBox stockBox)
    {
        Debug.Log("Legacy warehouse stock boxes are no longer used. Place crates into warehouse shelf slots.");
    }

    public void HandleDeliveryCrateClicked(DeliveryCrate crate)
    {
        EnsureDeliveryManager();
        if (deliveryManager != null)
        {
            deliveryManager.HandleCrateClicked(crate);
        }
    }

    public void HandleWarehouseShelfClicked(WarehouseShelf shelf)
    {
        EnsureDeliveryManager();
        EnsureWarehouseManager();
        if (shelf == null || IsPlacingShelf || carriedEmptyBox != null || warehouseManager == null)
        {
            return;
        }

        if (deliveryManager != null && deliveryManager.IsCarryingCrate)
        {
            deliveryManager.TryPlaceCarriedCrateOnWarehouseShelf(shelf);
            return;
        }

        if (carriedRestockBox != null)
        {
            TryReturnCarriedRestockBoxToWarehouseShelf(shelf);
            return;
        }

        warehouseManager.TryPickUpBoxFromShelf(shelf, GetSelectedOrFocusedShelf());
    }

    public bool TryPlaceBoxOnWarehouseShelf(WarehouseShelf shelf, ProductData product, int amount)
    {
        EnsureWarehouseManager();
        return warehouseManager != null && warehouseManager.TryPlaceBoxOnShelf(shelf, product, amount);
    }

    public void HandleShelfClicked(Shelf shelf)
    {
        if (shelf == null || IsPlacingShelf)
        {
            return;
        }

        BasicUIManager uiManager = BasicUIManager.Instance;
        if (uiManager != null)
        {
            uiManager.SetSelectedShelf(shelf);
        }

        if (carriedRestockBox != null)
        {
            TryApplyRestockBoxToShelf(shelf);
            return;
        }

        EnsureDeliveryManager();
        if (deliveryManager != null && deliveryManager.IsCarryingCrate)
        {
            return;
        }
    }

    private void HandlePlayerViewInteraction()
    {
        EnsurePlayerInteractionManager();
        playerInteractionManager?.HandlePlayerViewInteraction();
    }

    private RestockBox SpawnRestockBox(ProductData product, int amount, Vector3 position, bool carried)
    {
        if (product == null || amount <= 0)
        {
            return null;
        }

        EnsureBackroomZoneVisual();

        GameObject boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.name = product.productName + "_RestockBox";
        boxObject.transform.position = position;

        RestockBox restockBox = boxObject.AddComponent<RestockBox>();
        restockBox.Setup(product, amount);
        restockBox.SetCarriedState(carried);
        activeRestockBoxes.Add(restockBox);

        if (carried)
        {
            carriedRestockBox = restockBox;
        }

        return restockBox;
    }

    public void SpawnCarriedWarehouseBox(ProductData product, int amount, Vector3 position)
    {
        SpawnRestockBox(product, amount, position, true);
    }

    public void HandleRestockBoxClicked(RestockBox restockBox)
    {
        if (restockBox == null ||
            restockBox == carriedRestockBox ||
            carriedRestockBox != null ||
            carriedEmptyBox != null ||
            IsPlacingShelf ||
            (deliveryManager != null && deliveryManager.IsCarryingCrate))
        {
            return;
        }

        if (!activeRestockBoxes.Contains(restockBox))
        {
            activeRestockBoxes.Add(restockBox);
        }

        carriedRestockBox = restockBox;
        carriedRestockBox.SetCarriedState(true);
        UpdateCarriedRestockBoxPosition();
        NotifyStateChanged();
    }

    private void UpdateCarriedRestockBoxPosition()
    {
        if (carriedRestockBox == null)
        {
            return;
        }

        carriedRestockBox.transform.position = GetHeldObjectPosition();
    }

    private void TryApplyRestockBoxToShelf(Shelf shelf)
    {
        if (carriedRestockBox == null || shelf == null)
        {
            return;
        }

        ProductData carriedProduct = carriedRestockBox.Product;
        if (!shelf.CanAcceptRestock(carriedProduct))
        {
            return;
        }

        int addedAmount = shelf.AddStock(carriedProduct, carriedRestockBox.Amount);
        int leftoverAmount = carriedRestockBox.Amount - addedAmount;

        if (leftoverAmount > 0)
        {
            carriedRestockBox.SetAmount(leftoverAmount);
            NotifyStateChanged();
            return;
        }

        activeRestockBoxes.Remove(carriedRestockBox);
        Destroy(carriedRestockBox.gameObject);
        carriedRestockBox = null;
        CreateCarriedEmptyBox(carriedProduct);
        NotifyStateChanged();
    }

    private void TryReturnCarriedRestockBoxToWarehouseShelf(WarehouseShelf shelf)
    {
        if (carriedRestockBox == null || shelf == null)
        {
            return;
        }

        EnsureWarehouseManager();
        if (warehouseManager == null ||
            !warehouseManager.TryReturnWarehouseBoxToShelf(shelf, carriedRestockBox.Product, carriedRestockBox.Amount))
        {
            return;
        }

        activeRestockBoxes.Remove(carriedRestockBox);
        Destroy(carriedRestockBox.gameObject);
        carriedRestockBox = null;
        NotifyStateChanged();
    }

    private void CancelCarriedRestockBox()
    {
        if (carriedRestockBox == null)
        {
            return;
        }

        EnsureWarehouseManager();
        if (warehouseManager == null ||
            !warehouseManager.TryReturnWarehouseBox(carriedRestockBox.Product, carriedRestockBox.Amount))
        {
            Debug.Log("No compatible warehouse shelf slot is available for this box.");
            return;
        }

        activeRestockBoxes.Remove(carriedRestockBox);
        Destroy(carriedRestockBox.gameObject);
        carriedRestockBox = null;
        NotifyStateChanged();
    }

    private void ThrowCarriedRestockBox()
    {
        if (carriedRestockBox == null)
        {
            return;
        }

        Vector3 dropPosition = carriedRestockBox.transform.position;
        if (TryGetPlacementPositionFromMouse(out Vector3 pointerPosition))
        {
            dropPosition = pointerPosition;
        }
        else if (mainCamera != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude > 0.01f)
            {
                dropPosition = mainCamera.transform.position + forward * 1.4f;
            }
        }

        dropPosition.y = 0.24f;
        carriedRestockBox.transform.position = dropPosition;
        carriedRestockBox.SetCarriedState(false);
        carriedRestockBox = null;
        NotifyStateChanged();
    }

    public void CreateCarriedEmptyBox(ProductData sourceProduct)
    {
        ClearCarriedEmptyBox();

        GameObject boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.name = sourceProduct != null ? sourceProduct.productName + "_EmptyBox" : "EmptyBox";
        boxObject.transform.position = GetHeldObjectPosition();

        carriedEmptyBox = boxObject.AddComponent<EmptyBox>();
        carriedEmptyBox.Setup(sourceProduct);
        carriedEmptyBox.SetCarriedState(true);
        NotifyStateChanged();
    }

    private void UpdateCarriedEmptyBoxPosition()
    {
        if (carriedEmptyBox == null)
        {
            return;
        }

        carriedEmptyBox.transform.position = GetHeldObjectPosition();
    }

    public bool TryDisposeCarriedEmptyBoxFromPointer()
    {
        if (carriedEmptyBox == null)
        {
            return false;
        }

        EnsureTrashManager();
        if (trashManager == null ||
            !TryGetPlacementPositionFromMouse(out Vector3 placementPosition) ||
            !trashManager.IsPointInsideZone(placementPosition))
        {
            return false;
        }

        ClearCarriedEmptyBox();
        NotifyStateChanged();
        return true;
    }

    private void ClearCarriedEmptyBox()
    {
        if (carriedEmptyBox != null)
        {
            Destroy(carriedEmptyBox.gameObject);
            carriedEmptyBox = null;
        }
    }

    public bool TryGetPlacementPositionFromMouse(out Vector3 placementPosition)
    {
        EnsureShelfPlacementManager();
        if (shelfPlacementManager != null)
        {
            return shelfPlacementManager.TryGetPlacementPosition(out placementPosition);
        }

        placementPosition = Vector3.zero;
        return false;
    }

    public void PlacePendingShelfFromCurrentPointer()
    {
        EnsureShelfPlacementManager();
        if (shelfPlacementManager != null && shelfPlacementManager.IsPlacingShelf)
        {
            shelfPlacementManager.TryPlaceShelfFromCurrentPointer();
            return;
        }

        EnsureWarehouseManager();
        if (warehouseManager != null && warehouseManager.IsPlacingWarehouseShelf)
        {
            warehouseManager.TryPlaceWarehouseShelfFromCurrentPointer();
        }
    }

    public Shelf GetSelectedOrFocusedShelf()
    {
        if (BasicUIManager.Instance != null && BasicUIManager.Instance.SelectedShelf != null)
        {
            return BasicUIManager.Instance.SelectedShelf;
        }

        return GetFocusedShelf();
    }

    public bool TryPickUpWarehouseStockForShelf(WarehouseStockBox stockBox, Shelf shelf)
    {
        if (stockBox == null)
        {
            return false;
        }

        if (shelf != null && !shelf.CanAcceptProduct(stockBox.Product))
        {
            return false;
        }

        int amountToCarry = Mathf.Min(restockBoxCarryAmount, stockBox.Amount);
        if (shelf != null)
        {
            amountToCarry = Mathf.Min(amountToCarry, shelf.SpaceRemaining);
        }

        if (amountToCarry <= 0)
        {
            return false;
        }

        if (!TakeFromBackroom(stockBox.Product, amountToCarry))
        {
            return false;
        }

        SpawnRestockBox(stockBox.Product, amountToCarry, GetHeldObjectPosition(), true);
        NotifyStateChanged();
        return true;
    }

    public IEnumerable<ProductData> GetKnownProducts()
    {
        return productInventory.GetKnownProducts();
    }

    private void RefreshShelfLabels()
    {
        Shelf[] shelves = FindObjectsByType<Shelf>();
        foreach (Shelf shelf in shelves)
        {
            shelf?.RefreshDisplay();
        }
    }

    public Vector3 GetHeldObjectPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return carriedCrateOffset;
        }

        Transform cameraTransform = mainCamera.transform;
        Vector3 holdPosition = cameraTransform.position +
                               (cameraTransform.forward * heldObjectForwardDistance) +
                               (cameraTransform.right * heldObjectSideOffset) +
                               (Vector3.up * heldObjectVerticalOffset);

        return holdPosition;
    }

    public Material CreateRuntimeMaterial(Color color)
    {
        EnsureRuntimeSceneManager();
        return runtimeSceneManager != null
            ? runtimeSceneManager.CreateRuntimeMaterial(color)
            : new Material(Shader.Find("Standard")) { color = color };
    }


    private IEnumerator LoadGameAfterSceneSetup()
    {
        yield return null;

        LoadGame();
        hasLoadedSaveData = true;
        QueueStoreNavigationRebuild();
    }

    public bool CanPersistState => CanSaveGame;

    public void SaveGame()
    {
        EnsureSaveSystem();
        if (saveSystem != null)
        {
            if (!CanSaveGame)
            {
                return;
            }

            saveSystem.SaveGame();
            isStateDirty = false;
        }
    }

    public bool HasSaveGame()
    {
        EnsureSaveSystem();
        return saveSystem != null && saveSystem.HasSaveGame();
    }

    public void LoadGame()
    {
        EnsureSaveSystem();
        if (saveSystem != null)
        {
            saveSystem.LoadGame();
        }
    }

    public void StartNewGame()
    {
        EnsureSaveSystem();
        if (saveSystem != null)
        {
            saveSystem.StartNewGame();
        }
    }

    public void ResetToNewGameState()
    {
        isApplyingSaveData = true;

        try
        {
            CancelShelfPlacement();

            Shelf[] existingShelves = FindObjectsByType<Shelf>();
            foreach (Shelf shelf in existingShelves)
            {
                if (shelf != null)
                {
                    Destroy(shelf.gameObject);
                }
            }

            productInventory.ClearBackroomInventory();
            EnsureDeliveryManager();
            if (deliveryManager != null)
            {
                deliveryManager.ClearState();
            }
            ClearRestockBoxes();
            EnsureWarehouseManager();
            warehouseManager?.ClearWarehouseShelves();
            productPricing.ResetAll();
            BuildStartingInventory();
            MigrateBackroomInventoryToWarehouseShelves();
            CurrentDay = startingDay;
            EnsureDayNightCycle();
            dayNightCycle?.ResetToOpeningTime();
            nextDayCustomerWarningPending = false;
            ResetDailyCustomerFeedback();
            isStateDirty = true;
            defaultShelfProduct = initialDefaultShelfProduct;
            EnsureShelfPlacementManager();
            shelfPlacementManager?.ResetPlacementCount();

            if (moneyManager != null)
            {
                moneyManager.ResetToStartingMoney();
            }

            if (BasicUIManager.Instance != null)
            {
                BasicUIManager.Instance.SyncSelectedProduct(defaultShelfProduct);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to start a new game: " + exception.Message);
        }
        finally
        {
            isApplyingSaveData = false;
        }

        QueueStoreNavigationRebuild();
    }

    public GameSaveData CaptureSaveData()
    {
        GameSaveData saveData = new GameSaveData
        {
            currentDay = CurrentDay,
            currentMoney = moneyManager != null ? moneyManager.CurrentMoney : 0f,
            selectedProductName = defaultShelfProduct != null ? defaultShelfProduct.productName : string.Empty,
            hasDayNightCycleState = dayNightCycle != null,
            hasDayPhaseState = dayNightCycle != null,
            dayPhase = dayNightCycle != null ? (int)dayNightCycle.CurrentPhase : (int)DayNightCycle.DayPhase.BeforeOpen,
            currentTimeOfDayHours = dayNightCycle != null ? dayNightCycle.CurrentTimeOfDayHours : 8f,
            currentDayProgress = dayNightCycle != null ? dayNightCycle.CurrentDayProgress : 0f,
            isStoreOpen = dayNightCycle == null || dayNightCycle.IsStoreOpen,
            dailyItemsSold = dailyItemsSold,
            dailyMissingItems = dailyMissingItems,
            dailyTooExpensiveItems = dailyTooExpensiveItems,
            dailyCustomerTrips = dailyCustomerTrips,
            dailySalesRevenue = dailySalesRevenue,
            dailyWholesaleCostOfGoods = dailyWholesaleCostOfGoods,
            dailyProductOrderCosts = dailyProductOrderCosts
        };

        MigrateBackroomInventoryToWarehouseShelves();

        foreach (KeyValuePair<ProductData, int> entry in productInventory.GetBackroomEntries())
        {
            if (entry.Key == null)
            {
                continue;
            }

            saveData.backroomInventory.Add(new InventorySaveEntry
            {
                productName = entry.Key.productName,
                amount = entry.Value
            });
        }

        EnsureWarehouseManager();
        if (warehouseManager != null)
        {
            saveData.warehouseShelves = warehouseManager.BuildWarehouseShelfStates();
        }

        Shelf[] shelves = FindObjectsByType<Shelf>();
        foreach (Shelf shelf in shelves)
        {
            if (shelf == null)
            {
                continue;
            }

            saveData.shelves.Add(new ShelfSaveEntry
            {
                productName = shelf.AssignedProduct != null ? shelf.AssignedProduct.productName : string.Empty,
                stock = shelf.CurrentStock,
                position = shelf.transform.position
            });
        }

        EnsureDeliveryManager();
        if (deliveryManager != null)
        {
            saveData.deliveries = deliveryManager.BuildQueuedDeliveryStates();
            saveData.deliveryCrates = deliveryManager.BuildDockCrateStates();
        }

        foreach (RestockBox box in activeRestockBoxes)
        {
            if (box == null || box.Product == null)
            {
                continue;
            }

            saveData.restockBoxes.Add(new RestockBoxSaveEntry
            {
                productName = box.Product.productName,
                amount = box.Amount,
                position = box.transform.position,
                isCarried = box == carriedRestockBox
            });
        }

        saveData.productPrices = productPricing.BuildSaveEntries();

        return saveData;
    }

    public void ApplySaveData(GameSaveData saveData)
    {
        isApplyingSaveData = true;

        try
        {
            CancelShelfPlacement();

            productInventory.ClearBackroomInventory();
            EnsureDeliveryManager();
            if (deliveryManager != null)
            {
                deliveryManager.ClearState();
            }
            ClearRestockBoxes();
            EnsureWarehouseManager();
            warehouseManager?.ClearWarehouseShelves();
            productPricing.ResetAll();
            CurrentDay = Mathf.Max(1, saveData.currentDay);
            dailyItemsSold = Mathf.Max(0, saveData.dailyItemsSold);
            dailyMissingItems = Mathf.Max(0, saveData.dailyMissingItems);
            dailyTooExpensiveItems = Mathf.Max(0, saveData.dailyTooExpensiveItems);
            dailyCustomerTrips = Mathf.Max(0, saveData.dailyCustomerTrips);
            dailySalesRevenue = Mathf.Max(0f, saveData.dailySalesRevenue);
            dailyWholesaleCostOfGoods = Mathf.Max(0f, saveData.dailyWholesaleCostOfGoods);
            dailyProductOrderCosts = Mathf.Max(0f, saveData.dailyProductOrderCosts);
            EnsureDayNightCycle();
            if (dayNightCycle != null)
            {
                dayNightCycle.ApplySavedState(
                    saveData.currentTimeOfDayHours,
                    saveData.currentDayProgress,
                    saveData.dayPhase,
                    saveData.hasDayPhaseState,
                    saveData.hasDayNightCycleState);
            }
            nextDayCustomerWarningPending = false;
            isStateDirty = false;

            if (moneyManager != null)
            {
                moneyManager.SetMoney(saveData.currentMoney);
            }

            if (!string.IsNullOrWhiteSpace(saveData.selectedProductName))
            {
                defaultShelfProduct = ResolveProductByName(saveData.selectedProductName);
            }

            foreach (InventorySaveEntry entry in saveData.backroomInventory)
            {
                ProductData product = ResolveProductByName(entry.productName);
                if (product != null)
                {
                    AddToBackroom(product, entry.amount);
                }
            }

            EnsureDeliveryManager();
            foreach (DeliveryManager.QueuedDeliveryState deliveryEntry in saveData.deliveries)
            {
                ProductData product = ResolveProductByName(deliveryEntry.productName);
                if (product == null || deliveryEntry.amount <= 0)
                {
                    continue;
                }

                if (deliveryManager != null)
                {
                    deliveryManager.LoadQueuedDelivery(product, deliveryEntry.amount, deliveryEntry.remainingTime);
                }
            }

            foreach (DeliveryManager.DockCrateState crateEntry in saveData.deliveryCrates)
            {
                ProductData product = ResolveProductByName(crateEntry.productName);
                if (product == null || crateEntry.amount <= 0)
                {
                    continue;
                }

                if (deliveryManager != null)
                {
                    deliveryManager.LoadDockCrate(product, crateEntry.amount, crateEntry.position);
                }
            }

            foreach (RestockBoxSaveEntry boxEntry in saveData.restockBoxes)
            {
                ProductData product = ResolveProductByName(boxEntry.productName);
                if (product == null || boxEntry.amount <= 0)
                {
                    continue;
                }

                SpawnRestockBox(product, boxEntry.amount, boxEntry.position, boxEntry.isCarried);
            }

            if (saveData.productPrices != null)
            {
                foreach (ProductPriceSaveEntry priceEntry in saveData.productPrices)
                {
                    ProductData product = ResolveProductByName(priceEntry.productName);
                    if (product != null)
                    {
                        productPricing.SetSalePrice(product, priceEntry.salePrice);
                    }
                }
            }

            if (warehouseManager != null && saveData.warehouseShelves != null)
            {
                foreach (WarehouseShelfSaveEntry warehouseShelfEntry in saveData.warehouseShelves)
                {
                    ProductData productLock = ResolveProductByName(warehouseShelfEntry.lockedProductName);
                    List<WarehouseShelfSlotRuntimeState> loadedSlots = new List<WarehouseShelfSlotRuntimeState>();

                    if (warehouseShelfEntry.slots != null)
                    {
                        foreach (WarehouseShelfSlotSaveEntry slotEntry in warehouseShelfEntry.slots)
                        {
                            ProductData product = ResolveProductByName(slotEntry.productName);
                            if (product == null || slotEntry.amount <= 0)
                            {
                                continue;
                            }

                            loadedSlots.Add(new WarehouseShelfSlotRuntimeState
                            {
                                slotIndex = slotEntry.slotIndex,
                                product = product,
                                amount = slotEntry.amount
                            });
                        }
                    }

                    warehouseManager.SpawnLoadedWarehouseShelf(warehouseShelfEntry.position, productLock, loadedSlots);
                }
            }

            Shelf[] existingShelves = FindObjectsByType<Shelf>();
            foreach (Shelf shelf in existingShelves)
            {
                if (shelf != null)
                {
                    Destroy(shelf.gameObject);
                }
            }

            EnsureShelfPlacementManager();
            shelfPlacementManager?.ResetPlacementCount();

            foreach (ShelfSaveEntry shelfEntry in saveData.shelves)
            {
                ProductData product = ResolveProductByName(shelfEntry.productName);
                shelfPlacementManager?.SpawnLoadedShelf(shelfEntry.position, product, shelfEntry.stock);
            }

            if (BasicUIManager.Instance != null)
            {
                BasicUIManager.Instance.SyncSelectedProduct(defaultShelfProduct);
            }

            MigrateBackroomInventoryToWarehouseShelves();
            RefreshShelfLabels();
        }
        finally
        {
            isApplyingSaveData = false;
        }

        QueueStoreNavigationRebuild();
    }

    private ProductData ResolveProductByName(string productName)
    {
        return productInventory.ResolveProductByName(productName);
    }

    private void OnApplicationQuit()
    {
        EnsureSaveSystem();
        if (saveSystem != null)
        {
            saveSystem.SaveOnApplicationQuit();
        }
    }

    private void AdvanceToNextMorning()
    {
        ClearActiveCustomers();
        CurrentDay++;
        ResetDailyCustomerFeedback();
        EnsureDayNightCycle();
        dayNightCycle?.StartNextMorning();
        nextDayCustomerWarningPending = false;
        NotifyStateChanged();
        SaveGame();
    }

    private void ResetDailyCustomerFeedback()
    {
        dailyItemsSold = 0;
        dailyMissingItems = 0;
        dailyTooExpensiveItems = 0;
        dailyCustomerTrips = 0;
        dailySalesRevenue = 0f;
        dailyWholesaleCostOfGoods = 0f;
        dailyProductOrderCosts = 0f;
    }

    public bool GetLeftMouseButtonDown()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null && playerInteractionManager.GetLeftMouseButtonDown();
    }

    public bool GetRightMouseButtonDown()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null && playerInteractionManager.GetRightMouseButtonDown();
    }

    public bool IsLeftMouseButtonHeld()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null && playerInteractionManager.IsLeftMouseButtonHeld();
    }

    private bool IsPlayerViewInteractionPressed()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null && playerInteractionManager.IsPlayerViewInteractionPressed();
    }

    public Vector3 GetMouseScreenPosition()
    {
        EnsurePlayerInteractionManager();
        return playerInteractionManager != null ? playerInteractionManager.GetMouseScreenPosition() : Vector3.zero;
    }

    private void EnsurePlayerController()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || mainCamera.GetComponent<FirstPersonPlayerController>() != null)
        {
            return;
        }

        mainCamera.gameObject.AddComponent<FirstPersonPlayerController>();
    }

    private void EnsurePlayerInteractionManager()
    {
        if (playerInteractionManager != null)
        {
            return;
        }

        playerInteractionManager = GetComponent<PlayerInteractionManager>();
        if (playerInteractionManager == null)
        {
            playerInteractionManager = gameObject.AddComponent<PlayerInteractionManager>();
        }

        playerInteractionManager.Initialize(this, mainCamera, interactionDistance);
    }

    private void EnsureSaveSystem()
    {
        if (saveSystem != null)
        {
            return;
        }

        saveSystem = GetComponent<GameSaveSystem>();
        if (saveSystem == null)
        {
            saveSystem = gameObject.AddComponent<GameSaveSystem>();
        }

        saveSystem.Initialize(this);
    }

    private void EnsureDayNightCycle()
    {
        if (dayNightCycle != null)
        {
            return;
        }

        dayNightCycle = GetComponent<DayNightCycle>();
        if (dayNightCycle == null)
        {
            dayNightCycle = gameObject.AddComponent<DayNightCycle>();
        }

        dayNightCycle.Initialize(this);
    }

    private void EnsureDeliveryManager()
    {
        if (deliveryManager != null)
        {
            return;
        }

        deliveryManager = GetComponent<DeliveryManager>();
        if (deliveryManager == null)
        {
            deliveryManager = gameObject.AddComponent<DeliveryManager>();
        }

        deliveryManager.Initialize(this);
    }

    private void EnsureShelfPlacementManager()
    {
        if (shelfPlacementManager != null)
        {
            return;
        }

        shelfPlacementManager = GetComponent<ShelfPlacementManager>();
        if (shelfPlacementManager == null)
        {
            shelfPlacementManager = gameObject.AddComponent<ShelfPlacementManager>();
        }

        shelfPlacementManager.Initialize(
            this,
            mainCamera,
            shelfPrefab,
            floorLayer,
            shelfBuyCost,
            placementHeightOffset,
            autoPlaceShelves,
            firstShelfPosition,
            shelfSpacing,
            shelfPreviewColor);
    }

    private void EnsureWarehouseManager()
    {
        if (warehouseManager != null)
        {
            return;
        }

        warehouseManager = GetComponent<WarehouseManager>();
        if (warehouseManager == null)
        {
            warehouseManager = gameObject.AddComponent<WarehouseManager>();
        }

        warehouseManager.Initialize(this, mainCamera, floorLayer);
    }

    private void EnsureTrashManager()
    {
        if (trashManager != null)
        {
            return;
        }

        trashManager = GetComponent<TrashManager>();
        if (trashManager == null)
        {
            trashManager = gameObject.AddComponent<TrashManager>();
        }

        trashManager.Initialize(this);
    }

    private void EnsureStoreComputer()
    {
        if (storeComputer != null)
        {
            return;
        }

        storeComputer = FindAnyObjectByType<StoreComputer>();
        if (storeComputer == null)
        {
            GameObject computerObject = new GameObject("StoreComputer");
            storeComputer = computerObject.AddComponent<StoreComputer>();
        }
    }

    private void EnsureRuntimeSceneManager()
    {
        if (runtimeSceneManager != null)
        {
            return;
        }

        runtimeSceneManager = GetComponent<RuntimeSceneManager>();
        if (runtimeSceneManager == null)
        {
            runtimeSceneManager = gameObject.AddComponent<RuntimeSceneManager>();
        }

        runtimeSceneManager.Initialize();
    }

    public void QueueStoreNavigationRebuild()
    {
        EnsureRuntimeSceneManager();
        runtimeSceneManager?.QueueStoreNavigationRebuild();
    }
}
