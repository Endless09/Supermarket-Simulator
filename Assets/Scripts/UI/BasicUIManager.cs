using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Updates the basic UI for money, day, and a simple selected product for shop buttons.
/// </summary>
public class BasicUIManager : MonoBehaviour
{
    public static BasicUIManager Instance { get; private set; }
    public Shelf SelectedShelf => selectedShelf;

    [Header("Labels")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text inventoryText;
    [SerializeField] private TMP_Text selectedProductText;
    [SerializeField] private TMP_Text selectedShelfText;
    [SerializeField] private TMP_Text deliveryTaskText;
    [SerializeField] private TMP_Text playerModeText;
    [SerializeField] private TMP_Text interactionPromptText;

    [Header("Shop")]
    [SerializeField] private ProductData selectedProduct;
    [SerializeField] private int restockPurchaseAmount = 5;
    [SerializeField] private List<ProductData> availableProducts = new List<ProductData>();

    private MoneyManager moneyManager;
    private Shelf selectedShelf;
    private readonly List<Button> productButtons = new List<Button>();
    private RectTransform productButtonPanel;
    private Button saveGameButton;
    private Button loadGameButton;
    private Button newGameButton;
    private Button restockSelectedShelfButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        EnsureHudReferences();
        EnsureAvailableProducts();
        RegisterProductsWithGameManager();
        ConfigureHudLayout();
        BuildProductSelectionButtons();
        BuildPersistenceButtons();
        BuildShelfActionButtons();

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
        UpdateDayText();
        UpdateInventoryText();
        UpdateSelectedShelfText();
        UpdateDeliveryTaskText();
        UpdatePlayerModeText();
        UpdateInteractionPromptText();
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
            moneyText.text = "Money: $" + currentMoney.ToString("0.00");
        }
    }

    private void UpdateDayText()
    {
        if (dayText != null && GameManager.Instance != null)
        {
            dayText.text = "Day: " + GameManager.Instance.CurrentDay;
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
            inventoryText.text = "Backroom: -";
            return;
        }

        int stock = GameManager.Instance.GetBackroomStock(selectedProduct);
        int incoming = GameManager.Instance.GetIncomingDeliveryAmount(selectedProduct);
        int atDock = GameManager.Instance.GetDockDeliveryAmount(selectedProduct);
        float arrivalTime = GameManager.Instance.GetSoonestDeliveryTime(selectedProduct);

        inventoryText.text = selectedProduct.productName + " Backroom: " + stock;

        if (atDock > 0)
        {
            inventoryText.text += "\nAt Dock: +" + atDock;
        }

        if (incoming > 0)
        {
            inventoryText.text += "\nIncoming: +" + incoming;

            if (arrivalTime >= 0f)
            {
                inventoryText.text += " (" + Mathf.CeilToInt(arrivalTime) + "s)";
            }
        }
    }

    private void UpdateSelectedProductText()
    {
        if (selectedProductText == null)
        {
            return;
        }

        selectedProductText.text = selectedProduct != null
            ? "Buy Product: " + selectedProduct.productName
            : "Buy Product: None";
    }

    private void UpdateSelectedShelfText()
    {
        if (selectedShelfText == null)
        {
            return;
        }

        if (selectedShelf == null)
        {
            selectedShelfText.text = "Selected Shelf: None";
            return;
        }

        string productName = selectedShelf.AssignedProduct != null
            ? selectedShelf.AssignedProduct.productName
            : "Empty";

        selectedShelfText.text = "Selected Shelf: " + productName + " (" +
                                 selectedShelf.CurrentStock + "/" +
                                 selectedShelf.MaxCapacity + ")";
    }

    private void EnsureHudReferences()
    {
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

    private void ConfigureHudLayout()
    {
        ConfigureHudLabel(inventoryText, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(320f, 80f), 28, VerticalAlignmentOptions.Top);
        ConfigureHudLabel(selectedProductText, new Vector2(0f, 1f), new Vector2(20f, -120f), new Vector2(320f, 80f), 28, VerticalAlignmentOptions.Top);
        ConfigureHudLabel(moneyText, new Vector2(0f, 0.5f), new Vector2(20f, -100f), new Vector2(320f, 60f), 28, VerticalAlignmentOptions.Middle);
        ConfigureHudLabel(dayText, new Vector2(0f, 0.5f), new Vector2(20f, 160f), new Vector2(220f, 60f), 28, VerticalAlignmentOptions.Middle);
        ConfigureHudLabel(deliveryTaskText, new Vector2(0f, 0f), new Vector2(20f, 110f), new Vector2(380f, 90f), 22, VerticalAlignmentOptions.Bottom);
        ConfigureHudLabel(selectedShelfText, new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(360f, 80f), 24, VerticalAlignmentOptions.Bottom);
        ConfigureHudLabel(playerModeText, new Vector2(1f, 0f), new Vector2(-340f, 20f), new Vector2(320f, 80f), 20, VerticalAlignmentOptions.Bottom);
        ConfigureHudLabel(interactionPromptText, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 60f), 22, VerticalAlignmentOptions.Bottom);

        if (interactionPromptText != null)
        {
            interactionPromptText.alignment = TextAlignmentOptions.Bottom;
        }
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

        float buttonHeight = 30f;
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

        productButtonPanel.sizeDelta = new Vector2(170f, (availableProducts.Count * (buttonHeight + spacing)) - spacing);

    }

    private void BuildPersistenceButtons()
    {
        if (saveGameButton == null)
        {
            saveGameButton = CreateActionButton("SaveGameButton", "Save Game", new Vector2(-20f, -500f), SaveGameButton);
        }

        if (loadGameButton == null)
        {
            loadGameButton = CreateActionButton("LoadGameButton", "Load Game", new Vector2(-20f, -540f), LoadGameButton);
        }

        if (newGameButton == null)
        {
            newGameButton = CreateActionButton("NewGameButton", "New Game", new Vector2(-20f, -580f), NewGameButton);
        }
    }

    private void BuildShelfActionButtons()
    {
        if (restockSelectedShelfButton == null)
        {
            restockSelectedShelfButton = CreateActionButton(
                "RestockSelectedShelfButton",
                "Restock Selected",
                new Vector2(-20f, -110f),
                RestockSelectedShelfButton);
        }
    }

    private void ConfigureHudLabel(
        TMP_Text label,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        VerticalAlignmentOptions verticalAlignment)
    {
        if (label == null)
        {
            return;
        }

        if (label.rectTransform != null)
        {
            label.rectTransform.anchorMin = anchor;
            label.rectTransform.anchorMax = anchor;
            label.rectTransform.pivot = anchor;
            label.rectTransform.anchoredPosition = anchoredPosition;
            label.rectTransform.sizeDelta = size;
        }

        label.fontSize = fontSize;
        label.alignment = GetAlignment(verticalAlignment);
        label.textWrappingMode = TextWrappingModes.Normal;
    }

    private TMP_Text CreateHudLabel(string objectName)
    {
        Transform parent = selectedProductText != null
            ? selectedProductText.transform.parent
            : transform;

        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI template = selectedProductText as TextMeshProUGUI;
        TextMeshProUGUI newLabel = labelObject.AddComponent<TextMeshProUGUI>();

        if (template != null)
        {
            newLabel.font = template.font;
            newLabel.fontSharedMaterial = template.fontSharedMaterial;
            newLabel.color = template.color;
            newLabel.fontSize = template.fontSize;
            newLabel.textWrappingMode = template.textWrappingMode;
        }
        else
        {
            newLabel.fontSize = 24f;
            newLabel.color = Color.white;
            newLabel.textWrappingMode = TextWrappingModes.Normal;
        }

        newLabel.text = "Selected Shelf: None";
        return newLabel;
    }

    private RectTransform CreateProductButtonPanel()
    {
        Transform parent = selectedProductText != null
            ? selectedProductText.transform.parent
            : transform;

        GameObject panelObject = new GameObject("ProductButtonsPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -250f);
        panelRect.sizeDelta = new Vector2(170f, 200f);
        return panelRect;
    }

    private Button CreateActionButton(string objectName, string labelText, Vector2 anchoredPosition, UnityAction onClickAction)
    {
        Transform parent = selectedProductText != null
            ? selectedProductText.transform.parent
            : transform;

        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(170f, 30f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        if (onClickAction != null)
        {
            button.onClick.AddListener(onClickAction);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI template = selectedProductText as TextMeshProUGUI;
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
        }

        label.text = labelText;
        label.fontSize = 20f;
        label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        label.alignment = TextAlignmentOptions.Midline;

        return button;
    }

    private Button CreateProductButton(ProductData product, int index, float buttonHeight, float spacing)
    {
        GameObject buttonObject = new GameObject(product.productName + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(productButtonPanel, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(0f, -(index * (buttonHeight + spacing)));
        buttonRect.sizeDelta = new Vector2(170f, buttonHeight);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        ProductData capturedProduct = product;
        button.onClick.AddListener(() => SelectProduct(capturedProduct));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI template = selectedProductText as TextMeshProUGUI;
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
        }

        label.text = product.productName;
        label.fontSize = 20f;
        label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        label.alignment = TextAlignmentOptions.Midline;

        return button;
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
                image.color = product == selectedProduct
                    ? new Color(1f, 0.9f, 0.45f, 1f)
                    : Color.white;
            }
        }
    }

    private void UpdatePersistenceButtonStates()
    {
        bool hasSave = GameManager.Instance != null && GameManager.Instance.HasSaveGame();
        bool hasShelfTarget = selectedShelf != null || (GameManager.Instance != null && GameManager.Instance.GetFocusedShelf() != null);

        SetButtonState(saveGameButton, true);
        SetButtonState(loadGameButton, hasSave);
        SetButtonState(newGameButton, true);
        SetButtonState(restockSelectedShelfButton, hasShelfTarget);
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
            image.color = isEnabled
                ? Color.white
                : new Color(0.78f, 0.78f, 0.78f, 0.5f);
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
            deliveryTaskText.text = "Store Task: -";
            return;
        }

        deliveryTaskText.text = "Store Task: " + GameManager.Instance.GetStoreTaskStatus();
    }

    private void UpdatePlayerModeText()
    {
        if (playerModeText == null)
        {
            return;
        }

        if (FirstPersonPlayerController.Instance == null)
        {
            playerModeText.text = "View: Prototype Camera";
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

        interactionPromptText.text = GameManager.Instance.GetPlayerInteractionPrompt();
    }

    private TextAlignmentOptions GetAlignment(VerticalAlignmentOptions verticalAlignment)
    {
        switch (verticalAlignment)
        {
            case VerticalAlignmentOptions.Top:
                return TextAlignmentOptions.TopLeft;
            case VerticalAlignmentOptions.Bottom:
                return TextAlignmentOptions.BottomLeft;
            default:
                return TextAlignmentOptions.MidlineLeft;
        }
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
