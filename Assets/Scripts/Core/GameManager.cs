using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

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

    private readonly Dictionary<ProductData, int> backroomInventory = new Dictionary<ProductData, int>();
    private readonly Dictionary<string, ProductData> knownProductsByName = new Dictionary<string, ProductData>(StringComparer.OrdinalIgnoreCase);
    private readonly List<RestockBox> activeRestockBoxes = new List<RestockBox>();
    private const string CustomersStillInsideWarning = "Customers are still inside. Press Next Day again to continue anyway.";

    public int CurrentDay { get; private set; }
    public bool IsPlacingShelf => shelfPlacementManager != null && shelfPlacementManager.IsPlacingShelf;
    public bool IsMovingShelf => shelfPlacementManager != null && shelfPlacementManager.IsMovingShelf;

    private MoneyManager moneyManager;
    private Camera mainCamera;
    private bool isApplyingSaveData;
    private bool hasLoadedSaveData;
    private ProductData initialDefaultShelfProduct;
    private RestockBox carriedRestockBox;
    private EmptyBox carriedEmptyBox;
    private Shader cachedSurfaceShader;
    private NavMeshSurface storeNavMeshSurface;
    private bool navMeshRebuildQueued;
    private DeliveryManager deliveryManager;
    private PlayerInteractionManager playerInteractionManager;
    private GameSaveSystem saveSystem;
    private ShelfPlacementManager shelfPlacementManager;
    private WarehouseManager warehouseManager;
    private TrashManager trashManager;
    private StoreComputer storeComputer;
    private DayNightCycle dayNightCycle;
    private bool isStateDirty;
    private bool nextDayCustomerWarningPending;

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
        EnsureStoreNavigation();

        BuildStartingInventory();
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
                CancelCarriedRestockBox();
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

            if (GetLeftMouseButtonDown())
            {
                bool isInsideDropZone = false;
                EnsureWarehouseManager();
                if (warehouseManager != null &&
                    TryGetPlacementPositionFromMouse(out Vector3 placementPosition))
                {
                    isInsideDropZone = warehouseManager.IsPointInsideDropZone(placementPosition);
                }

                deliveryManager.TryUnloadCarriedCrateAtDropZone(isInsideDropZone);
            }

            if (GetRightMouseButtonDown())
            {
                deliveryManager.ReturnCarriedCrateToDock();
            }

            return;
        }

        EnsureShelfPlacementManager();
        shelfPlacementManager?.HandleUpdate();
    }

    public void StartShelfPlacement()
    {
        EnsureShelfPlacementManager();
        shelfPlacementManager?.StartShelfPlacement(moneyManager);
    }

    public void CancelShelfPlacement()
    {
        EnsureShelfPlacementManager();
        shelfPlacementManager?.CancelShelfPlacement();
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

    public int GetBackroomStock(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        if (backroomInventory.TryGetValue(product, out int amount))
        {
            return amount;
        }

        return 0;
    }

    public bool TakeFromBackroom(ProductData product, int amount)
    {
        if (product == null || amount <= 0)
        {
            return false;
        }

        int currentAmount = GetBackroomStock(product);
        if (currentAmount < amount)
        {
            return false;
        }

        backroomInventory[product] = currentAmount - amount;
        RefreshWarehouseStockBoxes();
        return true;
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

        RegisterKnownProduct(product);

        if (!backroomInventory.ContainsKey(product))
        {
            backroomInventory.Add(product, 0);
        }

        backroomInventory[product] += amount;
        RefreshWarehouseStockBoxes();
    }

    public void SetDefaultShelfProduct(ProductData product)
    {
        defaultShelfProduct = product;
        RegisterKnownProduct(product);
        NotifyStateChanged();
    }

    public void RegisterKnownProducts(IEnumerable<ProductData> products)
    {
        if (products == null)
        {
            return;
        }

        foreach (ProductData product in products)
        {
            RegisterKnownProduct(product);
        }
    }

    public void RegisterKnownProduct(ProductData product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.productName))
        {
            return;
        }

        knownProductsByName[product.productName] = product;
    }

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
            return "Placing new shelf\nLeft click on the floor to place it, or right click to cancel.";
        }

        if (carriedRestockBox != null && carriedRestockBox.Product != null)
        {
            return "Carrying stock: " + carriedRestockBox.Product.productName + " x" + carriedRestockBox.Amount +
                   "\nClick the matching shelf to restock it";
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
        EnsureDeliveryManager();
        if (shelf == null || shelf.AssignedProduct == null || IsPlacingShelf || (deliveryManager != null && deliveryManager.IsCarryingCrate) || carriedRestockBox != null)
        {
            return false;
        }

        EnsureWarehouseManager();
        return warehouseManager != null && warehouseManager.TryPrepareRestockForShelf(shelf, mainCamera);
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
        backroomInventory.Clear();

        foreach (ProductInventoryEntry entry in startingInventory)
        {
            if (entry.product == null)
            {
                continue;
            }

            AddToBackroom(entry.product, entry.amount);
        }
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
        EnsureWarehouseManager();
        if (warehouseManager != null)
        {
            warehouseManager.RefreshStockBoxes();
        }
    }

    public void HandleWarehouseStockBoxClicked(WarehouseStockBox stockBox)
    {
        EnsureDeliveryManager();
        if (stockBox == null || IsPlacingShelf || (deliveryManager != null && deliveryManager.IsCarryingCrate) || carriedRestockBox != null || carriedEmptyBox != null)
        {
            return;
        }

        EnsureWarehouseManager();
        if (warehouseManager != null)
        {
            warehouseManager.HandleStockBoxClicked(stockBox);
        }
    }

    public void HandleDeliveryCrateClicked(DeliveryCrate crate)
    {
        EnsureDeliveryManager();
        if (deliveryManager != null)
        {
            deliveryManager.HandleCrateClicked(crate);
        }
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
            if (deliveryManager.TryApplyCarriedCrateToShelf(shelf))
            {
                return;
            }
        }
    }

    private void HandlePlayerViewInteraction()
    {
        EnsurePlayerInteractionManager();
        playerInteractionManager?.HandlePlayerViewInteraction();
    }

    private void SpawnRestockBox(ProductData product, int amount, Vector3 position, bool carried)
    {
        if (product == null || amount <= 0)
        {
            return;
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

        int carriedAmount = carriedRestockBox.Amount;
        int addedAmount = shelf.AddStock(carriedProduct, carriedAmount);
        int leftoverAmount = carriedRestockBox.Amount - addedAmount;

        activeRestockBoxes.Remove(carriedRestockBox);
        Destroy(carriedRestockBox.gameObject);
        carriedRestockBox = null;

        if (leftoverAmount > 0)
        {
            AddToBackroom(carriedProduct, leftoverAmount);
        }

        CreateCarriedEmptyBox(carriedProduct);
        NotifyStateChanged();
    }

    private void CancelCarriedRestockBox()
    {
        if (carriedRestockBox == null)
        {
            return;
        }

        AddToBackroom(carriedRestockBox.Product, carriedRestockBox.Amount);
        activeRestockBoxes.Remove(carriedRestockBox);
        Destroy(carriedRestockBox.gameObject);
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
        shelfPlacementManager?.TryPlaceShelfFromCurrentPointer();
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

        EnsureWarehouseManager();
        Vector3 pickupPosition = warehouseManager != null
            ? warehouseManager.BackroomDropOffPosition + carriedCrateOffset
            : carriedCrateOffset;
        SpawnRestockBox(stockBox.Product, amountToCarry, pickupPosition, true);
        NotifyStateChanged();
        return true;
    }

    public IEnumerable<ProductData> GetKnownProducts()
    {
        return knownProductsByName.Values;
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
        Shader shader = GetRuntimeSurfaceShader();
        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    private Shader GetRuntimeSurfaceShader()
    {
        if (cachedSurfaceShader != null)
        {
            return cachedSurfaceShader;
        }

        string[] shaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                cachedSurfaceShader = shader;
                return cachedSurfaceShader;
            }
        }

        return null;
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

            backroomInventory.Clear();
            EnsureDeliveryManager();
            if (deliveryManager != null)
            {
                deliveryManager.ClearState();
            }
            ClearRestockBoxes();
            BuildStartingInventory();
            RefreshWarehouseStockBoxes();
            CurrentDay = startingDay;
            EnsureDayNightCycle();
            dayNightCycle?.ResetToOpeningTime();
            nextDayCustomerWarningPending = false;
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
            isStoreOpen = dayNightCycle == null || dayNightCycle.IsStoreOpen
        };

        foreach (KeyValuePair<ProductData, int> entry in backroomInventory)
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

        return saveData;
    }

    public void ApplySaveData(GameSaveData saveData)
    {
        isApplyingSaveData = true;

        try
        {
            CancelShelfPlacement();

            backroomInventory.Clear();
            EnsureDeliveryManager();
            if (deliveryManager != null)
            {
                deliveryManager.ClearState();
            }
            ClearRestockBoxes();
            CurrentDay = Mathf.Max(1, saveData.currentDay);
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

            RefreshWarehouseStockBoxes();
        }
        finally
        {
            isApplyingSaveData = false;
        }

        QueueStoreNavigationRebuild();
    }

    private ProductData ResolveProductByName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        if (knownProductsByName.TryGetValue(productName, out ProductData knownProduct))
        {
            return knownProduct;
        }

        foreach (KeyValuePair<ProductData, int> entry in backroomInventory)
        {
            if (entry.Key != null && string.Equals(entry.Key.productName, productName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterKnownProduct(entry.Key);
                return entry.Key;
            }
        }

        Shelf[] shelves = FindObjectsByType<Shelf>();
        foreach (Shelf shelf in shelves)
        {
            if (shelf != null && shelf.AssignedProduct != null &&
                string.Equals(shelf.AssignedProduct.productName, productName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterKnownProduct(shelf.AssignedProduct);
                return shelf.AssignedProduct;
            }
        }

        return null;
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
        EnsureDayNightCycle();
        dayNightCycle?.StartNextMorning();
        nextDayCustomerWarningPending = false;
        NotifyStateChanged();
        SaveGame();
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

        warehouseManager.Initialize(this);
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

    private void EnsureStoreNavigation()
    {
        if (storeNavMeshSurface != null)
        {
            return;
        }

        GameObject floorObject = GameObject.Find("Floor");
        if (floorObject == null)
        {
            return;
        }

        storeNavMeshSurface = floorObject.GetComponent<NavMeshSurface>();
        if (storeNavMeshSurface == null)
        {
            storeNavMeshSurface = floorObject.AddComponent<NavMeshSurface>();
        }

        storeNavMeshSurface.collectObjects = CollectObjects.All;
        storeNavMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        storeNavMeshSurface.layerMask = ~0;
        storeNavMeshSurface.agentTypeID = 0;
    }

    public void QueueStoreNavigationRebuild()
    {
        EnsureStoreNavigation();
        if (storeNavMeshSurface == null || navMeshRebuildQueued)
        {
            return;
        }

        navMeshRebuildQueued = true;
        StartCoroutine(RebuildStoreNavigationAtEndOfFrame());
    }

    private IEnumerator RebuildStoreNavigationAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        navMeshRebuildQueued = false;

        if (storeNavMeshSurface != null)
        {
            storeNavMeshSurface.BuildNavMesh();
        }
    }
}
