using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Builds and updates the prototype HUD for store status, product ordering, shelf focus, and current tasks.
/// </summary>
public class BasicUIManager : MonoBehaviour
{
    public static BasicUIManager Instance { get; private set; }
    public Shelf SelectedShelf => selectedShelf;

    private static readonly Color PanelColor = new Color(0.06f, 0.075f, 0.09f, 0.82f);
    private static readonly Color TaskPanelColor = new Color(0.08f, 0.12f, 0.14f, 0.9f);
    private static readonly Color PauseBackdropColor = new Color(0.01f, 0.015f, 0.018f, 0.72f);
    private static readonly Color PausePanelColor = new Color(0.055f, 0.085f, 0.11f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.9f, 0.95f, 0.94f, 0.96f);
    private static readonly Color ButtonHoverColor = new Color(0.78f, 0.93f, 0.88f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.42f, 0.7f, 0.64f, 1f);
    private static readonly Color ButtonDisabledColor = new Color(0.38f, 0.42f, 0.43f, 0.56f);
    private static readonly Color SelectedProductColor = new Color(1f, 0.74f, 0.25f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.98f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.72f, 0.8f, 0.8f, 1f);
    private static readonly Color DarkTextColor = new Color(0.08f, 0.12f, 0.12f, 1f);

    [Header("Labels")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text inventoryText;
    [SerializeField] private TMP_Text selectedProductText;
    [SerializeField] private TMP_Text selectedShelfText;
    [SerializeField] private TMP_Text deliveryTaskText;
    [SerializeField] private TMP_Text playerModeText;
    [SerializeField] private TMP_Text interactionPromptText;

    [Header("Scene Action Buttons")]
    [SerializeField] private Button buyShelfButton;
    [SerializeField] private Button buySelectedProductStockButton;
    [SerializeField] private Button nextDayButton;

    [Header("Shop")]
    [SerializeField] private ProductData selectedProduct;
    [SerializeField] private int restockPurchaseAmount = 5;
    [SerializeField] private List<ProductData> availableProducts = new List<ProductData>();

    private MoneyManager moneyManager;
    private Shelf selectedShelf;
    private readonly List<Button> productButtons = new List<Button>();
    private RectTransform canvasRoot;
    private RectTransform statusPanel;
    private RectTransform productPanel;
    private RectTransform shelfPanel;
    private RectTransform taskPanel;
    private RectTransform actionPanel;
    private RectTransform productButtonPanel;
    private RectTransform pauseMenuRoot;
    private RectTransform pauseMainPanel;
    private RectTransform pauseSettingsPanel;
    private RectTransform computerRoot;
    private RectTransform computerSidebarPanel;
    private RectTransform computerContentPanel;
    private RectTransform computerMarketPanel;
    private RectTransform computerFurniturePanel;
    private RectTransform computerProductButtonPanel;
    private TMP_Text computerTitleText;
    private TMP_Text computerSelectedProductText;
    private TMP_Text computerMarketStockText;
    private TMP_Text computerFurnitureText;
    private TMP_Text pauseSettingsText;
    private Button saveGameButton;
    private Button loadGameButton;
    private Button newGameButton;
    private Button moveSelectedShelfButton;
    private Button pauseResumeButton;
    private Button pauseSaveButton;
    private Button pauseLoadButton;
    private Button pauseSettingsButton;
    private Button pauseMainMenuButton;
    private Button pauseExitGameButton;
    private Button pauseSettingsBackButton;
    private readonly List<Button> computerProductButtons = new List<Button>();
    private Button computerMarketTabButton;
    private Button computerFurnitureTabButton;
    private Button computerCloseButton;
    private Button computerOrderStockButton;
    private Button computerBuyShelfButton;
    private bool isPauseMenuOpen;
    private bool isComputerOpen;
    private float timeScaleBeforePause = 1f;

    public bool IsBlockingGameplayInput => isPauseMenuOpen || isComputerOpen;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        EnsureCanvasRoot();
        EnsureHudReferences();
        EnsureAvailableProducts();
        RegisterProductsWithGameManager();
        ResolveSceneActionButtons();
        BuildPersistenceButtons();
        BuildFurnitureActionButtons();
        ConfigureHudLayout();
        BuildProductSelectionButtons();
        BuildPauseMenu();
        BuildStoreComputerUI();

        moneyManager = FindAnyObjectByType<MoneyManager>();

        if (moneyManager != null)
        {
            moneyManager.OnMoneyChanged += UpdateMoneyText;
            UpdateMoneyText(moneyManager.CurrentMoney);
        }

        UpdateDayText();
        UpdateInventoryText();
        UpdateSelectedProductText();
        UpdateSelectedShelfText();
        UpdateDeliveryTaskText();
        UpdatePlayerModeText();
        UpdateInteractionPromptText();

        if (GameManager.Instance != null && selectedProduct != null)
        {
            GameManager.Instance.SetDefaultShelfProduct(selectedProduct);
        }

        UpdateProductButtonVisuals();
        UpdatePersistenceButtonStates();
    }

    private void OnDestroy()
    {
        if (isPauseMenuOpen)
        {
            Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
        }

        if (moneyManager != null)
        {
            moneyManager.OnMoneyChanged -= UpdateMoneyText;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (GetPausePressed())
        {
            if (isComputerOpen)
            {
                CloseStoreComputer();
            }
            else
            {
                TogglePauseMenu();
            }
        }

        UpdateDayText();
        UpdateInventoryText();
        UpdateSelectedShelfText();
        UpdateDeliveryTaskText();
        UpdatePlayerModeText();
        UpdateInteractionPromptText();
        UpdatePersistenceButtonStates();
        UpdatePauseMenuButtonStates();
        UpdateStoreComputerButtonStates();
    }

    public void BuyShelfButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartShelfPlacement();
        }
    }

    public void BuySelectedProductStockButton()
    {
        if (GameManager.Instance != null && selectedProduct != null)
        {
            GameManager.Instance.BuyProductStock(selectedProduct, restockPurchaseAmount);
        }
    }

    public void RestockAllShelvesButton()
    {
        Shelf[] shelves = FindObjectsByType<Shelf>();

        foreach (Shelf shelf in shelves)
        {
            shelf.RestockFromBackroom();
        }
    }

    public void RestockSelectedShelfButton()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        Shelf targetShelf = selectedShelf;
        if (targetShelf == null)
        {
            targetShelf = GameManager.Instance.GetFocusedShelf();
            if (targetShelf != null)
            {
                SetSelectedShelf(targetShelf);
            }
        }

        if (targetShelf != null)
        {
            GameManager.Instance.PrepareRestockForShelf(targetShelf);
        }
    }

    public void MoveSelectedShelfButton()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        Shelf targetShelf = selectedShelf;
        if (targetShelf == null)
        {
            targetShelf = GameManager.Instance.GetFocusedShelf();
            if (targetShelf != null)
            {
                SetSelectedShelf(targetShelf);
            }
        }

        if (targetShelf != null)
        {
            GameManager.Instance.StartMovingShelf(targetShelf);
            UpdatePersistenceButtonStates();
        }
    }

    public void NextDayButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NextDay();
        }
    }

    public void SaveGameButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveGame();
            UpdatePersistenceButtonStates();
        }
    }

    public void LoadGameButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadGame();
            SyncSelectedProduct(null);
            UpdatePersistenceButtonStates();
        }
    }

    public void NewGameButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewGame();
            SyncSelectedProduct(null);
            SetSelectedShelf(null);
            UpdatePersistenceButtonStates();
        }
    }

    public void ResumeGameButton()
    {
        SetPauseMenuOpen(false);
    }

    public void SettingsButton()
    {
        ShowPauseSettingsPanel(true);
    }

    public void MainMenuButton()
    {
        Debug.Log("Main Menu is not wired yet. Later this can load a dedicated title scene.");
    }

    public void ExitGameButton()
    {
        Application.Quit();
#if UNITY_EDITOR
        Debug.Log("Exit Game requested. Application.Quit only closes a built game, not Play Mode in the editor.");
#endif
    }

    public void OpenStoreComputer()
    {
        SetStoreComputerOpen(true);
    }

    public void CloseStoreComputer()
    {
        SetStoreComputerOpen(false);
    }

    public void ShowComputerMarket()
    {
        ShowComputerPanel(computerMarketPanel);
    }

    public void ShowComputerFurniture()
    {
        ShowComputerPanel(computerFurniturePanel);
    }

    public void SelectProduct(ProductData product)
    {
        selectedProduct = product;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDefaultShelfProduct(selectedProduct);
        }

        UpdateSelectedProductText();
        UpdateInventoryText();
        UpdateProductButtonVisuals();
        UpdatePersistenceButtonStates();
    }

    public void SetSelectedShelf(Shelf shelf)
    {
        if (selectedShelf == shelf)
        {
            return;
        }

        if (selectedShelf != null)
        {
            selectedShelf.SetSelected(false);
        }

        selectedShelf = shelf;

        if (selectedShelf != null)
        {
            selectedShelf.SetSelected(true);
        }

        UpdateSelectedShelfText();
        UpdatePersistenceButtonStates();
    }

    private void UpdateMoneyText(float currentMoney)
    {
        if (moneyText != null)
        {
            moneyText.text = "Cash\n$" + currentMoney.ToString("0.00");
        }
    }

    private void UpdateDayText()
    {
        if (dayText != null && GameManager.Instance != null)
        {
            dayText.text =
                "Day " + GameManager.Instance.CurrentDay +
                "\n" + GameManager.Instance.StoreClockText +
                "\n" + GameManager.Instance.StoreStateText;
        }
    }

    private void UpdateInventoryText()
    {
        if (inventoryText == null)
        {
            return;
        }

        if (GameManager.Instance == null || selectedProduct == null)
        {
            inventoryText.text = "Choose a product to see stock and deliveries.";
            return;
        }

        int stock = GameManager.Instance.GetBackroomStock(selectedProduct);
        int incoming = GameManager.Instance.GetIncomingDeliveryAmount(selectedProduct);
        int atDock = GameManager.Instance.GetDockDeliveryAmount(selectedProduct);
        float arrivalTime = GameManager.Instance.GetSoonestDeliveryTime(selectedProduct);

        string incomingLabel = incoming > 0 ? "+" + incoming : "None";
        if (incoming > 0 && arrivalTime >= 0f)
        {
            incomingLabel += " arriving in " + Mathf.CeilToInt(arrivalTime) + "s";
        }

        inventoryText.text =
            "Backroom: " + stock +
            "\nAt loading dock: " + (atDock > 0 ? "+" + atDock : "None") +
            "\nIncoming delivery: " + incomingLabel;

        UpdateComputerTexts();
    }

    private void UpdateSelectedProductText()
    {
        if (selectedProductText == null)
        {
            return;
        }

        if (selectedProduct == null)
        {
            selectedProductText.text = "Ordering\nNo product selected";
            return;
        }

        selectedProductText.text =
            "Ordering: " + selectedProduct.productName +
            "\nBuy " + restockPurchaseAmount + " for $" +
            (selectedProduct.wholesaleCost * restockPurchaseAmount).ToString("0.00");

        UpdateComputerTexts();
    }

    private void UpdateSelectedShelfText()
    {
        if (selectedShelfText == null)
        {
            return;
        }

        if (selectedShelf == null)
        {
            selectedShelfText.text = "No shelf selected\nLook at a shelf and press [E], or click one in cursor mode.";
            return;
        }

        string productName = selectedShelf.AssignedProduct != null
            ? selectedShelf.AssignedProduct.productName
            : "Empty shelf";

        selectedShelfText.text =
            productName + "\nStocked " +
            selectedShelf.CurrentStock + " / " +
            selectedShelf.MaxCapacity +
            "\nUse Move Selected Shelf to reposition it.";
    }

    private void EnsureCanvasRoot()
    {
        Canvas canvas = null;
        TMP_Text template = GetAnyExistingLabel();

        if (template != null)
        {
            canvas = template.GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        canvasRoot = canvas.GetComponent<RectTransform>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void EnsureHudReferences()
    {
        if (moneyText == null)
        {
            moneyText = CreateHudLabel("MoneyText");
        }

        if (dayText == null)
        {
            dayText = CreateHudLabel("DayText");
        }

        if (inventoryText == null)
        {
            inventoryText = CreateHudLabel("InventoryText");
        }

        if (selectedProductText == null)
        {
            selectedProductText = CreateHudLabel("SelectedProductText");
        }

        if (selectedShelfText == null)
        {
            selectedShelfText = CreateHudLabel("SelectedShelfText");
        }

        if (deliveryTaskText == null)
        {
            deliveryTaskText = CreateHudLabel("DeliveryTaskText");
        }

        if (playerModeText == null)
        {
            playerModeText = CreateHudLabel("PlayerModeText");
        }

        if (interactionPromptText == null)
        {
            interactionPromptText = CreateHudLabel("InteractionPromptText");
        }
    }

    private void EnsureAvailableProducts()
    {
        availableProducts.RemoveAll(product => product == null);

        if (selectedProduct != null && !availableProducts.Contains(selectedProduct))
        {
            availableProducts.Insert(0, selectedProduct);
        }

        if (selectedProduct == null && availableProducts.Count > 0)
        {
            selectedProduct = availableProducts[0];
        }
    }

    private void RegisterProductsWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterKnownProducts(availableProducts);
        }
    }

    private void ResolveSceneActionButtons()
    {
        buyShelfButton = ResolveButton(buyShelfButton, "Buy Shelf", BuyShelfButton);
        buySelectedProductStockButton = ResolveButton(buySelectedProductStockButton, "Buy Selected Product Stock", BuySelectedProductStockButton);
        nextDayButton = ResolveButton(nextDayButton, "Next Day", NextDayButton);
        HideButtonByName("Restock All Shelves");
    }

    private Button ResolveButton(Button assignedButton, string objectName, UnityAction fallbackAction)
    {
        Button button = assignedButton != null ? assignedButton : FindButtonByName(objectName);
        if (button == null)
        {
            button = CreateActionButton(objectName, objectName, null);
        }

        if (button.onClick.GetPersistentEventCount() == 0 && fallbackAction != null)
        {
            button.onClick.AddListener(fallbackAction);
        }

        SetButtonLabel(button, objectName);
        return button;
    }

    private Button FindButtonByName(string objectName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private void HideButtonByName(string objectName)
    {
        Button button = FindButtonByName(objectName);
        if (button != null)
        {
            button.gameObject.SetActive(false);
        }
    }

    private void HideButton(Button button)
    {
        if (button != null)
        {
            button.gameObject.SetActive(false);
        }
    }

    private void ConfigureHudLayout()
    {
        statusPanel = CreatePanel("StoreStatusPanel", new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(330f, 154f), PanelColor);
        productPanel = CreatePanel("ProductOrderPanel", new Vector2(0f, 0.5f), new Vector2(20f, 92f), new Vector2(360f, 356f), PanelColor);
        shelfPanel = CreatePanel("ShelfFocusPanel", new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(400f, 154f), PanelColor);
        taskPanel = CreatePanel("CurrentTaskPanel", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(620f, 150f), TaskPanelColor);
        actionPanel = CreatePanel("ActionPanel", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(300f, 260f), PanelColor);
        productPanel.gameObject.SetActive(false);

        AddHeading(statusPanel, "Store");
        AddHeading(shelfPanel, "Selected Shelf");
        AddHeading(taskPanel, "Next Step");
        AddHeading(actionPanel, "Actions");

        ConfigureHudLabel(moneyText, statusPanel, new Vector2(14f, -44f), new Vector2(112f, 70f), 26f, TextAlignmentOptions.TopLeft, TextColor);
        ConfigureHudLabel(dayText, statusPanel, new Vector2(134f, -42f), new Vector2(188f, 102f), 23f, TextAlignmentOptions.TopLeft, TextColor);

        ConfigureHudLabel(selectedShelfText, shelfPanel, new Vector2(14f, -44f), new Vector2(372f, 102f), 21f, TextAlignmentOptions.TopLeft, TextColor);
        ConfigureHudLabel(deliveryTaskText, taskPanel, new Vector2(16f, -44f), new Vector2(588f, 58f), 23f, TextAlignmentOptions.TopLeft, TextColor);
        ConfigureHudLabel(interactionPromptText, taskPanel, new Vector2(16f, -104f), new Vector2(588f, 30f), 22f, TextAlignmentOptions.TopLeft, SelectedProductColor);
        ConfigureHudLabel(playerModeText, actionPanel, new Vector2(16f, -154f), new Vector2(268f, 80f), 18f, TextAlignmentOptions.TopLeft, MutedTextColor);

        HideButton(buyShelfButton);
        HideButton(buySelectedProductStockButton);
        HideButton(saveGameButton);
        HideButton(loadGameButton);
        HideButton(newGameButton);
        PlaceActionButton(moveSelectedShelfButton, new Vector2(16f, -52f), "Move Selected Shelf");
        PlaceActionButton(nextDayButton, new Vector2(16f, -94f), GameManager.Instance != null ? GameManager.Instance.DayControlButtonText : "Start Day");
    }

    private void BuildProductSelectionButtons()
    {
        if (availableProducts.Count == 0)
        {
            return;
        }

        if (productButtonPanel == null)
        {
            productButtonPanel = CreateProductButtonPanel();
        }

        foreach (Button button in productButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        productButtons.Clear();

        float buttonHeight = 34f;
        float spacing = 8f;

        for (int index = 0; index < availableProducts.Count; index++)
        {
            ProductData product = availableProducts[index];
            if (product == null)
            {
                continue;
            }

            Button button = CreateProductButton(product, index, buttonHeight, spacing);
            productButtons.Add(button);
        }

        productButtonPanel.sizeDelta = new Vector2(332f, (availableProducts.Count * (buttonHeight + spacing)) - spacing);
    }

    private void BuildPersistenceButtons()
    {
        if (saveGameButton == null)
        {
            saveGameButton = CreateActionButton("SaveGameButton", "Save Game", SaveGameButton);
        }

        if (loadGameButton == null)
        {
            loadGameButton = CreateActionButton("LoadGameButton", "Load Game", LoadGameButton);
        }

        if (newGameButton == null)
        {
            newGameButton = CreateActionButton("NewGameButton", "New Game", NewGameButton);
        }

    }

    private void BuildFurnitureActionButtons()
    {
        if (moveSelectedShelfButton == null)
        {
            moveSelectedShelfButton = CreateActionButton("MoveSelectedShelfButton", "Move Selected Shelf", MoveSelectedShelfButton);
        }
    }

    private void BuildPauseMenu()
    {
        if (pauseMenuRoot == null)
        {
            GameObject rootObject = new GameObject("PauseMenuRoot", typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(canvasRoot, false);
            pauseMenuRoot = rootObject.GetComponent<RectTransform>();
        }

        pauseMenuRoot.anchorMin = Vector2.zero;
        pauseMenuRoot.anchorMax = Vector2.one;
        pauseMenuRoot.pivot = new Vector2(0.5f, 0.5f);
        pauseMenuRoot.offsetMin = Vector2.zero;
        pauseMenuRoot.offsetMax = Vector2.zero;

        Image backdrop = pauseMenuRoot.GetComponent<Image>();
        if (backdrop == null)
        {
            backdrop = pauseMenuRoot.gameObject.AddComponent<Image>();
        }

        backdrop.color = PauseBackdropColor;
        backdrop.raycastTarget = true;

        pauseMainPanel = CreatePausePanel("PauseMainPanel", new Vector2(460f, 520f));
        pauseSettingsPanel = CreatePausePanel("PauseSettingsPanel", new Vector2(560f, 380f));

        AddHeading(pauseMainPanel, "Paused");
        AddHeading(pauseSettingsPanel, "Settings");

        pauseResumeButton = CreatePauseButton("PauseResumeButton", "Resume", ResumeGameButton, pauseMainPanel, -72f);
        pauseSaveButton = CreatePauseButton("PauseSaveButton", "Save Game", SaveGameButton, pauseMainPanel, -124f);
        pauseLoadButton = CreatePauseButton("PauseLoadButton", "Load Game", LoadGameButton, pauseMainPanel, -176f);
        pauseSettingsButton = CreatePauseButton("PauseSettingsButton", "Settings", SettingsButton, pauseMainPanel, -228f);
        pauseMainMenuButton = CreatePauseButton("PauseMainMenuButton", "Main Menu (Coming Soon)", MainMenuButton, pauseMainPanel, -280f);
        pauseExitGameButton = CreatePauseButton("PauseExitGameButton", "Exit Game", ExitGameButton, pauseMainPanel, -360f);

        pauseSettingsText = FindChildText(pauseSettingsPanel, "SettingsText");
        if (pauseSettingsText == null)
        {
            GameObject textObject = new GameObject("SettingsText", typeof(RectTransform));
            textObject.transform.SetParent(pauseSettingsPanel, false);
            pauseSettingsText = textObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(pauseSettingsText);
        }

        ConfigureHudLabel(
            pauseSettingsText,
            pauseSettingsPanel,
            new Vector2(28f, -70f),
            new Vector2(504f, 180f),
            22f,
            TextAlignmentOptions.TopLeft,
            TextColor);
        pauseSettingsText.text = "Settings will live here.\n\nGood first options later:\nMouse sensitivity\nVolume\nGraphics quality\nKeybinds";

        pauseSettingsBackButton = CreatePauseButton("PauseSettingsBackButton", "Back", () => ShowPauseSettingsPanel(false), pauseSettingsPanel, -300f);

        SetPauseMenuOpen(false);
    }

    private void BuildStoreComputerUI()
    {
        if (computerRoot == null)
        {
            GameObject rootObject = new GameObject("StoreComputerUI", typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(canvasRoot, false);
            computerRoot = rootObject.GetComponent<RectTransform>();
        }

        computerRoot.anchorMin = Vector2.zero;
        computerRoot.anchorMax = Vector2.one;
        computerRoot.pivot = new Vector2(0.5f, 0.5f);
        computerRoot.offsetMin = Vector2.zero;
        computerRoot.offsetMax = Vector2.zero;

        Image background = computerRoot.GetComponent<Image>();
        if (background == null)
        {
            background = computerRoot.gameObject.AddComponent<Image>();
        }

        background.color = new Color(0.74f, 0.95f, 0.82f, 0.98f);
        background.raycastTarget = true;

        computerSidebarPanel = CreateComputerPanel("ComputerSidebar", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(250f, 0f), new Color(0.02f, 0.14f, 0.24f, 1f));
        computerSidebarPanel.anchorMin = new Vector2(0f, 0f);
        computerSidebarPanel.anchorMax = new Vector2(0f, 1f);
        computerSidebarPanel.pivot = new Vector2(0f, 0.5f);
        computerSidebarPanel.sizeDelta = new Vector2(250f, 0f);

        computerContentPanel = CreateComputerPanel("ComputerContent", new Vector2(0f, 0f), new Vector2(250f, 0f), new Vector2(-250f, 0f), new Color(0.86f, 0.94f, 0.96f, 1f));
        computerContentPanel.anchorMin = new Vector2(0f, 0f);
        computerContentPanel.anchorMax = new Vector2(1f, 1f);
        computerContentPanel.pivot = new Vector2(0.5f, 0.5f);
        computerContentPanel.offsetMin = new Vector2(250f, 0f);
        computerContentPanel.offsetMax = Vector2.zero;

        computerTitleText = CreateComputerLabel("ComputerTitle", computerSidebarPanel, new Vector2(22f, -24f), new Vector2(206f, 48f), 30f, "STORE OS");
        computerMarketTabButton = CreateComputerButton("ComputerMarketTab", "Market", ShowComputerMarket, computerSidebarPanel, new Vector2(22f, -100f), new Vector2(206f, 54f));
        computerFurnitureTabButton = CreateComputerButton("ComputerFurnitureTab", "Furniture", ShowComputerFurniture, computerSidebarPanel, new Vector2(22f, -168f), new Vector2(206f, 54f));
        computerCloseButton = CreateComputerButton("ComputerCloseButton", "Close", CloseStoreComputer, computerSidebarPanel, new Vector2(22f, -250f), new Vector2(206f, 54f));

        computerMarketPanel = CreateComputerContentPanel("ComputerMarketPanel");
        computerFurniturePanel = CreateComputerContentPanel("ComputerFurniturePanel");
        BuildComputerMarketPanel();
        BuildComputerFurniturePanel();

        SetStoreComputerOpen(false);
    }

    private void BuildComputerMarketPanel()
    {
        AddHeading(computerMarketPanel, "Market - Order Stock");
        computerSelectedProductText = CreateComputerLabel("ComputerSelectedProductText", computerMarketPanel, new Vector2(28f, -64f), new Vector2(440f, 80f), 24f, string.Empty);
        computerMarketStockText = CreateComputerLabel("ComputerMarketStockText", computerMarketPanel, new Vector2(500f, -64f), new Vector2(480f, 92f), 22f, string.Empty);
        computerOrderStockButton = CreateComputerButton("ComputerOrderStockButton", "Order Selected Stock", BuySelectedProductStockButton, computerMarketPanel, new Vector2(28f, -150f), new Vector2(300f, 44f));

        computerProductButtonPanel = FindDirectChildRect(computerMarketPanel, "ComputerProductButtons");
        if (computerProductButtonPanel == null)
        {
            GameObject panelObject = new GameObject("ComputerProductButtons", typeof(RectTransform));
            panelObject.transform.SetParent(computerMarketPanel, false);
            computerProductButtonPanel = panelObject.GetComponent<RectTransform>();
        }

        computerProductButtonPanel.anchorMin = new Vector2(0f, 1f);
        computerProductButtonPanel.anchorMax = new Vector2(0f, 1f);
        computerProductButtonPanel.pivot = new Vector2(0f, 1f);
        computerProductButtonPanel.anchoredPosition = new Vector2(28f, -220f);
        computerProductButtonPanel.sizeDelta = new Vector2(900f, 520f);

        BuildComputerProductButtons();
    }

    private void BuildComputerFurniturePanel()
    {
        AddHeading(computerFurniturePanel, "Furniture - Order Store Fixtures");
        computerFurnitureText = CreateComputerLabel(
            "ComputerFurnitureText",
            computerFurniturePanel,
            new Vector2(28f, -72f),
            new Vector2(720f, 120f),
            24f,
            "Shelf\nUnit Price: $50.00\nAdds an empty shelf that can be placed on the store grid.");
        computerBuyShelfButton = CreateComputerButton("ComputerBuyShelfButton", "Buy Shelf", BuyShelfButton, computerFurniturePanel, new Vector2(28f, -196f), new Vector2(260f, 46f));
    }

    private void BuildComputerProductButtons()
    {
        foreach (Button button in computerProductButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        computerProductButtons.Clear();

        float buttonWidth = 250f;
        float buttonHeight = 48f;
        float spacingX = 18f;
        float spacingY = 16f;
        int columns = 3;

        for (int index = 0; index < availableProducts.Count; index++)
        {
            ProductData product = availableProducts[index];
            if (product == null)
            {
                continue;
            }

            int column = index % columns;
            int row = index / columns;
            Vector2 position = new Vector2(column * (buttonWidth + spacingX), -(row * (buttonHeight + spacingY)));
            ProductData capturedProduct = product;
            Button button = CreateComputerButton(
                "ComputerProduct_" + product.productName,
                product.productName,
                () => SelectProduct(capturedProduct),
                computerProductButtonPanel,
                position,
                new Vector2(buttonWidth, buttonHeight));
            computerProductButtons.Add(button);
        }
    }

    private RectTransform CreatePausePanel(string objectName, Vector2 size)
    {
        RectTransform panel = FindDirectChildRect(pauseMenuRoot, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(pauseMenuRoot, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        image.color = PausePanelColor;
        image.raycastTarget = true;
        return panel;
    }

    private RectTransform CreateComputerPanel(string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform panel = FindDirectChildRect(computerRoot, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(computerRoot, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = anchor;
        panel.anchorMax = anchor;
        panel.pivot = new Vector2(anchor.x, anchor.y);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private RectTransform CreateComputerContentPanel(string objectName)
    {
        RectTransform panel = FindDirectChildRect(computerContentPanel, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform));
            panelObject.transform.SetParent(computerContentPanel, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        return panel;
    }

    private TMP_Text CreateComputerLabel(string objectName, RectTransform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, string text)
    {
        TMP_Text label = FindChildText(parent, objectName);
        if (label == null)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(label);
        }

        ConfigureHudLabel(label, parent, anchoredPosition, size, fontSize, TextAlignmentOptions.TopLeft, TextColor);
        label.text = text;
        return label;
    }

    private Button CreateComputerButton(
        string objectName,
        string labelText,
        UnityAction onClickAction,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Button button = FindButtonByName(objectName);
        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        button.transform.SetParent(parent, false);
        button.onClick.RemoveAllListeners();
        if (onClickAction != null)
        {
            button.onClick.AddListener(onClickAction);
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        SetButtonLabel(button, labelText);
        StyleButton(button);
        return button;
    }

    private Button CreatePauseButton(string objectName, string labelText, UnityAction onClickAction, RectTransform parent, float yPosition)
    {
        Button button = FindButtonByName(objectName);
        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        button.transform.SetParent(parent, false);
        button.onClick.RemoveAllListeners();
        if (onClickAction != null)
        {
            button.onClick.AddListener(onClickAction);
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, yPosition);
        rect.sizeDelta = new Vector2(360f, 42f);

        SetButtonLabel(button, labelText);
        StyleButton(button);
        return button;
    }

    private void TogglePauseMenu()
    {
        SetPauseMenuOpen(!isPauseMenuOpen);
    }

    private void SetPauseMenuOpen(bool isOpen)
    {
        isPauseMenuOpen = isOpen;

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.gameObject.SetActive(isPauseMenuOpen);
        }

        if (isPauseMenuOpen)
        {
            timeScaleBeforePause = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowPauseSettingsPanel(false);
            UpdatePauseMenuButtonStates();
        }
        else
        {
            Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
        }
    }

    private void SetStoreComputerOpen(bool isOpen)
    {
        isComputerOpen = isOpen;

        if (computerRoot != null)
        {
            computerRoot.gameObject.SetActive(isComputerOpen);
        }

        if (isComputerOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowComputerMarket();
            UpdateComputerTexts();
            UpdateStoreComputerButtonStates();
        }
    }

    private void ShowComputerPanel(RectTransform panelToShow)
    {
        if (computerMarketPanel != null)
        {
            computerMarketPanel.gameObject.SetActive(panelToShow == computerMarketPanel);
        }

        if (computerFurniturePanel != null)
        {
            computerFurniturePanel.gameObject.SetActive(panelToShow == computerFurniturePanel);
        }

        UpdateStoreComputerButtonStates();
    }

    private void ShowPauseSettingsPanel(bool showSettings)
    {
        if (pauseMainPanel != null)
        {
            pauseMainPanel.gameObject.SetActive(!showSettings);
        }

        if (pauseSettingsPanel != null)
        {
            pauseSettingsPanel.gameObject.SetActive(showSettings);
        }
    }

    private RectTransform CreatePanel(string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform panel = FindDirectChildRect(canvasRoot, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasRoot, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = anchor;
        panel.anchorMax = anchor;
        panel.pivot = new Vector2(anchor.x, anchor.y);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private void AddHeading(RectTransform parent, string text)
    {
        TMP_Text heading = FindChildText(parent, "Heading");
        if (heading == null)
        {
            GameObject headingObject = new GameObject("Heading", typeof(RectTransform));
            headingObject.transform.SetParent(parent, false);
            heading = headingObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(heading);
        }

        RectTransform rect = heading.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(-24f, 26f);

        heading.text = text.ToUpperInvariant();
        heading.fontSize = 17f;
        heading.color = MutedTextColor;
        heading.alignment = TextAlignmentOptions.TopLeft;
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.raycastTarget = false;
    }

    private void ConfigureHudLabel(
        TMP_Text label,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (label == null || parent == null)
        {
            return;
        }

        label.transform.SetParent(parent, false);

        RectTransform rect = label.rectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
    }

    private TMP_Text CreateHudLabel(string objectName)
    {
        Transform parent = canvasRoot != null ? canvasRoot : transform;

        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI newLabel = labelObject.AddComponent<TextMeshProUGUI>();
        CopyFontSettings(newLabel);
        newLabel.text = string.Empty;
        newLabel.fontSize = 22f;
        newLabel.color = TextColor;
        newLabel.textWrappingMode = TextWrappingModes.Normal;
        newLabel.raycastTarget = false;
        return newLabel;
    }

    private RectTransform CreateProductButtonPanel()
    {
        GameObject panelObject = new GameObject("ProductButtonsPanel", typeof(RectTransform));
        panelObject.transform.SetParent(productPanel, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(14f, -234f);
        panelRect.sizeDelta = new Vector2(332f, 100f);
        return panelRect;
    }

    private Button CreateActionButton(string objectName, string labelText, UnityAction onClickAction)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(actionPanel != null ? actionPanel : canvasRoot, false);

        Button button = buttonObject.GetComponent<Button>();
        if (onClickAction != null)
        {
            button.onClick.AddListener(onClickAction);
        }

        EnsureButtonLabel(button, labelText);
        StyleButton(button);
        return button;
    }

    private Button CreateProductButton(ProductData product, int index, float buttonHeight, float spacing)
    {
        GameObject buttonObject = new GameObject(product.productName + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(productButtonPanel, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(0f, -(index * (buttonHeight + spacing)));
        buttonRect.sizeDelta = new Vector2(332f, buttonHeight);

        Button button = buttonObject.GetComponent<Button>();
        ProductData capturedProduct = product;
        button.onClick.AddListener(() => SelectProduct(capturedProduct));

        EnsureButtonLabel(button, product.productName);
        StyleButton(button);
        return button;
    }

    private void PlaceActionButton(Button button, Vector2 anchoredPosition, string labelText)
    {
        if (button == null || actionPanel == null)
        {
            return;
        }

        button.transform.SetParent(actionPanel, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(268f, 34f);

        SetButtonLabel(button, labelText);
        StyleButton(button);
    }

    private void EnsureButtonLabel(Button button, string labelText)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(label);
        }

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        label.text = labelText;
        label.fontSize = 19f;
        label.color = DarkTextColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    private void SetButtonLabel(Button button, string labelText)
    {
        if (button == null)
        {
            return;
        }

        EnsureButtonLabel(button, labelText);
    }

    private void StyleButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
        }

        image.color = button.interactable ? ButtonColor : ButtonDisabledColor;

        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = ButtonDisabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private void UpdateProductButtonVisuals()
    {
        for (int index = 0; index < productButtons.Count; index++)
        {
            Button button = productButtons[index];
            if (button == null)
            {
                continue;
            }

            ProductData product = index < availableProducts.Count ? availableProducts[index] : null;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = product == selectedProduct ? SelectedProductColor : ButtonColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = DarkTextColor;
                label.fontStyle = product == selectedProduct ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        UpdateComputerProductButtonVisuals();
    }

    private void UpdateComputerProductButtonVisuals()
    {
        for (int index = 0; index < computerProductButtons.Count; index++)
        {
            Button button = computerProductButtons[index];
            if (button == null)
            {
                continue;
            }

            ProductData product = index < availableProducts.Count ? availableProducts[index] : null;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = product == selectedProduct ? SelectedProductColor : ButtonColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = DarkTextColor;
                label.fontStyle = product == selectedProduct ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    private void UpdateComputerTexts()
    {
        if (computerSelectedProductText == null || computerMarketStockText == null)
        {
            return;
        }

        if (selectedProduct == null)
        {
            computerSelectedProductText.text = "No product selected";
            computerMarketStockText.text = "Choose a product to order stock.";
            return;
        }

        int stock = GameManager.Instance != null ? GameManager.Instance.GetBackroomStock(selectedProduct) : 0;
        int incoming = GameManager.Instance != null ? GameManager.Instance.GetIncomingDeliveryAmount(selectedProduct) : 0;
        int atDock = GameManager.Instance != null ? GameManager.Instance.GetDockDeliveryAmount(selectedProduct) : 0;
        float orderCost = selectedProduct.wholesaleCost * restockPurchaseAmount;

        computerSelectedProductText.text =
            selectedProduct.productName +
            "\nOrder amount: " + restockPurchaseAmount +
            "\nOrder cost: $" + orderCost.ToString("0.00");

        computerMarketStockText.text =
            "Backroom stock: " + stock +
            "\nAt loading dock: " + (atDock > 0 ? "+" + atDock : "None") +
            "\nIncoming delivery: " + (incoming > 0 ? "+" + incoming : "None");
    }

    private void UpdatePersistenceButtonStates()
    {
        bool hasSave = GameManager.Instance != null && GameManager.Instance.HasSaveGame();
        bool canSave = GameManager.Instance != null && GameManager.Instance.CanSaveGame;
        bool canUseDayControl = GameManager.Instance == null || GameManager.Instance.CanUseDayControl;
        bool hasShelfTarget = selectedShelf != null || (GameManager.Instance != null && GameManager.Instance.GetFocusedShelf() != null);
        bool hasSelectedProduct = selectedProduct != null;

        SetButtonLabel(nextDayButton, GameManager.Instance != null ? GameManager.Instance.DayControlButtonText : "Start Day");

        SetButtonState(saveGameButton, canSave);
        SetButtonState(loadGameButton, hasSave);
        SetButtonState(newGameButton, true);
        SetButtonState(moveSelectedShelfButton, hasShelfTarget && (GameManager.Instance == null || !GameManager.Instance.IsPlacingShelf));
        SetButtonState(buySelectedProductStockButton, hasSelectedProduct);
        SetButtonState(nextDayButton, canUseDayControl);
    }

    private void UpdateStoreComputerButtonStates()
    {
        if (computerRoot == null || !computerRoot.gameObject.activeSelf)
        {
            return;
        }

        bool hasSelectedProduct = selectedProduct != null;
        bool canPlaceShelf = GameManager.Instance == null || !GameManager.Instance.IsPlacingShelf;

        SetButtonState(computerOrderStockButton, hasSelectedProduct);
        SetButtonState(computerBuyShelfButton, canPlaceShelf);
        UpdateComputerTexts();
        UpdateComputerProductButtonVisuals();
    }

    private void UpdatePauseMenuButtonStates()
    {
        if (pauseMenuRoot == null || !pauseMenuRoot.gameObject.activeSelf)
        {
            return;
        }

        bool hasSave = GameManager.Instance != null && GameManager.Instance.HasSaveGame();
        bool canSave = GameManager.Instance != null && GameManager.Instance.CanSaveGame;

        SetButtonState(pauseResumeButton, true);
        SetButtonState(pauseSaveButton, canSave);
        SetButtonState(pauseLoadButton, hasSave);
        SetButtonState(pauseSettingsButton, true);
        SetButtonState(pauseMainMenuButton, false);
        SetButtonState(pauseExitGameButton, true);
        SetButtonState(pauseSettingsBackButton, true);
    }

    private void SetButtonState(Button button, bool isEnabled)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = isEnabled;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isEnabled ? ButtonColor : ButtonDisabledColor;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = isEnabled ? DarkTextColor : new Color(0.72f, 0.78f, 0.78f, 1f);
        }
    }

    private void UpdateDeliveryTaskText()
    {
        if (deliveryTaskText == null)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            deliveryTaskText.text = "Store is still starting up.";
            return;
        }

        string status = GameManager.Instance.GetStoreTaskStatus();
        deliveryTaskText.text = string.IsNullOrWhiteSpace(status) || status == "No active carry tasks"
            ? "No active carry task. Check shelves, order stock, or start the next day."
            : status;
    }

    private void UpdatePlayerModeText()
    {
        if (playerModeText == null)
        {
            return;
        }

        if (FirstPersonPlayerController.Instance == null)
        {
            playerModeText.text = "Camera mode\nPrototype camera";
            return;
        }

        playerModeText.text = FirstPersonPlayerController.Instance.GetControlsSummary();
    }

    private void UpdateInteractionPromptText()
    {
        if (interactionPromptText == null)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            interactionPromptText.text = string.Empty;
            return;
        }

        string prompt = GameManager.Instance.GetPlayerInteractionPrompt();
        interactionPromptText.text = string.IsNullOrWhiteSpace(prompt)
            ? "Tip: Use Tab to switch between Player View and Cursor/UI."
            : prompt;
    }

    private TMP_Text GetAnyExistingLabel()
    {
        if (selectedProductText != null)
        {
            return selectedProductText;
        }

        if (moneyText != null)
        {
            return moneyText;
        }

        if (dayText != null)
        {
            return dayText;
        }

        if (inventoryText != null)
        {
            return inventoryText;
        }

        return null;
    }

    private bool GetPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void CopyFontSettings(TMP_Text target)
    {
        TMP_Text template = GetAnyExistingLabel();
        if (target == null || template == null || target == template)
        {
            return;
        }

        target.font = template.font;
        target.fontSharedMaterial = template.fontSharedMaterial;
    }

    private RectTransform FindDirectChildRect(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child != null && child.name == objectName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private TMP_Text FindChildText(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    public void SyncSelectedProduct(ProductData product)
    {
        if (product != null)
        {
            selectedProduct = product;
        }

        UpdateSelectedProductText();
        UpdateInventoryText();
        UpdateSelectedShelfText();
        UpdateProductButtonVisuals();
    }
}
